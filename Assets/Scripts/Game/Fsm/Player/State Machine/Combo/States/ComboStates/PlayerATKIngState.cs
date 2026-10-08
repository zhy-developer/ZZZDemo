using UnityEngine;

namespace ZZZ
{
    public class PlayerATKIngState : PlayerComboState
    {
        private int motionHandle;
        public PlayerATKIngState(PlayerComboStateMachine stateMachine) : base(stateMachine) { }

        // Consecutive combo steps reuse this state, so each step explicitly replaces its motion.
        public void StartMotion(FrameSync.RootMotion.RootMotionSettings settings)
        {
            player.StopRootMotion(motionHandle);
            motionHandle = player.TryPlayRootMotion(settings);
        }

        public override void Exit()
        {
            player.StopRootMotion(motionHandle);
            motionHandle = 0;
            base.Exit();
        }
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
