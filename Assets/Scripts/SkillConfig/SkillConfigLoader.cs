using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SkillConfig
{
    public sealed class SkillConfigSnapshot
    {
        private readonly string json;
        public string CharacterId { get; }
        public CatalogCompleteness Completeness { get; }
        public bool BattleReady => false;
        internal SkillConfigSnapshot(string json, SkillConfigDto data) { this.json = json; CharacterId = data.characterId; Completeness = data.completeness; }
        // Return a detached copy, never the snapshot's internal mutable collections.
        public SkillConfigDto CopyData() => JsonUtility.FromJson<SkillConfigDto>(json);
    }
    public static class SkillConfigLoader
    {
        public static bool TryParse(string json, out SkillConfigSnapshot snapshot, out List<Diagnostic> diagnostics, bool requireBattleReady = false)
        {
            snapshot = null;
            diagnostics = new List<Diagnostic>();
            try
            {
                if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{")) throw new FormatException("Expected JSON object.");
                var data = JsonUtility.FromJson<SkillConfigDto>(json);
                diagnostics.AddRange(SkillConfigValidation.Validate(data));
                if (requireBattleReady) diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, "NOT_BATTLE_READY", "catalog", "Phase 1 config cannot be consumed as a complete battle configuration."));
                if (diagnostics.Any(d => d.severity == DiagnosticSeverity.Error)) return false;
                if (data.diagnostics.Any(d => d == null || !Enum.IsDefined(typeof(DiagnosticSeverity), d.severity) || d.severity == DiagnosticSeverity.Error))
                    throw new FormatException("Export contains invalid/error diagnostics.");
                foreach (var d in data.diagnostics)
                    if (!diagnostics.Any(x => x.code == d.code && x.location == d.location && x.message == d.message)) diagnostics.Add(d);
                snapshot = new SkillConfigSnapshot(json, data);
                return true;
            }
            catch (Exception ex) { diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, "JSON", "catalog", ex.Message)); return false; }
        }
        public static bool TryLoad(string characterId, out SkillConfigSnapshot snapshot, out List<Diagnostic> diagnostics, bool requireBattleReady = false)
        {
            snapshot = null;
            diagnostics = new List<Diagnostic>();
            if (!SkillConfigValidation.SafeId(characterId)) { diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, "CHARACTER_ID", "catalog", "Invalid ID.")); return false; }
            var asset = Resources.Load<TextAsset>("SkillConfigs/Character_" + characterId + "_Skills");
            if (asset == null) { diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, "MISSING_CONFIG", characterId, "Resources config not found.")); return false; }
            if (!TryParse(asset.text, out snapshot, out diagnostics, requireBattleReady)) return false;
            if (snapshot.CharacterId == characterId)
            {
                var data = snapshot.CopyData();
                foreach (var r in data.resources)
                {
                    bool exists = r.kind == ResourceKind.VfxPrefab ? Resources.Load<GameObject>(r.path) != null : Resources.Load<AudioClip>(r.path) != null;
                    if (!exists) diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, "MISSING_RESOURCE", r.id, r.path));
                }
                foreach (var s in data.skills) foreach (var a in s.animations)
                {
                    if (!a.rootMotionBound) continue;
                    var motion = Resources.Load<TextAsset>(a.rootMotion.resourcesPath);
                    if (motion == null || SkillConfigHash.Sha256(motion.text) != a.rootMotion.contentHash)
                        diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, "MOTION_RESOURCE_CHANGED", s.skillId + "/" + a.id, "Bound Root Motion is missing or changed; revalidate and export."));
                }
                if (!diagnostics.Any(d => d.severity == DiagnosticSeverity.Error)) return true;
                snapshot = null; return false;
            }
            snapshot = null; diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, "CHARACTER_ID", characterId, "File identity mismatch.")); return false;
        }
    }
}
