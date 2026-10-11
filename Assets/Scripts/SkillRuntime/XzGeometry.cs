using System;
using System.Numerics;
using System.Globalization;
using SkillConfig;

namespace SkillRuntime
{
    // Normalized exact fractions. No floating point or Unity geometry in the detection path.
    internal readonly struct Rational : IComparable<Rational>
    {
        internal readonly BigInteger N, D;
        internal Rational(BigInteger n, BigInteger d)
        {
            if (d.IsZero) throw new DivideByZeroException();
            if (d.Sign < 0) { n = -n; d = -d; }
            var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(n), d);
            N = n / gcd; D = d / gcd;
        }
        internal static Rational Of(long n) => new Rational(n, 1);
        public int CompareTo(Rational b) => (N * b.D).CompareTo(b.N * D);
        public static Rational operator +(Rational a, Rational b) => new Rational(a.N * b.D + b.N * a.D, a.D * b.D);
        public static Rational operator -(Rational a, Rational b) => new Rational(a.N * b.D - b.N * a.D, a.D * b.D);
        public static Rational operator -(Rational a) => new Rational(-a.N, a.D);
        public static Rational operator *(Rational a, Rational b) => new Rational(a.N * b.N, a.D * b.D);
        public static Rational operator /(Rational a, Rational b) => new Rational(a.N * b.D, a.D * b.N);
        public static bool operator <=(Rational a, Rational b) => a.CompareTo(b) <= 0;
        public static bool operator >=(Rational a, Rational b) => a.CompareTo(b) >= 0;
        internal Rational Abs() => new Rational(BigInteger.Abs(N), D);
    }

    internal readonly struct RPoint
    {
        internal readonly Rational X, Z;
        internal RPoint(Rational x, Rational z) { X = x; Z = z; }
        internal static RPoint Of(long x, long z) => new RPoint(Rational.Of(x), Rational.Of(z));
        public static RPoint operator +(RPoint a, RPoint b) => new RPoint(a.X + b.X, a.Z + b.Z);
        public static RPoint operator -(RPoint a, RPoint b) => new RPoint(a.X - b.X, a.Z - b.Z);
        public static RPoint operator *(RPoint a, Rational b) => new RPoint(a.X * b, a.Z * b);
        internal Rational Dot(RPoint b) => X * b.X + Z * b.Z;
        internal Rational Cross(RPoint b) => X * b.Z - Z * b.X;
        internal Rational Squared => Dot(this);
    }

    internal readonly struct PlaneRotation
    {
        internal readonly Rational C, S;
        PlaneRotation(Rational c, Rational s) { C = c; S = s; }
        // Unity positive Y yaw: local +Z turns towards +X.
        internal RPoint Apply(RPoint p) => new RPoint(C * p.X + S * p.Z, -S * p.X + C * p.Z);
        internal RPoint Inverse(RPoint p) => new RPoint(C * p.X - S * p.Z, S * p.X + C * p.Z);
        internal PlaneRotation Then(PlaneRotation b) => new PlaneRotation(C * b.C - S * b.S, S * b.C + C * b.S);
        static readonly BigInteger One = BigInteger.One << 96;
        static readonly BigInteger Pi = BigInteger.Parse("314159265358979323846264338327950288419716939937510", CultureInfo.InvariantCulture) * One / BigInteger.Pow(10, 50);
        static readonly BigInteger TScale = BigInteger.One << 48;
        // Half-millidegree input preserves odd sector half angles. Only this construction approximates angles.
        internal static PlaneRotation FromHalfMilli(long angle)
        {
            long a = ((angle % 720000) + 720000) % 720000;
            int quadrant = (int)(a / 180000); long remainder = a % 180000;
            BigInteger t = 0;
            if (remainder != 0)
            {
                BigInteger x = Pi * remainder / 360000; // [0, pi/2), Q96, truncate positive.
                BigInteger xx = x * x / One;
                BigInteger sin = x, cos = One, st = x, ct = One;
                for (int n = 1; n <= 24; n++)
                {
                    st = -(st * xx) / (One * (2 * n) * (2 * n + 1));
                    ct = -(ct * xx) / (One * (2 * n - 1) * (2 * n));
                    sin += st; cos += ct;
                }
                BigInteger denominator = One + cos;
                t = (sin * TScale * 2 + denominator) / (denominator * 2); // nearest, half away from zero (positive).
            }
            BigInteger q2 = TScale * TScale, t2 = t * t, d = q2 + t2;
            // Pythagorean rational rotation: C*C + S*S == 1 exactly, not an almost-unit fixed-point vector.
            Rational c = new Rational(q2 - t2, d), s = new Rational(2 * t * TScale, d);
            switch (quadrant)
            {
                case 0: return new PlaneRotation(c, s);
                case 1: return new PlaneRotation(-s, c);
                case 2: return new PlaneRotation(-c, -s);
                default: return new PlaneRotation(s, -c);
            }
        }
    }

    public readonly struct LogicPose
    {
        public readonly long X, Y, Z;
        // Existing roleDirection convention: zero = world +X, positive towards +Z.
        public readonly int FacingMilliDegrees;
        readonly PlaneRotation rotation;
        readonly bool initialized;
        public LogicPose(long x, long y, long z, int facingMilliDegrees)
        { GeometryLimits.Coordinate(x); GeometryLimits.Coordinate(z); X = x; Y = y; Z = z; FacingMilliDegrees = facingMilliDegrees; rotation = PlaneRotation.FromHalfMilli(180000L - 2L * facingMilliDegrees); initialized = true; }
        internal PlaneRotation Rotation => initialized ? rotation : throw new ArgumentException("Construct LogicPose explicitly; default is not a snapshot.");
    }

    public readonly struct HurtCircle
    {
        public readonly long OffsetX, OffsetY, OffsetZ, Radius;
        public HurtCircle(long radius, long offsetX = 0, long offsetY = 0, long offsetZ = 0)
        { GeometryLimits.Dimension(radius); GeometryLimits.Coordinate(offsetX); GeometryLimits.Coordinate(offsetZ); Radius = radius; OffsetX = offsetX; OffsetY = offsetY; OffsetZ = offsetZ; }
    }

    public static class GeometryLimits
    {
        public const long MaxInput = 1000000000L;
        public static void Coordinate(long value) { if (value < -MaxInput || value > MaxInput) throw new ArgumentOutOfRangeException(nameof(value), "XZ input must be within +/- 1,000,000,000 logic units."); }
        public static void Dimension(long value) { if (value <= 0 || value > MaxInput) throw new ArgumentOutOfRangeException(nameof(value), "Dimension must be in 1..1,000,000,000 logic units."); }
    }

    // Immutable subset compiled from the existing HitBox, not another editable skill definition.
    public sealed class AttackShape
    {
        public string Id { get; }
        public string GroupId { get; }
        public int StartFrame { get; }
        public int EndFrame { get; }
        public ShapeKind Kind { get; }
        internal readonly RPoint Offset;
        internal readonly PlaneRotation Rotation, LeftRotation, RightRotation;
        internal readonly Rational Radius, HalfWidth, HalfDepth;
        internal readonly int SectorAngle;
        public AttackShape(HitBox b)
        {
            if (b == null || !SkillConfigValidation.SafeId(b.id) || !SkillConfigValidation.SafeId(b.hitGroupId)) throw new ArgumentException("Shape requires stable IDs.");
            if (!Enum.IsDefined(typeof(ShapeKind), b.shape) || b.startFrame < 0 || b.endFrame <= b.startFrame) throw new ArgumentException("Invalid shape or frame interval.");
            GeometryLimits.Coordinate(b.offset.x); GeometryLimits.Coordinate(b.offset.z);
            if (b.shape == ShapeKind.Box) { GeometryLimits.Dimension(b.size.x); GeometryLimits.Dimension(b.size.z); if (b.size.y <= 0) throw new ArgumentException("Invalid authored Box height."); }
            else GeometryLimits.Dimension(b.radius);
            if (b.shape == ShapeKind.Capsule && b.height < 2L * b.radius) throw new ArgumentException("Only vertical Capsule with height >= diameter is supported.");
            if ((b.shape == ShapeKind.Cylinder || b.shape == ShapeKind.Sector) && b.height <= 0) throw new ArgumentException("Invalid authored height.");
            if (b.shape == ShapeKind.Sector && (b.sectorAngleMilliDegrees <= 0 || b.sectorAngleMilliDegrees > 360000)) throw new ArgumentException("Invalid sector angle.");
            Id = b.id; GroupId = b.hitGroupId; StartFrame = b.startFrame; EndFrame = b.endFrame; Kind = b.shape;
            Offset = RPoint.Of(b.offset.x, b.offset.z); Rotation = PlaneRotation.FromHalfMilli(2L * b.yawMilliDegrees);
            Radius = Rational.Of(b.radius); HalfWidth = new Rational(b.size.x, 2); HalfDepth = new Rational(b.size.z, 2);
            SectorAngle = b.sectorAngleMilliDegrees;
            LeftRotation = PlaneRotation.FromHalfMilli(-(long)SectorAngle); RightRotation = PlaneRotation.FromHalfMilli(SectorAngle);
        }
    }

    public static class XzGeometry
    {
        public static bool Intersects(AttackShape attack, LogicPose attacker, HurtCircle hurt, LogicPose target)
        {
            if (attack == null) throw new ArgumentNullException(nameof(attack));
            GeometryLimits.Dimension(hurt.Radius); // reject default(struct).
            var ar = attacker.Rotation;
            var center = RPoint.Of(attacker.X, attacker.Z) + ar.Apply(attack.Offset);
            var targetCenter = RPoint.Of(target.X, target.Z) + target.Rotation.Apply(RPoint.Of(hurt.OffsetX, hurt.OffsetZ));
            var difference = targetCenter - center;
            var hr = Rational.Of(hurt.Radius);
            if (attack.Kind == ShapeKind.Sphere || attack.Kind == ShapeKind.Capsule || attack.Kind == ShapeKind.Cylinder)
                return difference.Squared <= Square(attack.Radius + hr);
            // The shape's yaw rotates its orientation, never its offset a second time.
            var local = ar.Then(attack.Rotation).Inverse(difference);
            if (attack.Kind == ShapeKind.Box)
            {
                var dx = Positive(local.X.Abs() - attack.HalfWidth); var dz = Positive(local.Z.Abs() - attack.HalfDepth);
                return Square(dx) + Square(dz) <= Square(hr);
            }
            if (attack.SectorAngle == 360000) return local.Squared <= Square(attack.Radius + hr);
            var left = attack.LeftRotation.Apply(RPoint.Of(0, 1));
            var right = attack.RightRotation.Apply(RPoint.Of(0, 1));
            bool afterLeft = left.Cross(local).N.Sign <= 0, beforeRight = local.Cross(right).N.Sign <= 0;
            bool angular = attack.SectorAngle <= 180000 ? afterLeft && beforeRight : afterLeft || beforeRight;
            // In the angular wedge, disk expansion handles the arc and interior exactly.
            if (angular && local.Squared <= Square(attack.Radius + hr)) return true;
            return SegmentDistanceSquared(local, left * attack.Radius) <= Square(hr)
                || SegmentDistanceSquared(local, right * attack.Radius) <= Square(hr);
        }
        static Rational SegmentDistanceSquared(RPoint p, RPoint endpoint)
        {
            var dot = p.Dot(endpoint); var length2 = endpoint.Squared;
            if (dot.N.Sign <= 0) return p.Squared;
            if (dot >= length2) return (p - endpoint).Squared;
            return p.Squared - Square(dot) / length2;
        }
        static Rational Square(Rational a) => a * a;
        static Rational Positive(Rational a) => a.N.Sign < 0 ? Rational.Of(0) : a;
    }
}
