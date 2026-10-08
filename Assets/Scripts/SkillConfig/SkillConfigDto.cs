using System;
using System.Collections.Generic;

namespace SkillConfig
{
    [Serializable] public sealed class SkillConfigDto
    {
        public int schemaVersion;
        public string characterId, sourceFingerprint;
        public int logicFrameIntervalMs, precision;
        public CatalogCompleteness completeness;
        public bool battleReady;
        public string migrationNotes;
        public bool hurtBoxConfigured;
        public HurtBoxData hurtBox;
        public List<ResourceDto> resources = new List<ResourceDto>();
        public List<SkillDto> skills = new List<SkillDto>();
        public List<Diagnostic> diagnostics = new List<Diagnostic>();
    }
    [Serializable] public sealed class ResourceDto { public string id, path; public ResourceKind kind; }
    [Serializable] public sealed class MotionBindingDto
    {
        public string resourcesPath, contentHash, clipName;
        public int endFrameExclusive, distancePermille, sampleCount, sourceClipLengthMs, coverageEndMs;
        public bool requiresTimeRemap;
        public bool provenanceVerified;
        public MotionTailPolicy tailPolicy;
    }
    [Serializable] public sealed class AnimationDto
    {
        public string id, clipName, clipGuid;
        public long clipLocalId, sourceLengthUs, trimStartUs, trimEndUs;
        public int startFrame, endFrame, speedPermille;
        public List<string> statePaths = new List<string>();
        public bool rootMotionBound;
        public MotionBindingDto rootMotion;
    }
    [Serializable] public sealed class SkillDto
    {
        public string skillId, displayName, category, legacyEntryState;
        public SkillType skillType;
        public int baseDamage, durationFrames;
        public List<AnimationDto> animations = new List<AnimationDto>();
        public List<SkillTrack> tracks = new List<SkillTrack>();
        public List<HitGroup> hitGroups = new List<HitGroup>();
        public List<HitBox> hitBoxes = new List<HitBox>();
        public List<FrameWindow> invincibility = new List<FrameWindow>();
        public List<FrameWindow> interrupts = new List<FrameWindow>();
        public List<SkillEvent> events = new List<SkillEvent>();
        public List<VfxCue> vfx = new List<VfxCue>();
        public List<SfxCue> sfx = new List<SfxCue>();
        public int ResolveDamage(HitGroup group) => group.damageSource == DamageSource.ComboBaseDamage ? baseDamage : group.explicitDamage;
    }
    public enum DiagnosticSeverity { Warning, Error }
    [Serializable] public sealed class Diagnostic
    {
        public DiagnosticSeverity severity;
        public string code, location, message;
        public Diagnostic() { }
        public Diagnostic(DiagnosticSeverity severity, string code, string location, string message)
        { this.severity = severity; this.code = code; this.location = location; this.message = message; }
        public override string ToString() => severity + " " + code + " [" + location + "] " + message;
    }
}
