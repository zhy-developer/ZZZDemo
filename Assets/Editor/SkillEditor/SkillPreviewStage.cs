using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using ZZZ.SkillAuthoring;

namespace ZZZ.SkillEditor
{
    /// <summary>独立 Scene 编辑阶段的生命周期与动画采样。原场景及角色资产不参与预览。</summary>
    internal sealed class SkillPreviewStage : PreviewSceneStage
    {
        private GameObject previewRoot;
        private Animator animator;
        private PlayableGraph graph;
        private AnimationClipPlayable playable;
        private AnimationClip currentClip;
        private Transform[] bones;
        private Vector3[] positions, scales;
        private Quaternion[] rotations;
        private readonly SkillPreviewPresentation presentation = new SkillPreviewPresentation();
        private double sampledSeconds;
        public Vector3 LogicPosition { get; private set; }
        public string Error { get; private set; }

        protected override GUIContent CreateHeaderContent() => new GUIContent("技能 Scene 预览");
        protected override bool OnOpenStage() => base.OnOpenStage();

        public bool Load(SkillDefinition skill)
        {
            Clear();
            Error = null;
            try
            {
                if (skill?.animation?.clip == null) throw new ArgumentException("请先绑定动画片段。");
                animator = SkillPreviewModel.Create(skill.previewPrefab, skill.animatorPath, scene, out previewRoot);
                if (skill.animation.clip.isHumanMotion && (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman))
                    throw new ArgumentException("Humanoid 动画需要有效的人形 Avatar。");
                animator.gameObject.SetActive(true);
                animator.Rebind();
                bones = animator.GetComponentsInChildren<Transform>(true);
                positions = new Vector3[bones.Length]; rotations = new Quaternion[bones.Length]; scales = new Vector3[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                { positions[i] = bones[i].localPosition; rotations[i] = bones[i].localRotation; scales[i] = bones[i].localScale; }
                currentClip = skill.animation.clip;
                graph = PlayableGraph.Create("SkillAuthoringPreview");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                playable = AnimationClipPlayable.Create(graph, currentClip);
                playable.SetApplyFootIK(false);
                playable.SetApplyPlayableIK(false);
                playable.SetSpeed(0);
                var output = AnimationPlayableOutput.Create(graph, "技能动画", animator);
                output.SetSourcePlayable(playable);
                graph.Play();
                return true;
            }
            catch (Exception exception) { Error = exception.Message; Clear(); return false; }
        }

        public void Sample(SkillDefinition skill, SkillFrameSampler sampler, double seconds, bool audio, bool reset)
        {
            if (animator == null || !graph.IsValid() || skill.animation.clip != currentClip) return;
            presentation.Sample(skill, animator.transform, seconds, time => SamplePose(skill, sampler, time), audio, reset);
            sampledSeconds = seconds;
            Error = presentation.Error;
        }

        public bool TryGetEffectBinding(SkillDefinition skill, SkillFrameSampler sampler, SkillPresentationCue cue,
            out Matrix4x4 matrix, out Quaternion rotation)
        {
            matrix = Matrix4x4.identity; rotation = Quaternion.identity;
            if (animator == null || !graph.IsValid() || cue == null) return false;
            // 脱离节点的特效使用触发瞬间的坐标系；只临时采样骨骼，不能重建粒子或触发音效。
            bool sampleTrigger = !cue.followBinding;
            try
            {
                if (sampleTrigger) SamplePose(skill, sampler, SkillFrameUtility.FrameSeconds(cue.frame));
                var binding = string.IsNullOrEmpty(cue.bindingPath) ? animator.transform : animator.transform.Find(cue.bindingPath);
                if (binding == null) return false;
                matrix = binding.localToWorldMatrix; rotation = binding.rotation;
                return !Mathf.Approximately(matrix.determinant, 0f);
            }
            finally { if (sampleTrigger) SamplePose(skill, sampler, sampledSeconds); }
        }

        private void SamplePose(SkillDefinition skill, SkillFrameSampler sampler, double seconds)
        {
            // 每次从基准局部姿势采样，倒拖时间轴不会累积上次采样的位移。
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                bones[i].localPosition = positions[i]; bones[i].localRotation = rotations[i]; bones[i].localScale = scales[i];
            }
            playable.SetTime(SkillFrameSampler.SampleAnimationAtTime(skill, seconds));
            graph.Evaluate(0);
            // Animator 只负责姿势；世界位移以烘焙整数数据为准，防止双重根位移。
            animator.transform.localPosition = Vector3.zero;
            animator.transform.localRotation = Quaternion.identity;
            double frameTime = seconds / SkillFrameUtility.SecondsPerFrame;
            int frame = Mathf.Clamp((int)Math.Floor(frameTime), 0, skill.totalFrames);
            Vector2 displacement = Vector2.Lerp(sampler.SampleLocalPosition(skill, frame),
                sampler.SampleLocalPosition(skill, Math.Min(frame + 1, skill.totalFrames)), (float)(frameTime - frame));
            float precision = SkillFrameUtility.PositionPrecision;
            LogicPosition = new Vector3(displacement.x / precision, 0, displacement.y / precision);
            previewRoot.transform.position = LogicPosition;
        }

        public void FrameScene()
        {
            if (previewRoot == null) return;
            var bounds = new Bounds(LogicPosition + Vector3.up, Vector3.one * 3);
            foreach (var renderer in previewRoot.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            var view = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView : EditorWindow.GetWindow<SceneView>();
            view.Frame(bounds, false);
        }

        private void Clear()
        {
            presentation.Dispose();
            if (graph.IsValid()) graph.Destroy();
            if (previewRoot != null) DestroyImmediate(previewRoot);
            previewRoot = null; animator = null; currentClip = null; bones = null;
            LogicPosition = Vector3.zero;
        }

        public void ReleaseResources() => Clear();
        public void StopAudio() => presentation.StopAudio();

        protected override void OnCloseStage()
        {
            Clear();
            base.OnCloseStage();
        }
    }
}
