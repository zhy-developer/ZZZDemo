using System;
using System.Linq;
using SkillConfig;
using SkillConfig.Editor;
using SkillRuntime.Integration;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

// Seals an already exported source. Never migrates skills, generates gates or exports implicitly.
public static class PilotProfileMaintenance
{
    [MenuItem("Tools/Skill Runtime/Validate and Seal Selected Profile")]
    public static void SealSelected() => Seal(Selection.activeObject as PilotBattleProfile);

    public static void CheckExport(PilotBattleProfile profile)
    {
        if (!profile || !profile.catalog) throw new InvalidOperationException("Select a battle profile with a catalog.");
        if (SkillConfigExporter.IsStale(profile.catalog))
            throw new InvalidOperationException("Skill JSON is stale or invalid. Save, Validate and explicitly Export JSON in Skill Editor first.");
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
            if (GUILayout.Button("校验已导出的配置并更新摘要"))
                try { PilotProfileMaintenance.Seal(profile); }
                catch (Exception e) { Debug.LogError("[ProfileRejected] " + e.Message); }
    }
}

// Read-only build gate. Does not inject settings or mutate the BattleScene build copy.
public sealed class GameplayBuildValidation : IProcessSceneWithReport
{
    public int callbackOrder => 20000;
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null || scene.path != "Assets/Scenes/BattleScene.unity") return;
        var manager = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GameManager>(true)).Single();
        var settings = manager.GetComponent<PilotMatchSettings>();
        if (!settings) throw new BuildFailedException("BattleScene must declare its battle mode explicitly.");
        if (settings.mode == BattleAuthorityMode.Legacy) { Debug.LogWarning("[GameplayBuild] Explicit Legacy rollback; not the new-system delivery."); return; }
        PilotProfileMaintenance.CheckExport(settings.profile);
        if (!settings.profile.normalGameplay && (report.summary.options & BuildOptions.Development) == 0)
            throw new BuildFailedException("A test profile cannot be used in a non-development build.");
        var config = settings.Freeze();
        Debug.Log("[GameplayBuild] normalGameplay=" + settings.profile.normalGameplay + " digest=" + config.Digest);
    }
}
