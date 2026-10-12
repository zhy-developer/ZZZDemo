namespace ZZZ
{
    public sealed class PlayerNullState : PlayerComboState
    {
        public PlayerNullState(PlayerComboStateMachine machine) : base(machine) { }
        public override void Update() { }
    }
}
