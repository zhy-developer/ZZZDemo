using System;
using UnityEditor;
using UnityEngine;
using ZZZ.SkillAuthoring;
using ZZZ.SkillRuntime;

namespace ZZZ.SkillEditor
{
    internal enum SkillRegionTool { Move, Rotate, Resize }

    /// <summary>Scene 绘制、XZ 手柄与重叠提示。扇形复用整数几何，其余预览使用浮点；均不执行伤害。</summary>
    internal sealed class SkillSceneHandles
    {
        public Vector3 TargetPosition = new Vector3(0, 0, 2);
        public float TargetRadius = .4f;
        public float DisplayHeight = .03f;
        public SkillRegionTool Tool;
        public string OverlapLabel { get; private set; }

        public void Draw(SkillDefinition skill, SkillFrameSampler sampler, int frame, Vector3 logicPosition, SkillEditorSelection selection,
            Action<SkillEditorSelection> select, Action changed)
        {
            Color previous = Handles.color;
            try
            {
                Handles.color = new Color(.25f, .75f, 1);
                Vector3 ground = logicPosition + Vector3.up * DisplayHeight;
                Handles.DrawWireDisc(ground, Vector3.up, .12f);
                Handles.Label(ground, "逻辑根节点");
                TargetPosition = Handles.Slider2D(TargetPosition, Vector3.up, Vector3.right, Vector3.forward,
                    HandleUtility.GetHandleSize(TargetPosition) * .1f, Handles.RectangleHandleCap, Vector2.zero);
                TargetPosition.y = 0;
                Vector3 targetEdge = Handles.Slider(TargetPosition + Vector3.up * DisplayHeight + Vector3.right * TargetRadius,
                    Vector3.right, HandleUtility.GetHandleSize(TargetPosition) * .06f, Handles.CubeHandleCap, 0);
                TargetRadius = Mathf.Max(.01f, targetEdge.x - TargetPosition.x);
                Handles.DrawWireDisc(TargetPosition + Vector3.up * DisplayHeight, Vector3.up, TargetRadius);
                Handles.Label(TargetPosition + Vector3.up * DisplayHeight, "目标受击圆（仅预览）");
                int activeCount = 0, overlaps = 0;
                if (skill.hitRegions == null) return;
                for (int i = 0; i < skill.hitRegions.Count; i++)
                {
                    var region = skill.hitRegions[i]; if (region == null) continue;
                    Vector3 regionOrigin = PreviewOrigin(skill, sampler, region, logicPosition);
                    Vector3 regionGround = regionOrigin + Vector3.up * DisplayHeight;
                    bool selected = selection.kind == SkillSelectionKind.Region && selection.index == i;
                    bool active = frame < skill.totalFrames && SkillHitTiming.IsHitFrame(region, frame);
                    if (active) { activeCount++; if (Overlaps(region, regionOrigin)) overlaps++; }
                    if (!active && !selected) continue;
                    DrawRegion(region, regionGround, active, selected);
                    if (!selected)
                    {
                        Vector3 center = Center(region, regionGround);
                        Handles.color = new Color(1f, .6f, .15f);
                        if (Handles.Button(center, Quaternion.identity, .055f, .08f, Handles.DotHandleCap))
                            select(new SkillEditorSelection(SkillSelectionKind.Region, i));
                    }
                    else EditRegion(skill, region, regionGround, changed);
                }
                OverlapLabel = overlaps > 0 ? $"{overlaps} 个生效区域覆盖目标（不扣血）" : $"{activeCount} 个生效区域 / 未覆盖目标";
            }
            finally { Handles.color = previous; }
        }

        // Scene 使用独立技能、释放原点为零、朝向 +Z。固定区域直接采样锚定帧，
        // 不靠播放历史保存快照，保证跳帧、倒拖、循环和改配置后的结果一致。
        private static Vector3 PreviewOrigin(SkillDefinition skill, SkillFrameSampler sampler,
            SkillHitRegion region, Vector3 currentPosition)
        {
            if (region.anchorMode == SkillRegionAnchorMode.FollowCharacter) return currentPosition;
            int anchorFrame = region.anchorMode == SkillRegionAnchorMode.ActivationPosition ? region.startFrame : 0;
            Vector2Int position = sampler?.SampleLocalPosition(skill, anchorFrame) ?? Vector2Int.zero;
            float precision = SkillFrameUtility.PositionPrecision;
            return new Vector3(position.x / precision, 0, position.y / precision);
        }

        private static Vector3 Center(SkillHitRegion region, Vector3 origin)
        {
            float scale = SkillFrameUtility.PositionPrecision;
            return origin + new Vector3(region.center.x / scale, 0, region.center.y / scale);
        }

