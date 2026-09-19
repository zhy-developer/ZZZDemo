using UnityEngine;
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
        //玩家移动状态机
        public PlayerMovementStateMachine movementStateMachine { get;private set; }
        //玩家连招状态机
        public PlayerComboStateMachine comboStateMachine { get;private set; }

        public new Transform camera { get; private set; }

        //游戏黑板数据
        private GameBlackboard gameBlackboard;

        /// <summary>
        /// 是否能继承切人前的疾跑状态
        /// </summary>
        private bool canSprintOnSwitch;
        public bool CanSprintOnSwitch
        { 
        get { return canSprintOnSwitch; } 

        set {
                if (value != canSprintOnSwitch)
                { canSprintOnSwitch = value; } 
            }
        }

        protected override void Awake()
        {
            base.Awake();

            camera = Camera.main.transform;
            movementStateMachine = new PlayerMovementStateMachine(this);
            comboStateMachine = new PlayerComboStateMachine(this);
            playerCameraUtility.Init();
        }

       

        protected override void Start()
        {
            base.Start();
            if (characterName == SwitchCharacter.Instance.newCharacterName.Value)
            {
                movementStateMachine.ChangeState(movementStateMachine.idlingState); 
            }
            else
            {
                
                movementStateMachine.ChangeState(movementStateMachine.onSwitchOutState);
            }
         
            comboStateMachine.ChangeState(comboStateMachine.NullState);


            Player player= GetComponent<Player>();
            //注册黑板信息
            GameBlackboard.Instance.SetGameData<Player>(characterName.ToString(), player);
          
        }
       protected override void Update()
        {
            base.Update();

            if (characterName==SwitchCharacter.Instance.newCharacterName.Value)
            {
                movementStateMachine.HandInput();

                movementStateMachine.Update();

                comboStateMachine.Update();
            }
        }
       

        #region 相关动画进入或退出触发的方法
        public void OnAnimationTranslateEvent(OnEnterAnimationPlayerState playerState)
        {
            switch (playerState)
            {
                case OnEnterAnimationPlayerState.TurnBack:
                    movementStateMachine.OnAnimationTranslateEvent(movementStateMachine.returnRunState);
                    break;
                case OnEnterAnimationPlayerState.Dash:
                    movementStateMachine.OnAnimationTranslateEvent(movementStateMachine.dashingState);
                    comboStateMachine.OnAnimationTranslateEvent(comboStateMachine.NullState);
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
                case OnEnterAnimationPlayerState.DashBack:
                    movementStateMachine.OnAnimationTranslateEvent(movementStateMachine.dashBackingState);
                    comboStateMachine.OnAnimationTranslateEvent(comboStateMachine.NullState);
                    break;
            }
          
        }

        public void OnAnimationExitEvent()
        {
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

            gameBlackboard = GameBlackboard.Instance;
            if (gameBlackboard != null)
            {
                gameBlackboard.enemy.OnValueChanged += EnemyChanged;
            }
        }
       

        public void OnDisable()
        {
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
    

        public void MovementStateChanged(IState currentState)
        {
            currentMovementState= currentState.GetType().Name;
        }
        private void ComboStateChanged(IState state)
        {
           currentComboState= state.GetType().Name;
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
            comboStateMachine.ATKIngState.EnablePreInput();
        }
        /// <summary>
        /// 取消攻击冷却
        /// </summary>
        public void CancelAttackColdTime()
        { 
            comboStateMachine.ATKIngState.CancelAttackColdTime();
        }

        /// <summary>
        /// 取消连招
        /// </summary>
        public void DisableLinkCombo()
        { 
            comboStateMachine.ATKIngState.DisableLinkCombo();
        }
        /// <summary>
        /// 打断移动
        /// </summary>
        public void EnableMoveInterrupt()
        {
            comboStateMachine.ATKIngState.EnableMoveInterrupt();
        }
    
        /// <summary>
        /// 攻击事件
        /// </summary>
        public void ATK(AnimationEvent animationEvent = null)
        {
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
    }
}
