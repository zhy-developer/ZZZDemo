namespace ZZZ
{
    public sealed class PlayerMovementStateMachine : StateMachine
    {
        public Player Player { get; }
        public PlayerRenderFrame Frame { get; private set; }
        readonly PlayerIdlingState idle;
        readonly PlayerRunningState moving;
        readonly PlayerMovementNullState suspended;
        public PlayerMovementStateMachine(Player player)
        {
            Player = player; idle = new PlayerIdlingState(this);
            moving = new PlayerRunningState(this); suspended = new PlayerMovementNullState(this);
        }
        public void Apply(PlayerRenderFrame frame)
        {
            Frame = frame;
            ChangeState(frame.Action != PlayerRenderAction.None ? (IState)suspended : frame.Moving ? (IState)moving : idle);
            Update();
        }
    }
}
