using UnityEngine;

namespace ZZZ
{
    public class PlayerATKIngState : PlayerComboState
    {
        public PlayerATKIngState(PlayerComboStateMachine stateMachine) : base(stateMachine) { }
        public override void Update()
        {
            base.Update();
            characterCombo.CheckMoveInterrupt();
            if (HasAnimationFinished("ATK") && !reusableData.hasATKCommand
                && comboStateMachine.currentState.Value == this)
            {
                player.TraceCombo("ATTACK_FINISHED", "Neither current nor incoming Animator state reports ATK");
                comboStateMachine.ChangeState(comboStateMachine.NullState);
                player.movementStateMachine.ReturnToLocomotion();
            }
        }
        public void CancelAttackColdTime() { characterCombo.CanATK(); }
        public void EnablePreInput() { characterCombo.CanInput(); }
        public void EnableMoveInterrupt() { characterCombo.CanMoveInterrupt(); }
        public void DisableLinkCombo() { characterCombo.DisConnectCombo(); }
        public void ATK(AnimationEvent animationEvent = null) { characterCombo.ATK(animationEvent); }
        // Completion is checked in Update so an old clip's exit cannot finish a newly queued attack.
    }
}
