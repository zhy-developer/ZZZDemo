
using UnityEngine;
using UnityEngine.InputSystem;
using Tools;

namespace ZZZ
{
    public class CharacterCombo : CharacterComboBase
    {

        public CharacterCombo(Animator animator, Transform playerTransform,Transform cameraTransform, PlayerComboReusableData reusableData, PlayerComboSOData playerComboSOData, PlayerEnemyDetectionData playerEnemyDetectionData,Player player) : base(animator, playerTransform, cameraTransform, reusableData, playerComboSOData, playerEnemyDetectionData , player )
        {
           
        }

        #region 闪A处理
        public  void DodgeComboInput()
        {
            //以后可以改为给获取Player示例，Player根据PlayerSO获取是否还有特殊闪避攻击
            switch (SwitchCharacter.Instance.newCharacterName.Value)
            {
                case CharacterNameList.AnBi:
                    {
                        NormalDodgeCombo();
                    }
                    break;
                case CharacterNameList.Xingjianya:
                    NormalDodgeCombo();
                    break;
            }
        }

        #endregion

        #region 处决处理
       
        #endregion

        #region 技能处理

        /// <summary>
        /// 主动技能
        /// </summary>
        /// <returns></returns>
        public bool CanFinishSkillInput()
        {
            if (animator.AnimationAtTag("Skill")) { return false; }
            if (animator.AnimationAtTag("Hit")) { return false; }
            if (animator.AnimationAtTag("Parry")) { return false; }
            if (animator.AnimationAtTag("ATK")) { return false; }
            if (comboData.finishSkillCombo == null) { return false; }
         
            return true;

        }
        public bool CanSkillInput()
        {
            if (animator.AnimationAtTag("Skill")) { return false; }
            if (animator.AnimationAtTag("Hit")) { return false; }
            if (animator.AnimationAtTag("Parry")) { return false; }
            if (animator.AnimationAtTag("ATK")) { return false; }
            if (comboData.skillCombo == null) { return false; }

            return true;

        }

        /// <summary>
        /// 终极大招
        /// </summary>
        public void FinishSkillInput()
        {
            if (comboData.finishSkillCombo == null) { return; }
            if (comboResuableData.currentCombo == null || comboResuableData.currentCombo != comboData.finishSkillCombo)
            {
                comboResuableData.currentSkill = comboData.finishSkillCombo;
            }
            ExecuteSkill();
        }
        /// <summary>
        /// 大招
        /// </summary>
        public void SkillInput()
        {
            if (comboData.skillCombo == null) {
                DeLogger.LogErrorTrace("SkillCombo数据为空");
                return; 
            }
            if (comboResuableData.currentCombo == null || comboResuableData.currentCombo != comboData.skillCombo)
            {
                comboResuableData.currentSkill = comboData.skillCombo;
            }
            ExecuteSkill();
        }
       
        /// <summary>
        /// 执行大招
        /// </summary>
        private void ExecuteSkill()
        {
            ReSetATKIndex(0);
            //播放语音
            PlayCharacterVoice(comboResuableData.currentSkill);
            //播放武器音效
            PlayWeaponSound(comboResuableData.currentSkill);
            animator.CrossFadeInFixedTime(comboResuableData.currentSkill.comboName, 0.1f);
        }

        /// <summary>
        /// 被动技能:等待选择切换的角色
        /// </summary>
        /// <param name="attacker"></param>
        protected override void CanSwitchSkill(Transform transform)
        {
            if (playerTransform != transform) { return; }
             comboResuableData.canQTE = true;
        }
        protected override void TriggerSwitchSkill()
        {
            //禁用人物所有输入
            CharacterInputSystem.Instance.inputActions.Player.Disable();
            //重置玩家连招
            ReSetComboInfo();
            //激活切人相机
            CameraSwitcher.Instance.ActiveSwitchCamera(true);   
            //注册输入切人事件
            comboResuableData.canQTE = false;
            //留0.5的时间然后放慢是因为镜头要切换到位
            TimerManager.Instance.GetOneTimer(0.3f, startSlowTime);

        }
        protected void startSlowTime()
        {
            //放慢时间，但是还不能输入
            SFX_PoolManager.Instance.TryGetSoundPool(SoundStyle.SwitchTime,player.characterName.ToString(),playerTransform.position); 
            CameraHitFeel.Instance.StartSlowTime(0.06f);
            TimerManager.Instance.GetRealTimer(0.2f, StartSwitchSkill);
        }
        protected void StartSwitchSkill()
        {
            //QTE倒计时
            TimerManager.Instance.GetRealTimer(3, CancelSwitchSkill);
            //激活UI
            UIManager.Instance.switchTimeUI.ActiveImage(SwitchCharacter.Instance.waitingCharacterList[0], SwitchCharacter.Instance.waitingCharacterList[1],3);
            CharacterInputSystem.Instance.inputActions.SwitchSkill.L.started += SwitchL;
            CharacterInputSystem.Instance.inputActions.SwitchSkill.R.started += SwitchR;
        }
        protected void CancelSwitchSkill()
        {
            //恢复时间
            CameraHitFeel.Instance.EndSlowTime();
            //关闭UI
            UIManager.Instance.switchTimeUI.UnActive();
            //恢复镜头
            CameraSwitcher.Instance.ActiveSwitchCamera(false);
            //注销输入
            CharacterInputSystem.Instance.inputActions.SwitchSkill.L.started -= SwitchL;
            CharacterInputSystem.Instance.inputActions.SwitchSkill.R.started -= SwitchR;
            //恢复输入
            CharacterInputSystem.Instance.inputActions.Player.Enable();
        }

