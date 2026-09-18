
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
            CharacterInputSystem.Instance.inputActions.Player.Skill.started += OnSkill;
            characterCombo.AddEventAction();
        }


        protected virtual void RemoveInputActionEvent()
        {
            CharacterInputSystem.Instance.inputActions.Player.L_AtK.started -= OnAttackInput;
            CharacterInputSystem.Instance.inputActions.Player.FinishSkill.started -= OnFinishSkill;
            CharacterInputSystem.Instance.inputActions.Player.Skill.started -= OnSkill;
            characterCombo.RemoveEventActon();
        }
        private void OnAttackInput(InputAction.CallbackContext context)
        {
            if (player.characterName != SwitchCharacter.Instance.newCharacterName.Value) { return; }
          
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

    }
}
