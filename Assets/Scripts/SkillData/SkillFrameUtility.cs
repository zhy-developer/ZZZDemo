using System;

namespace ZZZ.SkillAuthoring
{
    /// <summary>技能时间与量化的唯一入口。帧间隔、坐标精度复用当前帧同步配置。</summary>
    public static class SkillFrameUtility
    {
        public static int IntervalMs => NetConfig.frameTime;
        public static int PositionPrecision => ToolMethod.Render2LogicScale;
        public static double SecondsPerFrame => IntervalMs / 1000.0;
        public const int MaxAuthoringFrames = 10000;

        public static double FrameSeconds(int frame) => frame * SecondsPerFrame;

        /// <summary>迁移事件向后吸附，确保不会比原事件更早触发。边界误差仅容忍一微秒。</summary>
        public static int SecondsToFrame(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            return checked((int)Math.Ceiling(Math.Max(0, seconds - 0.000001) / SecondsPerFrame));
        }
    }
}
