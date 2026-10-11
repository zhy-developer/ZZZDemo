using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Numerics;
using SkillConfig;

namespace SkillRuntime
{
    public enum DefenseState { Vulnerable, Invincible, SuperArmor, Interruptible }
    public enum RoundResult { None, Winner, Draw }

    public readonly struct StunInterval
    {
        public readonly long Start, End;
        public StunInterval(long start, long end)
        {
            if (start < 0 || end < start) throw new ArgumentOutOfRangeException(nameof(end));
            Start = start; End = end;
        }
        public bool Contains(long frame) => Start <= frame && frame < End;
        internal StunInterval Merge(long hitFrame, int duration)
        {
            if (duration == 0) return this;
            long start = checked(hitFrame + 1), end = checked(start + duration);
            return new StunInterval(End > start ? Math.Min(Start, start) : start, Math.Max(End, end));
        }
    }

    public readonly struct XzDelta
    {
        public readonly long X, Z;
        public XzDelta(long x, long z) { X = x; Z = z; }
    }

    // Immutable TOTAL displacement in logic units, not a velocity. No Transform/RoleBase writes.
    public sealed class KnockbackPlan
    {
        public HitCandidate Source { get; }
        public long StartFrame { get; }
        public long EndFrame { get; }
        public int Duration { get; }
        public XzDelta WorldTotal { get; }
        public long LocalLengthSquared { get; }
        internal KnockbackPlan(HitCandidate source, LogicPose sourcePose)
        {
            var g = source.Group;
            if (g.KnockbackFrames <= 0) throw new ArgumentException("Nonzero knockback requires duration.");
            Source = source; Duration = g.KnockbackFrames; StartFrame = checked(source.Frame + 1); EndFrame = checked(StartFrame + Duration);
            LocalLengthSquared = checked((long)g.KnockbackX * g.KnockbackX + (long)g.KnockbackZ * g.KnockbackZ);
            var world = sourcePose.Rotation.Apply(RPoint.Of(g.KnockbackX, g.KnockbackZ));
            WorldTotal = new XzDelta(Round(world.X), Round(world.Z));
        }
        // Round only the final rotated TOTAL: nearest logic unit, exact halves away from zero.
        static long Round(Rational value)
        {
            var magnitude = (BigInteger.Abs(value.N) * 2 + value.D) / (value.D * 2);
            return checked((long)(value.N.Sign < 0 ? -magnitude : magnitude));
        }
        public XzDelta CumulativeAfter(int steps)
        {
            if (steps < 0 || steps > Duration) throw new ArgumentOutOfRangeException(nameof(steps));
            // Division truncates towards zero, including negative totals. BigInteger avoids product overflow.
            return new XzDelta((long)((BigInteger)WorldTotal.X * steps / Duration), (long)((BigInteger)WorldTotal.Z * steps / Duration));
        }
        public XzDelta DeltaAt(long frame)
        {
            if (frame < StartFrame || frame >= EndFrame) return new XzDelta(0, 0);
            int n = checked((int)(frame - StartFrame)); var a = CumulativeAfter(n); var b = CumulativeAfter(n + 1);
            return new XzDelta(b.X - a.X, b.Z - a.Z);
        }
        internal string Describe() => Source + "/" + StartFrame.ToString(CultureInfo.InvariantCulture) + "/" + Duration.ToString(CultureInfo.InvariantCulture)
            + "/" + WorldTotal.X.ToString(CultureInfo.InvariantCulture) + "/" + WorldTotal.Z.ToString(CultureInfo.InvariantCulture);
    }

