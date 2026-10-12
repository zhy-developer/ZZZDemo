namespace ZZZ
{
    public sealed class PlayerRunningState : PlayerMovementState
    {
        public PlayerRunningState(PlayerMovementStateMachine machine) : base(machine) { }
        public override void Update() => Machine.Player.PlayLocomotion(Machine.Frame.IdleState, true);
    }
}
