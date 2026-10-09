using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace SkillConfig.Editor
{
    public sealed class SkillAnimationPreview : IDisposable
    {
        readonly SkillVisualProxy proxy;
        readonly Dictionary<AnimationClip, AnimationClip> clips = new Dictionary<AnimationClip, AnimationClip>();
        PlayableGraph graph;
        public string ClipName { get; private set; }
        public double SourceSeconds { get; private set; }
        public SkillAnimationPreview(SkillVisualProxy proxy) { this.proxy = proxy; }
        public static double MapTime(AnimationSegment segment, int frame)
        {
            if (segment == null || !segment.clip || segment.clip.legacy) throw new ArgumentException("A non-legacy AnimationClip is required.");
            long sourceLength = (long)Math.Round(segment.clip.length * 1000000.0);
            if (segment.speedPermille <= 0 || segment.speedPermille > 100000 || segment.trimStartUs < 0 || segment.trimEndUs <= segment.trimStartUs || segment.trimEndUs > sourceLength || segment.endFrame <= segment.startFrame)
                throw new ArgumentException("Invalid animation trim, speed or frame interval.");
            // Decimal avoids overflow even on malformed large frame input; the saved unit remains microseconds.
            decimal us = segment.trimStartUs + (decimal)Math.Max(0L, (long)frame - segment.startFrame) * 33000 * segment.speedPermille / 1000;
            return (double)Math.Min(segment.trimEndUs, us) / 1000000.0;
        }
        public void Sample(AnimationSegment segment, int frame)
        {
            double time = MapTime(segment, frame);
            if (segment.clip.isHumanMotion && (!proxy.Animator.avatar || !proxy.Animator.avatar.isValid || !proxy.Animator.avatar.isHuman))
                throw new ArgumentException("Humanoid animation requires a valid matching visual Avatar.");
            if (!clips.TryGetValue(segment.clip, out var clip))
            {
                clip = Object.Instantiate(segment.clip); clip.name = "SkillPreview::" + segment.clip.name; clip.hideFlags = HideFlags.HideAndDontSave;
                clips.Add(segment.clip, clip);
                AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)) AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    bool safe = binding.type == typeof(Transform) || (binding.type == typeof(Animator) && !binding.propertyName.StartsWith("m_", StringComparison.Ordinal)) || (binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal));
                    if (!safe) AnimationUtility.SetEditorCurve(clip, binding, null);
                }
            }
            // A fresh manual graph + rest pose makes random seek independent of prior clip/time.
            StopGraph(); proxy.RestoreRest(); proxy.Animator.Rebind(); proxy.RestoreRest();
            proxy.Animator.runtimeAnimatorController = null; proxy.Animator.applyRootMotion = false; proxy.Animator.fireEvents = false;
            try
            {
                graph = PlayableGraph.Create("SkillPreview::Animation"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false); playable.SetSpeed(0);
                var output = AnimationPlayableOutput.Create(graph, "Visual pose only", proxy.Animator); output.SetSourcePlayable(playable);
                playable.SetTime(time); graph.Play(); graph.Evaluate(0);
                proxy.RestoreAnimatorRoot(); // Only the integer table may move/rotate the logical preview root.
                SourceSeconds = time; ClipName = segment.clip.name;
            }
            catch { StopGraph(); throw; }
        }
        public void StopGraph() { if (graph.IsValid()) graph.Destroy(); }
        public void Dispose()
        {
            StopGraph(); foreach (var clip in clips.Values) if (clip) Object.DestroyImmediate(clip); clips.Clear();
        }
    }
}
