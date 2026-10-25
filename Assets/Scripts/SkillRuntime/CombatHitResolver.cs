using System;
using System.Collections.Generic;
using ZZZ.SkillAuthoring;

namespace ZZZ.SkillRuntime
{
    /// <summary>接触收集与统一结算。收集期间只记录结果，不能立即打断角色，以免遍历顺序改变同帧结果。</summary>
    internal sealed class CombatHitResolver
    {
        private readonly struct Contact
        {
            public readonly CombatFighter Attacker, Defender;
            public readonly SkillHitGroup Group;
            public readonly bool Parried;
            public Contact(CombatFighter attacker, CombatFighter defender, SkillHitGroup group, bool parried)
            { Attacker = attacker; Defender = defender; Group = group; Parried = parried; }
        }

        private readonly List<Contact> contacts = new List<Contact>();
        private readonly HashSet<CombatFighter> successes = new HashSet<CombatFighter>();
        public int HitCount { get; private set; }
        public int ParryCount { get; private set; }

        public void Clear()
        {
            contacts.Clear(); successes.Clear(); HitCount = 0; ParryCount = 0;
        }

        public void Collect(CombatFighter attacker, IReadOnlyList<CombatFighter> fighters)
        {
            if (attacker.BlockedThisFrame || attacker.State != CombatState.Skill || attacker.Skill.Data.hitRegions == null) return;
            for (int i = 0; i < attacker.Skill.Data.hitRegions.Count; i++)
            {
                var region = attacker.Skill.Data.hitRegions[i];
                if (!SkillHitTiming.TryGetHitFrame(region, attacker.SkillFrame, out int hitFrame)) continue;
                if (!attacker.RegionAnchors.TryGet(attacker, i, out var origin, out int facing)) continue;
                foreach (var defender in fighters)
                {
                    // 无敌优先，不产生伤害、振刀奖励或命中记录。
                    if (defender == attacker || defender.Health == 0 || defender.IsInvulnerable) continue;
                    var key = new CombatHitKey(region.hitGroupId, defender.BattleId, hitFrame);
                    if (attacker.HitTargets.Contains(key)) continue;
                    if (!CombatCollision.Overlaps(region, origin, facing, defender.Position, defender.Library.Profile.hurtRadius)) continue;
                    var group = attacker.Skill.Group(region.hitGroupId);
                    bool parried = group.canBeParried && defender.CanParry;
                    // 成功接住也消费该次命中；同组重叠区域不能重复伤害或重复触发反馈。
                    attacker.HitTargets.Add(key);
                    contacts.Add(new Contact(attacker, defender, group, parried));
                }
            }
        }

        public void Resolve(IReadOnlyList<CombatFighter> fighters, int frame)
        {
            // 先封存伤害和表现事实，再切换任何角色状态。已成立的普通命中不会被同帧振刀删除。
            foreach (var contact in contacts)
            {
                var attacker = contact.Attacker;
                var defender = contact.Defender;
                if (contact.Parried)
                {
                    // 同一招架释放当帧可以接住多个可振组，但只给一次成功动作和反馈。
                    if (!successes.Add(defender)) continue;
                    ParryCount++;
                    defender.HitFeedbackEvents.Add(new CombatHitFeedbackEvent(frame,
                        attacker.BattleId, defender.BattleId, defender.CastSerial, contact.Group.id,
                        defender.Position, defender.Facing, defender.Library.Profile.parryFeedback, true));
                }
                else
                {
                    HitCount++;
                    defender.Health = Math.Max(0, defender.Health - contact.Group.damage);
                    defender.HitFeedbackEvents.Add(new CombatHitFeedbackEvent(frame,
                        attacker.BattleId, defender.BattleId, attacker.CastSerial, contact.Group.id,
                        defender.Position, attacker.Facing, contact.Group.feedback));
                }
            }

            foreach (var fighter in fighters)
            {
                bool received = false, broken = false;
                int stun = 0;
                foreach (var contact in contacts)
                {
                    if (!contact.Parried && contact.Defender == fighter)
                    { received = true; stun = Math.Max(stun, contact.Group.hitStunFrames); }
                    if (contact.Parried && contact.Attacker == fighter)
                    { broken = true; stun = Math.Max(stun, fighter.Library.Profile.parryBrokenStunFrames); }
                }
                if (received || broken)
                {
                    // 不可振攻击仍会伤害招架者；死亡/硬直优先于同帧成功动作，反馈仍保留。
                    fighter.StopSkill();
                    fighter.State = fighter.Health == 0 ? CombatState.Dead : CombatState.HitStun;
                    fighter.HitStunRemaining = Math.Max(fighter.HitStunRemaining, stun);
                    fighter.ReactionKind = broken ? CombatReactionKind.ParryBroken : CombatReactionKind.Hit;
                    fighter.PoseFrame = 0; fighter.Moving = false; fighter.ReactionSerial++;
                }
                else if (successes.Contains(fighter)) CombatParry.EnterSuccess(fighter);
            }
        }
    }
}
