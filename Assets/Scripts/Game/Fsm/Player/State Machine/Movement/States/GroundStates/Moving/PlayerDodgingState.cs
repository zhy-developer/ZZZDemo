namespace ZZZ
{
    public abstract class PlayerDodgingState : PlayerMovementState
    {
        private readonly bool forward;
        private int motionHandle;
        protected PlayerDodgingState(PlayerMovementStateMachine stateMachine, bool forward) : base(stateMachine)
        {
            this.forward = forward;
        }
        public override void Enter()
        {
            base.Enter();
            movementStateMachine.EnterDash();
        }
        public override void Update()
        {
            if (motionHandle != 0) return; // Logic_Move owns completion, not local Animator timing.
            if (HasAnimationFinished("Dodge")) movementStateMachine.ReturnToLocomotion(forward);
        }

        public void StartMotion(FrameSync.RootMotion.RootMotionSettings settings)
        {
            // Repeated forward dodges need a new cursor even when ChangeState skips re-entry.
            movementStateMachine.player.StopRootMotion(motionHandle);
            motionHandle = movementStateMachine.player.TryPlayRootMotion(settings, OnMotionCompleted);
        }

        private void OnMotionCompleted()
        {
            motionHandle = 0;
            if (movementStateMachine.currentState.Value != this) return;
            movementStateMachine.ReturnToLocomotion(forward);
            // The source clip can contain a long recovery tail beyond the baked motion window.
            animator.CrossFadeInFixedTime("Movement", playerMovementData.dashData.fadeTime);
        }

        public override void Exit()
        {
            movementStateMachine.player.StopRootMotion(motionHandle);
            motionHandle = 0;
            base.Exit();
        }
        public override void OnAnimationExitEvent() { Update(); }
    }
}
