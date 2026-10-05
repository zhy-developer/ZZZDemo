namespace ZZZ
{
    public class PlayerWalkingState : PlayerMovementState
    {
        public PlayerWalkingState(PlayerMovementStateMachine stateMachine) : base(stateMachine) { }
        public override void Enter()
        {
            base.Enter();
            if (!movementStateMachine.player.IsPlayingAnimationTag("Movement"))
                animator.CrossFadeInFixedTime("WalkStart", 0.14f);
            SetMovementParameters(playerMovementData.walkData.inputMult, playerMovementData.walkData.rotationTime, true);
        }
    }
}
