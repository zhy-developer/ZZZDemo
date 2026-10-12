using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SkillConfig.Editor
{
    public sealed class SkillPreviewPanel : IDisposable
    {
        readonly SkillShapeHandles handles = new SkillShapeHandles();
        readonly SkillVfxHandles vfxHandles = new SkillVfxHandles();
        readonly Action<string> selectItem;
        SkillPreviewSession session;
        GameObject source;
        string catalogKey, failure;
        bool details, hurt;
        int interruptFrame;
        readonly Action<int> setCursor;
        readonly Action repaint;
        Vector2 scroll;
        public SkillPreviewSession Session => session;
        public SkillPreviewPanel(Action<int> setCursor, Action repaint, Action<string> selectItem = null)
        {
            this.setCursor = setCursor; this.repaint = repaint; this.selectItem = selectItem;
            SceneView.duringSceneGui += OnSceneGUI; SkillEditCommands.Changed += handles.Cancel; SkillEditCommands.Changed += vfxHandles.Cancel;
        }
        public void CloseSession() { handles.Cancel(); vfxHandles.Cancel(); if (session != null) { session.Updated -= OnUpdated; session.Dispose(); session = null; } }
        public void Dispose() { CloseSession(); SceneView.duringSceneGui -= OnSceneGUI; SkillEditCommands.Changed -= handles.Cancel; SkillEditCommands.Changed -= vfxHandles.Cancel; }
        public void Seek(int frame) { handles.Cancel(); vfxHandles.Cancel(); if (session == null || session.IsDisposed) return; session.SetPlaying(false); session.Seek(frame); }
        void OnUpdated() { if (session != null && !session.IsDisposed) setCursor(session.Frame); else { handles.Cancel(); vfxHandles.Cancel(); if (session != null) failure=session.Error; } repaint(); }
        void OnSceneGUI(SceneView scene) { if (session != null) { handles.Draw(session, selected, hurt); if (!hurt) vfxHandles.Draw(session, selected); else vfxHandles.Cancel(); } }
        string selected;
        public void Draw(ComboData owner, CharacterSkillCatalog catalog, string selection, int frame)
        {
            selected = selection;
            string key = catalog ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(catalog)) : "";
            if (catalogKey != key)
            {
                CloseSession(); catalogKey = key;
                var binding = SkillEditorWorkspaceState.instance.visualBindings.FirstOrDefault(b => b.catalogGuid == key);
                source = binding == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(binding.sourceGuid));
                if (!source && catalog && catalog.characterId == "miyabi") source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Model/雅/Model/星见雅.fbx");
            }
            if (session != null && session.Owner != owner) CloseSession();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("安全视觉代理", GUILayout.Width(85));
                var next = (GameObject)EditorGUILayout.ObjectField(source, typeof(GameObject), false, GUILayout.Width(170));
                if (next != source)
                {
                    CloseSession(); source = next;
                    var state = SkillEditorWorkspaceState.instance; state.visualBindings.RemoveAll(b => b.catalogGuid == key);
                    if (source) state.visualBindings.Add(new SkillEditorWorkspaceState.VisualBinding { catalogGuid = key, sourceGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)) }); state.Persist();
                }
                bool alive = session != null && !session.IsDisposed;
                using (new EditorGUI.DisabledScope(!source || !owner || owner.skill == null))
                    if (GUILayout.Button(alive ? "关闭预览" : "打开 Scene 预览", EditorStyles.toolbarButton))
                    {
                        if (alive) CloseSession();
                        else
                        {
                            CloseSession(); failure = null;
                            try
                            {
                                session = new SkillPreviewSession(owner, catalog, source); session.Updated += OnUpdated; session.Seek(frame);
                                var view = EditorWindow.GetWindow<SceneView>();
                                view.Frame(new Bounds(session.Proxy.Root.transform.position + Vector3.up, Vector3.one * 3), false);
                            }
                            catch (Exception ex) { failure = ex.Message; CloseSession(); }
                        }
                        GUIUtility.ExitGUI();
                    }
                using (new EditorGUI.DisabledScope(!alive))
                {
                    if (GUILayout.Button(session != null && session.Playing ? "暂停" : "播放", EditorStyles.toolbarButton)) session.SetPlaying(!session.Playing);
                    if (GUILayout.Button("◀", EditorStyles.toolbarButton)) Seek(session.Frame - 1);
                    if (GUILayout.Button("▶", EditorStyles.toolbarButton)) Seek(session.Frame + 1);
                    var speeds = new[] { .25f, .5f, 1f, 2f };
                    int index = Array.IndexOf(speeds, session?.PreviewSpeed ?? 1);
                    int picked = EditorGUILayout.Popup(index, new[] { "0.25×", "0.5×", "1×", "2×" }, GUILayout.Width(65));
                    if (session != null) { session.PreviewSpeed = speeds[Math.Max(0, picked)]; session.Loop = GUILayout.Toggle(session.Loop, "循环", EditorStyles.toolbarButton); }
                    hurt = GUILayout.Toggle(hurt, "编辑 HurtBox", EditorStyles.toolbarButton);
                }
                details = GUILayout.Toggle(details, "诊断 / 位移", EditorStyles.toolbarButton);
            }
            if (!string.IsNullOrEmpty(failure)) EditorGUILayout.HelpBox(failure, MessageType.Error);
            if (session == null || session.IsDisposed) return;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("形状工具", GUILayout.Width(60));
                var tool = UnityEditor.Tools.current;
                int current = tool == Tool.Rotate ? 1 : tool == Tool.Scale ? 2 : 0;
                int nextTool = GUILayout.Toolbar(current, new[] { "移动 (W)", "旋转 (E)", "尺寸 (R)" }, GUILayout.Width(270));
                if (nextTool != current)
                {
                    handles.Cancel(); vfxHandles.Cancel(); UnityEditor.Tools.current = nextTool == 1 ? Tool.Rotate : nextTool == 2 ? Tool.Scale : Tool.Move;
                    SceneView.RepaintAll();
                }
                GUILayout.Label("Scene View 使用 W / E / R；切换工具取消未提交拖动", EditorStyles.miniLabel);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("帧 " + session.Frame + " · " + session.ClipName + " · source " + session.SourceSeconds.ToString("F6") + "s", GUILayout.MinWidth(260));
                interruptFrame = EditorGUILayout.IntField("打断帧", interruptFrame, GUILayout.Width(190));
                if (GUILayout.Button("模拟打断", GUILayout.Width(80))) { handles.Cancel(); vfxHandles.Cancel(); session.Interrupt(interruptFrame); }
                if (GUILayout.Button("重置打断", GUILayout.Width(80))) { handles.Cancel(); vfxHandles.Cancel(); session.ClearInterrupt(); }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                session.LoopStartFrame = EditorGUILayout.IntField("循环起帧",session.LoopStartFrame);
                session.LoopEndFrame = EditorGUILayout.IntField("循环止帧（0=技能末尾）",session.LoopEndFrame);
            }
            EditorGUILayout.LabelField("表现帧 " + session.PresentationFrame + " / 技能帧 " + session.Frame + " · VFX " + session.Presentation.Effects.Count() + " · SFX " + session.Presentation.Audio.Count, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Seek/逐帧静音；播放倍速同步音调；打断后点击播放可试听收尾。循环回绕清理旧表现。",EditorStyles.miniLabel);
            if (session.Presentation.Issues.Count > 0) EditorGUILayout.HelpBox("表现预览有 " + session.Presentation.Issues.Count + " 条诊断，打开“诊断 / 位移”查看并定位条目。",MessageType.Warning);
            if (!string.IsNullOrEmpty(session.Error)) EditorGUILayout.HelpBox(session.Error, MessageType.Error);
            if (!session.Motion.Supported) EditorGUILayout.HelpBox("位移预览不支持此映射，已禁用整条轨迹；动画独立采样。" + string.Join("\n", session.Motion.Diagnostics), MessageType.Warning);
            else EditorGUILayout.LabelField("旧表回放：来源元数据未验证；无 Animator Transition 混合。", EditorStyles.miniLabel);
            if (hurt && catalog && !catalog.hurtBoxConfigured) EditorGUILayout.HelpBox("先在角色配置启用并填写合法 HurtBox，再使用 Handles。", MessageType.Info);
            if (!details) return;
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(100));
            foreach(var issue in session.Presentation.Issues) if(GUILayout.Button(issue.Text,EditorStyles.wordWrappedMiniLabel)) selectItem?.Invoke(issue.ItemId);
            foreach (var message in session.Proxy.Warnings.Concat(session.Motion.Diagnostics)) EditorGUILayout.LabelField(message, EditorStyles.wordWrappedMiniLabel);
            int start = Math.Max(0, session.Frame - 3), end = Math.Min(session.Duration, session.Frame + 3);
            for (int f = start; f <= end; f++)
            {
                var p = session.Motion.Position(f); var d = session.Motion.Delta(f); var q = session.Motion.Position(f + 1);
                EditorGUILayout.LabelField("F" + f + "  Δ(" + d.x + ", " + d.z + ")  帧前(" + p.x + ", " + p.z + ")  帧后/命中(" + q.x + ", " + q.z + ")  sourceEnd=" + session.Motion.SampleEndMs(f) + "ms", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
