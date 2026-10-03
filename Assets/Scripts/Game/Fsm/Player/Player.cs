using UnityEngine;
using UnityEngine.Playables;
using static OnAnimationTranslation;

namespace ZZZ  
{
    [RequireComponent(typeof(Animator), typeof(CharacterController))]
    public class Player : CharacterMoveControllerBase
    {
        //当前角色名称
        [SerializeField] public CharacterNameList characterName;

        [SerializeField] public Transform enemy;

        //当前角色数据
        [SerializeField] public PlayerSO playerSO;
        //玩家摄像机数据
        [SerializeField] public PlayerCameraUtility playerCameraUtility;


        //现在的移动状态
        [SerializeField] public string currentMovementState;
        //现在的连招状态
        [SerializeField] public string currentComboState;

        [SerializeField, Header("大招演出")]
        private PlayableDirector finishSkillTimeline;

        //玩家移动状态机
        public PlayerMovementStateMachine movementStateMachine { get;private set; }
        //玩家连招状态机
        public PlayerComboStateMachine comboStateMachine { get;private set; }

        public new Transform camera { get; private set; }

        //游戏黑板数据
        private GameBlackboard gameBlackboard;

        public int BattleID { get; private set; }
        public bool IsLocalPlayer { get; private set; }
        [SerializeField] private Transform cameraFollowTarget;
        [SerializeField] private Transform cameraLookAtTarget;
        public Transform CameraFollowTarget => cameraFollowTarget != null ? cameraFollowTarget : transform;
        public Transform CameraLookAtTarget => cameraLookAtTarget != null ? cameraLookAtTarget : transform;
        private bool statesStarted;

        // RoleManager 在创建后、Start 之前明确指定归属；未初始化角色不接收本地输入。
        public void InitializeNetworkRole(int battleID, bool isLocalPlayer)
        {
            BattleID = battleID;
            IsLocalPlayer = isLocalPlayer;
        }

        protected override void Awake()
        {
            base.Awake();

            camera = Camera.main != null ? Camera.main.transform : transform;
            movementStateMachine = new PlayerMovementStateMachine(this);
            comboStateMachine = new PlayerComboStateMachine(this);
        }

       

        protected override void Start()
        {
            base.Start();
            if (IsLocalPlayer) StartLocalStates();
          
        }
       protected override void Update()
        {
            if (IsLocalPlayer)
            {
                base.Update();
                movementStateMachine.HandInput();

                movementStateMachine.Update();

                comboStateMachine.Update();
            }
        }
       

        #region 相关动画进入或退出触发的方法
        public void OnAnimationTranslateEvent(OnEnterAnimationPlayerState playerState)
        {
            if (!IsLocalPlayer || !statesStarted) return;
            switch (playerState)
            {
                case OnEnterAnimationPlayerState.TurnBack:
                    movementStateMachine.OnAnimationTranslateEvent(movementStateMachine.returnRunState);
                    break;
                case OnEnterAnimationPlayerState.Switch:
                    movementStateMachine.OnAnimationTranslateEvent(movementStateMachine.onSwitchState);
                    break;
                case OnEnterAnimationPlayerState.SwitchOut:
                    movementStateMachine.OnAnimationTranslateEvent(movementStateMachine.onSwitchOutState);
                    comboStateMachine.OnAnimationTranslateEvent(comboStateMachine.NullState);
                    break;
                case OnEnterAnimationPlayerState.ATK:
                    comboStateMachine.OnAnimationTranslateEvent(comboStateMachine.ATKIngState);
                    movementStateMachine.OnAnimationTranslateEvent(movementStateMachine.playerMovementNullState);
                    break;
                case OnEnterAnimationPlayerState.Dash:
                    movementStateMachine.OnAnimationTranslateEvent(movementStateMachine.dashingState);
                    comboStateMachine.OnAnimationTranslateEvent(comboStateMachine.NullState);
                    break;
                case OnEnterAnimationPlayerState.DashBack:
                    movementStateMachine.OnAnimationTranslateEvent(movementStateMachine.dashBackingState);
                    comboStateMachine.OnAnimationTranslateEvent(comboStateMachine.NullState);
                    break;
            }
          
        }

        public void OnAnimationExitEvent()
        {
            if (!IsLocalPlayer || !statesStarted) return;
            movementStateMachine.OnAnimationExitEvent();

            comboStateMachine.OnAnimationExitEvent();
        }
        #endregion

        #region 状态变更事件
        public void OnEnable()
        {
            if (IsLocalPlayer && movementStateMachine != null && !statesStarted)
                StartLocalStates();
            //注册movement状态机中的状态变更事件
            if (movementStateMachine != null)
            {
                movementStateMachine.currentState.OnValueChanged += MovementStateChanged;
            }

            if (comboStateMachine != null)
            {
                comboStateMachine.currentState.OnValueChanged += ComboStateChanged;
            }

        }
       

