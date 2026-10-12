using System;
using System.Linq;
using SkillConfig;
using SkillConfig.Editor;
using SkillRuntime.Integration;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Exporting and sealing are explicit authoring actions; build validation remains read-only.
public static class PilotProfileMaintenance
{
    [MenuItem("Tools/Skill Runtime/Validate and Seal Selected Profile")]
    public static void SealSelected() => Seal(Selection.activeObject as PilotBattleProfile);

    [MenuItem("Tools/Skill Runtime/Export and Seal Selected Profile")]
    public static void ExportAndSealSelected() => ExportAndSeal(Selection.activeObject as PilotBattleProfile);

    public static void ExportAndSeal(PilotBattleProfile profile)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play Mode before exporting configuration.");
        if (!profile || !profile.catalog) throw new InvalidOperationException("Select a battle profile with a catalog.");
        // Require the correct binding before writing; never silently replace a profile's JSON asset.
        if (!profile.exportedJson || AssetDatabase.GetAssetPath(profile.exportedJson) != SkillConfigExporter.PathFor(profile.catalog))
            throw new InvalidOperationException("Profile must reference its catalog's actual exported JSON.");
        var issues = SkillConfigExporter.Export(profile.catalog);
        if (issues.Any(d => d.severity == DiagnosticSeverity.Error))
            throw new InvalidOperationException(string.Join("\n", issues));
        Seal(profile);
    }

    [MenuItem("Tools/Skill Runtime/Build Windows Player (Clean Cache)")]
    public static void BuildCleanPlayer()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play Mode before building.");
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            throw new InvalidOperationException("Switch Build Settings to Windows x86_64 first.");
        string path = EditorUtility.SaveFilePanel("Clean Windows build", "Builds", "ARPGDEMO", "exe");
        if (string.IsNullOrEmpty(path)) return;
        var options = BuildOptions.CleanBuildCache;
        if (EditorUserBuildSettings.development) options |= BuildOptions.Development;
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            target = BuildTarget.StandaloneWindows64, locationPathName = path, options = options });
        if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Clean build failed: " + report.summary.result);
        Debug.Log("[GameplayBuild] Clean build complete: " + path);
    }

    public static void CheckExport(PilotBattleProfile profile)
    {
        if (!profile || !profile.catalog) throw new InvalidOperationException("Select a battle profile with a catalog.");
        if (SkillConfigExporter.IsStale(profile.catalog))
            throw new InvalidOperationException("Skill JSON is stale or invalid (including moved Resources assets). Select the battle profile and use Tools/Skill Runtime/Export and Seal Selected Profile. Resolve any validation errors before building.");
        if (AssetDatabase.GetAssetPath(profile.exportedJson) != SkillConfigExporter.PathFor(profile.catalog))
            throw new InvalidOperationException("Profile must reference its catalog's actual exported JSON.");
    }

    public static void Seal(PilotBattleProfile profile)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play Mode before sealing configuration.");
        CheckExport(profile);
        var draft = UnityEngine.Object.Instantiate(profile);
        try
        {
            draft.sealedJsonHash = SkillConfigHash.Sha256(draft.exportedJson.text);
            draft.sealedSourceHash = draft.SourceHash();
            var config = draft.ValidateAndFreeze(); // Failure cannot change the original seal.
            Undo.IncrementCurrentGroup();
            Undo.RecordObject(profile, "Validate and seal battle profile");
            profile.sealedJsonHash = draft.sealedJsonHash;
            profile.sealedSourceHash = draft.sealedSourceHash;
            EditorUtility.SetDirty(profile);
            Undo.FlushUndoRecordObjects();
            AssetDatabase.SaveAssetIfDirty(profile);
            Debug.Log("[ProfileSealed] " + AssetDatabase.GetAssetPath(profile) + " digest=" + config.Digest +
                " scope=" + string.Join(",", config.Skills.Select(s => s.Skill.SkillId)) + " (new matches/builds only)");
        }
        finally { UnityEngine.Object.DestroyImmediate(draft); }
    }
}

[CustomEditor(typeof(PilotBattleProfile))]
public sealed class PilotBattleProfileInspector : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("Normal Gameplay：HP 使用下面的 Initial HP（旧序列化名 testHP）；HurtBox 只读取角色目录。技能仍在 ComboData.skill 中编辑。先保存并导出 JSON，再明确更新摘要；不会自动导出或迁移技能。", MessageType.Info);
        DrawPropertiesExcluding(serializedObject, "m_Script", "testHP", "testHurtRadius", "testHurtOffset", "sealedSourceHash", "sealedJsonHash");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("testHP"), new GUIContent("Initial HP"));
        if (!serializedObject.FindProperty("normalGameplay").boolValue)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("testHurtRadius"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("testHurtOffset"), true);
        }
        serializedObject.ApplyModifiedProperties();
        var profile = (PilotBattleProfile)target;
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Sealed source", profile.sealedSourceHash ?? "");
            EditorGUILayout.TextField("Sealed JSON", profile.sealedJsonHash ?? "");
        }
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("重新导出并校验配置、更新摘要（资源移动后使用）"))
                try { PilotProfileMaintenance.ExportAndSeal(profile); }
                catch (Exception e) { Debug.LogError("[ProfileRejected] " + e.Message); }
            if (GUILayout.Button("校验已导出的配置并更新摘要"))
                try { PilotProfileMaintenance.Seal(profile); }
                catch (Exception e) { Debug.LogError("[ProfileRejected] " + e.Message); }
        }
    }
}

// Read-only build gate. Does not inject settings or mutate the BattleScene build copy.
public sealed class GameplayBuildValidation : IProcessSceneWithReport, IPreprocessBuildWithReport
{
    public int callbackOrder => 20000;
    public void OnPreprocessBuild(BuildReport report)
    {
        // IProcessScene alone is insufficient: incremental builds can reuse serialized scenes
        // without invoking it. Validate the saved entry on EVERY build, without saving a scene.
        if (!EditorBuildSettings.scenes.Any(s => s.enabled && s.path == "Assets/Scenes/BattleScene.unity")) return;
        const string path = "Assets/Scenes/BattleScene.unity";
        var active = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(path);
        bool alreadyOpen = scene.IsValid() && scene.isLoaded;
        if (alreadyOpen && scene.isDirty) throw new BuildFailedException("Save BattleScene before validating its build configuration.");
        if (!alreadyOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try { ValidateScene(scene, report); }
        finally
        {
            if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true);
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
        }
        Debug.Log("[GameplayBuildPreflight] Saved BattleScene validated (also on incremental builds).");
    }
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null || scene.path != "Assets/Scenes/BattleScene.unity") return;
        ValidateScene(scene, report);
    }
    internal static void ValidateScene(Scene scene, BuildReport report)
    {
        var manager = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GameManager>(true)).Single();
        var settings = manager.GetComponent<PilotMatchSettings>();
        if (!settings) throw new BuildFailedException("BattleScene must declare its battle mode explicitly.");
        if (settings.mode != BattleAuthorityMode.DeterministicPilot) throw new BuildFailedException("Legacy combat has been removed. Select DeterministicPilot.");
        PilotProfileMaintenance.CheckExport(settings.profile);
        if (!settings.profile.normalGameplay && (report.summary.options & BuildOptions.Development) == 0)
            throw new BuildFailedException("A test profile cannot be used in a non-development build.");
        var config = settings.Freeze();
        Debug.Log("[GameplayBuild] normalGameplay=" + settings.profile.normalGameplay + " digest=" + config.Digest);
    }
}
