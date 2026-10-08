using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SkillConfig.Editor
{
    public sealed class SkillValidationWindow : EditorWindow
    {
        private CharacterSkillCatalog catalog;
        private List<Diagnostic> issues = new List<Diagnostic>();
        private Vector2 scroll;
        private bool stale = true;
        private double nextCheck;
        [MenuItem("Tools/Skill Config/Phase 1 Validation and Export")]
        public static void Open() => GetWindow<SkillValidationWindow>("技能配置 Phase 1");
        private void OnEnable() { Undo.undoRedoPerformed += Invalidate; EditorApplication.projectChanged += Invalidate; }
        private void OnDisable() { Undo.undoRedoPerformed -= Invalidate; EditorApplication.projectChanged -= Invalidate; }
        private void Invalidate() { nextCheck = 0; Repaint(); }
        private void OnInspectorUpdate() { if (catalog != null && EditorApplication.timeSinceStartup >= nextCheck) { stale = SkillConfigExporter.IsStale(catalog); nextCheck = EditorApplication.timeSinceStartup + 2; Repaint(); } }
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("仅数据、校验和导出。PartialPilot 不是完整战斗配置。任何保存/Undo/依赖变化都需重新检查导出状态。", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            catalog = (CharacterSkillCatalog)EditorGUILayout.ObjectField("角色技能目录", catalog, typeof(CharacterSkillCatalog), false);
            if (EditorGUI.EndChangeCheck()) { issues.Clear(); Invalidate(); }
            EditorGUILayout.LabelField(catalog == null ? "请选择目录" : stale ? "待导出 / 存在校验错误" : "与当前导出一致");
            using (new EditorGUI.DisabledScope(catalog == null))
            {
                if (GUILayout.Button("校验")) { SkillAssetValidator.Build(catalog, out issues); Invalidate(); }
                if (GUILayout.Button("保存并导出当前角色")) { issues = SkillConfigExporter.Export(catalog); Invalidate(); }
            }
            if (GUILayout.Button("逐角色导出全部目录"))
            {
                issues.Clear();
                foreach (var result in SkillConfigExporter.ExportAll())
                {
                    issues.AddRange(result.Value);
                    Debug.Log(result.Key + (result.Value.Any(d => d.severity == DiagnosticSeverity.Error) ? ": FAILED" : ": EXPORTED"));
                }
                Invalidate();
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var d in issues)
            {
                EditorGUILayout.HelpBox(d.ToString(), d.severity == DiagnosticSeverity.Error ? MessageType.Error : MessageType.Warning);
                if (catalog != null && GUILayout.Button("定位 " + d.location))
                {
                    var skill = catalog.skills.FirstOrDefault(s => s != null && s.skill != null && d.location.StartsWith(s.skill.skillId + "/", StringComparison.Ordinal));
                    if (skill == null) skill = catalog.skills.FirstOrDefault(s => s != null && s.skill != null && d.location == s.skill.skillId);
                    Selection.activeObject = skill != null ? (UnityEngine.Object)skill : catalog;
                    EditorGUIUtility.PingObject(Selection.activeObject);
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
