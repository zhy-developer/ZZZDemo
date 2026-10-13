using UnityEditor;
using UnityEngine;
using ZZZ.SkillAuthoring;

namespace ZZZ.SkillEditor
{
    /// <summary>资产 Inspector 入口；详细制作交给技能窗口。</summary>
    [CustomEditor(typeof(SkillDefinition))]
    internal sealed class SkillDefinitionInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("打开技能编辑器")) SkillEditorWindow.Open((SkillDefinition)target);
            EditorGUILayout.HelpBox("技能制作资产。第一轮尚未接入正式战斗。", MessageType.Info);
            if (DrawDefaultInspector()) SkillEditorWindow.NotifyAssetChanged((SkillDefinition)target);
        }
    }
}
