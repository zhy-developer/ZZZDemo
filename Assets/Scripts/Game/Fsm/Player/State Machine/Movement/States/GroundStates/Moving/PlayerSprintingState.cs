namespace ZZZ
{
    public class PlayerSprintingState : PlayerMovementState
    {
        public PlayerSprintingState(PlayerMovementStateMachine stateMachine) : base(stateMachine) { }
        public override void Enter()
        {
            base.Enter();
            SetMovementParameters(playerMovementData.sprintData.inputMult, playerMovementData.sprintData.rotationTime, true);
        }
    }
}
