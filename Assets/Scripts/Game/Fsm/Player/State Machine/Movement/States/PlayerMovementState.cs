using UnityEngine;

namespace ZZZ
{
    public class PlayerMovementState : IState
    {
        protected PlayerMovementStateMachine movementStateMachine { get; }
        protected Animator animator { get; }
        protected Transform playerTransform { get; }
        protected PlayerMovementData playerMovementData { get; }
        protected PlayerStateReusableData reusableData { get; }
        private bool animationObserved;

        public PlayerMovementState(PlayerMovementStateMachine stateMachine)
        {
            movementStateMachine = stateMachine;
            animator = stateMachine.player.characterAnimator;
            playerTransform = stateMachine.player.transform;
            playerMovementData = stateMachine.player.playerSO.movementData;
            reusableData = stateMachine.reusableData;
        }

        public virtual void Enter() { animationObserved = false; }
        public virtual void Exit() { }
        public virtual void Update() { }
        public virtual void UpdateAnimationParameters()
        {
            animator.SetFloat(AnimatorID.MovementID,
                movementStateMachine.player.HasNetworkMovement ? reusableData.inputMult : 0f, 0.35f, Time.deltaTime);
        }
        public virtual void OnAnimationTranslateEvent(IState state) { movementStateMachine.ChangeState(state); }
        public virtual void OnAnimationExitEvent() { }

        protected void SetMovementParameters(float inputMultiplier, float rotationTime, bool hasInput)
        {
            reusableData.inputMult = inputMultiplier;
            reusableData.rotationTime = rotationTime;
            animator.SetBool(AnimatorID.HasInputID, hasInput);
        }

        // During a crossfade the exiting animation can report an exit after the new state entered.
        // Only finish after this state's own animation was observed.
        protected bool HasAnimationFinished(string tag)
        {
            if (movementStateMachine.player.IsPlayingAnimationTag(tag))
            {
                animationObserved = true;
                return false;
            }
            return animationObserved;
        }
    }
}