        /// <summary>
        /// 切到右边角色
        /// </summary>
        /// <param name="context"></param>
        private void SwitchR(InputAction.CallbackContext context)
        {
            //选择切人的角色
            CharacterNameList selectCharacter = SwitchCharacter.Instance.waitingCharacterList[1];
            //通知对象你要触发技能：通过黑板模式通知
            GameBlackboard.Instance.GetGameData<Player>(selectCharacter.ToString()).comboStateMachine.ATKIngState.SwitchSkill();
            CharacterInputSystem.Instance.inputActions.SwitchSkill.L.started -= SwitchL;
            CharacterInputSystem.Instance.inputActions.SwitchSkill.R.started -= SwitchR;
        }

        private void SwitchL(InputAction.CallbackContext context)
        {
            CharacterNameList selectCharacter = SwitchCharacter.Instance.waitingCharacterList[0];
            //通知对象你要触发技能：通过黑板模式通知
            GameBlackboard.Instance.GetGameData<Player>(selectCharacter.ToString()).comboStateMachine.ATKIngState.SwitchSkill();
            CharacterInputSystem.Instance.inputActions.SwitchSkill.L.started -= SwitchL;
            CharacterInputSystem.Instance.inputActions.SwitchSkill.R.started -= SwitchR;
        }

        /// <summary>
        /// 支援技
        /// </summary>
        /// <param name="characterName"></param>
        public void SwitchSkill(CharacterNameList characterName)
        {
            //恢复时间
            CameraHitFeel.Instance.EndSlowTime();
            //关闭UI
            UIManager.Instance.switchTimeUI.UnActive();
            //结束切人相机
            CameraSwitcher.Instance.ActiveSwitchCamera(false);
            //改技能
            comboResuableData.currentSkill = comboData.switchSkill;
            //播放切人动画,//通知切换角色管理器处理切换技能
            SwitchCharacter.Instance.SwitchSkillInput(characterName, comboResuableData.currentSkill.comboName);
            //播放语音
            PlayCharacterVoice(comboResuableData.currentSkill);
            //播放武器音效
            PlayWeaponSound(comboResuableData.currentSkill);
            //恢复输入
            CharacterInputSystem.Instance.inputActions.Player.Enable();
           


        }

        #endregion

        #region 敌人检测
        public void UpdateDetectionDir()
        {
            Vector3 camForwardDir = Vector3.zero;
            camForwardDir.Set(comboResuableData.cameraTransform.forward.x, 0, comboResuableData.cameraTransform.forward.z);
            camForwardDir.Normalize();

            Vector3 camRightDir = Vector3.zero;
            camRightDir.Set(comboResuableData.cameraTransform.right.x, 0, comboResuableData.cameraTransform.right.z);
            camRightDir.Normalize();

            comboResuableData.detectionDir = camForwardDir * CharacterInputSystem.Instance.PlayerMove.y + camRightDir * CharacterInputSystem.Instance.PlayerMove.x;

            if (comboResuableData.detectionDir.sqrMagnitude <= 0.0001f)
            {
                Transform currentEnemy = GameBlackboard.Instance.GetEnemy();
                if (currentEnemy != null)
                {
                    Vector3 targetDir = currentEnemy.position - playerTransform.position;
                    targetDir.y = 0;
                    comboResuableData.detectionDir = targetDir;
                }
                else
                {
                    comboResuableData.detectionDir = playerTransform.forward;
                }
            }

            comboResuableData.detectionDir.Normalize();
        }
        public void UpdateEnemy()
        {
            UpdateDetectionDir();

            comboResuableData.detectionOrigin = new Vector3(playerTransform.position.x, playerTransform.position.y + 0.7f, playerTransform.position.z);
           
            if (Physics.SphereCast(comboResuableData.detectionOrigin,enemyDetectionData.detectionRadius, comboResuableData.detectionDir, out var hit, enemyDetectionData.detectionLength, enemyDetectionData.WhatIsEnemy))
            {
                if (GameBlackboard.Instance.GetEnemy() != hit.collider.transform || GameBlackboard.Instance.GetEnemy() == null)
                {   
                    GameBlackboard.Instance.SetEnemy(hit.collider.transform);
                }
            }
        }
        public void OnDrawGizmos()
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(comboResuableData. detectionOrigin + comboResuableData.detectionDir * enemyDetectionData.detectionLength, enemyDetectionData.detectionRadius);
        }

        #endregion

    }
}
