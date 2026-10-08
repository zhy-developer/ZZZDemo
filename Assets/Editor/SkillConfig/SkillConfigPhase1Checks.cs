using System;
using System.IO;
using System.Linq;
using SkillConfig;
using SkillConfig.Editor;
using UnityEditor;
using UnityEngine;

public static class SkillConfigPhase1Checks
{
    public static void Run()
    {
        SkillPilotSetup.Register();
        int count = 0;
        Action<bool, string> check = (ok, name) => { if (!ok) throw new InvalidOperationException("FAIL " + name); count++; Debug.Log("PHASE1_PASS " + name); };
        var catalog = AssetDatabase.LoadAssetAtPath<CharacterSkillCatalog>(SkillPilotSetup.CatalogPath);
        var dto = SkillAssetValidator.Build(catalog, out var issues);
        check(!issues.Any(d => d.severity == DiagnosticSeverity.Error), "pilot real asset validation");
        check(dto.skills.Count == 2 && dto.completeness == CatalogCompleteness.PartialPilot && !dto.battleReady, "partial catalog guard");
        check(issues.Any(d => d.code == "MOTION_METADATA"), "legacy provenance Warning");
        check(SkillConfigLoader.TryLoad("miyabi", out var loaded, out _), "Resources load real export");
        check(!SkillConfigLoader.TryLoad("miyabi", out _, out _, true), "battle configuration rejected");
        check(loaded.CopyData().skills.Select(s => s.skillId).OrderBy(x => x).SequenceEqual(dto.skills.Select(s => s.skillId).OrderBy(x => x)), "IDs survive Unity JsonUtility roundtrip");
        check(!SkillConfigExporter.IsStale(catalog), "fresh export status");
        var versionCheck = loaded.CopyData(); versionCheck.schemaVersion = 999;
        check(!SkillConfigLoader.TryParse(JsonUtility.ToJson(versionCheck), out _, out _), "Unity JSON unknown schema rejected");
        check(!SkillConfigLoader.TryParse("{broken", out _, out _), "Unity JSON malformed input rejected");
        var withoutMotion = loaded.CopyData();
        withoutMotion.skills[0].animations[0].rootMotion = null;
        withoutMotion.skills[0].animations[0].rootMotionBound = false;
        check(SkillConfigLoader.TryParse(JsonUtility.ToJson(withoutMotion), out _, out _), "optional no-motion segment survives Unity serialization");
        var eventData = new SkillEvent { id = "event-check", trackId = "events", frame = 0, eventId = "combo.preinput", execution = EventExecution.Authoritative, parameterKind = EventParameterKind.ComboGate, comboGate = new ComboGateParameters { enabled = true } };
        check(SkillEventRegistry.IsValid(JsonUtility.FromJson<SkillEvent>(JsonUtility.ToJson(eventData))), "typed event parameters survive Unity serialization");
        string exportPath = SkillConfigExporter.PathFor(catalog), oldJson = File.ReadAllText(exportPath);
        var first = catalog.skills[0]; string oldId = first.skill.skillId;
        var originalClip = first.skill.animations[0].clip;
        try
        {
            first.skill.animations[0].clip = null;
            SkillAssetValidator.Build(catalog, out var missingClip);
            check(missingClip.Any(d => d.code == "CLIP" && d.severity == DiagnosticSeverity.Error), "missing actual Clip reference rejected");
        }
        finally { first.skill.animations[0].clip = originalClip; }
        var originalMotion = first.rootMotion.json;
        try
        {
            first.rootMotion.json = null;
            SkillAssetValidator.Build(catalog, out var missingMotion);
            check(missingMotion.Any(d => d.code == "ROOT_MISSING" && d.severity == DiagnosticSeverity.Error), "missing actual Root Motion reference rejected");
        }
        finally { first.rootMotion.json = originalMotion; }
        var serialized = new SerializedObject(first);
        var damage = serialized.FindProperty("_comboDamage"); float originalDamage = damage.floatValue;
        try
        {
            damage.floatValue = originalDamage + 1; serialized.ApplyModifiedPropertiesWithoutUndo();
            check(SkillConfigExporter.IsStale(catalog), "legacy damage marks export stale");
        }
        finally { serialized.Update(); damage = serialized.FindProperty("_comboDamage"); damage.floatValue = originalDamage; serialized.ApplyModifiedPropertiesWithoutUndo(); }
        try
        {
            serialized.Update(); serialized.FindProperty("_comboDamage").floatValue = 2147483648f; serialized.ApplyModifiedPropertiesWithoutUndo();
            SkillAssetValidator.Build(catalog, out var damageIssues);
            check(damageIssues.Any(d => d.code == "BASE_DAMAGE"), "float damage int-overflow rejected");
        }
        finally { serialized.Update(); serialized.FindProperty("_comboDamage").floatValue = originalDamage; serialized.ApplyModifiedPropertiesWithoutUndo(); }
        string oldEntry = first.comboName;
        try
        {
            serialized.Update(); serialized.FindProperty("_comboName").stringValue = "InvalidEntry"; serialized.ApplyModifiedPropertiesWithoutUndo();
            check(SkillConfigExporter.IsStale(catalog), "legacy entry marks export stale");
        }
        finally { serialized.Update(); serialized.FindProperty("_comboName").stringValue = oldEntry; serialized.ApplyModifiedPropertiesWithoutUndo(); }
        int oldScale = first.rootMotion.distancePermille;
        try { first.rootMotion.distancePermille++; check(SkillConfigExporter.IsStale(catalog), "root settings mark export stale"); }
        finally { first.rootMotion.distancePermille = oldScale; }
        string oldCategory = first.skill.category;
        try { first.skill.category = "changed"; check(SkillConfigExporter.IsStale(catalog), "new authoring fields mark export stale"); }
        finally { first.skill.category = oldCategory; }
        string casePath = "Assets/SkillConfigCaseCheck_" + Guid.NewGuid().ToString("N") + ".asset";
        try
        {
            var otherCatalog = ScriptableObject.CreateInstance<CharacterSkillCatalog>(); otherCatalog.characterId = "MIYABI";
            AssetDatabase.CreateAsset(otherCatalog, casePath);
            SkillAssetValidator.Build(catalog, out var caseIssues);
            check(caseIssues.Any(d => d.code == "CATALOG_OWNERSHIP"), "case-folded CharacterID export collision rejected");
        }
        finally { AssetDatabase.DeleteAsset(casePath); }
        int end = first.skill.animations[0].endFrame;
        try
        {
            first.skill.animations[0].endFrame = 0; EditorUtility.SetDirty(first);
            check(SkillConfigExporter.Export(catalog).Any(d => d.severity == DiagnosticSeverity.Error), "illegal pilot blocks export");
            check(File.ReadAllText(exportPath) == oldJson, "blocked export preserves old JSON");
        }
        finally { first.skill.animations[0].endFrame = end; EditorUtility.SetDirty(first); AssetDatabase.SaveAssets(); }
        string secondId = catalog.skills[1].skill.skillId;
        try
        {
            catalog.skills[1].skill.skillId = oldId;
            SkillAssetValidator.Build(catalog, out var duplicates);
            check(duplicates.Any(d => d.code == "SKILL_ID"), "real asset duplicate ID rejected");
        }
        finally { catalog.skills[1].skill.skillId = secondId; }
        // Rename and reload use the existing real pilot asset, preserve its GUID and restore its path.
        string originalPath = AssetDatabase.GetAssetPath(first);
        string renamedPath = Path.GetDirectoryName(originalPath).Replace('\\', '/') + "/Phase1_IdStability.asset";
        try
        {
            check(string.IsNullOrEmpty(AssetDatabase.MoveAsset(originalPath, renamedPath)), "rename pilot asset");
            AssetDatabase.SaveAssets(); AssetDatabase.ImportAsset(renamedPath, ImportAssetOptions.ForceSynchronousImport);
            check(AssetDatabase.LoadAssetAtPath<ComboData>(renamedPath).skill.skillId == oldId, "ID survives rename/save/reimport");
        }
        finally { if (AssetDatabase.LoadAssetAtPath<ComboData>(renamedPath) != null) AssetDatabase.MoveAsset(renamedPath, originalPath); }
        var final = SkillConfigExporter.Export(catalog);
        check(!final.Any(d => d.severity == DiagnosticSeverity.Error), "restored pilot exported");
        string fixturePath = "Assets/SkillConfigReopenCheck_" + Guid.NewGuid().ToString("N") + ".asset";
        var fixture = UnityEngine.Object.Instantiate(first);
        fixture.skill.skillId = "reopen-fixture-" + Guid.NewGuid().ToString("N");
        fixture.skill.tracks.Add(new SkillTrack { id = "events", kind = TrackKind.Event });
        fixture.skill.events.Add(eventData);
        fixture.skill.animations[0].rootSource = RootBindingSource.None;
        AssetDatabase.CreateAsset(fixture, fixturePath); AssetDatabase.SaveAssetIfDirty(fixture);
        File.WriteAllText("Library/SkillConfigReopenFixture.txt", fixturePath + "\n" + fixture.skill.skillId);
        Debug.Log("PHASE1_RESULT " + count + " Unity checks passed. Run VerifyReopen in a NEW Editor process next.");
    }
    public static void VerifyReopen()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<CharacterSkillCatalog>(SkillPilotSetup.CatalogPath);
        if (catalog == null || catalog.skills.Count != 2 || catalog.skills[0].skill.skillId != "miyabi-normal-1" || catalog.skills[1].skill.skillId != "miyabi-normal-2")
            throw new InvalidOperationException("Saved skill/catalog identity did not survive Editor restart.");
        SkillAssetValidator.Build(catalog, out var issues);
        if (issues.Any(d => d.severity == DiagnosticSeverity.Error) || !SkillConfigLoader.TryLoad("miyabi", out _, out _) || SkillConfigExporter.IsStale(catalog))
            throw new InvalidOperationException("Reopened config validation/freshness/load failed: " + string.Join("\n", issues));
        var fixtureInfo = File.ReadAllLines("Library/SkillConfigReopenFixture.txt");
        if (fixtureInfo.Length != 2 || !System.Text.RegularExpressions.Regex.IsMatch(fixtureInfo[0], "^Assets/SkillConfigReopenCheck_[a-f0-9]{32}\\.asset$")) throw new InvalidOperationException("Unexpected fixture path.");
        var fixture = AssetDatabase.LoadAssetAtPath<ComboData>(fixtureInfo[0]);
        if (fixture == null || fixture.skill.skillId != fixtureInfo[1] || !SkillEventRegistry.IsValid(fixture.skill.events.Single()) || fixture.skill.animations[0].rootSource != RootBindingSource.None)
            throw new InvalidOperationException("Typed event/optional root binding did not survive real SO save/reopen.");
        AssetDatabase.DeleteAsset(fixtureInfo[0]); File.Delete("Library/SkillConfigReopenFixture.txt");
        Debug.Log("PHASE1_REOPEN_PASS actual separate Editor process: both SOs, IDs, catalog, export, Resources load and typed-event/no-motion SO fixture.");
    }

    // Acceptance revision only. Run on the isolated harness, never drive the user's battle instance.
    public static void RunAcceptanceRevision()
    {
        Run();
        int checks = 0;
        Action<bool, string> check = (ok, name) => {
            if (!ok) throw new InvalidOperationException("REVISION_FAIL " + name);
            checks++; Debug.Log("REVISION_PASS " + name);
        };
        var catalog = AssetDatabase.LoadAssetAtPath<CharacterSkillCatalog>(SkillPilotSetup.CatalogPath);
        var first = catalog.skills[0]; var second = catalog.skills[1];
        var objects = new UnityEngine.Object[] { first, second, catalog };
        var saved = objects.Select(EditorJsonUtility.ToJson).ToArray();
        Action restore = () => {
            for (int i = 0; i < objects.Length; i++)
            {
                EditorJsonUtility.FromJsonOverwrite(saved[i], objects[i]);
                EditorUtility.SetDirty(objects[i]); AssetDatabase.SaveAssetIfDirty(objects[i]);
            }
        };
        string path = SkillConfigExporter.PathFor(catalog);
        Action<string, string, Action> blocked = (name, code, mutate) => {
            byte[] before = File.ReadAllBytes(path), meta = File.ReadAllBytes(path + ".meta");
            try
            {
                mutate();
                foreach (var obj in objects) EditorUtility.SetDirty(obj);
                var result = SkillConfigExporter.Export(catalog);
                check(result.Any(d => d.severity == DiagnosticSeverity.Error && d.code == code), name + " expected Error " + code);
                check(File.ReadAllBytes(path).SequenceEqual(before), name + " preserves previous JSON bytes");
                check(File.ReadAllBytes(path + ".meta").SequenceEqual(meta), name + " preserves export GUID/meta");
                Debug.Log("REVISION_PROTECTION " + name + " before=" + SkillConfigHash.Sha256(System.Text.Encoding.UTF8.GetString(before))
                    + " after=" + SkillConfigHash.Sha256(File.ReadAllText(path)));
            }
            finally { restore(); }
        };
        blocked("start-equals-end", "WINDOW", () => first.skill.animations[0].startFrame = first.skill.animations[0].endFrame);
        blocked("start-after-end", "WINDOW", () => first.skill.animations[0].startFrame = first.skill.animations[0].endFrame + 1);
        blocked("zero-speed", "ANIMATION_TIME", () => first.skill.animations[0].speedPermille = 0);
        blocked("duplicate-ID-inside-catalog", "SKILL_ID", () => second.skill.skillId = first.skill.skillId);
        blocked("unknown-HitGroup", "GROUP_REF", () => {
            first.skill.tracks.Add(new SkillTrack { id = "revision-box-track", kind = TrackKind.HitBox });
            first.skill.hitBoxes.Add(new HitBox { id = "revision-box", trackId = "revision-box-track", hitGroupId = "nonexistent-group", shape = ShapeKind.Sphere, radius = 100, startFrame = 0, endFrame = 1 });
        });
        blocked("missing-required-motion", "ROOT_MISSING", () => first.rootMotion.json = null);
        blocked("missing-required-VFX", "RESOURCE_REF", () => {
            first.skill.tracks.Add(new SkillTrack { id = "revision-vfx-track", kind = TrackKind.Vfx });
            first.skill.vfx.Add(new VfxCue { id = "revision-vfx", trackId = "revision-vfx-track", resourceId = "nonexistent-required-resource", startFrame = 0, endFrame = 1 });
        });
        blocked("invalid-required-resource-asset", "RESOURCE_TYPE", () => {
            catalog.resources.Add(new SkillResourceBinding { id = "invalid-prefab", kind = ResourceKind.VfxPrefab, asset = null });
            first.skill.tracks.Add(new SkillTrack { id = "revision-vfx-track", kind = TrackKind.Vfx });
            first.skill.vfx.Add(new VfxCue { id = "revision-vfx", trackId = "revision-vfx-track", resourceId = "invalid-prefab", startFrame = 0, endFrame = 1 });
        });

        // This is NOT a generic illegal-data fixture. Derive the expected span from actual imported resource data.
        SkillAssetValidator.Build(catalog, out var baselineIssues);
        check(!baselineIssues.Any(d => d.severity == DiagnosticSeverity.Error), "real-resource baseline is valid");
        var segment = first.skill.animations[0];
        long numerator = (segment.trimEndUs - segment.trimStartUs) * 1000;
        long divisor = 33L * 1000 * segment.speedPermille;
        long expectedFrames = (numerator + divisor - 1) / divisor;
        Debug.Log("REVISION_MAPPING Normal1 clipLengthUs=" + (long)Math.Round(segment.clip.length * 1000000.0)
            + " trim=" + segment.trimStartUs + ".." + segment.trimEndUs + " speed=" + segment.speedPermille + " calculatedFrames=" + expectedFrames);
        check(expectedFrames == 20 && segment.startFrame == 0 && segment.endFrame == 20, "actual Normal1 full-trim 1x maps to 20 frames");
        blocked("Normal1-20-to-19-resource-specific", "ANIMATION_DURATION", () => first.skill.animations[0].endFrame = 19);

        Action<string, Action, bool> stale = (name, mutate, valid) => {
            check(!SkillConfigExporter.IsStale(catalog), name + " baseline fresh");
            try
            {
                mutate();
                check(SkillConfigExporter.IsStale(catalog), name + " marks stale");
                if (valid)
                {
                    SkillAssetValidator.Build(catalog, out var issues);
                    check(!issues.Any(d => d.severity == DiagnosticSeverity.Error), name + " still valid (not stale merely from Error)");
                }
            }
            finally { restore(); }
            check(!SkillConfigExporter.IsStale(catalog), name + " restored fresh");
        };
        Action<string, float?, string> legacy = (field, number, text) => {
            var so = new SerializedObject(first); var property = so.FindProperty(field);
            if (number.HasValue) property.floatValue = number.Value; else property.stringValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
        };
        stale("legacy-base-damage", () => legacy("_comboDamage", first.comboDamage + 1, null), true);
        stale("legacy-entry-full-state-path", () => legacy("_comboName", null, first.skill.animations[0].statePaths[0]), true);
        stale("legacy-root-distance", () => first.rootMotion.distancePermille++, true);
        stale("legacy-root-reference", () => first.rootMotion.json = second.rootMotion.json, false);
        stale("catalog-reference-list", () => catalog.skills.RemoveAt(1), true);
        stale("new-animation-endpoint", () => first.skill.animations[0].endFrame--, false);

        string duplicatePath = "Assets/SkillConfigDuplicateCheck_" + Guid.NewGuid().ToString("N") + ".asset";
        try
        {
            var duplicate = UnityEngine.Object.Instantiate(first);
            AssetDatabase.CreateAsset(duplicate, duplicatePath); AssetDatabase.SaveAssetIfDirty(duplicate);
            SkillAssetValidator.Build(catalog, out var outside);
            check(outside.Any(d => d.code == "GLOBAL_SKILL_ID"), "registered duplicate outside catalog found by AssetDatabase scan");
            blocked("duplicate-asset-added-to-catalog", "SKILL_ID", () => catalog.skills.Add(duplicate));
            duplicate.skill.registered = false; EditorUtility.SetDirty(duplicate); AssetDatabase.SaveAssetIfDirty(duplicate);
            SkillAssetValidator.Build(catalog, out var unregistered);
            check(!unregistered.Any(d => d.code == "GLOBAL_SKILL_ID"), "unregistered external asset intentionally outside ID scan");
        }
        finally { restore(); AssetDatabase.DeleteAsset(duplicatePath); }
        check(!SkillConfigExporter.IsStale(catalog), "restored catalog and original export remain consistent");
        Debug.Log("REVISION_MANUAL_REQUIRED Inspector copy button/new stable ID and full-project Play-mode combat regression were NOT executed.");
        Debug.Log("REVISION_RESULT " + checks + " additional actual Unity checks passed; original 25 checks also rerun. Full combat acceptance remains pending.");
    }
}
