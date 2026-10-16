using ZZZ.SkillAuthoring;

namespace ZZZ.SkillRuntime
{
    /// <summary>技能输入规则：入口指令、单次预输入、窗口内切段。只修改逻辑状态，不操作表现对象。</summary>
    internal static class CombatSkillInput
    {
        public static void Accept(CombatFighter fighter, CombatCommand command)
        {
            if (fighter.BlockedThisFrame || fighter.StartedSkillThisFrame || fighter.IsDodging
                || command.Action == 0 || command.Action == CombatDodge.Command) return;
            if (fighter.State == CombatState.Free)
            {
                if (fighter.Library.TryGet(command.Action, out var skill))
                    Start(fighter, skill, command.Action, command.Facing);
                return;
            }
            // 连段沿用入口按键；其他动作不覆盖缓存。同帧开始的新技能不再接收重复按键。
            if (fighter.State != CombatState.Skill || fighter.StartedSkillThisFrame
                || command.Action != fighter.SkillCommand || fighter.HasBufferedCombo) return;
            bool canBuffer = fighter.Skill.HasWindow(SkillWindowKind.PreInput, fighter.SkillFrame);
            bool canExecute = fighter.Library.TryGet(fighter.Skill.ComboTarget(fighter.SkillFrame), out _);
            if (!canBuffer && !canExecute) return;
            fighter.HasBufferedCombo = true;
            fighter.BufferedComboFacing = command.Facing >= 0 && command.Facing < 120 ? command.Facing : 121;
        }

        public static void AdvanceCombo(CombatFighter fighter)
        {
            if (fighter.BlockedThisFrame || fighter.IsDodging || fighter.State != CombatState.Skill
                || fighter.StartedSkillThisFrame || !fighter.HasBufferedCombo) return;
            if (!fighter.Library.TryGet(fighter.Skill.ComboTarget(fighter.SkillFrame), out var next)) return;
            int command = fighter.SkillCommand;
            int facing = fighter.BufferedComboFacing;
            // 每帧最多接一段。新段从 F0 开始；旧段已经走过的 [F-1,F) 位移仍消费一次，未来位移作废。
            Start(fighter, next, command, facing);
        }

        internal static void Start(CombatFighter fighter, CombatSkill skill, int command, int facing)
        {
            fighter.StopSkill(); // 所有退出路径共用清理，缓存和命中记录不能串到下一段。
            fighter.Skill = skill; fighter.SkillFrame = 0; fighter.SkillCommand = command;
            fighter.State = CombatState.Skill;
            fighter.CastSerial = fighter.CastSerial == int.MaxValue ? 1 : fighter.CastSerial + 1;
            fighter.PoseFrame = 0; fighter.Moving = false;
            fighter.StartedSkillThisFrame = true;
            fighter.CompletingSkillThisFrame = false;
            if (facing >= 0 && facing < 120) fighter.Facing = facing * 3;
        }
    }
}
