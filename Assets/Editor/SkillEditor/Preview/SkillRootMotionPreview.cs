using System;
using System.Collections.Generic;
using System.Linq;
using FrameSync.RootMotion;
using UnityEngine;

namespace SkillConfig.Editor
{
    public readonly struct SkillMotionPoint
    {
        public readonly long x, z;
        public SkillMotionPoint(long x, long z) { this.x = x; this.z = z; }
        public Vector3 World => new Vector3(x / 10000f, 0, z / 10000f);
    }
    public sealed class SkillRootMotionPreview
    {
        public bool Supported { get; private set; } = true;
        public readonly List<string> Diagnostics = new List<string>();
        readonly SkillMotionPoint[] positions;
        readonly int[] sampleEndMs;
        public int Duration => positions.Length - 1;
        public SkillRootMotionPreview(ComboData owner)
        {
            int duration = SkillTimelineAdapters.Duration(owner);
            if (duration < 1 || duration > 100000) throw new ArgumentException("Preview requires 1..100000 logical frames (memory safety limit).");
            positions = new SkillMotionPoint[duration + 1]; sampleEndMs = new int[duration];
            long x = 0, z = 0; int next = 0;
            foreach (var a in owner.skill.animations)
            {
                if (a == null || a.startFrame != next || a.endFrame <= next || a.endFrame > duration) throw new ArgumentException("Animation track must be contiguous from frame 0.");
                next = a.endFrame;
                if (a.rootSource == RootBindingSource.None) { Fill(a.startFrame, a.endFrame, x, z); continue; }
                try
                {
                    if (!a.clip) throw new ArgumentException("Missing animation Clip.");
                    long length = (long)Math.Round(a.clip.length * 1000000.0);
                    var animation = new AnimationDto { sourceLengthUs = length, trimStartUs = a.trimStartUs, trimEndUs = a.trimEndUs, speedPermille = a.speedPermille };
                    var issues = new List<Diagnostic>(); var binding = RootMotionBindingValidator.Build(owner, a, animation, issues, a.id);
                    if (binding == null || issues.Any(d => d.severity == DiagnosticSeverity.Error)) throw new ArgumentException(string.Join("; ", issues.Select(i => i.ToString())));
                    if (binding.requiresTimeRemap) throw new NotSupportedException("Trim/PlaybackSpeed requires a future exact time remapper; original displacement samples are NOT applied.");
                    if (!string.Equals(binding.clipName, a.clip.name, StringComparison.Ordinal) || binding.coverageEndMs * 1000L > length + 1000) throw new ArgumentException("Verified Root Motion clip/coverage mismatch.");
                    Diagnostics.Add("Warning MOTION_METADATA [" + a.id + "]: source GUID/bake provenance unavailable; replaying the legacy table is not proof of animation equivalence.");
                    if (binding.coverageEndMs * 1000L + 1000 < length) Diagnostics.Add("Static tail [" + a.id + "]: position holds at coverage end; policy=" + a.tailPolicy);
                    TextAsset json = a.rootSource == RootBindingSource.LegacyCombo ? owner.rootMotion.json : a.additionalRootMotion;
                    var data = JsonUtility.FromJson<RootMotionJsonData>(json.text);
                    if (!RootMotionClip.TryCreate(data, 33, 10000, out var clip, out var error)) throw new ArgumentException(error);
                    var player = new RootMotionPlayback();
                    if (!player.TryStart(clip, binding.endFrameExclusive, binding.distancePermille, 0, 10000, 10000)) throw new ArgumentException("Legacy playback rejected binding.");
                    for (int f = a.startFrame; f < a.endFrame; f++)
                    {
                        if (player.TryAdvance(out int dx, out int dz)) { x = checked(x + dx); z = checked(z + dz); sampleEndMs[f] = data.frames[f - a.startFrame].sampleEndTimeMs; }
                        positions[f + 1] = new SkillMotionPoint(x, z);
                    }
                }
                catch (Exception e)
                {
                    Supported = false; Diagnostics.Add((e is NotSupportedException ? "Unsupported mapping" : "Error") + " [" + a.id + "]: " + e.Message);
                }
            }
            if (!Supported) { Array.Clear(positions, 0, positions.Length); Array.Clear(sampleEndMs, 0, sampleEndMs.Length); }
        }
        void Fill(int start, int end, long x, long z) { for (int f = start; f < end; f++) positions[f + 1] = new SkillMotionPoint(x, z); }
        public SkillMotionPoint Position(int frame) => positions[Math.Max(0, Math.Min(Duration, frame))];
        public SkillMotionPoint Delta(int frame)
        {
            if (frame < 0 || frame >= Duration) return new SkillMotionPoint(0, 0);
            var a = positions[frame]; var b = positions[frame + 1]; return new SkillMotionPoint(b.x - a.x, b.z - a.z);
        }
        public int SampleEndMs(int frame) => frame >= 0 && frame < Duration ? sampleEndMs[frame] : 0;
    }
}
