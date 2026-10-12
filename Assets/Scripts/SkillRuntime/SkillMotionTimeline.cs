using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FrameSync.RootMotion;
using SkillConfig;

namespace SkillRuntime
{
    // Frozen copy of existing AnimationDto time fields; no new serialization contract.
    public sealed class SkillClipTime
    {
        public readonly string Id;
        public readonly int Start, End, SpeedPermille;
        public readonly long TrimStartUs, TrimEndUs, SourceLengthUs;
        public bool Identity => TrimStartUs == 0 && TrimEndUs == SourceLengthUs && SpeedPermille == 1000;
        public SkillClipTime(AnimationDto a)
        {
            if (a == null || a.startFrame < 0 || a.endFrame <= a.startFrame || a.endFrame > 1000000 ||
                a.sourceLengthUs <= 0 || a.sourceLengthUs > 86400000000L || a.trimStartUs < 0 ||
                a.trimEndUs <= a.trimStartUs || a.trimEndUs > a.sourceLengthUs || a.speedPermille <= 0 || a.speedPermille > 100000)
                throw new ArgumentException("Invalid animation time mapping.");
            long step = 33000L * a.speedPermille;
            if ((checked((a.trimEndUs - a.trimStartUs) * 1000) + step - 1) / step != a.endFrame - a.startFrame)
                throw new ArgumentException("Animation span must equal ceil(trim duration / speed / 33ms).");
            Id = a.id; Start = a.startFrame; End = a.endFrame; SpeedPermille = a.speedPermille;
            TrimStartUs = a.trimStartUs; TrimEndUs = a.trimEndUs; SourceLengthUs = a.sourceLengthUs;
        }
        // Nanoseconds preserve fractional microseconds at all schema-approved permille speeds.
        // Inputs are bounded above; maximum pre-clamp product is 1e6*33000*100000 < 2^63.
        public long SourceNs(int boundary)
        {
            if (boundary < Start || boundary > End) throw new ArgumentOutOfRangeException(nameof(boundary));
            return Math.Min(TrimEndUs * 1000, checked(TrimStartUs * 1000 + (boundary - (long)Start) * 33000 * SpeedPermille));
        }
    }

