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
public static class BattleProfileMaintenance
{
    public static void ExportAndSeal(BattleProfile profile)
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

    public static void CheckExport(BattleProfile profile)
    {
        if (!profile || !profile.catalog) throw new InvalidOperationException("Select a battle profile with a catalog.");
        if (SkillConfigExporter.IsStale(profile.catalog))
            throw new InvalidOperationException("技能 JSON 已过期或存在错误。请打开 Tools/Skill Editor/Skill Editor，选择对应战斗配置并点击“保存并导出”，修复校验错误后再打包。");
        if (AssetDatabase.GetAssetPath(profile.exportedJson) != SkillConfigExporter.PathFor(profile.catalog))
            throw new InvalidOperationException("Profile must reference its catalog's actual exported JSON.");
    }

    public static void Seal(BattleProfile profile)
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

[CustomEditor(typeof(BattleProfile))]
public sealed class BattleProfileInspector : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("在技能编辑器中编辑技能并点击“保存并导出”，同时更新战斗配置摘要。正式战斗的 HurtBox 读取角色目录。", MessageType.Info);
        DrawPropertiesExcluding(serializedObject, "m_Script", "testHP", "testHurtRadius", "testHurtOffset", "sealedSourceHash", "sealedJsonHash");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("testHP"), new GUIContent("Initial HP"));
        if (!serializedObject.FindProperty("normalGameplay").boolValue)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("testHurtRadius"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("testHurtOffset"), true);
        }
        serializedObject.ApplyModifiedProperties();
        var profile = (BattleProfile)target;
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Sealed source", profile.sealedSourceHash ?? "");
            EditorGUILayout.TextField("Sealed JSON", profile.sealedJsonHash ?? "");
        }
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("打开技能编辑器")) SkillEditorWindow.OpenProfile(profile);
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
        var settings = manager.GetComponent<BattleMatchSettings>();
        if (!settings) throw new BuildFailedException("BattleScene must declare its battle mode explicitly.");
        if (settings.mode != BattleAuthorityMode.Deterministic) throw new BuildFailedException("Legacy combat has been removed. Select Deterministic.");
        BattleProfileMaintenance.CheckExport(settings.profile);
        if (!settings.profile.normalGameplay && (report.summary.options & BuildOptions.Development) == 0)
            throw new BuildFailedException("A test profile cannot be used in a non-development build.");
        var config = settings.Freeze();
        Debug.Log("[GameplayBuild] normalGameplay=" + settings.profile.normalGameplay + " digest=" + config.Digest);
    }
}
