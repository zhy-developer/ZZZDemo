using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace SkillConfig.Editor.Tests
{
    public static class SkillEditorPhase2CChecks
    {
        public static void Safety()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Temp/SkillEditorPhase2C/")) throw new Exception("Use isolated Phase2C harness.");
            var type = typeof(SkillAssetValidator).Assembly.GetType("SkillConfig.Editor.SkillVisualProxyBuilder");
            Check(type != null, "visual-only builder exists");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Model/雅/Model/星见雅.fbx");
            Check(source, "real Miyabi visual source loaded");
            var scene = SceneManager.GetActiveScene(); bool dirty = scene.isDirty;
            string before = EditorJsonUtility.ToJson(source);
            var stage = ScriptableObject.CreateInstance<SkillPreviewStage>();
            StageUtility.GoToStage(stage, false);
            Check(stage.scene.IsValid() && EditorSceneManager.IsPreviewScene(stage.scene), "independent preview Stage opened");
            var previewScene = stage.scene;
            var proxy = SkillVisualProxyBuilder.Build(source, stage.scene);
            Check(proxy.Root.GetComponentsInChildren<MonoBehaviour>(true).Length == 0, "no managed gameplay components in visual proxy");
            Check(proxy.Root.GetComponentsInChildren<Renderer>(true).Length > 0, "real model meshes are visible components");
            Check(proxy.Animator.runtimeAnimatorController == null && !proxy.Animator.fireEvents && !proxy.Animator.applyRootMotion, "Animator has no controller/events/root motion application");
            var instance = proxy.Root; proxy.Dispose(); proxy.Dispose();
            Check(!instance, "visual proxy disposal is idempotent");
            StageUtility.GoToMainStage(); if (stage) UnityEngine.Object.DestroyImmediate(stage);
            Check(!previewScene.IsValid(), "Stage destruction closes preview Scene");
            Check(scene.isDirty == dirty && EditorJsonUtility.ToJson(source) == before, "main Scene and source unchanged");
        }
        static void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL " + name); Debug.Log("PHASE2C_PASS " + name); }
        public static void Animation()
        {
            Safety();
            var type = typeof(SkillAssetValidator).Assembly.GetType("SkillConfig.Editor.SkillAnimationPreview");
            Check(type != null, "stateless animation sampler exists");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Model/雅/Model/星见雅.fbx");
            var scene = EditorSceneManager.NewPreviewScene();
            using (var proxy = SkillVisualProxyBuilder.Build(source, scene))
            using (var sampler = new SkillAnimationPreview(proxy))
            {
                foreach (int number in new[] { 1, 2 })
                {
                    var owner = AssetDatabase.LoadAssetAtPath<ComboData>(SkillPilotSetup.ComboPath(number));
                    var segment = owner.skill.animations[0];
                    string before = EditorJsonUtility.ToJson(segment.clip);
                    sampler.Sample(segment, 0); var zero = Pose(proxy);
                    sampler.Sample(segment, 5); var first = Pose(proxy);
                    sampler.Sample(segment, 10); sampler.Sample(segment, 5); var again = Pose(proxy);
                    Check(first.SequenceEqual(again), "Normal" + number + " repeated random Seek pose identical");
                    Check(!zero.SequenceEqual(first), "Normal" + number + " actually animates bones");
                    Check(sampler.ClipName == segment.clip.name && Math.Abs(sampler.SourceSeconds - .165) < .000001, "Normal" + number + " correct Clip and source time");
                    Check(before == EditorJsonUtility.ToJson(segment.clip), "original Clip untouched");
                }
                var original = AssetDatabase.LoadAssetAtPath<ComboData>(SkillPilotSetup.ComboPath(1)).skill.animations[0];
                var trimmed = new AnimationSegment { clip = original.clip, startFrame = 10, endFrame = 14, trimStartUs = 100000, trimEndUs = 300000, speedPermille = 2000 };
                Check(Math.Abs(SkillAnimationPreview.MapTime(trimmed, 12) - .232) < .000001, "Trim and PlaybackSpeed map to source time");
                Check(Math.Abs(SkillAnimationPreview.MapTime(trimmed, 14) - .3) < .000001, "endpoint clamps to trim end");
            }
            EditorSceneManager.ClosePreviewScene(scene);
            Check(!Resources.FindObjectsOfTypeAll<AnimationClip>().Any(c => c.name.StartsWith("SkillPreview::")), "temporary animation clones released");
        }
        static float[] Pose(SkillVisualProxy proxy) => proxy.Root.GetComponentsInChildren<Transform>(true).SelectMany(t => new[] { t.localPosition.x, t.localPosition.y, t.localPosition.z, t.localRotation.x, t.localRotation.y, t.localRotation.z, t.localRotation.w }).ToArray();
        public static void Motion()
        {
            Animation();
            Check(typeof(SkillAssetValidator).Assembly.GetType("SkillConfig.Editor.SkillRootMotionPreview") != null, "integer motion preview exists");
            foreach (int number in new[] { 1, 2 })
            {
                var source = AssetDatabase.LoadAssetAtPath<ComboData>(SkillPilotSetup.ComboPath(number));
                var owner = UnityEngine.Object.Instantiate(source);
                try
                {
                    var motion = new SkillRootMotionPreview(owner); Check(motion.Supported, "Normal" + number + " identity mapping supported");
                    var data = JsonUtility.FromJson<RootMotionJsonData>(owner.rootMotion.json.text);
                    FrameSync.RootMotion.RootMotionClip.TryCreate(data, 33, 10000, out var clip, out _);
                    var player = new FrameSync.RootMotion.RootMotionPlayback(); player.TryStart(clip, owner.rootMotion.endFrameExclusive, owner.rootMotion.distancePermille, 0, 10000, 10000);
                    long x = 0, z = 0;
                    for (int f = 0; f < motion.Duration; f++)
                    {
                        player.TryAdvance(out int dx, out int dz); x += dx; z += dz;
                        Check(motion.Delta(f).x == dx && motion.Delta(f).z == dz && motion.Position(f + 1).x == x && motion.Position(f + 1).z == z, "Normal" + number + " exact legacy delta / random prefix at " + f);
                    }
                    Check(motion.Diagnostics.Any(s => s.Contains("MOTION_METADATA")), "missing provenance warns");
                    owner.rootMotion.endFrameExclusive = 3; motion = new SkillRootMotionPreview(owner);
                    Check(motion.Position(3).x == motion.Position(motion.Duration).x && motion.Position(3).z == motion.Position(motion.Duration).z, "valid static tail holds last position");
                    owner.skill.animations[0].speedPermille = 2000; SkillEditCommands.Reflow(owner); motion = new SkillRootMotionPreview(owner);
                    Check(!motion.Supported && motion.Position(motion.Duration).x == 0 && motion.Position(motion.Duration).z == 0, "unsupported speed mapping disables trajectory");
                }
                finally { UnityEngine.Object.DestroyImmediate(owner); }
            }
        }
        public static void Session()
        {
            Motion();
            Check(typeof(SkillAssetValidator).Assembly.GetType("SkillConfig.Editor.SkillPreviewSession") != null, "owned preview session exists");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Model/雅/Model/星见雅.fbx");
            var owner = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ComboData>(SkillPilotSetup.ComboPath(1)));
            var second = AssetDatabase.LoadAssetAtPath<ComboData>(SkillPilotSetup.ComboPath(2)).skill.animations[0];
            owner.skill.animations.Add(new AnimationSegment { id = "second", clip = second.clip, trimStartUs = second.trimStartUs, trimEndUs = second.trimEndUs, speedPermille = 1000 });
            SkillEditCommands.Reflow(owner);
            var beforeScene = SceneManager.GetActiveScene(); bool dirty = beforeScene.isDirty;
            using (var session = new SkillPreviewSession(owner, null, source))
            {
                Check(string.IsNullOrEmpty(session.Error), "session initial sample works");
                int boundary = owner.skill.animations[1].startFrame;
                session.Seek(boundary); Check(session.ClipName == second.clip.name && session.SourceSeconds == 0, "multi Clip switches exactly at half-open boundary");
                session.Seek(0); session.PreviewSpeed = .5f; session.SetPlaying(true); session.Advance(.066);
                Check(session.Frame == 1 && owner.skill.animations[0].speedPermille == 1000, "preview speed leaves skill speed and 33ms step unchanged");
                session.SetPlaying(false); session.Seek(7); var direct = session.Proxy.Root.transform.position;
                session.Seek(0); for (int i = 1; i <= 7; i++) session.Seek(i);
                Check(direct == session.Proxy.Root.transform.position, "sequential and random Seek root positions equal");
                session.Interrupt(7); session.Seek(18);
                Check(session.Frame == 7 && session.Proxy.Root.transform.position == direct && !session.Active(new HitBox { startFrame = 0, endFrame = 20 }), "interruption freezes position and disables attack visual intervals");
                session.ClearInterrupt(); session.Loop = true; session.Seek(session.Duration - 1); session.PreviewSpeed = 1; session.SetPlaying(true); session.Advance(.033);
                Check(session.Frame == 0, "loop resets to origin frame");
                session.SetPlaying(false); session.Seek(5); var pose = Pose(session.Proxy);
                var boundClip = owner.skill.animations[0].clip; owner.skill.animations[0].clip = null; session.Seek(5);
                Check(!string.IsNullOrEmpty(session.Error) && !session.Proxy.Root.activeSelf, "invalid segment hides stale pose");
                owner.skill.animations[0].clip = boundClip; session.Seek(5);
                Check(string.IsNullOrEmpty(session.Error) && Pose(session.Proxy).SequenceEqual(pose), "valid Seek after error restores exact pose");
            }
            Check(SkillPreviewSession.LiveCount == 0 && !Resources.FindObjectsOfTypeAll<SkillPreviewStage>().Any(), "session closes Stage and unregisters ownership");
            Check(!Resources.FindObjectsOfTypeAll<GameObject>().Any(g => g.name.StartsWith("SkillPreview::")), "session leaves no visual roots/lights");
            Check(!Resources.FindObjectsOfTypeAll<AnimationClip>().Any(c => c.name.StartsWith("SkillPreview::")), "session leaves no clip clones");
            Check(beforeScene.isDirty == dirty, "session does not dirty original Scene");
            UnityEngine.Object.DestroyImmediate(owner);
        }
        public static void Shapes()
        {
            Session();
            Check(typeof(SkillAssetValidator).Assembly.GetType("SkillConfig.Editor.SkillShapeEdit") != null, "temporary shape transaction exists");
            var owner = ScriptableObject.CreateInstance<ComboData>(); owner.skill = new SkillAuthoringData();
            var catalog = ScriptableObject.CreateInstance<CharacterSkillCatalog>(); catalog.hurtBoxConfigured = true; catalog.hurtBox.radius = 5000; catalog.hurtBox.height = 18000;
            using (var edit = new SkillShapeEdit())
            {
                foreach (ShapeKind shape in Enum.GetValues(typeof(ShapeKind)))
                {
                    owner.skill.hitBoxes.Clear(); owner.skill.hitBoxes.Add(new HitBox { id = "shape", shape = shape, startFrame = 2, endFrame = 7 });
                    edit.BeginBox(owner, "shape"); edit.Box.offset = SkillShapeEdit.Quantize(new Vector3(1.23456f, -.12346f, 2)); edit.Box.size = new Int3 { x = 12000, y = 20000, z = 10000 };
                    edit.Box.radius = 6000; edit.Box.height = 22000; edit.Box.yawMilliDegrees = SkillShapeEdit.Quantize(45.125f, 1000); edit.Box.sectorAngleMilliDegrees = 90000;
                    Check(owner.skill.hitBoxes[0].offset.x == 0, shape + " drag draft does not mutate SO");
                    edit.Commit(); var b = owner.skill.hitBoxes[0];
                    Check(b.offset.x == 12346 && b.offset.y == -1235 && b.yawMilliDegrees == 45125 && b.endFrame == 7, shape + " commits quantized geometry without timing changes");
                    Undo.PerformUndo(); Check(owner.skill.hitBoxes[0].offset.x == 0, shape + " Undo"); Undo.PerformRedo();
                    Check(owner.skill.hitBoxes[0].offset.x == 12346, shape + " Redo");
                    edit.BeginBox(owner, "shape"); edit.Box.radius = 123; edit.Cancel(); Check(owner.skill.hitBoxes[0].radius == 6000, shape + " cancel discards draft");
                }
                edit.BeginHurt(catalog); edit.Hurt.height = 25000; edit.Commit(); Check(catalog.hurtBox.height == 25000, "HurtBox commit");
                Undo.PerformUndo(); Check(catalog.hurtBox.height == 18000, "HurtBox Undo");
            }
            UnityEngine.Object.DestroyImmediate(owner); UnityEngine.Object.DestroyImmediate(catalog);
        }
        public static void Integration()
        {
            Shapes();
            Directory.CreateDirectory("Assets/Phase2CChecks"); AssetDatabase.Refresh();
            var go = new GameObject("UnsafeSource"); go.AddComponent<Animator>(); go.AddComponent<SkillPreviewSideEffectProbe>();
            var bone = new GameObject("Bone"); bone.transform.SetParent(go.transform, false);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, "Assets/Phase2CChecks/Source.prefab"); UnityEngine.Object.DestroyImmediate(go);
            SkillPreviewSideEffectProbe.LifecycleCalls = 0; SkillPreviewSideEffectProbe.EventCalls = 0;
            var scene = EditorSceneManager.NewPreviewScene();
            using (var proxy = SkillVisualProxyBuilder.Build(prefab, scene))
            using (var sampler = new SkillAnimationPreview(proxy))
            {
                Check(SkillPreviewSideEffectProbe.LifecycleCalls == 0 && proxy.Root.GetComponentsInChildren<MonoBehaviour>(true).Length == 0, "building source with ExecuteAlways never instantiates its scripts");
                proxy.Animator.gameObject.AddComponent<SkillPreviewSideEffectProbe>(); // Sentinel receiver, test only.
                var clip = new AnimationClip { name = "DangerousEvent" };
                clip.SetCurve("Bone", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0, 0, 1, 2));
                AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { functionName = "Attack", time = .1f } });
                var segment = new AnimationSegment { clip = clip, startFrame = 0, endFrame = 31, trimEndUs = 1000000, speedPermille = 1000 };
                for (int f = 0; f < 10; f++) sampler.Sample(segment, f);
                Check(SkillPreviewSideEffectProbe.EventCalls == 0, "attack AnimationEvent never executed during forward sampling");
                Check(Resources.FindObjectsOfTypeAll<AnimationClip>().Where(c => c.name.StartsWith("SkillPreview::")).All(c => AnimationUtility.GetAnimationEvents(c).Length == 0), "temporary clips contain zero AnimationEvents");
                Check(AnimationUtility.GetAnimationEvents(clip).Length == 1, "source AnimationEvent is preserved");
                UnityEngine.Object.DestroyImmediate(clip);
            }
            EditorSceneManager.ClosePreviewScene(scene);
            var first = AssetDatabase.LoadAssetAtPath<ComboData>(SkillPilotSetup.ComboPath(1));
            var owner = UnityEngine.Object.Instantiate(first); owner.skill.skillId = "phase2c-fixture";
            owner.skill.tracks.Clear(); owner.skill.hitBoxes.Clear(); owner.skill.hitGroups.Clear(); owner.skill.events.Clear();
            AssetDatabase.CreateAsset(owner, "Assets/Phase2CChecks/Skill.asset");
            var catalog = ScriptableObject.CreateInstance<CharacterSkillCatalog>(); catalog.characterId = "phase2c-fixture"; catalog.skills.Add(owner);
            catalog.animatorController = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(SkillPilotSetup.ControllerPath);
            catalog.hurtBoxConfigured = true; catalog.hurtBox.radius = 5000; catalog.hurtBox.height = 18000;
            AssetDatabase.CreateAsset(catalog, "Assets/Phase2CChecks/Catalog.asset");
            var track = SkillEditCommands.AddTrack(owner, TrackKind.HitBox);
            foreach (ShapeKind shape in Enum.GetValues(typeof(ShapeKind)))
            {
                var id = SkillEditCommands.AddItem(owner, track.id, 0);
                SkillEditCommands.Apply(owner, "fixture shape", () => owner.skill.hitBoxes.First(b => b.id == id).shape = shape);
                using (var edit = new SkillShapeEdit()) { edit.BeginBox(owner, id); edit.Box.offset = new Int3 { x = (int)shape * 12000, y = 10000 }; edit.Box.radius = 5500; edit.Box.height = 20000; edit.Commit(); }
            }
            SkillEditCommands.Flush();
            Check(!SkillConfigExporter.Export(catalog).Any(d => d.severity == DiagnosticSeverity.Error), "geometry edits save/export through unchanged Phase1 pipeline");
            Check(SkillConfigLoader.TryLoad(catalog.characterId, out var loaded, out _) && loaded.CopyData().skills[0].hitBoxes.Count == 5, "five shapes survive JSON readback");
            Gui();
        }
        public static void Gui()
        {
            var owner = AssetDatabase.LoadAssetAtPath<ComboData>("Assets/Phase2CChecks/Skill.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterSkillCatalog>("Assets/Phase2CChecks/Catalog.asset");
            Check(owner && owner.skill.hitBoxes.Count == 5 && catalog.hurtBoxConfigured, "persisted geometry fixture loads");
            SkillEditorWindow.OpenSkill(owner); var window = EditorWindow.GetWindow<SkillEditorWindow>(); window.position = new Rect(100, 100, 1500, 950);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            window.SendEvent(new Event { type = EventType.Layout }); window.SendEvent(new Event { type = EventType.Repaint });
            var panel = (SkillPreviewPanel)typeof(SkillEditorWindow).GetField("preview", flags).GetValue(window);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Model/雅/Model/星见雅.fbx");
            var failures = new List<string>();
            Application.LogCallback capture = (message, stack, type) => { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) failures.Add(message); };
            Application.logMessageReceived += capture;
            try
            {
                var session = new SkillPreviewSession(owner, catalog, source);
                typeof(SkillPreviewPanel).GetField("session", flags).SetValue(panel, session);
                var sceneView = EditorWindow.GetWindow<SceneView>();
                foreach (var box in owner.skill.hitBoxes)
                {
                    typeof(SkillEditorWindow).GetField("selected", flags).SetValue(window, box.id);
                    window.SendEvent(new Event { type = EventType.Layout }); window.SendEvent(new Event { type = EventType.Repaint });
                    sceneView.SendEvent(new Event { type = EventType.Layout }); sceneView.SendEvent(new Event { type = EventType.Repaint });
                }
                typeof(SkillPreviewPanel).GetField("hurt", flags).SetValue(panel, true);
                sceneView.SendEvent(new Event { type = EventType.Layout }); sceneView.SendEvent(new Event { type = EventType.Repaint });
                var replacement = new SkillPreviewSession(owner, catalog, source);
                Check(session.IsDisposed && SkillPreviewSession.LiveCount == 1, "opening replacement preview disposes previous session"); replacement.Dispose();
                var closing = new SkillPreviewSession(owner, catalog, source);
                typeof(SkillPreviewPanel).GetField("session", flags).SetValue(panel, closing);
                window.Close();
                Check(closing.IsDisposed && SkillPreviewSession.LiveCount == 0, "closing window disposes active preview");
            }
            finally { window.Close(); SkillPreviewSession.DisposeAll(); Application.logMessageReceived -= capture; }
            Check(failures.Count == 0, "Scene/Inspector five Handles and HurtBox GUI smoke: " + string.Join("; ", failures));
            Check(SkillPreviewSession.LiveCount == 0 && !Resources.FindObjectsOfTypeAll<SkillPreviewStage>().Any(), "window close leaves no session/Stage");
        }
    }
}
