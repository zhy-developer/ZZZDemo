namespace ZZZ
{
    public class PlayerMovementNullState : PlayerMovementState
    {
        private bool actionObserved;
        public PlayerMovementNullState(PlayerMovementStateMachine stateMachine) : base(stateMachine) { }
        public override void Enter()
        {
            base.Enter();
            actionObserved = false;
            reusableData.rotationTime = playerMovementData.comboRotaionTime;
        }
        public override void Update()
        {
            var player = movementStateMachine.player;
            if (player.IsPlayingAnimationTag("ATK") || player.IsPlayingAnimationTag("Skill"))
                actionObserved = true;
            else if (actionObserved && player.comboStateMachine.currentState.Value == player.comboStateMachine.NullState)
                movementStateMachine.ReturnToLocomotion();
        }
        public override void OnAnimationExitEvent() { Update(); }
    }
}
