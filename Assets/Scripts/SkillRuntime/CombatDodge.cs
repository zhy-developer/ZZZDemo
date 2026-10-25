using ZZZ.SkillAuthoring;

namespace ZZZ.SkillRuntime
{
    /// <summary>只负责闪避准入、方向选择和共享冷却；动画、位移、事件复用普通技能流程。</summary>
    internal static class CombatDodge
    {
        public const int Command = 4;

        public static void Accept(CombatFighter fighter, CombatCommand command)
        {
            if (command.Action != Command || fighter.BlockedThisFrame || fighter.StartedSkillThisFrame
                || fighter.IsDodging || fighter.DodgeCooldownRemaining > 0) return;
            if (fighter.State != CombatState.Free && fighter.State != CombatState.Charging && (fighter.State != CombatState.Skill
                || !fighter.Skill.HasWindow(SkillWindowKind.DodgeCancel, fighter.SkillFrame))) return;

            // 方向来自按键采集时的网络载荷，不读取晚到的移动输入或本地相机。
            // 121 明确表示无方向；后撤不翻转朝向，负 Z 位移由后撤资产提供。
            bool directional = command.Facing >= 0 && command.Facing < 120;
            if (!directional && command.Facing != 121) return;
            var skill = directional ? fighter.Library.DodgeFront : fighter.Library.DodgeBack;
            if (skill == null) return;
            CombatSkillInput.Start(fighter, skill, Command, command.Facing);
            fighter.DodgeCooldownRemaining = fighter.Library.Profile.dodgeCooldownFrames;
            // Start 共用 StopSkill 清除旧攻击区域和连段缓存。旧技能已走过的边界位移仍只消费一次。
            // 不缓存失败的闪避输入，冷却结束或后来进入取消窗口时不会自动补放。
        }
    }
}
