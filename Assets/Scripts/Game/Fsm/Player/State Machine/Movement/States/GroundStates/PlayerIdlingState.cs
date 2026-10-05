using ZZZ;

namespace TPF
{
    public class PlayerIdlingState : PlayerMovementState
    {
        public PlayerIdlingState(PlayerMovementStateMachine stateMachine) : base(stateMachine) { }

        public override void Enter()
        {
            base.Enter();
            SetMovementParameters(playerMovementData.idleData.inputMult, playerMovementData.idleData.rotationTime, false);
        }

        // Idle/Run 的切换统一由下发帧驱动，不注册本机输入回调或延迟计时器。
    }
}
