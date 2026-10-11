using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object=UnityEngine.Object;
namespace SkillConfig.Editor
{
    public sealed class SkillAudioPreview : IDisposable
    {
        public sealed class Voice
        {
            public SfxCue Cue; public AudioSource Source; public int End, Interrupt=-1; public float InterruptVolume;
            public bool Started; public float Offset;
        }
        readonly SkillPreviewSession session;
        readonly Dictionary<string,Voice> voices=new Dictionary<string,Voice>();
        readonly HashSet<string> fired=new HashSet<string>();
        public IEnumerable<Voice> Voices=>voices.Values;
        public int Count=>voices.Count;
        public SkillAudioPreview(SkillPreviewSession value) {session=value;}
        public void Reset() {foreach(var v in voices.Values) Destroy(v);voices.Clear();fired.Clear();}
        public void Dispose()=>Reset();
        static void Destroy(Voice v) {if(v.Source){v.Source.Stop();Object.DestroyImmediate(v.Source.gameObject);}}
        void Add(SfxCue cue,int frame)
        {
            if(!fired.Add(cue.id)) return;
            var clip=session.Presentation.Resource<AudioClip>(cue.resourceId,ResourceKind.AudioClip,cue.id);
            if(!clip || clip.samples<=0 || clip.frequency<=0) return;
            if(clip.loadState!=AudioDataLoadState.Loaded)
            {
                clip.LoadAudioData();
                if(clip.loadState!=AudioDataLoadState.Loaded) {session.Presentation.Note(cue.id,"Warning: AudioClip data is not ready. Wait for loading then Seek/restart preview.");return;}
            }
            if(clip.loadType!=AudioClipLoadType.DecompressOnLoad)
                session.Presentation.Note(cue.id,"Warning: compressed/streaming AudioClip cursor accuracy depends on codec packets; seek and clock correction are approximate.");
            int end=cue.endFrame;
            if(cue.mode==SoundMode.OneShot) end=Math.Min(end,cue.startFrame+(int)Math.Ceiling(clip.length/.033));
            if(frame>=end) return;
            var go=SkillVisualProxyBuilder.Empty("SkillPreview::SFX::"+cue.id,session.Stage.scene);
            try { var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.clip=clip;source.loop=cue.mode==SoundMode.Loop;
            var voice=new Voice {Cue=cue,Source=source,End=end,Offset=(float)((frame-cue.startFrame)*.033)%clip.length};
            voices.Add(cue.id,voice);UpdateVolume(voice,frame); } catch { Object.DestroyImmediate(go); throw; }
        }
        public void Prime(int frame,bool forInterrupt=false)
        {
            foreach(var cue in session.Owner.skill.sfx.Where(c=>c!=null && session.Presentation.ValidAudio(c)))
                if(cue.startFrame==frame || ((forInterrupt || cue.mode==SoundMode.Loop) && cue.startFrame<frame && frame<cue.endFrame)) Add(cue,frame);
        }
        public void Tick(int frame,bool trigger)
        {
            if(trigger) foreach(var cue in session.Owner.skill.sfx.Where(c=>c!=null && c.startFrame==frame && session.Presentation.ValidAudio(c))) Add(cue,frame);
            foreach(var v in voices.Values.ToArray())
            {
                if(frame>=v.End) {Destroy(v);voices.Remove(v.Cue.id);continue;}
                UpdateVolume(v,frame);
                if(session.Playing) Play(v);
            }
        }
        void Play(Voice v)
        {
            v.Source.pitch=session.PreviewSpeed;
            if(!v.Started) {v.Source.timeSamples=Math.Min(v.Source.clip.samples-1,(int)Math.Round(v.Offset*v.Source.clip.frequency));v.Source.Play();v.Started=true;}
            else v.Source.UnPause();
        }
        // A stalled Editor update may cross many logical frames before the audio thread sees the newly created voices.
        public void SyncClock(int frame)
        {
            foreach(var v in voices.Values)
            {
                var clip=v.Source.clip;
                double desired=(frame-v.Cue.startFrame)*.033;
                if(v.Cue.mode==SoundMode.Loop) desired%=clip.length;
                if(Math.Abs(v.Source.time-desired)>.066)
                    v.Source.timeSamples=Math.Min(clip.samples-1,Math.Max(0,(int)Math.Round(desired*clip.frequency)));
            }
        }
        public void Pause() {foreach(var v in voices.Values) v.Source.Pause();}
        public void Resume() {foreach(var v in voices.Values) Play(v);}
        public void Interrupt(int frame)
        {
            foreach(var v in voices.Values.ToArray())
            {
                if(v.Cue.startFrame>=frame || v.Cue.onInterrupt==SoundInterrupt.StopImmediately) {Destroy(v);voices.Remove(v.Cue.id);continue;}
                if(v.Cue.onInterrupt==SoundInterrupt.FadeOut)
                {v.Interrupt=frame;v.InterruptVolume=v.Source.volume;v.End=Math.Min(v.End,frame+v.Cue.fadeOutFrames);}
                // PlayToEnd uses the existing explicit endFrame; Loop therefore always has a finite end.
            }
        }
        void UpdateVolume(Voice v,int frame)
        {
            var c=v.Cue;float gain=c.volumePermille/1000f;
            if(c.fadeInFrames>0) gain*=Mathf.Clamp01((frame-c.startFrame)/(float)c.fadeInFrames);
            if(c.fadeOutFrames>0) gain*=Mathf.Clamp01((v.End-frame)/(float)c.fadeOutFrames);
            if(v.Interrupt>=0) gain=v.InterruptVolume*Mathf.Clamp01((v.End-frame)/(float)Math.Max(1,v.End-v.Interrupt));
            v.Source.volume=gain;v.Source.pitch=session.PreviewSpeed;
        }
    }
}
