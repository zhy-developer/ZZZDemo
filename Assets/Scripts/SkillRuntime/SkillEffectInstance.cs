using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZZZ.SkillAuthoring;

namespace ZZZ.SkillRuntime
{
    /// <summary>单次粒子实例。运行时由 Unity 连续播放；编辑预览使用 Simulate，二者都不读取战斗帧。</summary>
    public sealed class SkillEffectInstance : IDisposable
    {
        private GameObject root;
        private readonly List<ParticleSystem> roots = new List<ParticleSystem>();
        private readonly bool preview;
        public bool IsAlive
        {
            get
            {
                if (root == null) return false;
                foreach (var system in roots) if (system != null && system.IsAlive(true)) return true;
                return false;
            }
        }

        public static Transform Binding(Transform animatorRoot, string path)
            => animatorRoot == null ? null : string.IsNullOrEmpty(path) ? animatorRoot : animatorRoot.Find(path);

        public SkillEffectInstance(SkillPresentationCue cue, Transform binding, bool preview, uint seed)
            : this(cue, binding, binding != null ? binding.gameObject.scene
                : throw new ArgumentException("表现事件绑定节点不存在：" + cue.bindingPath),
                Vector3.zero, Quaternion.identity, preview, seed) { }

        /// <summary>命中反馈使用世界坐标入口，不创建临时绑定节点，也不依赖攻击者后续状态。</summary>
        public SkillEffectInstance(SkillPresentationCue cue, Scene scene, Vector3 position, Quaternion rotation, bool preview, uint seed)
            : this(cue, null, scene, position, rotation, preview, seed) { }

        private SkillEffectInstance(SkillPresentationCue cue, Transform binding, Scene scene,
            Vector3 position, Quaternion rotation, bool preview, uint seed)
        {
            this.preview = preview;
            string reason = SkillValidation.EffectPrefabIssue(cue.effectPrefab);
            if (reason != null) throw new ArgumentException(reason);
            try
            {
                root = new GameObject("技能特效_" + cue.effectPrefab.name) { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(root, scene);
                root.SetActive(false);
                if (binding != null)
                {
                    root.transform.SetParent(binding, false);
                    root.transform.localPosition = cue.localOffset;
                    root.transform.localRotation = Quaternion.Euler(cue.localEulerAngles);
                }
                else
                    root.transform.SetPositionAndRotation(position + rotation * cue.localOffset,
                        rotation * Quaternion.Euler(cue.localEulerAngles));
                // 缩放放在事件包装节点，与绑定节点和 Prefab 自身缩放相乘；脱离绑定前保留完整世界变换。
                root.transform.localScale = cue.localScale;
                if (binding != null && !cue.followBinding) root.transform.SetParent(null, true);
                var effect = UnityEngine.Object.Instantiate(cue.effectPrefab, root.transform, false);
                effect.transform.localPosition = Vector3.zero;
                effect.SetActive(true);
                var systems = effect.GetComponentsInChildren<ParticleSystem>(true);
                for (int i = 0; i < systems.Length; i++)
                {
                    var system = systems[i];
                    var main = system.main;
                    main.playOnAwake = false; main.stopAction = ParticleSystemStopAction.None;
                    main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                    main.useUnscaledTime = false;
                    // 预览 Simulate 与运行时自动模拟共用粒子速度，只乘一次；不改变生命周期计时。
                    // 保留每个子粒子在 Prefab 上的相对速度，不修改源资源。
                    main.simulationSpeed *= cue.playbackSpeed;
                    // 子粒子也继承事件缩放；只调整临时实例，预览和战斗统一，不回写源 Prefab。
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    var collision = system.collision; collision.enabled = false;
                    var trigger = system.trigger; trigger.enabled = false;
                    system.useAutoRandomSeed = false; system.randomSeed = seed + (uint)i + 1;
                    bool hasParent = false;
                    for (var parent = system.transform.parent; parent != root.transform; parent = parent.parent)
                        if (parent.GetComponent<ParticleSystem>() != null) { hasParent = true; break; }
                    if (!hasParent) roots.Add(system);
                }
                root.SetActive(true);
                foreach (var system in roots)
                {
                    system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    if (preview) system.Simulate(0, true, true, false);
                    else system.Play(true);
                }
            }
            catch { Dispose(); throw; }
        }

        public void Simulate(float delta)
        {
            foreach (var system in roots) if (system != null) system.Simulate(delta, true, false, false);
        }

        public void Dispose()
        {
            if (root == null) return;
            root.SetActive(false);
            if (preview) UnityEngine.Object.DestroyImmediate(root); else UnityEngine.Object.Destroy(root);
            root = null;
        }
    }
}
