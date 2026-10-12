using SkillRuntime;
using SkillConfig;
using System.Text.Json;
using FrameSync.RootMotion;
using ZZZ;

int checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var directionLines = File.ReadAllLines(Path.Combine(root, "Assets/StreamingAssets/Desktopspeed.txt"));
var byAngle = directionLines.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Split(',').Select(int.Parse).ToArray()).ToDictionary(a => a[0], a => new XzDelta(a[1], a[2]));
var directions = Enumerable.Range(0, 120).Select(i => byAngle[i * 3]).ToArray();
var bounds = new BattleBounds(-200000, 200000, -200000, 200000);

BattleSkill Skill(string id = "normal-1", int damage = 10, int duration = 8, bool invincible = false, bool interruptible = false, int stun = 0, int knockback = 0, RootMotionClip motion = null)
{
    var dto = new SkillDto { skillId = id, skillType = SkillType.Normal, durationFrames = duration, baseDamage = damage };
    dto.animations.Add(new AnimationDto { id = "animation", startFrame = 0, endFrame = duration });
    dto.hitGroups.Add(new HitGroup { id = "group", mode = HitMode.Single, damageSource = DamageSource.ComboBaseDamage,
        hitStunFrames = stun, knockback = new Int3 { z = knockback }, knockbackFrames = knockback == 0 ? 0 : 3 });
    dto.hitBoxes.Add(new HitBox { id = "box", hitGroupId = "group", startFrame = 0, endFrame = duration,
        shape = ShapeKind.Sphere, radius = 10000, height = 20000 });
    // Two overlapping boxes must still produce one hit per group/target/cast.
    dto.hitBoxes.Add(new HitBox { id = "box-2", hitGroupId = "group", startFrame = 0, endFrame = duration,
        shape = ShapeKind.Sphere, radius = 10000, height = 20000 });
    if (invincible) dto.invincibility.Add(new FrameWindow { id = "inv", startFrame = 0, endFrame = duration });
    if (interruptible) dto.interrupts.Add(new FrameWindow { id = "interrupt", startFrame = 0, endFrame = duration });
    return new BattleSkill(new RuntimeSkill("fixture", dto), 1, 3, duration, duration - 1, motion: motion);
}
BattleWorld World(BattleSkill[] skills = null, int hp = 100, int distance = 1000, int speed = 10) => new BattleWorld(
    new BattleConfiguration(skills ?? new[] { Skill() }, new HurtCircle(1000, 0, 0, 0), hp, (skills?.Length ?? 1), new string('a', 64)),
    new[] { new BattleSpawn(1, -distance, 0, speed), new BattleSpawn(2, distance, 0, speed) }, bounds, directions, 42);
BattleInput Attack(int slot, int serial = 1) => new BattleInput(slot, 121, 1, serial, slot == 1 ? 0 : 60);
void Step(BattleWorld w, long frame, params BattleInput[] inputs) { if (!w.Advance(frame, inputs)) throw new Exception(w.Fault ?? "Refused frame " + frame); }

