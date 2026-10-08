using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SkillConfig.Editor
{
    public static class SkillPilotSetup
    {
        public const string CatalogPath = "Assets/ScriptableObject/SkillConfigs/MiyabiPhase1.asset";
        public const string ControllerPath = "Assets/Art/AnimatorController/星见雅.controller";
        public static string ComboPath(int step) => "Assets/ScriptableObject/ComboData/星见雅/Unagi_Normal_" + step + ".asset";
        [MenuItem("Tools/Skill Config/Register Miyabi Normal 1-2 Pilot")]
        public static void Register()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) throw new InvalidOperationException("Miyabi controller missing.");
            var states = SkillAssetValidator.States(controller);
            // Resolve every required asset before changing either pilot.
            var combos = Enumerable.Range(1, 2).Select(i => AssetDatabase.LoadAssetAtPath<ComboData>(ComboPath(i))).ToArray();
            var paths = combos.Select(c => c == null ? null : states.Keys.SingleOrDefault(p => p.EndsWith("." + c.comboName, StringComparison.Ordinal))).ToArray();
            if (combos.Any(c => c == null || c.rootMotion == null || c.rootMotion.json == null) || paths.Any(p => p == null) || paths.Any(p => !(states[p] is AnimationClip)))
                throw new InvalidOperationException("Pilot ComboData/Clip/Root Motion binding is missing or ambiguous.");
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterSkillCatalog>(CatalogPath);
            if (catalog == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath)); AssetDatabase.Refresh();
                catalog = ScriptableObject.CreateInstance<CharacterSkillCatalog>();
                catalog.characterId = "miyabi"; catalog.completeness = CatalogCompleteness.PartialPilot;
                catalog.migrationNotes = "Phase 1 data pilot: only Normal1/Normal2. Other skills and all combat integration are not migrated.";
                catalog.animatorController = controller;
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            Undo.RecordObject(catalog, "Register Miyabi pilot");
            for (int i = 0; i < combos.Length; i++)
            {
                var combo = combos[i];
                if (combo.skill == null || !combo.skill.registered)
                {
                    Undo.RecordObject(combo, "Register pilot skill");
                    var clip = (AnimationClip)states[paths[i]];
                    long length = (long)Math.Round(clip.length * 1000000.0, MidpointRounding.AwayFromZero);
                    combo.skill = new SkillAuthoringData { registered = true, skillId = "miyabi-normal-" + (i + 1), displayName = combo.name, category = "Normal", skillType = SkillType.Normal };
                    combo.skill.animations.Add(new AnimationSegment {
                        id = "animation-1", clip = clip, startFrame = 0, endFrame = (int)((length + 32999) / 33000),
                        trimStartUs = 0, trimEndUs = length, speedPermille = 1000,
                        statePaths = new List<string> { paths[i] }, rootSource = RootBindingSource.LegacyCombo, tailPolicy = MotionTailPolicy.Unverified
                    });
                    EditorUtility.SetDirty(combo);
                }
                if (!catalog.skills.Contains(combo)) catalog.skills.Add(combo);
            }
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
            foreach (var combo in combos) AssetDatabase.SaveAssetIfDirty(combo);
            var issues = SkillConfigExporter.Export(catalog);
            foreach (var d in issues) Debug.Log(d.ToString(), catalog);
            if (issues.Any(d => d.severity == DiagnosticSeverity.Error)) throw new InvalidOperationException("Pilot configured but export blocked; inspect diagnostics. No gameplay assets were retimed.");
            Selection.activeObject = catalog;
        }
    }
}
