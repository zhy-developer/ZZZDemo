using System;
using UnityEngine;
using UnityEngine.Animations;

namespace ZZZ.SkillRuntime
{
    /// <summary>复用旧移动 FSM 的动画状态，通过 ControllerPlayable 输出；不访问战斗写入接口。</summary>
    public sealed class CombatLocomotionView : IMovementAnimationOutput, IDisposable
    {
        private readonly PlayerMovementStateMachine movement;
        private AnimatorControllerPlayable controller;

        public CombatLocomotionView(PlayerMovementStateMachine movement, AnimatorControllerPlayable controller)
        {
            this.movement = movement;
            this.controller = controller;
            if (!controller.HasState(0, Animator.StringToHash("Idle"))
                || !controller.HasState(0, Animator.StringToHash("WalkStart")))
                throw new ArgumentException("移动 Animator Controller 需要 Idle 和 WalkStart 状态。");
            bool hasInput = false, hasMovement = false;
            for (int i = 0; i < controller.GetParameterCount(); i++)
            {
                var parameter = controller.GetParameter(i);
                hasInput |= parameter.nameHash == AnimatorID.HasInputID && parameter.type == AnimatorControllerParameterType.Bool;
                hasMovement |= parameter.nameHash == AnimatorID.MovementID && parameter.type == AnimatorControllerParameterType.Float;
            }
            if (!hasInput || !hasMovement)
                throw new ArgumentException("移动 Animator Controller 需要 HasInput(bool)、Movement(float) 参数。");
            // 跳过原控制器的出场默认状态；开局只显示待机，不执行旧切人流程。
            controller.Play("Idle", 0, 0);
            movement.BindPresentation(this);
        }

        public void Step(bool moving, bool reenter, float deltaTime) => movement.PresentMovement(moving, reenter, deltaTime);
        public void SetBool(int id, bool value) => controller.SetBool(id, value);
        public void SetFloat(int id, float value, float dampTime, float deltaTime)
        {
            // ControllerPlayable 没有 Animator 的阻尼重载；使用渲染帧 deltaTime 平滑表现参数。
            float amount = dampTime <= 0 ? 1 : 1 - Mathf.Exp(-deltaTime / dampTime);
            controller.SetFloat(id, Mathf.Lerp(controller.GetFloat(id), value, amount));
        }
        public void CrossFade(string state, float duration) => controller.CrossFadeInFixedTime(state, duration, 0);
        public bool IsPlaying(string tag)
        {
            var state = controller.IsInTransition(0) ? controller.GetNextAnimatorStateInfo(0) : controller.GetCurrentAnimatorStateInfo(0);
            return state.IsTag(tag) || state.IsName(tag);
        }
        public void Dispose() => movement.UnbindPresentation();
    }
}
