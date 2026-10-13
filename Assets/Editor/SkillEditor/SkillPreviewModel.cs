using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZZ.SkillEditor
{
    /// <summary>
    /// 以白名单重建骨骼、网格与 Animator。绝不 Instantiate 原角色，避免 ExecuteAlways/Awake
    /// 在禁用脚本前运行，也不会带入网络、相机、物理、音效或粒子组件。
    /// </summary>
    internal static class SkillPreviewModel
    {
        public static Animator Create(GameObject prefab, string animatorPath, Scene scene, out GameObject root)
        {
            root = null;
            if (prefab == null) throw new ArgumentException("请先配置预览角色 Prefab。");
            Transform source = string.IsNullOrEmpty(animatorPath) ? prefab.transform : prefab.transform.Find(animatorPath);
            var sourceAnimator = source != null ? source.GetComponent<Animator>() : null;
            if (sourceAnimator == null) throw new ArgumentException("Animator 路径无效，请在资产中选择角色的 Animator 节点。");
            if (!sourceAnimator.hasTransformHierarchy)
                throw new ArgumentException("预览需要可访问的骨骼层级，请在模型导入设置中关闭 Optimize Game Objects 后人工重新导入。");
            root = new GameObject("技能预览（不保存）") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(root, scene);
            var mapping = new Dictionary<Transform, Transform>();
            try
            {
                CloneTransforms(source, root.transform, mapping);
                foreach (var pair in mapping) CopyRenderers(pair.Key, pair.Value, mapping);
                var target = mapping[source];
                target.localPosition = Vector3.zero;
                target.localRotation = Quaternion.identity;
                target.localScale = source.lossyScale;
                var animator = target.gameObject.AddComponent<Animator>();
                animator.avatar = sourceAnimator.avatar;
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = true;
                animator.fireEvents = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                return animator;
            }
            catch { UnityEngine.Object.DestroyImmediate(root); root = null; throw; }
        }

        private static void CloneTransforms(Transform source, Transform parent, Dictionary<Transform, Transform> mapping)
        {
            var go = new GameObject(source.name) { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
            var target = go.transform;
            target.SetParent(parent, false);
            target.localPosition = source.localPosition;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
            mapping.Add(source, target);
            foreach (Transform child in source) CloneTransforms(child, target, mapping);
            go.SetActive(source.gameObject.activeSelf);
        }

        private static void CopyRenderers(Transform source, Transform target, Dictionary<Transform, Transform> mapping)
        {
            var skinned = source.GetComponent<SkinnedMeshRenderer>();
            if (skinned != null)
            {
                var copy = target.gameObject.AddComponent<SkinnedMeshRenderer>();
                copy.sharedMesh = skinned.sharedMesh;
                copy.sharedMaterials = skinned.sharedMaterials;
                copy.localBounds = skinned.localBounds;
                copy.quality = skinned.quality;
                copy.updateWhenOffscreen = true;
                copy.enabled = skinned.enabled;
                var bones = skinned.bones;
                var mapped = new Transform[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                {
                    if (bones[i] == null) continue;
                    if (!mapping.TryGetValue(bones[i], out var bone))
                        throw new ArgumentException("网格引用了所选 Animator 子树之外的骨骼，请调整预览 Prefab 的 Animator 层级。");
                    mapped[i] = bone;
                }
                copy.bones = mapped;
                if (skinned.rootBone != null && mapping.TryGetValue(skinned.rootBone, out var rootBone)) copy.rootBone = rootBone;
                if (copy.sharedMesh != null)
                    for (int i = 0; i < copy.sharedMesh.blendShapeCount; i++) copy.SetBlendShapeWeight(i, skinned.GetBlendShapeWeight(i));
            }
            var filter = source.GetComponent<MeshFilter>();
            var renderer = source.GetComponent<MeshRenderer>();
            if (filter != null && renderer != null)
            {
                target.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var copy = target.gameObject.AddComponent<MeshRenderer>();
                copy.sharedMaterials = renderer.sharedMaterials;
                copy.enabled = renderer.enabled;
            }
        }
    }
}
