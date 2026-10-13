using System;
using UnityEngine;

namespace ZZZ.SkillRuntime
{
    /// <summary>固定整数方向表。运行时不调用三角函数；角度沿用网络的 +X 起点、朝 +Z 递增。</summary>
    public static class CombatMath
    {
        public const int DirectionScale = 1000000;
        public const int CoordinateLimit = 100000000;
        private static readonly int[] QuarterSin = { 0, 17452, 34899, 52336, 69756, 87156, 104528, 121869, 139173, 156434, 173648, 190809, 207912, 224951, 241922, 258819, 275637, 292372, 309017, 325568, 342020, 358368, 374607, 390731, 406737, 422618, 438371, 453990, 469472, 484810, 500000, 515038, 529919, 544639, 559193, 573576, 587785, 601815, 615661, 629320, 642788, 656059, 669131, 681998, 694658, 707107, 719340, 731354, 743145, 754710, 766044, 777146, 788011, 798636, 809017, 819152, 829038, 838671, 848048, 857167, 866025, 874620, 882948, 891007, 898794, 906308, 913545, 920505, 927184, 933580, 939693, 945519, 951057, 956305, 961262, 965926, 970296, 974370, 978148, 981627, 984808, 987688, 990268, 992546, 994522, 996195, 997564, 998630, 999391, 999848, 1000000 };

        public static int Normalize(int degrees) => (degrees % 360 + 360) % 360;
        private static int Sin(int degrees)
        {
            int angle = Normalize(degrees);
            if (angle <= 90) return QuarterSin[angle];
            if (angle <= 180) return QuarterSin[180 - angle];
            if (angle <= 270) return -QuarterSin[angle - 180];
            return -QuarterSin[360 - angle];
        }
        public static Vector2Int Direction(int degrees) => new Vector2Int(Sin(90 + degrees), Sin(degrees));

        // 本地 X 为右、Z 为前；整数累计位移先旋转再做差，保留旋转舍入的余量。
        public static Vector2Int Rotate(Vector2Int local, int facing)
        {
            var forward = Direction(facing);
            return new Vector2Int(checked((int)(((long)local.x * forward.y + (long)local.y * forward.x) / DirectionScale)),
                checked((int)((-(long)local.x * forward.x + (long)local.y * forward.y) / DirectionScale)));
        }

        public static Vector2Int Move(int direction, int distance)
        {
            var forward = Direction(direction);
            return new Vector2Int((int)((long)forward.x * distance / DirectionScale), (int)((long)forward.y * distance / DirectionScale));
        }

        public static bool InRange(int value) => Math.Abs((long)value) <= CoordinateLimit;
        public static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
    }
}