using (var w = World())
{
    var spawn = w.Latest;
    Step(w, 1, Attack(1), Attack(2));
    Check(w.Latest.Actors.All(a => a.HP == 90) && w.Latest.Feedback.Count == 2, "same-frame attacks and group deduplication");
    Check(spawn.Actors.All(a => a.HP == 100) && spawn.Frame == 0, "prior snapshot remains immutable");
    string hash = w.Latest.StateHash;
    Check(!w.Advance(1, new[] { Attack(1) }) && !w.Advance(3, Array.Empty<BattleInput>()) && hash == w.Latest.StateHash, "duplicate/gap frames have no effects");
    Step(w, 2, Attack(1), Attack(2));
    Check(w.Latest.Actors.All(a => a.HP == 90) && w.Latest.Feedback.Count == 0, "repeated command does not restart or damage twice");
}
using (var w = World(hp: 10))
{
    Step(w, 1, Attack(1), Attack(2));
    Check(w.Result == RoundResult.Draw && w.Latest.Actors.All(a => a.IsDead), "mutual lethal hits commit a draw atomically");
    var terminal = w.Latest;
    Check(!w.Advance(2, new[] { Attack(1, 2) }) && ReferenceEquals(terminal, w.Latest), "terminal world freezes without a renderer");
}
using (var w = World(hp: 10))
{
    Step(w, 1, Attack(2));
    Check(w.Result == RoundResult.Winner && w.Winner?.BattleId == 2, "winner derives from committed health");
}
using (var w = World(new[] { Skill(invincible: true) }))
{
    Step(w, 1, Attack(1), Attack(2));
    Check(w.Latest.Actors.All(a => a.HP == 100), "invincibility follows skill frames");
}
using (var w = World(new[] { Skill(stun: 3, knockback: 999) }))
{
    Step(w, 1, Attack(1), Attack(2));
    Check(w.Latest.Actors.All(a => a.Cast != null && a.Stun.End == 0 && a.Knockback == null), "super armor keeps casts and suppresses stun/knockback");
}
using (var w = World(new[] { Skill(interruptible: true, stun: 3, knockback: 999) }))
{
    Step(w, 1, Attack(1), Attack(2));
    Check(w.Latest.Actors.All(a => a.Cast == null), "both interrupted after simultaneous settlement");
    var before = w.Latest.Actors[1]; var plan = before.Knockback;
    for (int i = 2; i <= 4; i++) Step(w, i, Attack(2, i));
    Check(w.Latest.Actors[1].X - before.X == plan.WorldTotal.X && w.Latest.Actors[1].Z - before.Z == plan.WorldTotal.Z
        && w.Latest.Actors[1].Cast == null, "knockback advances once and rejects attacks while restricted");
}
using (var w = World(new[] { Skill("normal-1"), Skill("normal-2") }))
{
    Step(w, 1, Attack(1)); var first = w.Latest.Actors[0].Cast.Key;
    Step(w, 2, Attack(1, 2)); Step(w, 3); Step(w, 4);
    Check(w.Latest.Actors[0].Cast.Skill.SkillId == "normal-2" && !w.Latest.Actors[0].Cast.Key.Equals(first), "preinput switches combo on logical Ready frame");
    Check(w.Latest.Actors[1].HP == 80, "new cast can damage the same target once");
}
using (var w = World(distance: 100000))
{
    Step(w, 1, Attack(1));
    for (int f = 2; f <= 7; f++) Step(w, f, new BattleInput(1, 0));
    Check(w.Latest.Actors[0].X == -100000, "ordinary movement cannot move an active uncancellable cast");
    Step(w, 8, new BattleInput(1, 0));
    Check(w.Latest.Actors[0].Cast == null && w.Latest.Actors[0].X == -99000, "movement cancellation starts displacement at configured frame");
}
using (var w = World(distance: 199999, speed: 10000))
{
    Step(w, 1, new BattleInput(1, 60), new BattleInput(2, 0));
    Check(w.Latest.Actors[0].X == bounds.MinX && w.Latest.Actors[1].X == bounds.MaxX, "world owns map clamping");
}

// Compile and exercise the real presentation FSMs against a recording view, not a Unity substitute in the logic.
var player = new Player(); var movement = new PlayerMovementStateMachine(player); var combo = new PlayerComboStateMachine(player);
void Render(PlayerRenderFrame f) { movement.Apply(f); combo.Apply(f); }
Render(new PlayerRenderFrame { IdleState = "Idle", Moving = true });
Check(player.Moving && movement.currentState.Value is PlayerRunningState, "old movement FSM consumes visual movement snapshot");
Render(new PlayerRenderFrame { Action = PlayerRenderAction.Attack, Animation = "Attack", NormalizedTime = .4f });
Check(player.Pose == "Attack" && player.Time == .4f && combo.currentState.Value is PlayerATKIngState, "old attack state plays committed pose only");
combo.OnAnimationExitEvent(); combo.OnAnimationTranslateEvent(new PlayerNullState(combo));
Check(combo.currentState.Value is PlayerATKIngState, "late animation callbacks cannot end an authoritative action");
Render(new PlayerRenderFrame { Action = PlayerRenderAction.Hit, Animation = "Hit" });
Check(combo.currentState.Value is PlayerHitState, "hit visual comes from snapshot");
Render(new PlayerRenderFrame { Action = PlayerRenderAction.Dead, Animation = "Dead" });
Check(combo.currentState.Value is PlayerDeadState, "death visual comes from snapshot");
Render(new PlayerRenderFrame { IdleState = "Idle" });
Check(combo.currentState.Value is PlayerNullState && !player.Moving, "locomotion resumes from next committed state");

