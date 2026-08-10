#if GRAPH_DESIGNER
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.GraphDesigner.Runtime.Variables;
using UnityEngine;

[Opsive.Shared.Utility.Category("Enemy AI")]
[Opsive.Shared.Utility.Description("Stops the EnemyAIMovementController and optionally clears the current NavMesh path.")]
public class StopAIMovement : TargetGameObjectAction
{
    [Tooltip("Should the current NavMesh path be cleared?")]
    [SerializeField] private SharedVariable<bool> m_ClearPath = true;

    private EnemyAIMovementController movementController;

    protected override void InitializeTarget()
    {
        base.InitializeTarget();
        movementController = m_ResolvedGameObject != null ? m_ResolvedGameObject.GetComponent<EnemyAIMovementController>() : null;
    }

    public override TaskStatus OnUpdate()
    {
        if (movementController == null) {
            return TaskStatus.Failure;
        }

        movementController.StopMovement(m_ClearPath.Value);
        return TaskStatus.Success;
    }

    public override void Reset()
    {
        base.Reset();
        m_ClearPath = true;
    }
}
#endif
