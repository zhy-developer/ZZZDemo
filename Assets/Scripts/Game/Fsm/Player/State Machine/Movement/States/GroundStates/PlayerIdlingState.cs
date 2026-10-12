namespace ZZZ
{
    public sealed class PlayerIdlingState : PlayerMovementState
    {
        public PlayerIdlingState(PlayerMovementStateMachine machine) : base(machine) { }
        public override void Update() => Machine.Player.PlayLocomotion(Machine.Frame.IdleState, false);
    }
}
