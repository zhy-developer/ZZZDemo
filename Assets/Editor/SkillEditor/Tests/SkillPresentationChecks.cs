using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace SkillConfig.Editor.Tests
{
    public static class SkillPresentationChecks
    {
        const string Folder="Assets/Resources/Phase2D3";
        static void Check(bool ok,string text) {if(!ok) throw new Exception("PHASE2D3_FAIL "+text);Debug.Log("PHASE2D3_PASS "+text);}
        static void Guard() {if(!Application.dataPath.Replace('\\','/').Contains("/Temp/SkillEditorPhase2C/")) throw new Exception("Isolated harness only");}
        internal static ComboData Owner()
        {
            var owner=Object.Instantiate(AssetDatabase.LoadAssetAtPath<ComboData>(SkillPilotSetup.ComboPath(1)));
            owner.skill.tracks.Clear();owner.skill.vfx.Clear();owner.skill.sfx.Clear();owner.skill.interrupts.Clear();
            owner.skill.hitBoxes.Clear();owner.skill.hitGroups.Clear();owner.skill.invincibility.Clear();owner.skill.events.Clear();
            return owner;
        }
        static Type UnsafeType=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("PresentationUnsafeProbe")).First(t=>t!=null);
        internal static GameObject Model()=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Model/雅/Model/星见雅.fbx");
        internal static GameObject Particle(bool unsafeScript=false)
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var go=new GameObject("Particle fixture");go.SetActive(false);
            var p=go.AddComponent<ParticleSystem>();var main=p.main;main.loop=false;main.duration=.3f;main.startLifetime=.8f;main.startSpeed=.7f;main.simulationSpace=ParticleSystemSimulationSpace.World;main.playOnAwake=false;
            var emission=p.emission;emission.rateOverTime=80;emission.rateOverDistance=20;
            var child=new GameObject("Child");child.transform.SetParent(go.transform,false);
            var sub=child.AddComponent<ParticleSystem>();var sm=sub.main;sm.loop=false;sm.duration=.2f;sm.startLifetime=.3f;sm.playOnAwake=false;var se=sub.emission;se.enabled=false;var subs=p.subEmitters;subs.enabled=true;subs.AddSubEmitter(sub,ParticleSystemSubEmitterType.Birth,ParticleSystemSubEmitterProperties.InheritNothing);
            if(unsafeScript) go.AddComponent(UnsafeType);
            var prefab=PrefabUtility.SaveAsPrefabAsset(go,Folder+(unsafeScript?"/Unsafe.prefab":"/Particles.prefab"));Object.DestroyImmediate(go);
            // Enable the saved asset through isolated prefab contents, before safety counters are reset.
            var contents=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefab));contents.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(contents,AssetDatabase.GetAssetPath(prefab));PrefabUtility.UnloadPrefabContents(contents);
            return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(prefab));
        }
        internal static CharacterSkillCatalog Catalog(ComboData owner,GameObject prefab)
        {
            var c=ScriptableObject.CreateInstance<CharacterSkillCatalog>();c.characterId="phase2d3";
            c.animatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(SkillPilotSetup.ControllerPath);c.skills.Add(owner);
            c.resources.Add(new SkillResourceBinding {id="particles",kind=ResourceKind.VfxPrefab,asset=prefab});
            var audio=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Audio/Combo/Hit/IMPT-punch-1.wav");
            if(!audio) {audio=AudioClip.Create("fixture",44100*4,1,44100,false);}
            c.resources.Add(new SkillResourceBinding {id="audio",kind=ResourceKind.AudioClip,asset=audio});
            return c;
        }
        static VfxCue Vfx(string id,int start=0,AttachmentMode attachment=AttachmentMode.Root)=>new VfxCue {
            id=id,resourceId="particles",startFrame=start,endFrame=5,attachment=attachment,
            lifetime=VfxLifetime.AutoComplete,maxLifetimeFrames=50,classification=VfxClass.AttackCritical};
        static SfxCue Sound(string id,SoundInterrupt policy=SoundInterrupt.StopImmediately)=>new SfxCue {
            id=id,resourceId="audio",startFrame=0,endFrame=30,mode=SoundMode.Loop,volumePermille=600,
            onInterrupt=policy,fadeInFrames=2,fadeOutFrames=3};
        static string Snapshot(SkillPreviewSession s)
        {
            return string.Join("|",s.Presentation.Effects.OrderBy(e=>e.Cue.id).SelectMany(e=>e.Proxy.Systems.Select(p=>{
                var particles=new ParticleSystem.Particle[p.particleCount];p.GetParticles(particles);
                return e.Cue.id+":"+p.name+":"+p.time.ToString("R")+":"+string.Join(";",particles.Select(x=>x.position.ToString("F6")+","+x.remainingLifetime.ToString("R")+","+x.randomSeed));
            })));
        }
        public static void Run()
        {
            Guard();var prefab=Particle();var owner=Owner();var catalog=Catalog(owner,prefab);
            var original=File.ReadAllText(AssetDatabase.GetAssetPath(prefab));
            owner.skill.vfx.Add(Vfx("root"));owner.skill.vfx.Add(Vfx("world",2,AttachmentMode.World));
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(prefab),Folder+"/Loop.prefab");var loopContent=PrefabUtility.LoadPrefabContents(Folder+"/Loop.prefab");var loopMain=loopContent.GetComponent<ParticleSystem>().main;loopMain.loop=true;PrefabUtility.SaveAsPrefabAsset(loopContent,Folder+"/Loop.prefab");PrefabUtility.UnloadPrefabContents(loopContent);
            var unsafePrefab=Particle(true);UnsafeType.GetField("Calls").SetValue(null,0);Check(unsafePrefab.GetComponents<MonoBehaviour>().Length==1,"unsafe fixture really retains runtime ExecuteAlways script");
            owner.skill.skillId="presentation-session-probe";catalog.characterId="presentation-session";AssetDatabase.CreateAsset(owner,Folder+"/SessionSkill.asset");AssetDatabase.CreateAsset(catalog,Folder+"/SessionCatalog.asset");AssetDatabase.SaveAssets();
            var scene=SceneManager.GetActiveScene();bool dirty=scene.isDirty;
            using(var s=new SkillPreviewSession(owner,catalog,Model()))
            {
                Check(string.IsNullOrEmpty(s.Error),"presentation session opens real Miyabi model");
                Check(s.Presentation.Effects.Count()==1,"frame zero triggers once");
                Check(s.Presentation.Effects.Single().Proxy.Root.GetComponentsInChildren<MonoBehaviour>(true).Length==0,"native VFX contains no business scripts");
                var p=s.Presentation.Effects.Single().Proxy.Systems[0];
                Check(p.subEmitters.GetSubEmitterSystem(0)==s.Presentation.Effects.Single().Proxy.Systems[1],"sub-emitter dependency remaps to owned child");Check(!p.useAutoRandomSeed && p.main.stopAction==ParticleSystemStopAction.None,"fixed seeds and no callback stop action");
                s.SetPlaying(true);s.Advance(.033*7);s.SetPlaying(false);
                Check(s.Frame==7 && s.Presentation.Effects.Count()==2,"crossed logical frames trigger multiple cues");
                var sequential=Snapshot(s);Check(s.Presentation.Effects.Any(e=>e.Proxy.Systems.Any(ps=>ps.particleCount>0)),"particles actually simulated");
                var world=s.Presentation.Effects.Single(e=>e.Cue.id=="world").Proxy.Root.transform.position;
                Check(world==s.Motion.Position(2).World,"World anchor captures actual trigger-frame integer position");
                Check(s.Presentation.Effects.Single(e=>e.Cue.id=="root").Proxy.Root.transform.position==s.Motion.Position(7).World,"Root follows current logical position");
                s.Seek(7);Check(sequential==Snapshot(s),"moving-emitter forward history equals direct Seek exactly");
                s.Seek(3);s.Seek(7);Check(sequential==Snapshot(s),"backward and repeated Seek reproduce particles");
                s.Advance(.5);Check(sequential==Snapshot(s),"paused presentation does not advance");
                Check(s.Presentation.Effects.Single(e=>e.Cue.id=="world").Proxy.Root.transform.position==world,"World remains frozen after rebuilding");
                int rootCount=s.Stage.scene.GetRootGameObjects().Length;for(int n=0;n<5;n++)s.Seek(7);
                Check(s.Stage.scene.GetRootGameObjects().Length==rootCount,"repeated Seek has stable owned root count");
                var bone=s.Proxy.Root.GetComponentsInChildren<Transform>(true).First(t=>t!=s.Proxy.Root.transform && t.name.Contains("Head"));
                var cue=Vfx("bone",0,AttachmentMode.Bone);cue.bone=bone.name;owner.skill.vfx.Add(cue);s.Invalidate();s.Seek(6);
                Check(Vector3.Distance(s.Presentation.Effects.Single(e=>e.Cue.id=="bone").Proxy.Root.transform.position,bone.position)<.00001,"Bone follows sampled skeleton");
                owner.skill.vfx.Remove(cue);cue=Vfx("bad-bone");cue.attachment=AttachmentMode.Bone;cue.bone="missing/path";owner.skill.vfx.Add(cue);s.Invalidate();
                Check(s.Presentation.Issues.Any(i=>i.ItemId=="bad-bone" && i.Text.Contains("Bone")),"invalid bone diagnosed per cue");
                owner.skill.vfx.Remove(cue);
                catalog.resources.Add(new SkillResourceBinding{id="unsafe",kind=ResourceKind.VfxPrefab,asset=unsafePrefab});
                cue=Vfx("unsafe");cue.resourceId="unsafe";owner.skill.vfx.Add(cue);s.Invalidate();
                Check((int)UnsafeType.GetField("Calls").GetValue(null)==0 && !s.Presentation.Effects.Any(e=>e.Cue.id=="unsafe"),"unsafe prefab rejected before any lifecycle callback");
                Check(s.Presentation.Issues.Any(i=>i.ItemId=="unsafe" && i.Text.Contains("unsupported")),"unsafe component diagnostic");
                owner.skill.vfx.Remove(cue);
                owner.skill.vfx.Add(Vfx("future",10));owner.skill.vfx.Add(Vfx("precast"));owner.skill.vfx.Last().classification=VfxClass.Precast;
                s.Invalidate();s.Interrupt(4);
                Check(!s.Presentation.Effects.Any(e=>e.Cue.id=="future" || e.Cue.id=="precast"),"interrupt cancels future cues and clears precast");
                Check(s.Presentation.Effects.Count()==2,"already triggered critical effects retain lifecycle");
                var atInterrupt=s.Proxy.Root.transform.position;s.SetPlaying(true);s.Advance(.033*60);
                Check(s.Frame==4 && s.Proxy.Root.transform.position==atInterrupt && !s.Playing && !s.Presentation.HasTail,"interrupt tails finish without further skill movement");
                s.ClearInterrupt();s.Seek(s.Duration);s.SetPlaying(true);s.Advance(.033*60);
                Check(s.Frame==s.Duration && !s.Presentation.HasTail,"normal ending drains AutoComplete after skill end without extending duration");
                s.Loop=true;s.LoopStartFrame=2;s.LoopEndFrame=8;s.Seek(7);s.SetPlaying(true);s.Advance(.033);s.SetPlaying(false);
                Check(s.Frame==2 && s.Presentation.Effects.Count()==3,"loop resets and reconstructs start without duplicates");
                s.Loop=false;
                                owner.skill.vfx.Clear();
                catalog.resources.Add(new SkillResourceBinding{id="looping",kind=ResourceKind.VfxPrefab,asset=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Loop.prefab")});
                var looping=Vfx("looping-cue");looping.resourceId="looping";looping.maxLifetimeFrames=4;owner.skill.vfx.Add(looping);s.Invalidate();
                Check(s.Presentation.Issues.Any(i=>i.ItemId=="looping-cue" && i.Text.Contains("looping")),"looping VFX explicitly diagnosed");
                s.SetPlaying(true);s.Advance(.033*4);s.SetPlaying(false);Check(!s.Presentation.Effects.Any(),"looping AutoComplete clears at finite configured bound");
                owner.skill.vfx.Clear();var missing=Vfx("missing-resource",10);missing.resourceId="absent";owner.skill.vfx.Add(missing);s.Invalidate();
                Check(s.Presentation.Issues.Any(i=>i.ItemId=="missing-resource" && i.Text.Contains("missing/ambiguous")),"missing future VFX is diagnosed before its trigger");
                owner.skill.vfx.Clear();var timeline=Vfx("controlled");timeline.lifetime=VfxLifetime.TimelineControlled;timeline.endFrame=3;owner.skill.vfx.Add(timeline);s.Invalidate();s.Seek(3);
                Check(s.Presentation.Effects.Single().Stopped && s.Presentation.Effects.Single().Proxy.Alive,"Timeline end stops emission while particles finish");
                Check(s.Presentation.Effects.Single().Proxy.Systems.All(ps=>!ps.emission.enabled),"Timeline end disables all emitter emission");
                owner.skill.vfx.Clear();owner.skill.sfx.Add(Sound("stop"));owner.skill.sfx.Add(Sound("end",SoundInterrupt.PlayToEnd));owner.skill.sfx.Add(Sound("fade",SoundInterrupt.FadeOut));s.Invalidate();
                Check(s.Presentation.Audio.Count==0,"Seek remains silent");
                s.SetPlaying(true);Check(s.Presentation.Audio.Count==3 && s.Presentation.Audio.Voices.All(v=>v.Started),"three independent voices begin on play");
                s.Advance(.033*4);s.SetPlaying(false);
                Check(s.Presentation.Audio.Voices.All(v=>Math.Abs(v.Source.volume-.6)<.001),"per-voice volume reaches configured gain");
                s.SetPlaying(true);Check(s.Presentation.Audio.Count==3,"resume does not replay historical voices");s.SetPlaying(false);
                s.Interrupt(4);s.Seek(2);s.SetPlaying(true);s.Advance(.033*2);s.SetPlaying(false);Check(s.Frame==4 && s.PresentationFrame==4,"Seek before interruption resumes animation to the configured stop frame");Check(s.Presentation.Audio.Count==2 && !s.Presentation.Audio.Voices.Any(v=>v.Cue.id=="stop"),"interruption stops only StopImmediately voice");
                s.SetPlaying(true);s.Advance(.033);s.SetPlaying(false);
                Check(Math.Abs(s.Presentation.Audio.Voices.Single(v=>v.Cue.id=="fade").Source.volume-.4f)<.001,"FadeOut uses interruption volume and logical frames");
                s.SetPlaying(true);s.Advance(.033*2);s.SetPlaying(false);
                Check(s.Presentation.Audio.Count==1 && s.Presentation.Audio.Voices.Single().Cue.id=="end","FadeOut releases only its own source");
                s.SetPlaying(true);s.Advance(.033*30);Check(s.Presentation.Audio.Count==0 && !s.Playing,"Loop PlayToEnd stops at explicit finite end");
                s.ClearInterrupt();owner.skill.sfx[0].mode=SoundMode.OneShot;owner.skill.sfx[0].endFrame=1;s.Invalidate();s.Seek(5);s.SetPlaying(true);
                Check(!s.Presentation.Audio.Voices.Any(v=>v.Cue.id=="stop") && s.Presentation.Audio.Count==2,"Seek playback does not resurrect ended OneShot; active loops resume");
                s.SetPlaying(false);s.Seek(0);Check(s.Presentation.Audio.Count==0,"silent Seek destroys all old voices");
                Debug.Log("DIRTY_BEFORE_HANDLES "+scene.isDirty+" initial="+dirty);owner.skill.sfx.Clear();owner.skill.vfx.Add(Vfx("edit"));s.Invalidate();
                using(var edit=new SkillVfxHandles())
                {
                    edit.Begin(owner,"edit");edit.Set(new Vector3(1.23456f,0,2),new Vector3(0,45.125f,0),1.2345f);
                    Check(owner.skill.vfx[0].offset.x==0,"VFX Handles draft never mutates asset");
                    edit.Commit();Check(owner.skill.vfx[0].offset.x==12346 && owner.skill.vfx[0].rotationMilliDegrees.y==45125,"VFX Handles commit quantized position/rotation");
                    Debug.Log("DIRTY_AFTER_COMMIT "+scene.isDirty);Undo.PerformUndo();Debug.Log("DIRTY_AFTER_UNDO "+scene.isDirty);Check(owner.skill.vfx[0].offset.x==0,"VFX transform Undo");Undo.PerformRedo();Check(owner.skill.vfx[0].offset.x==12346,"VFX transform Redo");
                    edit.Begin(owner,"edit");edit.Set(Vector3.zero,Vector3.zero,1);edit.Cancel();Check(owner.skill.vfx[0].offset.x==12346,"VFX gesture cancel preserves saved values");
                }
                Check(s.Presentation.Effects.Single().Proxy.Root.transform.position.x>1,"command change refreshes Scene VFX");
            }
            Check(File.ReadAllText(AssetDatabase.GetAssetPath(prefab))==original,"original effect Prefab bytes unchanged");Check(scene.isDirty==dirty,"original Scene dirty status unchanged: before="+dirty+" after="+scene.isDirty+" valid="+scene.IsValid());
            Check(SkillPreviewSession.LiveCount==0 && !Resources.FindObjectsOfTypeAll<GameObject>().Any(g=>g.name.StartsWith("SkillPreview::")),"close removes all owned visual/audio roots");
            for(int n=0;n<3;n++)using(var s=new SkillPreviewSession(owner,catalog,Model())){s.Seek(8);s.SetPlaying(true);}
            Check(!Resources.FindObjectsOfTypeAll<AudioSource>().Any(a=>a.name.StartsWith("SkillPreview::")),"reopen/switch ownership leaves no audio sources");
                        owner.skill.sfx.Add(Sound("exception"));
            using(var s=new SkillPreviewSession(owner,catalog,Model()))
            {
                s.SetPlaying(true);Object.DestroyImmediate(s.Presentation.Audio.Voices.Single().Source.gameObject);s.Advance(.033);
                Check(s.IsDisposed && !string.IsNullOrEmpty(s.Error) && SkillPreviewSession.LiveCount==0,"unexpected preview failure releases session");
                Check(!Resources.FindObjectsOfTypeAll<GameObject>().Any(g=>g.name.StartsWith("SkillPreview::")),"unexpected failure leaves no owned presentation objects");
            }
            AssetDatabase.DeleteAsset(Folder+"/SessionCatalog.asset");AssetDatabase.DeleteAsset(Folder+"/SessionSkill.asset");
            Debug.Log("PHASE2D3_COMPLETE");
        }
    }
}
