using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ZZZ.SkillAuthoring;

namespace ZZZ.SkillEditor
{
    /// <summary>窗口配置共用绘制器：技能轨道面板和资产 Inspector 使用同一注册技能下拉框。</summary>
    [CustomPropertyDrawer(typeof(SkillWindow))]
    internal sealed class SkillWindowDrawer : PropertyDrawer
    {
        // 浏览来源只属于编辑器，不写入技能资产，也不会更改角色注册关系。
        private readonly Dictionary<int, SkillCombatProfile> selectedProfiles = new Dictionary<int, SkillCombatProfile>();
        private static float RowHeight => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        private const float HelpHeight = 58f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            bool combo = property.FindPropertyRelative("kind").intValue == (int)SkillWindowKind.ComboExecute;
            return (combo ? 6 : 4) * RowHeight + (combo ? HelpHeight : 0);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(row, label, EditorStyles.boldLabel);
            row.y += RowHeight;
            var kind = property.FindPropertyRelative("kind");
            EditorGUI.PropertyField(row, kind, new GUIContent("窗口类型"));
            row.y += RowHeight;
            EditorGUI.PropertyField(row, property.FindPropertyRelative("startFrame"), new GUIContent("开始帧（包含）"));
            row.y += RowHeight;
            EditorGUI.PropertyField(row, property.FindPropertyRelative("endFrame"), new GUIContent("结束帧（不包含）"));
            row.y += RowHeight;
            if (kind.intValue == (int)SkillWindowKind.ComboExecute) DrawTarget(row, property);
            EditorGUI.EndProperty();
        }

        private void DrawTarget(Rect row, SerializedProperty property)
        {
            var skill = property.serializedObject.targetObject as SkillDefinition;
            var owners = SkillRegistrationCatalog.FindOwners(skill);
            var target = property.FindPropertyRelative("nextSkillId");
            string message = "请先把当前技能加入角色对战配置的 Commands 或 Additional Skills。";
            bool warning = true;
            SkillCombatProfile profile = null;
            if (owners.Count > 0)
            {
                int key = skill.GetInstanceID();
                selectedProfiles.TryGetValue(key, out profile);
                int selected = owners.IndexOf(profile);
                if (selected < 0) selected = 0;
                var names = new string[owners.Count];
                for (int i = 0; i < owners.Count; i++)
                    names[i] = owners[i].name + " (" + AssetDatabase.GetAssetPath(owners[i]) + ")";
                // GUI.changed 只表示资产数据变化；切换浏览来源不应将技能资产标记为修改。
                bool changed = GUI.changed;
                using (new EditorGUI.DisabledScope(owners.Count == 1))
                    selected = EditorGUI.Popup(row, "注册来源", selected, names);
                GUI.changed = changed;
                profile = owners[selected]; selectedProfiles[key] = profile;
            }
            else EditorGUI.LabelField(row, "注册来源", "当前技能尚未注册");
            row.y += RowHeight;

            bool invalidIds = false;
            var skills = profile != null ? SkillRegistrationCatalog.UniqueSkills(profile, out invalidIds)
                : new List<SkillDefinition>();
            var labels = new List<string> { "无（不接下一段）" };
            var ids = new List<string> { "" };
            foreach (var entry in skills)
            {
                string name = string.IsNullOrWhiteSpace(entry.displayName) ? entry.name : entry.displayName;
                labels.Add(entry.skillId + " — " + name); ids.Add(entry.skillId);
            }
            int current = ids.IndexOf(target.stringValue);
            bool missing = current < 0;
            if (missing)
            {
                // 旧 ID 失效时仍保留原值；打开面板不能静默清空或替换用户配置。
                current = ids.Count; ids.Add(target.stringValue);
                labels.Add("当前值不可用：" + target.stringValue);
            }
            EditorGUI.BeginChangeCheck();
            int next = EditorGUI.Popup(row, "下一段技能", current, labels.ToArray());
            if (EditorGUI.EndChangeCheck()) target.stringValue = ids[next];
            row.y += RowHeight;

            if (profile != null)
            {
                warning = invalidIds || missing;
                message = invalidIds ? "注册列表存在空 ID 或重复 ID，已排除这些选项。请先修正对应技能的 skillId。"
                    : missing ? "当前 ID 未唯一注册或属于闪避技能，不能作为连段目标。原值已保留，请重新选择。"
                    : owners.Count > 1 ? "此技能被多个配置共用。下拉项来自所选来源；修改后，请确保目标也在其他使用此技能的配置中注册。"
                    : "从角色配置获取已注册技能，排除闪避资产。单选保存 skillId；选择“无”取消连段。闪避请使用 DodgeCancel 窗口。";
            }
            row.height = HelpHeight;
            EditorGUI.HelpBox(row, message, warning ? MessageType.Warning : MessageType.Info);
        }
    }
}
