using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Animator))]
public class EnemyAIMovementController : MonoBehaviour
{
    [System.Serializable]
    public struct AttackState
    {
        public string stateName;
        public int layer;
        public float crossFadeDuration;
    }

    [Header("References")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;

    [Header("Animator Parameters")]
    [SerializeField] private string movementParameter = "Movement";
    [SerializeField] private string hasInputParameter = "HasInput";
    [SerializeField] private string hasMoveInputParameter = "HasMoveInput";
    [SerializeField] private string runParameter = "Run";
    [SerializeField] private string turnDeltaAngleParameter = "TurnDeltaAngle";
    [SerializeField] private string hasInputForStopParameter = "HasInputForStop";
    [SerializeField] private float movementDampTime = 0.15f;

    [Header("Movement")]
    [SerializeField] private bool agentDrivesPosition = true;
    [SerializeField] private float rotationSpeed = 540f;
    [SerializeField] private float destinationRefreshDistance = 0.25f;
    [Header("Chase Stop Animation")]
    [SerializeField] private string chaseStopState = "Base Layer.Movement.急停.Run_End";
    [SerializeField] private string chaseIdleState = "Base Layer.Idle";
    [SerializeField] private int chaseStopLayer = 0;
    [SerializeField] private float chaseStopCrossFadeDuration = 0.1f;
    private bool chaseStopWasEntered;
    private bool chaseStopPlayedFully;
    private bool chaseStopOriginalApplyRootMotion;
    private bool chaseStopRootMotionOverridden;

    public bool LastMoveFailed { get; private set; }
    public bool CanAnimate => animator != null && animator.isActiveAndEnabled;
    public int HitReactionVersion { get; private set; }
    private int lastHitReactionFrame = -1;

    // Include the incoming state: CrossFade does not replace the current state immediately.
    public bool IsReactingToHit
    {
        get {
            if (lastHitReactionFrame == Time.frameCount) {
                return true;
            }
            if (!CanAnimate) {
                return false;
            }
            var current = animator.GetCurrentAnimatorStateInfo(0);
            if (current.IsTag("Hit") || current.IsTag("Parry")) {
                return true;
            }
            if (!animator.IsInTransition(0)) {
                return false;
            }
            var next = animator.GetNextAnimatorStateInfo(0);
            return next.IsTag("Hit") || next.IsTag("Parry");
        }
    }

    public bool CanAct => CanAnimate && (health == null || !health.IsDead) && !IsReactingToHit;

    public void NotifyHitReaction()
    {
        // Remember the interruption even if the tree ticks after the reaction has ended.
        HitReactionVersion++;
        lastHitReactionFrame = Time.frameCount;
        StopMovement();
    }

    [Header("Attacks")]
    [SerializeField]
    private AttackState[] attackStates = {};

    private CharacterHealthBase health;
    private int movementHash;
    private int hasInputHash;
    private int hasMoveInputHash;
    private int runHash;
    private int turnDeltaAngleHash;
    private int hasInputForStopHash;
    private string currentAttackState;
    private int currentAttackLayer;
    private bool currentAttackWasEntered;

    public bool IsOnNavMesh => agent != null && agent.isOnNavMesh;
    public bool HasPath => agent != null && agent.hasPath;
    public float RemainingDistance => agent != null ? agent.remainingDistance : 0f;

    private void Awake()
    {
        health = GetComponent<CharacterHealthBase>();
        if (agent == null) {
            agent = GetComponent<NavMeshAgent>();
        }
        if (animator == null) {
            animator = GetComponent<Animator>();
        }

        movementHash = Animator.StringToHash(movementParameter);
        hasInputHash = Animator.StringToHash(hasInputParameter);
        hasMoveInputHash = Animator.StringToHash(hasMoveInputParameter);
        runHash = Animator.StringToHash(runParameter);
        turnDeltaAngleHash = Animator.StringToHash(turnDeltaAngleParameter);
        hasInputForStopHash = Animator.StringToHash(hasInputForStopParameter);
        
        if (agent != null) {
            agent.updatePosition = agentDrivesPosition;
            agent.updateRotation = false;
            if (!agentDrivesPosition && agent.isOnNavMesh) {
                agent.nextPosition = transform.position;
            }
        }
    }

    private void LateUpdate()
    {
        if (agent != null && !agent.updatePosition && agent.enabled && agent.isOnNavMesh) {
            agent.nextPosition = transform.position;
        }
    }

    public bool MoveTo(GameObject target, float stoppingDistance, bool run)
    {
        if (!CanAct || target == null)
        {
            LastMoveFailed = true;
            StopMovement();
            return false;
        }

        return MoveTo(target.transform.position, stoppingDistance, run);
    }

    public bool MoveTo(Vector3 destination, float stoppingDistance, bool run)
    {
        LastMoveFailed = false;
        if (!CanAct || agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) {
            LastMoveFailed = true;
            DeLogger.LogErrorTrace("移动条件不满足");
            StopMovement();
            return false;
        }

        agent.stoppingDistance = stoppingDistance;
        var offset = destination - transform.position;
        offset.y = 0f;
        if (offset.magnitude <= stoppingDistance) {
            StopMovement(false);
            return true;
        }

        agent.isStopped = false;
        if (!agent.pathPending && (!agent.hasPath || Vector3.Distance(agent.destination, destination) > destinationRefreshDistance)) {
            if (!agent.SetDestination(destination)) {
                DeLogger.LogErrorTrace("没有设置Destination");
                LastMoveFailed = true;
                StopMovement();
                return false;
            }
        }

        // SetDestination can succeed before Unity has produced a path on this frame.
        if (!agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathInvalid) {
            DeLogger.LogErrorTrace("路径无效");
            LastMoveFailed = true;
            StopMovement();
            return false;
        }

        if (!agent.pathPending && !agent.hasPath) {
            SetMovementParameters(true, run);
            return false;
        }

        if (!agent.pathPending && agent.hasPath && agent.remainingDistance <= stoppingDistance) {
            if (agent.pathStatus != NavMeshPathStatus.PathComplete) {
                LastMoveFailed = true;
                StopMovement();
                return false;
            }
            StopMovement(false);
            return true;
        }

        var steeringDirection = agent.steeringTarget - transform.position;
        steeringDirection.y = 0f;
        if (steeringDirection.sqrMagnitude < 0.01f) {
            steeringDirection = agent.desiredVelocity;
            steeringDirection.y = 0f;
        }
        if (steeringDirection.sqrMagnitude > 0.01f) {
            RotateTowards(steeringDirection.normalized);
        }

        SetMovementParameters(true, run);
        return false;
    }

    public bool BeginChaseStop()
    {
        if (!CanAct) {
            StopMovement();
            return false;
        }
        RestoreChaseStopRootMotion();
        chaseStopWasEntered = false;
        chaseStopPlayedFully = false;
        if (!CanAnimate || string.IsNullOrEmpty(chaseStopState) || string.IsNullOrEmpty(chaseIdleState) ||
            !animator.HasState(chaseStopLayer, Animator.StringToHash(chaseStopState)) ||
            !animator.HasState(chaseStopLayer, Animator.StringToHash(chaseIdleState))) {
            Debug.LogWarning("Chase stop requires valid stop and Idle animation states.", this);
            return false;
        }

        StopMovement(false);
        DisableChaseStopRootMotion();
        animator.SetFloat(movementHash, 0f);
        animator.CrossFadeInFixedTime(chaseStopState, chaseStopCrossFadeDuration, chaseStopLayer, 0f);
        return true;
    }

    public bool IsChaseStopFinished()
    {
        if (!CanAnimate) {
            return false;
        }
        var state = animator.GetCurrentAnimatorStateInfo(chaseStopLayer);
        if (state.IsName(chaseStopState)) {
            chaseStopWasEntered = true;
            chaseStopPlayedFully |= state.normalizedTime >= 1f;
            return false;
        }

        // Idle before the crossfade starts is not proof that Run_End has finished.
        var finished = chaseStopWasEntered && chaseStopPlayedFully && state.IsName(chaseIdleState) &&
                       !animator.IsInTransition(chaseStopLayer);
        if (finished) {
            RestoreChaseStopRootMotion();
        }
        return finished;
    }

    private void DisableChaseStopRootMotion()
    {
        if (animator == null || chaseStopRootMotionOverridden) {
            return;
        }

        chaseStopOriginalApplyRootMotion = animator.applyRootMotion;
        animator.applyRootMotion = false;
        chaseStopRootMotionOverridden = true;
    }

    private void RestoreChaseStopRootMotion()
    {
        if (animator == null || !chaseStopRootMotionOverridden) {
            return;
        }

        animator.applyRootMotion = chaseStopOriginalApplyRootMotion;
        chaseStopRootMotionOverridden = false;
    }
    public void StopMovement(bool clearPath = true)
    {
        RestoreChaseStopRootMotion();
        if (agent != null && agent.enabled && agent.isOnNavMesh) {
            agent.isStopped = true;
            if (clearPath) {
                agent.ResetPath();
            }
            agent.nextPosition = transform.position;
        }
        if (animator != null) {
            animator.SetBool(hasInputForStopHash, true);
        }
        SetMovementParameters(false, false);
    }

    public bool FaceTarget(GameObject target, float angleTolerance)
    {
        if (!CanAct || target == null) {
            return false;
        }

        var direction = target.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) {
            return true;
        }

        RotateTowards(direction.normalized);
        return Vector3.Angle(transform.forward, direction) <= angleTolerance;
    }

