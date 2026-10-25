namespace ZZZ.SkillRuntime
{
    /// <summary>E 的同步按键边沿和蓄力状态。渲染层不决定是否蓄满，也不根据本地按住时间直接释放。</summary>
    internal static class CombatCharge
    {
        public const int Command = 2;
        public const int Press = 1;
        public const int Release = 2;

        public static void Accept(CombatFighter fighter, CombatCommand command)
        {
            if (command.Action != Command) return;
            if (command.Payload == Release)
            {
                fighter.SkillButtonHeld = false;
                if (fighter.State == CombatState.Charging)
                {
                    fighter.StopSkill();
                    fighter.PendingMotion = UnityEngine.Vector2Int.zero;
                }
                return; // 已经进入攻击时松开，不取消攻击。
            }
            // Payload=0 的旧式瞬发 E 不再执行；重复按下不重启或补放被打断的蓄力。
            if (command.Payload != Press || fighter.SkillButtonHeld) return;
            fighter.SkillButtonHeld = true;
            if (fighter.BlockedThisFrame || fighter.StartedSkillThisFrame || fighter.State != CombatState.Free
                || !fighter.Library.TryGet(Command, out _)) return;
            fighter.StopSkill();
            fighter.State = CombatState.Charging;
            fighter.CastSerial = fighter.CastSerial == int.MaxValue ? 1 : fighter.CastSerial + 1;
            fighter.SkillCommand = Command; fighter.Moving = false;
            fighter.StartedSkillThisFrame = true;
            fighter.CompletingSkillThisFrame = false;
            if (command.Facing >= 0 && command.Facing < 120) fighter.Facing = command.Facing * 3;
        }

        public static void Advance(CombatFighter fighter)
        {
            if (fighter.State != CombatState.Charging || fighter.BlockedThisFrame || fighter.StartedSkillThisFrame) return;
            // 输入先处理：同帧松开与蓄满相遇时，松开优先；取消后计数不再推进。
            fighter.ChargeFrame++;
            if (fighter.ChargeFrame < fighter.Library.Profile.skillChargeFrames || !fighter.SkillButtonHeld) return;
            if (fighter.Library.TryGet(Command, out var skill))
                CombatSkillInput.Start(fighter, skill, Command, 121); // 保持蓄力开始时的逻辑朝向。
        }
    }
}
