using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SkillConfig.Editor
{
    public sealed class SkillEditorWindow : EditorWindow
    {
        [SerializeField] CharacterSkillCatalog catalog;
        [SerializeField] ComboData owner;
        [SerializeField] string selected = "";
        readonly SkillTimelineView timeline = new SkillTimelineView();
        readonly SkillInspectorPanel inspector = new SkillInspectorPanel();
        SkillPreviewPanel preview;
        List<Diagnostic> issues = new List<Diagnostic>();
        Vector2 treeScroll, diagnosticScroll;
        string search = "";
        bool stale = true, diagnosticsDirty = true;
        double nextValidation;
        [MenuItem("Tools/Skill Editor/Skill Editor (Phase 2B)")]
        public static void Open() => GetWindow<SkillEditorWindow>("Skill Editor");
        [MenuItem("Tools/Skill Editor/Skill Editor")]
        public static void OpenCurrent() => Open();
        public static void OpenSkill(ComboData skill)
        {
            var w = GetWindow<SkillEditorWindow>("Skill Editor");
            w.catalog = AssetDatabase.FindAssets("t:CharacterSkillCatalog").Select(g => AssetDatabase.LoadAssetAtPath<CharacterSkillCatalog>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(c => c.skills.Contains(skill));
            w.Choose(skill);
        }
        void OnEnable()
        {
            minSize = new Vector2(1000, 520);
            var state = SkillEditorWorkspaceState.instance;
            if (!catalog) catalog = AssetDatabase.LoadAssetAtPath<CharacterSkillCatalog>(AssetDatabase.GUIDToAssetPath(state.catalogGuid ?? ""));
            if (!catalog) catalog = AssetDatabase.LoadAssetAtPath<CharacterSkillCatalog>("Assets/ScriptableObject/SkillConfigs/MiyabiPhase1.asset");
            if (!owner) owner = AssetDatabase.LoadAssetAtPath<ComboData>(AssetDatabase.GUIDToAssetPath(state.skillGuid ?? ""));
            if (catalog && (!owner || !catalog.skills.Contains(owner))) owner = catalog.skills.FirstOrDefault(s => s);
            SkillEditCommands.Changed += Changed;
            EditorApplication.projectChanged += Changed;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            diagnosticsDirty = true;
            preview = new SkillPreviewPanel(f => timeline.SetCursor(f), Repaint);
            timeline.CursorChanged += PreviewSeek;
        }
        void OnDisable()
        {
            SkillEditCommands.Changed -= Changed; EditorApplication.projectChanged -= Changed; EditorApplication.playModeStateChanged -= PlayModeChanged;
            timeline.Cancel(); SkillEditCommands.Flush(); Persist();
            timeline.CursorChanged -= PreviewSeek; preview?.Dispose(); preview = null;
        }
        void OnLostFocus() { timeline.Cancel(); Repaint(); }
        void PreviewSeek(int frame) => preview?.Seek(frame);
        void PlayModeChanged(PlayModeStateChange state) { preview?.CloseSession(); timeline.Cancel(); SkillEditCommands.Flush(); Repaint(); }
        void Changed()
        {
            timeline.Cancel();
            if (catalog && owner && !catalog.skills.Contains(owner)) { owner = catalog.skills.FirstOrDefault(s => s); selected = ""; }
            diagnosticsDirty = true; stale = true; Repaint();
        }
        void Persist()
        {
            var state = SkillEditorWorkspaceState.instance;
            state.catalogGuid = catalog ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(catalog)) : "";
            state.skillGuid = owner ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(owner)) : ""; state.Persist();
        }
        void Choose(ComboData value) { preview?.CloseSession(); timeline.Cancel(); SkillEditCommands.Flush(); owner = value; selected = ""; diagnosticsDirty = true; Persist(); Repaint(); }
        void SelectItem(string id) { selected = id; Repaint(); }
        void OnInspectorUpdate()
        {
            if (!timeline.IsDragging && (diagnosticsDirty || EditorApplication.timeSinceStartup > nextValidation)) Validate();
        }
        void Validate()
        {
            diagnosticsDirty = false; nextValidation = EditorApplication.timeSinceStartup + 2;
            if (!catalog) { issues.Clear(); stale = true; return; }
            SkillAssetValidator.Build(catalog, out issues); stale = SkillConfigExporter.IsStale(catalog); Repaint();
        }
        void OnGUI()
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    var next = (CharacterSkillCatalog)EditorGUILayout.ObjectField(catalog, typeof(CharacterSkillCatalog), false, GUILayout.Width(245));
                    if (next != catalog) { SkillEditCommands.Flush(); catalog = next; Choose(catalog ? catalog.skills.FirstOrDefault(s => s) : null); }
                    if (GUILayout.Button("角色配置", EditorStyles.toolbarButton)) selected = "@catalog";
                    GUILayout.Label(stale ? "● 待导出" : "✓ 已导出", GUILayout.Width(80));
                    GUILayout.Label((owner && EditorUtility.IsDirty(owner)) || (catalog && EditorUtility.IsDirty(catalog)) ? "SO 未保存" : "SO 已保存", GUILayout.Width(80));
                    if (GUILayout.Button("保存 SO", EditorStyles.toolbarButton)) Save();
                    if (GUILayout.Button("校验", EditorStyles.toolbarButton)) Validate();
                    using (new EditorGUI.DisabledScope(!catalog))
                        if (GUILayout.Button("Export JSON", EditorStyles.toolbarButton)) { SkillEditCommands.Flush(); issues = SkillConfigExporter.Export(catalog); stale = SkillConfigExporter.IsStale(catalog); diagnosticsDirty = false; nextValidation = EditorApplication.timeSinceStartup + 2; }
                }
                preview?.Draw(owner, catalog, selected, timeline.Cursor);
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawTree();
                    using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                    {
                        if (owner && owner.skill != null && owner.skill.registered) timeline.Draw(owner, selected, SelectItem);
                        else EditorGUILayout.HelpBox("选择已登记的 ComboData。编辑器直接编辑原技能配置。", MessageType.Info);
                    }
                    inspector.Draw(owner, catalog, selected, SelectItem);
                }
                diagnosticScroll = EditorGUILayout.BeginScrollView(diagnosticScroll, GUILayout.Height(110));
                foreach (var issue in issues)
                {
                    if (GUILayout.Button(issue.severity + " · " + issue.code + " · " + issue.location, EditorStyles.linkLabel)) FocusDiagnostic(issue);
                    EditorGUILayout.HelpBox(issue.message, issue.severity == DiagnosticSeverity.Error ? MessageType.Error : MessageType.Warning);
                }
                EditorGUILayout.EndScrollView();
                if (Event.current.type == EventType.KeyDown && Event.current.control && Event.current.keyCode == KeyCode.S) { Save(); Event.current.Use(); }
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Delete && !EditorGUIUtility.editingTextField && owner && SkillTimelineAdapters.Find(owner, selected) != null)
                { SkillEditCommands.DeleteItem(owner, selected); selected = ""; Event.current.Use(); }
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorGUILayout.HelpBox("Play Mode 中只读；Phase 2B 不修改运行中的战斗配置。", MessageType.Info);
        }
        void Save()
        {
            SkillEditCommands.Flush(); if (owner) AssetDatabase.SaveAssetIfDirty(owner); if (catalog) AssetDatabase.SaveAssetIfDirty(catalog); Persist(); Validate();
        }
        void FocusDiagnostic(Diagnostic issue)
        {
            if (!catalog) return;
            var skill = catalog.skills.FirstOrDefault(s => s && s.skill != null && !string.IsNullOrEmpty(s.skill.skillId) && (issue.location == s.skill.skillId || issue.location.StartsWith(s.skill.skillId + "/", StringComparison.Ordinal)));
            if (!skill) { selected = "@catalog"; return; }
            Choose(skill);
            var item = SkillTimelineAdapters.Items(skill).FirstOrDefault(i => issue.location.EndsWith("/" + i.id, StringComparison.Ordinal));
            if (item != null) selected = item.id;
        }
        void DrawTree()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(205)))
            {
                search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
                treeScroll = EditorGUILayout.BeginScrollView(treeScroll);
                if (catalog)
                {
                    GUILayout.Label(catalog.characterId + " · " + catalog.completeness, EditorStyles.boldLabel);
                    foreach (var group in catalog.skills.Where(s => s && (string.IsNullOrEmpty(search) || s.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || (s.skill?.displayName ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)).GroupBy(s => s.skill?.category ?? "未登记"))
                    {
                        GUILayout.Label(string.IsNullOrEmpty(group.Key) ? "未分类" : group.Key, EditorStyles.miniBoldLabel);
                        foreach (var skill in group)
                        {
                            string label = string.IsNullOrEmpty(skill.skill?.displayName) ? skill.name : skill.skill.displayName;
                            if (GUILayout.Toggle(owner == skill, label, "Button") && owner != skill) Choose(skill);
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
                using (new EditorGUI.DisabledScope(!catalog))
                {
                    if (GUILayout.Button("新建技能 SO")) CreateSkill(false);
                    using (new EditorGUI.DisabledScope(!owner || owner.skill == null || !owner.skill.registered)) if (GUILayout.Button("复制为新技能")) CreateSkill(true);
                    using (new EditorGUI.DisabledScope(!owner)) if (GUILayout.Button("从目录移除（保留 SO）"))
                    { var removed = owner; SkillEditCommands.Apply(catalog, "Remove skill from catalog", () => catalog.skills.Remove(removed)); Choose(catalog.skills.FirstOrDefault(s => s)); }
                }
            }
        }
        void CreateSkill(bool copy)
        {
            string path = EditorUtility.SaveFilePanelInProject(copy ? "复制技能" : "新建技能", copy ? owner.name + " Copy" : "NewSkill", "asset", "选择新 ComboData 的保存位置");
            if (string.IsNullOrEmpty(path)) return;
            SkillEditCommands.Flush();
            var created = copy ? Instantiate(owner) : CreateInstance<ComboData>();
            if (!copy) created.skill = new SkillAuthoringData { displayName = "New Skill" };
            created.skill.registered = true; created.skill.skillId = SkillEditCommands.NewId();
            AssetDatabase.CreateAsset(created, AssetDatabase.GenerateUniqueAssetPath(path));
            // Keep the new asset on catalog Undo; removing a skill never destroys the user's asset.
            SkillEditCommands.Apply(catalog, copy ? "Add skill copy" : "Add new skill", () => catalog.skills.Add(created));
            AssetDatabase.SaveAssetIfDirty(created); Choose(created);
        }
    }
}
