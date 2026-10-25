using System;
using System.Collections.Generic;
using UnityEditor;
using ZZZ.SkillAuthoring;

namespace ZZZ.SkillEditor
{
    /// <summary>编辑器只读注册表：按角色配置收集技能，不修改资产或加载运行时战斗。</summary>
    [InitializeOnLoad]
    internal static class SkillRegistrationCatalog
    {
        private static string[] profilePaths;

        static SkillRegistrationCatalog()
        {
            // 只缓存资产路径；引用列表和 ID 每次读取最新值，支持 Inspector 修改及 Undo。
            EditorApplication.projectChanged += () => profilePaths = null;
        }

        public static List<SkillCombatProfile> FindOwners(SkillDefinition skill)
        {
            var result = new List<SkillCombatProfile>();
            if (skill == null) return result;
            if (profilePaths == null)
            {
                var guids = AssetDatabase.FindAssets("t:SkillCombatProfile");
                profilePaths = new string[guids.Length];
                for (int i = 0; i < guids.Length; i++) profilePaths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);
                Array.Sort(profilePaths, StringComparer.Ordinal);
            }
            foreach (string path in profilePaths)
            {
                var profile = AssetDatabase.LoadAssetAtPath<SkillCombatProfile>(path);
                if (profile != null && RegisteredSkills(profile).Contains(skill)) result.Add(profile);
            }
            return result;
        }

        public static HashSet<SkillDefinition> RegisteredSkills(SkillCombatProfile profile)
        {
            // 与战斗技能库保持相同收录规则：入口技能自动注册，同一资产重复引用只列一次。
            var result = new HashSet<SkillDefinition>();
            if (profile.commands != null) foreach (var binding in profile.commands)
                if (binding?.skill != null) result.Add(binding.skill);
            if (profile.additionalSkills != null) foreach (var skill in profile.additionalSkills)
                if (skill != null) result.Add(skill);
            if (profile.dodgeBackSkill != null) result.Add(profile.dodgeBackSkill);
            if (profile.parrySuccessSkill != null) result.Add(profile.parrySuccessSkill);
            return result;
        }

        private static bool RequiresSpecialEntry(SkillCombatProfile profile, SkillDefinition skill)
        {
            if (skill == profile.dodgeBackSkill || skill == profile.parrySuccessSkill) return true;
            if (profile.commands != null) foreach (var binding in profile.commands)
                if (binding != null && ((int)binding.command == 2 || (int)binding.command == 4 || (int)binding.command == 5)
                    && binding.skill == skill) return true;
            return false;
        }

        public static List<SkillDefinition> UniqueSkills(SkillCombatProfile profile, out bool hasInvalidIds)
        {
            hasInvalidIds = false;
            var byId = new Dictionary<string, SkillDefinition>(StringComparer.Ordinal);
            var duplicates = new HashSet<string>(StringComparer.Ordinal);
            foreach (var skill in RegisteredSkills(profile))
            {
                if (string.IsNullOrWhiteSpace(skill.skillId)) { hasInvalidIds = true; continue; }
                if (byId.ContainsKey(skill.skillId)) duplicates.Add(skill.skillId);
                else byId.Add(skill.skillId, skill);
            }
            foreach (string id in duplicates) byId.Remove(id);
            hasInvalidIds |= duplicates.Count > 0;
            var result = new List<SkillDefinition>(byId.Values);
            // 蓄力、闪避、招架和成功动作有各自准入，不能通过连段绕过。
            result.RemoveAll(skill => RequiresSpecialEntry(profile, skill));
            result.Sort((a, b) => StringComparer.Ordinal.Compare(a.skillId, b.skillId));
            return result;
        }
    }
}
