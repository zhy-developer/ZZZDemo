using System;
using UnityEditor;
using UnityEngine;
using ZZZ.SkillAuthoring;

namespace ZZZ.SkillEditor
{
    internal enum SkillRegionTool { Move, Rotate, Resize }

    /// <summary>Scene 绘制、XZ 手柄与重叠提示。浮点重叠只用于编辑预览，不作为联机伤害判定。</summary>
    internal sealed class SkillSceneHandles
    {
        public Vector3 TargetPosition = new Vector3(0, 0, 2);
        public float TargetRadius = .4f;
        public float DisplayHeight = .03f;
        public SkillRegionTool Tool;
        public string OverlapLabel { get; private set; }

        public void Draw(SkillDefinition skill, int frame, Vector3 logicPosition, SkillEditorSelection selection,
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
                    bool selected = selection.kind == SkillSelectionKind.Region && selection.index == i;
                    bool active = frame < skill.totalFrames && region.IsActive(frame);
                    if (active) { activeCount++; if (Overlaps(region, logicPosition)) overlaps++; }
                    if (!active && !selected) continue;
                    DrawRegion(region, ground, active, selected);
                    if (!selected)
                    {
                        Vector3 center = Center(region, ground);
                        Handles.color = new Color(1f, .6f, .15f);
                        if (Handles.Button(center, Quaternion.identity, .055f, .08f, Handles.DotHandleCap))
                            select(new SkillEditorSelection(SkillSelectionKind.Region, i));
                    }
                    else EditRegion(skill, region, ground, changed);
                }
                OverlapLabel = overlaps > 0 ? $"{overlaps} 个生效区域覆盖目标（不扣血）" : $"{activeCount} 个生效区域 / 未覆盖目标";
            }
            finally { Handles.color = previous; }
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
            int nextRadius = region.radius, nextYaw = region.yawDegrees;
            switch (Tool)
            {
                case SkillRegionTool.Move:
                    Vector3 next = DrawXZMoveHandle(center);
                    nextCenter = SkillPropertyPanel.Quantize(new Vector2(next.x - origin.x, next.z - origin.z));
                    break;
                case SkillRegionTool.Rotate:
                    if (region.shape == SkillRegionShape.Rectangle)
                    {
                        Quaternion yaw = Handles.Disc(rotation, center, Vector3.up, HandleUtility.GetHandleSize(center) * .65f, false, 1f);
                        nextYaw = Mathf.RoundToInt(Mathf.DeltaAngle(0, yaw.eulerAngles.y));
                    }
                    break;
                case SkillRegionTool.Resize:
                    if (region.shape == SkillRegionShape.Circle)
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
