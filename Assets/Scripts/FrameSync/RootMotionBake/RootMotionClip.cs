namespace FrameSync.RootMotion
{
    /// <summary>Validated, immutable horizontal motion. Safe to share between roles.</summary>
    public sealed class RootMotionClip
    {
        private readonly int[] x;
        private readonly int[] z;
        public int FrameCount => x.Length;

        private RootMotionClip(int[] x, int[] z) { this.x = x; this.z = z; }

        public void GetDelta(int frame, out int deltaX, out int deltaZ)
        {
            deltaX = x[frame];
            deltaZ = z[frame];
        }

        public static bool TryCreate(RootMotionJsonData data, int intervalMs, int precision,
            out RootMotionClip clip, out string error)
        {
            clip = null;
            error = "Root motion metadata does not match the simulation.";
            if (data == null || intervalMs <= 0 || precision <= 0
                || data.logicFrameIntervalMs != intervalMs || data.precision != precision
                || data.clipLengthMs <= 0 || data.frames == null || data.frames.Count == 0
                || data.rootMotionEndTimeMs <= 0 || data.rootMotionEndTimeMs > data.clipLengthMs
                || data.frameCount != data.frames.Count
                || data.frameCount != ((long)data.rootMotionEndTimeMs + intervalMs - 1) / intervalMs)
                return false;

            var x = new int[data.frameCount];
            var z = new int[data.frameCount];
            long sumX = 0, sumY = 0, sumZ = 0;
            for (int i = 0; i < data.frameCount; i++)
            {
                var frame = data.frames[i];
                error = "Invalid root motion sample at frame " + i + ".";
                long endTime = System.Math.Min((long)(i + 1) * intervalMs, data.rootMotionEndTimeMs);
                if (frame == null || frame.logicFrame != i || frame.sampleEndTimeMs != endTime
                    || frame.sampleDurationMs != endTime - (long)i * intervalMs)
                    return false;
                sumX += frame.deltaX;
                sumY += frame.deltaY;
                sumZ += frame.deltaZ;
                if (sumX != frame.positionX || sumY != frame.positionY || sumZ != frame.positionZ)
                    return false;
                x[i] = frame.deltaX;
                z[i] = frame.deltaZ;
            }
            error = "Root motion totals do not match the samples.";
            if (sumX != data.totalX || sumY != data.totalY || sumZ != data.totalZ) return false;
            clip = new RootMotionClip(x, z);
            error = null;
            return true;
        }
    }
}
