using System;
using System.Globalization;

namespace SkillRuntime
{
    public readonly struct EntityKey : IEquatable<EntityKey>, IComparable<EntityKey>
    {
        public readonly long Session;
        public readonly int Round, BattleId, Generation;
        public EntityKey(long session, int round, int battleId, int generation)
        {
            if (session <= 0 || round <= 0 || battleId <= 0 || generation <= 0) throw new ArgumentOutOfRangeException(nameof(session), "Identity components must be positive.");
            Session = session; Round = round; BattleId = battleId; Generation = generation;
        }
        public int CompareTo(EntityKey b)
        {
            int c = Session.CompareTo(b.Session); if (c != 0) return c;
            c = Round.CompareTo(b.Round); if (c != 0) return c;
            c = BattleId.CompareTo(b.BattleId); return c != 0 ? c : Generation.CompareTo(b.Generation);
        }
        public bool Equals(EntityKey b) => CompareTo(b) == 0;
        public override bool Equals(object obj) => obj is EntityKey key && Equals(key);
        public override int GetHashCode() { unchecked { return (((Session.GetHashCode() * 397) ^ Round) * 397 ^ BattleId) * 397 ^ Generation; } }
        public override string ToString() => Session.ToString(CultureInfo.InvariantCulture) + "/" + Round.ToString(CultureInfo.InvariantCulture) + "/" + BattleId.ToString(CultureInfo.InvariantCulture) + "/" + Generation.ToString(CultureInfo.InvariantCulture);
    }
    public readonly struct CastKey : IEquatable<CastKey>, IComparable<CastKey>
    {
        public readonly EntityKey Entity;
        public readonly long Sequence;
        public CastKey(EntityKey entity, long sequence)
        {
            if (entity.Session <= 0 || sequence <= 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            Entity = entity; Sequence = sequence;
        }
        public int CompareTo(CastKey b) { int c = Entity.CompareTo(b.Entity); return c != 0 ? c : Sequence.CompareTo(b.Sequence); }
        public bool Equals(CastKey b) => CompareTo(b) == 0;
        public override bool Equals(object obj) => obj is CastKey key && Equals(key);
        public override int GetHashCode() { unchecked { return Entity.GetHashCode() * 397 ^ Sequence.GetHashCode(); } }
        public override string ToString() => Entity + ":" + Sequence.ToString(CultureInfo.InvariantCulture);
    }
    public enum CastExitReason { Completed, Replaced, Interrupted, Death, Destroyed }
    public sealed class CastFrame
    {
        public CastKey Key { get; }
        public RuntimeSkill Skill { get; }
        public long StartFrame { get; }
        public int SkillFrame { get; }
        public RuntimeSegment Segment => Skill.SegmentAt(SkillFrame);
        internal CastFrame(CastKey key, RuntimeSkill skill, long start, long frame)
        { Key = key; Skill = skill; StartFrame = start; SkillFrame = checked((int)(frame - start)); }
    }
    public sealed class EntityGeometrySnapshot
    {
        public EntityKey Entity { get; }
        public LogicPose Pose { get; }
        public HurtCircle Hurt { get; }
        public EntityGeometrySnapshot(EntityKey entity, LogicPose postMovePose, HurtCircle explicitHurt)
        { if (entity.Session <= 0) throw new ArgumentException("Invalid entity."); GeometryLimits.Dimension(explicitHurt.Radius); Entity = entity; Pose = postMovePose; Hurt = explicitHurt; }
    }
}
