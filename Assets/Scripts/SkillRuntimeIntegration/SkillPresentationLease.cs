using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkillRuntime.Integration
{
    // Pools keep idle objects; a lease alone owns an active object. Legacy queue APIs are unchanged.
    public sealed class SkillPresentationLease : IDisposable
    {
        GameObject value;
        Action<GameObject> release;
        public GameObject Object => value;
        internal SkillPresentationLease(GameObject value, Action<GameObject> release) { this.value = value; this.release = release; }
        public void Dispose()
        {
            if (release == null) return;
            var callback = release; var old = value; release = null; value = null;
            if (old) callback(old); // old callbacks holding this lease cannot access a re-rented object.
        }
    }
    internal sealed class SkillPresentationSlots
    {
        readonly Dictionary<UnityEngine.Object, Stack<GameObject>> idle = new Dictionary<UnityEngine.Object, Stack<GameObject>>();
        public SkillPresentationLease Rent(UnityEngine.Object resource, Transform poolRoot, Func<GameObject> create)
        {
            if (!resource || !poolRoot) throw new ArgumentException("Pool owner/resource missing.");
            if (!idle.TryGetValue(resource, out var stack)) idle.Add(resource, stack = new Stack<GameObject>());
            GameObject value = null;
            while (stack.Count > 0 && !value) value = stack.Pop();
            if (!value) value = create();
            return new SkillPresentationLease(value, go => {
                if (!poolRoot) { UnityEngine.Object.Destroy(go); return; }
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                foreach (var audio in go.GetComponentsInChildren<AudioSource>(true)) { audio.Stop(); audio.clip = null; }
                go.SetActive(false); go.transform.SetParent(poolRoot, false); stack.Push(go);
            });
        }
        public static void ValidateVfx(GameObject prefab)
        {
            if (!prefab) throw new ArgumentException("VFX Prefab missing.");
            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
            {
                if (!component) throw new ArgumentException("VFX has missing script.");
                if (!(component is Transform || component is ParticleSystem || component is ParticleSystemRenderer || component is MeshRenderer || component is MeshFilter || component is Light))
                    throw new ArgumentException("VFX requires explicit side-effect adapter: " + component.GetType().FullName);
            }
            if (prefab.GetComponentsInChildren<ParticleSystem>(true).Length == 0) throw new ArgumentException("Only particle VFX have supported lifetime semantics.");
        }
    }
}
