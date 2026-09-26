
using ZZZ;
using UnityEngine;
using UnityEngine.InputSystem;
using Tools;

namespace TPF
{
    public class PlayerIdlingState : PlayerMovementState
    {
        GameTimer GameTimer { get; set; }
        //调用父类的构造函数
        public PlayerIdlingState(PlayerMovementStateMachine playerMovementStateMachine) : base(playerMovementStateMachine)
        {

        }
        public override void Enter()
        {  
            base.Enter();
            reusableData.rotationTime= playerMovementData.idleData.rotationTime;
            animator.SetBool(AnimatorID.HasInputID, false);
            reusableData.inputMult = playerMovementData.idleData.inputMult;

        }
        public override void Update()
        {
            base.Update();
        }
        protected override void AddInputActionCallBacks()
        {
            base.AddInputActionCallBacks();
            CharacterInputSystem.Instance.inputActions.Player.Movement.started += bufferToRun;
        }
        protected override void RemoveInputActionCallBacks() 
        {
            base.RemoveInputActionCallBacks();
            CharacterInputSystem.Instance.inputActions.Player.Movement.started -= bufferToRun;

            TimerManager.Instance.UnregisterTimer(GameTimer);
        }

        /// <summary>
        /// 添加缓冲事件去run
        /// </summary>
        /// <param name="context"></param>
        private void bufferToRun(InputAction.CallbackContext context)
        {  
           //在0.11秒后检查移动输入，如果还有将会移动，若没有输入了说明角色只是短按转向
           GameTimer = TimerManager.Instance.GetTimer(0.11f,CheckMoveInput);
        }

        /// <summary>
        /// 检查移动输入
        /// </summary>
        private void CheckMoveInput()
        { 
            //视为轻击角色没有Walk或者Run而是Run_Start_End，（如果角色只是短按转向，播放Run_Start_End动画）
            if (CharacterInputSystem.Instance.PlayerMove == Vector2.zero)
            {
                 animator.CrossFadeInFixedTime("Run_Start_End",0.13f);
            }
            else
            {
                Move();
            }
        }

        private void Move()
        {
            if (movementStateMachine.reusableData.shouldWalk)
            {
                //切换到Walk状态
                movementStateMachine.ChangeState(movementStateMachine.walkingState);
                return;
            }
            //否则执行Run移动
            movementStateMachine.ChangeState(movementStateMachine.runningState);
        }

        public override void HandInput()
        {
            base.HandInput();

        }
        public override void Exit()
        {
            base.Exit();
        }
    }
}
