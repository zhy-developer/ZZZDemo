using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SkillConfig.Editor
{
    // No AssetDatabase or gameplay dependency. Tests inject failure immediately before each I/O phase.
    public static class SkillConfigFileStore
    {
        public static string Serialize(SkillConfigDto dto)
        {
            var errors = SkillConfigValidation.Validate(dto).Where(d => d.severity == DiagnosticSeverity.Error).ToList();
            if (dto != null && dto.diagnostics != null) errors.AddRange(dto.diagnostics.Where(d => d != null && d.severity == DiagnosticSeverity.Error));
            if (errors.Count > 0) throw new InvalidDataException(string.Join("\n", errors));
            dto.skills = dto.skills.OrderBy(s => s.skillId, StringComparer.Ordinal).ToList();
            dto.resources = dto.resources.OrderBy(r => r.id, StringComparer.Ordinal).ToList();
            foreach (var skill in dto.skills)
            {
                skill.animations = skill.animations.OrderBy(a => a.startFrame).ThenBy(a => a.id, StringComparer.Ordinal).ToList();
                skill.hitGroups = skill.hitGroups.OrderBy(g => g.id, StringComparer.Ordinal).ToList();
                skill.hitBoxes = skill.hitBoxes.OrderBy(b => b.startFrame).ThenBy(b => b.id, StringComparer.Ordinal).ToList();
                skill.events = skill.events.OrderBy(e => e.frame).ThenBy(e => e.execution).ThenBy(e => e.order).ThenBy(e => e.id, StringComparer.Ordinal).ToList();
                skill.invincibility = skill.invincibility.OrderBy(w => w.startFrame).ThenBy(w => w.id, StringComparer.Ordinal).ToList();
                skill.interrupts = skill.interrupts.OrderBy(w => w.startFrame).ThenBy(w => w.id, StringComparer.Ordinal).ToList();
                skill.vfx = skill.vfx.OrderBy(v => v.startFrame).ThenBy(v => v.id, StringComparer.Ordinal).ToList();
                skill.sfx = skill.sfx.OrderBy(a => a.startFrame).ThenBy(a => a.id, StringComparer.Ordinal).ToList();
            }
            string json = JsonUtility.ToJson(dto, true) + "\n";
            if (!SkillConfigLoader.TryParse(json, out _, out var diagnostics)) throw new InvalidDataException(string.Join("\n", diagnostics));
            return json;
        }
        public static void Write(string path, SkillConfigDto dto, Action<string> beforeStage = null)
        {
            string json = Serialize(dto); // No filesystem writes before all validation/serialization succeeds.
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            Directory.CreateDirectory(directory);
            string temp = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                beforeStage?.Invoke("write");
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var bytes = new UTF8Encoding(false).GetBytes(json);
                    stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
                }
                beforeStage?.Invoke("readback");
                string readback = File.ReadAllText(temp, Encoding.UTF8);
                if (readback != json || !SkillConfigLoader.TryParse(readback, out _, out _)) throw new InvalidDataException("Temporary export readback failed.");
                beforeStage?.Invoke("replace");
                if (File.Exists(fullPath)) File.Replace(temp, fullPath, null);
                else File.Move(temp, fullPath);
                // No delete-and-rewrite fallback: unsupported replacement leaves the old file intact.
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
