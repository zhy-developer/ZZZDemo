using System;

namespace ZZZ.SkillAuthoring
{
    /// <summary>攻击区域的纯整数命中时序；战斗、时间轴和 Scene 提示共用，不保存命中状态。</summary>
    public static class SkillHitTiming
    {
        public static bool IsHitFrame(SkillHitRegion region, int frame) => TryGetHitFrame(region, frame, out _);

        public static bool TryGetHitFrame(SkillHitRegion region, int frame, out int hitFrame)
        {
            hitFrame = -1;
            if (region == null || !region.IsActive(frame)) return false;
            switch (region.hitMode)
            {
                case SkillHitMode.Once:
                    // 区间内每帧尝试，但共享批次 -1，沿用首次碰到才结算的行为。
                    return true;
                case SkillHitMode.Frames:
                    if (region.hitFrames == null || Array.IndexOf(region.hitFrames, frame) < 0) return false;
                    break;
                case SkillHitMode.Interval:
                    if (region.hitIntervalFrames < 1 || ((long)frame - region.startFrame) % region.hitIntervalFrames != 0) return false;
                    break;
                default:
                    return false;
            }
            // 同组多个多段区域按绝对帧共享去重；不使用各列表下标，避免不同起点造成错配。
            hitFrame = frame;
            return true;
        }
    }
}
