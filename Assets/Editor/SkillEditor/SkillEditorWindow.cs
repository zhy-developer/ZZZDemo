using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZZZ.SkillAuthoring;

namespace ZZZ.SkillEditor
{
    /// <summary>编辑器协调层：持有会话状态并连接各视图，不实现数据转换、碰撞或动画采样算法。</summary>
    public sealed class SkillEditorWindow : EditorWindow
    {
        [SerializeField] private SkillDefinition skill;
        [SerializeField] private int frame;
        [SerializeField] private float pixelsPerFrame = 18;
        [SerializeField] private bool loop;
        [SerializeField] private int loopStart, loopEnd;
        [SerializeField] private SkillEditorSelection selection;
        private SerializedObject serialized;
        private SkillFrameSampler sampler;
        private SkillPreviewStage preview;
        private TextAsset sampledJson;
        private GameObject loadedPrefab;
        private AnimationClip loadedClip;
        private string loadedAnimatorPath;
        private SkillTimelineView timeline;
        private SkillPropertyPanel properties;
        private SkillSceneHandles sceneHandles;
        private SkillEffectSceneHandles effectHandles;
        private List<SkillIssue> issues = new List<SkillIssue>();
        private bool playing;
        [SerializeField] private bool previewAudio = true;
        internal bool PreviewAudio
        {
            get => previewAudio;
            set { previewAudio = value; if (!value) preview?.StopAudio(); }
        }
        private double lastTime, accumulator;
        private string previewError;

        // 两个窗口共享此处的制作会话；辅助窗口不创建第二套采样器或预览 Stage。
        internal static event Action PreviewStateChanged;
        internal SkillDefinition ActiveSkill => skill;
        internal int CurrentFrame => frame;
        internal SkillFrameSampler Sampler => sampler;
        internal SkillSceneHandles SceneHandles => sceneHandles;
        internal SkillEffectSceneHandles EffectHandles => effectHandles;
        internal bool IsPreviewPlaying => playing;
        internal SkillPresentationCue SelectedEffect
        {
            get
            {
                if (skill == null || selection.kind != SkillSelectionKind.Presentation || skill.presentation == null
                    || selection.index < 0 || selection.index >= skill.presentation.Count) return null;
                var cue = skill.presentation[selection.index];
                return cue != null && cue.kind == SkillPresentationKind.Vfx ? cue : null;
            }
        }
        internal void SeekSelectedEffect() { if (SelectedEffect != null) Seek(SelectedEffect.frame); }
        internal void PausePreviewForEditing() => Seek(frame);
        internal SkillHitRegion SelectedRegion => skill != null && selection.kind == SkillSelectionKind.Region
            && skill.hitRegions != null && selection.index >= 0 && selection.index < skill.hitRegions.Count
            ? skill.hitRegions[selection.index] : null;
        internal IReadOnlyList<SkillIssue> Issues => issues;
        internal string PreviewError => previewError;
        internal bool HasPreview => preview != null;
        internal bool CanPreview => skill != null && !HasErrors() && !EditorApplication.isPlayingOrWillChangePlaymode;

        internal static SkillEditorWindow FindOpenWindow()
        {
            var windows = Resources.FindObjectsOfTypeAll<SkillEditorWindow>();
            return windows.Length > 0 ? windows[0] : null;
        }

        internal void FocusPreview() { if (preview != null) preview.FrameScene(); }
        internal void RefreshPreviewViews() { Repaint(); SceneView.RepaintAll(); PreviewStateChanged?.Invoke(); }

        [MenuItem("Tools/Skill Editor/技能编辑器")]
        private static void OpenMenu() => Open(Selection.activeObject as SkillDefinition);

        public static void Open(SkillDefinition value)
        {
            var window = GetWindow<SkillEditorWindow>("技能编辑器");
            window.minSize = new Vector2(820, 280);
            if (value != null) window.SetSkill(value);
            window.Show();
        }

