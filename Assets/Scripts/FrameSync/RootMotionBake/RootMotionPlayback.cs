namespace FrameSync.RootMotion
{
    /// <summary>One sample per server tick; no Unity time, animation, transform or collision dependency.</summary>
    public sealed class RootMotionPlayback
    {
        private RootMotionClip clip;
        private int frame, endFrame, distancePermille, forwardX, forwardZ, directionScale;
        private long localX, localZ, previousWorldX, previousWorldZ;
        public bool IsPlaying => clip != null;

        // Local +Z is forward and +X is right. Basis comes from the shared integer direction table.
        // endFrameExclusive == 0 means the full clip. Invalid requests preserve current playback.
        public bool TryStart(RootMotionClip value, int endFrameExclusive, int scalePermille,
            int facingX, int facingZ, int facingScale)
        {
            int end = endFrameExclusive == 0 ? value?.FrameCount ?? 0 : endFrameExclusive;
            if (value == null || end <= 0 || end > value.FrameCount || scalePermille <= 0
                || scalePermille > 10000 || facingScale <= 0 || facingScale > 1000000
                || System.Math.Abs((long)facingX) > facingScale || System.Math.Abs((long)facingZ) > facingScale
                || (facingX == 0 && facingZ == 0)) return false;
            clip = value;
            frame = 0;
            endFrame = end;
            distancePermille = scalePermille;
            forwardX = facingX;
            forwardZ = facingZ;
            directionScale = facingScale;
            localX = localZ = previousWorldX = previousWorldZ = 0;
            return true;
        }

        // Returns true for the last sample too. Caller applies it before completing the action.
        public bool TryAdvance(out int deltaX, out int deltaZ)
        {
            deltaX = deltaZ = 0;
            if (!IsPlaying) return false;
            clip.GetDelta(frame++, out int x, out int z);
            localX += x;
            localZ += z;
            long divisor = (long)directionScale * 1000;
            // Transform cumulative offsets, then difference: retain fractional scale/rotation remainders.
            long worldX = (localX * forwardZ + localZ * forwardX) * distancePermille / divisor;
            long worldZ = (-localX * forwardX + localZ * forwardZ) * distancePermille / divisor;
            deltaX = checked((int)(worldX - previousWorldX));
            deltaZ = checked((int)(worldZ - previousWorldZ));
            previousWorldX = worldX;
            previousWorldZ = worldZ;
            if (frame >= endFrame) Stop();
            return true;
        }

        public void Stop() { clip = null; }
    }
}