    // Ephemeral inputs owned by the host, NOT a second production HP system.
    public sealed class CombatantSnapshot
    {
        public EntityGeometrySnapshot Geometry { get; }
        public EntityKey Entity => Geometry.Entity;
        public CastKey? Cast { get; }
        public long HP { get; }
        public StunInterval Stun { get; }
        public KnockbackPlan Knockback { get; }
        public CombatantSnapshot(EntityGeometrySnapshot geometry, CastKey? cast, long hp, StunInterval stun = default(StunInterval), KnockbackPlan knockback = null)
        {
            if (geometry == null || hp < 0 || (cast.HasValue && !cast.Value.Entity.Equals(geometry.Entity))) throw new ArgumentException("Invalid combat baseline.");
            Geometry = geometry; Cast = cast; HP = hp; Stun = stun; Knockback = knockback;
        }
    }
    public sealed class CombatFrameSnapshot
    {
        public long Frame { get; }
        public long Revision { get; }
        public ReadOnlyCollection<CombatantSnapshot> Entities { get; }
        public CombatFrameSnapshot(long frame, long revision, IEnumerable<CombatantSnapshot> entities)
        {
            if (frame <= 0 || revision < 0) throw new ArgumentOutOfRangeException(nameof(frame));
            var all = entities.ToArray();
            if (all.Length != 2 || all.Any(e => e == null) || all.Select(e => e.Entity).Distinct().Count() != 2) throw new ArgumentException("Two unique states required.");
            Frame = frame; Revision = revision; Entities = Array.AsReadOnly(all.OrderBy(e => e.Entity).ToArray());
        }
    }

    public sealed class TargetResolution
    {
        public CombatantSnapshot Expected { get; }
        public EntityKey Target => Expected.Entity;
        public long ExpectedOldHP => Expected.HP;
        public long FinalHP { get; }
        public long TotalDamage { get; }
        public DefenseState Defense { get; }
        public StunInterval Stun { get; }
        public KnockbackPlan Knockback { get; }
        public CastExitReason? ExitReason { get; }
        internal TargetResolution(CombatantSnapshot old, DefenseState defense, long damage, StunInterval stun, KnockbackPlan plan, CastExitReason? exit)
        {
            Expected = old; Defense = defense; TotalDamage = damage; FinalHP = damage >= old.HP ? 0 : old.HP - damage;
            Stun = FinalHP == 0 ? default(StunInterval) : stun; Knockback = FinalHP == 0 ? null : plan; ExitReason = exit;
        }
    }
    public sealed class HitFeedback
    {
        public HitCandidate Source { get; }
        public int Damage { get; }
        public string VfxResourceId { get; }
        public string SfxResourceId { get; }
        internal HitFeedback(HitCandidate hit) { Source = hit; Damage = hit.Group.Damage; VfxResourceId = hit.Group.HitVfxResourceId; SfxResourceId = hit.Group.HitSfxResourceId; }
    }

    // Apply must validate the WHOLE expected snapshot (frame, revision, identities, ownership and all state),
    // stage all new rows, then publish in one non-throwing step. false/exception MUST leave host state unchanged.
    // Called once, on the logic thread, with runtime mutation locked. Tests supply the only adapter this phase.
    // No Player/HP owner is implicitly supplied. A Phase 4 adapter must prove this same atomic contract.
    public interface ICombatStateAdapter
    {
        bool TryApply(CombatFrameSnapshot expected, ReadOnlyCollection<TargetResolution> results, out string error);
    }

    public sealed class CombatCommitReceipt
    {
        public long Frame { get; }
        public ReadOnlyCollection<TargetResolution> Targets { get; }
        public ReadOnlyCollection<HitFeedback> Feedback { get; }
        public RoundResult Round { get; }
        public EntityKey? Winner { get; }
        internal CombatCommitReceipt(CombatBatch batch)
        {
            Frame = batch.Baseline.Frame; Targets = batch.Targets;
            Feedback = Array.AsReadOnly(batch.Accepted.Select(h => new HitFeedback(h)).ToArray()); Round = batch.Round; Winner = batch.Winner;
        }
    }
    // Read-only proposal. No effects are dispatched and no ledger is consumed by Resolve.
    public sealed class CombatBatch
    {
        internal SkillFrameRuntime Owner { get; }
        internal string RuntimeBaseline { get; }
        public CombatFrameSnapshot Baseline { get; }
        public ReadOnlyCollection<HitCandidate> Accepted { get; }
        public ReadOnlyCollection<TargetResolution> Targets { get; }
        public RoundResult Round { get; }
        public EntityKey? Winner { get; }
        internal CombatBatch(SkillFrameRuntime owner, CombatFrameSnapshot baseline, HitCandidate[] hits, TargetResolution[] targets)
        {
            Owner = owner; RuntimeBaseline = owner.DescribeState(); Baseline = baseline;
            Accepted = Array.AsReadOnly(hits); Targets = Array.AsReadOnly(targets.OrderBy(t => t.Target).ToArray());
            if (!Targets.Any(t => t.ExpectedOldHP > 0 && t.FinalHP == 0)) return;
            var alive = Targets.Where(t => t.FinalHP > 0).ToArray();
            Round = alive.Length == 0 ? RoundResult.Draw : RoundResult.Winner;
            if (alive.Length == 1) Winner = alive[0].Target;
        }
        public bool TryCommit(ICombatStateAdapter adapter, out CombatCommitReceipt receipt, out string error)
            => Owner.TryCommitBatch(this, adapter, out receipt, out error);
    }

