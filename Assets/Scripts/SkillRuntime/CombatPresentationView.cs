using System;
using System.Collections.Generic;
using UnityEngine;
using ZZZ.SkillAuthoring;

namespace ZZZ.SkillRuntime
{
    /// <summary>表现事件消费者。通知先排队，在骨骼完成当帧更新后创建实例；播放与回收使用渲染时间。</summary>
    public sealed class CombatPresentationView : IDisposable
    {
        private sealed class Active
        {
            public int Cast;
            public SkillPresentationCue Cue;
            public SkillEffectInstance Effect;
            public GameObject AudioObject;
            public AudioSource Audio;
            public double Age;
            public void Dispose()
            {
                Effect?.Dispose();
                if (AudioObject != null) { AudioObject.SetActive(false); UnityEngine.Object.Destroy(AudioObject); }
            }
        }

        private readonly Transform animatorRoot;
        private readonly List<CombatPresentationEvent> pending = new List<CombatPresentationEvent>();
        private readonly List<Active> active = new List<Active>();
        private readonly HashSet<string> reported = new HashSet<string>();
        private bool ended;
        public CombatPresentationView(Transform animatorRoot) { this.animatorRoot = animatorRoot; }

        public void SyncState(CombatFighter fighter, bool matchEnded)
        {
            if (ended) return;
            pending.AddRange(fighter.PresentationEvents);
            // 对局结束统一清理；清理仍留到显示帧，不在逻辑调用链中销毁对象。
            ended = matchEnded;
        }

        public void Render(float deltaTime)
        {
            if (ended) { Clear(); return; }
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var item = active[i]; item.Age += Mathf.Max(0, deltaTime);
                bool finished = item.Effect != null
                    ? SkillEffectTiming.IsExpired(item.Cue, item.Age) || (item.Age > .05f && !item.Effect.IsAlive)
                    : item.Audio == null || (!item.Audio.isPlaying && item.Age > .1f);
                if (finished) { item.Dispose(); active.RemoveAt(i); }
            }
            for (int i = 0; i < pending.Count; i++)
            {
                var notification = pending[i];
                if (notification.IsEnd)
                {
                    // 结束帧等于 totalFrames 时，技能先结束，不再产生单独的区间结束通知。
                    StopCast(notification.CastSerial, notification.Interrupted);
                    continue;
                }
                if (notification.EffectEnded)
                {
                    StopEffect(notification.CastSerial, notification.Cue);
                    continue;
                }
                // 追赶多个逻辑帧时，若区间已过或随后被要求中断，不先闪一下/发声再清理。
                bool cancelled = false;
                for (int j = i + 1; j < pending.Count; j++)
                {
                    var later = pending[j];
                    if (later.CastSerial != notification.CastSerial) continue;
                    if ((later.EffectEnded && ReferenceEquals(later.Cue, notification.Cue))
                        || (later.IsEnd && (later.Interrupted ? notification.Cue.stopOnInterrupt
                            : SkillEffectTiming.UsesFrameRange(notification.Cue))))
                    { cancelled = true; break; }
                }
                if (!cancelled) Play(notification);
            }
            pending.Clear();
        }

        private void Play(CombatPresentationEvent notification)
        {
            var cue = notification.Cue;
            if (cue.kind == SkillPresentationKind.Vfx ? cue.effectPrefab == null : cue.audioClip == null) return;
            var item = new Active { Cast = notification.CastSerial, Cue = cue };
            try
            {
                var binding = SkillEffectInstance.Binding(animatorRoot, cue.bindingPath);
                if (binding == null) throw new ArgumentException("绑定节点不存在：" + cue.bindingPath);
                if (cue.kind == SkillPresentationKind.Vfx)
                    item.Effect = new SkillEffectInstance(cue, binding, false, (uint)notification.CastSerial);
                else
                {
                    item.AudioObject = new GameObject("技能音效_" + cue.kind) { hideFlags = HideFlags.HideAndDontSave };
                    item.AudioObject.transform.SetParent(binding, false);
                    item.AudioObject.transform.localPosition = cue.localOffset;
                    if (!cue.followBinding) item.AudioObject.transform.SetParent(null, true);
                    item.Audio = item.AudioObject.AddComponent<AudioSource>();
                    item.Audio.playOnAwake = false; item.Audio.loop = false;
                    item.Audio.spatialBlend = 0; item.Audio.volume = cue.volume;
                    item.Audio.clip = cue.audioClip; item.Audio.Play();
                }
                active.Add(item);
            }
            catch (Exception exception)
            {
                item.Dispose();
                if (reported.Add(exception.Message)) Debug.LogWarning("技能表现事件跳过：" + exception.Message, animatorRoot);
            }
        }

        private void StopEffect(int cast, SkillPresentationCue cue)
        {
            for (int i = active.Count - 1; i >= 0; i--)
                if (active[i].Cast == cast && ReferenceEquals(active[i].Cue, cue))
                { active[i].Dispose(); active.RemoveAt(i); }
        }

        private void StopCast(int cast, bool interrupted)
        {
            for (int i = active.Count - 1; i >= 0; i--)
                if (active[i].Cast == cast && (interrupted ? active[i].Cue.stopOnInterrupt
                    : SkillEffectTiming.UsesFrameRange(active[i].Cue)))
                { active[i].Dispose(); active.RemoveAt(i); }
        }
        private void Clear() { foreach (var item in active) item.Dispose(); active.Clear(); pending.Clear(); }
        public void Dispose() { Clear(); ended = true; }
    }
}
