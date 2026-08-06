
using Tools;
using UnityEngine;

namespace ZZZ
{
    public class CharacterComboBase
    {
        protected Animator animator{ get; }
        protected Transform playerTransform { get; }
        protected PlayerComboReusableData reusableData { get; }
        protected PlayerComboSOData comboData{ get; }
        protected PlayerEnemyDetectionData enemyDetectionData { get; }

        public Player player { get; }


        public CharacterComboBase(Animator animator, Transform playerTransform,Transform cameraTransform , PlayerComboReusableData reusableData, PlayerComboSOData playerComboSOData, PlayerEnemyDetectionData playerEnemyDetectionData,Player player)
            {
            //get访问器可以在构造函数里面赋值，而不能在其他地方赋值
                this.animator = animator;
                this.playerTransform = playerTransform;
                this.reusableData = reusableData;
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
                    //初始化其实就是缓存数据
                     comboData.lightCombo.Init();
                }
            }

        public void AddEventAction()
        {
            reusableData.currentIndex.OnValueChanged += ReSetATKIndex;
            GameEventsManager.Instance.AddEventListening<Transform>("达到QTE条件", CanSwitchSkill);
           
        }

        

        public void RemoveEventActon()
        {
            reusableData.currentIndex.OnValueChanged -= ReSetATKIndex;
            GameEventsManager.Instance.ReMoveEvent<Transform>("达到QTE条件", CanSwitchSkill);
        }

       

        public virtual bool CanBaseComboInput()
        {
            Debug.Log($"[AttackDebug] CanBaseComboInput check. canInput={reusableData.canInput}, hit={animator.AnimationAtTag("Hit")}, parry={animator.AnimationAtTag("Parry")}, execute={animator.AnimationAtTag("Execute")}, skill={animator.AnimationAtTag("Skill")}");
            if (!reusableData.canInput) { return false; }
            if (animator.AnimationAtTag("Hit")) return false;
            if (animator.AnimationAtTag("Parry")) return false;
            if (animator.AnimationAtTag("Execute")) return false;
            if (animator.AnimationAtTag("Skill")) { return false; }
      
            return true;
        }
        protected virtual void UpdateComboInfo()
        {
            reusableData.comboIndex++;
            if (reusableData.comboIndex > reusableData.currentCombo.GetComboMaxCount() - 1)
            {
                reusableData.comboIndex = 0;
            }
        }
        #region 一般攻击
        public virtual void LightComboInput()
        {
            Debug.Log($"[AttackDebug] LightComboInput. player={player.characterName}, playerSO={player.playerSO?.name}, comboDataNull={comboData == null}, lightCombo={comboData?.lightCombo?.name}, currentCombo={reusableData.currentCombo?.name}");
         
            if (comboData.lightCombo == null) { return; }
            
            if (reusableData.currentCombo != comboData.lightCombo || reusableData.currentCombo ==null)
            {
                reusableData.currentCombo = comboData.lightCombo;
                ReSetComboInfo();
            }
            //确保轻攻击是第一个轻攻击
            reusableData.currentCombo.ResetComboDatas();

            ExecuteBaseCombo();

        }
        public virtual void HeavyComboInput()
        {
            if (comboData.heavyCombo == null) { return; }
            if (reusableData.currentCombo != comboData.heavyCombo || reusableData.currentCombo == null)
            {
                reusableData.currentCombo = comboData.heavyCombo;

                ReSetComboInfo();
            }  

            ExecuteBaseCombo();

        }
        public virtual void NormalDodgeCombo()
        {
            if (comboData.lightCombo == null) { return; }
            if (reusableData.currentCombo != comboData.lightCombo || reusableData.currentCombo == null)
            {
                reusableData.currentCombo = comboData.lightCombo;

            }
            reusableData.currentCombo.SwitchDodgeATK();
            ReSetComboInfo();
            ReSetATKIndex(0);
            ExecuteBaseCombo();
        }

