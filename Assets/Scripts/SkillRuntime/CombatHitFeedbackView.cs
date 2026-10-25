using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZZZ.SkillAuthoring;

namespace ZZZ.SkillRuntime
{
    /// <summary>消费受击方的命中通知。只在渲染帧创建、播放和回收表现，不反向修改战斗状态。</summary>
    public sealed class CombatHitFeedbackView : IDisposable
    {
        private sealed class Active
        {
            public SkillEffectInstance Effect;
            public GameObject AudioObject;
            public AudioSource Audio;
            public float Age, EffectLifetime;
            public void Dispose()
            {
                Effect?.Dispose(); Effect = null;
                if (AudioObject != null)
                {
                    AudioObject.SetActive(false);
                    UnityEngine.Object.Destroy(AudioObject);
                }
                AudioObject = null; Audio = null;
            }
        }

        private readonly Transform targetRoot;
        private readonly List<CombatHitFeedbackEvent> pending = new List<CombatHitFeedbackEvent>();
        private readonly List<Active> active = new List<Active>();
        private readonly HashSet<string> reported = new HashSet<string>();
        private Vector2Int latestLogicalPosition;
        private int lastReceivedFrame = -1;
        private bool ended, disposed;

        public CombatHitFeedbackView(Transform targetRoot) { this.targetRoot = targetRoot; }

        public void SyncState(CombatFighter fighter, bool matchEnded)
        {
            if (disposed || ended) return;
            latestLogicalPosition = fighter.Position;
            int newestFrame = lastReceivedFrame;
            foreach (var notification in fighter.HitFeedbackEvents)
            {
                // 同帧可有多个命中组，全部收齐后才推进水位；重复 Sync 不重复播放。
                if (notification.Frame <= lastReceivedFrame) continue;
                pending.Add(notification);
                newestFrame = Math.Max(newestFrame, notification.Frame);
            }
            lastReceivedFrame = newestFrame;
            // 先保留致命一击，再关闭后续收取；已经触发的反馈继续按渲染时间播完。
            ended = matchEnded;
        }

        public void Render(float deltaTime)
        {
            if (disposed) return;
            if (targetRoot == null) { Dispose(); return; }
            float delta = Mathf.Max(0, deltaTime);
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var item = active[i]; item.Age += delta;
                if (item.Effect != null && (item.Age >= item.EffectLifetime || (item.Age > .05f && !item.Effect.IsAlive)))
                { item.Effect.Dispose(); item.Effect = null; }
                if (item.AudioObject != null && (item.Audio == null || (item.Age > .1f && !item.Audio.isPlaying)))
                {
                    item.AudioObject.SetActive(false);
                    UnityEngine.Object.Destroy(item.AudioObject);
                    item.AudioObject = null; item.Audio = null;
                }
                if (item.Effect == null && item.AudioObject == null)
                { item.Dispose(); active.RemoveAt(i); }
            }
            foreach (var notification in pending) Play(notification);
            pending.Clear();
        }

        private void Play(CombatHitFeedbackEvent notification)
        {
            var feedback = notification.Feedback;
            if (feedback == null || (feedback.effectPrefab == null && feedback.audioClip == null)) return;
            var item = new Active { EffectLifetime = feedback.maxLifetimeSeconds };
            // 以受击者当前显示根节点为基准，加回命中帧到最新逻辑帧的位置差。
            // 这样追帧时不会把所有历史命中堆在最新位置，也不会直接跳到尚未平滑到达的逻辑坐标。
            Vector2Int displacement = notification.TargetPosition - latestLogicalPosition;
            float precision = SkillFrameUtility.PositionPrecision;
            Vector3 position = targetRoot.position + new Vector3(displacement.x / precision, 0, displacement.y / precision);
            var forward = CombatMath.Direction(notification.AttackerFacing);
            Quaternion rotation = Quaternion.LookRotation(new Vector3(forward.x, 0, forward.y));

            if (feedback.effectPrefab != null)
            {
                try
                {
                    var cue = new SkillPresentationCue
                    {
                        kind = SkillPresentationKind.Vfx, effectPrefab = feedback.effectPrefab,
                        localOffset = feedback.localOffset, localEulerAngles = feedback.localEulerAngles,
                        localScale = feedback.localScale, maxLifetimeSeconds = feedback.maxLifetimeSeconds,
                        followBinding = false, stopOnInterrupt = false
                    };
                    uint seed;
                    unchecked
                    {
                        seed = (uint)notification.AttackerId * 73856093u ^ (uint)notification.DefenderId * 19349663u
                            ^ (uint)notification.CastSerial * 83492791u ^ (uint)notification.HitGroupId;
                    }
                    item.Effect = new SkillEffectInstance(cue, targetRoot.gameObject.scene, position, rotation, false, seed);
                }
                catch (Exception exception) { Report("特效", exception); }
            }
            // 两种资源分别处理；无效特效不能吞掉这次命中音效。
            if (feedback.audioClip != null)
            {
                try
                {
                    item.AudioObject = new GameObject((notification.IsParry ? "振刀音效_" : "命中音效_") + feedback.audioClip.name) { hideFlags = HideFlags.HideAndDontSave };
                    SceneManager.MoveGameObjectToScene(item.AudioObject, targetRoot.gameObject.scene);
                    item.AudioObject.transform.position = position;
                    item.Audio = item.AudioObject.AddComponent<AudioSource>();
                    item.Audio.playOnAwake = false; item.Audio.loop = false;
                    item.Audio.spatialBlend = 0; item.Audio.dopplerLevel = 0;
                    item.Audio.volume = feedback.volume; item.Audio.clip = feedback.audioClip;
                    item.Audio.Play();
                }
                catch (Exception exception)
                {
                    if (item.AudioObject != null) { item.AudioObject.SetActive(false); UnityEngine.Object.Destroy(item.AudioObject); }
                    item.AudioObject = null; item.Audio = null;
                    Report("音效", exception);
                }
            }
            if (item.Effect != null || item.AudioObject != null) active.Add(item);
            else item.Dispose();
        }

        private void Report(string kind, Exception exception)
        {
            string message = "命中" + kind + "跳过：" + exception.Message;
            if (reported.Add(message)) Debug.LogWarning(message, targetRoot);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var item in active) item.Dispose();
            active.Clear(); pending.Clear();
        }
    }
}
