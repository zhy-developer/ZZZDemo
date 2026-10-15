using System.Numerics;
using UnityEngine;
using ZZZ.SkillAuthoring;

namespace ZZZ.SkillRuntime
{
    /// <summary>扇形与受击圆的整数几何。检测内部、圆弧、侧边和端点，边界接触算命中。</summary>
    public static class CombatSectorGeometry
    {
        // offsetHalfDegrees 从扇形第一条侧边起，沿网络角度正方向遍历；两端为 0 与张角 * 2。
        // 奇数张角的半度边界使用相邻整数方向的角平分向量，不引入运行时浮点三角函数。
        public static Vector2Int ArcDirection(SkillHitRegion region, int facing, int offsetHalfDegrees)
        {
            int halfAngle = (2 * CombatMath.Normalize(facing - region.yawDegrees)
                - region.sectorAngleDegrees + offsetHalfDegrees) % 720;
            if (halfAngle < 0) halfAngle += 720;
            var direction = CombatMath.Direction(halfAngle / 2);
            return halfAngle % 2 == 0 ? direction : direction + CombatMath.Direction(halfAngle / 2 + 1);
        }

        public static bool Overlaps(SkillHitRegion region, Vector2Int delta, int facing, int targetRadius)
        {
            if (region.radius <= 0 || targetRadius < 0 || region.sectorAngleDegrees < 1 || region.sectorAngleDegrees > 360) return false;
            BigInteger distance2 = Dot(delta, delta);
            BigInteger sum = (long)region.radius + targetRadius;
            if (distance2 > sum * sum) return false;
            if (region.sectorAngleDegrees == 360) return true;

            var first = ArcDirection(region, facing, 0);
            var last = ArcDirection(region, facing, region.sectorAngleDegrees * 2);
            bool afterFirst = Cross(first, delta) >= 0;
            bool beforeLast = Cross(delta, last) >= 0;
            // 大于半圆时是两个半平面的并集；小于或等于半圆时是交集。
            bool insideAngle = region.sectorAngleDegrees <= 180 ? afterFirst && beforeLast : afterFirst || beforeLast;
            if (insideAngle) return true; // 上面的扩展半径检查已覆盖内部和外侧圆弧。
            return NearRadialEdge(delta, first, region.radius, targetRadius)
                || NearRadialEdge(delta, last, region.radius, targetRadius);
        }

        private static bool NearRadialEdge(Vector2Int delta, Vector2Int direction, int length, int radius)
        {
            BigInteger distance2 = Dot(delta, delta), radius2 = (BigInteger)radius * radius;
            BigInteger projection = Dot(delta, direction), direction2 = Dot(direction, direction);
            if (projection <= 0) return distance2 <= radius2; // 最近点为扇形顶点。
            BigInteger length2 = (BigInteger)length * length;
            if (projection * projection <= length2 * direction2)
            {
                BigInteger cross = Cross(direction, delta);
                return cross * cross <= radius2 * direction2;
            }
            // 最近点为弧端点。侧边长度严格使用 radius，不把方向表近似长度当作半径。
            // |delta - R * unitDirection|² <= r²；移项后在两边非负时平方，消除开方。
            BigInteger remaining = distance2 + length2 - radius2;
            return remaining <= 0 || remaining * remaining * direction2 <= 4 * length2 * projection * projection;
        }

        private static BigInteger Dot(Vector2Int a, Vector2Int b) => (BigInteger)a.x * b.x + (BigInteger)a.y * b.y;
        private static BigInteger Cross(Vector2Int a, Vector2Int b) => (BigInteger)a.x * b.y - (BigInteger)a.y * b.x;
    }
}