        protected virtual void ExecuteBaseCombo()
        {
            if (reusableData.currentCombo == null) { return; }
            Debug.Log($"[AttackDebug] ExecuteBaseCombo queued. character={player.characterName}, comboContainer={reusableData.currentCombo.name}, comboIndex={reusableData.comboIndex}, canATK={reusableData.canATK}, canInput={reusableData.canInput}, hasATKCommand={reusableData.hasATKCommand}, animatorSpeed={animator.speed}, timeScale={Time.timeScale}, unscaledTime={Time.unscaledTime}");
            reusableData.hasATKCommand = true;
            reusableData.canInput = false;

        }
        public virtual void UpdateComboAnimation()
        {
            if (!reusableData.canATK) { return; }
            if (!reusableData.hasATKCommand) { return; }

            reusableData.currentIndex.Value = reusableData.comboIndex;
            string comboName = reusableData.currentCombo.GetComboName(reusableData.currentIndex.Value);
            Debug.Log($"[AttackDebug] CrossFade attack animation. character={player.characterName}, combo={comboName}, comboIndex={reusableData.currentIndex.Value}, fixedFade=0.111, animatorSpeed={animator.speed}, timeScale={Time.timeScale}, unscaledTime={Time.unscaledTime}");
            animator.CrossFadeInFixedTime(comboName, 0.111f, 0, 0f);
            //播放语音
            PlayCharacterVoice(reusableData.currentCombo.comboDatas[reusableData.currentIndex.Value]);
            StartPlayWeapon();

            UpdateComboInfo();
          

            reusableData.hasATKCommand = false;
            reusableData.canATK = false;
        }


        public virtual void ReSetComboInfo()
        {
            reusableData.comboIndex = 0;
            reusableData.canInput = true;
            reusableData.canLink = true;
            reusableData.canMoveInterrupt = false;
            reusableData.canATK = true;
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
            reusableData.canLink = false;
        }
        public void CanMoveInterrupt()
        {
            reusableData.canMoveInterrupt = true;
        }

        public void CanInput()
        {
            reusableData.canInput = true;
        }
        public void CanATK()
        {
            reusableData.canATK = true;
        }
        public void PlayComboFX()
        {
            SFX_PoolManager.Instance.TryGetSoundPool(reusableData.currentCombo.GetComboSoundStyle(reusableData.currentIndex.Value), playerTransform.position, Quaternion.identity);
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
            if (DevelopmentTools.DistanceForTarget(GameBlackboard.Instance.GetEnemy(), playerTransform) > comboContainerData.GetComboDistance(reusableData.currentIndex.Value)) { return false; }
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
            reusableData.ATKIndex = 0;
        }
        public void UpdateATKIndex()
        {
            reusableData.ATKIndex++;
        }
        #endregion

