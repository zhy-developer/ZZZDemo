using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SkillConfig;

namespace SkillRuntime
{
    public sealed class RuntimeGroup
    {
        public string Id { get; }
        public HitMode Mode { get; }
        public int Interval { get; }
        public int Damage { get; }
        public int HitStunFrames { get; }
        public int KnockbackX { get; }
        public int KnockbackZ { get; }
        public int KnockbackFrames { get; }
        public string HitVfxResourceId { get; }
        public string HitSfxResourceId { get; }
        internal RuntimeGroup(SkillDto skill, HitGroup g)
        {
            if (g.knockback.y != 0) throw new ArgumentException(skill.skillId + "/" + g.id + ": KNOCKBACK_Y_UNSUPPORTED (source unchanged).");
            if ((g.knockback.x != 0 || g.knockback.z != 0) && g.knockbackFrames <= 0)
                throw new ArgumentException(skill.skillId + "/" + g.id + ": KNOCKBACK_DURATION_REQUIRED.");
            GeometryLimits.Coordinate(g.knockback.x); GeometryLimits.Coordinate(g.knockback.z);
            Id = g.id; Mode = g.mode; Interval = g.repeatIntervalFrames; Damage = skill.ResolveDamage(g);
            HitStunFrames = g.hitStunFrames; KnockbackX = g.knockback.x; KnockbackZ = g.knockback.z;
            KnockbackFrames = g.knockbackFrames; HitVfxResourceId = g.hitVfxResourceId; HitSfxResourceId = g.hitSfxResourceId;
        }
    }
    public sealed class RuntimeWindow
    {
        public string Id { get; }
        public int Start { get; }
        public int End { get; }
        internal RuntimeWindow(FrameWindow w) { Id = w.id; Start = w.startFrame; End = w.endFrame; }
        public bool Contains(int frame) => Start <= frame && frame < End;
    }
    public sealed class RuntimeSegment
    {
        public string Id { get; }
        public int Start { get; }
        public int End { get; }
        internal RuntimeSegment(AnimationDto a) { Id = a.id; Start = a.startFrame; End = a.endFrame; }
    }
    public sealed class RuntimeEvent
    {
        public string Id { get; }
        public string EventId { get; }
        public int Frame { get; }
        public int Order { get; }
        internal RuntimeEvent(SkillEvent e) { Id = e.id; EventId = e.eventId; Frame = e.frame; Order = e.order; }
    }
    public sealed class RuntimeSkill
    {
        public string CharacterId { get; }
        public string SkillId { get; }
        public int Duration { get; }
        public SkillType Type { get; }
        public ReadOnlyCollection<RuntimeWindow> Invincibility { get; }
        public ReadOnlyCollection<RuntimeWindow> Interrupts { get; }
        public ReadOnlyCollection<AttackShape> Shapes { get; }
        public ReadOnlyCollection<RuntimeGroup> Groups { get; }
        public ReadOnlyCollection<RuntimeSegment> Segments { get; }
        public ReadOnlyCollection<RuntimeEvent> Events { get; }
        readonly Dictionary<string, RuntimeGroup> groupIndex;
        internal RuntimeSkill(string characterId, SkillDto s)
        {
            CharacterId = characterId; SkillId = s.skillId; Duration = s.durationFrames;
            Type = s.skillType;
            if (Type == SkillType.Unspecified) throw new ArgumentException(s.skillId + ": SKILL_TYPE_UNSPECIFIED has no runtime defense policy.");
            Invincibility = Array.AsReadOnly(s.invincibility.Select(w => new RuntimeWindow(w)).OrderBy(w => w.Start).ThenBy(w => w.Id, StringComparer.Ordinal).ToArray());
            Interrupts = Array.AsReadOnly(s.interrupts.Select(w => new RuntimeWindow(w)).OrderBy(w => w.Start).ThenBy(w => w.Id, StringComparer.Ordinal).ToArray());
            Shapes = Array.AsReadOnly(s.hitBoxes.Select(b => CompileShape(s.skillId, b)).OrderBy(b => b.Id, StringComparer.Ordinal).ToArray());
            Groups = Array.AsReadOnly(s.hitGroups.Select(g => new RuntimeGroup(s, g)).OrderBy(g => g.Id, StringComparer.Ordinal).ToArray());
            Segments = Array.AsReadOnly(s.animations.Select(a => new RuntimeSegment(a)).ToArray());
            Events = Array.AsReadOnly(s.events.Select(e => new RuntimeEvent(e)).OrderBy(e => e.Frame).ThenBy(e => e.Order).ThenBy(e => e.Id, StringComparer.Ordinal).ToArray());
            groupIndex = Groups.ToDictionary(g => g.Id, StringComparer.Ordinal);
        }
        public RuntimeGroup Group(string id) => groupIndex[id];
        static AttackShape CompileShape(string skill, HitBox box)
        {
            try { return new AttackShape(box); }
            catch (ArgumentException ex) { throw new ArgumentException(skill + "/" + box.id + ": " + ex.Message, ex); }
        }
        public RuntimeSegment SegmentAt(int frame)
        {
            if (frame < 0 || frame >= Duration) throw new ArgumentOutOfRangeException(nameof(frame));
            return Segments.First(s => s.Start <= frame && frame < s.End);
        }
    }
    public sealed class RuntimeReadinessIssue
    {
        public string Code { get; }
        public string Location { get; }
        public string Message { get; }
        public DiagnosticSeverity Severity { get; }
        internal RuntimeReadinessIssue(string code, string location, string message, DiagnosticSeverity severity = DiagnosticSeverity.Error)
        { Code = code; Location = location; Message = message; Severity = severity; }
    }
    public sealed class RuntimeSkillCatalog
    {
        public string CharacterId { get; }
        public int FrameIntervalMs { get; }
        public int Precision { get; }
        public bool BattleReady => false;
        public ReadOnlyCollection<RuntimeSkill> Skills { get; }
        public ReadOnlyCollection<RuntimeReadinessIssue> ReadinessIssues { get; }
        readonly Dictionary<string, RuntimeSkill> skills;
        RuntimeSkillCatalog(SkillConfigDto data, int frameIntervalMs, int precision)
        {
            CharacterId = data.characterId; FrameIntervalMs = frameIntervalMs; Precision = precision;
            Skills = Array.AsReadOnly(data.skills.Select(s => new RuntimeSkill(data.characterId, s)).OrderBy(s => s.SkillId, StringComparer.Ordinal).ToArray());
            skills = Skills.ToDictionary(s => s.SkillId, StringComparer.Ordinal);
            var issues = new List<RuntimeReadinessIssue> {
                new RuntimeReadinessIssue("ISOLATED_ONLY", CharacterId, "Isolated runtime has no combat integration; BattleReady remains false.") };
            if (data.completeness != CatalogCompleteness.Complete) issues.Add(new RuntimeReadinessIssue("PARTIAL_CATALOG", CharacterId, "Not a complete battle catalog."));
            if (!data.hurtBoxConfigured) issues.Add(new RuntimeReadinessIssue("HURTBOX_REQUIRED", CharacterId, "Formal HurtBox is missing; isolated callers must supply their own explicit test circle."));
            else new HurtCircle(data.hurtBox.radius, data.hurtBox.offset.x, data.hurtBox.offset.y, data.hurtBox.offset.z);
            foreach (var s in data.skills) foreach (var a in s.animations.Where(a => a.rootMotionBound))
            {
                if (a.rootMotion.requiresTimeRemap) issues.Add(new RuntimeReadinessIssue("MOTION_REMAP_BINDING_REQUIRED", s.skillId + "/" + a.id, "Host must validate the actual motion resource with the Phase4B integer sampler before binding.", DiagnosticSeverity.Warning));
                if (!a.rootMotion.provenanceVerified) issues.Add(new RuntimeReadinessIssue("MOTION_PROVENANCE", s.skillId + "/" + a.id, "Source provenance remains unverified.", DiagnosticSeverity.Warning));
            }
            ReadinessIssues = Array.AsReadOnly(issues.OrderBy(i => i.Location, StringComparer.Ordinal).ThenBy(i => i.Code, StringComparer.Ordinal).ToArray());
        }
        public RuntimeSkill Skill(string id) => skills[id];
        // The explicit isolated entry does not load resources, enable combat or waive existing schema validation.
        // Pass NetConfig.frameTime and ToolMethod.Render2LogicScale from the eventual host; tests declare theirs.
        public static bool TryCreateIsolated(SkillConfigDto data, int frameIntervalMs, int precision,
            out RuntimeSkillCatalog catalog, out string error)
        {
            catalog = null; error = null;
            if (data == null) { error = "Configuration is required."; return false; }
            try
            {
                var diagnostics = SkillConfigValidation.Validate(data);
                if (diagnostics.Any(d => d.severity == DiagnosticSeverity.Error)) throw new ArgumentException(string.Join("\n", diagnostics));
                if (frameIntervalMs <= 0 || data.logicFrameIntervalMs != frameIntervalMs || data.precision != precision)
                    throw new ArgumentException("Runtime units differ from the loaded schema profile.");
                catalog = new RuntimeSkillCatalog(data, frameIntervalMs, precision); return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }
    }
}