// Load real exported authoring data and motion with no Unity runtime, prefab, Animator or service singleton.
var options = new JsonSerializerOptions { IncludeFields = true };
var jsonPath = Directory.GetFiles(Path.Combine(root, "Assets/Resources/SkillConfigs"), "*.json").First();
var data = JsonSerializer.Deserialize<SkillConfigDto>(File.ReadAllText(jsonPath), options);
Check(RuntimeSkillCatalog.TryCreateIsolated(data, 33, 10000, out var catalog, out var error), "real exported catalog validates headlessly: " + error);
// A valid DTO alone cannot catch Resources paths left behind by an asset move.
var resourceFiles = Directory.EnumerateFiles(Path.Combine(root, "Assets"), "*", SearchOption.AllDirectories)
    .Select(p => p.Replace('\\', '/')).Where(p => p.Contains("/Resources/") && !p.EndsWith(".meta"))
    .GroupBy(p => Path.ChangeExtension(p.Substring(p.LastIndexOf("/Resources/", StringComparison.Ordinal) + 11), null), StringComparer.OrdinalIgnoreCase)
    .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);
foreach (var resource in data.resources)
{
    Check(resourceFiles.TryGetValue(resource.path, out var files) && files.Length == 1
        && (resource.kind != ResourceKind.VfxPrefab || files[0].EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)),
        "exported resource resolves uniquely on disk: " + resource.id + " -> " + resource.path);
}
var profileText = File.ReadAllText(Path.Combine(root, "Assets/ScriptableObject/SkillConfigs/Miyabi_NormalGameplay.asset"));
var sealedHash = System.Text.RegularExpressions.Regex.Match(profileText, @"(?m)^  sealedJsonHash: ([a-f0-9]{64})\r?$").Groups[1].Value;
Check(sealedHash == SkillConfigHash.Sha256(File.ReadAllText(jsonPath)), "gameplay profile seal matches shipped JSON bytes");
var authored = data.skills.Where(s => Enumerable.Range(1, 5).Select(i => "miyabi-normal-" + i).Contains(s.skillId)).OrderBy(s => s.skillId).Select(s =>
{
    int Gate(string id) => s.events.Single(e => e.eventId == id).frame;
    var timeline = new SkillMotionTimeline(s.animations.Select(a => new SkillMotionSegment(a, a.rootMotionBound ?
        JsonSerializer.Deserialize<RootMotionJsonData>(File.ReadAllText(Path.Combine(root, "Assets/Resources", a.rootMotion.resourcesPath + ".json")), options) : null)));
    return new BattleSkill(catalog.Skill(s.skillId), Gate("combo.preinput"), Gate("combo.attack-ready"),
        s.events.Any(e => e.eventId == "combo.link") ? Gate("combo.link") : s.durationFrames, Gate("combo.move-interrupt"), timeline);
}).ToArray();
Check(authored.Length == 5, "five authored attacks compile without render resources");
using (var noView = World(authored, distance: 30000))
using (var rendered = World(authored, distance: 30000))
{
    int delivered = 0;
    for (int f = 1; f <= 250 && noView.Result == RoundResult.None; f++)
    {
        var ops = f % 20 == 1 ? new[] { Attack(1, f), Attack(2, f) } : new[] { new BattleInput(1, f % 80 < 40 ? 0 : 121), new BattleInput(2, f % 80 < 40 ? 60 : 121) };
        Step(noView, f, ops); Step(rendered, f, ops.Reverse().ToArray());
        // Independent render cadence, dropped frames, repeated callbacks and a failed renderer.
        if (f % 3 == 0)
        {
            foreach (var actor in rendered.Latest.Actors)
                Render(new PlayerRenderFrame { Action = actor.IsDead ? PlayerRenderAction.Dead : actor.Cast != null ? PlayerRenderAction.Attack : PlayerRenderAction.None,
                    Animation = actor.Cast?.Skill.SkillId, Moving = actor.Moving, IdleState = "Idle" });
            combo.OnAnimationExitEvent(); delivered++;
        }
        if (f == 12)
        {
            player.ThrowOnPose = true;
            try { Render(new PlayerRenderFrame { Action = PlayerRenderAction.Hit, Animation = "Missing view" }); }
            catch (InvalidOperationException) { }
            finally { player.ThrowOnPose = false; }
        }
        if (noView.Latest.StateHash != rendered.Latest.StateHash) throw new Exception("Render/input ordering changed frame " + f);
    }
    Check(delivered > 0 && noView.Latest.StateHash == rendered.Latest.StateHash, "real motion/combat replay identical with absent, delayed or failing presentation");
}
Check(!typeof(BattleWorld).Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("Unity")), "logic + FSM checks have zero Unity assembly references");
var networkConfig = new BattleConfiguration(new[] { Skill() }, new HurtCircle(1000, 0, 0, 0), 10, 1, new string('a', 64));
var networkSpawns = new[] { new BattleSpawn(1, -1000, 0, 10), new BattleSpawn(2, 1000, 0, 10) };
var match = "11111111111111111111111111111111";
using (var clientA = new SkillRuntime.Integration.PilotBattleSession(networkConfig, networkSpawns, bounds, directions, 99, match, "battle-world-v2"))
using (var clientB = new SkillRuntime.Integration.PilotBattleSession(networkConfig, networkSpawns, bounds, directions, 99, match, "battle-world-v2"))
{
    var input = new GameProtocol.AllPlayerOperation();
    input.operations.Add(new GameProtocol.PlayerOperation { battleID = 1, move = 121, rightOperation = GameProtocol.RightOpType.rop1, operationID = 1 });
    input.operations.Add(new GameProtocol.PlayerOperation { battleID = 2, move = 121, rightOperation = GameProtocol.RightOpType.rop1, operationID = 1, operationValue1 = 60 });
    Check(clientA.Advance(1, input) && clientB.Advance(1, input) && clientA.TerminalSnapshot.Equals(clientB.TerminalSnapshot), "network adapters produce identical terminal result without Unity");
    var agreement = new MatchLifecycle.RoundAgreement(match, 99);
    agreement.Ready(1, 1, 1, "battle-world-v2", networkConfig.Digest); agreement.Ready(2, 1, 1, "battle-world-v2", networkConfig.Digest);
    agreement.Report(1, clientA.TerminalSnapshot, 1);
    Check(agreement.Report(2, clientB.TerminalSnapshot, 1) == MatchLifecycle.AgreementStatus.Confirmed && agreement.ConfirmationCount == 1,
        "new simulation remains compatible with two-client result agreement");
    var confirmation = MatchLifecycle.ResultWire.Read(MatchLifecycle.ResultWire.Write(agreement.Confirmed));
    Check(confirmation.Equals(clientA.TerminalSnapshot) && !clientA.Advance(2, input), "wire result survives round trip and terminal session stays frozen");
}
// Real expanded configuration: keep the normal prefix separate from named action bindings.
string[] expandedIds = Enumerable.Range(1, 5).Select(i => "miyabi-normal-" + i)
    .Concat(new[] { "miyabi-skill", "miyabi-ultimate", "miyabi-dodge-forward", "miyabi-dodge-back", "miyabi-dodge-attack" }).ToArray();
