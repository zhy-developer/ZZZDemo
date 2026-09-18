
using Tools;
using UnityEngine;

namespace ZZZ
{
    public class CharacterComboBase
    {
        protected Animator animator{ get; }
        protected Transform playerTransform { get; }
        protected PlayerComboReusableData comboResuableData { get; }
        protected PlayerComboSOData comboData{ get; }
        protected PlayerEnemyDetectionData enemyDetectionData { get; }

        public Player player { get; }


        public CharacterComboBase(Animator animator, Transform playerTransform,Transform cameraTransform , PlayerComboReusableData reusableData, PlayerComboSOData playerComboSOData, PlayerEnemyDetectionData playerEnemyDetectionData,Player player)
            {
            //get访问器可以在构造函数里面赋值，而不能在其他地方赋值
                this.animator = animator;
                this.playerTransform = playerTransform;
                this.comboResuableData = reusableData;
                comboData = playerComboSOData;
                enemyDetectionData = playerEnemyDetectionData;
                reusableData.cameraTransform = cameraTransform;
                this.player = player;

                if (comboData.heavyCombo != null)
                {
                    comboData.heavyCombo.Init();
                }
                if (comboData.lightCombo != null)
                {
                    comboData.lightCombo.Init();
                }
            }

        public void AddEventAction()
        {
            comboResuableData.currentIndex.OnValueChanged += ReSetATKIndex;
            GameEventsManager.Instance.AddEventListening<Transform>("达到连携技条件", CanSwitchSkill);    
        }

        
        public void RemoveEventActon()
        {
            comboResuableData.currentIndex.OnValueChanged -= ReSetATKIndex;
            GameEventsManager.Instance.ReMoveEvent<Transform>("达到连携技条件", CanSwitchSkill);
        }

       

        public virtual bool CanBaseComboInput()
        {
            if (!comboResuableData.canInput) { return false; }
            if (animator.AnimationAtTag("Hit")) return false;
            if (animator.AnimationAtTag("Parry")) return false;
            if (animator.AnimationAtTag("Execute")) return false;
            if (animator.AnimationAtTag("Skill")) { return false; }
      
            return true;
        }
        protected virtual void UpdateComboInfo()
        {
            comboResuableData.comboIndex++;
            if (comboResuableData.comboIndex > comboResuableData.currentCombo.GetComboMaxCount() - 1)
            {
                comboResuableData.comboIndex = 0;
            }
        }
        #region 一般攻击
        public virtual void LightComboInput()
        {
         
            if (comboData.lightCombo == null) { return; }
            
            if (comboResuableData.currentCombo != comboData.lightCombo || comboResuableData.currentCombo ==null)
            {
                comboResuableData.currentCombo = comboData.lightCombo;
                ReSetComboInfo();
            }
            //确保轻攻击是第一个轻攻击
            comboResuableData.currentCombo.ResetComboDatas();

            ExecuteBaseCombo();

        }
        public virtual void HeavyComboInput()
        {
            if (comboData.heavyCombo == null) { return; }
            if (comboResuableData.currentCombo != comboData.heavyCombo || comboResuableData.currentCombo == null)
            {
                comboResuableData.currentCombo = comboData.heavyCombo;

                ReSetComboInfo();
            }  

            ExecuteBaseCombo();

        }
        public virtual void NormalDodgeCombo()
        {
            if (comboData.lightCombo == null) { return; }
            if (comboResuableData.currentCombo != comboData.lightCombo || comboResuableData.currentCombo == null)
            {
                comboResuableData.currentCombo = comboData.lightCombo;

            }
            comboResuableData.currentCombo.SwitchDodgeATK();
            ReSetComboInfo();
            ReSetATKIndex(0);
            ExecuteBaseCombo();
        }

        protected virtual void ExecuteBaseCombo()
        {
            if (comboResuableData.currentCombo == null) { return; }
            comboResuableData.hasATKCommand = true;
            comboResuableData.canInput = false;

        }
        public virtual void UpdateComboAnimation()
        {
            if (!comboResuableData.canATK) { return; }
            if (!comboResuableData.hasATKCommand) { return; }

            comboResuableData.currentIndex.Value = comboResuableData.comboIndex;
            string comboName = comboResuableData.currentCombo.GetComboName(comboResuableData.currentIndex.Value);
            animator.CrossFadeInFixedTime(comboName, 0.111f, 0, 0f);
            //播放语音
            PlayCharacterVoice(comboResuableData.currentCombo.comboDatas[comboResuableData.currentIndex.Value]);
            StartPlayWeapon();

            UpdateComboInfo();
          

            comboResuableData.hasATKCommand = false;
            comboResuableData.canATK = false;
        }


