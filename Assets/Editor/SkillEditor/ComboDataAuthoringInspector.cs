using UnityEditor;
using UnityEngine;

namespace ZZZ.SkillEditor
{
    /// <summary>保留原 ComboData Inspector，提供已关联技能资产的打开入口。</summary>
    [CustomEditor(typeof(ComboData))]
    internal sealed class ComboDataAuthoringInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            var combo = (ComboData)target;
            if (combo.AuthoredSkill != null)
            {
                if (GUILayout.Button("打开关联的技能制作资产")) SkillEditorWindow.Open(combo.AuthoredSkill);
            }
            DrawDefaultInspector();
        }
    }
}