var expandedSkills = expandedIds.Select(id =>
{
    var s = data.skills.Single(v => v.skillId == id);
    int Gate(string name, int fallback) => s.events.SingleOrDefault(e => e.eventId == name)?.frame ?? fallback;
    var timeline = new SkillMotionTimeline(s.animations.Select(a => new SkillMotionSegment(a, a.rootMotionBound ?
        JsonSerializer.Deserialize<RootMotionJsonData>(File.ReadAllText(Path.Combine(root, "Assets/Resources", a.rootMotion.resourcesPath + ".json")), options) : null)));
    return new BattleSkill(catalog.Skill(id), Gate("combo.preinput", s.durationFrames - 1), Gate("combo.attack-ready", s.durationFrames - 1),
        Gate("combo.link", s.durationFrames), Gate("combo.move-interrupt", s.durationFrames), timeline);
}).ToArray();
var expandedBindings = new BattleActionBindings(5, 6, 7, 8, 9, 9, 16);
BattleConfiguration ExpandedConfig(bool motion = true) => new BattleConfiguration(motion ? expandedSkills : expandedSkills.Select(s =>
    new BattleSkill(s.Skill, s.PreInput, s.Ready, s.LinkEnd, s.MoveCancel)).ToArray(), new HurtCircle(1000, 0, 0, 0), 1000, 6,
    "expanded-fixture", 5, expandedBindings);
