using System;

namespace ZZZ.SkillRuntime
{
    /// <summary>一次释放内的命中记录。单次模式批次为 -1，多段模式使用技能绝对帧号。</summary>
    internal readonly struct CombatHitKey : IEquatable<CombatHitKey>, IComparable<CombatHitKey>
    {
        public readonly int GroupId, TargetId, HitFrame;
        public CombatHitKey(int groupId, int targetId, int hitFrame)
        { GroupId = groupId; TargetId = targetId; HitFrame = hitFrame; }

        public bool Equals(CombatHitKey other) => GroupId == other.GroupId && TargetId == other.TargetId && HitFrame == other.HitFrame;
        public override bool Equals(object obj) => obj is CombatHitKey other && Equals(other);
        public override int GetHashCode()
        { unchecked { return (GroupId * 397 ^ TargetId) * 397 ^ HitFrame; } }

        // 状态摘要按显式整数顺序排序，不能依赖 HashSet 的遍历顺序。
        public int CompareTo(CombatHitKey other)
        {
            int result = GroupId.CompareTo(other.GroupId);
            if (result != 0) return result;
            result = TargetId.CompareTo(other.TargetId);
            return result != 0 ? result : HitFrame.CompareTo(other.HitFrame);
        }
    }
}
