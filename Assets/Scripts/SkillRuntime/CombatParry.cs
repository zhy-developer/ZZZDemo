using ZZZ.SkillAuthoring;

namespace ZZZ.SkillRuntime
{
    /// <summary>招架入口与成功动作切换。是否接触由命中结算器决定，不读取黄光或粒子状态。</summary>
    internal static class CombatParry
    {
        public const int Command = 5;

        public static void Accept(CombatFighter fighter, CombatCommand command)
        {
            if (command.Action != Command || fighter.Library.Parry == null || fighter.BlockedThisFrame
                || fighter.StartedSkillThisFrame || fighter.IsParrying || fighter.IsDodging) return;
            // 蓄力中可主动改为招架；攻击中必须有独立 ParryCancel 窗口。
            if (fighter.State != CombatState.Free && fighter.State != CombatState.Charging
                && (fighter.State != CombatState.Skill
                    || !fighter.Skill.HasWindow(SkillWindowKind.ParryCancel, fighter.SkillFrame))) return;
            CombatSkillInput.Start(fighter, fighter.Library.Parry, Command, command.Facing);
        }

        public static void EnterSuccess(CombatFighter fighter)
        {
            var success = fighter.Library.ParrySuccess;
            if (success == null) { fighter.ParryConsumed = true; return; }
            CombatSkillInput.Start(fighter, success, Command, 121);
            fighter.ParryConsumed = true;
            // 成功动作在本帧结算末尾切入；F0 事件只补发一次，避免下一帧从 F1 开始漏掉。
            fighter.RegionAnchors.Capture(fighter);
            CombatSkillEvents.Collect(fighter);
        }
    }
}
