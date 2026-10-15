using UnityEngine;
using ZZZ.SkillAuthoring;

namespace ZZZ.SkillRuntime
{
    /// <summary>人工验收显示：生命、逻辑帧、状态、连段缓存与同帧摘要。不反向修改战斗。</summary>
    public sealed class CombatStatusView : MonoBehaviour
    {
        public bool showPanel = true;
        public bool logEveryFrame;
        public bool drawCombatGizmos = true;
        private RoleManager manager;
        public void Bind(RoleManager value) => manager = value;

        public void OnLogicFrame()
        {
            var world = manager != null ? manager.CombatWorld : null;
            if (logEveryFrame && world != null)
            {
                var text = new System.Text.StringBuilder($"[FrameCombat] frame={world.Frame} hash={world.StateHash():X8} hits={world.LastHitCount}");
                foreach (var fighter in world.Fighters)
                    text.Append($" | P{fighter.BattleId} hp={fighter.Health} state={fighter.State} skill={fighter.Skill?.Data.skillId} F={fighter.SkillFrame}"
                        + $" stop={fighter.HitStopRemaining} frozen={fighter.FrozenThisFrame} stun={fighter.HitStunRemaining}"
                        + $" cast={fighter.CastSerial} command={fighter.SkillCommand} buffered={fighter.HasBufferedCombo}"
                        + $" pose={fighter.PoseFrame} XZ=({fighter.Position.x},{fighter.Position.y}) facing={fighter.Facing}");
                Debug.Log(text.ToString());
            }
        }
        private void OnGUI()
        {
            if (!showPanel || manager == null) return;
            GUILayout.BeginArea(new Rect(12, 12, 600, 260), GUI.skin.box);
            if (!string.IsNullOrEmpty(manager.CombatError)) GUILayout.Label("帧战斗已停止：" + manager.CombatError);
            var world = manager.CombatWorld;
            if (world != null)
            {
                GUILayout.Label($"对战逻辑帧 {world.Frame}　摘要 {world.StateHash():X8}　本帧命中 {world.LastHitCount}");
                foreach (var fighter in world.Fighters)
                {
                    GUILayout.Label($"玩家 {fighter.BattleId}　HP {fighter.Health}/{fighter.Library.Profile.maxHealth}　{fighter.State}");
                    GUILayout.Label($"技能 {fighter.Skill?.Data.skillId ?? "—"} / {fighter.SkillFrame}　顿帧已停用　硬直 {fighter.HitStunRemaining}");
                    GUILayout.Label($"释放序号 {fighter.CastSerial}　连段缓存：{(fighter.HasBufferedCombo ? "有" : "无")}");
                    GUILayout.Label($"XZ ({fighter.Position.x}, {fighter.Position.y})　朝向 {fighter.Facing}°");
                }
                if (world.Finished) GUILayout.Label(world.WinnerId == 0 ? "双方同时倒下：平局" : $"玩家 {world.WinnerId} 获胜");
            }
            else if (string.IsNullOrEmpty(manager.CombatError)) GUILayout.Label("正在准备帧战斗配置……");
            GUILayout.EndArea();
        }

        private void OnDrawGizmos()
        {
            var world = manager != null ? manager.CombatWorld : null;
            if (!drawCombatGizmos || world == null) return;
            Color previous = Gizmos.color;
            foreach (var fighter in world.Fighters)
            {
                Gizmos.color = Color.cyan;
                DrawCircle(fighter.Position, fighter.Library.Profile.hurtRadius);
                if (fighter.Skill == null || fighter.Skill.Data.hitRegions == null) continue;
                Gizmos.color = fighter.FrozenThisFrame ? Color.gray : new Color(1f, .5f, .1f);
                for (int i = 0; i < fighter.Skill.Data.hitRegions.Count; i++)
                {
                    var region = fighter.Skill.Data.hitRegions[i];
                    if (!SkillHitTiming.IsHitFrame(region, fighter.SkillFrame)) continue;
                    if (!fighter.RegionAnchors.TryGet(fighter, i, out var origin, out int facing)) continue;
                    Vector2Int center = origin + CombatMath.Rotate(region.center, facing);
                    if (region.shape == SkillAuthoring.SkillRegionShape.Circle) DrawCircle(center, region.radius);
                    else if (region.shape == SkillRegionShape.Sector) DrawSector(region, center, facing);
                    else
                    {
                        CombatCollision.RectangleCorners(region, center, facing, out var a, out var b, out var c, out var d);
                        Gizmos.DrawLine(Display(a), Display(b)); Gizmos.DrawLine(Display(b), Display(c));
                        Gizmos.DrawLine(Display(c), Display(d)); Gizmos.DrawLine(Display(d), Display(a));
                    }
                }
            }
            Gizmos.color = previous;
        }
        private static Vector3 Display(Vector2Int point) => new Vector3(point.x / 10000f, .03f, point.y / 10000f);
        private static void DrawSector(SkillHitRegion region, Vector2Int center, int facing)
        {
            if (region.sectorAngleDegrees == 360) { DrawCircle(center, region.radius); return; }
            int span = Mathf.Clamp(region.sectorAngleDegrees, 1, 360) * 2;
            Vector3 origin = Display(center);
            Vector3 Point(int offset)
            {
                var direction = CombatSectorGeometry.ArcDirection(region, facing, offset);
                return origin + new Vector3(direction.x, 0, direction.y).normalized * (region.radius / (float)SkillFrameUtility.PositionPrecision);
            }
            Vector3 previous = Point(0);
            Gizmos.DrawLine(origin, previous);
            for (int offset = 10; offset < span; offset += 10)
            {
                Vector3 next = Point(offset);
                Gizmos.DrawLine(previous, next); previous = next;
            }
            Vector3 last = Point(span);
            Gizmos.DrawLine(previous, last); Gizmos.DrawLine(last, origin);
        }
        private static void DrawCircle(Vector2Int center, int radius)
        {
            for (int angle = 0; angle < 360; angle += 10)
                Gizmos.DrawLine(Display(center + CombatMath.Move(angle, radius)), Display(center + CombatMath.Move(angle + 10, radius)));
        }
    }
}
