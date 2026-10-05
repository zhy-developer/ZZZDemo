using ZZZ;

namespace TPF
{
    public class PlayerRunningState : PlayerMovementState
    {
        public PlayerRunningState(PlayerMovementStateMachine stateMachine) : base(stateMachine) { }

        public override void Enter()
        {
            base.Enter();
            animator.CrossFadeInFixedTime("WalkStart", 0.14f);
            SetMovementParameters(playerMovementData.runData.inputMult, playerMovementData.runData.rotationTime, true);
        }

        // 仅进入 Run 时播放起跑；后续移动帧不会重复进入此状态。
    }
}
