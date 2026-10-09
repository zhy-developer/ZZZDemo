using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SkillConfig.Editor.Tests
{
    // Runs only in the disposable Unity harness, including real domain reload and Play Mode.
    [InitializeOnLoad]
    public static class SkillPreviewLifecycleChecks
    {
        const string Key = "SkillPreviewLifecycleChecks.Phase";
        static SkillPreviewLifecycleChecks() { EditorApplication.update += Tick; }
        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Temp/SkillEditorPhase2C/")) throw new Exception("Use isolated Phase2C harness.");
            SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 120);
            Open(); UnityEditor.SceneManagement.StageUtility.GoToMainStage();
            Clean("leaving preview Stage releases owned resources");
            Open(); SessionState.SetInt(Key, 1); EditorUtility.RequestScriptReload();
        }
        static void Open()
        {
            var session = new SkillPreviewSession(
                AssetDatabase.LoadAssetAtPath<ComboData>(SkillPilotSetup.ComboPath(1)),
                AssetDatabase.LoadAssetAtPath<CharacterSkillCatalog>(SkillPilotSetup.CatalogPath),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Model/雅/Model/星见雅.fbx"));
            session.Seek(5);
            if (session.Error != null || SkillPreviewSession.LiveCount != 1) throw new Exception("Preview did not open: " + session.Error);
        }
        static void Clean(string label)
        {
            if (SkillPreviewSession.LiveCount != 0 || Resources.FindObjectsOfTypeAll<SkillPreviewStage>().Length != 0 ||
                Resources.FindObjectsOfTypeAll<GameObject>().Any(x => x.name.StartsWith("SkillPreview::")) ||
                Resources.FindObjectsOfTypeAll<AnimationClip>().Any(x => x.name.StartsWith("SkillPreview::")))
                throw new Exception("Preview resources remain: " + label);
            Debug.Log("PHASE2C_PASS " + label);
        }
        static void Tick()
        {
            int phase = SessionState.GetInt(Key, 0); if (phase == 0) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", 0)) throw new Exception("Lifecycle test timeout");
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                if (phase == 1)
                {
                    // The original domain still owns its session until beforeAssemblyReload runs.
                    if (SkillPreviewSession.LiveCount != 0) return;
                    Clean("script reload releases Stage, proxy and Clip copies");
                    Open(); SessionState.SetInt(Key, 2); EditorApplication.isPlaying = true;
                }
                else if (phase == 2 && EditorApplication.isPlaying)
                {
                    Clean("entering Play Mode releases preview resources");
                    SessionState.SetInt(Key, 3); EditorApplication.isPlaying = false;
                }
                else if (phase == 3 && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    Clean("returning to Edit Mode leaves no preview resources");
                    SessionState.SetInt(Key, 0); EditorApplication.Exit(0);
                }
            }
            catch (Exception e) { SessionState.SetInt(Key, 0); Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
