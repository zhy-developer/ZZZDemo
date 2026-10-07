using UnityEngine;

namespace ZZZ
{
    public class PlayerComboState : IState
    {
        protected Player player { get; }
        protected PlayerComboStateMachine comboStateMachine { get; }
        protected CharacterCombo characterCombo { get; }
        protected PlayerComboReusableData reusableData { get; }
        protected PlayerComboData playerComboData { get; }
        protected Animator animator { get; }
        private bool animationObserved;

        public PlayerComboState(PlayerComboStateMachine stateMachine)
        {
            comboStateMachine = stateMachine;
            player = stateMachine.Player;
            animator = player.characterAnimator;
            reusableData = stateMachine.ReusableData;
            playerComboData = player.playerSO.ComboData;
            characterCombo = stateMachine.Combo;
        }
        public virtual void Enter()
        {
            animationObserved = false;
            characterCombo.AddEventAction();
        }
        public virtual void Exit() { characterCombo.RemoveEventActon(); }
        public virtual void UpdateAnimationParameters() { }
        public virtual void OnAnimationExitEvent() { }
        public virtual void OnAnimationTranslateEvent(IState state) { comboStateMachine.ChangeState(state); }
        public virtual void Update()
        {
            characterCombo.UpdateComboAnimation();
            characterCombo.CheckCanLinkCombo();
        }
        protected bool HasAnimationFinished(string tag)
        {
            // The outgoing clip still delivers events during its blend-out. Looking only
            // at the destination state would discard its remaining combo input window.
            var current = animator.GetCurrentAnimatorStateInfo(0);
            bool playing = current.IsTag(tag) || current.IsName(tag);
            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                playing |= next.IsTag(tag) || next.IsName(tag);
            }
            if (playing) { animationObserved = true; return false; }
            return animationObserved;
        }
    }
}
