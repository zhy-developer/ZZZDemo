
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Tools;

namespace ZZZ
{

    public class PlayerComboState : IState
    {
        protected Player player{ get; }
        protected PlayerComboStateMachine comboStateMachine { get; }
        protected CharacterCombo characterCombo { get; }
        protected PlayerComboReusableData reusableData { get;  }
        protected PlayerComboData playerComboData { get; }
        protected Animator animator { get; }
        public PlayerComboState(PlayerComboStateMachine comboStateMachine)
        {
           this.comboStateMachine = comboStateMachine;

            if (player == null)
            {
                player = comboStateMachine.Player;
            }  
            if (animator == null)
            {
                animator = comboStateMachine.Player.characterAnimator;
            }
            if (playerComboData == null)
            {
                playerComboData = player.playerSO.ComboData;
            }
            if (reusableData == null)
            {
                reusableData = this.comboStateMachine.ReusableData;
            }
            if (characterCombo == null)
            {
                characterCombo = new CharacterCombo(animator, player.transform,player.camera, reusableData, playerComboData.comboData,playerComboData.playerEnemyDetectionData,player);
            }
           
        }
        public virtual void Enter()
        {
            AddInputActionEvent();
        }

        public virtual void Exit()
        {
            RemoveInputActionEvent();
        }

        public virtual void HandInput()
        {
          
        }

        public virtual void OnAnimationExitEvent()
        {

        }

        public virtual void OnAnimationTranslateEvent(IState state)
        {

        }

        public virtual void Update()
        {
            characterCombo.UpdateComboAnimation();
            characterCombo.UpdateEnemy();
            characterCombo.CheckCanLinkCombo();
        }
        protected virtual void AddInputActionEvent()
        {
            CharacterInputSystem.Instance.inputActions.Player.L_AtK.started += OnAttackInput;
            CharacterInputSystem.Instance.inputActions.Player.FinishSkill.started += OnFinishSkill;
            CharacterInputSystem.Instance.inputActions.Player.Execute.started += OnSkill;
            characterCombo.AddEventAction();
        }


        protected virtual void RemoveInputActionEvent()
        {
            CharacterInputSystem.Instance.inputActions.Player.L_AtK.started -= OnAttackInput;
            CharacterInputSystem.Instance.inputActions.Player.FinishSkill.started -= OnFinishSkill;
            CharacterInputSystem.Instance.inputActions.Player.Execute.started -= OnSkill;
            characterCombo.RemoveEventActon();
        }
        private void OnAttackInput(InputAction.CallbackContext context)
        {
            if (player.characterName != SwitchCharacter.Instance.newCharacterName.Value) { return; }
            Debug.Log($"[AttackDebug] Attack input. character={player.characterName}, movementState={player.currentMovementState}, comboState={player.currentComboState}, {BuildAnimatorLayerDebug(0)}, animatorSpeed={animator.speed}, timeScale={Time.timeScale}, unscaledTime={Time.unscaledTime}");
          
            if (characterCombo.CanBaseComboInput())
            {
                if (player.currentMovementState == "PlayerSprintingState" || animator.AnimationAtTag("Dodge"))
                {
                    characterCombo.DodgeComboInput();
                    Debug.Log("闪避攻击");
                }
                else
                {
                  
                    characterCombo.LightComboInput();
                }

            }
        }
        private void OnFinishSkill(InputAction.CallbackContext context)
        {
            if (player.characterName != SwitchCharacter.Instance.newCharacterName.Value) { return; }
            if (characterCombo.CanFinishSkillInput())
            {
                characterCombo.FinishSkillInput();
                comboStateMachine.ChangeState(comboStateMachine.SkillState);
            }
        }
        private void OnSkill(InputAction.CallbackContext context)
        {
            if (player.characterName != SwitchCharacter.Instance.newCharacterName.Value) { return; }
            if (characterCombo.CanSkillInput())
            {
                characterCombo.SkillInput();
                comboStateMachine.ChangeState(comboStateMachine.SkillState);
            }
        }
        public void SwitchSkill()
        {
            characterCombo.SwitchSkill(player.characterName);
            //切换到技能状态
            comboStateMachine.ChangeState(comboStateMachine.SkillState);
        }

        private string BuildAnimatorLayerDebug(int layerIndex)
        {
            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(layerIndex);
            bool isInTransition = animator.IsInTransition(layerIndex);

            string log = $"currentHash={currentState.shortNameHash}, normalizedTime={currentState.normalizedTime}, length={currentState.length}, speed={currentState.speed}, speedMultiplier={currentState.speedMultiplier}, isIdleTag={currentState.IsTag("Idle")}, isMovementTag={currentState.IsTag("Movement")}, isATKTag={currentState.IsTag("ATK")}, isIdleName={currentState.IsName("Idle")}, isStandName={currentState.IsName("Stand")}, isLocomotionName={currentState.IsName("Locomotion")}, isAnbiNormal1Name={currentState.IsName("Anbi_Normal_1")}, isInTransition={isInTransition}";

            if (!isInTransition)
            {
                return log;
            }

            AnimatorTransitionInfo transition = animator.GetAnimatorTransitionInfo(layerIndex);
            AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(layerIndex);
            return $"{log}, transitionHash={transition.fullPathHash}, transitionNameHash={transition.nameHash}, transitionUserNameHash={transition.userNameHash}, transitionNormalizedTime={transition.normalizedTime}, transitionDuration={transition.duration}, nextHash={nextState.shortNameHash}, nextNormalizedTime={nextState.normalizedTime}, nextIsIdleTag={nextState.IsTag("Idle")}, nextIsMovementTag={nextState.IsTag("Movement")}, nextIsATKTag={nextState.IsTag("ATK")}, nextIsIdleName={nextState.IsName("Idle")}, nextIsStandName={nextState.IsName("Stand")}, nextIsLocomotionName={nextState.IsName("Locomotion")}, nextIsAnbiNormal1Name={nextState.IsName("Anbi_Normal_1")}";
        }
           


    }
}
