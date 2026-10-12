using System;
using System.IO;
using System.Linq;
using SkillConfig;
using SkillConfig.Editor;
using SkillRuntime.Integration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PilotArenaSetup
{
    public const string ProfilePath = "Assets/ScriptableObject/SkillConfigs/MiyabiPhase4A_Development.asset";
    public const string ScenePath = "Assets/Scenes/SkillRuntimePilotArena.unity";
    [MenuItem("Tools/Skill Runtime/Prepare Phase 4A Arena")]
    public static void Prepare()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var catalog = AssetDatabase.LoadAssetAtPath<CharacterSkillCatalog>(SkillPilotSetup.CatalogPath);
        var combos = new[] { AssetDatabase.LoadAssetAtPath<ComboData>(SkillPilotSetup.ComboPath(1)), AssetDatabase.LoadAssetAtPath<ComboData>(SkillPilotSetup.ComboPath(2)) };
        foreach (var combo in combos) SeedVerifiedGates(combo);
        var diagnostics = SkillConfigExporter.Export(catalog);
        if (diagnostics.Any(d => d.severity == DiagnosticSeverity.Error)) throw new InvalidOperationException(string.Join("\n", diagnostics));
        var profile = AssetDatabase.LoadAssetAtPath<PilotBattleProfile>(ProfilePath);
        if (!profile) { profile = ScriptableObject.CreateInstance<PilotBattleProfile>(); AssetDatabase.CreateAsset(profile, ProfilePath); }
        var prefab = Resources.Load<GameObject>("BattleScene/Role/Character");
        profile.catalog = catalog; profile.allowedCombos = combos;
        profile.comboContainer = prefab.GetComponentInChildren<ZZZ.Player>(true).playerSO.ComboData.comboData.lightCombo;
        profile.exportedJson = AssetDatabase.LoadAssetAtPath<TextAsset>(SkillConfigExporter.PathFor(catalog));
        var states = SkillAssetValidator.States((UnityEditor.Animations.AnimatorController)catalog.animatorController);
        profile.idleState = states.Keys.Single(s => s.EndsWith(".Locomotion", StringComparison.Ordinal));
        profile.hitState = states.Keys.Single(s => s.EndsWith(".正面受击1", StringComparison.Ordinal));
        // Existing fall animation used for basic KO pose only; no AnimatorController edits or old death callback.
        profile.deathState = states.Keys.Single(s => s.EndsWith(".Avatar_Female_Size02_Unagi_Ani_HitFly_Front", StringComparison.Ordinal));
        profile.sealedJsonHash = SkillConfigHash.Sha256(profile.exportedJson.text); profile.sealedSourceHash = profile.SourceHash();
        EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
        profile.ValidateAndFreeze();
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var arena = new GameObject("Phase 4A Development Arena").AddComponent<PilotDevelopmentArena>(); arena.profile = profile;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.name = "Development arena floor"; floor.transform.localScale = new Vector3(4, 1, 4);
        var light = new GameObject("Arena light").AddComponent<Light>(); light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(45, 20, 0);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[PilotSetup] Ready scene=" + ScenePath + " profile=" + ProfilePath + " gates are saved in the ORIGINAL ComboData.skill; no parallel definitions.");
    }
    internal static void SeedVerifiedGates(ComboData combo)
    {
        if (!combo || combo.skill == null || combo.skill.animations.Count != 1) throw new InvalidOperationException("Invalid pilot source");
        var animation = combo.skill.animations[0];
        if (!animation.clip || animation.trimStartUs != 0 || animation.speedPermille != 1000) throw new InvalidOperationException("Pilot gate migration requires identity source time.");
        string[] functions = { "EnablePreInput", "CancelAttackColdTime", "DisableLinkCombo", "EnableMoveInterrupt" };
        string[] ids = { "combo.preinput", "combo.attack-ready", "combo.link", "combo.move-interrupt" };
        var proposals = functions.Select((fn, i) => {
            var found = AnimationUtility.GetAnimationEvents(animation.clip).Where(e => e.functionName == fn).OrderBy(e => e.time).ToArray();
            if (found.Length == 0) throw new InvalidOperationException("Actual clip missing " + fn);
            long us = (long)Math.Round(found[0].time * 1000000.0, MidpointRounding.AwayFromZero);
            int frame = (int)((us + NetConfig.frameTime * 1000L - 1) / (NetConfig.frameTime * 1000L));
            Debug.Log("[PilotGateSource] " + combo.name + " " + fn + " seconds=" + found[0].time.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " -> k=" + frame);
            return new SkillEvent { id = "pilot-gate-" + i, trackId = "pilot-gates", eventId = ids[i], frame = frame, order = i, execution = EventExecution.Authoritative,
                parameterKind = EventParameterKind.ComboGate, comboGate = new ComboGateParameters { enabled = i != 2 } };
        }).ToArray();
        int duration = animation.endFrame;
        foreach (var p in proposals)
            if (p.frame >= duration && !(p.eventId == "combo.link" && p.frame == duration))
                throw new InvalidOperationException("Gate lies outside the accepted skill duration: " + combo.name + "/" + p.eventId);
        foreach (var p in proposals)
        {
            var existing = combo.skill.events.Where(e => e.eventId == p.eventId).ToArray();
            if (existing.Length > 1 || (existing.Length == 1 && (existing[0].frame != p.frame || existing[0].comboGate.enabled != p.comboGate.enabled)))
                throw new InvalidOperationException("Existing authored gate differs from clip; do not silently overwrite: " + combo.name + "/" + p.eventId);
        }
        Undo.RecordObject(combo, "Seed verified pilot logic gates");
        // Closing at D is the cast's normal exit, not an event in [0,D). Never clamp it into D-1.
        foreach (var p in proposals.Where(p => p.frame == duration))
            combo.skill.events.RemoveAll(e => e.id == p.id && e.eventId == p.eventId && e.frame == duration);
        if (!combo.skill.tracks.Any(t => t.id == "pilot-gates")) combo.skill.tracks.Add(new SkillTrack { id = "pilot-gates", name = "Imported logic gates (33ms)", kind = TrackKind.Event });
        foreach (var p in proposals.Where(p => p.frame < duration)) if (!combo.skill.events.Any(e => e.eventId == p.eventId)) combo.skill.events.Add(p);
        EditorUtility.SetDirty(combo); AssetDatabase.SaveAssetIfDirty(combo);
    }
    public static void RunBatch()
    {
        try
        {
            Prepare(); var arena = UnityEngine.Object.FindObjectOfType<PilotDevelopmentArena>(); arena.produceLoopbackFrames = false; arena.automatedChecks = true;
            EditorApplication.isPlaying = true;
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }
}
