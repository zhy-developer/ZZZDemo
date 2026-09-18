using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using Opsive.GraphDesigner.Runtime.Variables;
using Opsive.BehaviorDesigner.Runtime.Tasks;

public static class EnemyHitRegression
{
    static void Set(object owner, string name, object value) {
        for (var type = owner.GetType(); type != null; type = type.BaseType) {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null) { field.SetValue(owner, value); return; }
        }
        throw new Exception("Missing field: " + name);
    }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    class Rig
    {
        public EnemyAIMovementController Movement = new EnemyAIMovementController();
        public Animator Animator = new Animator();
        public NavMeshAgent Agent = new NavMeshAgent();
        public CharacterHealthBase Health = new CharacterHealthBase();
        public ChaseWithMovement Chase = new ChaseWithMovement();
        public RandomAttack Attack = new RandomAttack();
        public FaceTarget Face = new FaceTarget();
        public GameObject Target = new GameObject();
        public Rig() {
            Time.time = 10f; Time.frameCount = 100;
            Movement.gameObject.components[typeof(CharacterHealthBase)] = Health;
            Set(Movement, "animator", Animator); Set(Movement, "agent", Agent);
            Set(Movement, "attackStates", new[] { new EnemyAIMovementController.AttackState {
                stateName = "Base Layer.Attack.Attack_normal_1", layer = 0, crossFadeDuration = 0.1f
            }});
            typeof(EnemyAIMovementController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Movement, null);
            Set(Chase, "movementController", Movement);
            Set(Chase, "m_Target", new SharedVariable<GameObject> { Value = Target });
            Set(Attack, "movementController", Movement);
            Set(Face, "movementController", Movement);
            Set(Face, "m_Target", new SharedVariable<GameObject> { Value = Target });
            Idle(); Chase.OnStart(); Attack.OnStart(); Face.OnStart();
        }
        public void Idle() {
            Animator.Current = new AnimatorStateInfo { Name = "Base Layer.Idle", Tag = "Idle" };
            Animator.Transition = false;
        }
        public void Hit(string tag = "Hit", bool transition = false) {
            var state = new AnimatorStateInfo { Name = "Reaction", Tag = tag, normalizedTime = 0.2f };
            if (transition) { Animator.Next = state; Animator.Transition = true; }
            else { Animator.Current = state; Animator.Transition = false; }
        }
    }
    public static string Run() {
        var failures = new List<string>(); int count = 0;
        Action<string, Action> test = (name, body) => {
            try { body(); count++; } catch (Exception ex) { failures.Add(name + ": " + ex.Message); }
        };
        test("Hit interrupts incomplete stop and permits a fresh chase", () => {
            var r = new Rig();
            Check(r.Chase.OnUpdate() == TaskStatus.Running, "Arrival should start stop");
            r.Animator.Current = new AnimatorStateInfo { Name = "Base Layer.Movement.急停.Run_End", normalizedTime = 0.4f };
            r.Chase.OnUpdate(); r.Hit();
            Check(r.Chase.OnUpdate() == TaskStatus.Failure, "Interrupted stop must exit rather than wait forever");
            r.Chase.OnEnd();
            Check(r.Animator.applyRootMotion, "Aborted stop must restore root motion");
            r.Idle(); r.Target.transform.position = new Vector3(0, 0, 10); r.Chase.OnStart();
            Check(r.Chase.OnUpdate() == TaskStatus.Running && !r.Agent.isStopped, "Recovery must allow navigation again");
        });
        foreach (var tag in new[] { "Hit", "Parry" }) {
            var reactionTag = tag;
            test(reactionTag + " interrupts attack before animation entry", () => {
                var r = new Rig();
                Check(r.Attack.OnUpdate() == TaskStatus.Running, "Attack should start");
                r.Hit(reactionTag, true);
                Check(r.Attack.OnUpdate() == TaskStatus.Failure, "Pending reaction must abort attack");
                r.Attack.OnEnd(); r.Attack.OnStart();
                Check(r.Attack.OnUpdate() == TaskStatus.Failure && r.Animator.CrossFades == 1, "Reentry must not overwrite reaction");
                r.Idle(); r.Attack.OnStart();
                Check(r.Attack.OnUpdate() == TaskStatus.Running && r.Animator.CrossFades == 2, "Recovery must permit a new attack without cooldown from the aborted one");
            });
        }
        test("Hit blocks navigation, stopping, attacks and facing", () => {
            var r = new Rig(); r.Hit(); r.Target.transform.position = new Vector3(0, 0, 10);
            Check(r.Chase.OnUpdate() == TaskStatus.Failure, "Chase should yield to hit");
            Check(!r.Movement.MoveTo(r.Target, 1f, true) && r.Agent.isStopped, "Navigation must remain stopped");
            Check(!r.Movement.BeginChaseStop() && !r.Movement.PlayRandomAttack(), "AI must not replace hit animation");
            Check(r.Face.OnUpdate() == TaskStatus.Failure, "Facing should yield to hit");
            Check(r.Animator.CrossFades == 0, "No AI animation may be started during hit");
        });
        test("Normal attack completion and cooldown still work", () => {
            var r = new Rig();
            Check(r.Attack.OnUpdate() == TaskStatus.Running, "Must wait for entry");
            r.Animator.Current = new AnimatorStateInfo { Name = "Base Layer.Attack.Attack_normal_1", normalizedTime = 0.5f };
            Check(r.Attack.OnUpdate() == TaskStatus.Running, "Must wait for completion");
            r.Animator.Current = new AnimatorStateInfo { Name = "Base Layer.Attack.Attack_normal_1", normalizedTime = 0.96f };
            Check(r.Attack.OnUpdate() == TaskStatus.Success, "Completed attack must succeed");
            r.Attack.OnEnd(); r.Attack.OnStart();
            Check(r.Attack.OnUpdate() == TaskStatus.Failure, "Completed attack must apply cooldown");
        });
        test("Death and disabled Animator release running tasks", () => {
            var r = new Rig(); r.Attack.OnUpdate(); r.Health.IsDead = true;
            Check(r.Attack.OnUpdate() == TaskStatus.Failure, "Dead attacker must exit");
            Check(r.Face.OnUpdate() == TaskStatus.Failure, "Dead facing task must exit");
            r = new Rig(); r.Attack.OnUpdate(); r.Animator.isActiveAndEnabled = false;
            Check(r.Attack.OnUpdate() == TaskStatus.Failure, "Disabled animator must exit attack");
        });
        test("Hit notification blocks AI before Animator evaluates the crossfade", () => {
            var r = new Rig(); r.Chase.OnUpdate();
            Check(!r.Animator.applyRootMotion, "Stop should temporarily disable root motion");
            r.Movement.NotifyHitReaction();
            Check(r.Animator.applyRootMotion && r.Agent.isStopped, "Hit must immediately restore root motion and stop navigation");
            Check(r.Chase.OnUpdate() == TaskStatus.Failure, "Notify must interrupt stop in the same frame");
            r.Attack.OnStart();
            Check(r.Attack.OnUpdate() == TaskStatus.Failure, "A new action cannot overwrite a hit before Animator evaluates it");
            Time.frameCount++; r.Hit();
            Check(r.Attack.OnUpdate() == TaskStatus.Failure, "Hit must remain protected on later frames");
        });
        test("A missed hit still invalidates the old stop and attack", () => {
            var r = new Rig(); r.Chase.OnUpdate(); r.Movement.NotifyHitReaction();
            Time.frameCount += 30; r.Idle();
            Check(r.Chase.OnUpdate() == TaskStatus.Failure, "Old stop must exit even if tree never observed the hit");
            r.Chase.OnEnd(); r.Target.transform.position = new Vector3(0, 0, 10); r.Chase.OnStart();
            Check(r.Chase.OnUpdate() == TaskStatus.Running && !r.Agent.isStopped, "Fresh chase may resume after a missed hit");
            r = new Rig(); r.Attack.OnUpdate(); r.Movement.NotifyHitReaction();
            Time.frameCount += 30; r.Idle();
            Check(r.Attack.OnUpdate() == TaskStatus.Failure, "Old attack must exit even if tree never observed the hit");
            r.Attack.OnEnd(); r.Attack.OnStart();
            Check(r.Attack.OnUpdate() == TaskStatus.Running, "Fresh attack may resume after a missed hit");
        });
        test("Consecutive hits and the outgoing blend remain protected", () => {
            var r = new Rig(); r.Movement.NotifyHitReaction(); Time.frameCount++; r.Hit();
            r.Movement.NotifyHitReaction(); Time.frameCount++;
            r.Animator.Next = new AnimatorStateInfo { Name = "Base Layer.Idle", Tag = "Idle" };
            r.Animator.Transition = true;
            r.Attack.OnStart();
            Check(r.Attack.OnUpdate() == TaskStatus.Failure, "Outgoing hit blend still owns the Animator");
            r.Idle(); r.Attack.OnStart();
            Check(r.Attack.OnUpdate() == TaskStatus.Running, "Finished consecutive hits must release AI");
        });
        if (failures.Count > 0) throw new Exception(string.Join("\n", failures));
        return "PASS: " + count + " hit interruption scenarios (standalone engine adapters; not Unity playback)";
    }
}
