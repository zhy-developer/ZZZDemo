using System;
using System.Collections.Generic;
using UnityEngine;
using ZZZ.SkillAuthoring;
using ZZZ.SkillRuntime;

namespace ZZZ.SkillEditor
{
    /// <summary>预览事件实例与重采样。倒拖/修改时重建，以小时间步重放绑定节点轨迹，保留世界空间粒子轨迹。</summary>
    internal sealed class SkillPreviewPresentation : IDisposable
    {
        private readonly Dictionary<int, SkillEffectInstance> effects = new Dictionary<int, SkillEffectInstance>();
        private readonly Dictionary<int, SkillPreviewAudio> sounds = new Dictionary<int, SkillPreviewAudio>();
        private readonly HashSet<int> emitted = new HashSet<int>();
        private readonly HashSet<int> audioStarted = new HashSet<int>();
        private double cursor = -1;
        public string Error { get; private set; }

        public void Sample(SkillDefinition skill, Transform animator, double time, Action<double> pose, bool audio, bool reset)
        {
            if (reset || time < cursor) Reset();
            if (!audio) StopAudio();
            if (cursor < 0)
            {
                // 区间特效可超过自然模式的 60 秒上限。从目标时刻仍可能存活的最早
                // 触发点重放，避免长区间被截掉，也避免无关的长时间空档从零模拟。
                cursor = ReplayStart(skill, time);
                pose(cursor); SpawnEffects(skill, animator, cursor);
            }
            while (cursor + .0000001 < time)
            {
                double next = Math.Min(time, cursor + 1.0 / 60);
                // 准确落在事件边界生成实例，再模拟它后面的时间，不让绑定姿势晚一帧。
                if (skill.presentation != null) foreach (var cue in skill.presentation)
                {
                    if (cue == null) continue;
                    double start = SkillFrameUtility.FrameSeconds(cue.frame);
                    if (start > cursor + .0000001 && start < next) next = start;
                    if (cue.kind == SkillPresentationKind.Vfx)
                    {
                        double end = start + SkillEffectTiming.LifetimeSeconds(cue);
                        if (end > cursor + .0000001 && end < next) next = end;
                    }
                }
                pose(next);
                foreach (var effect in effects.Values) effect.Simulate((float)(next - cursor));
                cursor = next;
                SpawnEffects(skill, animator, cursor);
                RemoveExpiredEffects(skill, cursor);
            }
            pose(time);
            if (audio) UpdateAudio(skill, animator, time);
        }

        private void SpawnEffects(SkillDefinition skill, Transform animator, double time)
        {
            if (skill.presentation == null) return;
            for (int i = 0; i < skill.presentation.Count; i++)
            {
                var cue = skill.presentation[i];
                if (cue == null || cue.kind != SkillPresentationKind.Vfx || emitted.Contains(i)) continue;
                double start = SkillFrameUtility.FrameSeconds(cue.frame);
                if (start > time + .0000001) continue;
                emitted.Add(i);
                if (cue.effectPrefab == null || SkillEffectTiming.IsExpired(cue, time - start)) continue;
                try
                {
                    effects.Add(i, new SkillEffectInstance(cue, SkillEffectInstance.Binding(animator, cue.bindingPath), true, (uint)i));
                }
                catch (Exception exception) { Error = "特效预览：" + exception.Message; }
            }
        }

        private void RemoveExpiredEffects(SkillDefinition skill, double time)
        {
            var remove = new List<int>();
            foreach (var pair in effects)
                if (SkillEffectTiming.IsExpired(skill.presentation[pair.Key],
                    time - SkillFrameUtility.FrameSeconds(skill.presentation[pair.Key].frame)))
                    remove.Add(pair.Key);
            foreach (int index in remove) { effects[index].Dispose(); effects.Remove(index); }
        }

        private static double ReplayStart(SkillDefinition skill, double time)
        {
            double start = Math.Max(0, time);
            if (skill.presentation == null) return start;
            foreach (var cue in skill.presentation)
            {
                if (cue == null || cue.kind != SkillPresentationKind.Vfx || cue.effectPrefab == null) continue;
                double trigger = SkillFrameUtility.FrameSeconds(cue.frame);
                if (trigger <= time && !SkillEffectTiming.IsExpired(cue, time - trigger)) start = Math.Min(start, trigger);
            }
            return start;
        }

        private void UpdateAudio(SkillDefinition skill, Transform animator, double time)
        {
            if (skill.presentation == null) return;
            for (int i = 0; i < skill.presentation.Count; i++)
            {
                var cue = skill.presentation[i];
                if (cue == null || cue.kind == SkillPresentationKind.Vfx || cue.audioClip == null) continue;
                double age = time - SkillFrameUtility.FrameSeconds(cue.frame);
                if (age < 0) continue;
                if (age >= cue.audioClip.length)
                {
                    if (sounds.TryGetValue(i, out var ended)) { ended.Dispose(); sounds.Remove(i); }
                    continue;
                }
                if (!audioStarted.Add(i)) continue;
                try
                {
                    var binding = SkillEffectInstance.Binding(animator, cue.bindingPath);
                    if (binding == null) throw new ArgumentException("绑定节点不存在：" + cue.bindingPath);
                    sounds.Add(i, new SkillPreviewAudio(cue, binding, age));
                }
                catch (Exception exception) { Error = "音效预览：" + exception.Message; }
            }
            // 保持编辑模式下的播放循环活跃，但不向音频图传入视觉采样的 deltaTime。
            if (sounds.Count > 0) UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        }

        public void StopAudio()
        { foreach (var sound in sounds.Values) sound.Dispose(); sounds.Clear(); audioStarted.Clear(); }
        public void Reset()
        {
            foreach (var effect in effects.Values) effect.Dispose(); effects.Clear(); emitted.Clear();
            StopAudio(); cursor = -1; Error = null;
        }
        public void Dispose() => Reset();
    }
}
