using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using ZZZ.SkillAuthoring;

namespace ZZZ.SkillRuntime
{
    /// <summary>逻辑层只通知状态和时间锚点；动画、移动参数阻尼和混合统一由渲染帧推进。</summary>
    public sealed class CombatAnimationView : IDisposable
    {
        private readonly Animator animator;
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimatorControllerPlayable controller;
        private AnimationClipPlayable clipPlayable;

        private CombatLocomotionView locomotion;
        private AnimationClip currentClip, reactionClip;
        private SkillDefinition skill;
        private readonly CombatPresentationClock clock = new CombatPresentationClock();
        private readonly Transform[] bones;
        private readonly Vector3[] basePositions, baseScales, posePositions, poseScales;
        private readonly Quaternion[] baseRotations, poseRotations;
        private readonly bool originalFireEvents, originalRootMotion;
        private readonly AnimatorCullingMode originalCulling;
        private readonly RuntimeAnimatorController originalController;
        private CombatState state, renderedState;
        private int cast, reaction, renderedCast, renderedReaction;
        private bool moving, frozen, hasState, hasPose, disposed;
        private bool reactionAtEnd;
        private float clipWeight, blendStart, blendTarget, blendDuration, blendElapsed;

        public CombatAnimationView(Player player)
        {
            animator = player.characterAnimator;
            if (animator == null || !animator.hasTransformHierarchy) {
                throw new ArgumentException("帧战斗动画需要 Animator 和可访问的骨骼层级。");
            }

            originalController = animator.runtimeAnimatorController;
            if (originalController == null) {
                throw new ArgumentException("角色 Animator 需要绑定原移动动画控制器。");
            } 

            originalFireEvents = animator.fireEvents;
            originalRootMotion = animator.applyRootMotion;
            originalCulling = animator.cullingMode;
            try
            {
                // 同一 Graph 托管旧 Controller 和技能片段；只在 Render 中求值，避免双重推进。
                animator.runtimeAnimatorController = null;
                animator.fireEvents = false;
                animator.applyRootMotion = true;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                bones = animator.GetComponentsInChildren<Transform>(true);
                basePositions = new Vector3[bones.Length]; baseScales = new Vector3[bones.Length];
                baseRotations = new Quaternion[bones.Length];
                posePositions = new Vector3[bones.Length]; poseScales = new Vector3[bones.Length];
                poseRotations = new Quaternion[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                {
                    basePositions[i] = bones[i].localPosition; baseRotations[i] = bones[i].localRotation;
                    baseScales[i] = bones[i].localScale;
                }

                graph = PlayableGraph.Create("FrameCombat_" + animator.name);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                controller = AnimatorControllerPlayable.Create(graph, originalController);
                mixer = AnimationMixerPlayable.Create(graph, 2);
                graph.Connect(controller, 0, mixer, 0);
                mixer.SetInputWeight(0, 1);
                var output = AnimationPlayableOutput.Create(graph, "移动 FSM / 技能时间轴", animator);
                output.SetSourcePlayable(mixer);
                output.SetWeight(1);
                graph.Play();
                locomotion = new CombatLocomotionView(player.movementStateMachine, controller);
            }
            catch { Dispose(); throw; }
        }

        /// <summary>接收已确认状态，不求值动画、不累加播放时间、不重设同一动作的播放游标。</summary>
        public void SyncState(CombatFighter fighter, bool matchEnded = false)
        {
            if (disposed) return;
            state = fighter.State; cast = fighter.CastSerial; reaction = fighter.ReactionSerial;
            moving = fighter.Moving;
            skill = fighter.Skill?.Data;
            reactionClip = fighter.Library.Profile.hitClip;
            // 顿帧暂时停用，命中后连续播放；仅死亡或对局结束保持最终显示。
            frozen = matchEnded || state == CombatState.Dead;
            // 只有技能需要对齐制作时间轴。受击片段不读取 PoseFrame，也不做网络帧调速校准。
            if (state == CombatState.Skill)
                clock.Observe(SkillFrameUtility.FrameSeconds(fighter.SkillFrame), Time.time);
            hasState = true;
        }

        /// <summary>每个渲染帧调用一次。状态切换听从逻辑，连续播放和混合使用 deltaTime。</summary>
        public void Render(float deltaTime)
        {
            if (disposed || !hasState) return;
            bool changed = !hasPose || state != renderedState || cast != renderedCast || reaction != renderedReaction;
            if (frozen && hasPose && !changed)
            {
                ApplyPose(posePositions, poseRotations, poseScales);
                return;
            }
            // 新状态先显示其起始/校准姿势，不把本渲染帧之前消耗的时间计入新动作。
            float delta = !frozen && hasPose && !changed ? Mathf.Max(0, deltaTime) : 0;
            if (state == CombatState.Skill)
            {
                if (changed) clock.Restart();
                else if (!frozen) clock.Tick(delta, Time.time);
            }
            bool free = state == CombatState.Free;
            if (free)
            {
                // 恢复移动时保留退出动作的末次姿势参与淡出，不能在后台继续推进它。
                if (clipPlayable.IsValid()) clipPlayable.SetSpeed(0);
                locomotion.Step(moving, changed, delta);
                if (changed) StartBlend(0, hasPose ? .14f : 0);
            }
            else
            {
                bool isSkill = state == CombatState.Skill;
                BindClip(isSkill ? skill.animation.clip : reactionClip);
                if (isSkill)
                {
                    clipPlayable.SetSpeed(0);
                    clipPlayable.SetDone(false);
                    clipPlayable.SetTime(SkillFrameSampler.SampleAnimationAtTime(skill, clock.Seconds));
                }
                else
                {
                    // 每次新命中只重启一次；之后由 Graph.Evaluate(renderDelta) 以原速播放。
                    // 不在每个逻辑帧调速，也不在每个渲染帧重新 SetTime。
                    if (changed)
                    {
                        reactionAtEnd = false;
                        clipPlayable.SetDone(false);
                        clipPlayable.SetTime(0);
                        clipPlayable.SetSpeed(1);
                    }
                    double reactionEnd = Math.Max(0, currentClip.length - 0.000001);
                    if (!reactionAtEnd && clipPlayable.GetTime() + delta >= reactionEnd)
                    {
                        // 即使源片段开启循环，受击也只播一遍；末姿势保持到逻辑解除硬直。
                        clipPlayable.SetTime(reactionEnd);
                        clipPlayable.SetSpeed(0);
                        reactionAtEnd = true;
                    }
                }
                if (changed)
                {
                    float duration = isSkill && hasPose && renderedState == CombatState.Free
                        ? skill.animation.blendInSeconds : 0;
                    StartBlend(1, duration);
                }
            }
            blendElapsed += delta;
            clipWeight = blendDuration <= 0 ? blendTarget
                : Mathf.Lerp(blendStart, blendTarget, Mathf.Clamp01(blendElapsed / blendDuration));
            mixer.SetInputWeight(0, 1 - clipWeight);
            mixer.SetInputWeight(1, clipWeight);
            controller.SetSpeed(free ? 1 : 0);
            ApplyPose(basePositions, baseRotations, baseScales);
            graph.Evaluate(delta);
            // Graph 只输出骨骼动作；世界位置仍由 RoleBase 的显示平滑管理。
            animator.transform.localPosition = basePositions[0];
            animator.transform.localRotation = baseRotations[0];
            animator.transform.localScale = baseScales[0];
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                posePositions[i] = bones[i].localPosition; poseRotations[i] = bones[i].localRotation;
                poseScales[i] = bones[i].localScale;
            }
            hasPose = true; renderedState = state; renderedCast = cast; renderedReaction = reaction;
        }

