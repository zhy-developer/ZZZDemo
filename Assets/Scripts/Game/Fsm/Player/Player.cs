using GameProtocol;
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
        private bool networkRoleInitialized;
        public bool HasNetworkMovement { get; private set; }
        private PlayerNetworkInput networkInput;
        private PlayerActionController actions;

        // RoleManager 在创建后、Start 之前明确指定归属；未初始化角色不接收本地输入。
        public void InitializeNetworkRole(int battleID, bool isLocalPlayer)
        {
            BattleID = battleID;
            IsLocalPlayer = isLocalPlayer;
            networkRoleInitialized = true;
        }

        protected override void Awake()
        {
            base.Awake();

            camera = Camera.main != null ? Camera.main.transform : transform;
            movementStateMachine = new PlayerMovementStateMachine(this);
            comboStateMachine = new PlayerComboStateMachine(this);
            networkInput = new PlayerNetworkInput(this);
            actions = new PlayerActionController(this);
        }

       

        protected override void Start()
        {
            base.Start();
            if (networkRoleInitialized) StartStates();
          
        }
        protected override void Update()
        {
            if (!statesStarted) return;
            if (IsLocalPlayer)
            {
                base.Update();
                networkInput.Capture();
                comboStateMachine.Combo.UpdateEnemy(networkInput.WorldMoveDirection);
            }
            movementStateMachine.UpdateAnimationParameters();
            movementStateMachine.Update();
            comboStateMachine.Update();
        }

        public void ApplyNetworkMovement(int direction)
        {
            HasNetworkMovement = direction >= 0 && direction <= 120;
            if (!statesStarted && networkRoleInitialized && isActiveAndEnabled) StartStates();
            if (!statesStarted || !isActiveAndEnabled) return;
            movementStateMachine.ApplyMovement(direction);
        }

        public bool ApplyNetworkAction(PlayerOperation operation)
        {
            if (!statesStarted || !isActiveAndEnabled) return false;
            return actions.Execute(operation.rightOperation, operation.operationValue2);
        }

        public ActionPreparation PrepareNetworkAction(RightOpType type, int moving, out int payload)
        {
            return actions.Prepare(type, moving, out payload);
        }

        public void LogicTick()
        {
            if (statesStarted) actions.LogicTick();
        }

        public bool IsPlayingAnimationTag(string tag)
        {
            var state = characterAnimator.IsInTransition(0)
                ? characterAnimator.GetNextAnimatorStateInfo(0)
                : characterAnimator.GetCurrentAnimatorStateInfo(0);
            // Some existing controllers name TurnRun but leave its tag empty.
            return state.IsTag(tag) || state.IsName(tag);
        }

        private void OnDestroy()
        {
            comboStateMachine?.Combo.Dispose();
        }
        #region 相关动画进入或退出触发的方法
        public void OnAnimationTranslateEvent(OnEnterAnimationPlayerState playerState)
        {
            if (!statesStarted) return;
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
                // ATK/Dash are entered by the network action dispatcher. Late animation
                // callbacks must not overwrite a newer command.
            }
          
        }

        public void OnAnimationExitEvent()
        {
            if (!statesStarted) return;
            movementStateMachine.OnAnimationExitEvent();

            comboStateMachine.OnAnimationExitEvent();
        }
        #endregion

        #region 状态变更事件
        public void OnEnable()
        {
            //注册movement状态机中的状态变更事件
            if (movementStateMachine != null)
            {
                movementStateMachine.currentState.OnValueChanged += MovementStateChanged;
            }

            if (comboStateMachine != null)
            {
                comboStateMachine.currentState.OnValueChanged += ComboStateChanged;
            }

            // 先恢复监听，再进入状态，否则重新启用后状态名称仍为空。
            if (networkRoleInitialized && movementStateMachine != null && !statesStarted)
                StartStates();
        }
       

        public void OnDisable()
        {
            networkInput?.Clear();
            if (IsLocalPlayer && networkRoleInitialized) BattleData.Instance.StopMove();
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

        private void StartStates()
        {
            if (statesStarted) return;
            statesStarted = true;
            movementStateMachine.ChangeState(movementStateMachine.idlingState);
            movementStateMachine.ReturnToLocomotion();
            comboStateMachine.ChangeState(comboStateMachine.NullState);
            if (!IsLocalPlayer) return;
            playerCameraUtility?.Init();
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
            if (!statesStarted || comboStateMachine.currentState.Value != comboStateMachine.ATKIngState) return;
            comboStateMachine.ATKIngState.EnablePreInput();
        }
        /// <summary>
        /// 取消攻击冷却
        /// </summary>
        public void CancelAttackColdTime()
        { 
            if (!statesStarted || comboStateMachine.currentState.Value != comboStateMachine.ATKIngState) return;
            comboStateMachine.ATKIngState.CancelAttackColdTime();
        }

        /// <summary>
        /// 取消连招
        /// </summary>
        public void DisableLinkCombo()
        { 
            if (!statesStarted || comboStateMachine.currentState.Value != comboStateMachine.ATKIngState) return;
            comboStateMachine.ATKIngState.DisableLinkCombo();
        }
        /// <summary>
        /// 打断移动
        /// </summary>
        public void EnableMoveInterrupt()
        {
            if (!statesStarted || comboStateMachine.currentState.Value != comboStateMachine.ATKIngState) return;
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
            if (!IsLocalPlayer || finishSkillTimeline == null) return;
            finishSkillTimeline.time = 0;
            finishSkillTimeline.Play();
        }
    }
}