        public virtual void ReSetComboInfo()
        {
            comboResuableData.comboIndex = 0;
            comboResuableData.canInput = true;
            comboResuableData.canLink = true;
            comboResuableData.canMoveInterrupt = false;
            comboResuableData.canATK = true;
        }
        #endregion

        #region 被动技能
        protected virtual void CanSwitchSkill(Transform transform)
        {
            
        }
        protected virtual void TriggerSwitchSkill()
        {

        }

        #endregion




        #region 动画事件
        public void DisConnectCombo()//事件调用
        {
            comboResuableData.canLink = false;
        }
        public void CanMoveInterrupt()
        {
            comboResuableData.canMoveInterrupt = true;
        }

        public void CanInput()
        {
            comboResuableData.canInput = true;
        }
        public void CanATK()
        {
            comboResuableData.canATK = true;
        }
        public void PlayComboFX()
        {
            SFX_PoolManager.Instance.TryGetSoundPool(comboResuableData.currentCombo.GetComboSoundStyle(comboResuableData.currentIndex.Value), playerTransform.position, Quaternion.identity);
        }

        #endregion


        //注册转递伤害的动画事件
        public void ATK()
        {
            AttackTrigger();

        }
        #region 伤害检测
        protected bool AttackDetection(ComboContainerData comboContainerData)
        {
            //敌人
            //距离
            //角度
            if (GameBlackboard.Instance.GetEnemy() == null) { return false; }
           // Debug.Log("敌人条件满足");
            if (DevelopmentTools.DistanceForTarget(GameBlackboard.Instance.GetEnemy(), playerTransform) > comboContainerData.GetComboDistance(comboResuableData.currentIndex.Value)) { return false; }
           // Debug.Log("距离满足");
            if (DevelopmentTools.GetAngleForTargetDirection(GameBlackboard.Instance.GetEnemy(), playerTransform) < 80) { return false; }
           // Debug.Log("角度条件满足");
            return true;
        }

        protected bool SkillDetection(ComboData comboData)
        {
            if (GameBlackboard.Instance.GetEnemy() == null) { return false; }
            if (DevelopmentTools.DistanceForTarget(GameBlackboard.Instance.GetEnemy(), playerTransform) > comboData.attackDistance) { return false; }
            if (DevelopmentTools.GetAngleForTargetDirection(GameBlackboard.Instance.GetEnemy(), playerTransform) < 135) { return false; }
            return true;
        }
        #endregion

        protected int UpdateExecuteIndex(ComboContainerData containerData)
        {
            return Random.Range(0, containerData.GetComboMaxCount());
        }
        #region 更新伤害点
        /// <summary>
        /// 重置伤害点的计数
        /// </summary>
        /// <param name="这个参数没意义"></param>
        public void ReSetATKIndex(int index)//大招每一次需要执行手动清零，而普攻是攻击索引值发生变化而清零，还有闪A也要手动清零，因为闪A的连招索引值始终为0
        {
            comboResuableData.ATKIndex = 0;
        }
        public void UpdateATKIndex()
        {
            comboResuableData.ATKIndex++;
        }
        #endregion

