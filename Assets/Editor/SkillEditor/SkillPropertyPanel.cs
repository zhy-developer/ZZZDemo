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
                    EditorGUILayout.HelpBox("结束收刀动画只在战斗中自然结束且无后续输入时播放，不占用技能时间轴、不产生根位移；移动、攻击和受击可打断。留空则直接回待机。", MessageType.Info);
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
                    DrawWindowHelp(Element(serialized, "windows", selection.index));
                    break;
                case SkillSelectionKind.Presentation:
                    DrawPresentation(serialized, Element(serialized, "presentation", selection.index));
                    break;
                case SkillSelectionKind.HitGroups:
                    EditorGUILayout.HelpBox("勾选命中组的“可被振刀”后，实际接触处于 Parry 窗口的角色才会被振刀。黄光由特效轨单独配置，不决定振刀时机。", MessageType.Info);
                    if (selection.index >= 0)
                    {
                        DrawElement(Element(serialized, "hitGroups", selection.index), $"命中组条目 {selection.index + 1}");
                        EditorGUILayout.LabelField("点击上方“命中组”可查看全部命中组。", EditorStyles.wordWrappedMiniLabel);
                    }
                    else Field(serialized, "hitGroups", "命中组");
                    EditorGUILayout.HelpBox("命中组定义每次伤害、硬直和反馈；命中次数由攻击区域的“命中方式”决定。单次模式整次释放只命中一次；多段模式同组同目标在同一命中帧只结算一次。顿帧保持停用，硬直 -1 会阻止对战初始化。", MessageType.Info);
                    EditorGUILayout.HelpBox("展开命中组的“命中反馈”配置实际命中时的特效和 2D 音效。位置为受击者根节点加偏移，方向沿攻击者朝向；生成后不跟随，打断不会取消，致命一击也会播完。资源留空即可关闭，不需要添加时间轴事件。", MessageType.Info);
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

        private static void DrawWindowHelp(SerializedProperty window)
        {
            if (window == null) return;
            var kind = (SkillWindowKind)window.FindPropertyRelative("kind").intValue;
            string message;
            switch (kind)
            {
                case SkillWindowKind.DodgeCancel:
                    message = "在 [开始帧, 结束帧) 内允许闪避打断当前技能；仍需满足冷却结束、非受击、非闪避中。失败输入不缓存。成功闪避优先于连段和移动取消，并清空旧连段缓存。";
                    break;
                case SkillWindowKind.Invulnerable:
                    message = "在 [开始帧, 结束帧) 内免受攻击区域的伤害、受击打断和命中反馈，不消耗攻击方命中记录。例如 [0,6) 仅第 0～5 帧无敌。不会延长技能或暂停动画；Scene 预览显示当前是否生效。";
                    break;
                case SkillWindowKind.Parry:
                    message = "在空格招架技能中配置。只有可振刀攻击实际碰到自身受击圆，且当前帧属于此区间时才成功；黄光只是预警，不参与判定。一次释放只触发一次成功反馈，无敌优先于招架。";
                    break;
                case SkillWindowKind.ParryCancel:
                    message = "在攻击技能中配置：区间内允许空格打断当前技能进入招架。失败输入不缓存；闪避优先于招架，招架优先于连段。受击硬直中不能招架。";
                    break;
                default:
                    message = "PreInput 缓存一次同一攻击键；ComboExecute 消费缓存或响应当帧按键。闪避 > 招架 > E 蓄力/释放 > 连段 > 移动取消；受击、取消和技能结束清空缓存。闪避/招架中不接连段，可配置 MoveCancel 缩短恢复阶段。";
                    break;
            }
            EditorGUILayout.HelpBox(message, MessageType.Info);
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

        private static void DrawPresentation(SerializedObject serialized, SerializedProperty cue)
        {
            if (cue == null) { EditorGUILayout.LabelField("请选择表现事件。"); return; }
            void Field(string name, string label) => EditorGUILayout.PropertyField(cue.FindPropertyRelative(name), new GUIContent(label), true);
            Field("kind", "事件类型");
            bool vfx = cue.FindPropertyRelative("kind").intValue == (int)SkillPresentationKind.Vfx;
            if (vfx)
            {
                var lifetimeMode = cue.FindPropertyRelative("lifetimeMode");
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(lifetimeMode, new GUIContent("特效存活方式"));
                bool modeChanged = EditorGUI.EndChangeCheck();
                bool range = lifetimeMode.intValue == (int)SkillEffectLifetimeMode.FrameRange;
                // 旧事件切到区间模式时给出一个可编辑的有效初值；后续越界不静默改值，由提示定位。
                if (modeChanged && range && cue.FindPropertyRelative("endFrame").intValue <= cue.FindPropertyRelative("frame").intValue)
                    cue.FindPropertyRelative("endFrame").intValue = cue.FindPropertyRelative("frame").intValue + 1;
                Field("frame", range ? "开始帧（包含）" : "触发帧");
                if (range) Field("endFrame", "结束帧（不包含）");
                else Field("maxLifetimeSeconds", "最长存活（秒）");
                Field("playbackSpeed", "播放速度倍率");
                EditorGUILayout.HelpBox(range
                    ? "到结束帧直接清除剩余粒子；非循环特效可能提前播完。播放速度不会改变区间长度。"
                    : "特效自然播完后回收，循环特效由最长存活时间限制。播放速度不会改变最长存活时间。", MessageType.Info);
            }
            else Field("frame", "触发帧");
            if (vfx) Field("effectPrefab", "粒子特效 Prefab"); else Field("audioClip", "音频片段");
            var skill = serialized.targetObject as SkillDefinition;
            var source = skill?.previewPrefab != null
                ? (string.IsNullOrEmpty(skill.animatorPath) ? skill.previewPrefab.transform : skill.previewPrefab.transform.Find(skill.animatorPath)) : null;
            var binding = cue.FindPropertyRelative("bindingPath");
            if (source != null)
            {
                var nodes = source.GetComponentsInChildren<Transform>(true);
                var labels = new string[nodes.Length + 1]; var paths = new string[nodes.Length];
                labels[0] = "当前路径未找到"; int selected = 0;
                for (int i = 0; i < nodes.Length; i++)
                {
                    paths[i] = AnimationUtility.CalculateTransformPath(nodes[i], source);
                    labels[i + 1] = string.IsNullOrEmpty(paths[i]) ? "角色 Animator 根节点" : paths[i];
                    if (paths[i] == binding.stringValue) selected = i + 1;
                }
                EditorGUI.BeginChangeCheck();
                int next = EditorGUILayout.Popup("绑定节点", selected, labels);
                if (EditorGUI.EndChangeCheck() && next > 0) binding.stringValue = paths[next - 1];
            }
            Field("bindingPath", "节点相对路径"); Field("localOffset", "局部偏移（米）");
            if (vfx)
            {
                Field("localEulerAngles", "局部旋转（度）");
                Field("localScale", "特效缩放 XYZ");
            }
            else Field("volume", "音量");
            Field("followBinding", "跟随绑定节点"); Field("stopOnInterrupt", "被打断时停止");
            if (vfx) EditorGUILayout.HelpBox("选中此事件并打开 Scene 预览后，可拖动特效手柄修改偏移、旋转和缩放。在“预览与提示”窗口切换工具。缩放为额外倍数，各轴至少 0.01；只影响当前事件实例，不修改 Prefab。", MessageType.Info);
            EditorGUILayout.HelpBox("自然模式在技能正常结束后允许播完；区间模式按结束帧清除。受击、移动取消和提前接段按“被打断时停止”清理，未勾选则继续至自身结束。音效为 2D，播放速度设置只用于粒子特效。", MessageType.Info);
        }

        private static void DrawRegion(SerializedProperty region)
        {
            if (region == null) { EditorGUILayout.LabelField("请选择攻击区域。"); return; }
            foreach (string name in new[] { "label", "enabled", "needsReview", "startFrame", "endFrame", "hitGroupId", "shape" })
                EditorGUILayout.PropertyField(region.FindPropertyRelative(name), true);
            EditorGUILayout.Space();
            var anchorMode = region.FindPropertyRelative("anchorMode");
            EditorGUILayout.PropertyField(anchorMode, new GUIContent("区域定位方式"));
            string anchorHelp = anchorMode.intValue == (int)SkillRegionAnchorMode.CastPosition
                ? "锁定本次技能第 0 帧的位置与朝向；后续角色移动、转向不影响此区域。"
                : anchorMode.intValue == (int)SkillRegionAnchorMode.ActivationPosition
                    ? "在区域 Start Frame 锁定位置与朝向，不等待首次命中帧。Scene 可提前查看预计生效位置。"
                    : "区域随角色当前逻辑位置与朝向更新。";
            EditorGUILayout.HelpBox(anchorHelp + " 偏移和旋转相对于该定位基准；技能结束或被打断时，区域仍会关闭。", MessageType.Info);
            EditorGUILayout.LabelField("伤害触发时序", EditorStyles.boldLabel);
            var hitMode = region.FindPropertyRelative("hitMode");
            EditorGUILayout.PropertyField(hitMode, new GUIContent("命中方式"));
            if (hitMode.intValue == (int)SkillHitMode.Frames)
            {
                var hitFrames = region.FindPropertyRelative("hitFrames");
                hitFrames.isExpanded = true;
                EditorGUILayout.PropertyField(hitFrames, new GUIContent("命中帧（技能绝对帧）"), true);
                EditorGUILayout.HelpBox("填写如 7、10、13；仅这些帧检测命中，没碰到不补打。每项须位于区域区间内且不能重复。整体拖动轨道会一起平移命中帧；调整边界不会删除已有帧。", MessageType.Info);
            }
            else if (hitMode.intValue == (int)SkillHitMode.Interval)
            {
                EditorGUILayout.PropertyField(region.FindPropertyRelative("hitIntervalFrames"), new GUIContent("命中间隔（帧）"));
                EditorGUILayout.HelpBox("从 Start Frame 开始，每隔 N 帧检测一次，不包含 End Frame。例如 [7,18)、间隔 3：在 7、10、13、16 帧检测；错过不补打。", MessageType.Info);
            }
            else
                EditorGUILayout.HelpBox("区间内每帧检测，首次碰到时命中；同次释放、同组对同一目标最多一次。", MessageType.Info);
            EditorGUILayout.Space();
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
                if ((SkillRegionShape)region.FindPropertyRelative("shape").intValue == SkillRegionShape.Sector)
                {
                    EditorGUILayout.PropertyField(region.FindPropertyRelative("sectorAngleDegrees"), new GUIContent("扇形完整张角（度）"));
                    EditorGUILayout.PropertyField(region.FindPropertyRelative("yawDegrees"), new GUIContent("绕 Y 轴角度"));
                    EditorGUILayout.HelpBox("偏移 XZ 是扇形顶点，半径是攻击距离；朝角色局部 Z 正方向展开，可用绕 Y 轴角度调整朝向。Scene 的“尺寸”工具可调整半径和张角。", MessageType.Info);
                }
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