        public void OnDisable()
        {
            if (statesStarted)
            {
                movementStateMachine.currentState.Value?.Exit();
                comboStateMachine.currentState.Value?.Exit();
                movementStateMachine.currentState.Value = null;
                comboStateMachine.currentState.Value = null;
                statesStarted = false;
            }
            if (movementStateMachine != null)
            {
                movementStateMachine.currentState.OnValueChanged -= MovementStateChanged;
            }

            if (comboStateMachine != null)
            {
                comboStateMachine.currentState.OnValueChanged -= ComboStateChanged;
            }

            if (gameBlackboard != null)
            {
                gameBlackboard.enemy.OnValueChanged -= EnemyChanged;
            }
        }

        private void StartLocalStates()
        {
            if (statesStarted) return;
            statesStarted = true;
            playerCameraUtility?.Init();
            movementStateMachine.ChangeState(movementStateMachine.idlingState);
            comboStateMachine.ChangeState(comboStateMachine.NullState);
            gameBlackboard = GameBlackboard.Instance;
            gameBlackboard.enemy.OnValueChanged += EnemyChanged;
            enemy = gameBlackboard.GetEnemy();
        }
    

        public void MovementStateChanged(IState currentState)
        {
            currentMovementState = currentState != null ? currentState.GetType().Name : string.Empty;
        }
        private void ComboStateChanged(IState state)
        {
            string previousComboState = currentComboState;
            currentComboState = state != null ? state.GetType().Name : string.Empty;
            //Debug.Log($"[ComboState] {characterName} ({gameObject.name}) | Frame {Time.frameCount} | {previousComboState} -> {currentComboState}", this);
        }
        private void EnemyChanged(Transform transform)
        {
            enemy = transform;
        }
        #endregion

        #region 连招动画帧事件
        /// <summary>
        /// 启动预输入
        /// </summary>
        public void EnablePreInput()
        {
            if (!IsLocalPlayer) return;
            comboStateMachine.ATKIngState.EnablePreInput();
        }
        /// <summary>
        /// 取消攻击冷却
        /// </summary>
        public void CancelAttackColdTime()
        { 
            if (!IsLocalPlayer) return;
            comboStateMachine.ATKIngState.CancelAttackColdTime();
        }

        /// <summary>
        /// 取消连招
        /// </summary>
        public void DisableLinkCombo()
        { 
            if (!IsLocalPlayer) return;
            comboStateMachine.ATKIngState.DisableLinkCombo();
        }
        /// <summary>
        /// 打断移动
        /// </summary>
        public void EnableMoveInterrupt()
        {
            if (!IsLocalPlayer) return;
            comboStateMachine.ATKIngState.EnableMoveInterrupt();
        }
    
        /// <summary>
        /// 攻击事件
        /// </summary>
        public void ATK(AnimationEvent animationEvent = null)
        {
            if (!IsLocalPlayer) return;
            comboStateMachine.ATKIngState.ATK(animationEvent);
        }

        #endregion

        #region 动画音特效帧事件

        public void PlayVFX(string name)
        {
            VFX_PoolManager.Instance.TryGetVFX(characterName, name);
        }

        public void PlayFootSound()
        {
            //Debug.Log("播放脚步声");
            SFX_PoolManager.Instance.TryGetSoundPool(SoundStyle.FOOT,transform.position,Quaternion.identity);
        }
        public void PlayFootBackSound()
        {
            SFX_PoolManager.Instance.TryGetSoundPool(SoundStyle.FOOTBACK,transform.position,Quaternion.identity);
        }

        public void PlayWeaponBackSound()
        {
            SFX_PoolManager.Instance.TryGetSoundPool(SoundStyle.WeaponBack, characterName.ToString(), transform.position);
        }

        public void PlayWeaponEndSound()
        {
            SFX_PoolManager.Instance.TryGetSoundPool(SoundStyle.WeaponEnd, characterName.ToString(), transform.position);
        }

        #endregion
        public void PlayDodgeSound()
        {
            SFX_PoolManager.Instance.TryGetSoundPool(SoundStyle.DodgeSound, transform.position, Quaternion.identity);
        }
        public void PlaySwitchWindSound()
        {
            SFX_PoolManager.Instance.TryGetSoundPool(SoundStyle.SwitchInWindSound, transform.position, Quaternion.identity);
        }
        public void PlaySwitchInVoice()
        {
            SFX_PoolManager.Instance.TryGetSoundPool(SoundStyle.SwitchInVoice, characterName.ToString(), transform.position);
        }

        public void PlayFinishSkillTimeline() {
            if (finishSkillTimeline == null) return;
            finishSkillTimeline.time = 0;
            finishSkillTimeline.Play();
        }
    }
}
