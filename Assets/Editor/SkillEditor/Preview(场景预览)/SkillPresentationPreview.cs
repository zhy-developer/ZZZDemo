using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace SkillConfig.Editor
{
    public sealed class SkillPreviewIssue { public string ItemId, Text; }
    public sealed class SkillPresentationPreview : IDisposable
    {
        public sealed class Effect { public VfxCue Cue; public SkillVfxProxy Proxy; public Matrix4x4 Anchor; public bool Stopped; public int Bound; }
        readonly SkillPreviewSession session;
        readonly Dictionary<string,Effect> effects=new Dictionary<string,Effect>();
        readonly HashSet<string> diagnosed=new HashSet<string>();
        public readonly List<SkillPreviewIssue> Issues=new List<SkillPreviewIssue>();
        public readonly SkillAudioPreview Audio;
        public IEnumerable<Effect> Effects=>effects.Values;
        public bool HasTail=>effects.Count>0 || Audio.Count>0;
        public SkillPresentationPreview(SkillPreviewSession s) {session=s;Audio=new SkillAudioPreview(s);Preflight();}
        void Preflight()
        {
            foreach(var c in session.Owner.skill.vfx.Where(c=>c!=null))
            {
                if(!ValidVfx(c)) continue;
                var prefab=Resource<GameObject>(c.resourceId,ResourceKind.VfxPrefab,c.id);
                if(!prefab) continue;
                try
                {
                    Anchor(c); SkillVfxProxy.Validate(prefab);
                    foreach(var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                        if(renderer.sharedMaterials.Any(m=>!m || !m.shader || !m.shader.isSupported))
                            Note(c.id,"Warning: missing/unsupported material or shader; particle-state preview only, rendered appearance is not verified.");
                    if(!UnityEditor.AssetDatabase.GetAssetPath(prefab).Replace('\\','/').Contains("/Resources/"))
                        Note(c.id,"Warning: preview reference is outside Resources. Phase1 Export will reject it; preview does not migrate assets.");
                }
                catch(Exception ex) { Note(c.id,"Error: VFX preview rejected: "+ex.Message); }
            }
            foreach(var c in session.Owner.skill.sfx.Where(c=>c!=null))
                if(ValidAudio(c)) Resource<AudioClip>(c.resourceId,ResourceKind.AudioClip,c.id);
        }
        public void Note(string id,string message)
        {
            if(diagnosed.Add(id+message)) Issues.Add(new SkillPreviewIssue {ItemId=id,Text=(session.Owner.skill.skillId??session.Owner.name)+" / "+id+": "+message});
        }
        public T Resource<T>(string id,ResourceKind kind,string cue) where T:UnityEngine.Object
        {
            var matches=session.Catalog?session.Catalog.resources.Where(r=>r!=null && r.id==id && r.kind==kind).ToArray():Array.Empty<SkillResourceBinding>();
            if(matches.Length!=1 || !(matches[0].asset is T resource)) {Note(cue,"Error: missing/ambiguous "+kind+" resource: "+id);return null;}
            return resource;
        }
        public bool ValidAudio(SfxCue c)
        {
            bool valid=!string.IsNullOrEmpty(c.id) && session.Owner.skill.sfx.Count(x=>x!=null && x.id==c.id)==1 && c.startFrame>=0 && c.startFrame<session.Duration && c.endFrame>c.startFrame && c.endFrame<=60000
                && c.volumePermille>=0 && c.volumePermille<=1000 && c.fadeInFrames>=0 && c.fadeOutFrames>=0 && Enum.IsDefined(typeof(SoundMode),c.mode) && Enum.IsDefined(typeof(SoundInterrupt),c.onInterrupt)
                && (c.onInterrupt!=SoundInterrupt.FadeOut || c.fadeOutFrames>0);
            if(!valid) Note(c.id,"Error: illegal/unbounded SFX configuration (preview limit 60000 frames).");
            return valid;
        }
        bool ValidVfx(VfxCue c)
        {
            int last=session.Owner.skill.interrupts.Where(x=>x!=null).Select(x=>x.endFrame).DefaultIfEmpty(0).Max();
            bool valid=!string.IsNullOrEmpty(c.id) && session.Owner.skill.vfx.Count(x=>x!=null && x.id==c.id)==1 && c.startFrame>=0 && c.startFrame<session.Duration && c.scalePermille>0
                && Enum.IsDefined(typeof(VfxClass),c.classification) && Enum.IsDefined(typeof(VfxLifetime),c.lifetime) && Enum.IsDefined(typeof(AttachmentMode),c.attachment)
                && (c.classification!=VfxClass.AttackCritical || c.startFrame>=last)
                && (c.lifetime==VfxLifetime.AutoComplete?c.maxLifetimeFrames>0 && (long)c.startFrame+c.maxLifetimeFrames<=60000:c.endFrame>c.startFrame && c.endFrame<=session.Duration);
            if(!valid) Note(c.id,"Error: illegal VFX interval/lifetime or critical VFX before final interrupt end.");
            return valid;
        }
        public Matrix4x4 Anchor(VfxCue cue)
        {
            Transform anchor=session.Proxy.Root.transform;
            if(cue.attachment==AttachmentMode.Bone)
            {
                if(string.IsNullOrWhiteSpace(cue.bone)) throw new ArgumentException("Bone path missing.");
                var found=session.Proxy.Root.transform.Find(cue.bone) ?? session.Proxy.Animator.transform.Find(cue.bone);
                if(!found && !cue.bone.Contains("/"))
                {
                    var matches=session.Proxy.Root.GetComponentsInChildren<Transform>(true).Where(t=>t.name==cue.bone).ToArray();
                    if(matches.Length==1) found=matches[0];
                }
                if(!found) throw new ArgumentException("Bone path missing/ambiguous: "+cue.bone);
                anchor=found;
            }
            // Bone offsets use metres, not inherited FBX bone scale.
            return Matrix4x4.TRS(anchor.position,anchor.rotation,Vector3.one);
        }
        public static void Pose(Effect effect,VfxCue cue)
        {
            var t=effect.Proxy.Root.transform;
            t.position=effect.Anchor.MultiplyPoint3x4(SkillShapeEdit.World(cue.offset));
            t.rotation=effect.Anchor.rotation*Quaternion.Euler(cue.rotationMilliDegrees.x/1000f,cue.rotationMilliDegrees.y/1000f,cue.rotationMilliDegrees.z/1000f);
            t.localScale=Vector3.one*(cue.scalePermille/1000f);
        }
        static uint Seed(string id) {unchecked {uint h=2166136261;foreach(char c in id) h=(h^c)*16777619;return h==0?1:h;}}
        public void Reset() {foreach(var e in effects.Values) e.Proxy.Dispose();effects.Clear();Audio.Reset();}
        public void Dispose()=>Reset();
        public void Tick(int frame,int interrupt,bool audio)
        {
            foreach(var effect in effects.Values.ToArray())
            {
                var c=effect.Cue;
                if(interrupt>=0 && frame>=interrupt && c.classification==VfxClass.Precast) {Remove(effect);continue;}
                if(c.attachment!=AttachmentMode.World) effect.Anchor=Anchor(c);
                Pose(effect,c);
                effect.Proxy.Simulate(.033f);
                if(c.lifetime==VfxLifetime.TimelineControlled && frame>=c.endFrame && !effect.Stopped) {effect.Proxy.StopEmission();effect.Stopped=true;}
                if(frame>=effect.Bound)
                {if(effect.Proxy.Alive) Note(c.id,"Warning: reached explicit/editor safety lifetime; remaining particles cleared.");Remove(effect);}
                else if(frame>c.startFrame && !effect.Proxy.Alive) Remove(effect);
            }
            if(interrupt<0 || frame<interrupt)
                foreach(var c in session.Owner.skill.vfx.Where(c=>c!=null && c.startFrame==frame && ValidVfx(c)))
                {
                    var prefab=Resource<GameObject>(c.resourceId,ResourceKind.VfxPrefab,c.id);if(!prefab) continue;
                    SkillVfxProxy proxy=null;
                    try
                    {
                        Matrix4x4 anchor=Anchor(c);proxy=SkillVfxProxy.Build(prefab,session.Stage.scene,Seed(c.id));
                        var effect=new Effect {Cue=c,Proxy=proxy,Anchor=anchor,Bound=c.lifetime==VfxLifetime.AutoComplete?c.startFrame+c.maxLifetimeFrames:c.endFrame+900};
                        Pose(effect,c); proxy.Restart(); effects.Add(c.id,effect);
                        if(proxy.HasLoop) Note(c.id,"Warning: looping particle resource is bounded by configured lifetime/end; it cannot naturally AutoComplete.");
                        Note(c.id,"Preview boundary: particle state uses fixed 33ms history; material/shader global time is not frozen. Shader-driven appearance needs visual verification.");
                    }
                    catch(Exception ex) {proxy?.Dispose();Note(c.id,"Error: VFX preview rejected: "+ex.Message);}
                }
            if(audio) Audio.Tick(frame,interrupt<0 || frame<interrupt);
        }
        void Remove(Effect e) {e.Proxy.Dispose();effects.Remove(e.Cue.id);}
    }
}