    public static class CombatResolver
    {
        public static DefenseState Defense(CastFrame cast)
        {
            if (cast == null) return DefenseState.Vulnerable;
            if (cast.SkillFrame < 0 || cast.SkillFrame >= cast.Skill.Duration) throw new ArgumentException("Defense requires an active skill frame.");
            if (cast.Skill.Type == SkillType.Ultimate || cast.Skill.Invincibility.Any(w => w.Contains(cast.SkillFrame))) return DefenseState.Invincible;
            return cast.Skill.Interrupts.Any(w => w.Contains(cast.SkillFrame)) ? DefenseState.Interruptible : DefenseState.SuperArmor;
        }
        public static CombatBatch Resolve(SkillFrameRuntime runtime, CombatFrameSnapshot baseline, IEnumerable<HitCandidate> candidateOrder = null)
        {
            if (runtime == null || baseline == null) throw new ArgumentNullException(nameof(baseline));
            runtime.ValidateCombatBaseline(baseline);
            var ordered = (candidateOrder ?? runtime.Candidates).ToArray();
            // A permutation hook for deterministic hosts/tests, never permission to omit/add candidates.
            if (ordered.Length != runtime.Candidates.Count || ordered.Distinct().Count() != ordered.Length || ordered.Any(h => !runtime.Candidates.Contains(h)))
                throw new ArgumentException("The complete captured candidate set is required.");
            var states = baseline.Entities.ToDictionary(s => s.Entity);
            var defenses = baseline.Entities.ToDictionary(s => s.Entity, s => Defense(runtime.Current(s.Entity)));
            var hits = ordered.Where(h => states[h.Attacker].HP > 0 && states[h.Target].HP > 0 && runtime.CanAccept(h) && defenses[h.Target] != DefenseState.Invincible).OrderBy(h => h).ToArray();
            var targets = new List<TargetResolution>();
            foreach (var old in baseline.Entities)
            {
                var incoming = hits.Where(h => h.Target.Equals(old.Entity)).ToArray();
                long damage = 0; foreach (var h in incoming) damage = checked(damage + h.Group.Damage);
                bool dead = damage >= old.HP;
                var cast = runtime.Current(old.Entity); var stun = old.Stun; var plan = old.Knockback;
                var defense = defenses[old.Entity];
                CastExitReason? exit = null;
                if (dead && cast != null) exit = CastExitReason.Death;
                else if (incoming.Length > 0 && defense == DefenseState.Interruptible) exit = CastExitReason.Interrupted;
                else if (cast != null && cast.SkillFrame == cast.Skill.Duration - 1) exit = CastExitReason.Completed;
                if (!dead && defense != DefenseState.SuperArmor && defense != DefenseState.Invincible)
                {
                    foreach (var h in incoming) stun = stun.Merge(baseline.Frame, h.Group.HitStunFrames);
                    // Local squared magnitude, then FULL stable candidate key; all plan parameters stay from this source.
                    var strongest = incoming.Where(h => h.Group.KnockbackX != 0 || h.Group.KnockbackZ != 0)
                        .OrderByDescending(h => checked((long)h.Group.KnockbackX * h.Group.KnockbackX + (long)h.Group.KnockbackZ * h.Group.KnockbackZ)).ThenBy(h => h).FirstOrDefault();
                    if (strongest != null) plan = new KnockbackPlan(strongest, states[strongest.Attacker].Geometry.Pose);
                }
                targets.Add(new TargetResolution(old, defense, damage, stun, plan, exit));
            }
            return new CombatBatch(runtime, baseline, hits, targets.ToArray());
        }
    }
}
