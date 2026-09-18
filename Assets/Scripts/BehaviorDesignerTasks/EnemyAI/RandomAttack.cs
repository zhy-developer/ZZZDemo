#if GRAPH_DESIGNER
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.GraphDesigner.Runtime.Variables;
using UnityEngine;

[Opsive.Shared.Utility.Category("Enemy AI")]
[Opsive.Shared.Utility.Description("Plays one random attack from EnemyAIMovementController and waits for it to complete.")]
public class RandomAttack : TargetGameObjectAction
{
    [Tooltip("The cooldown after a completed attack.")]
    [SerializeField] private SharedVariable<float> m_Cooldown = 1f;
    [Tooltip("The normalized time where the attack is considered finished.")]
    [SerializeField] private SharedVariable<float> m_FinishedNormalizedTime = 0.95f;

    private EnemyAIMovementController movementController;
    private bool attackStarted;
    private int hitReactionVersion;
    private float nextAttackTime;

    protected override void InitializeTarget()
    {
        base.InitializeTarget();
        movementController = m_ResolvedGameObject != null ? m_ResolvedGameObject.GetComponent<EnemyAIMovementController>() : null;
    }

    public override void OnStart()
    {
        attackStarted = false;
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

        if (!attackStarted) {
            if (Time.time < nextAttackTime) {
                return TaskStatus.Failure;
            }

            if (!movementController.PlayRandomAttack()) {
                return TaskStatus.Failure;
            }

            attackStarted = true;
        }

        if (!movementController.IsCurrentAttackFinished(m_FinishedNormalizedTime.Value)) {
            return TaskStatus.Running;
        }

        nextAttackTime = Time.time + m_Cooldown.Value;
        return TaskStatus.Success;
    }

    public override void OnEnd()
    {
        attackStarted = false;
    }

    public override void Reset()
    {
        base.Reset();
        m_Cooldown = 1f;
        m_FinishedNormalizedTime = 0.95f;
    }
}
#endif
