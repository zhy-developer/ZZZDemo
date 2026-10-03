
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
            switch (player.characterName)
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
            // 大招收尾期间禁止再次释放大招，保留移动和攻击。
            if (animator.GetCurrentAnimatorStateInfo(0)
                .IsName("Unagi_FishSkill_End"))
            {
                return false;
            }

            if (animator.IsInTransition(0) &&
                animator.GetNextAnimatorStateInfo(0)
                    .IsName("Unagi_FishSkill_End"))
            {
                return false;
            }

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
