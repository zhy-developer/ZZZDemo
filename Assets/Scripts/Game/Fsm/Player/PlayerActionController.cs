using GameProtocol;
using UnityEngine;

namespace ZZZ
{
    // Both local and remote players enter here only when an ordered server frame is consumed.
    public sealed class PlayerActionController
    {
        private readonly Player player;
        private int dashCooldownFrames;
        public PlayerActionController(Player player) { this.player = player; }

        public void LogicTick()
        {
            if (dashCooldownFrames > 0) dashCooldownFrames--;
            player.movementStateMachine.reusableData.canDash = dashCooldownFrames == 0;
        }

        public ActionPreparation Prepare(RightOpType action, int moving, out int payload)
        {
            payload = moving & 1;
            var combo = player.comboStateMachine;
            var executor = combo.Combo;
            switch (action)
            {
                case RightOpType.rop1:
                case RightOpType.rop6:
                    if (!executor.CanBaseComboInput()) return ActionPreparation.Discard;
                    bool dodge = action == RightOpType.rop1 && player.movementStateMachine.IsDodgeOrSprint;
                    if (!executor.TryGetAttackIndex(action == RightOpType.rop6, dodge, out int index))
                        return ActionPreparation.Discard;
                    // bit 0: moving; bit 1: dodge attack; remaining bits: exact combo step.
                    payload |= (dodge ? 2 : 0) | (index << 2);
                    // Lock the selected step as soon as pre-input is accepted, before waiting.
                    return combo.ReusableData.canATK ? ActionPreparation.Ready : ActionPreparation.Wait;
                case RightOpType.rop2:
                    return executor.CanSkillInput() ? ActionPreparation.Ready : ActionPreparation.Discard;
                case RightOpType.rop3:
                    return executor.CanFinishSkillInput() ? ActionPreparation.Ready : ActionPreparation.Discard;
                case RightOpType.rop4:
                    return player.movementStateMachine.reusableData.canDash && combo.currentState.Value != combo.SkillState
                        ? ActionPreparation.Ready : ActionPreparation.Discard;
                case RightOpType.rop5:
                    return ActionPreparation.Ready;
                default:
                    return ActionPreparation.Discard;
            }
        }

        public bool Execute(RightOpType action, int payload)
        {
            var movement = player.movementStateMachine;
            var combo = player.comboStateMachine;
            var executor = combo.Combo;
            switch (action)
            {
                case RightOpType.rop5:
                    movement.ToggleWalk();
                    return false; // No facing change for a movement-mode toggle.
                case RightOpType.rop4:
                    dashCooldownFrames = Mathf.Max(1, Mathf.CeilToInt(player.playerSO.movementData.dashData.coldTime
                        / (NetConfig.frameTime * 0.001f)));
                    movement.reusableData.canDash = false;
                    combo.ChangeState(combo.NullState);
                    executor.ReSetComboInfo();
                    movement.StartDash((payload & 1) != 0);
                    return true;
                case RightOpType.rop1:
                case RightOpType.rop6:
                    return executor.PlayNetworkAttack(action == RightOpType.rop6, (payload & 2) != 0, payload >> 2);
                case RightOpType.rop2:
                    if (player.playerSO.ComboData.comboData.skillCombo == null) return false;
                    executor.ReSetComboInfo();
                    executor.SkillInput();
                    combo.ChangeState(combo.SkillState);
                    return true;
                case RightOpType.rop3:
                    if (player.playerSO.ComboData.comboData.finishSkillCombo == null) return false;
                    executor.ReSetComboInfo();
                    executor.FinishSkillInput();
                    combo.ChangeState(combo.SkillState);
                    return true;
                default:
                    return false;
            }
        }
    }
}