        private static void DrawRegion(SkillHitRegion region, Vector3 origin, bool active, bool selected)
        {
            Handles.color = active ? new Color(1, .5f, .15f, .95f) : new Color(1, .6f, .25f, .3f);
            Vector3 center = Center(region, origin);
            float precision = SkillFrameUtility.PositionPrecision;
            if (region.shape == SkillRegionShape.Circle) Handles.DrawWireDisc(center, Vector3.up, Mathf.Max(.0001f, region.radius / precision));
            else if (region.shape == SkillRegionShape.Sector)
            {
                if (region.sectorAngleDegrees == 360)
                    Handles.DrawWireDisc(center, Vector3.up, Mathf.Max(.0001f, region.radius / precision));
                else
                {
                    int angle = Mathf.Clamp(region.sectorAngleDegrees, 1, 360);
                    int steps = Mathf.CeilToInt(angle / 5f);
                    var outline = new Vector3[steps + 3];
                    outline[0] = center; outline[outline.Length - 1] = center;
                    for (int i = 0; i <= steps; i++)
                    {
                        var direction = CombatSectorGeometry.ArcDirection(region, 90, Math.Min(i * 10, angle * 2));
                        outline[i + 1] = center + new Vector3(direction.x, 0, direction.y).normalized * Mathf.Max(.0001f, region.radius / precision);
                    }
                    Handles.DrawAAPolyLine(selected ? 3 : 2, outline);
                }
            }
            else
            {
                Quaternion rotation = Quaternion.Euler(0, region.yawDegrees, 0);
                float x = region.size.x / precision / 2, z = region.size.y / precision / 2;
                var corners = new[] { center + rotation * new Vector3(-x, 0, -z), center + rotation * new Vector3(-x, 0, z),
                    center + rotation * new Vector3(x, 0, z), center + rotation * new Vector3(x, 0, -z), center + rotation * new Vector3(-x, 0, -z) };
                Handles.DrawAAPolyLine(selected ? 3 : 2, corners);
            }
            Handles.Label(center + Vector3.up * .08f, region.label + (active ? " 生效" : " 未生效"));
        }

