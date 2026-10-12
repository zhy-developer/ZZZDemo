using System;
using System.Linq;
using SkillRuntime.Integration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZZZ;

// Uses the real character Avatar/Animator in an isolated preview scene, without starting a match.
public static class AttackPlaybackChecks
{
    const string ProfilePath = "Assets/ScriptableObject/SkillConfigs/Miyabi_NormalGameplay.asset";

    [MenuItem("Tools/Skill Runtime/Check Continuous Attack Playback")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play Mode before checks.");
        var profile = AssetDatabase.LoadAssetAtPath<PilotBattleProfile>(ProfilePath);
        var prefab = Resources.Load<GameObject>("BattleScene/Role/Character");
        var scene = EditorSceneManager.NewPreviewScene();
        GameObject instance = null;
        int checks = 0;
        Action<bool, string> check = (ok, label) => {
            if (!ok) throw new Exception("ATTACK_PLAYBACK_FAIL " + label);
            checks++; Debug.Log("ATTACK_PLAYBACK_PASS " + label);
        };
        try
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var animator = instance.GetComponentInChildren<Player>(true).GetComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false; animator.fireEvents = false;
            animator.Rebind(); animator.Update(0);
            var playback = new PlayerAttackPlayback(animator);
            long sequence = 0;
            PlayerRenderFrame first = default;
            foreach (var combo in profile.ReachableCombos())
            foreach (var source in combo.skill.animations)
            {
                var frame = new PlayerRenderFrame {
                    Action = PlayerRenderAction.Attack, Animation = source.statePaths[0], AnimationSegment = source.id,
                    ClipLength = source.clip.length, PlaybackSpeed = source.speedPermille / 1000f,
                    EndNormalizedTime = (float)(source.trimEndUs / 1000000.0 / source.clip.length) };
                if (first.Animation == null) first = frame;
                foreach (int fps in new[] { 30, 60, 144 })
                {
                    frame.CastSequence = ++sequence; frame.NormalizedTime = 0;
                    playback.Apply(frame);
                    int logicFrame = 0;
                    bool smooth = true;
                    // Pass the old Animator exit times: it must stay in this attack until a snapshot exits it.
                    int steps = (int)(source.trimEndUs / 1000000.0 / frame.PlaybackSpeed * .9f * fps);
                    for (int i = 1; i <= steps; i++)
                    {
                        float before = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                        animator.Update(1f / fps); playback.ClampEnd();
                        var now = animator.GetCurrentAnimatorStateInfo(0);
                        smooth &= now.normalizedTime > before && now.fullPathHash == Animator.StringToHash(frame.Animation) && !animator.IsInTransition(0);
                        int nextLogicFrame = (int)(i / (double)fps / .033);
                        if (nextLogicFrame > logicFrame)
                        {
                            logicFrame = nextLogicFrame;
                            frame.NormalizedTime = (float)(logicFrame * .033 * frame.PlaybackSpeed / frame.ClipLength);
                            playback.Apply(frame);
                            smooth &= Mathf.Abs(animator.GetCurrentAnimatorStateInfo(0).normalizedTime - now.normalizedTime) < .0001f;
                        }
                    }
                    check(smooth, combo.name + "/" + source.id + " advances every render frame without snapshot seeks at " + fps + " FPS");
                }
            }
            first.CastSequence = ++sequence; first.NormalizedTime = .1f; first.PlaybackSpeed = 2;
            playback.Apply(first);
            float start = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            animator.Update(.02f);
            check(Mathf.Abs((animator.GetCurrentAnimatorStateInfo(0).normalizedTime - start) * first.ClipLength - .04f) < .002f, "configured double speed");
            first.PlaybackSpeed = 1; first.CastSequence = ++sequence; first.NormalizedTime = 0;
            playback.Apply(first);
            check(animator.GetCurrentAnimatorStateInfo(0).normalizedTime < .001f, "new cast of the same attack restarts");
            animator.Update(.04f);
            first.AnimationSegment += "-next"; first.NormalizedTime = .2f;
            playback.Apply(first);
            check(Mathf.Abs(animator.GetCurrentAnimatorStateInfo(0).normalizedTime - .2f) < .001f, "new segment using the same state seeks to its trim start");
            first.NormalizedTime = .7f; playback.Apply(first);
            check(Mathf.Abs(animator.GetCurrentAnimatorStateInfo(0).normalizedTime - .7f) < .001f, "large snapshot discontinuity corrects the clock");
            first.CastSequence = ++sequence; first.NormalizedTime = .3f; first.EndNormalizedTime = .4f;
            playback.Apply(first); animator.Update(.3f); playback.ClampEnd();
            check(animator.speed == 0 && Mathf.Abs(animator.GetCurrentAnimatorStateInfo(0).normalizedTime - .4f) < .001f, "packet gap holds at trim end without looping");
            first.NormalizedTime = .39f; playback.Apply(first); animator.Update(.05f);
            check(animator.speed == 0 && Mathf.Abs(animator.GetCurrentAnimatorStateInfo(0).normalizedTime - .4f) < .001f, "nearby snapshot does not restart a finished segment");
            check(!animator.applyRootMotion && !animator.fireEvents, "animation cannot apply root motion or combat events");
            var player = instance.GetComponentInChildren<Player>(true);
            player.SendMessage("Awake", SendMessageOptions.DontRequireReceiver);
            player.InitializeNetworkRole(1, true);
            player.BindBattleInput(() => null);
            player.BindDeterministicPilot();
            first.CastSequence = ++sequence; first.EndNormalizedTime = 1; first.NormalizedTime = 0;
            player.ApplyRenderFrame(first);
            player.StartSkillPresentation(AttackStyle.FinishSkill, .1);
            var director = (UnityEngine.Playables.PlayableDirector)new SerializedObject(player).FindProperty("finishSkillTimeline").objectReferenceValue;
            check(director.playableAsset.outputs.All(o => director.GetGenericBinding(o.sourceObject) != animator), "ultimate Timeline has no character Animator binding");
            director.Evaluate(); animator.Update(.02f);
            check(animator.GetCurrentAnimatorStateInfo(0).fullPathHash == Animator.StringToHash(first.Animation)
                && animator.GetCurrentAnimatorStateInfo(0).normalizedTime > 0, "camera Timeline leaves character playback advancing independently");
            player.StopSkillPresentation();
            check(director.state != UnityEngine.Playables.PlayState.Playing, "interruption releases ultimate camera Timeline");
            player.ApplyRenderFrame(new PlayerRenderFrame { IdleState = profile.idleState });
            check(animator.speed == 1 && player.comboStateMachine.currentState.Value is PlayerNullState, "committed exit restores locomotion");
            Debug.Log("ATTACK_PLAYBACK_CHECKS_COMPLETE checks=" + checks);
        }
        finally
        {
            if (instance) UnityEngine.Object.DestroyImmediate(instance);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    // Explicit batch maintenance entry after an intentional Animator source edit.
    public static void RunAndSeal()
    {
        Run();
        var profile = AssetDatabase.LoadAssetAtPath<PilotBattleProfile>(ProfilePath);
        PilotProfileMaintenance.ExportAndSeal(profile);
        PilotProfileMaintenance.CheckExport(profile);
        profile.ValidateAndFreeze();
    }
}
