using System;
using UnityEditor;
using UnityEngine;

namespace SkillConfig.Editor
{
    [CustomEditor(typeof(ComboData)), CanEditMultipleObjects]
    public sealed class ComboDataSkillInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.LabelField("原有 ComboData 配置", EditorStyles.boldLabel);
            DrawPropertiesExcluding(serializedObject, "m_Script", "skill");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Phase 1 技能数据（未接入战斗）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("保留旧字段和运行行为。新技能类型不由 AttackStyle 推断；HitGroup 基础伤害读取上方旧字段，不另存副本。保存不会自动导出 JSON。", MessageType.Info);
            var data = (ComboData)target;
            if (targets.Length == 1 && (data.skill == null || !data.skill.registered))
            {
                if (GUILayout.Button("登记此技能（生成一次性 SkillID）"))
                {
                    serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(data, "Register skill");
                    data.skill = new SkillAuthoringData { registered = true, skillId = Guid.NewGuid().ToString("N"), displayName = data.name };
                    EditorUtility.SetDirty(data); serializedObject.Update();
                }
            }
            else
            {
                var property = serializedObject.FindProperty("skill").Copy();
                var end = property.GetEndProperty();
                bool enter = true;
                while (property.NextVisible(enter) && !SerializedProperty.EqualContents(property, end))
                {
                    enter = false;
                    using (new EditorGUI.DisabledScope(property.name == "skillId" || property.name == "registered"))
                        EditorGUILayout.PropertyField(property, true);
                }
            }
            serializedObject.ApplyModifiedProperties();
            if (GUILayout.Button("保存 SO（不导出）")) foreach (var selected in targets) AssetDatabase.SaveAssetIfDirty(selected);
            if (targets.Length == 1 && data.skill != null && data.skill.registered && GUILayout.Button("复制为新技能（新 SkillID）"))
            {
                string path = EditorUtility.SaveFilePanelInProject("复制技能", data.name + " Copy", "asset", "选择副本位置");
                if (!string.IsNullOrEmpty(path))
                {
                    var copy = Instantiate(data);
                    copy.skill.skillId = Guid.NewGuid().ToString("N");
                    AssetDatabase.CreateAsset(copy, path); Undo.RegisterCreatedObjectUndo(copy, "Duplicate skill");
                    AssetDatabase.SaveAssetIfDirty(copy); Selection.activeObject = copy;
                }
            }
        }
    }
}
