namespace ZZZ
{
    public abstract class PlayerComboState : IState
    {
        protected readonly PlayerComboStateMachine Machine;
        protected PlayerComboState(PlayerComboStateMachine machine) { Machine = machine; }
        public virtual void Enter() { }
        public virtual void Exit() { }
        public virtual void Update() => Machine.Player.PlayPose(Machine.Frame.Animation, Machine.Frame.NormalizedTime);
        public void UpdateAnimationParameters() { }
        public void OnAnimationTranslateEvent(IState state) { }
        public void OnAnimationExitEvent() { }
    }
    public sealed class PlayerHitState : PlayerComboState
    { public PlayerHitState(PlayerComboStateMachine machine) : base(machine) { } }
    public sealed class PlayerDeadState : PlayerComboState
    { public PlayerDeadState(PlayerComboStateMachine machine) : base(machine) { } }
    public sealed class PlayerSkillViewState : PlayerComboState
    {
        public PlayerSkillViewState(PlayerComboStateMachine machine) : base(machine) { }
        public override void Update() => Machine.Player.PlayAttack(Machine.Frame);
    }
    public sealed class PlayerDodgeViewState : PlayerComboState
    {
        public PlayerDodgeViewState(PlayerComboStateMachine machine) : base(machine) { }
        public override void Update() => Machine.Player.PlayAttack(Machine.Frame);
    }
}