    // Exact fractions until the final world cumulative position, truncated toward zero just
    // like RootMotionPlayback. BigInteger is intentional: schema coordinates, scale and
    // interpolation denominators together cannot be proven to fit a signed 64-bit product.
    internal readonly struct MotionFraction
    {
        readonly BigInteger n, d;
        BigInteger Den => d.IsZero ? BigInteger.One : d;
        public MotionFraction(BigInteger numerator, BigInteger denominator)
        {
            if (denominator.Sign <= 0) throw new ArgumentOutOfRangeException(nameof(denominator));
            var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
            n = numerator / gcd; d = denominator / gcd;
        }
        public static MotionFraction operator +(MotionFraction a, MotionFraction b) => new MotionFraction(a.n * b.Den + b.n * a.Den, a.Den * b.Den);
        public static MotionFraction operator -(MotionFraction a, MotionFraction b) => new MotionFraction(a.n * b.Den - b.n * a.Den, a.Den * b.Den);
        public MotionFraction Scale(long numerator, long denominator = 1) => new MotionFraction(n * numerator, Den * denominator);
        public long Truncate() => checked((long)(n / Den));
    }
    internal readonly struct MotionVector
    {
        public readonly MotionFraction X, Z;
        public MotionVector(MotionFraction x, MotionFraction z) { X = x; Z = z; }
        public static MotionVector operator +(MotionVector a, MotionVector b) => new MotionVector(a.X + b.X, a.Z + b.Z);
        public static MotionVector operator -(MotionVector a, MotionVector b) => new MotionVector(a.X - b.X, a.Z - b.Z);
        public MotionVector Scale(int permille) => new MotionVector(X.Scale(permille, 1000), Z.Scale(permille, 1000));
    }
    public readonly struct SkillMotionPosition
    {
        public readonly long X, Z;
        public SkillMotionPosition(long x, long z) { X = x; Z = z; }
    }
    public sealed class SkillMotionSegment
    {
        public readonly SkillClipTime Time;
        readonly long[] times;
        readonly int[] x, z;
        readonly int scale;
        readonly MotionVector origin;
        public readonly bool ProvenanceVerified;
        public SkillMotionSegment(AnimationDto animation, RootMotionJsonData data)
        {
            Time = new SkillClipTime(animation);
            if (!animation.rootMotionBound)
            {
                if (data != null) throw new ArgumentException("Unbound segment cannot consume a motion table.");
                return;
            }
            var binding = animation.rootMotion ?? throw new ArgumentException("Missing motion binding.");
            if (!RootMotionClip.TryCreate(data, 33, 10000, out _, out var error)) throw new ArgumentException(error);
            if (binding.clipName != animation.clipName || data.clipName != animation.clipName ||
                data.frameCount != binding.sampleCount || data.clipLengthMs != binding.sourceClipLengthMs ||
                binding.requiresTimeRemap != !Time.Identity ||
                binding.distancePermille <= 0 || binding.distancePermille > 10000)
                throw new ArgumentException("Motion binding and actual table disagree.");
            if (Math.Abs(data.clipLengthMs * 1000L - Time.SourceLengthUs) > 1000 && (!Time.Identity || binding.provenanceVerified))
                throw new ArgumentException("Cannot map mismatching animation/motion source durations.");
            int count = binding.endFrameExclusive == 0 ? data.frameCount : binding.endFrameExclusive;
            if (count <= 0 || count > data.frameCount) throw new ArgumentException("Invalid motion endpoint.");
            long coverageUs = data.frames[count - 1].sampleEndTimeMs * 1000L;
            if (binding.coverageEndMs * 1000L != coverageUs) throw new ArgumentException("Motion binding effective endpoint differs from table.");
            if (coverageUs > Time.SourceLengthUs + 1000) throw new ArgumentException("Motion coverage exceeds source duration.");
            if (Time.TrimEndUs > coverageUs + 1000 && binding.tailPolicy != MotionTailPolicy.HoldAtCoverageEnd)
                throw new ArgumentException("MOTION_TAIL_UNCONFIRMED: explicitly confirm stationary tail before runtime sampling.");
            times = new long[count + 1]; x = new int[count + 1]; z = new int[count + 1];
            for (int i = 0; i < count; i++)
            {
                times[i + 1] = data.frames[i].sampleEndTimeMs * 1000000L;
                x[i + 1] = data.frames[i].positionX; z[i + 1] = data.frames[i].positionZ;
            }
            scale = binding.distancePermille; ProvenanceVerified = binding.provenanceVerified;
            origin = Sample(Time.SourceNs(Time.Start));
        }
        MotionVector Sample(long ns)
        {
            if (times == null) return default;
            // The legacy bake quantizes source endpoints to milliseconds. For identity
            // playback the last logical frame still consumes the entire final sample.
            ns = Math.Min(ns, times[times.Length - 1]);
            int i = Array.BinarySearch(times, ns);
            if (i >= 0) return new MotionVector(new MotionFraction(x[i], 1), new MotionFraction(z[i], 1));
            i = ~i;
            long span = times[i] - times[i - 1], at = ns - times[i - 1];
            return new MotionVector(new MotionFraction((BigInteger)x[i - 1] * span + ((BigInteger)x[i] - x[i - 1]) * at, span),
                new MotionFraction((BigInteger)z[i - 1] * span + ((BigInteger)z[i] - z[i - 1]) * at, span));
        }
        internal MotionVector At(int boundary)
        {
            if (times == null) return default;
            long ns = Time.SourceNs(boundary);
            if (Time.Identity) ns = (boundary - (long)Time.Start) * 33000000L;
            return (Sample(ns) - origin).Scale(scale);
        }
    }
    public sealed class SkillMotionTimeline
    {
        readonly SkillMotionSegment[] segments;
        readonly MotionVector[] prefix;
        public readonly int Duration;
        public SkillMotionTimeline(IEnumerable<SkillMotionSegment> source)
        {
            segments = source.ToArray(); prefix = new MotionVector[segments.Length + 1];
            int next = 0;
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == null || segments[i].Time.Start != next) throw new ArgumentException("Segments must be contiguous from zero; no overlap/gap blending.");
                next = segments[i].Time.End; prefix[i + 1] = prefix[i] + segments[i].At(next);
            }
            if (next == 0) throw new ArgumentException("Empty motion timeline.");
            Duration = next;
        }
        public SkillMotionPosition Position(int boundary, int forwardX, int forwardZ, int directionScale)
        {
            if (boundary < 0 || boundary > Duration || directionScale <= 0 || directionScale > 1000000 ||
                Math.Abs((long)forwardX) > directionScale || Math.Abs((long)forwardZ) > directionScale || (forwardX == 0 && forwardZ == 0))
                throw new ArgumentOutOfRangeException(nameof(boundary), "Invalid boundary or frozen direction basis.");
            int i = Array.FindIndex(segments, s => boundary < s.Time.End);
            var local = i < 0 ? prefix[segments.Length] : prefix[i] + segments[i].At(boundary);
            return new SkillMotionPosition((local.X.Scale(forwardZ) + local.Z.Scale(forwardX)).Scale(1, directionScale).Truncate(),
                (local.Z.Scale(forwardZ) - local.X.Scale(forwardX)).Scale(1, directionScale).Truncate());
        }
    }
}
