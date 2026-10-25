using System.Collections.Generic;
using UnityEngine;

namespace ZZZ.SkillRuntime
{
    public enum CombatState { Free = 0, Skill = 1, HitStun = 2, Dead = 3, Charging = 4 }
    public enum CombatReactionKind { Hit, ParryBroken }

    /// <summary>单个角色的独立逻辑状态。只有 CombatWorld 修改；不读取 Animator、Transform 或本地输入。</summary>
    public sealed class CombatFighter
    {
        public int BattleId { get; }
        public CombatSkillLibrary Library { get; }
        public Vector2Int Position { get; internal set; }
        public int Facing { get; internal set; }
        public int Health { get; internal set; }
        public CombatState State { get; internal set; }
        public CombatSkill Skill { get; internal set; }
        public int SkillFrame { get; internal set; }
        public int CastSerial { get; internal set; }
        public int SkillCommand { get; internal set; }
        public bool HasBufferedCombo { get; internal set; }
        public int BufferedComboFacing { get; internal set; } = 121;
        public int DodgeCooldownRemaining { get; internal set; }
        public int ChargeFrame { get; internal set; }
        // 按住标记只由同步的 E 按下/松开事件改变；打断不能伪造松开，否则长按可能自动补放。
        public bool SkillButtonHeld { get; internal set; }
        public bool ParryConsumed { get; internal set; }
        public CombatReactionKind ReactionKind { get; internal set; }
        public bool IsParrying => State == CombatState.Skill && Library.IsParry(Skill);
        public bool CanParry => State == CombatState.Skill && Skill == Library.Parry
            && !ParryConsumed && Skill.HasWindow(SkillAuthoring.SkillWindowKind.Parry, SkillFrame);
        public bool IsDodging => State == CombatState.Skill && Library.IsDodge(Skill);
        public bool IsInvulnerable => State == CombatState.Skill
            && Skill.HasWindow(SkillAuthoring.SkillWindowKind.Invulnerable, SkillFrame);
        public int HitStopRemaining { get; internal set; }
        public int HitStunRemaining { get; internal set; }
        public int ReactionSerial { get; internal set; }
        public int PoseFrame { get; internal set; }
        public bool FrozenThisFrame { get; internal set; }
        public bool BlockedThisFrame { get; internal set; }
        public bool Moving { get; internal set; }
        public int MoveDirection { get; internal set; } = 121;
        internal Vector2Int PendingMotion;
        internal bool CompletingSkillThisFrame;
        internal bool StartedSkillThisFrame;
        internal readonly CombatRegionAnchors RegionAnchors = new CombatRegionAnchors();
        // 每次释放重置；同组多个区域对同一目标的同一批次只产生一次候选命中。
        internal readonly HashSet<CombatHitKey> HitTargets = new HashSet<CombatHitKey>();
        // 每逻辑帧清空；场景适配层在该帧结束时复制到表现队列，不参与战斗状态摘要。
        internal readonly List<CombatPresentationEvent> PresentationEvents = new List<CombatPresentationEvent>();
        // 受击方持有已结算的反馈；与技能事件分离，避免受击打断或同帧互击取消命中反馈。
        internal readonly List<CombatHitFeedbackEvent> HitFeedbackEvents = new List<CombatHitFeedbackEvent>();

        public CombatFighter(int battleId, CombatSkillLibrary library, Vector2Int position, int facing)
        {
            BattleId = battleId; Library = library; Position = position; Facing = facing;
            Health = library.Profile.maxHealth;
        }
        internal void StopSkill(bool interrupted = true)
        {
            if (Skill != null) PresentationEvents.Add(new CombatPresentationEvent(CastSerial, null, interrupted, Skill.Data));
            Skill = null; SkillFrame = 0; HitTargets.Clear();
            RegionAnchors.Clear();
            SkillCommand = 0; HasBufferedCombo = false; BufferedComboFacing = 121;
            ChargeFrame = 0; ParryConsumed = false;
            if (State == CombatState.Skill || State == CombatState.Charging) { State = CombatState.Free; PoseFrame = 0; }
        }
    }

    public readonly struct CombatCommand
    {
        public readonly int BattleId, MoveDirection, Action, Facing, Payload;
        public CombatCommand(int battleId, int moveDirection, int action, int facing, int payload = 0)
        { BattleId = battleId; MoveDirection = moveDirection; Action = action; Facing = facing; Payload = payload; }
    }
}
