using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using FrameSync.RootMotion;
using SkillConfig;
using SkillConfig.Editor;
using SkillRuntime.Integration;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEditor.SceneManagement;

public static class ExpandedCombatMigration
{
    const string ProfilePath = "Assets/ScriptableObject/SkillConfigs/Miyabi_NormalGameplay.asset";
    const string ComboDirectory = "Assets/ScriptableObject/ComboData/星见雅/";
    static long Us(double seconds) => (long)Math.Round(seconds * 1000000, MidpointRounding.AwayFromZero);
    static int Frames(long us) => checked((int)((us + 32999) / 33000));

    [MenuItem("Tools/Skill Runtime/Migrate Miyabi Expanded Actions")]
    public static void Migrate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before migration.");
        var profile = AssetDatabase.LoadAssetAtPath<PilotBattleProfile>(ProfilePath);
        var player = Resources.Load<GameObject>("BattleScene/Role/Character").GetComponentInChildren<ZZZ.Player>(true);
        var controller = (AnimatorController)profile.catalog.animatorController;
        var states = SkillAssetValidator.States(controller);
        profile.skillCombo = player.playerSO.ComboData.comboData.skillCombo;
        profile.ultimateCombo = player.playerSO.ComboData.comboData.finishSkillCombo;
        profile.dodgeCooldownFrames = Frames(Us(player.playerSO.movementData.dashData.coldTime));
        profile.dodgeForward = DodgeAsset("Unagi_DodgeForward", player.playerSO.movementData.dashData.frontDushAnimationName);
        profile.dodgeBack = DodgeAsset("Unagi_DodgeBack", player.playerSO.movementData.dashData.backDushAnimationName);
        Register(profile, profile.skillCombo, "miyabi-skill", SkillType.Skill, states, new[] { "Unagi_Skill", "Unagi_Skill_End" });
        Register(profile, profile.ultimateCombo, "miyabi-ultimate", SkillType.Ultimate, states, new[] { "Unagi_FinishSkill_Start", "Unagi_FinishSkill", "Unagi_FishSkill_End" });
        var front = player.playerSO.movementData.dashData.frontRootMotion;
        if (!front.json) throw new InvalidOperationException("Existing front-dodge bake required.");
        var frontData = JsonUtility.FromJson<RootMotionJsonData>(front.json.text);
        long frontEnd = frontData.frames[(front.endFrameExclusive == 0 ? frontData.frameCount : front.endFrameExclusive) - 1].sampleEndTimeMs * 1000L;
        Register(profile, profile.dodgeForward, "miyabi-dodge-forward", SkillType.Dodge, states, new[] { "Dodge_Front" }, frontEnd, front);
        // Back dodge previously had no bake. Preserve its Animator exit boundary and bake that range.
        var backState = FindStates(controller).Single(s => s.name == "Dodge_Back");
        long backEnd = Us(((AnimationClip)backState.motion).length * backState.transitions.Single().exitTime);
        Register(profile, profile.dodgeBack, "miyabi-dodge-back", SkillType.Dodge, states, new[] { "Dodge_Back" }, backEnd);
        Register(profile, profile.comboContainer.DodgeATKData, "miyabi-dodge-attack", SkillType.Normal, states, new[] { "Unagi_Rush_ATK" });
        AddUltimateVfx(profile);
        // Repair the initial migration's event IDs if a previous export stopped at validation.
        foreach (var combo in profile.ReachableCombos())
            foreach (var gate in combo.skill.events.Where(e => e.id == e.eventId && e.id.StartsWith("combo.", StringComparison.Ordinal)))
            { gate.id = gate.id.Replace('.', '-'); EditorUtility.SetDirty(combo); }
        var reachableStates = new HashSet<string>(profile.ReachableCombos().SelectMany(c => c.skill.animations).SelectMany(a => a.statePaths));
        foreach (var entry in StateEntries(controller))
            if (reachableStates.Contains(entry.Key))
                foreach (var transition in entry.Value.transitions) { transition.mute = true; EditorUtility.SetDirty(transition); }
        EditorUtility.SetDirty(controller);
        profile.catalog.migrationNotes = "Movement + Normal1-5 + E Skill + Q Ultimate + forward/back dodge + shared dodge attack. Normal6/QTE remain outside the scope. "
            + "Legacy Rush clip has no ATK/gate events: initial authored hit frames 6/12 (20 damage each), preinput15/ready18/move18; tune in Skill Editor. "
            + "No legacy dodge invincibility window exists; none added. Ultimate follows runtime Ultimate defense policy. "
            + "Front dodge keeps original 750ms bake; back dodge baked to original Animator exit time. Skill/ultimate recovery can be cancelled from the final segment.";
        EditorUtility.SetDirty(profile.catalog); EditorUtility.SetDirty(profile);
        foreach (var combo in profile.ReachableCombos()) AssetDatabase.SaveAssetIfDirty(combo);
        AssetDatabase.SaveAssetIfDirty(controller); AssetDatabase.SaveAssetIfDirty(profile.catalog); AssetDatabase.SaveAssetIfDirty(profile);
        PilotProfileMaintenance.ExportAndSeal(profile);
        Debug.Log("EXPANDED_COMBAT_MIGRATION_COMPLETE " + string.Join(",", profile.ReachableCombos().Select(c => c.skill.skillId)));
    }
    static ComboData DodgeAsset(string name, string state)
    {
        string path = ComboDirectory + name + ".asset";
        var combo = AssetDatabase.LoadAssetAtPath<ComboData>(path);
        if (combo) return combo;
        combo = ScriptableObject.CreateInstance<ComboData>(); combo.name = name;
        var serialized = new SerializedObject(combo);
        serialized.FindProperty("_comboName").stringValue = state;
        serialized.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(combo, path); return combo;
    }
    static IEnumerable<KeyValuePair<string, AnimatorState>> StateEntries(AnimatorController controller)
    {
        foreach (var layer in controller.layers)
            foreach (var state in Walk(layer.stateMachine, layer.name)) yield return state;
    }
    static IEnumerable<KeyValuePair<string, AnimatorState>> Walk(AnimatorStateMachine machine, string path)
    {
        foreach (var child in machine.states) yield return new KeyValuePair<string, AnimatorState>(path + "." + child.state.name, child.state);
        foreach (var child in machine.stateMachines) foreach (var entry in Walk(child.stateMachine, path + "." + child.stateMachine.name)) yield return entry;
    }
    static IEnumerable<AnimatorState> FindStates(AnimatorController controller) => StateEntries(controller).Select(e => e.Value);
    static void Register(PilotBattleProfile profile, ComboData combo, string id, SkillType type, Dictionary<string, Motion> states,
        string[] names, long trimEnd = 0, RootMotionSettings originalMotion = null)
    {
        if (combo.skill != null && combo.skill.registered)
        {
            if (combo.skill.skillId != id || !profile.catalog.skills.Contains(combo)) throw new InvalidOperationException("Conflicting registered source: " + combo.name);
            return; // Never overwrite subsequent edits to the migrated configuration.
        }
        var skill = new SkillAuthoringData { registered = true, skillId = id, displayName = combo.name, category = type.ToString(), skillType = type };
        skill.tracks.Add(new SkillTrack { id = "hits", name = "Logical hit points", kind = TrackKind.HitBox });
        skill.tracks.Add(new SkillTrack { id = "gates", name = "Logical cancellation", kind = TrackKind.Event });
        skill.tracks.Add(new SkillTrack { id = "vfx", name = "Legacy presentation", kind = TrackKind.Vfx });
        int start = 0, hit = 0;
        foreach (var name in names)
        {
            var entry = states.Single(s => s.Key.EndsWith("." + name, StringComparison.Ordinal));
            var clip = entry.Value as AnimationClip;
            if (!clip) throw new InvalidOperationException("Expected clip for " + entry.Key);
            long end = trimEnd > 0 ? trimEnd : Us(clip.length);
            var segment = new AnimationSegment { id = "animation-" + (skill.animations.Count + 1), clip = clip, startFrame = start,
                endFrame = start + Frames(end), trimEndUs = end, statePaths = new List<string> { entry.Key }, rootSource = RootBindingSource.Explicit,
                additionalRootMotion = originalMotion?.json ?? BakeMotion(clip, end, id + "-" + skill.animations.Count),
                additionalEndFrameExclusive = originalMotion?.endFrameExclusive ?? 0, additionalDistancePermille = originalMotion?.distancePermille ?? 1000,
                tailPolicy = MotionTailPolicy.HoldAtCoverageEnd };
            skill.animations.Add(segment);
            foreach (var e in AnimationUtility.GetAnimationEvents(clip).Where(e => e.functionName == "ATK" && Us(e.time) < end))
                AddHit(skill, combo, start + Frames(Us(e.time)), hit++, type == SkillType.Normal ? 200000 : 90000);
            start = segment.endFrame;
        }
        if (type == SkillType.Normal)
        {
            if (hit != 0) throw new InvalidOperationException("Rush source events changed; review authored migration defaults.");
            AddHit(skill, combo, 6, hit++, 200000); AddHit(skill, combo, 12, hit++, 200000);
            AddGate(skill, "combo.preinput", 15); AddGate(skill, "combo.attack-ready", 18); AddGate(skill, "combo.move-interrupt", 18);
            foreach (int f in new[] { 6, 12 }) skill.vfx.Add(new VfxCue { id = "rush-slash-" + f, trackId = "vfx", resourceId = "miyabi-legacy-slash06",
                startFrame = f, endFrame = f + 1, attachment = AttachmentMode.Root, classification = VfxClass.AttackCritical,
                lifetime = VfxLifetime.AutoComplete, maxLifetimeFrames = PilotPresentation.NaturalLifetimeBound((GameObject)profile.catalog.resources.Single(r => r.id == "miyabi-legacy-slash06").asset) });
        }
        else if (type != SkillType.Dodge) AddGate(skill, "combo.move-interrupt", skill.animations.Last().startFrame);
        combo.skill = skill; profile.catalog.skills.Add(combo); EditorUtility.SetDirty(combo);
    }
    static void AddHit(SkillAuthoringData skill, ComboData combo, int frame, int i, int sector)
    {
        if (frame >= skill.animations.Last().endFrame) throw new InvalidOperationException("Hit outside animation range.");
        skill.hitGroups.Add(new HitGroup { id = "hit-" + i, mode = HitMode.Single, damageSource = DamageSource.ComboBaseDamage });
        skill.hitBoxes.Add(new HitBox { id = "box-" + i, trackId = "hits", hitGroupId = "hit-" + i, startFrame = frame, endFrame = frame + 1,
            shape = ShapeKind.Sector, radius = checked((int)Math.Round(combo.attackDistance * 10000)), height = 1, sectorAngleMilliDegrees = sector });
    }
    static void AddGate(SkillAuthoringData skill, string id, int frame) => skill.events.Add(new SkillEvent { id = id.Replace('.', '-'), trackId = "gates", eventId = id, frame = frame,
        execution = EventExecution.Authoritative, parameterKind = EventParameterKind.ComboGate, comboGate = new ComboGateParameters { enabled = true } });

    static TextAsset BakeMotion(AnimationClip clip, long endUs, string id)
    {
        Directory.CreateDirectory("Assets/Resources/RootMotion/Expanded");
        string path = "Assets/Resources/RootMotion/Expanded/" + id + ".json";
        var scene = EditorSceneManager.NewPreviewScene(); PlayableGraph graph = default; GameObject instance = null;
        try
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>("BattleScene/Role/Character"), scene);
            var animator = instance.GetComponentInChildren<ZZZ.Player>(true).GetComponent<Animator>();
            foreach (var script in instance.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
            animator.runtimeAnimatorController = null; animator.applyRootMotion = true; animator.fireEvents = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.Rebind();
            graph = PlayableGraph.Create("Expanded action bake"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var playable = AnimationClipPlayable.Create(graph, clip); playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
            AnimationPlayableOutput.Create(graph, "motion", animator).SetSourcePlayable(playable); graph.Play(); graph.Evaluate(0);
            var inverse = Quaternion.Inverse(animator.transform.rotation);
            var data = new RootMotionJsonData { clipName = clip.name, sourceAnimationFrameRate = Mathf.RoundToInt(clip.frameRate), logicFrameIntervalMs = 33,
                precision = 10000, clipLengthMs = (int)Math.Round(clip.length * 1000), rootMotionEndAnimationFrame = -1, rootMotionEndTimeMs = (int)Math.Round(endUs / 1000.0) };
            Vector3 total = Vector3.zero; Vector3Int previous = Vector3Int.zero; int previousMs = 0; long previousUs = 0;
            for (int i = 0; i < Frames(endUs); i++)
            {
                long timeUs = Math.Min((i + 1L) * 33000, endUs);
                graph.Evaluate((timeUs - previousUs) / 1000000f); total += inverse * animator.deltaPosition;
                var position = new Vector3Int(Mathf.RoundToInt(total.x * 10000), Mathf.RoundToInt(total.y * 10000), Mathf.RoundToInt(total.z * 10000));
                var delta = position - previous; int ms = (int)Math.Round(timeUs / 1000.0);
                data.frames.Add(new RootMotionJsonFrame { logicFrame = i, sampleEndTimeMs = ms, sampleDurationMs = ms - previousMs,
                    deltaX = delta.x, deltaY = delta.y, deltaZ = delta.z, positionX = position.x, positionY = position.y, positionZ = position.z });
                previous = position; previousMs = ms; previousUs = timeUs;
            }
            data.frameCount = data.frames.Count; data.totalX = previous.x; data.totalY = previous.y; data.totalZ = previous.z;
            if (!RootMotionClip.TryCreate(data, 33, 10000, out _, out var error)) throw new InvalidOperationException(error);
            File.WriteAllText(path, JsonUtility.ToJson(data, true) + "\n", new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        }
        finally { if (graph.IsValid()) graph.Destroy(); if (instance) UnityEngine.Object.DestroyImmediate(instance); EditorSceneManager.ClosePreviewScene(scene); }
    }
    static void AddUltimateVfx(PilotBattleProfile profile)
    {
        const string id = "miyabi-ultimate-ex";
        const string path = "Assets/Resources/Prefabs/VFX/Miyabi_UltimateEX.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!prefab)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var original = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("3b465010f1063fd46b68d705587aa570"));
                var clone = (GameObject)PrefabUtility.InstantiatePrefab(original, scene);
                // Legacy HS_VolumeChanger controls global postprocessing/camera. The view owns those now.
                foreach (var script in clone.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(script);
                prefab = PrefabUtility.SaveAsPrefabAsset(clone, path); UnityEngine.Object.DestroyImmediate(clone);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        int bound = PilotPresentation.NaturalLifetimeBound(prefab);
        if (!profile.catalog.resources.Any(r => r.id == id)) profile.catalog.resources.Add(new SkillResourceBinding { id = id, kind = ResourceKind.VfxPrefab, asset = prefab });
        var skill = profile.ultimateCombo.skill;
        foreach (var a in skill.animations)
            foreach (var e in AnimationUtility.GetAnimationEvents(a.clip).Where(e => e.functionName == "PlayVFX"))
            {
                if (e.stringParameter != "Unagi_EX") throw new InvalidOperationException("Unknown ultimate VFX alias " + e.stringParameter);
                string cueId = "ex-" + a.id;
                if (skill.vfx.Any(v => v.id == cueId)) continue;
                int f = a.startFrame + Frames(Us(e.time));
                skill.vfx.Add(new VfxCue { id = cueId, trackId = "vfx", resourceId = id, startFrame = f, endFrame = f + 1,
                    attachment = AttachmentMode.Root, classification = VfxClass.AttackCritical, lifetime = VfxLifetime.AutoComplete, maxLifetimeFrames = bound });
            }
        EditorUtility.SetDirty(profile.ultimateCombo);
    }
    public static void Inspect()
    {
        var profile = AssetDatabase.LoadAssetAtPath<PilotBattleProfile>(ProfilePath);
        var output = new StringBuilder();
        foreach (var state in SkillAssetValidator.States((AnimatorController)profile.catalog.animatorController)
            .Where(s => s.Key.Contains("Skill") || s.Key.Contains("Dodge") || s.Key.Contains("Rush_ATK")))
        {
            var clip = state.Value as AnimationClip;
            output.AppendLine(state.Key + " | clip=" + clip?.name + " length=" + clip?.length);
            if (clip) foreach (var e in AnimationUtility.GetAnimationEvents(clip))
                output.AppendLine("  " + e.time.ToString("R") + " " + e.functionName + " " + e.stringParameter + " int=" + e.intParameter);
        }
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/ExpandedCombatSources.txt", output.ToString());
    }
}
