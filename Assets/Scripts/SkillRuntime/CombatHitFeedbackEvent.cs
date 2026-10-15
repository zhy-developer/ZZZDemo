using UnityEngine;
using ZZZ.SkillAuthoring;

namespace ZZZ.SkillRuntime
{
    /// <summary>已结算命中的一次性通知。只携带配置快照和整数逻辑事实，不读取 Transform 或创建实例。</summary>
    public readonly struct CombatHitFeedbackEvent
    {
        public readonly int Frame, AttackerId, DefenderId, CastSerial, HitGroupId, AttackerFacing;
        public readonly Vector2Int TargetPosition;
        public readonly SkillHitFeedback Feedback;

        public CombatHitFeedbackEvent(int frame, int attackerId, int defenderId, int castSerial,
            int hitGroupId, Vector2Int targetPosition, int attackerFacing, SkillHitFeedback feedback)
        {
            Frame = frame; AttackerId = attackerId; DefenderId = defenderId;
            CastSerial = castSerial; HitGroupId = hitGroupId; TargetPosition = targetPosition;
            AttackerFacing = attackerFacing; Feedback = feedback;
        }
    }
}
