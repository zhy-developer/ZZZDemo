using UnityEngine;
using UnityEngine.AI;

public class NavMeshAgentRuntimeDebug : MonoBehaviour
{
    [SerializeField] private float logInterval = 1f;

    private NavMeshAgent agent;
    private float nextLogTime;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Update()
    {
        if (agent == null || Time.time < nextLogTime) {
            return;
        }

        nextLogTime = Time.time + logInterval;
        Debug.Log(
            $"[NavMeshAgentRuntimeDebug] {name} " +
            $"isOnNavMesh={agent.isOnNavMesh}, " +
            $"hasPath={agent.hasPath}, " +
            $"pathPending={agent.pathPending}, " +
            $"pathStatus={agent.pathStatus}, " +
            $"remainingDistance={agent.remainingDistance}, " +
            $"velocity={agent.velocity}, " +
            $"desiredVelocity={agent.desiredVelocity}, " +
            $"destination={agent.destination}, " +
            $"isStopped={agent.isStopped}",
            this);
    }
}
