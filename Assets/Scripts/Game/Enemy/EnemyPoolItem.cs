using UnityEngine;
using UnityEngine.AI;
#if GRAPH_DESIGNER
using Opsive.BehaviorDesigner.Runtime;
#endif

/// <summary>Resets an enemy between lives; pool ownership is assigned by EnemyPoolManager.</summary>
[DisallowMultipleComponent]
public class EnemyPoolItem : MonoBehaviour
{
    [SerializeField, Min(0f), Tooltip("Seconds to keep the corpse after its death animation finishes.")]
    private float corpseDelay = 0f;
    [SerializeField, Min(0.01f)] private float navMeshSampleRadius = 2f;
    [SerializeField] private string idleStateName = "Base Layer.Idle";
    [SerializeField, Tooltip("Optional GameObject shared variable set before the tree starts.")]
    private string targetVariableName = "PlayerTarget";
    [SerializeField] private string deathStateName = "Base Layer.Hit .Dead";
    [SerializeField, Min(0f)] private float deathCrossFadeDuration = 0.1f;

    public EnemyPoolManager Owner { get; private set; }
    public GameObject Prefab { get; private set; }
    public bool IsSpawned { get; private set; }
    public int SpawnVersion { get; private set; }

    private CharacterHealthBase health;
    private EnemyAIMovementController movement;
    private Animator animator;
    private NavMeshAgent agent;
    private bool agentEnabledOnSpawn;
    private bool animatorEnabledOnSpawn;
    private bool originalRootMotion;
    private float originalAnimatorSpeed;
    private bool returnScheduled;
    private bool deathStarted;
    private float returnAt;
#if GRAPH_DESIGNER
    private BehaviorTree behaviorTree;
    private bool startTreeOnSpawn;
#endif

    internal void Initialize(EnemyPoolManager owner, GameObject prefab)
    {
        Owner = owner;
        Prefab = prefab;
        health = GetComponent<CharacterHealthBase>();
        movement = GetComponent<EnemyAIMovementController>();
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        if (animator != null) {
            animatorEnabledOnSpawn = animator.enabled;
            originalRootMotion = animator.applyRootMotion;
            originalAnimatorSpeed = animator.speed;
        }
        if (agent != null) {
            agentEnabledOnSpawn = agent.enabled;
            agent.enabled = false;
        }
#if GRAPH_DESIGNER
        behaviorTree = GetComponent<BehaviorTree>();
        if (behaviorTree != null) {
            startTreeOnSpawn = behaviorTree.enabled;
            behaviorTree.StartWhenEnabled = false;
            behaviorTree.enabled = false;
        }
#endif
    }

    internal bool SpawnAt(Vector3 position, Quaternion rotation, GameObject target)
    {
        returnScheduled = false;
        deathStarted = false;
        StopTree();
        if (agent != null && agentEnabledOnSpawn) {
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(position, out var hit, Mathf.Max(0.01f, navMeshSampleRadius), filter)) {
                Debug.LogWarning("Enemy spawn position has no compatible NavMesh nearby.", this);
                return false;
            }
            position = hit.position;
        }

        // Health initialization is idempotent and also works before the first Awake.
        health?.ResetHealthForSpawn();
        transform.SetPositionAndRotation(position, rotation);
        if (animator != null) { animator.enabled = animatorEnabledOnSpawn; }
        SpawnVersion++;
        IsSpawned = true;
        gameObject.SetActive(true);

        // Animator evaluation requires an active instance. The tree is still disabled here.
        if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null) {
            animator.applyRootMotion = originalRootMotion;
            animator.speed = originalAnimatorSpeed;
            animator.Rebind();
            if (!string.IsNullOrEmpty(idleStateName) && animator.HasState(0, Animator.StringToHash(idleStateName))) {
                animator.Play(idleStateName, 0, 0f);
            }
            animator.Update(0f);
        }

        if (agent != null && agentEnabledOnSpawn) {
            agent.enabled = true;
            if (!agent.Warp(position) || !agent.isOnNavMesh) {
                Debug.LogWarning("Enemy could not be placed on its NavMesh.", this);
                return false;
            }
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.isStopped = true;
        }
        movement?.ResetForSpawn();
        SetTarget(target);