        #region 传递伤害
        private void AttackTrigger()
        {
            if (animator.AnimationAtTag("ATK") )//给普通攻击传递伤害和可能多个攻击点的受击动画
            {
                UpdateATKIndex();
               // Debug.Log("此时ATK值为："+ reusableData.ATKIndex) ;
                 CameraHitFeel.Instance.CameraShake(reusableData.currentCombo.GetComboShakeForce(reusableData.currentIndex.Value,reusableData.ATKIndex));
                Debug.Log(reusableData.currentCombo);
                if (!AttackDetection(reusableData.currentCombo)) { return; }
                float pauseFrameTime = reusableData.currentCombo.GetPauseFrameTime(reusableData.currentIndex.Value, reusableData.ATKIndex);
                Debug.Log($"[AttackDebug] Normal attack hit. character={player.characterName}, combo={reusableData.currentCombo.GetComboName(reusableData.currentIndex.Value)}, comboIndex={reusableData.currentIndex.Value}, atkIndex={reusableData.ATKIndex}, pauseFrameTime={pauseFrameTime}, timeScale={Time.timeScale}, animatorSpeed={animator.speed}, enemy={GameBlackboard.Instance.GetEnemy()?.name}");

                  GameEventsManager.Instance.CallEvent("触发伤害",
                  reusableData.currentCombo.GetComboDamage(reusableData.currentIndex.Value),
                  reusableData.currentCombo.GetComboHitName(reusableData.currentIndex.Value),
                  reusableData.currentCombo.GetComboParryName(reusableData.currentIndex.Value),
                  playerTransform, GameBlackboard.Instance.GetEnemy(),
                  this);

                 CameraHitFeel.Instance.PF(pauseFrameTime);
                //Debug.Log("QTE的条件：" + reusableData.canQTE);
                //Debug.Log("人物的ATK索引值："+reusableData.ATKIndex);
                //Debug.Log("设置的最大ATK数量为：" + reusableData.currentCombo.GetComboATKCount(reusableData.currentIndex.Value));
                #region 触发QTE
                if (reusableData.canQTE && reusableData.ATKIndex >= reusableData.currentCombo.GetComboATKCount(reusableData.currentIndex.Value))
                {
                    TriggerSwitchSkill();
                }
                #endregion

            }
            else if (animator.AnimationAtTag("Skill"))
            {
                UpdateATKIndex();

                if (!SkillDetection(reusableData.currentSkill)) { return; }
               
                GameEventsManager.Instance.CallEvent("触发伤害", reusableData.currentSkill.comboDamage, reusableData.currentSkill.hitName, reusableData.currentSkill.parryName, playerTransform, GameBlackboard.Instance.GetEnemy(),this);

                #region 顿帧
                if (reusableData.currentSkill.pauseFrameTimeList!=null && reusableData.currentSkill.pauseFrameTimeList.Length > 0&& reusableData.ATKIndex <= reusableData.currentSkill.pauseFrameTimeList.Length)
                {
                   float skillPauseFrameTime = reusableData.currentSkill.pauseFrameTimeList[reusableData.ATKIndex - 1];
                   Debug.Log($"[AttackDebug] Skill hit. character={player.characterName}, skill={reusableData.currentSkill.comboName}, atkIndex={reusableData.ATKIndex}, pauseFrameTime={skillPauseFrameTime}, timeScale={Time.timeScale}, animatorSpeed={animator.speed}, enemy={GameBlackboard.Instance.GetEnemy()?.name}");
                   CameraHitFeel.Instance.PF(skillPauseFrameTime);
                }
                else
                {
                    Debug.Log($"[AttackDebug] Skill hit. character={player.characterName}, skill={reusableData.currentSkill.comboName}, atkIndex={reusableData.ATKIndex}, pauseFrameTime={reusableData.currentSkill.pauseFrameTime}, timeScale={Time.timeScale}, animatorSpeed={animator.speed}, enemy={GameBlackboard.Instance.GetEnemy()?.name}");
                    CameraHitFeel.Instance.PF(reusableData.currentSkill.pauseFrameTime);
                }

                #endregion

                #region 触发QTE

                if (reusableData.canQTE && reusableData.ATKIndex >= reusableData.currentSkill.ATKCount)
                {
                    TriggerSwitchSkill();
                }
                #endregion

                #region 震屏
                if (reusableData.currentSkill.shakeForce==null||reusableData.ATKIndex > reusableData.currentSkill.shakeForce.Length)//避免没有设置完整ATK的force而导致给出的ATKindex超出技能force索引值
                {
                    return;
                }
                CameraHitFeel.Instance.CameraShake(reusableData.currentSkill.shakeForce[reusableData.ATKIndex-1]);
                #endregion
            }
            else//处理只有一次受击动画，但是可能有多次伤害
            {
                if (!AttackDetection(comboData.executeCombo)) { return; }
                GameEventsManager.Instance.CallEvent("生成伤害", comboData.executeCombo.GetComboDamage(reusableData.executeIndex));
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
            if (reusableData.canMoveInterrupt == false) { return; }
            if (CharacterInputSystem.Instance.PlayerMove.sqrMagnitude != 0)
            {
                animator.CrossFadeInFixedTime("Locomotion", 0.155f, 0);
                reusableData.canMoveInterrupt = false;
            }
        }
        public void CheckCanLinkCombo()
        {
            if (!reusableData.canLink || CharacterInputSystem.Instance.Run)
            {
                ReSetComboInfo();
            }
        }

        #region 播放音效
        private void StartPlayWeapon()
        {
            PlayWeaponSound(reusableData.currentCombo.comboDatas[reusableData.currentIndex.Value]);
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
