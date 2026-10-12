namespace ZZZ
{
    public abstract class PlayerMovementState : IState
    {
        protected readonly PlayerMovementStateMachine Machine;
        protected PlayerMovementState(PlayerMovementStateMachine machine) { Machine = machine; }
        public virtual void Enter() { }
        public virtual void Exit() { }
        public virtual void Update() { }
        public void UpdateAnimationParameters() { }
        public void OnAnimationTranslateEvent(IState state) { }
        public void OnAnimationExitEvent() { }
    }
}
