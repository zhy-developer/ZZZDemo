#if GRAPH_DESIGNER
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.GraphDesigner.Runtime.Variables;
using UnityEngine;

[Opsive.Shared.Utility.Category("Enemy AI")]
[Opsive.Shared.Utility.Description("Chases with distance-based walk/run, then waits for the stop animation to finish before succeeding.")]
public class ChaseWithMovement : TargetGameObjectAction
{
    [Tooltip("The target GameObject to chase.")]
    [SerializeField] private SharedVariable<GameObject> m_Target;
    [Tooltip("The distance where the enemy should stop chasing.")]
    [SerializeField] private SharedVariable<float> m_StoppingDistance = 1f;
    [Tooltip("Allow running. Disable to always walk.")]
    [SerializeField] private SharedVariable<bool> m_Run = true;
    [Tooltip("Start running beyond this distance.")]
    [SerializeField] private SharedVariable<float> m_RunDistance = 6f;
    [Tooltip("Return to walking below this distance. Between the two thresholds, keep the current gait.")]
    [SerializeField] private SharedVariable<float> m_WalkDistance = 5f;

    private EnemyAIMovementController movementController;
    private bool isRunning;
    private bool waitingForStop;
    private int hitReactionVersion;

    protected override void InitializeTarget()
    {
        base.InitializeTarget();
        movementController = m_ResolvedGameObject != null ? m_ResolvedGameObject.GetComponent<EnemyAIMovementController>() : null;
    }

    public override void OnStart()
    {
        isRunning = false;
        waitingForStop = false;
        hitReactionVersion = movementController != null ? movementController.HitReactionVersion : 0;
    }

    public override TaskStatus OnUpdate()
    {
        if (movementController == null) {
            return TaskStatus.Failure;
        }

        if (!movementController.CanAct || hitReactionVersion != movementController.HitReactionVersion) {
            movementController.StopMovement();
            return TaskStatus.Failure;
        }

        // Once stopping begins, finish it even if the target moves or disappears.
        if (waitingForStop) {
            if (!movementController.CanAnimate) {
                DeLogger.LogTrace("movementcontroller���ɲ��Ŷ���");
                return TaskStatus.Failure;
            }
            return movementController.IsChaseStopFinished() ? TaskStatus.Success : TaskStatus.Running;
        }

        if (m_Target == null || m_Target.Value == null) {
            DeLogger.LogTrace("targetΪ��");
            return TaskStatus.Failure;
        }

        var runDistance = Mathf.Max(0f, m_RunDistance.Value);
        var walkDistance = Mathf.Min(runDistance, Mathf.Max(0f, m_WalkDistance.Value));
        var direction = m_Target.Value.transform.position - movementController.transform.position;
        direction.y = 0f;
        var distance = direction.magnitude;
        if (!m_Run.Value) {
            isRunning = false;
        } else if (distance > runDistance) {
            isRunning = true;
        } else if (distance < walkDistance) {
            isRunning = false;
        }

        var arrived = movementController.MoveTo(m_Target.Value, Mathf.Max(0f, m_StoppingDistance.Value), isRunning);
        if (movementController.LastMoveFailed) {
            DeLogger.LogTrace("LastMoveFailedΪtrue");
            return TaskStatus.Failure;
        }
        if (!arrived) {
            return TaskStatus.Running;
        }

        waitingForStop = movementController.BeginChaseStop();
        return waitingForStop ? TaskStatus.Running : TaskStatus.Failure;
    }

    public override void OnEnd()
    {
        movementController?.StopMovement(false);
        waitingForStop = false;
    }

    public override void Reset()
    {
        base.Reset();
        m_Target = null;
        m_StoppingDistance = 1.5f;
        m_Run = true;
        m_RunDistance = 6f;
        m_WalkDistance = 5f;
    }
}
#endif