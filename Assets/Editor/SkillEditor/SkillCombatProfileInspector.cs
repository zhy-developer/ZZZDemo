using UnityEditor;
using UnityEngine;
using ZZZ.SkillAuthoring;

namespace ZZZ.SkillEditor
{
    /// <summary>角色配置入口说明。只绘制字段和提示，不创建资产或启动对战。</summary>
    [CustomEditor(typeof(SkillCombatProfile))]
    internal sealed class SkillCombatProfileInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var profile = (SkillCombatProfile)target;
            EditorGUILayout.HelpBox("Rop 2 = E 蓄力完成后的攻击；Rop 4 = 前闪；Rop 5 = 空格招架。蓄力动画放在 Skill Charge Clip，招架技能内添加 Parry 窗口。Play 使用开局快照，修改后需重新开局。", MessageType.Info);
            bool hasSkill = false, hasParry = false;
            if (profile.commands != null) foreach (var binding in profile.commands)
            {
                if (binding == null) continue;
                hasSkill |= (int)binding.command == 2 && binding.skill != null;
                hasParry |= (int)binding.command == 5 && binding.skill != null;
            }
            if (hasSkill && profile.skillChargeClip == null)
                EditorGUILayout.HelpBox("已绑定 E 释放技能，请配置 Skill Charge Clip。缺少蓄力动画会阻止对战初始化。", MessageType.Error);
            if (profile.parrySuccessSkill != null && !hasParry)
                EditorGUILayout.HelpBox("已配置成功动作，请在 Commands / Rop 5 绑定招架技能。", MessageType.Error);
            EditorGUILayout.HelpBox("十字黄光放在 E 攻击资产的 Vfx 轨道前摇帧；只做预警。要允许振刀，另在对应命中组勾选“可被振刀”。Parry Feedback 是招架成功时的反馈，不是预警。", MessageType.Info);
        }
    }
}
