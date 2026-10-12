using System;
using System.Linq;
using SkillConfig;
using SkillConfig.Editor;
using SkillRuntime.Integration;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class Phase4BSetup
{
    [MenuItem("Tools/Skill Runtime/Prepare Phase 4B approved Normal3-5")]
    public static void Prepare()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var profile = AssetDatabase.LoadAssetAtPath<PilotBattleProfile>(PilotArenaSetup.ProfilePath);
        if (!profile) throw new InvalidOperationException("Existing accepted profile required.");
        var catalog = profile.catalog;
        var states = SkillAssetValidator.States((AnimatorController)catalog.animatorController);
        Undo.RecordObject(catalog, "Register approved Normal3-5");
        for (int n = 3; n <= 5; n++)
        {
            var combo = profile.comboContainer.comboDatas[n - 1];
            if (combo.name != "Unagi_Normal_" + n) throw new InvalidOperationException("Container changed; recheck approved migration source.");
            if (combo.skill != null && combo.skill.registered)
            {
                if (!catalog.skills.Contains(combo)) throw new InvalidOperationException("Registered skill belongs outside this catalog; do not silently reassign.");
                continue;
            }
            var state = states.Single(p => p.Key.EndsWith("." + combo.comboName, StringComparison.Ordinal));
            if (!(state.Value is AnimationClip clip)) throw new InvalidOperationException("Explicit single Clip expected for approved Normal3-5 migration.");
            long length = (long)Math.Round(clip.length * 1000000.0, MidpointRounding.AwayFromZero);
            int duration = (int)((length + 32999) / 33000);
            var attacks = AnimationUtility.GetAnimationEvents(clip).Where(e => e.functionName == "ATK").OrderBy(e => e.time).ToArray();
            if (attacks.Length == 0) throw new InvalidOperationException("No ATK source events; migration not authorized to invent them.");
            var skill = new SkillAuthoringData { registered = true, skillId = "miyabi-normal-" + n, displayName = combo.name, category = "Normal", skillType = SkillType.Normal };
            skill.animations.Add(new AnimationSegment { id = "animation-1", clip = clip, startFrame = 0, endFrame = duration, trimEndUs = length,
                rootSource = RootBindingSource.LegacyCombo, statePaths = new System.Collections.Generic.List<string> { state.Key } });
            skill.tracks.Add(new SkillTrack { id = "imported-atk", name = "Approved legacy ATK points (XZ)", kind = TrackKind.HitBox });
            for (int i = 0; i < attacks.Length; i++)
            {
                int frame = (int)(((long)Math.Round(attacks[i].time * 1000000.0, MidpointRounding.AwayFromZero) + 32999) / 33000);
                if (frame >= duration) throw new InvalidOperationException("ATK event outside [0,D); explicit decision required.");
                string group = "atk-group-" + i;
                skill.hitGroups.Add(new HitGroup { id = group, mode = HitMode.Single, damageSource = DamageSource.ComboBaseDamage });
                // Approved conversion: forward 200-degree sector, radius from old distance.
                // Height=1 is a minimal schema/display extent ONLY; Y never filters runtime hits.
                skill.hitBoxes.Add(new HitBox { id = "atk-box-" + i, trackId = "imported-atk", hitGroupId = group, startFrame = frame, endFrame = frame + 1,
                    shape = ShapeKind.Sector, radius = checked((int)Math.Round(combo.attackDistance * 10000.0, MidpointRounding.AwayFromZero)), height = 1, sectorAngleMilliDegrees = 200000 });
            }
            Undo.RecordObject(combo, "Migrate approved ATK source events"); combo.skill = skill;
            catalog.skills.Add(combo); EditorUtility.SetDirty(combo);
            PilotArenaSetup.SeedVerifiedGates(combo);
        }
        catalog.completeness = CatalogCompleteness.PartialPilot;
        SeedApprovedVfx(profile);
        catalog.migrationNotes = "Phase4B partial migration: Normal1-5; Normal6/Rush_ATK await source events. Skill/FinishSkill and full presentation readiness remain pending. HP/HurtBox remain explicit test values.";
        EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        var diagnostics = SkillConfigExporter.Export(catalog);
        if (diagnostics.Any(d => d.severity == DiagnosticSeverity.Error)) throw new InvalidOperationException(string.Join("\n", diagnostics));
        Undo.RecordObject(profile, "Update approved reachable prefix and export seal");
        profile.allowedCombos = profile.comboContainer.comboDatas.Take(5).ToArray();
        profile.sealedJsonHash = SkillConfigHash.Sha256(profile.exportedJson.text); profile.sealedSourceHash = profile.SourceHash();
        EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
        profile.ValidateAndFreeze();
        Debug.Log("PHASE4B_PREPARE_COMPLETE normal1-5=true completeCatalog=false; Normal6/Rush_ATK remain blocked");
    }
    public static void RunBatch()
    { try { Prepare(); EditorApplication.Exit(0); } catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); } }
    static void SeedApprovedVfx(PilotBattleProfile profile)
    {
        const string path = "Assets/Resources/Prefabs/VFX/Miyabi_LegacySlash06.prefab";
        const string resource = "miyabi-legacy-slash06";
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("4650564957e69704eaab576b687102e0"));
        int lifetime = PilotPresentation.NaturalLifetimeBound(source); // validate BEFORE any instance
        var variant = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!variant)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
                variant = PrefabUtility.SaveAsPrefabAsset(instance, path);
                UnityEngine.Object.DestroyImmediate(instance);
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        }
        if (PrefabUtility.GetCorrespondingObjectFromSource(variant) != source) throw new InvalidOperationException("Expected approved slash06 Prefab Variant source.");
        var existing = profile.catalog.resources.SingleOrDefault(r => r.id == resource);
        if (existing != null && (existing.asset != variant || existing.kind != ResourceKind.VfxPrefab)) throw new InvalidOperationException("VFX binding conflict; do not overwrite.");
        if (existing == null) profile.catalog.resources.Add(new SkillResourceBinding { id = resource, kind = ResourceKind.VfxPrefab, asset = variant });
        foreach (var combo in profile.comboContainer.comboDatas.Take(5))
        {
            var sourceEvents = AnimationUtility.GetAnimationEvents(combo.skill.animations.Single().clip).Where(e => e.functionName == "PlayVFX").OrderBy(e => e.time).ToArray();
            Undo.RecordObject(combo, "Migrate approved Root-bound slash events");
            if (!combo.skill.tracks.Any(t => t.id == "legacy-vfx")) combo.skill.tracks.Add(new SkillTrack { id = "legacy-vfx", name = "Original PlayVFX events", kind = TrackKind.Vfx });
            for (int i = 0; i < sourceEvents.Length; i++)
            {
                var e = sourceEvents[i]; string id = "legacy-vfx-" + i;
                // Scope is the exact audited aliases, never infer other legacy resources by name.
                if (!new[] { "Unagi_01", "Unagi_02", "Unagi_03", "Unagi_04", "Unagi_05_01", "Unagi_05_02", "Unagi_05_03", "Unagi_05_04", "Unagi_05_05" }.Contains(e.stringParameter))
                    throw new InvalidOperationException("Unaudited VFX alias " + e.stringParameter);
                if (combo.skill.vfx.Any(v => v.id == id)) continue; // preserve subsequent authored edits
                int frame = (int)(((long)Math.Round(e.time * 1000000.0, MidpointRounding.AwayFromZero) + 32999) / 33000);
                combo.skill.vfx.Add(new VfxCue { id = id, trackId = "legacy-vfx", resourceId = resource, startFrame = frame, endFrame = frame + 1,
                    attachment = AttachmentMode.Root, classification = VfxClass.AttackCritical, lifetime = VfxLifetime.AutoComplete, maxLifetimeFrames = lifetime, scalePermille = 1000 });
                Debug.Log("[Phase4BVfxSource] " + combo.name + " " + e.stringParameter + " k=" + frame + " Root/zero offset; original rotation=0; bound=" + lifetime);
            }
            EditorUtility.SetDirty(combo);
        }
    }
}
