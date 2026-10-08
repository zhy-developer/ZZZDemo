using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace SkillConfig.Editor
{
    public static class SkillConfigExporter
    {
        public static string PathFor(CharacterSkillCatalog catalog)
        {
            if (catalog == null || !SkillConfigValidation.SafeId(catalog.characterId)) throw new ArgumentException("Invalid CharacterID.");
            return "Assets/Resources/SkillConfigs/Character_" + catalog.characterId + "_Skills.json";
        }
        public static bool IsStale(CharacterSkillCatalog catalog)
        {
            try
            {
                var dto = SkillAssetValidator.Build(catalog, out var issues);
                if (issues.Any(d => d.severity == DiagnosticSeverity.Error)) return true;
                string path = PathFor(catalog);
                return !File.Exists(path) || File.ReadAllText(path) != SkillConfigFileStore.Serialize(dto);
            }
            catch { return true; }
        }
        public static List<Diagnostic> Export(CharacterSkillCatalog catalog)
        {
            // Persist source first: dependency hashes must describe the saved sources, not a transient state.
            if (catalog != null)
            {
                AssetDatabase.SaveAssetIfDirty(catalog);
                if (catalog.skills != null) foreach (var skill in catalog.skills) if (skill != null) AssetDatabase.SaveAssetIfDirty(skill);
            }
            var dto = SkillAssetValidator.Build(catalog, out var issues);
            if (issues.Any(d => d.severity == DiagnosticSeverity.Error)) return issues;
            try
            {
                string path = PathFor(catalog);
                SkillConfigFileStore.Write(path, dto);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            catch (Exception ex) { issues.Add(new Diagnostic(DiagnosticSeverity.Error, "EXPORT_IO", catalog.name, ex.Message)); }
            return issues;
        }
        public static Dictionary<string, List<Diagnostic>> ExportAll()
        {
            var results = new Dictionary<string, List<Diagnostic>>();
            foreach (var guid in AssetDatabase.FindAssets("t:CharacterSkillCatalog").OrderBy(g => g, StringComparer.Ordinal))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                try { results[path] = Export(AssetDatabase.LoadAssetAtPath<CharacterSkillCatalog>(path)); }
                catch (Exception ex) { results[path] = new List<Diagnostic> { new Diagnostic(DiagnosticSeverity.Error, "EXPORT", path, ex.Message) }; }
            }
            return results;
        }
    }
}
