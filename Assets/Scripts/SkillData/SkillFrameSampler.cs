using System;
using FrameSync.RootMotion;
using UnityEngine;

namespace ZZZ.SkillAuthoring
{
    /// <summary>
    /// 无状态时间采样 + 不可变位移前缀表。支持任意顺序拖动，不依赖 Update 或累计 deltaTime。
    /// 游标 F 表示 F*33ms 的帧起点，已消费 [0,F) 的位移；末端游标仅展示完成姿态。
    /// </summary>
    public sealed class SkillFrameSampler
    {
        private readonly long[] cumulativeX;
        private readonly long[] cumulativeZ;
        public RootMotionJsonData MotionMetadata { get; }
        public string MotionError { get; }
        public int MotionFrameCount => cumulativeX == null ? 0 : cumulativeX.Length - 1;

        public SkillFrameSampler(SkillDefinition skill)
        {
            TextAsset json = skill != null ? skill.motion?.settings?.json : null;
            if (json == null) return;
            try
            {
                var data = JsonUtility.FromJson<RootMotionJsonData>(json.text);
                if (!RootMotionClip.TryCreate(data, SkillFrameUtility.IntervalMs,
                    SkillFrameUtility.PositionPrecision, out var clip, out string error))
                {
                    MotionError = error;
                    return;
                }
                MotionMetadata = data;
                cumulativeX = new long[clip.FrameCount + 1];
                cumulativeZ = new long[clip.FrameCount + 1];
                for (int i = 0; i < clip.FrameCount; i++)
                {
                    clip.GetDelta(i, out int x, out int z);
                    cumulativeX[i + 1] = cumulativeX[i] + x;
                    cumulativeZ[i + 1] = cumulativeZ[i] + z;
                }
            }
            catch (Exception exception) { MotionError = exception.Message; }
        }

        public Vector2Int SampleLocalPosition(SkillDefinition skill, int frameBoundary)
        {
            if (cumulativeX == null || skill?.motion?.settings == null) return Vector2Int.zero;
            var settings = skill.motion.settings;
            int end = settings.endFrameExclusive == 0 ? MotionFrameCount : settings.endFrameExclusive;
            int count = Math.Max(0, Math.Min(Math.Min(end, MotionFrameCount), frameBoundary - skill.motion.startFrame));
            // 与 RootMotionPlayback 相同：先按累计值缩放，再取整，避免逐帧舍入漂移。
            return new Vector2Int(checked((int)(cumulativeX[count] * settings.distancePermille / 1000)),
                checked((int)(cumulativeZ[count] * settings.distancePermille / 1000)));
        }

        public static double SampleAnimationSeconds(SkillDefinition skill, int frameBoundary)
        {
            var track = skill?.animation;
            if (track?.clip == null) return 0;
            double end = AnimationEndSeconds(track);
            double start = Math.Max(0, Math.Min(track.sourceStartSeconds, end));
            double elapsed = SkillFrameUtility.FrameSeconds(Math.Max(0, frameBoundary - track.startFrame));
            return Math.Min(end, start + elapsed * Math.Max(0.01f, track.speed));
        }

        public static double AnimationEndSeconds(SkillAnimationTrack track) => track?.clip == null ? 0
            : track.sourceEndSeconds <= 0 ? track.clip.length : Math.Min(track.clip.length, track.sourceEndSeconds);

        public static int AnimationEndFrame(SkillAnimationTrack track)
        {
            if (track?.clip == null || track.speed <= 0 || float.IsNaN(track.speed)) return 0;
            double duration = Math.Max(0, AnimationEndSeconds(track) - track.sourceStartSeconds) / track.speed;
            return track.startFrame + SkillFrameUtility.SecondsToFrame(duration);
        }
    }
}
