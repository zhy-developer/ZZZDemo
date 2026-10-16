using UnityEditor;
using UnityEngine;
using ZZZ.SkillAuthoring;

namespace ZZZ.SkillEditor
{
    /// <summary>只显示当前游标的窗口状态；共用运行时查询，不执行战斗或依赖播放历史。</summary>
    internal static class SkillWindowPreview
    {
        public static void DrawPanel(SkillDefinition skill, int frame)
        {
            bool invulnerable = SkillWindowTiming.IsActive(skill, SkillWindowKind.Invulnerable, frame);
            bool dodgeCancel = SkillWindowTiming.IsActive(skill, SkillWindowKind.DodgeCancel, frame);
            EditorGUILayout.LabelField("无敌窗口", invulnerable ? "生效：免受攻击区域伤害与打断" : "未生效");
            EditorGUILayout.LabelField("闪避取消窗口", dodgeCancel ? "生效（实战还需满足冷却等条件）" : "未生效");
        }

        public static void DrawScene(SkillDefinition skill, int frame, Vector3 position, float displayHeight)
        {
            bool active = SkillWindowTiming.IsActive(skill, SkillWindowKind.Invulnerable, frame);
            Color previous = Handles.color;
            try
            {
                Handles.color = active ? new Color(.2f, 1f, .45f) : Color.gray;
                // 圆环只表示角色无敌状态，不代表受击半径，也不参与 XZ 判定。
                Handles.DrawWireDisc(position + Vector3.up * displayHeight, Vector3.up, .55f);
                var style = new GUIStyle(EditorStyles.boldLabel);
                style.normal.textColor = Handles.color;
                Handles.Label(position + Vector3.up * 1.6f,
                    active ? "无敌窗口：生效" : "无敌窗口：未生效", style);
            }
            finally { Handles.color = previous; }
        }
    }
}
