using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
namespace SkillConfig.Editor
{
    // No prefab instantiation: only audited native visual component types are constructed.
    public sealed class SkillVfxProxy : IDisposable
    {
        public GameObject Root;
        public ParticleSystem[] Systems, Roots;
        public Vector3 SourceScale;
        public bool HasLoop;
        public bool Alive => Roots.Any(p=>p && p.IsAlive(true));
        public void Restart() {foreach(var p in Roots) p.Simulate(0,true,true,false);}
        public void Simulate(float seconds) { foreach(var p in Roots) p.Simulate(seconds,true,false,false); }
        public void StopEmission() { foreach(var p in Systems) { var e=p.emission; e.enabled=false; p.Stop(false,ParticleSystemStopBehavior.StopEmitting); } }
        public void Dispose() { if(Root) Object.DestroyImmediate(Root); }
        static bool Allowed(Component c) => c && (c is Transform || c is ParticleSystem || c is ParticleSystemRenderer || c is MeshFilter || c is MeshRenderer);
        public static void Validate(GameObject prefab)
        {
            if(!prefab || !EditorUtility.IsPersistent(prefab)) throw new ArgumentException("VFX requires a persistent prefab asset.");
            var components=prefab.GetComponentsInChildren<Component>(true);
            var bad=components.Where(c=>!Allowed(c)).Select(c=>c?c.GetType().Name:"Missing script").Distinct().ToArray();
            if(bad.Length>0) throw new ArgumentException("Unsafe/unsupported VFX components: "+string.Join(", ",bad));
            var systems=prefab.GetComponentsInChildren<ParticleSystem>(true);
            if(systems.Length==0) throw new ArgumentException("No ParticleSystem: native particle preview only.");
            foreach(var c in components)
            {
                var scan=new SerializedObject(c); var property=scan.GetIterator();
                while(property.Next(true)) if(property.propertyType==SerializedPropertyType.ObjectReference)
                {
                    var reference=property.objectReferenceValue;
                    if(reference is Component component && !components.Contains(component))
                        throw new ArgumentException("External component dependency: "+property.propertyPath);
                    if(reference is GameObject go && go!=prefab && !go.transform.IsChildOf(prefab.transform))
                        throw new ArgumentException("External GameObject dependency: "+property.propertyPath);
                }
            }
            foreach(var p in systems)
            {
                if(p.collision.enabled || p.trigger.enabled || p.externalForces.enabled || p.lights.enabled)
                    throw new ArgumentException("Scene-dependent collision/trigger/external force/light modules cannot be reconstructed safely.");
                if(p.main.prewarm) throw new ArgumentException("Prewarm requires pre-trigger movement history; Seek unsupported.");
                var so=new SerializedObject(p); var it=so.GetIterator();
                while(it.Next(true)) if(it.propertyType==SerializedPropertyType.ObjectReference && it.objectReferenceValue is Component component && !components.Contains(component))
                    throw new ArgumentException("External component dependency: "+it.propertyPath);
                for(int i=0;p.subEmitters.enabled && i<p.subEmitters.subEmittersCount;i++)
                {
                    var sub=p.subEmitters.GetSubEmitterSystem(i);
                    if(!sub || !sub.transform.IsChildOf(p.transform) || sub==p)
                        throw new ArgumentException("Sub-emitter must be a descendant of its emitter; external/cross-root links are unsupported.");
                }
            }
        }
        public static SkillVfxProxy Build(GameObject prefab, Scene scene, uint seed)
        {
            Validate(prefab);
            var components=prefab.GetComponentsInChildren<Component>(true);
            var systems=prefab.GetComponentsInChildren<ParticleSystem>(true);
            var proxy=new SkillVfxProxy();
            try
            {
                proxy.Root=SkillVisualProxyBuilder.Empty("SkillPreview::VFX",scene);
                var map=new Dictionary<Object,Object>();
                foreach(var t in prefab.GetComponentsInChildren<Transform>(true))
                {
                    var go=SkillVisualProxyBuilder.Empty(t.name,scene);go.SetActive(false);
                    go.transform.SetParent(t==prefab.transform?proxy.Root.transform:(Transform)map[t.parent],false);
                    go.transform.localPosition=t==prefab.transform?Vector3.zero:t.localPosition;
                    go.transform.localRotation=t==prefab.transform?Quaternion.identity:t.localRotation;
                    go.transform.localScale=t.localScale;
                    map[t]=go.transform;map[t.gameObject]=go;
                    if(t.GetComponent<ParticleSystem>()) { var p=go.AddComponent<ParticleSystem>(); map[t.GetComponent<ParticleSystem>()]=p; if(t.GetComponent<ParticleSystemRenderer>()) map[t.GetComponent<ParticleSystemRenderer>()]=go.GetComponent<ParticleSystemRenderer>(); }
                    if(t.GetComponent<MeshFilter>()) map[t.GetComponent<MeshFilter>()]=go.AddComponent<MeshFilter>();
                    if(t.GetComponent<MeshRenderer>()) map[t.GetComponent<MeshRenderer>()]=go.AddComponent<MeshRenderer>();
                }
                foreach(var c in components.Where(c=>!(c is Transform)))
                {
                    var target=map[c]; EditorUtility.CopySerialized(c,target);
                    var so=new SerializedObject(target);var it=so.GetIterator();
                    while(it.Next(true)) if(it.propertyType==SerializedPropertyType.ObjectReference && it.objectReferenceValue && map.TryGetValue(it.objectReferenceValue,out var reference)) it.objectReferenceValue=reference;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                proxy.Systems=systems.Select(p=>(ParticleSystem)map[p]).ToArray();
                proxy.Roots=proxy.Systems.Where(p=>!proxy.Systems.Any(other=>other!=p && p.transform.IsChildOf(other.transform))).ToArray();
                int n=0;
                foreach(var p in proxy.Systems)
                {
                    var main=p.main;main.playOnAwake=false;main.stopAction=ParticleSystemStopAction.None;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
                    proxy.HasLoop|=main.loop; p.useAutoRandomSeed=false; p.randomSeed=seed+(uint)(++n*7919);
                }
                foreach(var t in prefab.GetComponentsInChildren<Transform>(true)) ((Transform)map[t]).gameObject.SetActive(t.gameObject.activeSelf);
                foreach(var p in proxy.Roots) { p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear); p.Simulate(0,true,true,false); }
                proxy.SourceScale=prefab.transform.localScale;
                return proxy;
            }
            catch {proxy.Dispose();throw;}
        }
    }
}
