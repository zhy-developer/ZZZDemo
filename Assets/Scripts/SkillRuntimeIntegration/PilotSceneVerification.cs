#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using FrameSync.RootMotion;
using GameProtocol;
using SkillConfig;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SkillRuntime.Integration
{
    // Main-project Play Mode checks using the real Player/RoleBase prefabs. NOT a two-client test.
    public static class PilotSceneVerification
    {
        static int checks;
        static void Check(bool condition, string name)
        { if (!condition) throw new Exception("PHASE4A_FAIL " + name); checks++; Debug.Log("PHASE4A_PASS " + name); }
        static AllPlayerOperation Ops(params PlayerOperation[] ops)
        { var result = new AllPlayerOperation(); result.operations.AddRange(ops); return result; }
        static PlayerOperation Attack(int id, int serial = 1) => new PlayerOperation { battleID = id, move = 121, rightOperation = RightOpType.rop1, operationID = serial, operationValue1 = id == 1 ? 0 : 60 };
        static void Tick(PilotDevelopmentArena a, params PlayerOperation[] ops)
        { if (!a.Deliver(BattleData.Instance.NextFrameID, Ops(ops))) throw new Exception("Scene frame refused " + a.Roles.Pilot?.Fault); }
        public static IEnumerator Run(PilotDevelopmentArena arena)
        {
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            var work = Cases(arena); checks = 0;
            while (true)
            {
                bool more;
                try { more = work.MoveNext(); }
                catch (Exception ex) { Debug.LogException(ex); UnityEditor.EditorApplication.Exit(1); yield break; }
                if (!more) break;
                yield return work.Current;
            }
            Debug.Log("PHASE4A_SCENE_COMPLETE checks=" + checks + " realPlayers=true twoClients=false");
            UnityEditor.EditorApplication.Exit(0);
        }
        static PilotConfiguration Fixture(PilotDevelopmentArena arena, Action<SkillDto> edit, int hp = 100)
        {
            var c = arena.profile.ValidateAndFreeze();
            var dto = JsonUtility.FromJson<SkillConfigDto>(arena.profile.exportedJson.text);
            foreach (var s in dto.skills.Where(s => c.Skills.Any(p => p.Skill.SkillId == s.skillId))) edit(s);
            if (!SkillConfigLoader.TryParse(JsonUtility.ToJson(dto), out var snapshot, out var diagnostics)) throw new Exception(string.Join("\n", diagnostics));
            if (!RuntimeSkillCatalog.TryCreateIsolated(snapshot, 33, 10000, out var index, out var error)) throw new Exception(error);
            return new PilotConfiguration(c.Skills.Select(s => new PilotSkill(s.Source, index.Skill(s.Skill.SkillId), s.Clip, s.State, s.Motion, s.MotionEnd, s.DistancePermille,
                dto.skills.Single(d => d.skillId == s.Skill.SkillId).events)).ToArray(), c.Hurt, hp,
                SkillConfigHash.Sha256(JsonUtility.ToJson(dto) + "/testHP=" + hp), c.Idle, c.Hit, c.Death, c.Container);
        }
        static void Wide(SkillDto s)
        {
            var box = s.hitBoxes[0]; box.startFrame = 0; box.endFrame = s.durationFrames;
            box.shape = ShapeKind.Sphere; box.offset = default(Int3); box.radius = 100000;
            s.hitBoxes.Add(new HitBox { id = "scene-test-second-box", trackId = box.trackId, hitGroupId = box.hitGroupId,
                startFrame = 0, endFrame = s.durationFrames, shape = ShapeKind.Sphere, radius = 100000, height = 200000 });
        }
        static void Window(SkillDto s, bool invincible)
        {
            string id = invincible ? "scene-inv" : "scene-interrupt";
            s.tracks.Add(new SkillTrack { id = id + "-track", kind = invincible ? TrackKind.Invincibility : TrackKind.Interrupt });
            (invincible ? s.invincibility : s.interrupts).Add(new FrameWindow { id = id, trackId = id + "-track", startFrame = 0, endFrame = s.durationFrames });
        }
        static IEnumerator Cases(PilotDevelopmentArena a)
        {
            yield return null;
            Check(a.Ready && a.Players.All(p => p.Player && p.Player.IsDeterministicPilot), "two real prefab Players bound");
            Check(!SkillConfigLoader.TryLoad(a.profile.catalog.characterId, out _, out _, true), "formal BattleReady guard preserved");
            var original = a.Configuration;
            // Batch mode has no focused Game View. Inject through the real InputSystem/action bindings,
            // then invoke the real Player render-input update before another editor input update clears it.
            Application.runInBackground = true;
            var inputSettings = InputSystem.settings;
            var testSettings = UnityEngine.Object.Instantiate(inputSettings);
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testSettings;
            var input = CharacterInputSystem.Instance.inputActions;
            input.Player.Enable(); input.Player.SwitchCharacter.Disable();
            var mouse = InputSystem.AddDevice<Mouse>("PilotVerificationMouse");
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            InputSystem.Update(); a.Players[0].Player.SendMessage("Update");
            Debug.Log("[PilotInputProbe] enabled=" + input.Player.L_AtK.enabled + " pressed=" + input.Player.L_AtK.WasPressedThisFrame()
                + " local=" + a.Players[0].Player.IsLocalPlayer + " active=" + a.Players[0].Player.isActiveAndEnabled + " mouse=" + mouse.leftButton.isPressed);
            Check(BattleData.Instance.PendingActionCount > 0, "real InputSystem mouse -> PlayerNetworkInput -> reliable queue");
            Check(a.Roles.Pilot.Cast(1) == null, "local input alone does not create CastKey");
            var actualInput = PilotDevelopmentArena.Copy(BattleData.Instance.selfOperation);
            Tick(a, actualInput);
            Check(a.Roles.Pilot.Cast(1)?.Skill.SkillId == "miyabi-normal-1", "echoed real input accepted as Normal1");
            InputSystem.QueueStateEvent(mouse, new MouseState()); InputSystem.Update();
            InputSystem.RemoveDevice(mouse); InputSystem.settings = inputSettings; UnityEngine.Object.Destroy(testSettings); yield return null;

            a.Restart(BattleAuthorityMode.DeterministicPilot); yield return null;
            var reference = new RootMotionPlayback(); var s1 = original.Skills[0];
            Check(reference.TryStart(s1.Motion, s1.MotionEnd, s1.DistancePermille, 100, 0, 100), "reference old motion starts");
            var start = a.Players[0].LogicalPosition; int x = start.x, z = start.y;
            for (int k = 0; k < s1.Skill.Duration; k++)
            {
                Tick(a, k == 0 ? new[] { Attack(1) } : new PlayerOperation[0]);
                if (reference.TryAdvance(out int dx, out int dz)) { x += dx; z += dz; }
                Check(a.Players[0].LogicalPosition.x == x && a.Players[0].LogicalPosition.y == z, "single old motion sample k=" + k);
            }
            Check(a.Roles.Pilot.Cast(1) == null && !a.Players[0].IsPlayingRootMotion, "D-1 applied before normal exit");
            Check(a.Roles.Pilot.Health(2).ReadPilotHealth() < original.HP, "real authored hit changes unique HP");
            Check(a.Roles.Pilot.Health(2).DisplayHP == a.Roles.Pilot.Health(2).ReadPilotHealth(), "display reads authority HP");

            a.Restart(BattleAuthorityMode.DeterministicPilot); yield return null;
            a.Players[1].objShape.SetPosition(new GameVector2(180000, 180000));
            for (int k = 0; k < s1.Skill.Duration; k++) Tick(a, k == 0 ? new[] { Attack(1) } : new PlayerOperation[0]);
            Check(a.Roles.Pilot.Health(2).ReadPilotHealth() == original.HP, "real distant target takes no damage");

            a.Restart(BattleAuthorityMode.DeterministicPilot); yield return null;
            Tick(a, Attack(1)); var first = a.Roles.Pilot.Cast(1).Key;
            for (int k = 1; k < s1.Ready; k++) Tick(a);
            var normal2Origin = a.Players[0].LogicalPosition;
            var normal2Reference = new RootMotionPlayback(); var s2 = original.Skills[1];
            normal2Reference.TryStart(s2.Motion, s2.MotionEnd, s2.DistancePermille, 100, 0, 100);
            int normal2X = normal2Origin.x, normal2Z = normal2Origin.y;
            Tick(a, Attack(1, 2));
            normal2Reference.TryAdvance(out int n2dx, out int n2dz); normal2X += n2dx; normal2Z += n2dz;
            Check(a.Players[0].LogicalPosition.x == normal2X && a.Players[0].LogicalPosition.y == normal2Z, "Normal2 first sample on accepted frame");
            Check(a.Roles.Pilot.Cast(1).Skill.SkillId == "miyabi-normal-2" && !a.Roles.Pilot.Cast(1).Key.Equals(first), "logical gate Normal1 -> Normal2 creates new cast");
            a.Players[0].PilotStopMotion(first);
            Check(a.Players[0].IsPlayingRootMotion, "old Cast stop cannot stop new motion");
            var second = a.Roles.Pilot.Cast(1).Key;
            long beforeLateEventHP = a.Roles.Pilot.Health(2).ReadPilotHealth();
            a.Players[0].Player.EnablePreInput(); a.Players[0].Player.CancelAttackColdTime(); a.Players[0].Player.DisableLinkCombo(); a.Players[0].Player.EnableMoveInterrupt(); a.Players[0].Player.ATK();
            a.Players[0].Player.OnAnimationExitEvent();
            GameEventsManager.Instance.CallEvent<float>("生成伤害", 999);
            GameEventsManager.Instance.CallEvent<float, string, string, Transform, Transform, ZZZ.CharacterComboBase>("触发伤害", 999, "", "", a.Players[0].Player.transform, a.Players[1].Player.transform, null);
            Check(a.Roles.Pilot.Cast(1).Key.Equals(second) && a.Roles.Pilot.Health(2).ReadPilotHealth() == beforeLateEventHP, "late old attack/gate callbacks are inert");
            for (int k = 1; k < original.Skills[1].Ready; k++)
            {
                Tick(a); if (normal2Reference.TryAdvance(out n2dx, out n2dz)) { normal2X += n2dx; normal2Z += n2dz; }
                Check(a.Players[0].LogicalPosition.x == normal2X && a.Players[0].LogicalPosition.y == normal2Z, "Normal2 single sample k=" + k);
            }
            Tick(a, Attack(1, 3));
            Check(a.Roles.Pilot.Cast(1).Key.Equals(second), "unmapped Normal3 rejected without old fallback");
            if (normal2Reference.TryAdvance(out n2dx, out n2dz)) { normal2X += n2dx; normal2Z += n2dz; }
            for (int k = s2.Ready + 1; k < s2.Skill.Duration; k++)
            { Tick(a); if (normal2Reference.TryAdvance(out n2dx, out n2dz)) { normal2X += n2dx; normal2Z += n2dz; } }
            Check(a.Players[0].LogicalPosition.x == normal2X && a.Players[0].LogicalPosition.y == normal2Z && a.Roles.Pilot.Cast(1) == null, "Normal2 final sample and D-1 exit");
            var shortMotion = original.Skills.Select(s => new PilotSkill(s.Source, s.Skill, s.Clip, s.State, s.Motion, 3, s.DistancePermille, s.Source.skill.events)).ToArray();
            a.UseTestConfiguration(new PilotConfiguration(shortMotion, original.Hurt, original.HP, "explicit-test-short-motion", original.Idle, original.Hit, original.Death, original.Container)); yield return null;
            Tick(a, Attack(1)); Tick(a); Tick(a); var tail = a.Players[0].LogicalPosition;
            for (int k = 3; k < 20; k++) Tick(a);
            Check(a.Players[0].LogicalPosition.x == tail.x && a.Players[0].LogicalPosition.y == tail.y, "short motion coverage holds through remaining cast tail");
            a.UseTestConfiguration(original); yield return null;
            var unsupported = Attack(1); unsupported.rightOperation = RightOpType.rop2; Tick(a, unsupported);
            Check(a.Roles.Pilot.Cast(1) == null && !a.Players[0].IsPlayingRootMotion, "unsupported skill input cannot invoke old chain");

            a.UseTestConfiguration(Fixture(a, s => { Wide(s); s.hitGroups[0].hitStunFrames = 3; s.hitGroups[0].knockback = new Int3 { z = 999 }; s.hitGroups[0].knockbackFrames = 3; })); yield return null;
            Tick(a, Attack(1), Attack(2));
            Check(a.Roles.Pilot.LastReceipt.Feedback.Count == 2 && a.Roles.Pilot.Health(1).ReadPilotHealth() == 80 && a.Roles.Pilot.Health(2).ReadPilotHealth() == 80, "same-frame mirror hits and same-group multi-box dedupe");
            Check(a.Roles.Pilot.Cast(1) != null && a.Roles.Pilot.Cast(2) != null, "normal armor keeps casts");
            Check(a.Roles.Pilot.LastReceipt.Targets.All(t => t.Stun.End == 0 && t.Knockback == null), "armor suppresses configured nonzero stun and knockback");
            var hash = a.Roles.Pilot.LastStateHash;
            Check(!a.Deliver(1, Ops(Attack(1), Attack(2))) && a.Roles.Pilot.LastStateHash == hash, "duplicate network frame no side effects");
            Check(!a.Deliver(3, Ops()) && a.Roles.Pilot.LastStateHash == hash, "missing continuous frame stalls");
            Check(a.Deliver(2, Ops()) && a.ConsumeNext() && a.Roles.Pilot.Runtime.LastCompletedFrame == 3, "empty frames progress and queued gap recovers");
            Check(a.Roles.Pilot.Health(1).ReadPilotHealth() == 80 && a.Roles.Pilot.FeedbackCount == 2, "Single does not re-damage or replay feedback");

            a.UseTestConfiguration(Fixture(a, s => { Wide(s); Window(s, true); })); yield return null;
            Tick(a, Attack(1), Attack(2));
            Check(a.Roles.Pilot.Health(1).ReadPilotHealth() == 100 && a.Roles.Pilot.Health(2).ReadPilotHealth() == 100
                && a.Roles.Pilot.Runtime.RecordCount == 0 && a.Roles.Pilot.FeedbackCount == 0, "invincibility blocks damage feedback and ledger");

            a.UseTestConfiguration(Fixture(a, s => { Wide(s); Window(s, false); })); yield return null;
            Tick(a, Attack(1), Attack(2)); var pos = a.Players.Select(p => p.LogicalPosition).ToArray();
            Check(a.Roles.Pilot.Cast(1) == null && a.Roles.Pilot.Cast(2) == null && a.Roles.Pilot.LastReceipt.Feedback.Count == 2, "zero-stun hits interrupt both after mutual settlement");
            Tick(a);
            Check(a.Players.Select((p, i) => p.LogicalPosition.x == pos[i].x && p.LogicalPosition.y == pos[i].y).All(v => v), "interrupt preserves current motion and stops future samples");

            a.UseTestConfiguration(Fixture(a, s => { Wide(s); var g = s.hitGroups[0]; g.hitStunFrames = 3; g.knockback = new Int3 { z = 999 }; g.knockbackFrames = 3; })); yield return null;
            Tick(a, Attack(1)); var plan = a.Roles.Pilot.LastReceipt.Targets.Single(r => r.Target.BattleId == 2).Knockback;
            Check(plan != null, "idle real target receives knockback plan"); var before = a.Players[1].LogicalPosition;
            for (int f = 2; f <= 4; f++) Tick(a, Attack(2, f));
            var after = a.Players[1].LogicalPosition;
            Check(after.x - before.x == plan.WorldTotal.X && after.y - before.y == plan.WorldTotal.Z && a.Roles.Pilot.Cast(2) == null, "knockback applied once and stun/knockback block attacks");
            a.Restart(BattleAuthorityMode.DeterministicPilot); yield return null;
            a.Players[0].objShape.SetPosition(new GameVector2(174000, 0)); a.Players[1].objShape.SetPosition(new GameVector2(199900, 0));
            Tick(a, Attack(1)); Tick(a); Tick(a); Tick(a);
            Check(a.Players[1].LogicalPosition.x == BattleData.Instance.mapMaxX, "knockback obeys existing map boundary");

            a.UseTestConfiguration(Fixture(a, s => { Wide(s); Window(s, false); }, 10)); yield return null;
            var info = (CharacterHealthInfo)typeof(CharacterHealthBase).GetField("healthInfo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(a.Roles.Pilot.Health(1));
            bool atomicNotification = false;
            info.currentHP.OnValueChanged += hp => { if (hp == 0) atomicNotification = a.Roles.Pilot.Health(2).ReadPilotHealth() == 0; };
            Tick(a, Attack(1), Attack(2));
            Check(atomicNotification, "both authority HP writes precede first bindable notification");
            Check(a.Roles.Pilot.TerminalResult == RoundResult.Draw && a.Roles.Pilot.Health(1).IsDead && a.Roles.Pilot.Health(2).IsDead, "same-frame real death retains Draw");
            Tick(a, Attack(1, 2), Attack(2, 2));
            Check(a.Roles.Pilot.Cast(1) == null && a.Roles.Pilot.Cast(2) == null && a.Roles.Pilot.TerminalResult == RoundResult.Draw, "dead input rejected and Draw retained");

            a.UseTestConfiguration(Fixture(a, Wide)); yield return null;
            var host = a.Roles.Pilot; var runtime = host.Runtime;
            var keys = new[] { new EntityKey(41002, 1, 1, 1), new EntityKey(41002, 1, 2, 1) };
            runtime.TryBeginFrame(1); runtime.StartAccepted(keys[0], a.Configuration.Skills[0].Skill); runtime.StartAccepted(keys[1], a.Configuration.Skills[0].Skill);
            var geometry = a.Players.Select((p, i) => new EntityGeometrySnapshot(keys[i], new LogicPose(p.LogicalPosition.x, 0, p.LogicalPosition.y, p.LogicalFacing * 1000), a.Configuration.Hurt)).ToArray();
            runtime.CapturePostMove(geometry);
            var baseline = new CombatFrameSnapshot(1, 0, geometry.Select(g => new CombatantSnapshot(g, runtime.Current(g.Entity).Key, 100, default(StunInterval), null)));
            typeof(PilotBattleSession).GetField("baseline", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(host, baseline);
            var proposal = CombatResolver.Resolve(runtime, baseline);
            host.Health(2).WritePilotHealth(99); // Deliberately invalidate SECOND target after Resolve, before Apply.
            Check(!proposal.TryCommit(host, out _, out _) && host.Health(1).ReadPilotHealth() == 100 && host.Health(2).ReadPilotHealth() == 99
                && runtime.RecordCount == 0 && host.FeedbackCount == 0, "real adapter rejects second baseline without partial first HP ledger or feedback");

            a.UseTestConfiguration(original); yield return null;
            Tick(a, Attack(1)); var state = a.Roles.Pilot.LastStateHash;
            a.UseTestConfiguration(original); yield return null;
            Tick(a, Attack(1)); Check(a.Roles.Pilot.LastStateHash == state, "fresh real scene replay stable state hash");
            var invalid = UnityEngine.Object.Instantiate(a.profile); invalid.sealedJsonHash = "invalid";
            bool rejected = false; try { invalid.ValidateAndFreeze(); } catch (InvalidOperationException) { rejected = true; }
            UnityEngine.Object.Destroy(invalid); Check(rejected, "inconsistent configuration refuses start");
            var ownedSession = a.Roles.Pilot;
            UnityEngine.Object.Destroy(a.Players[1].gameObject); yield return null;
            Check(ownedSession.Runtime.IsDisposed && !a.Players[0].IsPlayingRootMotion, "entity destruction disposes cast ownership and motion");
            a.Restart(BattleAuthorityMode.Legacy); yield return null;
            Check(a.Roles.Pilot == null && a.Players.All(p => !p.Player.IsDeterministicPilot), "fresh Legacy restores old authority mode");
            Tick(a, Attack(1));
            Check(a.Players[0].IsPlayingRootMotion, "Legacy real network attack still starts old root motion");
        }
    }
}
#endif
