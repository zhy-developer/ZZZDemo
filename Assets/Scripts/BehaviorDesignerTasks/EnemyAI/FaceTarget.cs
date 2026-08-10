#if GRAPH_DESIGNER
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.GraphDesigner.Runtime.Variables;
using UnityEngine;

[Opsive.Shared.Utility.Category("Enemy AI")]
[Opsive.Shared.Utility.Description("Rotates the enemy toward a target.")]
public class FaceTarget : TargetGameObjectAction
{
    [Tooltip("The target GameObject to face.")]
    [SerializeField] private SharedVariable<GameObject> m_Target;
    [Tooltip("The angle tolerance for considering the target faced.")]
    [SerializeField] private SharedVariable<float> m_AngleTolerance = 5f;

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

        return movementController.FaceTarget(m_Target.Value, m_AngleTolerance.Value)
            ? TaskStatus.Success
            : TaskStatus.Running;
    }

    public override void Reset()
    {
        base.Reset();
        m_Target = null;
        m_AngleTolerance = 5f;
    }
}
#endif
