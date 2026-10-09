using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SkillConfig.Editor
{
    public sealed class SkillVisualProxy : IDisposable
    {
        public GameObject Root { get; internal set; }
        public Animator Animator { get; internal set; }
        public readonly List<string> Warnings = new List<string>();
        internal Material fallback;
        Transform[] transforms;
        Vector3[] positions, scales;
        Quaternion[] rotations;
        SkinnedMeshRenderer[] skins;
        float[][] weights;
        internal void CaptureRest()
        {
            transforms = Root.GetComponentsInChildren<Transform>(true);
            positions = transforms.Select(t => t.localPosition).ToArray(); scales = transforms.Select(t => t.localScale).ToArray(); rotations = transforms.Select(t => t.localRotation).ToArray();
            skins = Root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            weights = skins.Select(s => Enumerable.Range(0, s.sharedMesh ? s.sharedMesh.blendShapeCount : 0).Select(s.GetBlendShapeWeight).ToArray()).ToArray();
        }
        public void RestoreRest()
        {
            for (int i = 0; i < transforms.Length; i++) { transforms[i].localPosition = positions[i]; transforms[i].localScale = scales[i]; transforms[i].localRotation = rotations[i]; }
            for (int i = 0; i < skins.Length; i++) for (int j = 0; j < weights[i].Length; j++) skins[i].SetBlendShapeWeight(j, weights[i][j]);
        }
        public void RestoreAnimatorRoot()
        {
            int i = Array.IndexOf(transforms, Animator.transform);
            Animator.transform.localPosition = positions[i]; Animator.transform.localRotation = rotations[i]; Animator.transform.localScale = scales[i];
        }
        public void Dispose() { if (Root) Object.DestroyImmediate(Root); if (fallback) Object.DestroyImmediate(fallback); }
    }

    public static class SkillVisualProxyBuilder
    {
        public static GameObject Empty(string name, Scene scene)
        {
            var go = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave);
            SceneManager.MoveGameObjectToScene(go, scene); return go;
        }
        public static SkillVisualProxy Build(GameObject source, Scene scene)
        {
            if (!source || !EditorUtility.IsPersistent(source)) throw new ArgumentException("Choose a persistent model/Prefab asset, never a live player.");
            var animators = source.GetComponentsInChildren<Animator>(true);
            if (animators.Length > 1) throw new ArgumentException("Multiple Animator roots: select a single visual model asset.");
            var proxy = new SkillVisualProxy();
            try
            {
                proxy.Root = Empty("SkillPreview::VisualRoot", scene);
                var map = new Dictionary<Transform, Transform>();
                foreach (var t in source.GetComponentsInChildren<Transform>(true))
                {
                    var go = Empty(t.name, scene); var copy = go.transform;
                    copy.SetParent(t == source.transform ? proxy.Root.transform : map[t.parent], false);
                    copy.localPosition = t.localPosition; copy.localRotation = t.localRotation; copy.localScale = t.localScale;
                    go.SetActive(t == source.transform || t.gameObject.activeSelf); map.Add(t, copy);
                }
                // Construct only explicitly allowed native components. No Instantiate(GameObject), AddComponent(Type),
                // CopySerialized(component), prefab contents load, player scripts, colliders, constraints or pools.
                foreach (var sourceRenderer in source.GetComponentsInChildren<Renderer>(true))
                {
                    Renderer renderer;
                    var go = map[sourceRenderer.transform].gameObject;
                    if (sourceRenderer is SkinnedMeshRenderer skin)
                    {
                        var copy = go.AddComponent<SkinnedMeshRenderer>(); copy.sharedMesh = skin.sharedMesh;
                        copy.bones = skin.bones.Select(b => b && map.TryGetValue(b, out var v) ? v : null).ToArray();
                        copy.rootBone = skin.rootBone && map.TryGetValue(skin.rootBone, out var bone) ? bone : null;
                        copy.localBounds = skin.localBounds; copy.updateWhenOffscreen = true;
                        for (int i = 0; copy.sharedMesh && i < copy.sharedMesh.blendShapeCount; i++) copy.SetBlendShapeWeight(i, skin.GetBlendShapeWeight(i));
                        renderer = copy;
                    }
                    else if (sourceRenderer is MeshRenderer && sourceRenderer.TryGetComponent<MeshFilter>(out var filter))
                    { go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh; renderer = go.AddComponent<MeshRenderer>(); }
                    else { proxy.Warnings.Add("Skipped unsupported renderer: " + sourceRenderer.GetType().Name); continue; }
                    renderer.enabled = sourceRenderer.enabled;
                    renderer.sharedMaterials = sourceRenderer.sharedMaterials.Select(m => m ? m : Fallback(proxy)).ToArray();
                }
                var animatorSource = animators.FirstOrDefault();
                var animatorRoot = animatorSource ? map[animatorSource.transform].gameObject : map[source.transform].gameObject;
                animatorRoot.SetActive(true);
                proxy.Animator = animatorRoot.AddComponent<Animator>();
                proxy.Animator.avatar = animatorSource ? animatorSource.avatar : null;
                proxy.Animator.runtimeAnimatorController = null; proxy.Animator.applyRootMotion = false; proxy.Animator.fireEvents = false;
                proxy.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                int skipped = source.GetComponentsInChildren<Component>(true).Count(c => !c || !(c is Transform || c is Animator || c is MeshFilter || c is MeshRenderer || c is SkinnedMeshRenderer));
                if (skipped > 0) proxy.Warnings.Add("Omitted gameplay/missing/physics components: " + skipped);
                if (proxy.fallback) proxy.Warnings.Add("Missing materials use a temporary neutral material; original resources are untouched.");
                proxy.CaptureRest(); return proxy;
            }
            catch { proxy.Dispose(); throw; }
        }
        static Material Fallback(SkillVisualProxy proxy)
        {
            if (!proxy.fallback) proxy.fallback = new Material(Shader.Find("Standard") ?? Shader.Find("Hidden/InternalErrorShader")) { hideFlags = HideFlags.HideAndDontSave };
            return proxy.fallback;
        }
    }
}
