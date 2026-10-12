using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace SkillConfig.Editor.Tests
{
    public static class SkillPresentationResourceChecks
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception("PHASE2D3_RESOURCE_FAIL "+message);Debug.Log("PHASE2D3_RESOURCE_PASS "+message);}
        static void Guard(){if(!Application.dataPath.Replace('\\','/').Contains("/Temp/SkillEditorPhase2C/"))throw new Exception("Harness only");}
        static ComboData owner;static CharacterSkillCatalog catalog;static SkillPreviewSession session;
        static void Setup()
        {
            Guard();owner=SkillPresentationChecks.Owner();owner.skill.skillId="presentation-resource-probe";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/VFX/FX_slash_01.prefab");
            Check(prefab,"actual project slash prefab loads");catalog=SkillPresentationChecks.Catalog(owner,prefab);
            Check(AssetDatabase.GetAssetPath(catalog.resources[1].asset).EndsWith("IMPT-punch-1.wav"),"actual project AudioClip loads");
        }
        static void Cleanup(){session?.Dispose();session=null;if(owner)Object.DestroyImmediate(owner);if(catalog)Object.DestroyImmediate(catalog);}
        public static void Resources()
        {
            Setup();
            try
            {
                var prefab=(GameObject)catalog.resources[0].asset;string path=AssetDatabase.GetAssetPath(prefab),before=File.ReadAllText(path);
                owner.skill.vfx.Add(new VfxCue {id="real-slash",resourceId="particles",startFrame=0,endFrame=10,lifetime=VfxLifetime.AutoComplete,maxLifetimeFrames=120});
                session=new SkillPreviewSession(owner,catalog,SkillPresentationChecks.Model());
                Check(session.Presentation.Effects.Count()==1,"real slash native hierarchy accepted: "+string.Join(";",session.Presentation.Issues.Select(i=>i.Text)));
                Check(session.Presentation.Effects.Single().Proxy.Systems.Length==prefab.GetComponentsInChildren<ParticleSystem>(true).Length,"real nested particle systems retained");
                session.Seek(5);var native=session.Presentation.Effects.Single().Proxy.Systems;
                Check(native.Any(p=>p.particleCount>0),"real slash emits particles under logical clock");
                string snapshot=Particles(native);session.Seek(0);session.SetPlaying(true);session.Advance(.165);session.SetPlaying(false);
                Check(Particles(session.Presentation.Effects.Single().Proxy.Systems)==snapshot,"real slash sequential/Seek native states equal");
                Check(File.ReadAllText(path)==before && !EditorUtility.IsDirty(prefab),"real effect prefab remains unchanged");
                foreach(var material in prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m))
                    Debug.Log("PHASE2D3_RENDER_BOUNDARY material="+AssetDatabase.GetAssetPath(material)+" shader="+(material.shader?material.shader.name:"MISSING")+" supported="+(material.shader && material.shader.isSupported));
                session.Dispose();session=null;
                var second=Object.Instantiate(AssetDatabase.LoadAssetAtPath<ComboData>(SkillPilotSetup.ComboPath(2)));
                second.skill.vfx.Clear();second.skill.sfx.Clear();second.skill.interrupts.Clear();
                second.skill.vfx.Add(JsonUtility.FromJson<VfxCue>(JsonUtility.ToJson(owner.skill.vfx[0])));
                using(var normal2=new SkillPreviewSession(second,catalog,SkillPresentationChecks.Model()))
                {
                    normal2.Seek(5);
                    Check(normal2.Presentation.Effects.Count()==1 && normal2.ClipName==second.skill.animations[0].clip.name && normal2.Proxy.Root.transform.position==normal2.Motion.Position(5).World,"Normal2 actual animation integer motion and real VFX preview together");
                }
                Object.DestroyImmediate(second);
                const string folder="Assets/Resources/Phase2D3";
                Directory.CreateDirectory(folder);AssetDatabase.Refresh();
                if(!AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/RealSlash.prefab")) AssetDatabase.CopyAsset(path,folder+"/RealSlash.prefab");
                catalog.resources[0].asset=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/RealSlash.prefab");
                owner.skill.tracks.Clear();var vfx=SkillEditCommands.AddTrack(owner,TrackKind.Vfx);owner.skill.vfx[0].trackId=vfx.id;
                var audio=SkillEditCommands.AddTrack(owner,TrackKind.Sfx);
                owner.skill.sfx.Add(new SfxCue{id="real-audio",trackId=audio.id,resourceId="audio",startFrame=1,endFrame=24,volumePermille=420,fadeInFrames=2,fadeOutFrames=3,onInterrupt=SoundInterrupt.FadeOut});
                AssetDatabase.CreateAsset(owner,folder+"/SavedSkill.asset");AssetDatabase.CreateAsset(catalog,folder+"/SavedCatalog.asset");
                SkillEditCommands.Flush();AssetDatabase.SaveAssets();
                var issues=SkillConfigExporter.Export(catalog);
                Check(!issues.Any(x=>x.severity==DiagnosticSeverity.Error),"real-resource binding exports via unchanged Phase1 exporter: "+string.Join(";",issues.Select(x=>x.code)));
                string jsonPath=SkillConfigExporter.PathFor(catalog),json=File.ReadAllText(jsonPath);
                using(var edit=new SkillVfxHandles()){edit.Begin(owner,"real-slash");edit.Set(new Vector3(.2f,.3f,.4f),new Vector3(0,30,0),1.5f);edit.Commit();}
                Check(SkillConfigExporter.IsStale(catalog),"VFX transform marks JSON stale");
                Check(File.ReadAllText(jsonPath)==json,"SO edits do not auto-export JSON");
                SkillEditCommands.Flush();SkillConfigExporter.Export(catalog);Check(!SkillConfigExporter.IsStale(catalog),"explicit export matches saved VFX transform");
                json=File.ReadAllText(jsonPath);SkillEditCommands.Apply(owner,"Invalid cue",()=>owner.skill.vfx[0].maxLifetimeFrames=0);
                Check(SkillConfigExporter.Export(catalog).Any(d=>d.severity==DiagnosticSeverity.Error) && File.ReadAllText(jsonPath)==json,"invalid VFX blocks export without overwriting JSON");
                Undo.PerformUndo();SkillEditCommands.Flush();SkillConfigExporter.Export(catalog);AssetDatabase.SaveAssets();
                Check(owner.skill.vfx[0].offset.x==2000 && owner.skill.sfx[0].volumePermille==420,"saved transform and sound parameters retained");
                owner=null;catalog=null;
            }
            finally{Cleanup();}
        }
        public static void Reopen()
        {
            Guard();var c=AssetDatabase.LoadAssetAtPath<CharacterSkillCatalog>("Assets/Resources/Phase2D3/SavedCatalog.asset");
            Check(c && c.skills.Count==1,"fresh Unity process reopens catalog/skill references");
            var skill=c.skills[0];
            Check(skill.skill.vfx[0].offset.x==2000 && skill.skill.vfx[0].rotationMilliDegrees.y==30000 && skill.skill.vfx[0].scalePermille==1500,"fresh process VFX spatial values persisted");
            Check(skill.skill.sfx[0].volumePermille==420 && skill.skill.sfx[0].onInterrupt==SoundInterrupt.FadeOut,"fresh process SFX policy persisted");
            Check(!SkillConfigExporter.IsStale(c),"fresh process JSON/source consistency");
        }
        static double start;static int phase,sampleA,sampleB;static AudioSource a,b,external;static GameObject externalGo;
        public static void Final() { Reopen(); SkillComboGraphChecks.Reopen(); Audio(); }
        public static void Audio()
        {
            Setup();
            var clip=AudioClip.Create("Audio acceptance sine",44100*5,1,44100,false);
            var pcm=Enumerable.Range(0,44100*5).Select(i=>(float)Math.Sin(i*2*Math.PI*440/44100)*.1f).ToArray();clip.SetData(pcm,0);catalog.resources[1].asset=clip;
            owner.skill.sfx.Add(new SfxCue{id="A",resourceId="audio",startFrame=0,endFrame=150,mode=SoundMode.Loop,volumePermille=250});
            owner.skill.sfx.Add(new SfxCue{id="B",resourceId="audio",startFrame=0,endFrame=150,mode=SoundMode.Loop,volumePermille=750});
            session=new SkillPreviewSession(owner,catalog,SkillPresentationChecks.Model());session.SetPlaying(true);
            a=session.Presentation.Audio.Voices.Single(v=>v.Cue.id=="A").Source;b=session.Presentation.Audio.Voices.Single(v=>v.Cue.id=="B").Source;
            externalGo=EditorUtility.CreateGameObjectWithHideFlags("External audio sentinel",HideFlags.HideAndDontSave);external=externalGo.AddComponent<AudioSource>();external.clip=clip;external.Play();
            phase=0;start=EditorApplication.timeSinceStartup;EditorApplication.update+=AudioTick;
        }
        static void AudioTick()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            if(EditorApplication.timeSinceStartup-start<.35)return;start=EditorApplication.timeSinceStartup;
            try
            {
                if(phase==0)
                {
                    Check(a.isPlaying && b.isPlaying && a.timeSamples>0 && b.timeSamples>0,"two stage-owned native AudioSources actually advance in EditMode");
                    Check(Math.Abs(a.volume-.25)<.001 && Math.Abs(b.volume-.75)<.001,"independent native voice gains");
                    session.SetPlaying(false);sampleA=a.timeSamples;sampleB=b.timeSamples;
                }
                else if(phase==1)
                {
                    Check(!a.isPlaying && !b.isPlaying && a.timeSamples==sampleA && b.timeSamples==sampleB,"pause freezes both native audio sample cursors");
                    session.SetPlaying(true);
                }
                else if(phase==2)
                {
                    Check(a.timeSamples>sampleA && b.timeSamples>sampleB,"resume advances existing native cursors");
                    // Pause the preview clock separately; directly stopping A below must not be auto-resumed.
                    session.SetPlaying(false);b.UnPause();a.Stop();sampleB=b.timeSamples;
                }
                else if(phase==3)
                {
                    Check(!a.isPlaying && b.isPlaying && b.timeSamples>sampleB,"stopping one voice does not stop another");
                    session.Dispose();session=null;
                    Check(!a && !b && external && external.isPlaying,"session disposal destroys owned voices and leaves unrelated audio playing");
                    EndAudio(0);return;
                }
                phase++;
            }
            catch(Exception ex){Debug.LogException(ex);EndAudio(1);}
        }
        static void EndAudio(int code)
        {
            EditorApplication.update-=AudioTick;var clip=catalog?catalog.resources[1].asset:null;
            if(externalGo)Object.DestroyImmediate(externalGo);Cleanup();if(clip)Object.DestroyImmediate(clip);EditorApplication.Exit(code);
        }
        static string Particles(ParticleSystem[] systems)=>string.Join("|",systems.Select(p=>{
            var values=new ParticleSystem.Particle[p.particleCount];p.GetParticles(values);
            return p.time.ToString("R")+":"+string.Join(";",values.Select(v=>v.position.ToString("F6")+":"+v.remainingLifetime.ToString("R")+":"+v.randomSeed));
        }));
    }
}
