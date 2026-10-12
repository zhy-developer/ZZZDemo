using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Globalization;
using FrameSync.RootMotion;
using SkillConfig;

namespace SkillRuntime
{
    // No scene objects, network singletons, clocks or presentation callbacks enter this model.
    public readonly struct BattleInput
    {
        public readonly int Slot, Move, Action, Serial, Facing, Flags;
        public BattleInput(int slot, int move = 121, int action = 0, int serial = 0, int facing = 0, int flags = 0)
        { Slot = slot; Move = move; Action = action; Serial = serial; Facing = facing; Flags = flags; }
    }
    public readonly struct BattleSpawn
    {
        public readonly int Slot, X, Z, MoveSpeed;
        public BattleSpawn(int slot, int x, int z, int moveSpeed)
        { Slot = slot; X = x; Z = z; MoveSpeed = moveSpeed; }
    }
    public readonly struct BattleBounds
    {
        public readonly int MinX, MaxX, MinZ, MaxZ;
        public BattleBounds(int minX, int maxX, int minZ, int maxZ)
        {
            if (minX > maxX || minZ > maxZ) throw new ArgumentException("Invalid battle bounds.");
            MinX = minX; MaxX = maxX; MinZ = minZ; MaxZ = maxZ;
        }
    }
    public sealed class BattleSkill
    {
        public RuntimeSkill Skill { get; }
        public SkillMotionTimeline Timeline { get; }
        public RootMotionClip Motion { get; }
        public int MotionEnd { get; }
        public int MotionScale { get; }
        public int PreInput { get; }
        public int Ready { get; }
        public int LinkEnd { get; }
        public int MoveCancel { get; }
        public BattleSkill(RuntimeSkill skill, int preInput, int ready, int linkEnd, int moveCancel,
            SkillMotionTimeline timeline = null, RootMotionClip motion = null, int motionEnd = 0, int motionScale = 1000)
        {
            Skill = skill ?? throw new ArgumentNullException(nameof(skill));
            if (preInput < 0 || preInput > ready || ready >= linkEnd || linkEnd > skill.Duration || moveCancel < 0 || moveCancel > skill.Duration)
                throw new ArgumentException("Invalid logical combo windows.");
            PreInput = preInput; Ready = ready; LinkEnd = linkEnd; MoveCancel = moveCancel;
            Timeline = timeline; Motion = motion; MotionEnd = motionEnd; MotionScale = motionScale;
        }
    }
    public sealed class BattleActionBindings
    {
        public readonly int Skill, Ultimate, DodgeForward, DodgeBack, DodgeAttack, BackDodgeAttack, DodgeCooldown;
        public BattleActionBindings(int skill, int ultimate, int forward, int back, int attack, int backAttack, int cooldown)
        { Skill = skill; Ultimate = ultimate; DodgeForward = forward; DodgeBack = back; DodgeAttack = attack; BackDodgeAttack = backAttack; DodgeCooldown = cooldown; }
        public bool IsDodge(int index) => index == DodgeForward || index == DodgeBack;
        public bool IsSkill(int index) => index == Skill || index == Ultimate;
        public bool IsDodgeAttack(int index) => index == DodgeAttack || index == BackDodgeAttack;
        internal void Validate(int count, int normalCount)
        {
            if (new[] { Skill, Ultimate, DodgeForward, DodgeBack, DodgeAttack, BackDodgeAttack }.Any(i => i < normalCount || i >= count) || DodgeCooldown <= 0)
                throw new ArgumentException("Invalid expanded action bindings.");
            if (new[] { Skill, Ultimate, DodgeForward, DodgeBack, DodgeAttack }.Distinct().Count() != 5
                || BackDodgeAttack != DodgeAttack && new[] { Skill, Ultimate, DodgeForward, DodgeBack }.Contains(BackDodgeAttack))
                throw new ArgumentException("Action roles must refer to distinct skills, except the shared dodge attack.");
        }
    }
    public sealed class BattleConfiguration
    {
        public ReadOnlyCollection<BattleSkill> Skills { get; }
        public HurtCircle Hurt { get; }
        public int HP { get; }
        public int ContainerCount { get; }
        public int NormalCount { get; }
        public BattleActionBindings Actions { get; }
        public string Digest { get; }
        public BattleConfiguration(IEnumerable<BattleSkill> skills, HurtCircle hurt, int hp, int containerCount, string digest,
            int normalCount = -1, BattleActionBindings actions = null)
        {
            var items = skills.ToArray();
            if (normalCount == -1) normalCount = items.Length;
            if (items.Length == 0 || items.Any(s => s == null) || normalCount <= 0 || normalCount > items.Length || containerCount < normalCount || hp <= 0 || hp > 16777216)
                throw new ArgumentException("Invalid battle configuration.");
            actions?.Validate(items.Length, normalCount);
            NormalCount = normalCount; Actions = actions;
            Skills = Array.AsReadOnly(items); Hurt = hurt; HP = hp; ContainerCount = containerCount; Digest = digest;
        }
    }
    public sealed class BattleActorSnapshot
    {
        public EntityKey Entity { get; }
        public int X { get; }
        public int Z { get; }
        public int Facing { get; }
        public long HP { get; }
        public int MaxHP { get; }
        public bool IsDead => HP == 0;
        public bool Moving { get; }
        public CastFrame Cast { get; }
        public StunInterval Stun { get; }
        public KnockbackPlan Knockback { get; }
        internal BattleActorSnapshot(EntityKey entity, int x, int z, int facing, long hp, int maxHP, bool moving,
            CastFrame cast, StunInterval stun, KnockbackPlan knockback)
        { Entity = entity; X = x; Z = z; Facing = facing; HP = hp; MaxHP = maxHP; Moving = moving; Cast = cast; Stun = stun; Knockback = knockback; }
    }
    // Immutable hand-off. Views can consume late, skip rendering, or disappear without writing back.
    public sealed class BattleFrame
    {
        public long Frame { get; }
        public ReadOnlyCollection<BattleActorSnapshot> Actors { get; }
        public ReadOnlyCollection<CastFrame> CapturedCasts { get; }
        public ReadOnlyCollection<CastExit> Exits { get; }
        public ReadOnlyCollection<HitFeedback> Feedback { get; }
        public RoundResult Result { get; }
        public EntityKey? Winner { get; }
        public string StateHash { get; }
        internal BattleFrame(long frame, BattleActorSnapshot[] actors, CastFrame[] casts, CastExit[] exits,
            HitFeedback[] feedback, RoundResult result, EntityKey? winner, string hash)
        {
            Frame = frame; Actors = Array.AsReadOnly(actors); CapturedCasts = Array.AsReadOnly(casts);
            Exits = Array.AsReadOnly(exits); Feedback = Array.AsReadOnly(feedback); Result = result; Winner = winner; StateHash = hash;
        }
    }
    public sealed class BattleWorld : ICombatStateAdapter, IDisposable
    {
        sealed class Actor
        {
            internal EntityKey Key;
            internal int X, Z, Facing, Speed, LastOp, Move = 121, PendingIndex = -1, PendingFacing;
            internal long HP;
            internal long NextDodgeFrame;
            internal int DodgeFollowUp = -1;
            internal StunInterval Stun;
            internal KnockbackPlan Knockback;
            internal CastKey? PendingOwner, MotionOwner;
            internal readonly RootMotionPlayback Motion = new RootMotionPlayback();
            internal SkillMotionTimeline Timeline;
            internal int Boundary, ForwardX, ForwardZ;
            internal SkillMotionPosition Previous;
        }
        readonly Actor[] actors;
        readonly BattleConfiguration config;
        readonly BattleBounds bounds;
        readonly XzDelta[] directions;
        CombatFrameSnapshot baseline;
        long revision;
        bool disposed;
        public SkillFrameRuntime Runtime { get; }
        public BattleFrame Latest { get; private set; }
        public CombatCommitReceipt LastReceipt { get; private set; }
        public RoundResult Result { get; private set; }
        public EntityKey? Winner { get; private set; }
        public bool Faulted { get; private set; }
        public string Fault { get; private set; }
        public string TerminalState { get; private set; }
        public BattleWorld(BattleConfiguration configuration, IEnumerable<BattleSpawn> spawns, BattleBounds map, IEnumerable<XzDelta> directionTable, long session)
        {
            config = configuration ?? throw new ArgumentNullException(nameof(configuration)); bounds = map;
            directions = directionTable.ToArray();
            if (directions.Length != 120 || directions.Any(d => Math.Abs(d.X) > 100 || Math.Abs(d.Z) > 100 || d.X == 0 && d.Z == 0))
                throw new ArgumentException("120 nonzero integer directions with scale 100 required.");
            var ordered = spawns.OrderBy(s => s.Slot).ToArray();
            if (ordered.Length != 2 || ordered[0].Slot != 1 || ordered[1].Slot != 2 || ordered.Any(s => s.MoveSpeed < 0 || s.X < map.MinX || s.X > map.MaxX || s.Z < map.MinZ || s.Z > map.MaxZ))
                throw new ArgumentException("Two valid spawns required.");
            actors = ordered.Select(s => new Actor { Key = new EntityKey(session, 1, s.Slot, 1), X = s.X, Z = s.Z,
                Speed = s.MoveSpeed, HP = config.HP, Facing = s.Slot == 1 ? 0 : 180 }).ToArray();
            Runtime = new SkillFrameRuntime(actors[0].Key, actors[1].Key);
            Latest = Snapshot(0, new CastFrame[0]);
        }
        bool Restricted(Actor a, long frame) => a.HP == 0 || a.Stun.Contains(frame) || a.Knockback != null && frame >= a.Knockback.StartFrame && frame < a.Knockback.EndFrame;
        BattleSkill Binding(CastFrame cast) => config.Skills.Single(s => ReferenceEquals(s.Skill, cast.Skill));
        void StopMotion(Actor a) { a.Motion.Stop(); a.Timeline = null; a.MotionOwner = null; }
        void Start(Actor a, int index, int facing)
        {
            StopMotion(a); var skill = config.Skills[index]; a.Facing = facing * 3;
            var key = Runtime.StartAccepted(a.Key, skill.Skill); Runtime.OwnHandle(key, "root/" + key);
            var direction = directions[facing]; a.MotionOwner = key;
            a.Timeline = skill.Timeline; a.Boundary = 0; a.Previous = default;
            a.ForwardX = (int)direction.X; a.ForwardZ = (int)direction.Z;
            if (a.Timeline == null && skill.Motion != null && !a.Motion.TryStart(skill.Motion, skill.MotionEnd, skill.MotionScale, a.ForwardX, a.ForwardZ, 100))
                throw new InvalidOperationException("Invalid skill motion.");
            a.PendingIndex = -1; a.PendingOwner = null;
            a.DodgeFollowUp = -1;
        }
        void Attack(Actor a, int facing)
        {
            if (Restricted(a, Runtime.OpenFrame.Value)) return;
            var cast = Runtime.Current(a.Key);
            if (cast == null) { Start(a, a.DodgeFollowUp >= 0 ? a.DodgeFollowUp : 0, facing); return; }
            var skill = Binding(cast); int k = cast.SkillFrame;
            int current = config.Skills.IndexOf(skill);
            if (config.Actions != null)
            {
                if (config.Actions.IsSkill(current))
                { if (k >= skill.MoveCancel) Start(a, 0, facing); return; }
                if (config.Actions.IsDodge(current))
                { Start(a, current == config.Actions.DodgeForward ? config.Actions.DodgeAttack : config.Actions.BackDodgeAttack, facing); return; }
            }
            if (k < skill.PreInput) return;
            int index = k < skill.LinkEnd ? (config.Actions != null && config.Actions.IsDodgeAttack(current) ? 1 : (current + 1) % config.ContainerCount) : 0;
            if (index >= config.NormalCount) return;
            if (k >= skill.Ready) Start(a, index, facing);
            else if (a.PendingIndex < 0) { a.PendingIndex = index; a.PendingFacing = facing; a.PendingOwner = cast.Key; }
        }
        void Special(Actor a, BattleInput op, long frame)
        {
            var actions = config.Actions;
            if (actions == null || Restricted(a, frame)) return;
            var cast = Runtime.Current(a.Key);
            int current = cast == null ? -1 : config.Skills.IndexOf(Binding(cast));
            if (op.Action == 4)
            {
                if (frame < a.NextDodgeFrame || actions.IsSkill(current) && cast.SkillFrame < Binding(cast).MoveCancel) return;
                bool forward = (op.Flags & 1) != 0;
                Start(a, forward ? actions.DodgeForward : actions.DodgeBack, forward ? op.Facing % 120 : a.Facing / 3);
                a.NextDodgeFrame = checked(frame + actions.DodgeCooldown);
            }
            else if ((op.Action == 2 || op.Action == 3) && !(op.Action == 3 && current == actions.Ultimate)
                && (cast == null || actions.IsDodge(current) || actions.IsSkill(current) && cast.SkillFrame >= Binding(cast).MoveCancel))
                Start(a, op.Action == 2 ? actions.Skill : actions.Ultimate, op.Facing % 120);
        }
        void Displace(Actor a, long x, long z)
        {
            a.X = (int)Math.Max(bounds.MinX, Math.Min(bounds.MaxX, checked(a.X + x)));
            a.Z = (int)Math.Max(bounds.MinZ, Math.Min(bounds.MaxZ, checked(a.Z + z)));
        }
        void Move(Actor a, long frame)
        {
            if (a.Knockback != null && frame >= a.Knockback.StartFrame && frame < a.Knockback.EndFrame)
            { var delta = a.Knockback.DeltaAt(frame); Displace(a, delta.X, delta.Z); return; }
            if (a.Timeline != null && a.Boundary < a.Timeline.Duration)
            {
                var next = a.Timeline.Position(++a.Boundary, a.ForwardX, a.ForwardZ, 100);
                Displace(a, checked(next.X - a.Previous.X), checked(next.Z - a.Previous.Z)); a.Previous = next; return;
            }
            if (a.Motion.TryAdvance(out int x, out int z)) { Displace(a, x, z); return; }
            if (a.Move < 120 && Runtime.Current(a.Key) == null && !Restricted(a, frame))
            { a.Facing = a.Move * 3; var d = directions[a.Move]; Displace(a, a.Speed * d.X, a.Speed * d.Z); }
        }
        public bool Advance(long frame, IEnumerable<BattleInput> input)
        {
            if (disposed || Faulted || Result != RoundResult.None) return false;
            try
            {
                var operations = input?.ToArray();
                if (operations == null || operations.Any(o => o.Slot < 1 || o.Slot > 2) || operations.Select(o => o.Slot).Distinct().Count() != operations.Length)
                    throw new ArgumentException("Malformed whole-frame input.");
                if (Runtime.TryBeginFrame(frame) != FrameBeginResult.Started) return false;
                foreach (var op in operations.OrderBy(o => o.Slot))
                {
                    var a = actors[op.Slot - 1]; a.Move = op.Move >= 0 && op.Move <= 120 ? op.Move % 120 : 121;
                    if (a.Move == 121) a.DodgeFollowUp = -1;
                    if (op.Action == 0 || op.Serial <= a.LastOp || op.Serial <= 0) continue;
                    a.LastOp = op.Serial;
                    if (op.Action == 1 && op.Facing >= 0 && op.Facing <= 120) Attack(a, op.Facing % 120);
                    else if (op.Facing >= 0 && op.Facing <= 120) Special(a, op, frame);
                }
                foreach (var a in actors)
                {
                    var cast = Runtime.Current(a.Key);
                    if (a.PendingIndex >= 0)
                    {
                        if (cast == null || !Nullable.Equals(a.PendingOwner, cast.Key) || Restricted(a, frame)) { a.PendingIndex = -1; a.PendingOwner = null; }
                        else if (cast.SkillFrame >= Binding(cast).Ready) Start(a, a.PendingIndex, a.PendingFacing);
                    }
                    cast = Runtime.Current(a.Key);
                    if (cast != null && a.Move < 120 && cast.SkillFrame >= Binding(cast).MoveCancel && !Restricted(a, frame))
                    { Runtime.Exit(cast.Key, CastExitReason.Interrupted); StopMotion(a); a.PendingIndex = -1; a.PendingOwner = null; }
                    Move(a, frame);
                }
                var geometry = actors.Select(a => new EntityGeometrySnapshot(a.Key, new LogicPose(a.X, 0, a.Z, a.Facing * 1000), config.Hurt)).ToArray();
                Runtime.CapturePostMove(geometry);
                var casts = actors.Select(a => Runtime.Current(a.Key)).Where(c => c != null).ToArray();
                baseline = new CombatFrameSnapshot(frame, revision, actors.Select((a, i) => new CombatantSnapshot(geometry[i], Runtime.Current(a.Key)?.Key, a.HP, a.Stun, a.Knockback)));
                var batch = CombatResolver.Resolve(Runtime, baseline);
                if (!batch.TryCommit(this, out var receipt, out var error)) throw new InvalidOperationException(error);
                LastReceipt = receipt; Result = receipt.Round; Winner = receipt.Winner;
                Runtime.CompleteFrame();
                foreach (var exit in Runtime.Exits)
                {
                    var a = actors.Single(v => v.Key.Equals(exit.Cast.Entity));
                    if (Nullable.Equals(a.MotionOwner, exit.Cast)) StopMotion(a);
                    if (Nullable.Equals(a.PendingOwner, exit.Cast)) { a.PendingIndex = -1; a.PendingOwner = null; }
                    var ended = casts.FirstOrDefault(c => c.Key.Equals(exit.Cast));
                    if (config.Actions != null && ended != null && ReferenceEquals(ended.Skill, config.Skills[config.Actions.DodgeForward].Skill)
                        && exit.Reason == CastExitReason.Completed && a.Move < 120 && Runtime.Current(a.Key) == null)
                        a.DodgeFollowUp = config.Actions.DodgeAttack;
                    if (exit.Reason == CastExitReason.Death || exit.Reason == CastExitReason.Interrupted) a.DodgeFollowUp = -1;
                }
                if (Result != RoundResult.None)
                {
                    foreach (var a in actors) { StopMotion(a); a.PendingIndex = -1; a.PendingOwner = null; a.DodgeFollowUp = -1; }
                    TerminalState = DescribeState() + "\naccepted=" + string.Join(";", Runtime.Committed.Select(c => c.ToString())) +
                        "\nexits=" + string.Join(";", Runtime.Exits.Select(e => e.Cast + ":" + e.Reason));
                }
                Latest = Snapshot(frame, casts);
                return true;
            }
            catch (Exception ex) { Faulted = true; Fault = ex.ToString(); return false; }
        }
        bool ICombatStateAdapter.TryApply(CombatFrameSnapshot expected, ReadOnlyCollection<TargetResolution> results, out string error)
        {
            error = null;
            if (!ReferenceEquals(expected, baseline) || expected.Revision != revision || Runtime.OpenFrame != expected.Frame || results.Count != actors.Length)
            { error = "Invalid frame baseline."; return false; }
            var ordered = results.OrderBy(r => r.Target).ToArray();
            long nextRevision = checked(revision + 1);
            for (int i = 0; i < actors.Length; i++)
            {
                var a = actors[i]; var e = expected.Entities[i]; var row = ordered[i];
                if (!row.Target.Equals(a.Key) || !ReferenceEquals(row.Expected, e) || a.HP != e.HP || a.X != e.Geometry.Pose.X || a.Z != e.Geometry.Pose.Z ||
                    a.Facing * 1000 != e.Geometry.Pose.FacingMilliDegrees || !Nullable.Equals(Runtime.Current(a.Key)?.Key, e.Cast) ||
                    a.Stun.Start != e.Stun.Start || a.Stun.End != e.Stun.End || !ReferenceEquals(a.Knockback, e.Knockback) || row.FinalHP < 0 || row.FinalHP > config.HP)
                { error = "Actor baseline changed."; return false; }
            }
            for (int i = 0; i < actors.Length; i++)
            { actors[i].HP = ordered[i].FinalHP; actors[i].Stun = ordered[i].Stun; actors[i].Knockback = ordered[i].Knockback; }
            revision = nextRevision; return true;
        }
        BattleFrame Snapshot(long frame, CastFrame[] casts) => new BattleFrame(frame,
            actors.Select(a => new BattleActorSnapshot(a.Key, a.X, a.Z, a.Facing, a.HP, config.HP,
                Result == RoundResult.None && a.Move < 120 && Runtime.Current(a.Key) == null && !Restricted(a, frame), Runtime.Current(a.Key), a.Stun, a.Knockback)).ToArray(),
            casts, Runtime.Exits.ToArray(), LastReceipt?.Feedback.ToArray() ?? new HitFeedback[0], Result, Winner, SkillConfigHash.Sha256(DescribeState()));
        public string DescribeState() => string.Join("\n", DescribeDiagnosticFields().Select(p => p.Key + "=" + p.Value));
        public SortedDictionary<string, string> DescribeDiagnosticFields()
        {
            var fields = new SortedDictionary<string, string>(StringComparer.Ordinal) {
                ["runtime"] = Runtime.DescribeState(), ["revision"] = revision.ToString(CultureInfo.InvariantCulture),
                ["result"] = Result + "/" + Winner, ["config"] = config.Digest,
                ["map"] = FormattableString.Invariant($"{bounds.MinX},{bounds.MaxX},{bounds.MinZ},{bounds.MaxZ}") };
            foreach (var a in actors)
            {
                string key = "entity." + a.Key.BattleId.ToString(CultureInfo.InvariantCulture) + ".";
                fields[key + "identity"] = a.Key.ToString();
                fields[key + "position"] = FormattableString.Invariant($"{a.X},{a.Z}");
                fields[key + "facing"] = a.Facing.ToString(CultureInfo.InvariantCulture);
                fields[key + "hp"] = a.HP.ToString(CultureInfo.InvariantCulture);
                fields[key + "stun"] = FormattableString.Invariant($"{a.Stun.Start},{a.Stun.End}");
                fields[key + "input"] = FormattableString.Invariant($"{a.LastOp}/{a.Move}/{a.PendingIndex}/{a.PendingFacing}/{a.PendingOwner}");
                fields[key + "dodge"] = FormattableString.Invariant($"{a.NextDodgeFrame}/{a.DodgeFollowUp}");
                fields[key + "motion"] = FormattableString.Invariant($"{a.MotionOwner};speed={a.Speed};timeline={a.Timeline != null};boundary={(a.Timeline != null ? a.Boundary : 0)};basis={(a.Timeline != null ? a.ForwardX : 0)},{(a.Timeline != null ? a.ForwardZ : 0)};previous={(a.Timeline != null ? a.Previous.X : 0)},{(a.Timeline != null ? a.Previous.Z : 0)};{a.Motion.DescribeDiagnosticState()}");
                fields[key + "knockback"] = a.Knockback?.Describe() ?? "none";
            }
            fields["directions"] = string.Join(";", directions.Select(d => FormattableString.Invariant($"{d.X},{d.Z}")));
            return fields;
        }
        public void Dispose() { if (disposed) return; disposed = true; Runtime.Dispose(); }
    }
}