#if GRAPH_DESIGNER
        if (behaviorTree != null && startTreeOnSpawn) {
            behaviorTree.enabled = true;
            if (!behaviorTree.StartBehavior()) {
                Debug.LogWarning("Enemy behavior tree could not start.", this);
                return false;
            }
        }
#endif
        return true;
    }

    internal void ReturnToPool()
    {
        // Set first: OnDisable and task end callbacks must not return this instance twice.
        IsSpawned = false;
        returnScheduled = false;
        deathStarted = false;
        StopTree();
        movement?.StopMovement();
        SetTarget(null);
        if (agent != null) {
            if (agent.enabled && agent.isOnNavMesh) {
                agent.isStopped = true;
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }
            agent.enabled = false;
        }
        gameObject.SetActive(false);
    }

    public bool Despawn()
    {
        return Owner != null && Owner.Despawn(this);
    }

    /// <summary>Called by Death after animation completion, never disables a tree during its tick.</summary>
    public void ScheduleReturnAfterDeath()
    {
        if (!IsSpawned || Owner == null || returnScheduled || health == null || !health.IsDead) { return; }
        returnScheduled = true;
        returnAt = Time.time + Mathf.Max(0f, corpseDelay);
    }

    private void LateUpdate()
    {
        if (!IsSpawned || health == null || !health.IsDead) { return; }
        if (!deathStarted) {
            deathStarted = true;
            StopTree();
            movement?.StopMovement();
            if (agent != null && agent.enabled && agent.isOnNavMesh) {
                agent.isStopped = true;
                agent.ResetPath();
            }

            // A Death task may already have completed; never rewind its animation.
            if (!returnScheduled) {
                if (animator == null || !animator.isActiveAndEnabled ||
                    string.IsNullOrEmpty(deathStateName) ||
                    !animator.HasState(0, Animator.StringToHash(deathStateName))) {
                    Debug.LogWarning("Pooled enemy has no playable death state; returning without an animation.", this);
                    ScheduleReturnAfterDeath();
                } else {
                    var current = animator.GetCurrentAnimatorStateInfo(0);
                    var incomingDeath = animator.IsInTransition(0) &&
                        animator.GetNextAnimatorStateInfo(0).IsName(deathStateName);
                    if (!current.IsName(deathStateName) && !incomingDeath) {
                        animator.CrossFadeInFixedTime(deathStateName, Mathf.Max(0f, deathCrossFadeDuration), 0, 0f);
                    }
                }
            }
        }
        if (!returnScheduled && animator != null && animator.isActiveAndEnabled) {
            var current = animator.GetCurrentAnimatorStateInfo(0);
            if (current.IsName(deathStateName) && current.normalizedTime >= 1f && !animator.IsInTransition(0)) {
                ScheduleReturnAfterDeath();
            }
        }
        if (returnScheduled && Time.time >= returnAt) { Despawn(); }
    }

    private void StopTree()
    {
#if GRAPH_DESIGNER
        if (behaviorTree != null) {
            behaviorTree.StopBehavior(false); // Stop, not pause: a new life starts a fresh branch.
            behaviorTree.enabled = false;
        }
#endif
    }

    private void SetTarget(GameObject target)
    {
#if GRAPH_DESIGNER
        if (behaviorTree == null || string.IsNullOrEmpty(targetVariableName)) { return; }
        if (behaviorTree.GetVariable<GameObject>(targetVariableName) != null) {
            behaviorTree.SetVariableValue<GameObject>(targetVariableName, target);
        } else if (target != null) {
            Debug.LogWarning("Enemy tree has no GameObject variable named " + targetVariableName + ".", this);
        }
#endif
    }

    private void OnDisable()
    {
        if (IsSpawned) { Despawn(); }
    }

    private void OnDestroy()
    {
        if (Owner != null) { Owner.ForgetDestroyed(this); }
    }
}
