using System;
using System.Collections.Generic;
using SkillConfig;

namespace SkillRuntime
{
    // Read-only visualization of the SAME rational transforms used by detection.
    // Conversion to double happens only at this output boundary, never in collision tests.
    public readonly struct XzOutlinePoint
    {
        public readonly double X, Z;
        internal XzOutlinePoint(RPoint p) { X = (double)p.X.N / (double)p.X.D; Z = (double)p.Z.N / (double)p.Z.D; }
    }
    public static class XzOutline
    {
        public static IReadOnlyList<XzOutlinePoint> Attack(HitBox box, LogicPose pose)
        {
            var shape = new AttackShape(box);
            var center = RPoint.Of(pose.X, pose.Z) + pose.Rotation.Apply(shape.Offset);
            var rotation = pose.Rotation.Then(shape.Rotation);
            var points = new List<XzOutlinePoint>();
            Action<RPoint> add = p => points.Add(new XzOutlinePoint(center + rotation.Apply(p)));
            if (shape.Kind == ShapeKind.Box)
            {
                add(new RPoint(-shape.HalfWidth, -shape.HalfDepth)); add(new RPoint(shape.HalfWidth, -shape.HalfDepth));
                add(new RPoint(shape.HalfWidth, shape.HalfDepth)); add(new RPoint(-shape.HalfWidth, shape.HalfDepth));
            }
            else
            {
                int angle = shape.Kind == ShapeKind.Sector ? shape.SectorAngle : 360000;
                if (angle < 360000) add(RPoint.Of(0, 0));
                for (int i = 0; i <= 96; i++)
                {
                    long halfMilli = -angle + 2L * angle * i / 96;
                    add(PlaneRotation.FromHalfMilli(halfMilli).Apply(new RPoint(Rational.Of(0), shape.Radius)));
                }
            }
            points.Add(points[0]); return points.AsReadOnly();
        }
        public static IReadOnlyList<XzOutlinePoint> Hurt(HurtCircle hurt, LogicPose pose)
        {
            GeometryLimits.Dimension(hurt.Radius);
            var center = RPoint.Of(pose.X, pose.Z) + pose.Rotation.Apply(RPoint.Of(hurt.OffsetX, hurt.OffsetZ));
            var points = new List<XzOutlinePoint>();
            for (int i = 0; i <= 96; i++)
                points.Add(new XzOutlinePoint(center + PlaneRotation.FromHalfMilli(720000L * i / 96).Apply(RPoint.Of(0, hurt.Radius))));
            return points.AsReadOnly();
        }
    }
}
