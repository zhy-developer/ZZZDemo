using UnityEngine;
using UnityEngine.Playables;
using SkillRuntime;
using static OnAnimationTranslation;

namespace ZZZ
{
    // Input/presentation adapter. FSMs below only consume committed visual state.
    [RequireComponent(typeof(Animator), typeof(CharacterController))]
    public class Player : CharacterMoveControllerBase
    {
        [SerializeField] public CharacterNameList characterName;
        [SerializeField] public Transform enemy;
        [SerializeField] public PlayerSO playerSO;
        [SerializeField] public PlayerCameraUtility playerCameraUtility;
        [SerializeField] public string currentMovementState, currentComboState;
        [SerializeField] private Transform cameraFollowTarget, cameraLookAtTarget;
        [SerializeField] private PlayableDirector finishSkillTimeline;
        public Transform CameraFollowTarget => cameraFollowTarget ? cameraFollowTarget : transform;
        public Transform CameraLookAtTarget => cameraLookAtTarget ? cameraLookAtTarget : transform;
        public new Transform camera { get; private set; }
        public int BattleID { get; private set; }
        public bool IsLocalPlayer { get; private set; }
        public bool IsBattleBound { get; private set; }
        public bool BattleRoundClosed { get; internal set; }
        public bool HasNetworkMovement { get; private set; }
        public PlayerMovementStateMachine movementStateMachine { get; private set; }
        public PlayerComboStateMachine comboStateMachine { get; private set; }
        PlayerNetworkInput networkInput;
        System.Func<BattleFrame> inputSnapshot;
        bool locomotion, locomotionMoving, hasLocomotion;
        PlayerAttackPlayback attackPlayback;
        AttackStyle? cinematicStyle;
        CameraSwitcher cinematicCameras;
        public void BindBattleInput(System.Func<BattleFrame> snapshot)
        { inputSnapshot = snapshot; IsBattleBound = true; }
        internal Vector3 BattleInputAim()
        {
            var snapshot = inputSnapshot?.Invoke();
            if (snapshot != null && BattleID >= 1 && BattleID <= 2)
            {
                var self = snapshot.Actors[BattleID - 1]; var target = snapshot.Actors[2 - BattleID];
                long x = (long)target.X - self.X, z = (long)target.Z - self.Z;
                if (x != 0 || z != 0) return new Vector3(x, 0, z);
                float angle = self.Facing * Mathf.Deg2Rad;
                return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            }
            return Vector3.right;
        }
        public void InitializeNetworkRole(int battleID, bool isLocalPlayer)
        { BattleID = battleID; IsLocalPlayer = isLocalPlayer; }
        protected override void Awake()
        {
            base.Awake();
            camera = Camera.main ? Camera.main.transform : transform;
            characterAnimator.applyRootMotion = false;
            characterAnimator.fireEvents = false;
            characterAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            attackPlayback = new PlayerAttackPlayback(characterAnimator);
            movementStateMachine = new PlayerMovementStateMachine(this);
            comboStateMachine = new PlayerComboStateMachine(this);
            networkInput = new PlayerNetworkInput(this);
        }
        protected override void Start()
        {
            base.Start();
            if (IsLocalPlayer) playerCameraUtility?.Init();
        }
        protected override void Update()
        {
            if (IsBattleBound && IsLocalPlayer && inputSnapshot?.Invoke()?.Result == RoundResult.None) networkInput.Capture();
        }
        public void BindBattleView()
        {
            hasLocomotion = characterAnimator.HasState(0, Animator.StringToHash("WalkStart"))
                && characterAnimator.HasState(0, Animator.StringToHash("Locomotion"))
                && characterAnimator.HasState(0, Animator.StringToHash("Idle"));
            StopBattlePresentation();
        }
        public void ApplyRenderFrame(PlayerRenderFrame frame)
        {
            HasNetworkMovement = frame.Moving;
            movementStateMachine.Apply(frame);
            comboStateMachine.Apply(frame);
            currentMovementState = movementStateMachine.currentState.Value?.GetType().Name;
            currentComboState = comboStateMachine.currentState.Value?.GetType().Name;
        }
        // Called by presentation states only. These methods never mutate combat state.
        internal void PlayAttack(PlayerRenderFrame frame)
        {
            if (locomotion) StopBattlePresentation();
            attackPlayback.Apply(frame);
            transform.localPosition = Vector3.zero;
        }
        internal void PlayPose(string state, float normalizedTime)
        {
            if (!characterAnimator || string.IsNullOrEmpty(state) || !characterAnimator.HasState(0, Animator.StringToHash(state))) return;
            StopBattlePresentation();
            characterAnimator.Play(state, 0, normalizedTime);
            characterAnimator.Update(0);
            transform.localPosition = Vector3.zero;
        }
        internal void PlayLocomotion(string idleState, bool moving)
        {
            moving &= hasLocomotion && !BattleRoundClosed;
            bool entering = !locomotion;
            if (!entering && locomotionMoving == moving) return;
            locomotion = true; locomotionMoving = moving;
            attackPlayback?.Reset();
            characterAnimator.applyRootMotion = false; characterAnimator.fireEvents = false; characterAnimator.speed = 1;
            characterAnimator.SetBool(AnimatorID.HasInputID, moving);
            characterAnimator.SetBool(AnimatorID.TurnBackID, false);
            characterAnimator.SetBool(AnimatorID.RunID, false);
            characterAnimator.SetFloat(AnimatorID.LockID, 0);
            characterAnimator.SetFloat(AnimatorID.MovementID, moving ? playerSO.movementData.runData.inputMult : 0);
            if (moving) characterAnimator.CrossFadeInFixedTime("WalkStart", .14f, 0, 0);
            else if (entering) characterAnimator.CrossFadeInFixedTime(hasLocomotion ? "Idle" : idleState, .14f, 0, 0);
        }
        public void StopBattlePresentation()
        {
            attackPlayback?.Reset();
            if (!characterAnimator) return;
            if (locomotion)
            {
                characterAnimator.SetBool(AnimatorID.HasInputID, false);
                characterAnimator.SetFloat(AnimatorID.MovementID, 0);
            }
            locomotion = false; locomotionMoving = false; characterAnimator.speed = 0;
        }
        void LateUpdate()
        {
            attackPlayback?.ClampEnd();
            if (IsBattleBound) transform.localPosition = Vector3.zero;
        }
        void OnDisable() { StopSkillPresentation(); StopBattlePresentation(); networkInput?.Clear(); }
        public void StartSkillPresentation(AttackStyle style, double elapsedSeconds)
        {
            StopSkillPresentation();
            if (!IsLocalPlayer) return;
            cinematicStyle = style;
            cinematicCameras = FindObjectOfType<CameraSwitcher>();
            cinematicCameras?.ActiveStateCamera(this, style);
            if (style != AttackStyle.FinishSkill || !finishSkillTimeline || !finishSkillTimeline.playableAsset) return;
            // The old Timeline binds both camera and character. Only its camera may play here;
            // otherwise it would compete with the snapshot-driven character Animator.
            foreach (var output in finishSkillTimeline.playableAsset.outputs)
                if (finishSkillTimeline.GetGenericBinding(output.sourceObject) == characterAnimator)
                    finishSkillTimeline.ClearGenericBinding(output.sourceObject);
            finishSkillTimeline.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            if (elapsedSeconds >= finishSkillTimeline.duration) return;
            finishSkillTimeline.time = elapsedSeconds;
            finishSkillTimeline.Play();
        }
        public void StopSkillPresentation()
        {
            if (cinematicStyle.HasValue && cinematicCameras) cinematicCameras.UnActiveStateCamera(this, cinematicStyle.Value);
            if (cinematicStyle == AttackStyle.FinishSkill && finishSkillTimeline) finishSkillTimeline.Stop();
            cinematicStyle = null; cinematicCameras = null;
        }
        // Compatibility receivers for existing animation assets. Combat authority was removed.
        public void OnAnimationTranslateEvent(OnEnterAnimationPlayerState state) { }
        public void OnAnimationExitEvent() { }
        public void EnablePreInput(AnimationEvent value = null) { }
        public void CancelAttackColdTime(AnimationEvent value = null) { }
        public void DisableLinkCombo(AnimationEvent value = null) { }
        public void EnableMoveInterrupt(AnimationEvent value = null) { }
        public void ATK(AnimationEvent value = null) { }
        public void PlayVFX(string name) { }
        public void PlayFootSound() { }
        public void PlayFootBackSound() { }
        public void PlayWeaponBackSound() { }
        public void PlayWeaponEndSound() { }
        public void PlayDodgeSound() { }
        public void PlaySwitchWindSound() { }
        public void PlaySwitchInVoice() { }
        public void PlayFinishSkillTimeline()
        { StartSkillPresentation(AttackStyle.FinishSkill, 0); }
    }
}
