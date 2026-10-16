using UnityEditor;
using UnityEngine;
using ZZZ.SkillAuthoring;

namespace ZZZ.SkillEditor
{
    /// <summary>独立的预览控制与配置提示视图。会话、采样和资源清理由技能主窗口负责。</summary>
    public sealed class SkillPreviewWindow : EditorWindow
    {
        [SerializeField] private SkillEditorWindow owner;
        private Vector2 scroll;

        [MenuItem("Tools/Skill Editor/技能预览与提示")]
        private static void OpenMenu() => Open(SkillEditorWindow.FindOpenWindow());

        internal static void Open(SkillEditorWindow source)
        {
            var window = GetWindow<SkillPreviewWindow>("技能预览与提示");
            window.owner = source;
            window.Show();
            window.Repaint();
        }

        private void OnEnable()
        {
            minSize = new Vector2(340, 260);
            SkillEditorWindow.PreviewStateChanged += Repaint;
        }

        private void OnDisable()
        {
            // 关闭辅助窗口只取消订阅；主窗口中的播放和 Scene 手柄仍可继续使用。
            SkillEditorWindow.PreviewStateChanged -= Repaint;
        }

        private void OnGUI()
        {
            if (owner == null) owner = SkillEditorWindow.FindOpenWindow();
            if (owner == null || owner.ActiveSkill == null)
            {
                EditorGUILayout.HelpBox("请在技能编辑器中选择技能；此窗口跟随当前编辑的资产。", MessageType.Info);
                if (GUILayout.Button("打开技能编辑器")) SkillEditorWindow.Open(null);
                return;
            }

            var skill = owner.ActiveSkill;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField(skill, typeof(SkillDefinition), false);
                if (GUILayout.Button("返回时间轴", EditorStyles.toolbarButton, GUILayout.Width(85))) owner.Focus();
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Scene 预览仅在非 Play 模式使用。", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Scene 预览", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("当前游标", $"{owner.CurrentFrame} 帧 / {owner.CurrentFrame * SkillFrameUtility.IntervalMs} ms");
            SkillWindowPreview.DrawPanel(skill, owner.CurrentFrame);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!owner.CanPreview))
                    if (GUILayout.Button(owner.HasPreview ? "重建预览" : "打开 Scene 预览")) owner.OpenPreview();
                using (new EditorGUI.DisabledScope(!owner.HasPreview))
                {
                    if (GUILayout.Button("聚焦角色")) owner.FocusPreview();
                    if (GUILayout.Button("关闭预览")) owner.ClosePreview();
                }
            }

            var handles = owner.SceneHandles;
            owner.PreviewAudio = EditorGUILayout.Toggle("播放预览音效", owner.PreviewAudio);
            EditorGUILayout.LabelField("拖动游标可采样粒子；音效仅随播放预览发声，暂停和拖动时停止。", EditorStyles.wordWrappedMiniLabel);
            DrawEffectTools();
            if (handles != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("攻击区域编辑（当前选中区域）", EditorStyles.boldLabel);
                var region = owner.SelectedRegion;
                EditorGUILayout.LabelField("操作对象", region == null ? "未选中攻击区域"
                    : string.IsNullOrEmpty(region.label) ? "未命名攻击区域" : region.label);
                if (region != null)
                    EditorGUILayout.LabelField("定位方式", region.anchorMode == SkillRegionAnchorMode.CastPosition ? "固定在释放位置"
                        : region.anchorMode == SkillRegionAnchorMode.ActivationPosition ? "固定在区域生效位置（Start Frame）" : "跟随角色");
                if (region == null && owner.SelectedEffect == null)
                    EditorGUILayout.HelpBox("请先在技能轨道中选中攻击区域，再使用 Scene 手柄调整。", MessageType.Info);
                else if (region != null && !owner.HasPreview)
                    EditorGUILayout.HelpBox("打开 Scene 预览后，可拖动选中攻击区域的手柄。", MessageType.Info);
                EditorGUI.BeginChangeCheck();
                using (new EditorGUI.DisabledScope(region == null || !owner.HasPreview))
                    handles.Tool = (SkillRegionTool)GUILayout.Toolbar((int)handles.Tool, new[] { "移动 XZ", "旋转 Y", "尺寸" });
                if (region != null && handles.Tool == SkillRegionTool.Move)
                    EditorGUILayout.LabelField("红色 X 箭头：左右；蓝色 Z 箭头：前后；方块：XZ 平面拖动。", EditorStyles.wordWrappedMiniLabel);
                if (region != null && region.shape == SkillRegionShape.Circle && handles.Tool == SkillRegionTool.Rotate)
                    EditorGUILayout.LabelField("圆形攻击区域不需要旋转。", EditorStyles.wordWrappedMiniLabel);
                if (region != null && region.shape == SkillRegionShape.Sector && handles.Tool == SkillRegionTool.Resize)
                    EditorGUILayout.LabelField("前方方块：调整半径；侧边圆点：调整完整张角。", EditorStyles.wordWrappedMiniLabel);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("判定区域显示（仅影响预览）", EditorStyles.boldLabel);
                handles.DisplayHeight = Mathf.Max(0, EditorGUILayout.FloatField("判定区域显示高度", handles.DisplayHeight));
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("测试目标（受击圆）", EditorStyles.boldLabel);
                handles.TargetRadius = Mathf.Max(.01f, EditorGUILayout.FloatField("测试目标半径", handles.TargetRadius));
                if (EditorGUI.EndChangeCheck()) owner.RefreshPreviewViews();
                EditorGUILayout.LabelField(handles.OverlapLabel ?? "拖动目标圆可观察区域重叠；不执行伤害。", EditorStyles.wordWrappedMiniLabel);
            }
            if (skill.animation?.clip != null)
                EditorGUILayout.LabelField("动画实际数据", $"{skill.animation.clip.length * 1000:F3}ms / {skill.animation.clip.frameRate:F2} FPS");
            var sampler = owner.Sampler;
            EditorGUILayout.LabelField("根位移", sampler?.MotionMetadata != null
                ? $"{sampler.MotionFrameCount} 采样 / 记录时长 {sampler.MotionMetadata.clipLengthMs}ms" : "未绑定或数据无效");
            if (!string.IsNullOrEmpty(owner.PreviewError)) EditorGUILayout.HelpBox(owner.PreviewError, MessageType.Error);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("配置提示（非测试结果）", EditorStyles.boldLabel);
            if (owner.Issues.Count > 0)
                EditorGUILayout.LabelField("点击提示，定位对应轨道或配置模块。", EditorStyles.wordWrappedMiniLabel);
            if (owner.Issues.Count == 0) EditorGUILayout.HelpBox("未发现配置格式问题，仍需人工验收实际效果。", MessageType.Info);
            SkillIssue? clickedIssue = null;
            foreach (var issue in owner.Issues)
            {
                EditorGUILayout.HelpBox(issue.Message, issue.Severity == SkillIssueSeverity.Error ? MessageType.Error : MessageType.Warning);
                // 整张提示卡可点击，保留原有错误/警告图标和自动换行。
                Rect rect = GUILayoutUtility.GetLastRect();
                EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
                if (GUI.Button(rect, new GUIContent("", "点击定位对应轨道或配置模块"), GUIStyle.none))
                    clickedIssue = issue;
            }
            EditorGUILayout.EndScrollView();
            // 完成当前窗口的布局后再切换选中状态，保持 IMGUI 各事件的布局结构一致。
            if (clickedIssue.HasValue) owner.NavigateToIssue(clickedIssue.Value);
        }

        private void DrawEffectTools()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("特效事件编辑（当前选中特效）", EditorStyles.boldLabel);
            var cue = owner.SelectedEffect;
            EditorGUILayout.LabelField("操作对象", cue == null ? "未选中特效事件"
                : (cue.effectPrefab != null ? cue.effectPrefab.name : "未绑定资源") + " / 触发帧 " + cue.frame);
            using (new EditorGUI.DisabledScope(cue == null || !owner.HasPreview || owner.EffectHandles == null))
            {
                EditorGUI.BeginChangeCheck();
                int tool = GUILayout.Toolbar(owner.EffectHandles != null ? (int)owner.EffectHandles.Tool : 0,
                    new[] { "特效移动 XYZ", "特效旋转 XYZ", "特效缩放 XYZ" });
                if (EditorGUI.EndChangeCheck())
                {
                    owner.EffectHandles.Tool = (SkillEffectTool)tool;
                    owner.PausePreviewForEditing();
                    owner.RefreshPreviewViews();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("定位特效触发帧")) owner.SeekSelectedEffect();
                    if (owner.IsPreviewPlaying && GUILayout.Button("暂停并编辑")) owner.PausePreviewForEditing();
                }
            }
            if (cue == null)
                EditorGUILayout.LabelField("在技能轨道中选中 Vfx 事件后，可用 Scene 手柄摆放。", EditorStyles.wordWrappedMiniLabel);
            else if (!owner.HasPreview)
                EditorGUILayout.HelpBox("请先打开 Scene 预览。", MessageType.Info);
            else if (owner.IsPreviewPlaying)
                EditorGUILayout.HelpBox("播放时隐藏特效编辑手柄；暂停后可调整。", MessageType.Info);
            else
            {
                EditorGUILayout.LabelField("手柄修改当前事件的偏移、旋转与缩放，支持 Ctrl+Z。缩放可拖动单轴或中心进行调整，各轴最小 0.01。", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField(cue.followBinding ? "坐标基准：当前绑定节点。" : "坐标基准：触发瞬间的绑定节点。", EditorStyles.wordWrappedMiniLabel);
            }
        }
    }
}