BattleWorld ExpandedWorld(bool motion = true, int distance = 10000) => new BattleWorld(ExpandedConfig(motion),
    new[] { new BattleSpawn(1, -distance, 0, 10), new BattleSpawn(2, distance, 0, 10) }, bounds, directions, 50);
BattleInput Command(int action, int serial, int move = 121, int flags = 0, int facing = 0) => new BattleInput(1, move, action, serial, facing, flags);
using (var w = ExpandedWorld())
{
    Step(w, 1, Command(1, 1)); var normal = w.Latest.Actors[0].Cast.Key;
    Step(w, 2, Command(4, 2, 0, 1));
    var dodge = w.Latest.Actors[0].Cast;
    Check(dodge.Skill.SkillId == "miyabi-dodge-forward" && !normal.Equals(dodge.Key), "forward dodge cancels normal attack logically");
    Step(w, 3, Command(4, 2, 0, 1));
    Check(w.Latest.Actors[0].Cast.Key.Equals(dodge.Key), "retransmitted dodge does not restart cast or cooldown");
    Step(w, 4, Command(1, 3));
    var rush = w.Latest.Actors[0].Cast;
    Check(rush.Skill.SkillId == "miyabi-dodge-attack", "attack during dodge selects shared dodge attack");
    Step(w, 5, Command(4, 4, 0, 1));
    Check(w.Latest.Actors[0].Cast.Key.Equals(rush.Key), "dodge cooldown survives cancellation into attack");
    for (int f = 6; f < 18; f++) Step(w, f, new BattleInput(1));
    Step(w, 18, Command(4, 5, 0, 1));
    Check(w.Latest.Actors[0].Cast.Skill.SkillId == "miyabi-dodge-forward", "dodge reopens exactly after 16 logical frames");
}
using (var w = ExpandedWorld())
{
    int x = w.Latest.Actors[0].X;
    Step(w, 1, Command(4, 1, facing: 60));
    Check(w.Latest.Actors[0].Cast.Skill.SkillId == "miyabi-dodge-back" && w.Latest.Actors[0].Facing == 0,
        "no-direction dodge uses current logical facing instead of a client target");
    for (int f = 2; f <= expandedSkills[8].Skill.Duration; f++) Step(w, f, new BattleInput(1));
    Check(w.Latest.Actors[0].X < x && w.Latest.Actors[0].Cast == null, "back dodge consumes baked backward motion and completes without Animator");
}
using (var w = ExpandedWorld())
{
    Step(w, 1, Command(4, 1, 0, 1));
    for (int f = 2; f <= expandedSkills[7].Skill.Duration; f++) Step(w, f, new BattleInput(1, 0));
    Step(w, expandedSkills[7].Skill.Duration + 1, Command(1, 2, 0));
    Check(w.Latest.Actors[0].Cast.Skill.SkillId == "miyabi-dodge-attack", "continued movement after forward dodge retains dodge attack opportunity");
}
using (var w = ExpandedWorld(false))
{
    Step(w, 1, Command(4, 1)); Step(w, 2, Command(1, 2));
    for (int f = 3; f <= 2 + expandedSkills[9].PreInput; f++) Step(w, f, new BattleInput(1));
    Step(w, 3 + expandedSkills[9].PreInput, Command(1, 3));
    for (int f = 4 + expandedSkills[9].PreInput; f <= 2 + expandedSkills[9].Ready; f++) Step(w, f, new BattleInput(1));
    Check(w.Latest.Actors[0].Cast.Skill.SkillId == "miyabi-normal-2", "dodge attack links into normal step two through logical gates");
}
foreach (var action in new[] { 2, 3 })
using (var w = ExpandedWorld(false, 1000))
{
    int index = action == 2 ? 5 : 6;
    Step(w, 1, Command(action, 1)); var cast = w.Latest.Actors[0].Cast;
    Check(cast.Skill.SkillId == expandedIds[index], "E/Q command selects its explicit binding: " + expandedIds[index]);
    Step(w, 2, Command(4, 2, 0, 1)); Step(w, 3, Command(1, 3));
    Check(w.Latest.Actors[0].Cast.Key.Equals(cast.Key), "active E/Q ignores dash and normal attack cancellation");
    for (int f = 4; f <= expandedSkills[index].MoveCancel; f++) Step(w, f, new BattleInput(1));
    int points = data.skills.Single(s => s.skillId == expandedIds[index]).hitGroups.Count;
    Check(w.Latest.Actors[1].HP == 1000 - (action == 2 ? 20 : 30) * points && points == (action == 2 ? 1 : 4),
        "E/Q original hit events deal damage once each: " + expandedIds[index]);
    Step(w, expandedSkills[index].MoveCancel + 1, new BattleInput(1, 0));
    Check(w.Latest.Actors[0].Cast == null, "skill recovery movement cancel is logical");
}
using (var w = ExpandedWorld(false, 1000))
{
    Step(w, 1, Command(4, 1)); Step(w, 2, Command(1, 2));
    for (int f = 3; f <= 15; f++) Step(w, f, new BattleInput(1));
    Check(w.Latest.Actors[1].HP == 960, "authored dodge attack deals two 20 HP hits");
}
using (var w = ExpandedWorld(false, 1000))
{
    Step(w, 1, Command(3, 1), new BattleInput(2, 121, 1, 1, 60));
    for (int f = 2; f < 10; f++) Step(w, f);
    Check(w.Latest.Actors[0].HP == 1000, "Q uses the existing Ultimate invincibility policy");
}
using (var a = ExpandedWorld())
using (var b = ExpandedWorld())
{
    for (int f = 1; f <= 400; f++)
    {
        int action = f % 40 == 1 ? new[] { 1, 2, 3, 4 }[(f / 40) % 4] : 0;
        var ops = new[] { new BattleInput(1, 0, action, f, 0, 1), new BattleInput(2, 60, action, f, 60, 1) };
        Step(a, f, ops); Step(b, f, ops.Reverse().ToArray());
        if (a.Latest.StateHash != b.Latest.StateHash) throw new Exception("Expanded replay differs at " + f);
    }
    Check(true, "400-frame expanded combat replay is independent of renderer and input enumeration order");
}
using (var session = new SkillRuntime.Integration.PilotBattleSession(ExpandedConfig(), networkSpawns, bounds, directions, 51, null, "expanded"))
{
    var input = new GameProtocol.AllPlayerOperation();
    input.operations.Add(new GameProtocol.PlayerOperation { battleID = 1, move = 0, rightOperation = GameProtocol.RightOpType.rop4, operationID = 1, operationValue1 = 0, operationValue2 = 1 });
    Check(session.Advance(1, input) && session.Latest.Actors[0].Cast.Skill.SkillId == "miyabi-dodge-forward", "network adapter preserves dodge direction flag");
}
Console.WriteLine("BATTLE_SEPARATION_CHECKS_COMPLETE checks=" + checks);

namespace ZZZ
{
    // Test double for the render sink; no logical methods or mutable battle state are available to FSMs.
    public sealed class Player
    {
        public string Pose; public float Time; public bool Moving, ThrowOnPose;
        internal void PlayPose(string state, float time)
        { if (ThrowOnPose) throw new InvalidOperationException("View destroyed"); Pose = state; Time = time; }
        internal void PlayAttack(PlayerRenderFrame frame) => PlayPose(frame.Animation, frame.NormalizedTime);
        internal void PlayLocomotion(string idle, bool moving) { Pose = idle; Moving = moving; }
        public void StopPilotPresentation() { Moving = false; }
    }
}
