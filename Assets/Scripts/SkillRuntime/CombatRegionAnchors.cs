using System;
using UnityEngine;
using ZZZ.SkillAuthoring;

namespace ZZZ.SkillRuntime
{
    /// <summary>单次释放的区域定位快照。只保存逻辑坐标；碰撞和 Gizmos 共用，不读取表现 Transform。</summary>
    internal sealed class CombatRegionAnchors
    {
        private readonly struct Pose
        {
            public readonly Vector2Int Position;
            public readonly int Facing;
            public Pose(Vector2Int position, int facing) { Position = position; Facing = facing; }
        }

        private Pose castPose;
        private Pose?[] activationPoses = Array.Empty<Pose?>();

        public void Clear()
        {
            castPose = default;
            activationPoses = Array.Empty<Pose?>();
        }

        // 在全员移动之后、收集命中之前调用。新段 F0 已消费上一段遗留的边界位移，
        // 以实际 F0 逻辑位置作为释放基准，包含地图边界裁剪，不由动画累计位移倒推。
        public void Capture(CombatFighter fighter)
        {
            if (fighter.BlockedThisFrame || fighter.State != CombatState.Skill || fighter.Skill == null) return;
            var regions = fighter.Skill.Data.hitRegions;
            if (fighter.StartedSkillThisFrame)
            {
                castPose = new Pose(fighter.Position, fighter.Facing);
                activationPoses = new Pose?[regions?.Count ?? 0];
            }
            if (regions == null) return;
            for (int i = 0; i < regions.Count; i++)
            {
                var region = regions[i];
                // 与命中频率无关：指定帧模式可能在 Start Frame 不检测，仍须当帧锁定。
                if (region != null && region.enabled && region.anchorMode == SkillRegionAnchorMode.ActivationPosition
                    && fighter.SkillFrame == region.startFrame && !activationPoses[i].HasValue)
                    activationPoses[i] = new Pose(fighter.Position, fighter.Facing);
            }
        }

        public bool TryGet(CombatFighter fighter, int regionIndex, out Vector2Int position, out int facing)
        {
            position = fighter.Position; facing = fighter.Facing;
            var region = fighter.Skill.Data.hitRegions[regionIndex];
            switch (region.anchorMode)
            {
                case SkillRegionAnchorMode.FollowCharacter: return true;
                case SkillRegionAnchorMode.CastPosition:
                    position = castPose.Position; facing = castPose.Facing; return true;
                case SkillRegionAnchorMode.ActivationPosition:
                    if (regionIndex >= activationPoses.Length || !activationPoses[regionIndex].HasValue) return false;
                    var pose = activationPoses[regionIndex].Value;
                    position = pose.Position; facing = pose.Facing; return true;
                default: return false;
            }
        }

        // 固定坐标会影响未来命中，必须进入逻辑摘要；按资产区域索引遍历，避免字典顺序差异。
        public void AppendState(Action<int> add)
        {
            add(castPose.Position.x); add(castPose.Position.y); add(castPose.Facing);
            add(activationPoses.Length);
            foreach (var pose in activationPoses)
            {
                add(pose.HasValue ? 1 : 0);
                if (!pose.HasValue) continue;
                add(pose.Value.Position.x); add(pose.Value.Position.y); add(pose.Value.Facing);
            }
        }
    }
}
