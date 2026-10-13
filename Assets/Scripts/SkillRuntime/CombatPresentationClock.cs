using System;

namespace ZZZ.SkillRuntime
{
    /// <summary>技能时间轴的连续显示时钟。受击和移动使用原速播放，不使用此校准时钟。</summary>
    public sealed class CombatPresentationClock
    {
        private double anchorSeconds, anchorRenderTime;
        public double Seconds { get; private set; }

        public void Observe(double logicalSeconds, double renderTime)
        {
            anchorSeconds = logicalSeconds;
            anchorRenderTime = renderTime;
        }

        // 仅新技能时重定位；同一技能的后续网络帧不会重置播放游标。
        public void Restart() => Seconds = anchorSeconds;

        public void Tick(float deltaTime, double renderTime)
        {
            double delta = Math.Max(0, deltaTime);
            Seconds += delta;
            double expected = anchorSeconds + Math.Max(0, renderTime - anchorRenderTime);
            // 网络节奏变化时用小幅调速追踪校准点，避免每次收包硬跳帧或倒放。
            double correction = (expected - Seconds) * Math.Min(1, delta * 8);
            Seconds += Math.Max(-delta * .2, Math.Min(delta * .2, correction));
        }
    }
}
