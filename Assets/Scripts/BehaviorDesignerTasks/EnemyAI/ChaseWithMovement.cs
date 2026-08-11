#if GRAPH_DESIGNER
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.GraphDesigner.Runtime.Variables;
using UnityEngine;

[Opsive.Shared.Utility.Category("Enemy AI")]
[Opsive.Shared.Utility.Description("Moves toward a target by using NavMeshAgent for path steering and the enemy Movement animation for root-motion movement.")]
public class ChaseWithMovement : TargetGameObjectAction
{
    [Tooltip("The target GameObject to chase.")]
    [SerializeField] private SharedVariable<GameObject> m_Target;
    [Tooltip("The distance where the enemy should stop chasing.")]
    [SerializeField] private SharedVariable<float> m_StoppingDistance = 1f;
    [Tooltip("Should the Run animator parameter be enabled while chasing?")]
    [SerializeField] private SharedVariable<bool> m_Run = true;

    private EnemyAIMovementController movementController;

    protected override void InitializeTarget()
    {
        base.InitializeTarget();
        movementController = m_ResolvedGameObject != null ? m_ResolvedGameObject.GetComponent<EnemyAIMovementController>() : null;
    }

    public override TaskStatus OnUpdate()
    {
        if (movementController == null || m_Target.Value == null) {
            return TaskStatus.Failure;
        }

        return movementController.MoveTo(m_Target.Value, m_StoppingDistance.Value, m_Run.Value)
            ? TaskStatus.Success
            : TaskStatus.Running;
    }

    public override void OnEnd()
    {
        DeLogger.LogTrace("ChaseWithMovement.OnEnd");
        movementController?.StopMovement(false);
    }

    public override void Reset()
    {
        base.Reset();
        m_Target = null;
        m_StoppingDistance = 1.5f;
        m_Run = true;
    }
}
#endif
