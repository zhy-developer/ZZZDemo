namespace ZZZ
{
    public abstract class PlayerDodgingState : PlayerMovementState
    {
        private readonly bool forward;
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
            if (HasAnimationFinished("Dodge")) movementStateMachine.ReturnToLocomotion(forward);
        }
        public override void OnAnimationExitEvent() { Update(); }
    }
}
