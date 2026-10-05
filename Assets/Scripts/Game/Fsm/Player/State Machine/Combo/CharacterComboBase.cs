
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
        private readonly ComboContainerData lightCombo;
        private readonly ComboContainerData heavyCombo;


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

                lightCombo = CloneCombo(comboData.lightCombo);
                heavyCombo = CloneCombo(comboData.heavyCombo);
            }

        private static ComboContainerData CloneCombo(ComboContainerData source)
        {
            if (source == null) return null;
            var copy = Object.Instantiate(source);
            copy.comboDatas = new System.Collections.Generic.List<ComboData>(source.comboDatas);
            copy.Init();
            return copy;
        }

        public void Dispose()
        {
            if (lightCombo != null) Object.Destroy(lightCombo);
            if (heavyCombo != null) Object.Destroy(heavyCombo);
        }

        public bool TryGetAttackIndex(bool heavy, bool dodge, out int index)
        {
            var container = heavy ? heavyCombo : lightCombo;
            index = 0;
            if (container == null || container.GetComboMaxCount() == 0) return false;
            if (!dodge && comboResuableData.currentCombo == container && comboResuableData.canLink)
                index = comboResuableData.comboIndex % container.GetComboMaxCount();
            return true;
        }

        // The sender chose the step; never reject/reselect it using this client's animation timing.
        public bool PlayNetworkAttack(bool heavy, bool dodge, int index)
        {
            var container = heavy ? heavyCombo : lightCombo;
            if (container == null || index < 0 || index >= container.GetComboMaxCount()) return false;
            if (dodge) container.SwitchDodgeATK();
            else container.ResetComboDatas();
            ReSetComboInfo();
            comboResuableData.currentCombo = container;
            comboResuableData.comboIndex = index;
            ExecuteBaseCombo();
            UpdateComboAnimation();
            return true;
        }
        public void AddEventAction()
        {
            comboResuableData.currentIndex.OnValueChanged += ReSetATKIndex;
        }

        
        public void RemoveEventActon()
        {
            comboResuableData.currentIndex.OnValueChanged -= ReSetATKIndex;
        }

       

        public virtual bool CanBaseComboInput()
        {
            if (!comboResuableData.canInput) { return false; }
            if (player.IsPlayingAnimationTag("Hit")) return false;
            if (player.IsPlayingAnimationTag("Parry")) return false;
            if (player.IsPlayingAnimationTag("Execute")) return false;
            if (player.IsPlayingAnimationTag("Skill")) { return false; }
      
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
            ReSetATKIndex(0);
            player.comboStateMachine.ChangeState(player.comboStateMachine.ATKIngState);
            player.movementStateMachine.ChangeState(player.movementStateMachine.playerMovementNullState);
            string comboName = comboResuableData.currentCombo.GetComboName(comboResuableData.currentIndex.Value);
            // 每一段新攻击都重新等待动画事件开放移动打断。
            comboResuableData.canMoveInterrupt = false;
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
            comboResuableData.hasATKCommand = false;
            comboResuableData.canInput = true;
            comboResuableData.canLink = true;
            comboResuableData.canMoveInterrupt = false;
            comboResuableData.canATK = true;
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
        public void ATK(AnimationEvent animationEvent = null)
        {
            // 淡入期间 CurrentState 可能仍是旧动画，伤害类型应由事件来源决定。
            AnimatorStateInfo attackState = animationEvent != null && animationEvent.isFiredByAnimator
                ? animationEvent.animatorStateInfo
                : animator.GetCurrentAnimatorStateInfo(0);
            AttackTrigger(attackState);

        }
        #region 伤害检测
        protected bool AttackDetection(ComboContainerData comboContainerData)
        {
            //敌人
            //距离
            //角度
            if (player.enemy == null) { return false; }
           // Debug.Log("敌人条件满足");
            if (DevelopmentTools.DistanceForTarget(player.enemy, playerTransform) > comboContainerData.GetComboDistance(comboResuableData.currentIndex.Value)) { return false; }
           // Debug.Log("距离满足");
            if (DevelopmentTools.GetAngleForTargetDirection(player.enemy, playerTransform) < 80) { return false; }
           // Debug.Log("角度条件满足");
            return true;
        }

        protected bool SkillDetection(ComboData comboData)
        {
            if (player.enemy == null) { return false; }
            if (DevelopmentTools.DistanceForTarget(player.enemy, playerTransform) > comboData.attackDistance) { return false; }
            if (DevelopmentTools.GetAngleForTargetDirection(player.enemy, playerTransform) < 135) { return false; }
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
        private void AttackTrigger(AnimatorStateInfo attackState)
        {
            if (attackState.IsTag("ATK"))//给普通攻击传递伤害和可能多个攻击点的受击动画
            {
                if (comboResuableData.currentCombo == null) { return; }
                UpdateATKIndex();
                CameraHitFeel.Instance.CameraShake(comboResuableData.currentCombo.GetComboShakeForce(comboResuableData.currentIndex.Value,comboResuableData.ATKIndex));
                Debug.Log(comboResuableData.currentCombo);
                if (!AttackDetection(comboResuableData.currentCombo)) { return; }
                float pauseFrameTime = comboResuableData.currentCombo.GetPauseFrameTime(comboResuableData.currentIndex.Value, comboResuableData.ATKIndex);

                  GameEventsManager.Instance.CallEvent("触发伤害",
                  comboResuableData.currentCombo.GetComboDamage(comboResuableData.currentIndex.Value),
                  comboResuableData.currentCombo.GetComboHitName(comboResuableData.currentIndex.Value),
                  comboResuableData.currentCombo.GetComboParryName(comboResuableData.currentIndex.Value),
                  playerTransform, player.enemy,
                  this);

                 CameraHitFeel.Instance.PF(pauseFrameTime);

            }
            else if (attackState.IsTag("Skill"))
            {
                if (comboResuableData.currentSkill == null) { return; }
                UpdateATKIndex();

                if (!SkillDetection(comboResuableData.currentSkill)) { return; }
               
                GameEventsManager.Instance.CallEvent("触发伤害", comboResuableData.currentSkill.comboDamage, comboResuableData.currentSkill.hitName, comboResuableData.currentSkill.parryName, playerTransform, player.enemy,this);

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

                #region 震屏
                if (comboResuableData.currentSkill.shakeForce==null||comboResuableData.ATKIndex > comboResuableData.currentSkill.shakeForce.Length)//避免没有设置完整ATK的force而导致给出的ATKindex超出技能force索引值
                {
                    return;
                }
                CameraHitFeel.Instance.CameraShake(comboResuableData.currentSkill.shakeForce[comboResuableData.ATKIndex-1]);
                #endregion
            }
            else if (attackState.IsTag("Execute"))//只有明确的处决动画才使用处决数据
            {
                if (comboData.executeCombo == null) { return; }
                if (!AttackDetection(comboData.executeCombo)) { return; }
                GameEventsManager.Instance.CallEvent("生成伤害", comboData.executeCombo.GetComboDamage(comboResuableData.executeIndex));
            }

        }
        #endregion

        public void CheckMoveInterrupt()
        {
            if (comboResuableData.canMoveInterrupt == false) { return; }
            if (!player.IsPlayingAnimationTag("ATK") || animator.IsInTransition(0)) { return; }
            if (player.HasNetworkMovement)
            {
                animator.CrossFadeInFixedTime("Locomotion", 0.155f, 0);
                comboResuableData.canMoveInterrupt = false;
                player.comboStateMachine.ChangeState(player.comboStateMachine.NullState);
                player.movementStateMachine.ReturnToLocomotion();
            }
        }
        public void CheckCanLinkCombo()
        {
            if (!comboResuableData.canLink)
            {
                // 连招超时只重置连段，当前攻击的后摇打断窗口仍然有效。
                bool canMoveInterrupt = comboResuableData.canMoveInterrupt;
                bool pendingAttack = comboResuableData.hasATKCommand;
                ReSetComboInfo();
                comboResuableData.canMoveInterrupt = canMoveInterrupt;
                comboResuableData.hasATKCommand = pendingAttack;
                comboResuableData.canInput = !pendingAttack;
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
