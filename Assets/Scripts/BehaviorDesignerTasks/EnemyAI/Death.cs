#if GRAPH_DESIGNER
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.GraphDesigner.Runtime.Variables;
using UnityEngine;
using UnityEngine.AI;

[Opsive.Shared.Utility.Category("Enemy AI")]
[Opsive.Shared.Utility.Description("Fails while alive. When dead, stops navigation and plays the death state once, succeeding after the animation finishes.")]
public class Death : TargetGameObjectAction
{
    [Tooltip("Full Animator state path for the death animation.")]
    [SerializeField] private SharedVariable<string> m_DeathState = "Base Layer.Hit.Dead";
    [SerializeField] private SharedVariable<int> m_Layer = 0;
    [SerializeField] private SharedVariable<float> m_CrossFadeDuration = 0.1f;

    private CharacterHealthBase health;
    private EnemyAIMovementController movementController;
    private Animator animator;
    private NavMeshAgent agent;
    private bool deathStarted;
    private EnemyPoolItem pooledItem;

    protected override void InitializeTarget()
    {
        base.InitializeTarget();
        health = m_ResolvedGameObject != null ? m_ResolvedGameObject.GetComponent<CharacterHealthBase>() : null;
        movementController = m_ResolvedGameObject != null ? m_ResolvedGameObject.GetComponent<EnemyAIMovementController>() : null;
        animator = m_ResolvedGameObject != null ? m_ResolvedGameObject.GetComponent<Animator>() : null;
        agent = m_ResolvedGameObject != null ? m_ResolvedGameObject.GetComponent<NavMeshAgent>() : null;
        pooledItem = m_ResolvedGameObject != null ? m_ResolvedGameObject.GetComponent<EnemyPoolItem>() : null;
    }

    public override void OnStart()
    {
        deathStarted = false;
    }

    public override TaskStatus OnUpdate()
    {
        if (health == null || !health.IsDead) {
            return TaskStatus.Failure;
        }

        // Stop even when the animation is misconfigured.
        movementController?.StopMovement();
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) {
            agent.isStopped = true;
            if (agent.hasPath) {
                agent.ResetPath();
            }
        }

        if (animator == null || !animator.isActiveAndEnabled) {
            return TaskStatus.Failure;
        }

        var stateName = m_DeathState.Value;
        var layer = m_Layer.Value;
        if (layer < 0 || layer >= animator.layerCount || string.IsNullOrEmpty(stateName) ||
            !animator.HasState(layer, Animator.StringToHash(stateName))) {
            Debug.LogWarning($"Death: Animator state '{stateName}' was not found on layer {layer}.", m_ResolvedGameObject);
            return TaskStatus.Failure;
        }

        var current = animator.GetCurrentAnimatorStateInfo(layer);
        var transitioning = animator.IsInTransition(layer);
        if (!deathStarted) {
            // A tree restart must not rewind a death animation already playing or finished.
            var alreadyPlaying = current.IsName(stateName) ||
                (transitioning && animator.GetNextAnimatorStateInfo(layer).IsName(stateName));
            if (!alreadyPlaying) {
                animator.CrossFadeInFixedTime(stateName, Mathf.Max(0f, m_CrossFadeDuration.Value), layer, 0f);
            }
            deathStarted = true;
        }

        var finished = current.IsName(stateName) && current.normalizedTime >= 1f && !transitioning;
        if (finished) {
            // Release in LateUpdate, after Behavior Designer has finished this task evaluation.
            pooledItem?.ScheduleReturnAfterDeath();
        }
        return finished ? TaskStatus.Success : TaskStatus.Running;
    }

    public override void Reset()
    {
        base.Reset();
        m_DeathState = "Base Layer.Hit.Dead";
        m_Layer = 0;
        m_CrossFadeDuration = 0.1f;
    }
}
#endif