        private void EditRegion(SkillDefinition skill, SkillHitRegion region, Vector3 origin, Action changed)
        {
            Vector3 center = Center(region, origin);
            Quaternion rotation = Quaternion.Euler(0, region.yawDegrees, 0);
            float precision = SkillFrameUtility.PositionPrecision;
            EditorGUI.BeginChangeCheck();
            Vector2Int nextCenter = region.center, nextSize = region.size;
            int nextRadius = region.radius, nextYaw = region.yawDegrees, nextAngle = region.sectorAngleDegrees;
            switch (Tool)
            {
                case SkillRegionTool.Move:
                    Vector3 next = DrawXZMoveHandle(center);
                    nextCenter = SkillPropertyPanel.Quantize(new Vector2(next.x - origin.x, next.z - origin.z));
                    break;
                case SkillRegionTool.Rotate:
                    if (region.shape == SkillRegionShape.Rectangle || region.shape == SkillRegionShape.Sector)
                    {
                        Quaternion yaw = Handles.Disc(rotation, center, Vector3.up, HandleUtility.GetHandleSize(center) * .65f, false, 1f);
                        nextYaw = Mathf.RoundToInt(Mathf.DeltaAngle(0, yaw.eulerAngles.y));
                    }
                    break;
                case SkillRegionTool.Resize:
                    if (region.shape == SkillRegionShape.Sector)
                    {
                        Vector3 forward = rotation * Vector3.forward;
                        float handleSize = HandleUtility.GetHandleSize(center) * .08f;
                        Vector3 edge = Handles.Slider(center + forward * region.radius / precision, forward,
                            handleSize, Handles.CubeHandleCap, 0);
                        nextRadius = Mathf.Max(1, Mathf.RoundToInt(Vector3.Dot(edge - center, forward) * precision));
                        Handles.Label(edge + Vector3.up * handleSize, "半径");

                        // 张角手柄放在右侧边之外，与半径手柄区分；沿 XZ 平面拖动，仅回写完整张角。
                        Vector3 boundary = rotation * Quaternion.Euler(0, Mathf.Clamp(region.sectorAngleDegrees, 1, 360) * .5f, 0) * Vector3.forward;
                        Vector3 anglePoint = center + boundary * (Mathf.Max(0, region.radius / precision) + handleSize * 3);
                        EditorGUI.BeginChangeCheck();
                        Vector3 nextAnglePoint = Handles.Slider2D(anglePoint, Vector3.up, Vector3.right, Vector3.forward,
                            handleSize, Handles.DotHandleCap, Vector2.zero);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Vector3 local = Quaternion.Inverse(rotation) * (nextAnglePoint - center);
                            if (local.sqrMagnitude > .000001f)
                                nextAngle = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg) * 2), 1, 360);
                        }
                        Handles.DrawDottedLine(center, anglePoint, 4);
                        Handles.Label(anglePoint + Vector3.up * handleSize, "张角 " + region.sectorAngleDegrees + "°");
                    }
                    else if (region.shape == SkillRegionShape.Circle)
                    {
                        Vector3 edge = Handles.Slider(center + Vector3.right * region.radius / precision, Vector3.right,
                            HandleUtility.GetHandleSize(center) * .08f, Handles.CubeHandleCap, 0);
                        nextRadius = Mathf.Max(1, Mathf.RoundToInt((edge.x - center.x) * precision));
                    }
                    else
                    {
                        Vector3 right = rotation * Vector3.right, forward = rotation * Vector3.forward;
                        float handleSize = HandleUtility.GetHandleSize(center) * .08f;
                        Vector3 x = Handles.Slider(center + right * region.size.x / precision / 2, right, handleSize, Handles.CubeHandleCap, 0);
                        Vector3 z = Handles.Slider(center + forward * region.size.y / precision / 2, forward, handleSize, Handles.CubeHandleCap, 0);
                        nextSize = new Vector2Int(Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(Vector3.Dot(x - center, right)) * 2 * precision)),
                            Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(Vector3.Dot(z - center, forward)) * 2 * precision)));
                    }
                    break;
            }
            if (!EditorGUI.EndChangeCheck()) return;
            Undo.RecordObject(skill, "调整技能攻击区域");
            region.center = nextCenter; region.size = nextSize; region.radius = nextRadius; region.yawDegrees = nextYaw;
            region.sectorAngleDegrees = nextAngle;
            EditorUtility.SetDirty(skill); changed();
        }

        /// <summary>地面移动工具：X/Z 单轴箭头和偏离轴线的平面手柄，不提供 Y 方向操作。</summary>
        private static Vector3 DrawXZMoveHandle(Vector3 center)
        {
            float size = HandleUtility.GetHandleSize(center) * .8f;
            Color previous = Handles.color;
            try
            {
                // 坐标与 region.center 的 XZ 分量一致，不随攻击矩形自身的 yaw 旋转。
                Handles.color = Handles.xAxisColor;
                Vector3 x = Handles.Slider(center, Vector3.right, size, Handles.ArrowHandleCap, 0);
                Handles.Label(center + Vector3.right * size * 1.15f, "X");
                Handles.color = Handles.zAxisColor;
                Vector3 z = Handles.Slider(center, Vector3.forward, size, Handles.ArrowHandleCap, 0);
                Handles.Label(center + Vector3.forward * size * 1.15f, "Z");

                // 平面方块移到两轴之间，避免与箭头的轴线重叠而难以选中。
                Vector3 planeCenter = center + (Vector3.right + Vector3.forward) * size * .25f;
                Handles.color = new Color(1f, .8f, .2f);
                Vector3 plane = Handles.Slider2D(planeCenter, Vector3.up, Vector3.right, Vector3.forward,
                    size * .12f, Handles.RectangleHandleCap, Vector2.zero);

                // 同一时刻仅一个手柄持有鼠标；合并其增量后固定显示高度，再由调用方量化为整数 XZ。
                return new Vector3(x.x + plane.x - planeCenter.x, center.y, z.z + plane.z - planeCenter.z);
            }
            finally { Handles.color = previous; }
        }

        private bool Overlaps(SkillHitRegion region, Vector3 origin)
        {
            if (region.shape == SkillRegionShape.Sector)
            {
                // 扇形预览复用战斗整数几何，包含受击圆擦到侧边/弧端点的情况。
                var attacker = SkillPropertyPanel.Quantize(new Vector2(origin.x, origin.z));
                var target = SkillPropertyPanel.Quantize(new Vector2(TargetPosition.x, TargetPosition.z));
                return CombatCollision.Overlaps(region, attacker, 90, target,
                    Mathf.Max(0, Mathf.RoundToInt(TargetRadius * SkillFrameUtility.PositionPrecision)));
            }
            Vector3 delta = TargetPosition - Center(region, origin);
            delta.y = 0;
            float precision = SkillFrameUtility.PositionPrecision;
            if (region.shape == SkillRegionShape.Circle)
            {
                float sum = TargetRadius + region.radius / precision;
                return delta.sqrMagnitude <= sum * sum;
            }
            Vector3 local = Quaternion.Inverse(Quaternion.Euler(0, region.yawDegrees, 0)) * delta;
            float x = Mathf.Max(0, Mathf.Abs(local.x) - region.size.x / precision / 2);
            float z = Mathf.Max(0, Mathf.Abs(local.z) - region.size.y / precision / 2);
            return x * x + z * z <= TargetRadius * TargetRadius;
        }
    }
}
