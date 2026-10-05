namespace ZZZ
{
    public class PlayerReturnRunState : PlayerMovementState
    {
        public PlayerReturnRunState(PlayerMovementStateMachine stateMachine) : base(stateMachine) { }
        public override void Enter()
        {
            base.Enter();
            SetMovementParameters(playerMovementData.returnRunData.inputMult, playerMovementData.returnRunData.rotationTime, true);
        }
        public override void Update()
        {
            if (HasAnimationFinished("TurnRun")) movementStateMachine.ReturnToLocomotion(true);
        }
        public override void Exit() { animator.SetBool(AnimatorID.TurnBackID, false); }
        public override void OnAnimationExitEvent() { Update(); }
    }
}
