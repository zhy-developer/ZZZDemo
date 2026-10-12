namespace ZZZ
{
    public sealed class PlayerMovementNullState : PlayerMovementState
    {
        public PlayerMovementNullState(PlayerMovementStateMachine machine) : base(machine) { }
        public override void Enter() => Machine.Player.StopPilotPresentation();
    }
}
