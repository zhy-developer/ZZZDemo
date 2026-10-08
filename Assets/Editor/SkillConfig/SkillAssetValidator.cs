using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SkillConfig.Editor
{
    public static class SkillAssetValidator
    {
        public static string ResourcesPath(UnityEngine.Object asset, List<Diagnostic> issues, string location)
        {
            string path = AssetDatabase.GetAssetPath(asset).Replace('\\', '/');
            int at = path.LastIndexOf("/Resources/", StringComparison.Ordinal);
            if (asset == null || at < 0) { issues.Add(new Diagnostic(DiagnosticSeverity.Error, "RESOURCES_PATH", location, "Resource must be a resolvable Resources asset.")); return null; }
            string key = path.Substring(at + 11);
            key = key.Substring(0, key.Length - Path.GetExtension(key).Length);
            var collisions = AssetDatabase.GetAllAssetPaths().Where(p => {
                int i = p.LastIndexOf("/Resources/", StringComparison.Ordinal);
                return i >= 0 && !AssetDatabase.IsValidFolder(p) && string.Equals(Path.ChangeExtension(p.Substring(i + 11), null), key, StringComparison.OrdinalIgnoreCase);
            }).ToArray();
            if (collisions.Length != 1 || Resources.Load(key, asset.GetType()) != asset)
                issues.Add(new Diagnostic(DiagnosticSeverity.Error, "RESOURCES_AMBIGUOUS", location, "Resource does not resolve uniquely to the referenced object: " + key));
            return key;
        }

        public static Dictionary<string, Motion> States(AnimatorController controller)
        {
            var result = new Dictionary<string, Motion>(StringComparer.Ordinal);
            foreach (var layer in controller.layers) Walk(layer.stateMachine, layer.name, result);
            return result;
        }
        private static void Walk(AnimatorStateMachine machine, string prefix, Dictionary<string, Motion> result)
        {
            foreach (var state in machine.states) result[prefix + "." + state.state.name] = state.state.motion;
            foreach (var child in machine.stateMachines) Walk(child.stateMachine, prefix + "." + child.stateMachine.name, result);
        }
        private static bool ContainsClip(Motion motion, AnimationClip clip)
        {
            if (motion == clip) return true;
            return motion is BlendTree tree && tree.children.Any(c => ContainsClip(c.motion, clip));
        }
        public static SkillConfigDto Build(CharacterSkillCatalog catalog, out List<Diagnostic> issues)
        {
            issues = new List<Diagnostic>();
            var dto = new SkillConfigDto { schemaVersion = 1, logicFrameIntervalMs = 33, precision = 10000 };
            if (catalog == null) { issues.Add(new Diagnostic(DiagnosticSeverity.Error, "CATALOG", "catalog", "Select a catalog.")); return dto; }
            dto.characterId = catalog.characterId; dto.completeness = catalog.completeness;
            dto.migrationNotes = catalog.migrationNotes; dto.battleReady = false;
            dto.hurtBox = catalog.hurtBox == null ? null : JsonUtility.FromJson<HurtBoxData>(JsonUtility.ToJson(catalog.hurtBox));
            dto.hurtBoxConfigured = catalog.hurtBoxConfigured;
            var fingerprint = new StringBuilder(EditorJsonUtility.ToJson(catalog));
            AddDependency(fingerprint, catalog);
            var states = catalog.animatorController is AnimatorController controller ? States(controller) : new Dictionary<string, Motion>();
            if (states.Count == 0) issues.Add(new Diagnostic(DiagnosticSeverity.Error, "CONTROLLER", catalog.name, "A concrete AnimatorController is required; override controllers need an explicit future adapter."));
            if (catalog.skills == null || catalog.resources == null) { issues.Add(new Diagnostic(DiagnosticSeverity.Error, "COLLECTION", catalog.name, "Catalog lists cannot be null.")); return dto; }
            foreach (var r in catalog.resources)
            {
                if (r == null) { issues.Add(new Diagnostic(DiagnosticSeverity.Error, "RESOURCE", catalog.name, "Null resource binding.")); continue; }
                bool typeOk = r.kind == ResourceKind.VfxPrefab ? r.asset is GameObject : r.kind == ResourceKind.AudioClip && r.asset is AudioClip;
                if (!typeOk) issues.Add(new Diagnostic(DiagnosticSeverity.Error, "RESOURCE_TYPE", r.id, "Resource asset has the wrong type."));
                dto.resources.Add(new ResourceDto { id = r.id, kind = r.kind, path = ResourcesPath(r.asset, issues, r.id) });
                AddDependency(fingerprint, r.asset);
            }
            foreach (var combo in catalog.skills)
            {
                if (combo == null || combo.skill == null || !combo.skill.registered)
                { issues.Add(new Diagnostic(DiagnosticSeverity.Error, "UNREGISTERED", catalog.name, "Missing/unregistered ComboData.")); continue; }
                var source = combo.skill;
                fingerprint.Append(EditorJsonUtility.ToJson(combo)); AddDependency(fingerprint, combo);
                if (float.IsNaN(combo.comboDamage) || float.IsInfinity(combo.comboDamage) || combo.comboDamage < 0 || (double)combo.comboDamage > int.MaxValue || (double)combo.comboDamage != Math.Truncate(combo.comboDamage))
                    issues.Add(new Diagnostic(DiagnosticSeverity.Error, "BASE_DAMAGE", source.skillId, "Phase 1 integer HP contract requires a non-negative, exactly integral legacy base damage."));
                var s = new SkillDto {
                    skillId = source.skillId, displayName = source.displayName, category = source.category, skillType = source.skillType,
                    baseDamage = combo.comboDamage >= 0 && (double)combo.comboDamage <= int.MaxValue ? (int)combo.comboDamage : 0,
                    legacyEntryState = combo.comboName,
                    tracks = Clone(source.tracks), hitGroups = Clone(source.hitGroups), hitBoxes = Clone(source.hitBoxes),
                    invincibility = Clone(source.invincibility), interrupts = Clone(source.interrupts), events = Clone(source.events), vfx = Clone(source.vfx), sfx = Clone(source.sfx)
                };
                if (source.animations == null) { issues.Add(new Diagnostic(DiagnosticSeverity.Error, "ANIMATIONS", s.skillId, "Missing animation list.")); dto.skills.Add(s); continue; }
                foreach (var segment in source.animations)
                {
                    if (segment == null || segment.clip == null) { issues.Add(new Diagnostic(DiagnosticSeverity.Error, "CLIP", s.skillId, "Missing animation segment or Clip.")); continue; }
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(segment.clip, out string guid, out long localId);
                    var a = new AnimationDto {
                        id = segment.id, startFrame = segment.startFrame, endFrame = segment.endFrame,
                        clipName = segment.clip.name, clipGuid = guid, clipLocalId = localId,
                        sourceLengthUs = (long)Math.Round(segment.clip.length * 1000000.0, MidpointRounding.AwayFromZero),
                        trimStartUs = segment.trimStartUs, trimEndUs = segment.trimEndUs, speedPermille = segment.speedPermille,
                        statePaths = segment.statePaths == null ? null : new List<string>(segment.statePaths)
                    };
                    if (a.statePaths != null) foreach (string path in a.statePaths)
                        if (path == null || !states.TryGetValue(path, out var motion) || !ContainsClip(motion, segment.clip))
                            issues.Add(new Diagnostic(DiagnosticSeverity.Error, "STATE_CLIP", s.skillId + "/" + a.id, "State is missing or does not reference this Clip: " + path));
                    a.rootMotion = RootMotionBindingValidator.Build(combo, segment, a, issues, s.skillId + "/" + a.id);
                    a.rootMotionBound = segment.rootSource != RootBindingSource.None;
                    AddDependency(fingerprint, segment.clip); AddDependency(fingerprint, segment.additionalRootMotion);
                    s.animations.Add(a);
                }
                s.animations = s.animations.OrderBy(a => a.startFrame).ThenBy(a => a.id, StringComparer.Ordinal).ToList();
                s.durationFrames = s.animations.Count == 0 ? 0 : s.animations.Last().endFrame;
                if (s.animations.Count > 0 && (s.animations[0].statePaths == null || !s.animations[0].statePaths.Any(p => p != null && (p == combo.comboName || p.EndsWith("." + combo.comboName, StringComparison.Ordinal)))))
                    issues.Add(new Diagnostic(DiagnosticSeverity.Error, "LEGACY_ENTRY", s.skillId, "First segment must include the existing ComboData entry; later segments may map other states."));
                dto.skills.Add(s);
            }
            // Validate global registration identity/ownership without modifying or initializing other assets.
            foreach (string guid in AssetDatabase.FindAssets("t:ComboData"))
            {
                var other = AssetDatabase.LoadAssetAtPath<ComboData>(AssetDatabase.GUIDToAssetPath(guid));
                if (other == null || other.skill == null || !other.skill.registered || catalog.skills.Contains(other)) continue;
                if (dto.skills.Any(s => s.skillId == other.skill.skillId)) issues.Add(new Diagnostic(DiagnosticSeverity.Error, "GLOBAL_SKILL_ID", other.name, "Another registered ComboData has this SkillID."));
            }
            foreach (string guid in AssetDatabase.FindAssets("t:CharacterSkillCatalog"))
            {
                var other = AssetDatabase.LoadAssetAtPath<CharacterSkillCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                if (other == null || other == catalog) continue;
                if (string.Equals(other.characterId, catalog.characterId, StringComparison.OrdinalIgnoreCase) || (other.skills != null && other.skills.Any(s => s != null && catalog.skills.Contains(s))))
                    issues.Add(new Diagnostic(DiagnosticSeverity.Error, "CATALOG_OWNERSHIP", other.name, "Duplicate CharacterID or skill owned by another catalog."));
            }
            dto.sourceFingerprint = RootMotionBindingValidator.Hash(fingerprint.ToString());
            issues.AddRange(SkillConfigValidation.Validate(dto));
            if (dto.skills.Any(s => s.vfx != null && s.vfx.Any(v => v != null && v.classification == VfxClass.AttackCritical)))
                issues.Add(new Diagnostic(DiagnosticSeverity.Warning, "COMBO_TIMING_UNVERIFIED", catalog.name, "Early combo cancellation timing must be approved before critical effects are battle-ready."));
            dto.diagnostics = issues.OrderBy(d => d.location, StringComparer.Ordinal).ThenBy(d => d.code, StringComparer.Ordinal).ThenBy(d => d.message, StringComparer.Ordinal).ToList();
            return dto;
        }
        private static void AddDependency(StringBuilder text, UnityEngine.Object asset)
        {
            if (asset == null) { text.Append("<null>"); return; }
            var path = AssetDatabase.GetAssetPath(asset);
            text.Append(path).Append(AssetDatabase.GetAssetDependencyHash(path).ToString());
        }
        [Serializable] private sealed class ListWrapper<T> { public List<T> items; }
        private static List<T> Clone<T>(List<T> source) => source == null ? null : JsonUtility.FromJson<ListWrapper<T>>(JsonUtility.ToJson(new ListWrapper<T> { items = source })).items;
    }
}
