using SkillConfig;
using SkillConfig.Editor;
using UnityEngine;

int checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); checks++; Console.WriteLine("PASS: " + name); }
SkillConfigDto Valid() => new() {
 schemaVersion = 1, characterId = "miyabi", logicFrameIntervalMs = 33, precision = 10000, completeness = CatalogCompleteness.PartialPilot,
 skills = new() { new SkillDto { skillId = "normal-1", skillType = SkillType.Normal, durationFrames = 20, baseDamage = 20,
 animations = new() { new AnimationDto { id = "anim-1", clipName = "Attack01", clipGuid = "guid", clipLocalId = 1, startFrame = 0, endFrame = 20,
 sourceLengthUs = 657000, trimEndUs = 657000, speedPermille = 1000, statePaths = new() { "Base Layer.Normal1" } } } } }
};
bool HasError(SkillConfigDto d, string code = null) => SkillConfigValidation.Validate(d).Any(x => x.severity == DiagnosticSeverity.Error && (code == null || x.code == code));
Check(HasError(new SkillConfigDto()), "empty document rejected");
Check(!HasError(Valid()), "valid partial read-only config");
foreach (var version in new[] { 0, 2, -1 }) { var d = Valid(); d.schemaVersion = version; Check(HasError(d, "VERSION"), "unsupported version " + version); }
{
 var d = Valid(); d.skills.Add(d.skills[0]); Check(HasError(d, "SKILL_ID"), "duplicate ID rejected");
 d = Valid(); d.skills[0].skillType = SkillType.Unspecified; Check(HasError(d, "SKILL_TYPE"), "no implicit skill type");
 d = Valid(); d.skills[0].animations[0].startFrame = 1; Check(HasError(d, "ANIMATION_CONTINUITY"), "animation gap rejected");
 d = Valid(); d.skills[0].animations[0].speedPermille = 0; Check(HasError(d, "ANIMATION_TIME"), "zero speed rejected");
 d = Valid(); d.skills[0].animations[0].trimEndUs++; Check(HasError(d, "ANIMATION_TIME"), "out-of-source trim rejected");
 d = Valid(); d.skills[0].animations[0].statePaths.Add("Base Layer.Normal1Outro"); Check(!HasError(d), "multi-state binding allowed");
 d.skills[0].animations.Add(new AnimationDto { id = "anim-2", clipName = "Outro", clipGuid = "other", clipLocalId = 2, startFrame = 20, endFrame = 30, sourceLengthUs = 330000, trimEndUs = 330000, speedPermille = 1000, statePaths = new() { "Base Layer.Outro" } });
 d.skills[0].durationFrames = 30; Check(!HasError(d), "multi-clip contiguous skill supported");
 d.skills[0].animations[1].startFrame = 19; Check(HasError(d, "ANIMATION_CONTINUITY"), "animation overlap rejected");
}
{
 var d = Valid(); var a = d.skills[0].animations[0];
 a.rootMotionBound = true;
 a.rootMotion = new MotionBindingDto { resourcesPath = "RootMotion/Attack01", contentHash = "hash", clipName = "Attack01", sampleCount = 10, sourceClipLengthMs = 657, coverageEndMs = 330, distancePermille = 1000, tailPolicy = MotionTailPolicy.HoldAtCoverageEnd };
 Check(!HasError(d), "legitimate static tail accepted");
 Check(SkillConfigValidation.Validate(d).Any(x => x.code == "MOTION_METADATA" && x.severity == DiagnosticSeverity.Warning), "missing provenance Warning");
 a.rootMotion.clipName = "Wrong"; Check(HasError(d, "MOTION_SOURCE"), "wrong source Error"); a.rootMotion.clipName = "Attack01";
 a.speedPermille = 2000; a.endFrame = 10; d.skills[0].durationFrames = 10; a.rootMotion.requiresTimeRemap = true;
 Check(!HasError(d) && SkillConfigValidation.Validate(d).Any(x => x.code == "MOTION_REMAP_REQUIRED"), "2x mapping declared future-only");
 a.rootMotion.requiresTimeRemap = false; Check(HasError(d, "MOTION_REMAP_FLAG"), "cannot hide remapping requirement");
 a.rootMotion.requiresTimeRemap = true; a.rootMotion.provenanceVerified = true; a.rootMotion.sourceClipLengthMs = 1000;
 Check(HasError(d, "MOTION_SOURCE_TIME"), "verified inconsistent source duration Error");
 a.rootMotion.provenanceVerified = false; a.rootMotion.coverageEndMs = 1000;
 Check(HasError(d, "MOTION_COVERAGE"), "coverage beyond source rejected even without provenance");
}
{
 var d = Valid(); var s = d.skills[0]; s.hitGroups.Add(new HitGroup { id = "g", damageSource = DamageSource.ComboBaseDamage });
 Check(s.ResolveDamage(s.hitGroups[0]) == 20, "base damage resolved"); s.baseDamage = 31; Check(s.ResolveDamage(s.hitGroups[0]) == 31, "base damage change needs no manual sync");
 s.hitGroups[0].explicitDamage = 12; Check(HasError(d, "DUPLICATE_DAMAGE"), "duplicate base damage rejected"); s.hitGroups[0].explicitDamage = 0;
 s.hitGroups[0].mode = HitMode.Repeat; s.hitGroups[0].repeatIntervalFrames = 0; Check(HasError(d, "GROUP_RANGE"), "Repeat positive interval"); s.hitGroups[0].repeatIntervalFrames = 2;
 s.tracks.Add(new SkillTrack { id = "boxes", kind = TrackKind.HitBox }); s.hitBoxes.Add(new HitBox { id = "b", trackId = "boxes", hitGroupId = "missing", shape = ShapeKind.Sphere, radius = 100, endFrame = 3 });
 Check(HasError(d, "GROUP_REF"), "dangling group rejected"); s.hitBoxes[0].hitGroupId = "g";
 foreach (var shape in Enum.GetValues<ShapeKind>()) { var b = s.hitBoxes[0]; b.shape = shape; b.radius = 100; b.height = 200; b.size = new Int3 { x = 100, y = 200, z = 300 }; b.sectorAngleMilliDegrees = 90000; Check(!HasError(d), "shape parameters " + shape); }
 s.hitBoxes[0].radius = 0; Check(HasError(d, "SHAPE"), "invalid shape rejected");
}
{
 var d = Valid(); var s = d.skills[0]; s.tracks.Add(new SkillTrack { id = "event-track", kind = TrackKind.Event });
 s.events.Add(new SkillEvent { id = "event-1", trackId = "event-track", frame = 2, eventId = "combo.preinput", execution = EventExecution.Authoritative, parameterKind = EventParameterKind.ComboGate, comboGate = new ComboGateParameters { enabled = true } });
 Check(!HasError(d), "typed registered event valid"); s.events[0].marker = new MarkerParameters { text = "wrong payload" }; Check(HasError(d, "EVENT"), "wrong typed payload rejected");
 s.events[0].marker = null; s.events[0].eventId = "unknown"; Check(HasError(d, "EVENT"), "unknown event rejected");
}
{
 var d = Valid(); var s = d.skills[0]; s.tracks.Add(new SkillTrack { id = "interrupt", kind = TrackKind.Interrupt }); s.tracks.Add(new SkillTrack { id = "vfx", kind = TrackKind.Vfx });
 s.interrupts.Add(new FrameWindow { id = "window", trackId = "interrupt", startFrame = 2, endFrame = 10 }); d.resources.Add(new ResourceDto { id = "fx", kind = ResourceKind.VfxPrefab, path = "Vfx/test" });
 s.vfx.Add(new VfxCue { id = "fx-1", trackId = "vfx", resourceId = "fx", startFrame = 9, endFrame = 15, classification = VfxClass.AttackCritical });
 Check(HasError(d, "CRITICAL_VFX"), "critical before window end rejected"); s.vfx[0].startFrame = 10; Check(!HasError(d), "critical at exclusive end accepted");
 s.vfx[0].resourceId = "missing"; Check(HasError(d, "RESOURCE_REF"), "missing resource rejected");
}
string directory = Path.Combine(Path.GetTempPath(), "skill-config-test-" + Guid.NewGuid().ToString("N"));
try {
 string path = Path.Combine(directory, "Character_miyabi_Skills.json"); var d = Valid(); d.diagnostics = SkillConfigValidation.Validate(d);
 SkillConfigFileStore.Write(path, d); string old = File.ReadAllText(path);
 Check(d.diagnostics.Any(x => x.severity == DiagnosticSeverity.Warning), "Warning export succeeds");
 Check(SkillConfigLoader.TryParse(old, out var snapshot, out _), "export readback succeeds");
 Check(!SkillConfigLoader.TryParse(old, out _, out var refused, true) && refused.Any(x => x.code == "NOT_BATTLE_READY"), "partial cannot become battle config");
 var copy = snapshot.CopyData(); copy.skills[0].baseDamage = 999; Check(snapshot.CopyData().skills[0].baseDamage == 20, "snapshot mutation isolated");
 Check(SkillConfigFileStore.Serialize(d) == old, "deterministic serialization"); Check(snapshot.CopyData().skills[0].skillId == d.skills[0].skillId, "ID survives serialization");
 foreach (string stage in new[] { "write", "readback", "replace" }) {
   bool threw = false; try { SkillConfigFileStore.Write(path, d, s => { if (s == stage) throw new IOException("injected " + stage); }); } catch (IOException) { threw = true; }
   Check(threw && File.ReadAllText(path) == old, "old JSON protected on " + stage); Check(Directory.GetFiles(directory, "*.tmp").Length == 0, "temporary cleanup " + stage);
 }
 var bad = Valid(); bad.skills.Add(bad.skills[0]); bool blocked = false; try { SkillConfigFileStore.Write(path, bad); } catch (InvalidDataException) { blocked = true; }
 Check(blocked && File.ReadAllText(path) == old, "invalid export preserves bytes"); d.skills[0].baseDamage = 22; SkillConfigFileStore.Write(path, d); Check(File.ReadAllText(path) != old, "successful atomic replacement");
} finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
foreach (string bad in new[] { "", "garbage", "[]", "{}", "{broken" }) Check(!SkillConfigLoader.TryParse(bad, out _, out _), "malformed/versionless input: " + bad);
Console.WriteLine($"RESULT: {checks} standalone checks passed. Unity serialization/Inspector/Resources NOT covered by adapters.");
