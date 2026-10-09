using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SkillConfig.Editor
{
    public sealed class SkillPreviewPanel : IDisposable
    {
        readonly SkillShapeHandles handles = new SkillShapeHandles();
        SkillPreviewSession session;
        GameObject source;
        string catalogKey, failure;
        bool details, hurt;
        int interruptFrame;
        readonly Action<int> setCursor;
        readonly Action repaint;
        Vector2 scroll;
        public SkillPreviewSession Session => session;
        public SkillPreviewPanel(Action<int> setCursor, Action repaint)
        {
            this.setCursor = setCursor; this.repaint = repaint;
            SceneView.duringSceneGui += OnSceneGUI; SkillEditCommands.Changed += handles.Cancel;
        }
        public void CloseSession() { handles.Cancel(); if (session != null) { session.Updated -= OnUpdated; session.Dispose(); session = null; } }
        public void Dispose() { CloseSession(); SceneView.duringSceneGui -= OnSceneGUI; SkillEditCommands.Changed -= handles.Cancel; }
        public void Seek(int frame) { handles.Cancel(); if (session == null || session.IsDisposed) return; session.SetPlaying(false); session.Seek(frame); }
        void OnUpdated() { if (session != null && !session.IsDisposed) setCursor(session.Frame); else handles.Cancel(); repaint(); }
        void OnSceneGUI(SceneView scene) { if (session != null) handles.Draw(session, selected, hurt); }
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
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("帧 " + session.Frame + " · " + session.ClipName + " · source " + session.SourceSeconds.ToString("F6") + "s", GUILayout.MinWidth(260));
                interruptFrame = EditorGUILayout.IntField("打断帧", interruptFrame, GUILayout.Width(190));
                if (GUILayout.Button("模拟打断", GUILayout.Width(80))) { handles.Cancel(); session.Interrupt(interruptFrame); }
                if (GUILayout.Button("重置打断", GUILayout.Width(80))) { handles.Cancel(); session.ClearInterrupt(); }
            }
            if (!string.IsNullOrEmpty(session.Error)) EditorGUILayout.HelpBox(session.Error, MessageType.Error);
            if (!session.Motion.Supported) EditorGUILayout.HelpBox("位移预览不支持此映射，已禁用整条轨迹；动画独立采样。" + string.Join("\n", session.Motion.Diagnostics), MessageType.Warning);
            else EditorGUILayout.LabelField("旧表回放：来源元数据未验证；无 Animator Transition 混合。", EditorStyles.miniLabel);
            if (hurt && catalog && !catalog.hurtBoxConfigured) EditorGUILayout.HelpBox("先在角色配置启用并填写合法 HurtBox，再使用 Handles。", MessageType.Info);
            if (!details) return;
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(100));
            foreach (var message in session.Proxy.Warnings.Concat(session.Motion.Diagnostics)) EditorGUILayout.LabelField(message, EditorStyles.wordWrappedMiniLabel);
            int start = Math.Max(0, session.Frame - 3), end = Math.Min(session.Duration, session.Frame + 3);
            for (int f = start; f <= end; f++)
            {
                var p = session.Motion.Position(f); var d = session.Motion.Delta(f);
                EditorGUILayout.LabelField("F" + f + "  Δ(" + d.x + ", " + d.z + ")  Σ[0,F)(" + p.x + ", " + p.z + ")  sampleEnd=" + session.Motion.SampleEndMs(f) + "ms", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
