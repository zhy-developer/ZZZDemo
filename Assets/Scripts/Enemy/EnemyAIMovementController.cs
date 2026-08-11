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
    [SerializeField] private float rotationSpeed = 540f;
    [SerializeField] private float destinationRefreshDistance = 0.25f;
    [SerializeField] private float arriveVelocityThreshold = 0.05f;

    [Header("Attacks")]
    [SerializeField]
    private AttackState[] attackStates = {};

    private int movementHash;
    private int hasInputHash;
    private int hasMoveInputHash;
    private int runHash;
    private int turnDeltaAngleHash;
    private int hasInputForStopHash;
    private string currentAttackState;
    private int currentAttackLayer;

    public bool IsOnNavMesh => agent != null && agent.isOnNavMesh;
    public bool HasPath => agent != null && agent.hasPath;
    public float RemainingDistance => agent != null ? agent.remainingDistance : 0f;

    private void Awake()
    {
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
            agent.updatePosition = false;
            agent.updateRotation = false;
            if (agent.isOnNavMesh) {
                agent.nextPosition = transform.position;
            }
        }
    }

    private void LateUpdate()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh) {
            agent.nextPosition = transform.position;
        }
    }

    public bool MoveTo(GameObject target, float stoppingDistance, bool run)
    {
        if (target == null) {
            StopMovement();
            return false;
        }

        return MoveTo(target.transform.position, stoppingDistance, run);
    }

    public bool MoveTo(Vector3 destination, float stoppingDistance, bool run)
    {
        if (agent == null || animator == null || !agent.enabled || !agent.isOnNavMesh) {
            StopMovement();
            return false;
        }

        agent.stoppingDistance = stoppingDistance;
        agent.isStopped = false;

        if (!agent.hasPath || Vector3.Distance(agent.destination, destination) > destinationRefreshDistance) {
            if (!agent.SetDestination(destination)) {
                StopMovement();
                return false;
            }
        }

        var arrived = !agent.pathPending &&
                      agent.remainingDistance <= stoppingDistance &&
                      agent.desiredVelocity.sqrMagnitude <= arriveVelocityThreshold * arriveVelocityThreshold;
        if (arrived) {
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

        SetMovementParameters(true, run, agent.desiredVelocity.magnitude);
        return false;
    }

    public void StopMovement(bool clearPath = true)
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh) {
            agent.isStopped = true;
            if (clearPath) {
                agent.ResetPath();
            }
            agent.nextPosition = transform.position;
        }
        animator.SetBool(hasInputForStopHash, true);
        SetMovementParameters(false, false, 0f);
    }

    public bool FaceTarget(GameObject target, float angleTolerance)
    {
        if (target == null) {
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
        if (animator == null || attackStates == null || attackStates.Length == 0) {
            return false;
        }

        StopMovement();

        var attack = attackStates[Random.Range(0, attackStates.Length)];
        if (string.IsNullOrEmpty(attack.stateName)) {
            return false;
        }

        currentAttackState = attack.stateName;
        currentAttackLayer = attack.layer;
        animator.CrossFadeInFixedTime(attack.stateName, attack.crossFadeDuration, attack.layer);
        return true;
    }

    public bool IsCurrentAttackFinished(float normalizedTime = 0.95f)
    {
        if (animator == null || string.IsNullOrEmpty(currentAttackState)) {
            return true;
        }

        var stateInfo = animator.GetCurrentAnimatorStateInfo(currentAttackLayer);
        if (!stateInfo.IsName(currentAttackState)) {
            return false;
        }

        return stateInfo.normalizedTime >= normalizedTime && !animator.IsInTransition(currentAttackLayer);
    }

    private void SetMovementParameters(bool hasMoveInput, bool run, float speed)
    {
        if (animator == null) {
            return;
        }

        animator.SetBool(hasInputHash, hasMoveInput);
        animator.SetBool(hasMoveInputHash, hasMoveInput);
        animator.SetBool(runHash, run);
        animator.SetFloat(movementHash, hasMoveInput ? Mathf.Max(1f, speed) : 0f, movementDampTime, Time.deltaTime);
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
