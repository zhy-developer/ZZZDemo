namespace ZZZ
{
    public sealed class PlayerATKIngState : PlayerComboState
    {
        public PlayerATKIngState(PlayerComboStateMachine machine) : base(machine) { }
        public override void Update() => Machine.Player.PlayAttack(Machine.Frame);
    }
}
