using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class EnemyAIMovementControllerTests
{
    [Test]
    public void MovementParametersClearStopGate()
    {
        var enemy = new GameObject("Enemy");
        try {
            var animator = enemy.AddComponent<Animator>();
            var controller = enemy.AddComponent<EnemyAIMovementController>();
            var animatorController = CreateControllerWithEnemyParameters();

            animator.runtimeAnimatorController = animatorController;
            SetPrivateField(controller, "animator", animator);
            InvokePrivateMethod(controller, "Awake");

            animator.SetBool("HasInputForStop", true);
            InvokePrivateMethod(controller, "SetMovementParameters", true, true, 1f);

            Assert.False(animator.GetBool("HasInputForStop"));
        }
        finally {
            Object.DestroyImmediate(enemy);
        }
    }

    [Test]
    public void MonsterAnimatorUsesExistingMovementParameterForBlendTrees()
    {
        var path = AssetDatabase.GUIDToAssetPath("ad2b501a0717b424c837463d3837fcc3");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        Assert.NotNull(controller);

        var parameterNames = controller.parameters.Select(parameter => parameter.name).ToHashSet();
        var blendTreeParameters = controller.layers
            .SelectMany(layer => CollectBlendTreeParameters(layer.stateMachine))
            .Distinct()
            .ToArray();

        Assert.That(blendTreeParameters, Does.Not.Contain("MoveMent"));
        foreach (var parameter in blendTreeParameters) {
            Assert.That(parameterNames, Does.Contain(parameter), $"{parameter} is used by a BlendTree but is not declared on the controller.");
        }
    }

    private static AnimatorController CreateControllerWithEnemyParameters()
    {
        var controller = new AnimatorController();
        controller.AddParameter("Movement", AnimatorControllerParameterType.Float);
        controller.AddParameter("HasInput", AnimatorControllerParameterType.Bool);
        controller.AddParameter("HasMoveInput", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Run", AnimatorControllerParameterType.Bool);
        controller.AddParameter("TurnDeltaAngle", AnimatorControllerParameterType.Float);
        controller.AddParameter("HasInputForStop", AnimatorControllerParameterType.Bool);
        return controller;
    }

    private static System.Collections.Generic.IEnumerable<string> CollectBlendTreeParameters(AnimatorStateMachine stateMachine)
    {
        foreach (var childState in stateMachine.states) {
            foreach (var parameter in CollectBlendTreeParameters(childState.state.motion)) {
                yield return parameter;
            }
        }

        foreach (var childStateMachine in stateMachine.stateMachines) {
            foreach (var parameter in CollectBlendTreeParameters(childStateMachine.stateMachine)) {
                yield return parameter;
            }
        }
    }

    private static System.Collections.Generic.IEnumerable<string> CollectBlendTreeParameters(Motion motion)
    {
        if (motion is not BlendTree blendTree) {
            yield break;
        }

        if (!string.IsNullOrEmpty(blendTree.blendParameter)) {
            yield return blendTree.blendParameter;
        }
        if (!string.IsNullOrEmpty(blendTree.blendParameterY)) {
            yield return blendTree.blendParameterY;
        }

        foreach (var child in blendTree.children) {
            foreach (var parameter in CollectBlendTreeParameters(child.motion)) {
                yield return parameter;
            }
        }
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field.SetValue(target, value);
    }

    private static void InvokePrivateMethod(object target, string methodName, params object[] parameters)
    {
        var method = target.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        method.Invoke(target, parameters);
    }
}