        [OnOpenAsset]
        private static bool OnOpenAsset(int instanceId, int line)
        {
            if (!(EditorUtility.InstanceIDToObject(instanceId) is SkillDefinition value)) return false;
            Open(value);
            return true;
        }

        private void OnEnable()
        {
            minSize = new Vector2(820, 280);
            timeline = new SkillTimelineView(); properties = new SkillPropertyPanel(); sceneHandles = new SkillSceneHandles();
            effectHandles = new SkillEffectSceneHandles();
            EditorApplication.update += TickPreview;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.projectChanged += OnProjectChanged;
            AssemblyReloadEvents.beforeAssemblyReload += ClosePreview;
            Undo.undoRedoPerformed += OnUndoRedo;
            SceneView.duringSceneGui += DrawScene;
            if (skill != null) SetSkill(skill);
        }

        private void OnDisable()
        {
            timeline?.CancelDrag();
            EditorApplication.update -= TickPreview;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.projectChanged -= OnProjectChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= ClosePreview;
            Undo.undoRedoPerformed -= OnUndoRedo;
            SceneView.duringSceneGui -= DrawScene;
            ClosePreview();
            serialized?.Dispose(); serialized = null;
            PreviewStateChanged?.Invoke();
        }

        private void SetSkill(SkillDefinition value)
        {
            timeline?.CancelDrag();
            ClosePreview();
            serialized?.Dispose();
            skill = value; frame = 0; previewError = null;
            selection = new SkillEditorSelection(SkillSelectionKind.General);
            serialized = skill != null ? new SerializedObject(skill) : null;
            loopStart = 0; loopEnd = skill != null ? Mathf.Max(1, skill.totalFrames) : 1;
            RefreshData(true);
            Repaint();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var next = (SkillDefinition)EditorGUILayout.ObjectField(skill, typeof(SkillDefinition), false, GUILayout.MinWidth(180));
                if (next != skill) SetSkill(next);
                if (GUILayout.Button("新建", EditorStyles.toolbarButton, GUILayout.Width(45))) CreateAsset();
                if (GUILayout.Button("预览与提示", EditorStyles.toolbarButton, GUILayout.Width(85))) SkillPreviewWindow.Open(this);
                using (new EditorGUI.DisabledScope(skill == null))
                {
                    if (GUILayout.Button("保存草稿", EditorStyles.toolbarButton, GUILayout.Width(75))) SaveDraft();
                    if (GUILayout.Button("显示资产", EditorStyles.toolbarButton, GUILayout.Width(70))) EditorGUIUtility.PingObject(skill);
                }
            }
            if (skill == null)
            {
                EditorGUILayout.HelpBox("选择已有技能资产，或点击“新建”开始制作技能。", MessageType.Info);
                return;
            }
            if (serialized == null) serialized = new SerializedObject(skill);
            if (sampler == null) RefreshData(true);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("技能制作与 Scene 预览仅在非 Play 模式使用。", MessageType.Info);
                return;
            }
            // 主窗口可停靠在 Scene 上方：左侧时间轴、右侧属性。两栏各自滚动，避免轨道被属性挤到底部。
            float propertyWidth = Mathf.Clamp(position.width * .32f, 300, 440);
            float timelineHeight = Mathf.Max(80, position.height - 115);
            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width - propertyWidth - 12)))
                {
                    DrawTransport();
                    DrawTrackToolbar();
                    timeline.Draw(skill, sampler, frame, pixelsPerFrame, selection, Select, next => Seek(next), RefreshAfterEdit, timelineHeight);
                    EditorGUILayout.LabelField("拖动片段或两端调整帧区间。", EditorStyles.miniLabel);
                }
                GUILayout.Box(GUIContent.none, GUILayout.Width(1), GUILayout.ExpandHeight(true));
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(propertyWidth)))
                {
                    DrawSelectionToolbar();
                    if (properties.Draw(serialized, selection)) RefreshData();
                }
            }
        }

        private void DrawTransport()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(preview == null || HasErrors()))
                    if (GUILayout.Button(playing ? "暂停" : "播放预览", GUILayout.Width(75)))
                    {
                        playing = !playing; accumulator = 0; lastTime = EditorApplication.timeSinceStartup;
                        if (playing && frame >= PreviewEnd) Seek(loop ? loopStart : 0, false);
                        Sample();
                    }
                if (GUILayout.Button("|◀", GUILayout.Width(32))) Seek(0);
                if (GUILayout.Button("◀", GUILayout.Width(30))) Seek(frame - 1);
                if (GUILayout.Button("▶", GUILayout.Width(30))) Seek(frame + 1);
                int next = EditorGUILayout.IntSlider(frame, 0, Mathf.Clamp(skill.totalFrames, 1, SkillFrameUtility.MaxAuthoringFrames));
                if (next != frame) Seek(next);
                GUILayout.Label($"{frame * SkillFrameUtility.IntervalMs} ms", GUILayout.Width(75));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                loop = GUILayout.Toggle(loop, "循环区间", GUILayout.Width(80));
                loopStart = EditorGUILayout.IntField(loopStart, GUILayout.Width(48));
                GUILayout.Label("至", GUILayout.Width(18));
                loopEnd = EditorGUILayout.IntField(loopEnd, GUILayout.Width(48));
                loopStart = Mathf.Clamp(loopStart, 0, Math.Max(0, skill.totalFrames - 1));
                loopEnd = Mathf.Clamp(loopEnd, loopStart + 1, Math.Max(1, skill.totalFrames));
                GUILayout.FlexibleSpace();
                GUILayout.Label("时间轴缩放", GUILayout.Width(70));
                pixelsPerFrame = GUILayout.HorizontalSlider(pixelsPerFrame, 4, 40, GUILayout.Width(100));
            }
        }

        private void DrawSelectionToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("基本信息")) Select(new SkillEditorSelection(SkillSelectionKind.General));
                if (GUILayout.Button("命中组")) Select(new SkillEditorSelection(SkillSelectionKind.HitGroups));
            }
        }

        private void DrawTrackToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("技能轨道", GUILayout.Width(60));
                if (EditorGUILayout.DropdownButton(new GUIContent("添加轨道"), FocusType.Passive, EditorStyles.toolbarDropDown, GUILayout.Width(105)))
                    ShowAddTrackMenu(GUILayoutUtility.GetLastRect());
                GUILayout.FlexibleSpace();
                bool removable = selection.kind == SkillSelectionKind.Region || selection.kind == SkillSelectionKind.Window || selection.kind == SkillSelectionKind.Presentation;
                using (new EditorGUI.DisabledScope(!removable)) if (GUILayout.Button("删除选中片段", EditorStyles.toolbarButton)) RemoveSelected();
            }
        }

        private void ShowAddTrackMenu(Rect buttonRect)
        {
            var menu = new GenericMenu();
            var menuSkill = skill;
            // 菜单回调在选择后执行：若期间切换技能或进入 Play，不能误改另一份资产。
            void Add(string label, Action action) => menu.AddItem(new GUIContent(label), false, () =>
            {
                if (this != null && menuSkill != null && skill == menuSkill && !EditorApplication.isPlayingOrWillChangePlaymode) action();
            });
            Add("攻击区域/矩形", () => AddRegion(SkillRegionShape.Rectangle));
            Add("攻击区域/圆形", () => AddRegion(SkillRegionShape.Circle));
            Add("攻击区域/扇形", () => AddRegion(SkillRegionShape.Sector));
            Add("窗口/预输入", () => AddWindow(SkillWindowKind.PreInput));
            Add("窗口/连段执行", () => AddWindow(SkillWindowKind.ComboExecute));
            Add("窗口/移动取消", () => AddWindow(SkillWindowKind.MoveCancel));
            Add("窗口/闪避取消 DodgeCancel", () => AddWindow(SkillWindowKind.DodgeCancel));
            Add("窗口/无敌 Invulnerable", () => AddWindow(SkillWindowKind.Invulnerable));
            Add("表现事件/特效", () => AddPresentation(SkillPresentationKind.Vfx));
            Add("表现事件/武器音效", () => AddPresentation(SkillPresentationKind.WeaponAudio));
            Add("表现事件/角色语音", () => AddPresentation(SkillPresentationKind.VoiceAudio));
            menu.AddSeparator("");
            menu.AddDisabledItem(new GUIContent("动画与根位移为固定轨道"));
            menu.DropDown(buttonRect);
        }

        private void AddRegion(SkillRegionShape shape)
        {
            Undo.RecordObject(skill, "添加攻击区域");
            if (skill.hitGroups == null) skill.hitGroups = new List<SkillHitGroup>();
            if (skill.hitRegions == null) skill.hitRegions = new List<SkillHitRegion>();
            if (skill.hitGroups.Count == 0) skill.hitGroups.Add(new SkillHitGroup());
            int start = Mathf.Clamp(frame, 0, Math.Max(0, skill.totalFrames - 1));
            skill.hitRegions.Add(new SkillHitRegion { shape = shape, startFrame = start, endFrame = start + 1,
                hitGroupId = skill.hitGroups[0]?.id ?? 1, label = "攻击区域 " + (skill.hitRegions.Count + 1) });
            Select(new SkillEditorSelection(SkillSelectionKind.Region, skill.hitRegions.Count - 1)); RefreshAfterEdit();
        }

        private void AddWindow(SkillWindowKind kind)
        {
            Undo.RecordObject(skill, "添加技能窗口");
            if (skill.windows == null) skill.windows = new List<SkillWindow>();
            int start = Mathf.Clamp(frame, 0, Math.Max(0, skill.totalFrames - 1));
            skill.windows.Add(new SkillWindow { kind = kind, startFrame = start, endFrame = Math.Max(start + 1, skill.totalFrames) });
            Select(new SkillEditorSelection(SkillSelectionKind.Window, skill.windows.Count - 1)); RefreshAfterEdit();
        }

        private void AddPresentation(SkillPresentationKind kind)
        {
            Undo.RecordObject(skill, "添加表现事件");
            if (skill.presentation == null) skill.presentation = new List<SkillPresentationCue>();
            int start = Mathf.Clamp(frame, 0, Math.Max(0, skill.totalFrames - 1));
            skill.presentation.Add(new SkillPresentationCue { kind = kind, frame = start, endFrame = start + 1 });
            Select(new SkillEditorSelection(SkillSelectionKind.Presentation, skill.presentation.Count - 1)); RefreshAfterEdit();
        }

        private void RemoveSelected()
        {
            Undo.RecordObject(skill, "删除技能片段");
            int index = selection.index;
            if (selection.kind == SkillSelectionKind.Region && index >= 0 && index < skill.hitRegions.Count) skill.hitRegions.RemoveAt(index);
            if (selection.kind == SkillSelectionKind.Window && index >= 0 && index < skill.windows.Count) skill.windows.RemoveAt(index);
            if (selection.kind == SkillSelectionKind.Presentation && index >= 0 && index < skill.presentation.Count) skill.presentation.RemoveAt(index);
            Select(new SkillEditorSelection(SkillSelectionKind.General)); RefreshAfterEdit();
        }

        internal void NavigateToIssue(SkillIssue issue)
        {
            if (skill == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            SkillSelectionKind kind;
            int? targetFrame = null;
            int index = issue.Index;
            switch (issue.Target)
            {
                case SkillIssueTarget.Animation:
                    kind = SkillSelectionKind.Animation; index = -1;
                    targetFrame = skill.animation?.startFrame ?? 0;
                    break;
                case SkillIssueTarget.Motion:
                    kind = SkillSelectionKind.Motion; index = -1;
                    targetFrame = skill.motion?.startFrame ?? 0;
                    break;
                case SkillIssueTarget.HitRegion:
                    kind = SkillSelectionKind.Region;
                    if (skill.hitRegions != null && index >= 0 && index < skill.hitRegions.Count)
                        targetFrame = skill.hitRegions[index]?.startFrame ?? 0;
                    break;
                case SkillIssueTarget.Window:
                    kind = SkillSelectionKind.Window;
                    if (skill.windows != null && index >= 0 && index < skill.windows.Count)
                        targetFrame = skill.windows[index]?.startFrame ?? 0;
                    break;
                case SkillIssueTarget.Presentation:
                    kind = SkillSelectionKind.Presentation;
                    if (skill.presentation != null && index >= 0 && index < skill.presentation.Count)
                        targetFrame = skill.presentation[index]?.frame ?? 0;
                    break;
                case SkillIssueTarget.HitGroup: kind = SkillSelectionKind.HitGroups; break;
                default: kind = SkillSelectionKind.General; index = -1; break;
            }
            // 跳转只调整编辑会话：暂停预览、选中配置并滚动视图，不修复数据或开启新的预览。
            timeline.CancelDrag();
            Select(new SkillEditorSelection(kind, index));
            properties.RevealSelection();
            timeline.RevealSelection(selection);
            Seek(targetFrame ?? frame);
            Focus();
        }

        private void Select(SkillEditorSelection value)
        {
            selection = value;
            // 选中特效即固定当前游标，手柄拖动期间绑定骨骼不能继续随播放移动。
            if (SelectedEffect != null && playing) Seek(frame);
            RefreshPreviewViews();
        }
        private void RefreshAfterEdit() { EditorUtility.SetDirty(skill); RefreshData(); }

        private void RefreshData(bool forceMotion = false)
        {
            if (skill == null) { sampler = null; issues.Clear(); PreviewStateChanged?.Invoke(); return; }
            var json = skill.motion?.settings?.json;
            if (forceMotion || sampler == null || sampledJson != json) { sampler = new SkillFrameSampler(skill); sampledJson = json; }
            issues = SkillValidation.Collect(skill, sampler);
            if (preview != null && (loadedPrefab != skill.previewPrefab || loadedClip != skill.animation?.clip || loadedAnimatorPath != skill.animatorPath))
            {
                if (HasErrors()) ClosePreview(); else LoadPreview();
            }
            frame = Mathf.Clamp(frame, 0, Mathf.Clamp(skill.totalFrames, 1, SkillFrameUtility.MaxAuthoringFrames));
            if (HasErrors()) { playing = false; preview?.StopAudio(); }
            Sample(true); Repaint(); PreviewStateChanged?.Invoke();
        }

        private bool HasErrors() => issues.Exists(issue => issue.Severity == SkillIssueSeverity.Error);
        private int PreviewEnd => loop ? loopEnd : Mathf.Clamp(skill.totalFrames, 1, SkillFrameUtility.MaxAuthoringFrames);

        private void Seek(int next, bool pause = true)
        {
            if (skill == null) return;
            if (pause) { playing = false; accumulator = 0; }
            frame = Mathf.Clamp(next, 0, Mathf.Clamp(skill.totalFrames, 1, SkillFrameUtility.MaxAuthoringFrames));
            Sample(true); Repaint(); PreviewStateChanged?.Invoke();
        }

        private void Sample(bool reset = false)
        {
            if (preview == null || skill == null || sampler == null || HasErrors()) return;
            try
            {
                preview.Sample(skill, sampler, SkillFrameUtility.FrameSeconds(frame) + (playing ? accumulator : 0),
                    playing && previewAudio, reset);
                previewError = preview.Error;
            }
            catch (Exception exception) { previewError = exception.Message; playing = false; preview.StopAudio(); }
            SceneView.RepaintAll();
        }

        internal void OpenPreview()
        {
            if (skill == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            ClosePreview();
            RefreshData(true);
            if (HasErrors()) return;
            preview = CreateInstance<SkillPreviewStage>();
            preview.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                StageUtility.GoToStage(preview, true);
                if (LoadPreview()) { Sample(); preview.FrameScene(); }
                else ClosePreview();
            }
            catch (Exception exception) { previewError = exception.Message; ClosePreview(); }
            RefreshPreviewViews();
        }

        private bool LoadPreview()
        {
            playing = false;
            loadedPrefab = skill.previewPrefab; loadedClip = skill.animation?.clip; loadedAnimatorPath = skill.animatorPath;
            bool result = preview.Load(skill); previewError = preview.Error;
            return result;
        }

        internal static void NotifyAssetChanged(SkillDefinition value)
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<SkillEditorWindow>())
                if (window.skill == value) window.RefreshData(true);
        }

        internal void ClosePreview()
        {
            playing = false; accumulator = 0;
            if (preview == null) return;
            // 先释放模型/Playable；即使用户已切换到别的 Stage，也不会留下后台采样对象。
            preview.ReleaseResources();
            if (StageUtility.GetCurrentStage() == preview) StageUtility.GoToMainStage();
            preview = null;
            SceneView.RepaintAll();
            PreviewStateChanged?.Invoke();
        }

        private void TickPreview()
        {
            if (!playing || skill == null) return;
            if (preview == null || StageUtility.GetCurrentStage() != preview || EditorApplication.isPlayingOrWillChangePlaymode || HasErrors())
            { playing = false; preview?.StopAudio(); return; }
            double now = EditorApplication.timeSinceStartup;
            accumulator += Math.Min(.25, Math.Max(0, now - lastTime)); lastTime = now;
            int steps = (int)(accumulator / SkillFrameUtility.SecondsPerFrame);
            accumulator -= steps * SkillFrameUtility.SecondsPerFrame;
            int next = frame + steps;
            bool wrapped = loop && next >= PreviewEnd;
            if (next >= PreviewEnd)
            {
                if (loop) next = loopStart + (next - loopStart) % Math.Max(1, loopEnd - loopStart);
                else { next = PreviewEnd; playing = false; accumulator = 0; }
            }
            // 游标仍显示整数逻辑帧，姿势/粒子以编辑器刷新时间连续采样，不能每 33ms 才刷新一次。
            frame = next;
            Sample(wrapped); Repaint(); PreviewStateChanged?.Invoke();
        }

        private void DrawScene(SceneView view)
        {
            if (preview == null || skill == null || StageUtility.GetCurrentStage() != preview) return;
            sceneHandles.Draw(skill, sampler, frame, preview.LogicPosition, selection, Select, RefreshAfterEdit);
            SkillWindowPreview.DrawScene(skill, frame, preview.LogicPosition, sceneHandles.DisplayHeight);
            var effect = SelectedEffect;
            if (effect != null && !playing && sampler != null && !HasErrors()
                && preview.TryGetEffectBinding(skill, sampler, effect, out var matrix, out var rotation))
                effectHandles.Draw(skill, effect, matrix, rotation, RefreshAfterEdit);
            Repaint();
            PreviewStateChanged?.Invoke();
        }

        private void OnPlayModeChanged(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingEditMode) ClosePreview(); }
        private void OnUndoRedo() { if (skill != null) { serialized?.Update(); RefreshData(true); } }
        private void OnProjectChanged() { if (skill != null) RefreshData(true); }

        private void SaveDraft()
        {
            serialized?.ApplyModifiedProperties();
            RefreshData(true);
            // 草稿必须允许保存未完成的摆盒/资源绑定；格式错误在面板持续显示，不宣称可用于战斗。
            EditorUtility.SetDirty(skill); AssetDatabase.SaveAssetIfDirty(skill);
            ShowNotification(new GUIContent(HasErrors() ? "草稿已保存，仍有配置错误" : "技能草稿已保存"));
        }

        private void CreateAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject("新建技能", "Skill", "asset", "保存技能制作资产");
            if (string.IsNullOrEmpty(path)) return;
            var value = CreateInstance<SkillDefinition>();
            value.skillId = System.IO.Path.GetFileNameWithoutExtension(path);
            value.displayName = value.skillId;
            AssetDatabase.CreateAsset(value, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssetIfDirty(value);
            SetSkill(value);
        }
    }
}
