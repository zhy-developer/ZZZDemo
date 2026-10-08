using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using FrameSync.RootMotion;
using UnityEngine;

namespace SkillConfig.Editor
{
    public static class RootMotionBindingValidator
    {
        public static string Hash(string value)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }
        public static MotionBindingDto Build(ComboData owner, AnimationSegment source, AnimationDto animation, List<Diagnostic> diagnostics, string loc)
        {
            if (source.rootSource == RootBindingSource.None) return null;
            TextAsset json;
            int end, distance;
            if (source.rootSource == RootBindingSource.LegacyCombo)
            {
                json = owner.rootMotion?.json;
                end = owner.rootMotion?.endFrameExclusive ?? 0;
                distance = owner.rootMotion?.distancePermille ?? 0;
            }
            else if (source.rootSource == RootBindingSource.Explicit)
            { json = source.additionalRootMotion; end = source.additionalEndFrameExclusive; distance = source.additionalDistancePermille; }
            else { diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, "ROOT_SOURCE", loc, "Unknown binding source.")); return null; }
            if (json == null) { diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, "ROOT_MISSING", loc, "Selected Root Motion source is missing.")); return null; }
            try
            {
                var data = JsonUtility.FromJson<RootMotionJsonData>(json.text);
                if (!RootMotionClip.TryCreate(data, 33, 10000, out _, out string error)) throw new FormatException(error);
                if (end < 0 || end > data.frameCount || distance <= 0 || distance > 10000) throw new FormatException("Invalid legacy end frame/distance.");
                int effectiveEnd = end == 0 ? data.frameCount : end;
                return new MotionBindingDto {
                    resourcesPath = SkillAssetValidator.ResourcesPath(json, diagnostics, loc), contentHash = Hash(json.text), clipName = data.clipName,
                    endFrameExclusive = end, distancePermille = distance, sampleCount = data.frameCount,
                    sourceClipLengthMs = data.clipLengthMs, coverageEndMs = data.frames[effectiveEnd - 1].sampleEndTimeMs,
                    requiresTimeRemap = animation.trimStartUs != 0 || animation.trimEndUs != animation.sourceLengthUs || animation.speedPermille != 1000,
                    provenanceVerified = false, tailPolicy = source.tailPolicy
                };
            }
            catch (Exception ex) { diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, "ROOT_DATA", loc, ex.Message)); return null; }
        }
    }
}
