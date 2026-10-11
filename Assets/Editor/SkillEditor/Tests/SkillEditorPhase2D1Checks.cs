using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SkillConfig.Editor.Tests
{
    public static class SkillEditorPhase2D1Checks
    {
        static void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL " + name); Debug.Log("PHASE2D1_PASS " + name); }
        static void Good(string error) { Check(error == null, "operation validates: " + error); }
        public static void Run()
        {
            if (!Application.dataPath.Replace('\\','/').Contains("/Temp/SkillEditorPhase2C/")) throw new Exception("Isolated harness only");
            var owner = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ComboData>(SkillPilotSetup.ComboPath(1)));
            owner.skill.skillId = "batch-" + Guid.NewGuid().ToString("N");
            var catalog = ScriptableObject.CreateInstance<CharacterSkillCatalog>();
            catalog.characterId = "phase2d1-" + Guid.NewGuid().ToString("N"); catalog.animatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(SkillPilotSetup.ControllerPath);
            catalog.skills.Clear(); catalog.skills.Add(owner);
            owner.skill.tracks.Clear(); owner.skill.hitBoxes.Clear(); owner.skill.hitGroups.Clear(); owner.skill.invincibility.Clear(); owner.skill.interrupts.Clear(); owner.skill.events.Clear(); owner.skill.vfx.Clear(); owner.skill.sfx.Clear();
            var track = SkillEditCommands.AddTrack(owner, TrackKind.Invincibility);
            string a = SkillEditCommands.AddItem(owner, track.id, 1), b = SkillEditCommands.AddItem(owner, track.id, 3);
            string before = JsonUtility.ToJson(owner.skill);
            Good(SkillTimelineBatch.Move(owner, catalog, new[] { a,b }, 2));
            Check(owner.skill.invincibility[0].startFrame == 3 && owner.skill.invincibility[1].startFrame == 5, "relative timing preserved");
            Undo.PerformUndo(); Check(JsonUtility.ToJson(owner.skill) == before, "single Undo restores whole batch");
            Undo.PerformRedo(); Check(owner.skill.invincibility[1].startFrame == 5, "single Redo restores whole batch");
            string valid = JsonUtility.ToJson(owner.skill);
            Check(SkillTimelineBatch.Move(owner, catalog, new[] { a,b }, -99) != null && JsonUtility.ToJson(owner.skill) == valid, "negative batch rejected atomically");
            Check(SkillTimelineBatch.Move(owner, catalog, new[] { a,b }, 100) != null && JsonUtility.ToJson(owner.skill) == valid, "out of duration rejected atomically");
            SkillTimelineBatch.Copy(owner, new[] { a,b });
            Good(SkillTimelineBatch.Paste(owner, catalog, 7, PasteGroups.Copy, out var ids));
            Check(ids.Length == 2 && ids.Distinct().Count() == 2 && !ids.Contains(a) && owner.skill.invincibility[3].startFrame == 9, "paste generates distinct IDs and relative times");
            Good(SkillTimelineBatch.Delete(owner, catalog, ids)); Check(owner.skill.invincibility.Count == 2, "batch delete");
            var hitTrack = SkillEditCommands.AddTrack(owner, TrackKind.HitBox);
            string hit = SkillEditCommands.AddItem(owner, hitTrack.id, 1);
            string group = owner.skill.hitBoxes[0].hitGroupId;
            SkillTimelineBatch.Copy(owner, new[] { hit });
            Good(SkillTimelineBatch.Paste(owner, catalog, 3, PasteGroups.Copy, out ids));
            Check(owner.skill.hitBoxes[1].hitGroupId == group && owner.skill.hitGroups.Count == 1, "same skill keeps existing HitGroup");
            var target = Object.Instantiate(owner); target.skill.skillId += "-target"; target.skill.hitBoxes.Clear(); target.skill.hitGroups.Clear(); catalog.skills.Add(target);
            string targetBefore = JsonUtility.ToJson(target.skill);
            Check(SkillTimelineBatch.Paste(target, catalog, 4, PasteGroups.ReuseEquivalent, out ids) != null && JsonUtility.ToJson(target.skill) == targetBefore, "missing equivalent group rejects without mutation");
            Good(SkillTimelineBatch.Paste(target, catalog, 4, PasteGroups.Copy, out ids));
            Check(target.skill.hitGroups.Count == 1 && target.skill.hitGroups[0].id != group && target.skill.hitBoxes[0].hitGroupId == target.skill.hitGroups[0].id, "cross skill copies valid shared group");
            Good(SkillTimelineBatch.Paste(target, catalog, 6, PasteGroups.ReuseEquivalent, out ids));
            Check(target.skill.hitGroups.Count == 1 && target.skill.hitBoxes.Count == 2, "equivalent group reused only on request");
            Check(owner.skill.hitGroups[0].id == group && owner.skill.hitBoxes.Count == 2, "source asset untouched by cross paste");
            var eventTrack = SkillEditCommands.AddTrack(target, TrackKind.Event);
            string eventId = SkillEditCommands.AddItem(target, eventTrack.id, 2);
            target.skill.events[0].order = 17;
            target.skill.events[0].marker.text = "typed payload";
            var interruptTrack = SkillEditCommands.AddTrack(target, TrackKind.Interrupt);
            string interruptId = SkillEditCommands.AddItem(target, interruptTrack.id, 1);
            var vfxTrack = SkillEditCommands.AddTrack(target, TrackKind.Vfx);
            string vfxId = SkillEditCommands.AddItem(target, vfxTrack.id, 18);
            target.skill.vfx[0].resourceId = "missing-test-vfx"; target.skill.vfx[0].lifetime = VfxLifetime.AutoComplete; target.skill.vfx[0].maxLifetimeFrames = 30;
            SkillTimelineBatch.Copy(target, new[] { eventId, interruptId, vfxId });
            string invalidBefore = JsonUtility.ToJson(target.skill);
            Check(SkillTimelineBatch.Paste(target, catalog, 2, PasteGroups.Copy, out ids) != null && JsonUtility.ToJson(target.skill) == invalidBefore, "missing required resource rejects mixed paste atomically");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Phase2D1")) AssetDatabase.CreateFolder("Assets/Resources", "Phase2D1");
            var effect = new GameObject("Test effect");
            var prefab = PrefabUtility.SaveAsPrefabAsset(effect, "Assets/Resources/Phase2D1/Effect.prefab"); Object.DestroyImmediate(effect);
            catalog.resources.Add(new SkillResourceBinding { id = "missing-test-vfx", kind = ResourceKind.VfxPrefab, asset = prefab });
            Good(SkillTimelineBatch.Paste(target, catalog, 2, PasteGroups.Copy, out ids));
            Check(target.skill.events.Last().marker.text == "typed payload" && target.skill.events.Last().order == 17 && target.skill.vfx.Last().maxLifetimeFrames == 30 && target.skill.vfx.Last().startFrame == 19, "mixed typed event/window/VFX copy preserves payload and legal AutoComplete tail");
            Good(SkillTimelineBatch.Delete(target, catalog, ids));
            using (var stream = File.Create("Assets/Resources/Phase2D1/Tone.wav"))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + 882); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(44100); writer.Write(88200); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(882); writer.Write(new byte[882]);
            }
            AssetDatabase.ImportAsset("Assets/Resources/Phase2D1/Tone.wav", ImportAssetOptions.ForceSynchronousImport);
            catalog.resources.Add(new SkillResourceBinding { id = "tone", kind = ResourceKind.AudioClip, asset = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Phase2D1/Tone.wav") });
            var soundTrack = SkillEditCommands.AddTrack(target, TrackKind.Sfx);
            string soundId = SkillEditCommands.AddItem(target, soundTrack.id, 2);
            var sound = target.skill.sfx[0]; sound.resourceId = "tone"; sound.endFrame = 8; sound.volumePermille = 430; sound.mode = SoundMode.Loop; sound.onInterrupt = SoundInterrupt.FadeOut; sound.fadeInFrames = 1; sound.fadeOutFrames = 2;
            SkillTimelineBatch.Copy(target, new[] { soundId });
            Good(SkillTimelineBatch.Paste(target, catalog, 4, PasteGroups.Copy, out ids));
            Check(target.skill.sfx[1].volumePermille == 430 && target.skill.sfx[1].fadeOutFrames == 2 && target.skill.sfx[1].endFrame == 10 && target.skill.sfx[1].mode == SoundMode.Loop, "SFX payload and duration survive copy");
            Good(SkillTimelineBatch.Delete(target, catalog, ids));
            string[] eventOrder = target.skill.events.Select(e => e.id).ToArray();
            Good(SkillTimelineBatch.ReorderTrack(target, catalog, eventTrack.id, -1));
            Check(eventOrder.SequenceEqual(target.skill.events.Select(e => e.id)) && target.skill.events[0].order == 17, "display order never sorts events or changes execution order");
            var anim = owner.skill.animations[0]; SkillTimelineBatch.Copy(owner, new[] { anim.id });
            Good(SkillTimelineBatch.Paste(target, catalog, SkillTimelineAdapters.Duration(target), PasteGroups.Copy, out ids));
            Check(target.skill.animations.Count == 2 && target.skill.animations[1].rootSource == RootBindingSource.Explicit && target.skill.animations[1].additionalRootMotion == owner.rootMotion.json, "cross animation preserves original motion binding");
            int dataStart = target.skill.hitBoxes[0].startFrame;
            Check(SkillTimelineBatch.Move(target, catalog, new[] { target.skill.animations[0].id }, 1) != null && target.skill.hitBoxes[0].startFrame == dataStart, "animation gap rejected; other events untouched");
            const string folder = "Assets/Phase2D1Checks";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "Phase2D1Checks");
            AssetDatabase.DeleteAsset(folder + "/Skill.asset"); AssetDatabase.CreateAsset(target, folder + "/Skill.asset"); AssetDatabase.SaveAssets();
            string stable = target.skill.hitBoxes[0].id;
            Directory.CreateDirectory("Library/SkillEditor");
            File.WriteAllText("Library/SkillEditor/Phase2D1ExpectedId.txt", stable);
            SkillEditCommands.Apply(target, "Set test track group", () => target.skill.tracks[0].group = "Persisted test group");
            SkillEditCommands.Flush();
            var workspace = SkillEditorWorkspaceState.instance;
            string trackKey = AssetDatabase.AssetPathToGUID(folder + "/Skill.asset") + ":" + target.skill.tracks[0].id;
            workspace.collapsed.Add(trackKey); workspace.hiddenTracks.Add(trackKey); workspace.Persist();
            AssetDatabase.ImportAsset(folder + "/Skill.asset", ImportAssetOptions.ForceUpdate);
            Check(AssetDatabase.LoadAssetAtPath<ComboData>(folder + "/Skill.asset").skill.hitBoxes[0].id == stable, "saved item ID stable after import");
            catalog.skills.Clear(); catalog.skills.Add(target);
            AssetDatabase.DeleteAsset(folder + "/Catalog.asset"); AssetDatabase.CreateAsset(catalog, folder + "/Catalog.asset");
            var exported = SkillConfigExporter.Export(catalog);
            Check(!exported.Any(d => d.severity == DiagnosticSeverity.Error) && exported.Any(d => d.severity == DiagnosticSeverity.Warning), "Warnings allow JSON export");
            string json = File.ReadAllText(SkillConfigExporter.PathFor(catalog));
            Good(SkillTimelineBatch.Move(target, catalog, new[] { stable }, 1));
            Check(SkillConfigExporter.IsStale(catalog) && File.ReadAllText(SkillConfigExporter.PathFor(catalog)) == json, "persistent owner identity preserved; changed batch marks stale without exporting");
            Undo.PerformUndo(); Check(!SkillConfigExporter.IsStale(catalog), "Undo restores export consistency");
            var duplicate = Object.Instantiate(target); AssetDatabase.DeleteAsset(folder + "/Duplicate.asset"); AssetDatabase.CreateAsset(duplicate, folder + "/Duplicate.asset");
            string unchanged = JsonUtility.ToJson(target.skill);
            Check(SkillTimelineBatch.Move(target, catalog, new[] { stable }, 1) != null && JsonUtility.ToJson(target.skill) == unchanged, "draft validation still rejects unrelated duplicate global SkillID");
            AssetDatabase.DeleteAsset(folder + "/Duplicate.asset");
            Check(SkillTimelineBatch.Move(target, catalog, new[] { stable }, -99) != null && File.ReadAllText(SkillConfigExporter.PathFor(catalog)) == json, "rejected batch never overwrites existing JSON");
            SkillEditCommands.Flush(); AssetDatabase.SaveAssets();
            Object.DestroyImmediate(owner);
        }
        public static void Reopen()
        {
            var owner = AssetDatabase.LoadAssetAtPath<ComboData>("Assets/Phase2D1Checks/Skill.asset");
            Check(owner && owner.skill.animations.Count == 2 && owner.skill.hitBoxes.Count == 2, "fresh Unity process reopens batch data");
            Check(owner.skill.hitBoxes.All(b => owner.skill.hitGroups.Any(g => g.id == b.hitGroupId)), "saved HitGroup references valid");
            Check(owner.skill.hitBoxes[0].id == File.ReadAllText("Library/SkillEditor/Phase2D1ExpectedId.txt"), "stable IDs identical after fresh process");
            string key = AssetDatabase.AssetPathToGUID("Assets/Phase2D1Checks/Skill.asset") + ":" + owner.skill.tracks[0].id;
            Check(owner.skill.tracks[0].group == "Persisted test group" && SkillEditorWorkspaceState.instance.collapsed.Contains(key) && SkillEditorWorkspaceState.instance.hiddenTracks.Contains(key), "group and Library visibility/fold state persist across restart");
        }
    }
}
