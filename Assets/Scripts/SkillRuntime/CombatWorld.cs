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
        private readonly CombatHitResolver hitResolver = new CombatHitResolver();
        private readonly int minX, maxX, minZ, maxZ;
        public int Frame { get; private set; }
        public bool Finished { get; private set; }
        public int WinnerId { get; private set; } // 0 为双方同时归零。
        public int LastHitCount => hitResolver.HitCount;
        public int LastParryCount => hitResolver.ParryCount;

        public CombatWorld(List<CombatFighter> actors, int minX, int maxX, int minZ, int maxZ)
        {
            if (actors.Count != 2 || actors[0].BattleId == actors[1].BattleId) throw new ArgumentException("对战只支持两个不同 battleID 的角色。");
            if (minX >= maxX || minZ >= maxZ || !CombatMath.InRange(minX) || !CombatMath.InRange(maxX)
                || !CombatMath.InRange(minZ) || !CombatMath.InRange(maxZ)) throw new ArgumentException("地图边界无效或超出整数安全范围。");
            fighters = new List<CombatFighter>(actors);
            fighters.Sort((a, b) => a.BattleId.CompareTo(b.BattleId));
            this.minX = minX; this.maxX = maxX; this.minZ = minZ; this.maxZ = maxZ;
        }

        public void Step(IReadOnlyList<CombatCommand> commands)
        {
            Frame++;
            hitResolver.Clear();
            // 即使对局已经结束，也不让最后一击的通知残留给下一帧重复消费。
            foreach (var fighter in fighters) fighter.HitFeedbackEvents.Clear();
            if (Finished) return; // 网络仍消费和确认输入；已结束的对局不再产生战斗行为。
            foreach (var fighter in fighters) BeginFrame(fighter);
            // 先处理闪避/招架，再收 E 边沿与普通攻击。F0 窗口在本帧接触收集之前已经生效。
            foreach (var fighter in fighters)
                foreach (var command in commands)
                    if (command.BattleId == fighter.BattleId) CombatDodge.Accept(fighter, command);
            foreach (var fighter in fighters)
                foreach (var command in commands)
                    if (command.BattleId == fighter.BattleId) CombatParry.Accept(fighter, command);
            foreach (var fighter in fighters)
                foreach (var command in commands)
                    if (command.BattleId == fighter.BattleId) CombatCharge.Accept(fighter, command);
            foreach (var fighter in fighters) CombatCharge.Advance(fighter);
            foreach (var fighter in fighters)
                foreach (var command in commands)
                    if (command.BattleId == fighter.BattleId) ApplyCommand(fighter, command);
            // 优先级：成功闪避 > 招架 > E 蓄力/释放 > 连段 > 移动取消。失败的闪避不吞掉原有连段缓存。
            foreach (var fighter in fighters) CombatSkillInput.AdvanceCombo(fighter);
            foreach (var fighter in fighters) Move(fighter);
            foreach (var fighter in fighters) fighter.RegionAnchors.Capture(fighter);
            // 移动取消/切段已经确定，只有仍生效技能的当前帧能发出表现事件。
            foreach (var fighter in fighters) CombatSkillEvents.Collect(fighter);
            foreach (var attacker in fighters) hitResolver.Collect(attacker, fighters);
            hitResolver.Resolve(fighters, Frame);
            if (fighters.Exists(f => f.Health == 0))
            {
                Finished = true;
                foreach (var fighter in fighters)
                {
                    fighter.HasBufferedCombo = false; fighter.BufferedComboFacing = 121;
                    if (fighter.Health > 0) WinnerId = fighter.BattleId;
                }
            }
        }

        private static void BeginFrame(CombatFighter fighter)
        {
            fighter.PresentationEvents.Clear();
            fighter.PendingMotion = Vector2Int.zero;
            fighter.CompletingSkillThisFrame = false;
            fighter.StartedSkillThisFrame = false;
            // 独立于技能进度和受击硬直；F 帧成功设置 24，F+24 帧可再次释放。
            if (fighter.DodgeCooldownRemaining > 0) fighter.DodgeCooldownRemaining--;
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
                    fighter.StopSkill(false);
                }
            }
        }

        private static void ApplyCommand(CombatFighter fighter, CombatCommand command)
        {
            fighter.MoveDirection = command.MoveDirection >= 0 && command.MoveDirection < 120 ? command.MoveDirection : 121;
            CombatSkillInput.Accept(fighter, command);
        }

        private void Move(CombatFighter fighter)
        {
            if (fighter.BlockedThisFrame) return;
            // 原地蓄力、锁定朝向。可以闪避或招架退出，普通移动不隐式取消蓄力。
            if (fighter.State == CombatState.Charging)
            {
                fighter.Position = new Vector2Int(CombatMath.Clamp(fighter.Position.x + fighter.PendingMotion.x, minX, maxX),
                    CombatMath.Clamp(fighter.Position.y + fighter.PendingMotion.y, minZ, maxZ));
                return;
            }
            bool inputMoving = fighter.MoveDirection < 120;
            if (fighter.State == CombatState.Skill && !fighter.StartedSkillThisFrame
                && inputMoving && fighter.Skill.CanMoveCancel(fighter.SkillFrame))
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
                Add(fighter.SkillCommand); Add(fighter.HasBufferedCombo ? 1 : 0); Add(fighter.BufferedComboFacing);
                Add(fighter.DodgeCooldownRemaining);
                Add(fighter.ChargeFrame); Add(fighter.SkillButtonHeld ? 1 : 0);
                Add(fighter.ParryConsumed ? 1 : 0); Add((int)fighter.ReactionKind);
                Add(fighter.HitStunRemaining); Add(fighter.PoseFrame); Add(fighter.ReactionSerial); Add(fighter.MoveDirection);
                Add(fighter.Moving ? 1 : 0); Add(fighter.FrozenThisFrame ? 1 : 0); Add(fighter.BlockedThisFrame ? 1 : 0);
                Add(fighter.Skill != null ? fighter.Skill.Data.skillId.Length : 0);
                if (fighter.Skill != null) foreach (char c in fighter.Skill.Data.skillId) Add(c);
                fighter.RegionAnchors.AppendState(Add);
                var keys = new List<CombatHitKey>(fighter.HitTargets); keys.Sort();
                Add(keys.Count);
                foreach (var key in keys) { Add(key.GroupId); Add(key.TargetId); Add(key.HitFrame); }
            }
            return hash;
        }
    }
}
