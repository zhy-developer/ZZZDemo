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
            if (player.IsPlayingAnimationTag(tag)) { animationObserved = true; return false; }
            return animationObserved;
        }
    }
}
