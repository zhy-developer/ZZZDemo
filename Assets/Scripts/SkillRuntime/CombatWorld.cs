using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZZZ.SkillRuntime
{
    /// <summary>唯一战斗时钟：推进边界/收输入 → 全员位移 → 全员收集命中 → 统一结算。每个网络帧仅调用一次。</summary>
    public sealed class CombatWorld
    {
        private readonly List<CombatFighter> fighters;
        public IReadOnlyList<CombatFighter> Fighters => fighters;
        private readonly List<PendingHit> hits = new List<PendingHit>();
        private readonly int minX, maxX, minZ, maxZ;
        public int Frame { get; private set; }
        public bool Finished { get; private set; }
        public int WinnerId { get; private set; } // 0 为双方同时归零。
        public int LastHitCount { get; private set; }

        private readonly struct PendingHit
        {
            public readonly CombatFighter Attacker, Defender;
            public readonly SkillAuthoring.SkillHitGroup Group;
            public PendingHit(CombatFighter attacker, CombatFighter defender, SkillAuthoring.SkillHitGroup group)
            { Attacker = attacker; Defender = defender; Group = group; }
        }

        public CombatWorld(List<CombatFighter> actors, int minX, int maxX, int minZ, int maxZ)
        {
            if (actors.Count != 2 || actors[0].BattleId == actors[1].BattleId) throw new ArgumentException("第二轮只支持两个不同 battleID 的角色。");
            if (minX >= maxX || minZ >= maxZ || !CombatMath.InRange(minX) || !CombatMath.InRange(maxX)
                || !CombatMath.InRange(minZ) || !CombatMath.InRange(maxZ)) throw new ArgumentException("地图边界无效或超出整数安全范围。");
            fighters = new List<CombatFighter>(actors);
            fighters.Sort((a, b) => a.BattleId.CompareTo(b.BattleId));
            this.minX = minX; this.maxX = maxX; this.minZ = minZ; this.maxZ = maxZ;
        }

        public void Step(IReadOnlyList<CombatCommand> commands)
        {
            Frame++;
            LastHitCount = 0;
            if (Finished) return; // 网络仍消费和确认输入；已结束的对局不再产生战斗行为。
            hits.Clear();
            foreach (var fighter in fighters) BeginFrame(fighter);
            foreach (var fighter in fighters)
                foreach (var command in commands)
                    if (command.BattleId == fighter.BattleId) ApplyCommand(fighter, command);
            foreach (var fighter in fighters) Move(fighter);
            foreach (var attacker in fighters) CollectHits(attacker);
            ResolveHits();
            if (fighters.Exists(f => f.Health == 0))
            {
                Finished = true;
                foreach (var fighter in fighters) if (fighter.Health > 0) WinnerId = fighter.BattleId;
            }
        }

        private static void BeginFrame(CombatFighter fighter)
        {
            fighter.PendingMotion = Vector2Int.zero;
            fighter.CompletingSkillThisFrame = false;
            // 顿帧暂时全局停用。保留字段供后续恢复，清除历史计数，不能再阻塞技能或位移。
            fighter.HitStopRemaining = 0;
            fighter.FrozenThisFrame = false;
            fighter.BlockedThisFrame = fighter.State == CombatState.Dead;
            if (fighter.State == CombatState.Dead) return;
            if (fighter.State == CombatState.HitStun)
            {
                if (fighter.HitStunRemaining > 0)
                {
                    fighter.HitStunRemaining--; fighter.PoseFrame++; fighter.BlockedThisFrame = true;
                    return; // 计数变为 0 的这一帧仍然属于硬直，下一帧才恢复输入。
                }
                fighter.State = CombatState.Free; fighter.PoseFrame = 0;
            }
            if (fighter.State == CombatState.Skill)
            {
                // 新释放在 F0 判定并显示初始姿势；之后先消费 [F-1,F) 位移，再用 F 检测攻击区域。
                // 因而 Scene 游标 F 的姿势、位移和生效区域与战斗中 F 一致，结束边界只消费最后一条位移。
                fighter.PendingMotion = fighter.Skill.MotionDelta(fighter.SkillFrame, fighter.Facing);
                fighter.SkillFrame++;
                if (fighter.SkillFrame >= fighter.Skill.Data.totalFrames)
                {
                    fighter.CompletingSkillThisFrame = true;
                    fighter.StopSkill();
                }
            }
        }

        private static void ApplyCommand(CombatFighter fighter, CombatCommand command)
        {
            fighter.MoveDirection = command.MoveDirection >= 0 && command.MoveDirection < 120 ? command.MoveDirection : 121;
            if (fighter.BlockedThisFrame || fighter.State != CombatState.Free || command.Action == 0) return;
            if (!fighter.Library.TryGet(command.Action, out var skill)) return;
            fighter.Skill = skill; fighter.SkillFrame = 0; fighter.State = CombatState.Skill;
            fighter.CastSerial = fighter.CastSerial == int.MaxValue ? 1 : fighter.CastSerial + 1;
            fighter.HitTargets.Clear(); fighter.PoseFrame = 0; fighter.Moving = false;
            if (command.Facing >= 0 && command.Facing < 120) fighter.Facing = command.Facing * 3;
        }

        private void Move(CombatFighter fighter)
        {
            if (fighter.BlockedThisFrame) return;
            bool inputMoving = fighter.MoveDirection < 120;
            if (fighter.State == CombatState.Skill && inputMoving && fighter.Skill.CanMoveCancel(fighter.SkillFrame))
            { fighter.StopSkill(); fighter.PendingMotion = Vector2Int.zero; }
            Vector2Int delta;
            if (fighter.State == CombatState.Skill || fighter.CompletingSkillThisFrame)
                delta = fighter.PendingMotion;
            else
            {
                if (fighter.Moving != inputMoving) fighter.PoseFrame = 0;
                fighter.Moving = inputMoving;
                if (inputMoving) fighter.Facing = fighter.MoveDirection * 3;
                delta = inputMoving ? CombatMath.Move(fighter.Facing, fighter.Library.Profile.movePerFrame) : Vector2Int.zero;
                fighter.PoseFrame++;
            }
            Vector2Int next = fighter.Position + delta;
            fighter.Position = new Vector2Int(CombatMath.Clamp(next.x, minX, maxX), CombatMath.Clamp(next.y, minZ, maxZ));
        }

        private void CollectHits(CombatFighter attacker)
        {
            if (attacker.BlockedThisFrame || attacker.State != CombatState.Skill || attacker.Skill.Data.hitRegions == null) return;
            foreach (var region in attacker.Skill.Data.hitRegions)
            {
                if (!region.IsActive(attacker.SkillFrame)) continue;
                foreach (var defender in fighters)
                {
                    if (defender == attacker || defender.Health == 0) continue;
                    long key = ((long)region.hitGroupId << 32) | (uint)defender.BattleId;
                    if (attacker.HitTargets.Contains(key)) continue;
                    if (!CombatCollision.Overlaps(region, attacker.Position, attacker.Facing, defender.Position, defender.Library.Profile.hurtRadius)) continue;
                    attacker.HitTargets.Add(key);
                    hits.Add(new PendingHit(attacker, defender, attacker.Skill.Group(region.hitGroupId)));
                }
            }
        }

        private void ResolveHits()
        {
            LastHitCount = hits.Count;
            // 命中列表已经封存：这里不能再检查攻击者是否被打断/死亡，否则先结算的一方会吞掉同帧对攻。
            foreach (var hit in hits)
            {
                hit.Defender.Health = Math.Max(0, hit.Defender.Health - hit.Group.damage);
                // 暂不读取命中组的 hitStopFrames；命中不再给双方施加暂停。
            }
            foreach (var fighter in fighters)
            {
                bool received = false;
                int stun = 0;
                foreach (var hit in hits) if (hit.Defender == fighter) { received = true; stun = Math.Max(stun, hit.Group.hitStunFrames); }
                if (!received) continue;
                fighter.StopSkill(); // 立即取消剩余攻击区域及位移，命中组记录属于这次释放，不跨技能保留。
                fighter.State = fighter.Health == 0 ? CombatState.Dead : CombatState.HitStun;
                fighter.HitStunRemaining = Math.Max(fighter.HitStunRemaining, stun);
                fighter.PoseFrame = 0; fighter.Moving = false; fighter.ReactionSerial++;
            }
        }

        /// <summary>人工双端对照用；仅摘要当前逻辑状态，不宣称是配置版本握手或防作弊校验。</summary>
        public uint StateHash()
        {
            uint hash = 2166136261;
            void Add(int value) { unchecked { hash = (hash ^ (uint)value) * 16777619; } }
            Add(Frame); Add(Finished ? 1 : 0); Add(WinnerId);
            foreach (var fighter in fighters)
            {
                Add(fighter.BattleId); Add(fighter.Position.x); Add(fighter.Position.y); Add(fighter.Facing); Add(fighter.Health);
                Add((int)fighter.State); Add(fighter.SkillFrame); Add(fighter.CastSerial); Add(fighter.HitStopRemaining);
                Add(fighter.HitStunRemaining); Add(fighter.PoseFrame); Add(fighter.ReactionSerial); Add(fighter.MoveDirection);
                Add(fighter.Moving ? 1 : 0); Add(fighter.FrozenThisFrame ? 1 : 0); Add(fighter.BlockedThisFrame ? 1 : 0);
                Add(fighter.Skill != null ? fighter.Skill.Data.skillId.Length : 0);
                if (fighter.Skill != null) foreach (char c in fighter.Skill.Data.skillId) Add(c);
                var keys = new List<long>(fighter.HitTargets); keys.Sort();
                Add(keys.Count);
                foreach (long key in keys) { Add((int)(key >> 32)); Add((int)key); }
            }
            return hash;
        }
    }
}