        #region 传递伤害
        private void AttackTrigger()
        {
            if (animator.AnimationAtTag("ATK") )//给普通攻击传递伤害和可能多个攻击点的受击动画
            {
                UpdateATKIndex();
                CameraHitFeel.Instance.CameraShake(comboResuableData.currentCombo.GetComboShakeForce(comboResuableData.currentIndex.Value,comboResuableData.ATKIndex));
                Debug.Log(comboResuableData.currentCombo);
                if (!AttackDetection(comboResuableData.currentCombo)) { return; }
                float pauseFrameTime = comboResuableData.currentCombo.GetPauseFrameTime(comboResuableData.currentIndex.Value, comboResuableData.ATKIndex);

                  GameEventsManager.Instance.CallEvent("触发伤害",
                  comboResuableData.currentCombo.GetComboDamage(comboResuableData.currentIndex.Value),
                  comboResuableData.currentCombo.GetComboHitName(comboResuableData.currentIndex.Value),
                  comboResuableData.currentCombo.GetComboParryName(comboResuableData.currentIndex.Value),
                  playerTransform, GameBlackboard.Instance.GetEnemy(),
                  this);

                 CameraHitFeel.Instance.PF(pauseFrameTime);
                #region 触发QTE
                if (comboResuableData.canQTE && comboResuableData.ATKIndex >= comboResuableData.currentCombo.GetComboATKCount(comboResuableData.currentIndex.Value))
                {
                    TriggerSwitchSkill();
                }
                #endregion

            }
            else if (animator.AnimationAtTag("Skill"))
            {
                UpdateATKIndex();

                if (!SkillDetection(comboResuableData.currentSkill)) { return; }
               
                GameEventsManager.Instance.CallEvent("触发伤害", comboResuableData.currentSkill.comboDamage, comboResuableData.currentSkill.hitName, comboResuableData.currentSkill.parryName, playerTransform, GameBlackboard.Instance.GetEnemy(),this);

                #region 顿帧
                if (comboResuableData.currentSkill.pauseFrameTimeList!=null && comboResuableData.currentSkill.pauseFrameTimeList.Length > 0&& comboResuableData.ATKIndex <= comboResuableData.currentSkill.pauseFrameTimeList.Length)
                {
                   float skillPauseFrameTime = comboResuableData.currentSkill.pauseFrameTimeList[comboResuableData.ATKIndex - 1];
                   CameraHitFeel.Instance.PF(skillPauseFrameTime);
                }
                else
                {
                    CameraHitFeel.Instance.PF(comboResuableData.currentSkill.pauseFrameTime);
                }

                #endregion

                #region 触发QTE

                if (comboResuableData.canQTE && comboResuableData.ATKIndex >= comboResuableData.currentSkill.ATKCount)
                {
                    TriggerSwitchSkill();
                }
                #endregion

                #region 震屏
                if (comboResuableData.currentSkill.shakeForce==null||comboResuableData.ATKIndex > comboResuableData.currentSkill.shakeForce.Length)//避免没有设置完整ATK的force而导致给出的ATKindex超出技能force索引值
                {
                    return;
                }
                CameraHitFeel.Instance.CameraShake(comboResuableData.currentSkill.shakeForce[comboResuableData.ATKIndex-1]);
                #endregion
            }
            else//处理只有一次受击动画，但是可能有多次伤害
            {
                if (!AttackDetection(comboData.executeCombo)) { return; }
                GameEventsManager.Instance.CallEvent("生成伤害", comboData.executeCombo.GetComboDamage(comboResuableData.executeIndex));
            }

        }
        #endregion

        public void UpdateAttackLookAtEnemy()
        {
            if (GameBlackboard.Instance.GetEnemy() == null) { return; }
            if ((animator.AnimationAtTag("ATK") && animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.3f)|| animator.AnimationAtTag("Skill"))
            {
                if (DevelopmentTools.DistanceForTarget(playerTransform, GameBlackboard.Instance.GetEnemy()) > 6.5f) return;
                if (DevelopmentTools.DistanceForTarget(playerTransform, GameBlackboard.Instance.GetEnemy()) < 0.09f) return;
                playerTransform.Look(GameBlackboard.Instance.GetEnemy().position, 60);
            }

        }

        public void CheckMoveInterrupt()
        {
            if (comboResuableData.canMoveInterrupt == false) { return; }
            if (CharacterInputSystem.Instance.PlayerMove.sqrMagnitude != 0)
            {
                animator.CrossFadeInFixedTime("Locomotion", 0.155f, 0);
                comboResuableData.canMoveInterrupt = false;
            }
        }
        public void CheckCanLinkCombo()
        {
            if (!comboResuableData.canLink || CharacterInputSystem.Instance.Run)
            {
                ReSetComboInfo();
            }
        }

        #region 播放音效
        private void StartPlayWeapon()
        {
            PlayWeaponSound(comboResuableData.currentCombo.comboDatas[comboResuableData.currentIndex.Value]);
        }
        protected void PlayCharacterVoice(ComboData comboData)
        {
            SFX_PoolManager.Instance.TryGetSoundPool(SoundStyle.ComboVoice,comboData.comboName, playerTransform.position);
        }
        protected void PlayWeaponSound(ComboData comboData)
        {
            SFX_PoolManager.Instance.TryGetSoundPool(SoundStyle.WeaponSound, comboData.comboName, playerTransform.position);
        }

        #endregion
    }
}
