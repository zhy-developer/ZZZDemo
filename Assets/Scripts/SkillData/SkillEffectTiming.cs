using System;

namespace ZZZ.SkillAuthoring
{
    /// <summary>特效存活时间规则。制作帧仅定义生命周期；速度只作用于粒子模拟，不缩放截止时间。</summary>
    public static class SkillEffectTiming
    {
        public static bool UsesFrameRange(SkillPresentationCue cue) => cue != null
            && cue.kind == SkillPresentationKind.Vfx && cue.lifetimeMode == SkillEffectLifetimeMode.FrameRange;

        public static double LifetimeSeconds(SkillPresentationCue cue) => UsesFrameRange(cue)
            ? Math.Max(0, ((double)cue.endFrame - cue.frame) * SkillFrameUtility.SecondsPerFrame)
            : cue.maxLifetimeSeconds;

        // 容忍采样累计的浮点微差，结束帧本身不显示残留粒子。
        public static bool IsExpired(SkillPresentationCue cue, double age) => age + .0000001 >= LifetimeSeconds(cue);
    }
}
