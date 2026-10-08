using System;
using System.Collections.Generic;
using System.Linq;

namespace SkillConfig
{
    public static class SkillConfigValidation
    {
        public const int SchemaVersion = 1;
        public static bool SafeId(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 128 &&
            id.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-');
        public static bool ResourcePath(string path) => !string.IsNullOrWhiteSpace(path) &&
            !path.StartsWith("/") && !path.Contains("\\") && !path.Contains(":") &&
            path.Split('/').All(p => p.Length > 0 && p != "." && p != "..");

        public static List<Diagnostic> Validate(SkillConfigDto data)
        {
            var result = new List<Diagnostic>();
            Action<string, string, string> error = (code, loc, msg) => result.Add(new Diagnostic(DiagnosticSeverity.Error, code, loc, msg));
            Action<string, string, string> warn = (code, loc, msg) => result.Add(new Diagnostic(DiagnosticSeverity.Warning, code, loc, msg));
            if (data == null) { error("DOCUMENT", "catalog", "Document is null."); return result; }
            if (data.schemaVersion != SchemaVersion) error("VERSION", "catalog", "Unsupported or missing SchemaVersion; explicit migration required.");
            if (!SafeId(data.characterId)) error("CHARACTER_ID", "catalog", "Use a stable filesystem-safe CharacterID.");
            if (data.logicFrameIntervalMs != 33 || data.precision != 10000) error("UNITS", "catalog", "This schema profile requires 33ms and precision 10000.");
            if (!Enum.IsDefined(typeof(CatalogCompleteness), data.completeness) || data.battleReady)
                error("READINESS", "catalog", "Phase 1 documents cannot declare battleReady.");
            if (data.completeness == CatalogCompleteness.PartialPilot) warn("PARTIAL_CATALOG", "catalog", "Partial migration; not a complete combat configuration.");
            if (!data.hurtBoxConfigured)
            {
                if (data.completeness == CatalogCompleteness.Complete) error("HURTBOX", "catalog", "Complete catalog requires a vertical Capsule HurtBox.");
                else warn("HURTBOX_UNCONFIGURED", "catalog", "Pilot has no authoritative HurtBox yet.");
            }
            else if (data.hurtBox == null || data.hurtBox.radius <= 0 || data.hurtBox.height < (long)data.hurtBox.radius * 2)
                error("HURTBOX", "catalog", "Capsule radius must be positive; total height must be at least its diameter.");
            if (data.resources == null || data.skills == null || data.diagnostics == null)
            { error("COLLECTION", "catalog", "Required collections cannot be null."); return result; }
            var resources = new Dictionary<string, ResourceKind>(StringComparer.Ordinal);
            foreach (var r in data.resources)
            {
                if (r == null || !SafeId(r.id) || resources.ContainsKey(r.id)) { error("RESOURCE_ID", "resources", "Missing/duplicate resource ID."); continue; }
                resources.Add(r.id, r.kind);
                if (!ResourcePath(r.path) || !Enum.IsDefined(typeof(ResourceKind), r.kind)) error("RESOURCE_PATH", r.id, "Invalid resource path or kind.");
            }
            Action<string, ResourceKind, string, bool> resource = (id, kind, loc, required) => {
                if (string.IsNullOrEmpty(id) && !required) return;
                if (id == null || !resources.TryGetValue(id, out var actual) || kind != actual) error("RESOURCE_REF", loc, "Missing/wrong-kind resource: " + id);
            };
            var skillIds = new HashSet<string>(StringComparer.Ordinal);
            if (data.skills.Count == 0) error("NO_SKILLS", "catalog", "Register at least one skill.");
            foreach (var s in data.skills)
            {
                if (s == null) { error("SKILL_NULL", "skills", "Null skill."); continue; }
                string loc = s.skillId ?? "skill";
                if (!SafeId(s.skillId) || !skillIds.Add(s.skillId)) error("SKILL_ID", loc, "Missing or duplicate stable SkillID.");
                if (s.skillType != SkillType.Normal && s.skillType != SkillType.Ultimate) error("SKILL_TYPE", loc, "Explicit Normal or Ultimate required; legacy AttackStyle is not inferred.");
                if (s.baseDamage < 0 || s.durationFrames <= 0 || s.durationFrames > 1000000) error("SKILL_RANGE", loc, "Invalid base damage or duration.");
                if (s.animations == null || s.tracks == null || s.hitGroups == null || s.hitBoxes == null || s.invincibility == null || s.interrupts == null || s.events == null || s.vfx == null || s.sfx == null)
                { error("COLLECTION", loc, "Required skill collections cannot be null."); continue; }
                var ids = new HashSet<string>(StringComparer.Ordinal);
                Action<string, string> idCheck = (id, where) => { if (!SafeId(id) || !ids.Add(id)) error("ITEM_ID", where, "Missing/duplicate item ID within skill."); };
                var tracks = new Dictionary<string, TrackKind>(StringComparer.Ordinal);
                foreach (var t in s.tracks)
                {
                    if (t == null) { error("TRACK_NULL", loc, "Null track."); continue; }
                    idCheck(t.id, loc + "/tracks");
                    if (!Enum.IsDefined(typeof(TrackKind), t.kind)) error("TRACK_KIND", loc, "Unknown track kind.");
                    if (SafeId(t.id)) tracks[t.id] = t.kind;
                }
                Action<string, TrackKind, string> track = (id, kind, where) => { if (id == null || !tracks.TryGetValue(id, out var actual) || actual != kind) error("TRACK_REF", where, "Missing/wrong-kind track."); };
                Action<int, int, string> window = (start, end, where) => { if (start < 0 || start >= end || end > s.durationFrames) error("WINDOW", where, "Expected 0 <= start < end <= duration."); };
                int next = 0;
                foreach (var a in s.animations)
                {
                    if (a == null) { error("ANIMATION_NULL", loc, "Null animation segment."); continue; }
                    string aloc = loc + "/animation/" + a.id;
                    idCheck(a.id, aloc); window(a.startFrame, a.endFrame, aloc);
                    if (a.startFrame != next) error("ANIMATION_CONTINUITY", aloc, "Animation track must start at zero and be contiguous.");
                    next = a.endFrame;
                    if (string.IsNullOrWhiteSpace(a.clipName) || string.IsNullOrWhiteSpace(a.clipGuid) || a.clipLocalId == 0 || a.statePaths == null || a.statePaths.Count == 0 || a.statePaths.Any(string.IsNullOrWhiteSpace) || a.statePaths.Distinct().Count() != a.statePaths.Count)
                        error("ANIMATION_REF", aloc, "Clip identity and one or more distinct full state paths required.");
                    if (a.sourceLengthUs <= 0 || a.sourceLengthUs > 86400000000L || a.trimStartUs < 0 || a.trimStartUs >= a.trimEndUs || a.trimEndUs > a.sourceLengthUs || a.speedPermille <= 0 || a.speedPermille > 100000)
                        error("ANIMATION_TIME", aloc, "Invalid source length, trim or speed.");
                    else if (data.logicFrameIntervalMs > 0)
                    {
                        long divisor = (long)data.logicFrameIntervalMs * 1000 * a.speedPermille;
                        long expected = ((a.trimEndUs - a.trimStartUs) * 1000 + divisor - 1) / divisor;
                        if (expected != (long)a.endFrame - a.startFrame) error("ANIMATION_DURATION", aloc, "Frame span must be ceil(trim duration / playback speed / tick).");
                    }
                    var m = a.rootMotion;
                    if (!a.rootMotionBound) continue;
                    if (m == null) { error("MOTION_BINDING", aloc, "Declared motion binding is missing."); continue; }
                    if (!ResourcePath(m.resourcesPath) || string.IsNullOrEmpty(m.contentHash) || m.sampleCount <= 0 || m.sourceClipLengthMs <= 0 || m.coverageEndMs <= 0 || m.coverageEndMs > m.sourceClipLengthMs || m.endFrameExclusive < 0 || m.endFrameExclusive > m.sampleCount || m.distancePermille <= 0 || m.distancePermille > 10000 || !Enum.IsDefined(typeof(MotionTailPolicy), m.tailPolicy))
                        error("MOTION_BINDING", aloc, "Invalid motion reference, coverage or settings.");
                    if (!string.Equals(m.clipName, a.clipName, StringComparison.Ordinal)) error("MOTION_SOURCE", aloc, "Motion declares a different source clip.");
                    if ((long)m.coverageEndMs * 1000 > a.sourceLengthUs + 1000)
                        error("MOTION_COVERAGE", aloc, "Active motion coverage exceeds the available source animation time (1ms quantization tolerance).");
                    bool remap = a.trimStartUs != 0 || a.trimEndUs != a.sourceLengthUs || a.speedPermille != 1000;
                    if (m.requiresTimeRemap != remap) error("MOTION_REMAP_FLAG", aloc, "Remap flag does not match trim/speed.");
                    if (remap) warn("MOTION_REMAP_REQUIRED", aloc, "Requires future integer time sampler; legacy player does not support this mapping.");
                    if (!m.provenanceVerified) warn("MOTION_METADATA", aloc, "Legacy bake lacks source GUID/version/scale provenance; association is not proof of bake equivalence.");
                    if (Math.Abs((long)m.sourceClipLengthMs * 1000 - a.sourceLengthUs) > 1000)
                    {
                        if (m.provenanceVerified) error("MOTION_SOURCE_TIME", aloc, "Verified source duration is inconsistent.");
                        else warn("MOTION_SOURCE_TIME_UNKNOWN", aloc, "Source duration differs; missing bake provenance prevents confirming the mapping.");
                    }
                    // Coverage shorter than the animation is legal. Never demand duration equality.
                    if ((long)m.coverageEndMs * 1000 + 1000 < a.trimEndUs && m.tailPolicy == MotionTailPolicy.Unverified)
                        warn("MOTION_TAIL", aloc, "Uncovered tail: confirm intended stationary/ending segment before combat integration.");
                }
                if (s.animations.Count == 0 || next != s.durationFrames) error("ANIMATION_END", loc, "Duration must equal final animation endpoint.");
                var groups = new HashSet<string>(StringComparer.Ordinal);
                foreach (var g in s.hitGroups)
                {
                    if (g == null) { error("GROUP_NULL", loc, "Null group."); continue; }
                    idCheck(g.id, loc + "/groups"); if (g.id != null) groups.Add(g.id);
                    if (!Enum.IsDefined(typeof(HitMode), g.mode) || !Enum.IsDefined(typeof(DamageSource), g.damageSource) || (g.mode == HitMode.Repeat && g.repeatIntervalFrames <= 0) || g.hitStunFrames < 0 || g.knockbackFrames < 0 || g.explicitDamage < 0)
                        error("GROUP_RANGE", loc + "/" + g.id, "Invalid mode, interval or effects.");
                    if (g.damageSource == DamageSource.ComboBaseDamage && g.explicitDamage != 0) error("DUPLICATE_DAMAGE", loc + "/" + g.id, "Base-damage groups must not store a second editable damage value.");
                    resource(g.hitVfxResourceId, ResourceKind.VfxPrefab, loc, false); resource(g.hitSfxResourceId, ResourceKind.AudioClip, loc, false);
                }
                foreach (var b in s.hitBoxes)
                {
                    if (b == null) { error("BOX_NULL", loc, "Null hit box."); continue; }
                    idCheck(b.id, loc); track(b.trackId, TrackKind.HitBox, loc); window(b.startFrame, b.endFrame, loc);
                    if (b.hitGroupId == null || !groups.Contains(b.hitGroupId)) error("GROUP_REF", loc + "/" + b.id, "Unknown HitGroupID.");
                    bool valid = Enum.IsDefined(typeof(ShapeKind), b.shape);
                    if (b.shape == ShapeKind.Box) valid &= b.size.x > 0 && b.size.y > 0 && b.size.z > 0;
                    else valid &= b.radius > 0;
                    if (b.shape == ShapeKind.Capsule) valid &= b.height >= (long)b.radius * 2;
                    if (b.shape == ShapeKind.Cylinder || b.shape == ShapeKind.Sector) valid &= b.height > 0;
                    if (b.shape == ShapeKind.Sector) valid &= b.sectorAngleMilliDegrees > 0 && b.sectorAngleMilliDegrees <= 360000;
                    if (!valid) error("SHAPE", loc + "/" + b.id, "Illegal shape dimensions.");
                }
                int lastInterrupt = 0;
                foreach (var w in s.invincibility.Concat(s.interrupts))
                {
                    if (w == null) { error("WINDOW_NULL", loc, "Null window."); continue; }
                    idCheck(w.id, loc); window(w.startFrame, w.endFrame, loc);
                }
                foreach (var w in s.invincibility.Where(w => w != null)) track(w.trackId, TrackKind.Invincibility, loc);
                foreach (var w in s.interrupts.Where(w => w != null)) { track(w.trackId, TrackKind.Interrupt, loc); lastInterrupt = Math.Max(lastInterrupt, w.endFrame); }
                foreach (var e in s.events)
                {
                    if (e == null) { error("EVENT_NULL", loc, "Null event."); continue; }
                    idCheck(e.id, loc); track(e.trackId, TrackKind.Event, loc);
                    if (e.frame < 0 || e.frame >= s.durationFrames || !SkillEventRegistry.IsValid(e)) error("EVENT", loc + "/" + e.id, "Unknown event/version, wrong typed parameters or invalid frame.");
                }
                foreach (var v in s.vfx)
                {
                    if (v == null) { error("VFX_NULL", loc, "Null cue."); continue; }
                    idCheck(v.id, loc); track(v.trackId, TrackKind.Vfx, loc); resource(v.resourceId, ResourceKind.VfxPrefab, loc, true);
                    if (v.startFrame < 0 || v.startFrame >= s.durationFrames || v.scalePermille <= 0 || !Enum.IsDefined(typeof(VfxClass), v.classification) || !Enum.IsDefined(typeof(VfxLifetime), v.lifetime) || !Enum.IsDefined(typeof(AttachmentMode), v.attachment)) error("VFX", loc, "Invalid VFX configuration.");
                    if (v.lifetime == VfxLifetime.TimelineControlled) window(v.startFrame, v.endFrame, loc);
                    else if (v.maxLifetimeFrames <= 0) error("VFX_LIFETIME", loc, "AutoComplete requires an explicit safety lifetime bound.");
                    if (v.attachment == AttachmentMode.Bone && string.IsNullOrWhiteSpace(v.bone)) error("VFX_BONE", loc, "Bone binding requires a bone name.");
                    if (v.classification == VfxClass.AttackCritical && v.startFrame < lastInterrupt) error("CRITICAL_VFX", loc, "Critical VFX precedes the final interrupt-window end.");
                }
                foreach (var a in s.sfx)
                {
                    if (a == null) { error("SFX_NULL", loc, "Null cue."); continue; }
                    idCheck(a.id, loc); track(a.trackId, TrackKind.Sfx, loc); resource(a.resourceId, ResourceKind.AudioClip, loc, true);
                    // Explicit end can extend beyond cast duration for PlayToEnd; start must belong to the cast.
                    if (a.startFrame < 0 || a.startFrame >= s.durationFrames || a.endFrame <= a.startFrame || a.volumePermille < 0 || a.volumePermille > 1000 || a.fadeInFrames < 0 || a.fadeOutFrames < 0 || !Enum.IsDefined(typeof(SoundMode), a.mode) || !Enum.IsDefined(typeof(SoundInterrupt), a.onInterrupt) || (a.onInterrupt == SoundInterrupt.FadeOut && a.fadeOutFrames <= 0))
                        error("SFX", loc, "Invalid or unbounded SFX lifetime/fade.");
                }
                if (s.hitBoxes.Count == 0) warn("NO_HITBOX", loc, "Data pilot only: no hit boxes configured, no battle behavior enabled.");
            }
            return result;
        }
    }
}
