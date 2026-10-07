
using TPF;
using Unity.VisualScripting;
using UnityEngine;

namespace ZZZ
{
    public class PlayerMovementStateMachine : StateMachine
    {
        //缓存初始状态
        public PlayerStateReusableData reusableData { get;  }
        public Player player { get; }
        public PlayerIdlingState idlingState { get; }
        public PlayerWalkingState walkingState { get; }
        public PlayerRunningState runningState { get; }
        public PlayerSprintingState sprintingState { get; }

        public PlayerDashingState dashingState { get;  }

        public PlayerDashBackingState dashBackingState { get; }

        public PlayerReturnRunState returnRunState{ get; }

        public PlayerOnSwitchState onSwitchState { get; }

        public PlayerOnSwitchOutState onSwitchOutState { get; }

        public PlayerMovementNullState playerMovementNullState { get; }
        private bool sprinting;
        private int lastMoveDirection = 121;
        public bool IsLocomotion => currentState.Value == idlingState || currentState.Value == walkingState
            || currentState.Value == runningState || currentState.Value == sprintingState;
        public bool IsDodgeOrSprint => currentState.Value == dashingState || currentState.Value == dashBackingState
            || currentState.Value == sprintingState;

        public void ApplyMovement(int direction)
        {
            bool moving = player.HasNetworkMovement;
            if (moving)
            {
                reusableData.targetAngle = 90f - direction * 3f;
                // Compare two logical directions, not the locally interpolated model rotation.
                if (currentState.Value == sprintingState && lastMoveDirection <= 120
                    && Mathf.Abs(Mathf.DeltaAngle(lastMoveDirection * 3f, direction * 3f)) > player.playerSO.movementData.turnBackAngle)
                {
                    player.characterAnimator.SetBool(AnimatorID.TurnBackID, true);
                    // Enter returnRunState when Animator actually enters TurnRun; keep sprint's
                    // blend parameter until then so the > 2.4 transition remains eligible.
                }
            }
            else
            {
                sprinting = false;
                player.characterAnimator.SetBool(AnimatorID.TurnBackID, false);
            }
            lastMoveDirection = direction;
            if (IsLocomotion) ReturnToLocomotion();
        }

        public void ToggleWalk()
        {
            reusableData.shouldWalk = !reusableData.shouldWalk;
            sprinting = false;
            player.characterAnimator.SetBool(AnimatorID.TurnBackID, false);
            if (IsLocomotion) ReturnToLocomotion();
        }

        public void ReturnToLocomotion(bool resumeSprint = false)
        {
            if (resumeSprint) sprinting = true;
            if (!player.HasNetworkMovement) sprinting = false;
            IState next = idlingState;
            if (player.HasNetworkMovement)
                next = reusableData.shouldWalk ? (IState)walkingState : sprinting ? sprintingState : (IState)runningState;
            ChangeState(next);
        }

        public void StartDash(bool forward)
        {
            sprinting = false;
            player.characterAnimator.SetBool(AnimatorID.TurnBackID, false);
            var data = player.playerSO.movementData.dashData;
            PlayerDodgingState state = forward ? (PlayerDodgingState)dashingState : dashBackingState;
            ChangeState(state);
            state.StartMotion(forward ? data.frontRootMotion : data.backRootMotion);
            player.characterAnimator.CrossFadeInFixedTime(forward ? data.frontDushAnimationName : data.backDushAnimationName, data.fadeTime);
        }

        public void EnterDash()
        {
            reusableData.rotationTime = player.playerSO.movementData.dashData.rotationTime;
            player.PlayDodgeSound();
        }

        public PlayerMovementStateMachine(Player P)
        {
            player = P;

            reusableData =new PlayerStateReusableData();
            //给状态传入该状态机的引用
           idlingState = new PlayerIdlingState(this);

            walkingState = new PlayerWalkingState(this);

            runningState = new PlayerRunningState(this);

            sprintingState = new PlayerSprintingState(this);

            dashingState = new PlayerDashingState(this);

            dashBackingState = new PlayerDashBackingState(this);

            returnRunState = new PlayerReturnRunState(this);

            onSwitchState=new PlayerOnSwitchState(this);

            onSwitchOutState=new PlayerOnSwitchOutState(this);

            playerMovementNullState=new PlayerMovementNullState(this);  

        }
            
    }
}