    public bool PlayRandomAttack()
    {
        if (!CanAct || attackStates == null || attackStates.Length == 0) {
            return false;
        }

        StopMovement();

        var attack = attackStates[Random.Range(0, attackStates.Length)];
        if (string.IsNullOrEmpty(attack.stateName)) {
            return false;
        }

        currentAttackState = attack.stateName;
        currentAttackLayer = attack.layer;
        currentAttackWasEntered = false;
        animator.CrossFadeInFixedTime(attack.stateName, attack.crossFadeDuration, attack.layer);
        return true;
    }

    public bool IsCurrentAttackFinished(float normalizedTime = 0.95f)
    {
        if (animator == null || string.IsNullOrEmpty(currentAttackState)) {
            return true;
        }

        var stateInfo = animator.GetCurrentAnimatorStateInfo(currentAttackLayer);
        if (stateInfo.IsName(currentAttackState)) {
            currentAttackWasEntered = true;
            return stateInfo.normalizedTime >= normalizedTime && !animator.IsInTransition(currentAttackLayer);
        }

        if (animator.IsInTransition(currentAttackLayer)) {
            var nextStateInfo = animator.GetNextAnimatorStateInfo(currentAttackLayer);
            if (nextStateInfo.IsName(currentAttackState)) {
                return false;
            }
        }

        if (!currentAttackWasEntered) {
            return false;
        }

        currentAttackState = null;
        return true;
    }

    private void SetMovementParameters(bool hasMoveInput, bool run)
    {
        if (animator == null) {
            return;
        }

        if (hasMoveInput) {
            animator.SetBool(hasInputForStopHash, false);
        }
        animator.SetBool(hasInputHash, hasMoveInput);
        animator.SetBool(hasMoveInputHash, hasMoveInput);
        animator.SetBool(runHash, run);
        animator.SetFloat(movementHash, hasMoveInput ? (run ? 2f : 1f) : 0f, movementDampTime, Time.deltaTime);
    }

    private void RotateTowards(Vector3 direction)
    {
        var targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

        if (animator != null) {
            var signedAngle = Vector3.SignedAngle(transform.forward, direction, Vector3.up);
            animator.SetFloat(turnDeltaAngleHash, signedAngle);
        }
    }
}
