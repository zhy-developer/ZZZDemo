using UnityEditor;
using UnityEngine;
using ZZZ.SkillAuthoring;

namespace ZZZ.SkillEditor
{
    /// <summary>只绘制选中配置；SerializedObject 统一负责属性编辑的 Undo 与序列化。</summary>
    internal sealed class SkillPropertyPanel
    {
        private Vector2 scroll;
        private bool revealSelection;

        public void RevealSelection() { scroll = Vector2.zero; revealSelection = true; }

        public bool Draw(SerializedObject serialized, SkillEditorSelection selection)
        {
            serialized.Update();
            if (revealSelection)
            {
                // 定位时展开固定轨道。展开状态只属于编辑器，不修改技能配置。
                string path = selection.kind == SkillSelectionKind.Animation ? "animation"
                    : selection.kind == SkillSelectionKind.Motion ? "motion" : null;
                if (path != null) serialized.FindProperty(path).isExpanded = true;
                revealSelection = false;
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            switch (selection.kind)
            {
                case SkillSelectionKind.Animation:
                    Field(serialized, "animation", "动画轨");
                    break;
                case SkillSelectionKind.Motion:
                    Field(serialized, "motion", "根位移轨");
                    EditorGUILayout.HelpBox("一个 JSON 采样对应一个逻辑帧。修改动画速度、裁剪或起点后必须重新核对烘焙数据。", MessageType.Info);
                    break;
                case SkillSelectionKind.Region:
                    DrawRegion(Element(serialized, "hitRegions", selection.index));
                    break;
                case SkillSelectionKind.Window:
                    DrawElement(Element(serialized, "windows", selection.index), "窗口");
                    EditorGUILayout.HelpBox("PreInput 缓存一次同一攻击键；ComboExecute 消费缓存或响应当帧按键。在 ComboExecute 的“下一段技能”下拉框选择已注册目标。连段优先于同帧移动取消；受击、取消和技能结束清空缓存。", MessageType.Info);
                    break;
                case SkillSelectionKind.Presentation:
                    DrawElement(Element(serialized, "presentation", selection.index), "表现事件");
                    EditorGUILayout.HelpBox("当前保存资源和时点；Scene 预览和本轮帧战斗尚不播放音效或特效。", MessageType.Info);
                    break;
                case SkillSelectionKind.HitGroups:
                    if (selection.index >= 0)
                    {
                        DrawElement(Element(serialized, "hitGroups", selection.index), $"命中组条目 {selection.index + 1}");
                        EditorGUILayout.LabelField("点击上方“命中组”可查看全部命中组。", EditorStyles.wordWrappedMiniLabel);
                    }
                    else Field(serialized, "hitGroups", "命中组");
                    EditorGUILayout.HelpBox("同次释放、同组区域对同一目标只命中一次。伤害和受击硬直由逻辑帧结算；顿帧暂时全局停用，配置值仅保留。硬直 -1 会阻止对战初始化。", MessageType.Info);
                    break;
                case SkillSelectionKind.ImportedEvents:
                    using (new EditorGUI.DisabledScope(true))
                    {
                        Field(serialized, "legacySource", "旧数据来源");
                        if (selection.index >= 0)
                            DrawElement(Element(serialized, "importedEvents", selection.index), $"迁移事件条目 {selection.index + 1}");
                        else Field(serialized, "importedEvents", "实际导入的动画事件");
                        Field(serialized, "importNotes", "迁移说明");
                    }
                    if (selection.index >= 0)
                        EditorGUILayout.LabelField("点击上方“迁移记录”可查看全部事件。", EditorStyles.wordWrappedMiniLabel);
                    break;
                default:
                    Field(serialized, "skillId", "技能 ID");
                    Field(serialized, "displayName", "显示名称");
                    Field(serialized, "character", "角色");
                    Field(serialized, "category", "技能类别");
                    Field(serialized, "totalFrames", "总逻辑帧数");
                    Field(serialized, "previewPrefab", "预览 Prefab");
                    DrawAnimatorPath(serialized);
                    EditorGUILayout.LabelField("逻辑间隔", SkillFrameUtility.IntervalMs + " ms（项目统一）");
                    EditorGUILayout.LabelField("坐标精度", SkillFrameUtility.PositionPrecision + " = 1 Unity 单位");
                    break;
            }
            EditorGUILayout.EndScrollView();
            return serialized.ApplyModifiedProperties();
        }

        private static void DrawAnimatorPath(SerializedObject serialized)
        {
            var prefab = serialized.FindProperty("previewPrefab").objectReferenceValue as GameObject;
            var path = serialized.FindProperty("animatorPath");
            var animators = prefab != null ? prefab.GetComponentsInChildren<Animator>(true) : new Animator[0];
            if (animators.Length > 0)
            {
                var labels = new string[animators.Length + 1];
                var paths = new string[animators.Length];
                labels[0] = "选择 Animator 节点";
                int selected = 0;
                for (int i = 0; i < animators.Length; i++)
                {
                    paths[i] = AnimationUtility.CalculateTransformPath(animators[i].transform, prefab.transform);
                    labels[i + 1] = string.IsNullOrEmpty(paths[i]) ? "根节点" : paths[i];
                    if (paths[i] == path.stringValue) selected = i + 1;
                }
                EditorGUI.BeginChangeCheck();
                int next = EditorGUILayout.Popup("Animator 节点", selected, labels);
                if (EditorGUI.EndChangeCheck() && next > 0) path.stringValue = paths[next - 1];
            }
            EditorGUILayout.PropertyField(path, new GUIContent("相对路径"));
        }

        private static void DrawRegion(SerializedProperty region)
        {
            if (region == null) { EditorGUILayout.LabelField("请选择攻击区域。"); return; }
            foreach (string name in new[] { "label", "enabled", "needsReview", "startFrame", "endFrame", "hitGroupId", "shape" })
                EditorGUILayout.PropertyField(region.FindPropertyRelative(name), true);
            float scale = SkillFrameUtility.PositionPrecision;
            var center = region.FindPropertyRelative("center");
            var size = region.FindPropertyRelative("size");
            EditorGUI.BeginChangeCheck();
            Vector2 nextCenter = EditorGUILayout.Vector2Field("偏移 X / Z（Unity 单位）", (Vector2)center.vector2IntValue / scale);
            if (EditorGUI.EndChangeCheck()) center.vector2IntValue = Quantize(nextCenter);
            if ((SkillRegionShape)region.FindPropertyRelative("shape").enumValueIndex == SkillRegionShape.Rectangle)
            {
                EditorGUI.BeginChangeCheck();
                Vector2 nextSize = EditorGUILayout.Vector2Field("完整宽 / 长（Unity 单位）", (Vector2)size.vector2IntValue / scale);
                if (EditorGUI.EndChangeCheck()) size.vector2IntValue = Quantize(nextSize);
                EditorGUILayout.PropertyField(region.FindPropertyRelative("yawDegrees"), new GUIContent("绕 Y 轴角度"));
            }
            else
            {
                var radius = region.FindPropertyRelative("radius");
                EditorGUI.BeginChangeCheck();
                float next = EditorGUILayout.FloatField("半径（Unity 单位）", radius.intValue / scale);
                if (EditorGUI.EndChangeCheck()) radius.intValue = Mathf.RoundToInt(next * scale);
            }
            EditorGUILayout.HelpBox("X = 左右，Z = 前后。Scene 显示高度不参与碰撞。区间包含开始帧，不包含结束帧。", MessageType.Info);
        }

        internal static Vector2Int Quantize(Vector2 value) => new Vector2Int(
            Mathf.RoundToInt(value.x * SkillFrameUtility.PositionPrecision), Mathf.RoundToInt(value.y * SkillFrameUtility.PositionPrecision));

        private static SerializedProperty Element(SerializedObject serialized, string name, int index)
        {
            var array = serialized.FindProperty(name);
            return index >= 0 && index < array.arraySize ? array.GetArrayElementAtIndex(index) : null;
        }

        private static void DrawElement(SerializedProperty element, string label)
        {
            if (element == null) { EditorGUILayout.LabelField("请选择" + label); return; }
            element.isExpanded = true;
            EditorGUILayout.PropertyField(element, new GUIContent(label), true);
        }

        private static void Field(SerializedObject serialized, string name, string label)
        {
            var property = serialized.FindProperty(name);
            EditorGUILayout.PropertyField(property, new GUIContent(label), true);
        }
    }
}
