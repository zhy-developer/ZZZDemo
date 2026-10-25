using UnityEngine;
using ZZZ.SkillAuthoring;

namespace ZZZ.SkillRuntime
{
    /// <summary>已结算命中/振刀的一次性反馈。只携带配置快照和整数事实，不读取 Transform 或创建实例。</summary>
    public readonly struct CombatHitFeedbackEvent
    {
        public readonly int Frame, AttackerId, DefenderId, CastSerial, HitGroupId, AttackerFacing;
        public readonly Vector2Int TargetPosition;
        public readonly SkillHitFeedback Feedback;
        public readonly bool IsParry;

        public CombatHitFeedbackEvent(int frame, int attackerId, int defenderId, int castSerial,
            int hitGroupId, Vector2Int targetPosition, int attackerFacing, SkillHitFeedback feedback, bool isParry = false)
        {
            Frame = frame; AttackerId = attackerId; DefenderId = defenderId;
            CastSerial = castSerial; HitGroupId = hitGroupId; TargetPosition = targetPosition;
            AttackerFacing = attackerFacing; Feedback = feedback;
            IsParry = isParry;
        }
    }
}
