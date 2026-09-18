using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using Opsive.BehaviorDesigner.Runtime.Tasks;
public static class EnemyDeathRegression
{
    static void Set(object o, string name, object value) {
        for (var t = o.GetType(); t != null; t = t.BaseType) {
            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (f != null) { f.SetValue(o, value); return; }
        }
        throw new Exception("Missing field " + name);
    }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    class Rig {
        public GameObject Owner = new GameObject();
        public CharacterHealthBase Health = new CharacterHealthBase();
        public Animator Animator = new Animator();
        public NavMeshAgent Agent = new NavMeshAgent();
        public EnemyAIMovementController Movement = new EnemyAIMovementController();
        public Death Task = new Death();
        public Rig() {
            Owner.components[typeof(CharacterHealthBase)] = Health;
            Owner.components[typeof(Animator)] = Animator;
            Owner.components[typeof(NavMeshAgent)] = Agent;
            Owner.components[typeof(EnemyAIMovementController)] = Movement;
            Movement.gameObject = Owner;
            Set(Movement, "agent", Agent); Set(Movement, "animator", Animator);
            typeof(EnemyAIMovementController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Movement, null);
            Set(Task, "m_ResolvedGameObject", Owner);
            typeof(Death).GetMethod("InitializeTarget", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Task, null);
            Task.OnStart();
        }
    }
    public static string Run() {
        int count = 0;
        var r = new Rig();
        Check(r.Task.OnUpdate() == TaskStatus.Failure && r.Animator.CrossFades == 0, "Living enemy must not play death or succeed"); count++;
        r.Health.IsDead = true;
        Check(r.Task.OnUpdate() == TaskStatus.Running, "Death must wait for animation");
        Check(r.Agent.isStopped && !r.Agent.hasPath, "Death must stop and clear navigation");
        Check(r.Animator.CrossFades == 1 && r.Animator.LastCrossFade == "Base Layer.Hit .Dead", "Death must enter configured state once");
        r.Animator.Current = new AnimatorStateInfo { Name = "Base Layer.Idle", normalizedTime = 3 };
        Check(r.Task.OnUpdate() == TaskStatus.Running && r.Animator.CrossFades == 1, "Previous animation progress must not finish death or restart it");
        r.Animator.Current = new AnimatorStateInfo { Name = "Base Layer.Hit .Dead", normalizedTime = 0.4f };
        Check(r.Task.OnUpdate() == TaskStatus.Running, "Incomplete death must keep running");
        r.Animator.Current = new AnimatorStateInfo { Name = "Base Layer.Hit .Dead", normalizedTime = 1.01f };
        Check(r.Task.OnUpdate() == TaskStatus.Success, "Full death must succeed"); count++;
        r.Task.OnEnd(); r.Task.OnStart();
        Check(r.Task.OnUpdate() == TaskStatus.Success && r.Animator.CrossFades == 1, "Re-entering task must not replay completed death"); count++;
        Check(!r.Movement.MoveTo(new Vector3(0, 0, 10), 1, true) && r.Movement.LastMoveFailed && r.Agent.isStopped, "Dead enemy cannot restart chase");
        Check(!r.Movement.BeginChaseStop() && !r.Movement.PlayRandomAttack() && !r.Movement.FaceTarget(new GameObject(), 5), "Dead enemy cannot play stop or attack or turn"); count++;
        r = new Rig(); r.Health.IsDead = true; r.Animator.StateExists = false;
        Check(r.Task.OnUpdate() == TaskStatus.Failure && r.Animator.CrossFades == 0 && r.Agent.isStopped, "Missing death state must fail safely and still stop movement"); count++;
        r = new Rig(); r.Health.IsDead = true; r.Animator.isActiveAndEnabled = false;
        Check(r.Task.OnUpdate() == TaskStatus.Failure, "Disabled animator must not wait indefinitely"); count++;
        r = new Rig(); r.Health.IsDead = true; r.Animator.Transition = true;
        r.Animator.Next = new AnimatorStateInfo { Name = "Base Layer.Hit .Dead" };
        Check(r.Task.OnUpdate() == TaskStatus.Running && r.Animator.CrossFades == 0, "Reentry during death transition must not restart it"); count++;
        return "PASS: " + count + " death task scenarios (standalone engine adapters; not Unity playback)";
    }
}
