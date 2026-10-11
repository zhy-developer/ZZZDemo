using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SkillConfig.Editor
{
    [InitializeOnLoad]
    public sealed class SkillPreviewSession : ISkillPreviewSession
    {
        static readonly HashSet<SkillPreviewSession> live = new HashSet<SkillPreviewSession>();
        public static int LiveCount => live.Count;
        public readonly ComboData Owner;
        public readonly CharacterSkillCatalog Catalog;
        readonly GameObject source;
        readonly Stage previousStage;
        readonly Object[] previousSelection;
        SkillAnimationPreview animation;
        Hash128 sourceHash;
        double lastTime, accumulator;
        public SkillPreviewStage Stage { get; private set; }
        public SkillVisualProxy Proxy { get; private set; }
        public SkillRootMotionPreview Motion { get; private set; }
        public SkillPresentationPreview Presentation { get; private set; }
        public int PresentationFrame { get; private set; }
        public int LoopStartFrame { get; set; }
        public int LoopEndFrame { get; set; }
        public bool IsDisposed { get; private set; }
        public bool Playing { get; private set; }
        public bool Loop { get; set; }
        public float PreviewSpeed { get; set; } = 1;
        public int Frame { get; private set; }
        public int Duration => SkillTimelineAdapters.Duration(Owner);
        public int InterruptFrame { get; private set; } = -1;
        public bool Interrupted => InterruptFrame >= 0;
        public string Error { get; private set; }
        public string ClipName => animation?.ClipName;
        public double SourceSeconds => animation?.SourceSeconds ?? 0;
        public event Action Updated;
        static SkillPreviewSession()
        {
            AssemblyReloadEvents.beforeAssemblyReload += DisposeAll;
            EditorApplication.quitting += DisposeAll;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingEditMode) DisposeAll(); };
        }
        public static void DisposeAll() { foreach (var item in live.ToArray()) item.Dispose(); }
        public SkillPreviewSession(ComboData owner, CharacterSkillCatalog catalog, GameObject source)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !owner || owner.skill == null) throw new ArgumentException("Preview requires a configured skill in Edit Mode.");
            if (!source || !EditorUtility.IsPersistent(source)) throw new ArgumentException("Select a model/Prefab asset for visual-only reconstruction.");
            DisposeAll(); Owner = owner; Catalog = catalog; this.source = source;
            previousStage = StageUtility.GetCurrentStage(); previousSelection = Selection.objects;
            try
            {
                Stage = ScriptableObject.CreateInstance<SkillPreviewStage>(); Stage.Closing = () => Release(true);
                StageUtility.GoToStage(Stage, false);
                if (StageUtility.GetCurrentStage() != Stage || !Stage.scene.IsValid()) throw new InvalidOperationException("Preview Stage was not opened.");
                var light = SkillVisualProxyBuilder.Empty("SkillPreview::Light", Stage.scene).AddComponent<Light>();
                light.type = LightType.Directional; light.intensity = 1; light.transform.rotation = Quaternion.Euler(45, -30, 0);
                Proxy = SkillVisualProxyBuilder.Build(source, Stage.scene); sourceHash = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(source));
                animation = new SkillAnimationPreview(Proxy); Motion = new SkillRootMotionPreview(owner); Presentation = new SkillPresentationPreview(this);
                live.Add(this); lastTime = EditorApplication.timeSinceStartup;
                EditorApplication.update += Tick; SkillEditCommands.Changed += Invalidate; EditorApplication.projectChanged += Invalidate;
                Seek(0);
            }
            catch { Dispose(); throw; }
        }
        public void SamplePose(int frame)
        {
            Frame=Math.Max(0,Math.Min(Duration,Interrupted?Math.Min(frame,InterruptFrame):frame));
            var segments=Owner.skill.animations;
            var segment=Frame==Duration?segments.LastOrDefault():segments.FirstOrDefault(a=>a!=null && Frame>=a.startFrame && Frame<a.endFrame);
            if(segment==null) throw new ArgumentException("No animation segment at this frame.");
            Proxy.Root.SetActive(true);animation.Sample(segment,Frame);Proxy.Root.transform.position=Motion.Position(Frame).World;
            if(Interrupted && Frame>=InterruptFrame) animation.StopGraph();
        }
        public void Seek(int frame)
        {
            if(IsDisposed) return;
            Playing=false; accumulator=0; Presentation.Reset();
            if(!Owner || !source || !Stage || !Proxy.Root) {Dispose();return;}
            try
            {
                int target=Math.Max(0,Math.Min(Duration,Interrupted?Math.Min(frame,InterruptFrame):frame));
                if(target>60000) throw new ArgumentException("Preview history exceeds 60000-frame safety limit.");
                for(int f=0;f<=target;f++) {SamplePose(f);Presentation.Tick(f,InterruptFrame,false);}
                PresentationFrame=target;Error=null;
            }
            catch(Exception e) {Fail(e);}
            Notify();
        }
        void Fail(Exception e) {Error=e.Message;Playing=false;Presentation?.Reset();animation?.StopGraph();if(Proxy?.Root) Proxy.Root.SetActive(false);if(!(e is ArgumentException)) Dispose();}
        void Notify() {Updated?.Invoke();SceneView.RepaintAll();}
        public void SetPlaying(bool play)
        {
            if(IsDisposed || !string.IsNullOrEmpty(Error)) {Playing=false;return;}
            if(!play) {Playing=false;Presentation.Audio.Pause();accumulator=0;return;}
            if(!Interrupted && Frame>=Duration && !Presentation.HasTail) Seek(0);
            if(!Interrupted || PresentationFrame<InterruptFrame) Presentation.Audio.Prime(PresentationFrame);
            else if(PresentationFrame==InterruptFrame && Presentation.Audio.Count==0)
            { Presentation.Audio.Prime(InterruptFrame,true); Presentation.Audio.Interrupt(InterruptFrame); }
            if(Interrupted && PresentationFrame>=InterruptFrame && !Presentation.HasTail) return;
            Playing=true;Presentation.Audio.Resume();accumulator=0;lastTime=EditorApplication.timeSinceStartup;
        }
        public void Advance(double seconds)
        {
            if(!Playing || IsDisposed || seconds<0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
            accumulator+=seconds*Mathf.Clamp(PreviewSpeed,.25f,2);
            int steps=(int)Math.Min(1000000,Math.Floor((accumulator+1e-10)/.033));
            if(steps==0) return;accumulator-=steps*.033;
            try
            {
                for(int i=0;i<steps && Playing;i++)
                {
                    int next=PresentationFrame+1;
                    int end=LoopEndFrame>0?Math.Min(Duration,LoopEndFrame):Duration;
                    if(Loop && !Interrupted && next>=end && end>0)
                    {
                        double remainder=accumulator;Seek(Mathf.Clamp(LoopStartFrame,0,end-1));accumulator=remainder;if(IsDisposed || !string.IsNullOrEmpty(Error)) break;Playing=true;Presentation.Audio.Prime(PresentationFrame);Presentation.Audio.Resume();
                    }
                    else
                    {
                        if(next>60000) throw new ArgumentException("Preview tail exceeds 60000-frame safety limit.");
                        if((!Interrupted || next<=InterruptFrame) && next<=Duration) SamplePose(next);Presentation.Tick(next,InterruptFrame,true);if(Interrupted && next==InterruptFrame) Presentation.Audio.Interrupt(next);PresentationFrame=next;
                        if(((Interrupted && next>=InterruptFrame) || next>=Duration) && !Presentation.HasTail) {Playing=false;Presentation.Audio.Pause();}
                    }
                }
                if(steps>1) Presentation.Audio.SyncClock(PresentationFrame);
            }
            catch(Exception e) {Fail(e);}
            Notify();
        }
        void Tick()
        {
            if (IsDisposed) return;
            if (!Stage || StageUtility.GetCurrentStage() != Stage || !Owner || !source) { Dispose(); return; }
            double now = EditorApplication.timeSinceStartup; double elapsed = now - lastTime; lastTime = now; Advance(elapsed);
        }
        public void Interrupt(int frame) { SetPlaying(false); InterruptFrame = Math.Max(0, Math.Min(Duration, frame)); Seek(InterruptFrame); Presentation.Audio.Prime(InterruptFrame, true); Presentation.Audio.Interrupt(InterruptFrame); Notify(); }
        public void ClearInterrupt() { InterruptFrame = -1; Seek(0); }
        public bool Active(HitBox box) => !IsDisposed && (!Interrupted || Frame<InterruptFrame) && string.IsNullOrEmpty(Error) && box.startFrame <= Frame && Frame < box.endFrame;
        public void Invalidate()
        {
            if (IsDisposed) return;
            SetPlaying(false);
            try
            {
                animation.Dispose();
                var hash = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(source));
                if (hash != sourceHash) { Proxy.Dispose(); Proxy = SkillVisualProxyBuilder.Build(source, Stage.scene); sourceHash = hash; }
                Presentation.Dispose(); Presentation = new SkillPresentationPreview(this); animation = new SkillAnimationPreview(Proxy); Motion = new SkillRootMotionPreview(Owner); Seek(Frame);
            }
            catch (Exception e) { Fail(e); Notify(); }
        }
        public void Dispose() => Release(false);
        void Release(bool closingStage)
        {
            if (IsDisposed) return; IsDisposed = true; Playing = false; live.Remove(this);
            EditorApplication.update -= Tick; SkillEditCommands.Changed -= Invalidate; EditorApplication.projectChanged -= Invalidate;
            Presentation?.Dispose(); animation?.Dispose(); Proxy?.Dispose();
            if (!closingStage && Stage)
            {
                Stage.Closing = null;
                try
                {
                    if (StageUtility.GetCurrentStage() == Stage)
                    {
                        if (previousStage) StageUtility.GoToStage(previousStage, false); else StageUtility.GoToMainStage();
                        Selection.objects = previousSelection.Where(o => o).ToArray();
                    }
                }
                finally { if (Stage) Object.DestroyImmediate(Stage); }
            }
            Updated?.Invoke(); Updated = null; SceneView.RepaintAll();
        }
    }
}
