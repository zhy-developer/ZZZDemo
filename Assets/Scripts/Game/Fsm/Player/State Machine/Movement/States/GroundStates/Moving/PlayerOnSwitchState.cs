using System.Collections;
using System.Collections.Generic;
using ZZZ;
using UnityEngine;
namespace ZZZ
{
    public class PlayerOnSwitchState : PlayerMovementState
    {
        //切换进入
        public PlayerOnSwitchState(PlayerMovementStateMachine playerMovementStateMachine) : base(playerMovementStateMachine)
        {
        }
        public override void Enter()
        {
            base.Enter();
            //播放音效
            movementStateMachine.player.PlaySwitchWindSound();
            movementStateMachine.player.PlaySwitchInVoice();
        }

        public override void OnAnimationExitEvent()
        {
            movementStateMachine.ReturnToLocomotion();
        }
    }
}