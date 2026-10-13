using System.Numerics;
using UnityEngine;
using ZZZ.SkillAuthoring;

namespace ZZZ.SkillRuntime
{
    /// <summary>XZ 整数几何。旋转矩形按整数顶点生成；圆与边/角点均判定，边界接触算命中。</summary>
    public static class CombatCollision
    {
        public static bool Overlaps(SkillHitRegion region, Vector2Int attacker, int facing, Vector2Int target, int radius)
        {
            Vector2Int center = attacker + CombatMath.Rotate(region.center, facing);
            if (region.shape == SkillRegionShape.Circle)
            {
                Vector2Int delta = target - center;
                BigInteger sum = (long)radius + region.radius;
                return Dot(delta, delta) <= sum * sum;
            }
            RectangleCorners(region, center, facing, out var a, out var b, out var c, out var d);
            bool positive = false, negative = false;
            CheckSide(a, b, target, ref positive, ref negative);
            CheckSide(b, c, target, ref positive, ref negative);
            CheckSide(c, d, target, ref positive, ref negative);
            CheckSide(d, a, target, ref positive, ref negative);
            // 量化成线/点的极小矩形不能将整个平面判为内部。
            if (Cross(b - a, d - a) != 0 && !(positive && negative)) return true;
            return NearSegment(a, b, target, radius) || NearSegment(b, c, target, radius)
                || NearSegment(c, d, target, radius) || NearSegment(d, a, target, radius);
        }

        // 供逻辑碰撞和运行时 Gizmos 共用，调试线不再用另一套浮点旋转猜测实际区域。
        public static void RectangleCorners(SkillHitRegion region, Vector2Int center, int facing,
            out Vector2Int a, out Vector2Int b, out Vector2Int c, out Vector2Int d)
        {
            // yaw 绕 Unity 的 +Y 转动，对应网络平面角的反方向。
            int angle = CombatMath.Normalize(facing - region.yawDegrees);
            a = center + CombatMath.Rotate(new Vector2Int(-region.size.x / 2, -region.size.y / 2), angle);
            b = center + CombatMath.Rotate(new Vector2Int(-region.size.x / 2, region.size.y / 2), angle);
            c = center + CombatMath.Rotate(new Vector2Int(region.size.x / 2, region.size.y / 2), angle);
            d = center + CombatMath.Rotate(new Vector2Int(region.size.x / 2, -region.size.y / 2), angle);
        }

        private static void CheckSide(Vector2Int a, Vector2Int b, Vector2Int p, ref bool positive, ref bool negative)
        {
            int sign = Cross(b - a, p - a).Sign;
            positive |= sign > 0; negative |= sign < 0;
        }
        private static bool NearSegment(Vector2Int a, Vector2Int b, Vector2Int p, int radius)
        {
            Vector2Int edge = b - a, delta = p - a;
            BigInteger length = Dot(edge, edge), projection = Dot(delta, edge), r2 = (BigInteger)radius * radius;
            if (length == 0 || projection <= 0) return Dot(delta, delta) <= r2;
            if (projection >= length) return Dot(p - b, p - b) <= r2;
            BigInteger cross = Cross(edge, delta);
            // 两边同乘边长平方，无开方/浮点；大整数避免远距离或大区域在平方时溢出。
            return cross * cross <= r2 * length;
        }
        private static BigInteger Dot(Vector2Int a, Vector2Int b) => (BigInteger)a.x * b.x + (BigInteger)a.y * b.y;
        private static BigInteger Cross(Vector2Int a, Vector2Int b) => (BigInteger)a.x * b.y - (BigInteger)a.y * b.x;
    }
}
