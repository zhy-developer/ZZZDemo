
using TPF;
using Unity.VisualScripting;
using UnityEngine;

namespace ZZZ
{
    /// <summary>移动 FSM 的表现出口，不提供位移、输入准入或伤害接口。</summary>
    public interface IMovementAnimationOutput
    {
        void SetBool(int id, bool value);
        void SetFloat(int id, float value, float dampTime, float deltaTime);
        void CrossFade(string state, float duration);
        bool IsPlaying(string tag);
    }

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
        private IMovementAnimationOutput presentationOutput;
        private bool presentationMoving;
        private float presentationDelta;
        public bool IsPresentationOnly => presentationOutput != null;
        public bool HasMovement => IsPresentationOnly ? presentationMoving : player.HasNetworkMovement;

        public void BindPresentation(IMovementAnimationOutput output)
        {
            currentState.Value = null;
            presentationOutput = output;
        }

        /// <summary>只由表现适配器调用；自由行动时根据已确认移动结果选择旧的基础移动状态。</summary>
        public void PresentMovement(bool moving, bool reenter, float deltaTime)
        {
            if (!IsPresentationOnly) return;
            presentationMoving = moving;
            presentationDelta = deltaTime;
            // 从技能返回时重新进入，移动状态的起步动画不能因状态对象相同而被跳过。
            if (reenter) { currentState.Value?.Exit(); currentState.Value = null; }
            IState next = moving ? (reusableData.shouldWalk ? (IState)walkingState : runningState) : idlingState;
            ChangeState(next);
            currentState.Value.UpdateAnimationParameters();
            // 不调用旧 Update / 动画回调：它们可能触发闪避完成、切人或战斗状态变化。
        }

        public void UnbindPresentation()
        {
            if (!IsPresentationOnly) return;
            currentState.Value?.Exit(); currentState.Value = null;
            presentationOutput = null;
        }

        public void SetAnimationBool(int id, bool value)
        {
            if (IsPresentationOnly) presentationOutput.SetBool(id, value);
            else player.characterAnimator.SetBool(id, value);
        }
        public void SetAnimationFloat(int id, float value, float damping)
        {
            if (IsPresentationOnly) presentationOutput.SetFloat(id, value, damping, presentationDelta);
            else player.characterAnimator.SetFloat(id, value, damping, Time.deltaTime);
        }
        public void CrossFadeAnimation(string state, float duration)
        {
            if (IsPresentationOnly) presentationOutput.CrossFade(state, duration);
            else player.characterAnimator.CrossFadeInFixedTime(state, duration);
        }
        public bool IsPlayingAnimation(string tag) => IsPresentationOnly
            ? presentationOutput.IsPlaying(tag) : player.IsPlayingAnimationTag(tag);
        public bool IsLocomotion => currentState.Value == idlingState || currentState.Value == walkingState
            || currentState.Value == runningState || currentState.Value == sprintingState;
        public bool IsDodgeOrSprint => currentState.Value == dashingState || currentState.Value == dashBackingState
            || currentState.Value == sprintingState;

        public void ApplyMovement(int direction)
        {
            if (IsPresentationOnly) return;
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
            if (IsPresentationOnly) return;
            reusableData.shouldWalk = !reusableData.shouldWalk;
            sprinting = false;
            player.characterAnimator.SetBool(AnimatorID.TurnBackID, false);
            if (IsLocomotion) ReturnToLocomotion();
        }

        public void ReturnToLocomotion(bool resumeSprint = false)
        {
            if (IsPresentationOnly) return;
            if (resumeSprint) sprinting = true;
            if (!player.HasNetworkMovement) sprinting = false;
            IState next = idlingState;
            if (player.HasNetworkMovement)
                next = reusableData.shouldWalk ? (IState)walkingState : sprinting ? sprintingState : (IState)runningState;
            ChangeState(next);
        }

        public void StartDash(bool forward)
        {
            if (IsPresentationOnly) return;
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
            if (IsPresentationOnly) return;
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
