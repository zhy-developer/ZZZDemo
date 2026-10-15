using System.Collections.Generic;
using UnityEngine;

namespace ZZZ.SkillRuntime
{
    public enum CombatState { Free, Skill, HitStun, Dead }

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
            if (State == CombatState.Skill) { State = CombatState.Free; PoseFrame = 0; }
        }
    }

    public readonly struct CombatCommand
    {
        public readonly int BattleId, MoveDirection, Action, Facing;
        public CombatCommand(int battleId, int moveDirection, int action, int facing)
        { BattleId = battleId; MoveDirection = moveDirection; Action = action; Facing = facing; }
    }
}
