
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

        public bool CanFinishSkillInput()
        {
            // Preserve the ultimate outro restriction from the existing controller.
            if (animator.GetCurrentAnimatorStateInfo(0).IsName("Unagi_FishSkill_End")) return false;
            if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("Unagi_FishSkill_End")) return false;
            return CanPlaySkill(comboData.finishSkillCombo);
        }

        public bool CanSkillInput() { return CanPlaySkill(comboData.skillCombo); }

        private bool CanPlaySkill(ComboData skill)
        {
            return skill != null && !player.IsPlayingAnimationTag("Skill")
                && !player.IsPlayingAnimationTag("Hit") && !player.IsPlayingAnimationTag("Parry")
                && !player.IsPlayingAnimationTag("ATK");
        }

        public void FinishSkillInput() { PlaySkill(comboData.finishSkillCombo); }
        public void SkillInput() { PlaySkill(comboData.skillCombo); }

        private void PlaySkill(ComboData skill)
        {
            if (skill == null) return;
            comboResuableData.currentSkill = skill;
            ReSetATKIndex(0);
            PlayCharacterVoice(skill);
            PlayWeaponSound(skill);
            animator.CrossFadeInFixedTime(skill.comboName, 0.1f);
        }
        #region 敌人检测
        public void UpdateDetectionDir(Vector3 worldMoveDirection)
        {
            comboResuableData.detectionDir = worldMoveDirection;

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
        public void UpdateEnemy(Vector3 worldMoveDirection)
        {
            if (!player.IsLocalPlayer) return;
            UpdateDetectionDir(worldMoveDirection);

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
