using ZZZ.SkillAuthoring;

namespace ZZZ.SkillRuntime
{
    /// <summary>收集当前技能帧的表现边界；普通推进与结算末尾的成功动作共用，不直接播放资源。</summary>
    internal static class CombatSkillEvents
    {
        public static void Collect(CombatFighter fighter)
        {
            if (fighter.BlockedThisFrame || fighter.State != CombatState.Skill || fighter.Skill.Data.presentation == null) return;
            foreach (var cue in fighter.Skill.Data.presentation)
            {
                if (cue != null && cue.frame == fighter.SkillFrame)
                    fighter.PresentationEvents.Add(new CombatPresentationEvent(fighter.CastSerial, cue));
                if (SkillEffectTiming.UsesFrameRange(cue) && cue.endFrame == fighter.SkillFrame)
                    fighter.PresentationEvents.Add(new CombatPresentationEvent(fighter.CastSerial, cue, effectEnded: true));
            }
        }
    }
}