        private void StartBlend(float target, float duration)
        {
            blendStart = clipWeight; blendTarget = target;
            blendDuration = Mathf.Max(0, duration); blendElapsed = 0;
        }

        private void BindClip(AnimationClip clip)
        {
            if (clip == currentClip) return;
            if (clip.isHumanMotion && (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman))
                throw new ArgumentException("Humanoid 动作需要有效的人形 Avatar：" + clip.name);
            if (clipPlayable.IsValid())
            {
                graph.Disconnect(mixer, 1);
                graph.DestroyPlayable(clipPlayable);
            }
            clipPlayable = AnimationClipPlayable.Create(graph, clip);
            clipPlayable.SetApplyFootIK(false); clipPlayable.SetApplyPlayableIK(false); clipPlayable.SetSpeed(0);
            graph.Connect(clipPlayable, 0, mixer, 1);
            currentClip = clip;
        }

        private void ApplyPose(Vector3[] positions, Quaternion[] rotations, Vector3[] scales)
        {
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                bones[i].localPosition = positions[i]; bones[i].localRotation = rotations[i]; bones[i].localScale = scales[i];
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            locomotion?.Dispose();
            if (graph.IsValid()) graph.Destroy();
            if (animator == null) return;
            animator.runtimeAnimatorController = originalController;
            animator.fireEvents = originalFireEvents; animator.applyRootMotion = originalRootMotion;
            animator.cullingMode = originalCulling;
        }
    }
}
