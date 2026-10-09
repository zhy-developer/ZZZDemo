using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SkillConfig.Editor.Tests
{
    public static class SkillEditorPhase2BChecks
    {
        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Temp/SkillEditorPhase2B/")) throw new Exception("Use the isolated Phase2B harness; never run mutation tests on production assets.");
            var commands = typeof(SkillAssetValidator).Assembly.GetType("SkillConfig.Editor.SkillEditCommands");
            if (commands == null) throw new Exception("FAIL: Phase 2B edit commands are missing");
            Debug.Log("PHASE2B command service exists");
            Check(SkillTimelineAdapters.OutOfRange(new SkillTimelineItem { start = 5, end = 4, tail = true }, 20), "tail still requires a positive interval");
            var owner = ScriptableObject.CreateInstance<ComboData>();
            owner.skill = new SkillAuthoringData { registered = true, skillId = "test-skill", displayName = "before" };
            try
            {
                Action change = () => owner.skill.displayName = "after";
                commands.GetMethod("Apply").Invoke(null, new object[] { owner, "Test edit", change });
                Check(owner.skill.displayName == "after", "command applies edit");
                Undo.PerformUndo(); Check(owner.skill.displayName == "before", "Undo restores before mutation");
                Undo.PerformRedo(); Check(owner.skill.displayName == "after", "Redo restores edit");
                foreach (TrackKind kind in Enum.GetValues(typeof(TrackKind)))
                {
                    var track = (SkillTrack)commands.GetMethod("AddTrack").Invoke(null, new object[] { owner, kind });
                    string id = (string)commands.GetMethod("AddItem").Invoke(null, new object[] { owner, track.id, 2 });
                    Check(!string.IsNullOrEmpty(id), "create " + kind);
                    commands.GetMethod("DeleteItem").Invoke(null, new object[] { owner, id });
                    Undo.PerformUndo();
                    var adapters = commands.Assembly.GetType("SkillConfig.Editor.SkillTimelineAdapters");
                    Check(adapters.GetMethod("Find").Invoke(null, new object[] { owner, id }) != null, "delete Undo " + kind);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
            Integration();
        }
        static void Integration()
        {
            SkillConfigPhase1Checks.RunAcceptanceRevision();
            var source = AssetDatabase.LoadAssetAtPath<CharacterSkillCatalog>(SkillPilotSetup.CatalogPath);
            Directory.CreateDirectory("Assets/Phase2BChecks"); AssetDatabase.Refresh();
            var owner = UnityEngine.Object.Instantiate(source.skills[0]);
            owner.skill.skillId = "phase2b-check-skill";
            AssetDatabase.CreateAsset(owner, "Assets/Phase2BChecks/Skill.asset");
            var catalog = ScriptableObject.CreateInstance<CharacterSkillCatalog>();
            catalog.characterId = "phase2b-checks"; catalog.animatorController = source.animatorController; catalog.skills.Add(owner);
            AssetDatabase.CreateAsset(catalog, "Assets/Phase2BChecks/Catalog.asset");
            var originalClip = owner.skill.animations[0].clip;
            string second = SkillEditCommands.AddAnimation(owner, originalClip);
            SkillEditCommands.Apply(owner, "bind second clip", () => owner.skill.animations[1].statePaths.AddRange(owner.skill.animations[0].statePaths));
            Check(owner.skill.animations[1].startFrame == owner.skill.animations[0].endFrame, "animation contiguous after insert");
            Undo.PerformUndo(); Undo.PerformUndo();
            Check(owner.skill.animations.Count == 1, "animation insert Undo");
            var tracks = new Dictionary<TrackKind, SkillTrack>();
            foreach (TrackKind kind in Enum.GetValues(typeof(TrackKind))) tracks[kind] = SkillEditCommands.AddTrack(owner, kind);
            string hit = SkillEditCommands.AddItem(owner, tracks[TrackKind.HitBox].id, 2);
            SkillEditCommands.SetRange(owner, hit, 3, 8);
            Check(owner.skill.hitBoxes.Single().startFrame == 3 && owner.skill.hitBoxes.Single().endFrame == 8, "range edit persists");
            Undo.PerformUndo(); Check(owner.skill.hitBoxes.Single().startFrame == 2, "range Undo"); Undo.PerformRedo();
            int priorEnd = owner.skill.hitBoxes[0].endFrame;
            SkillEditCommands.Apply(owner, "speed", () => { owner.skill.animations[0].speedPermille = 2000; SkillEditCommands.Reflow(owner); });
            Check(owner.skill.animations[0].endFrame == 10 && owner.skill.hitBoxes[0].endFrame == priorEnd, "speed reflows animation only");
            Undo.PerformUndo();
            SkillEditCommands.AddItem(owner, tracks[TrackKind.Event].id, 18);
            SkillEditCommands.Apply(owner, "shorten", () => { owner.skill.animations[0].speedPermille = 2000; SkillEditCommands.Reflow(owner); });
            SkillAssetValidator.Build(catalog, out var invalidEvent);
            Check(invalidEvent.Any(d => d.code == "EVENT" && d.severity == DiagnosticSeverity.Error), "shortening invalidates late event");
            Undo.PerformUndo();
            Directory.CreateDirectory("Assets/Resources/Phase2BChecks"); AssetDatabase.Refresh();
            var go = new GameObject("effect"); var prefab = PrefabUtility.SaveAsPrefabAsset(go, "Assets/Resources/Phase2BChecks/Effect.prefab"); UnityEngine.Object.DestroyImmediate(go);
            var audio = AudioClip.Create("sound", 100, 1, 1000, false); AssetDatabase.CreateAsset(audio, "Assets/Resources/Phase2BChecks/Sound.asset");
            SkillEditCommands.Apply(catalog, "resources", () => { catalog.resources.Add(new SkillResourceBinding { id = "effect", kind = ResourceKind.VfxPrefab, asset = prefab }); catalog.resources.Add(new SkillResourceBinding { id = "sound", kind = ResourceKind.AudioClip, asset = audio }); });
            string vfx = SkillEditCommands.AddItem(owner, tracks[TrackKind.Vfx].id, 19);
            SkillEditCommands.Apply(owner, "auto tail", () => { var v = owner.skill.vfx[0]; v.resourceId = "effect"; v.lifetime = VfxLifetime.AutoComplete; v.maxLifetimeFrames = 90; });
            SkillEditCommands.AddItem(owner, tracks[TrackKind.Sfx].id, 19);
            SkillEditCommands.Apply(owner, "sound tail", () => { var s = owner.skill.sfx[0]; s.resourceId = "sound"; s.endFrame = 50; s.onInterrupt = SoundInterrupt.PlayToEnd; });
            SkillEditCommands.AddItem(owner, tracks[TrackKind.Invincibility].id, 0);
            SkillEditCommands.AddItem(owner, tracks[TrackKind.Interrupt].id, 0);
            SkillEditCommands.Flush();
            SkillAssetValidator.Build(catalog, out var valid);
            Check(!valid.Any(d => d.severity == DiagnosticSeverity.Error), "all types valid including AutoComplete and sound tails");
            Check(!SkillTimelineAdapters.OutOfRange(SkillTimelineAdapters.Find(owner, vfx), 20), "AutoComplete tail beyond cast is not range error");
            var exported = SkillConfigExporter.Export(catalog);
            Check(!exported.Any(d => d.severity == DiagnosticSeverity.Error), "Warning export succeeds");
            string path = SkillConfigExporter.PathFor(catalog), json = File.ReadAllText(path);
            Check(!SkillConfigExporter.IsStale(catalog), "export clean");
            Check(SkillConfigLoader.TryLoad(catalog.characterId, out var loaded, out _) && loaded.CopyData().skills[0].events.Count == 1, "Resources loader roundtrip");
            var props = new SerializedObject(owner); props.Update(); props.FindProperty("_comboDamage").floatValue += 1;
            SkillEditCommands.ApplyProperties(props, false); Check(SkillConfigExporter.IsStale(catalog), "legacy damage stale");
            Undo.PerformUndo(); SkillEditCommands.Flush(); Check(!SkillConfigExporter.IsStale(catalog), "Undo restores export consistency");
            SkillEditCommands.Apply(owner, "invalid speed", () => owner.skill.animations[0].speedPermille = 0);
            Check(SkillConfigExporter.Export(catalog).Any(d => d.severity == DiagnosticSeverity.Error) && File.ReadAllText(path) == json, "invalid export preserves old bytes");
            Undo.PerformUndo(); SkillEditCommands.Flush();
            SkillEditCommands.DeleteTrack(owner, tracks[TrackKind.HitBox].id);
            Check(owner.skill.hitBoxes.Count == 0, "track delete removes owned items"); Undo.PerformUndo();
            Check(owner.skill.hitBoxes.Count == 1, "track delete Undo restores references");
            SkillEditCommands.DeleteGroup(owner, owner.skill.hitGroups[0].id);
            Check(owner.skill.hitBoxes.Count == 0 && owner.skill.hitGroups.Count == 0, "group cascade delete"); Undo.PerformUndo();
            Check(owner.skill.hitBoxes[0].hitGroupId == owner.skill.hitGroups[0].id, "group Undo restores references");
            SkillEditCommands.Apply(owner, "stable edit", () => owner.skill.displayName = "Phase2B Reopen"); SkillEditCommands.Flush();
            SkillConfigExporter.Export(catalog);
            var window = EditorWindow.GetWindow<SkillEditorWindow>(); window.Close();
            Check(owner.skill.displayName == "Phase2B Reopen", "window close keeps authoring values");
            Debug.Log("PHASE2B_INTEGRATION_COMPLETE");
        }
        public static void VerifyReopen()
        {
            var owner = AssetDatabase.LoadAssetAtPath<ComboData>("Assets/Phase2BChecks/Skill.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterSkillCatalog>("Assets/Phase2BChecks/Catalog.asset");
            Check(owner && owner.skill.displayName == "Phase2B Reopen" && owner.skill.skillId == "phase2b-check-skill", "new Unity process stable ID and saved fields");
            Check(owner.skill.hitBoxes.Count == 1 && owner.skill.events.Count == 1 && owner.skill.vfx[0].maxLifetimeFrames == 90, "all saved list references survive reopen");
            Check(SkillConfigLoader.TryLoad(catalog.characterId, out _, out _), "reopen Resources load");
            Check(!SkillConfigExporter.IsStale(catalog), "reopen export consistency");
        }
        public static void GuiSmoke()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Temp/SkillEditorPhase2B/")) throw new Exception("Use isolated harness");
            var owner = AssetDatabase.LoadAssetAtPath<ComboData>("Assets/Phase2BChecks/Skill.asset");
            if (!owner) throw new Exception("Run integration checks first");
            var failures = new List<string>();
            Application.LogCallback capture = (message, stack, type) => { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) failures.Add(message); };
            Application.logMessageReceived += capture;
            SkillEditorWindow window = null;
            try
            {
                SkillEditorWindow.OpenSkill(owner); window = EditorWindow.GetWindow<SkillEditorWindow>(); window.position = new Rect(100, 100, 1400, 800);
                var selection = typeof(SkillEditorWindow).GetField("selected", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var ids = new[] { "", "@catalog" }.Concat(SkillTimelineAdapters.Items(owner).Select(i => i.id)).Concat(owner.skill.tracks.Select(t => "track:" + t.id)).Concat(owner.skill.hitGroups.Select(g => "group:" + g.id));
                foreach (string id in ids)
                {
                    selection.SetValue(window, id);
                    window.SendEvent(new Event { type = EventType.Layout }); window.SendEvent(new Event { type = EventType.Repaint });
                }
            }
            finally { if (window) window.Close(); Application.logMessageReceived -= capture; }
            Check(failures.Count == 0, "graphics IMGUI layout/repaint every item inspector: " + string.Join("; ", failures));
        }
        static void Check(bool value, string label) { if (!value) throw new Exception("FAIL: " + label); Debug.Log("PHASE2B_PASS " + label); }
    }
}
