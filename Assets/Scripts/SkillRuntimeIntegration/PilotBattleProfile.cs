using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using FrameSync.RootMotion;
using SkillConfig;
using UnityEngine;

namespace SkillRuntime.Integration
{
    public enum BattleAuthorityMode { Legacy, DeterministicPilot }

    // Match setup/allowlist only. Skills remain in the original ComboData.skill and exported JSON.
    public sealed class PilotBattleProfile : ScriptableObject
    {
        public const string Rules = "phase3c-v1/pilot4a-v1";
        public CharacterSkillCatalog catalog;
        public ComboContainerData comboContainer;
        public ComboData[] allowedCombos;
        public TextAsset exportedJson;
        public string sealedJsonHash, sealedSourceHash;
        public int testHP = 100;
        public int testHurtRadius = 5000;
        public Int3 testHurtOffset;
        public string idleState, hitState, deathState;
        public string testParameterNotice = "DEVELOPMENT TEST ONLY: HP and HurtBox are not approved gameplay values.";

        public string SourceHash()
        {
            var b = new StringBuilder();
            b.Append(Rules).Append('|').Append(testHP).Append('|').Append(testHurtRadius).Append('|').Append(JsonUtility.ToJson(testHurtOffset));
            b.Append('|').Append(idleState).Append('|').Append(hitState).Append('|').Append(deathState);
            b.Append('|').Append(catalog.characterId).Append('|').Append((int)catalog.completeness).Append('|').Append(catalog.hurtBoxConfigured).Append('|').Append(JsonUtility.ToJson(catalog.hurtBox));
            foreach (var combo in catalog.skills)
            {
                if (combo == null || combo.skill == null) throw new InvalidOperationException("Missing catalog source.");
                var a = combo.skill;
                var s = new SkillDto { skillId = a.skillId, displayName = a.displayName, category = a.category, skillType = a.skillType,
                    legacyEntryState = combo.comboName, tracks = a.tracks, hitGroups = a.hitGroups, hitBoxes = a.hitBoxes,
                    invincibility = a.invincibility, interrupts = a.interrupts, events = a.events, vfx = a.vfx, sfx = a.sfx };
                b.Append('|').Append(a.registered).Append('|').Append(combo.comboDamage.ToString("R", CultureInfo.InvariantCulture)).Append(JsonUtility.ToJson(s));
                foreach (var clip in a.animations)
                {
                    b.Append(JsonUtility.ToJson(new AnimationDto { id = clip.id, clipName = clip.clip ? clip.clip.name : null, startFrame = clip.startFrame, endFrame = clip.endFrame,
                        trimStartUs = clip.trimStartUs, trimEndUs = clip.trimEndUs, speedPermille = clip.speedPermille, statePaths = clip.statePaths }));
                    b.Append('|').Append((int)clip.rootSource).Append('|').Append((int)clip.tailPolicy);
                    if (clip.clip)
                    {
                        b.Append('|').Append(TimeUs(clip.clip.length));
                        foreach (var e in clip.clip.events.OrderBy(e => e.time).ThenBy(e => e.functionName, StringComparer.Ordinal))
                            b.Append('|').Append(e.functionName).Append(':').Append(TimeUs(e.time));
                    }
                    var root = clip.rootSource == RootBindingSource.LegacyCombo ? combo.rootMotion : new RootMotionSettings { json = clip.additionalRootMotion, endFrameExclusive = clip.additionalEndFrameExclusive, distancePermille = clip.additionalDistancePermille };
                    b.Append('|').Append(root.endFrameExclusive).Append('|').Append(root.distancePermille).Append('|').Append(root.json ? SkillConfigHash.Sha256(root.json.text) : "none");
                }
            }
            foreach (var c in comboContainer.comboDatas) b.Append('|').Append(c ? c.skill?.skillId : "missing").Append(':').Append(c ? c.comboName : "missing");
            foreach (var c in allowedCombos) b.Append('|').Append(c ? c.skill?.skillId : "missing");
            return SkillConfigHash.Sha256(b.ToString());
        }
        internal static long TimeUs(float seconds) => (long)Math.Round(seconds * 1000000.0, MidpointRounding.AwayFromZero);

        public PilotConfiguration ValidateAndFreeze()
        {
            if (!Debug.isDebugBuild) throw new InvalidOperationException("DeterministicPilot requires a development build.");
            if (!catalog || !comboContainer || !exportedJson || allowedCombos == null || allowedCombos.Length != 2 || allowedCombos.Any(c => !c)
                || allowedCombos.Distinct().Count() != 2 || comboContainer.comboDatas.Count < 2 || !comboContainer.comboDatas.Take(2).SequenceEqual(allowedCombos))
                throw new InvalidOperationException("Pilot requires the two mapped consecutive Normal entries of the real combo container.");
            if (testHP <= 0 || testHP > 16777216 || testHurtRadius <= 0 || string.IsNullOrEmpty(testParameterNotice)) throw new InvalidOperationException("Explicit bounded test HP/HurtBox required.");
            if (sealedJsonHash != SkillConfigHash.Sha256(exportedJson.text) || sealedSourceHash != SourceHash())
                throw new InvalidOperationException("Pilot source/export seal changed. Rebuild via Tools/Skill Runtime/Prepare Phase 4A Arena.");
            if (!SkillConfigLoader.TryLoad(catalog.characterId, out var snapshot, out var issues)) throw new InvalidOperationException(string.Join("\n", issues));
            var dto = snapshot.CopyData();
            if (JsonUtility.ToJson(dto) != JsonUtility.ToJson(JsonUtility.FromJson<SkillConfigDto>(exportedJson.text))) throw new InvalidOperationException("Loaded JSON differs from sealed export.");
            if (!RuntimeSkillCatalog.TryCreateIsolated(snapshot, NetConfig.frameTime, ToolMethod.Render2LogicScale, out var index, out var error)) throw new InvalidOperationException(error);
            var moves = new List<PilotSkill>();
            foreach (var combo in allowedCombos)
            {
                if (!catalog.skills.Contains(combo) || !combo.skill.registered) throw new InvalidOperationException("Allowlist skill absent from catalog.");
                var s = dto.skills.Single(v => v.skillId == combo.skill.skillId);
                if (s.animations.Count != 1 || combo.skill.animations.Count != 1 || s.skillType != SkillType.Normal
                    || s.events.Any(e => e.execution != EventExecution.Authoritative || e.parameterKind != EventParameterKind.ComboGate) || s.vfx.Count != 0 || s.sfx.Count != 0)
                    throw new InvalidOperationException(s.skillId + ": Phase4A supports one identity-mapped Normal clip; custom events/full presentation are Phase4B.");
                var a = s.animations[0]; var source = combo.skill.animations[0];
                if (!source.clip || a.clipName != source.clip.name || !a.rootMotionBound || a.rootMotion.requiresTimeRemap || a.trimStartUs != 0 || a.speedPermille != 1000 || a.trimEndUs != a.sourceLengthUs)
                    throw new InvalidOperationException(s.skillId + ": unsupported/missing clip or exact motion time mapping.");
                var json = Resources.Load<TextAsset>(a.rootMotion.resourcesPath);
                if (!json || SkillConfigHash.Sha256(json.text) != a.rootMotion.contentHash || !RootMotionClip.TryCreate(JsonUtility.FromJson<RootMotionJsonData>(json.text), NetConfig.frameTime, 10000, out var motion, out error))
                    throw new InvalidOperationException(s.skillId + ": invalid motion JSON/hash.");
                int end = a.rootMotion.endFrameExclusive == 0 ? motion.FrameCount : a.rootMotion.endFrameExclusive;
                if (end <= 0 || end > motion.FrameCount || end > s.durationFrames) throw new InvalidOperationException("Motion coverage exceeds cast duration.");
                moves.Add(new PilotSkill(combo, index.Skill(s.skillId), source.clip, a.statePaths[0], motion, end, a.rootMotion.distancePermille, s.events));
            }
            Debug.Log("[PilotReady] restricted development allowlist ONLY; formal BattleReady=" + index.BattleReady + "; missing formal HurtBox remains unresolved. " + testParameterNotice);
            foreach (var issue in index.ReadinessIssues) Debug.Log("[PilotReadiness] " + issue.Code + " " + issue.Location + ": " + issue.Message);
            return new PilotConfiguration(moves.ToArray(), new HurtCircle(testHurtRadius, testHurtOffset.x, testHurtOffset.y, testHurtOffset.z), testHP,
                SkillConfigHash.Sha256(Rules + "|" + sealedSourceHash + "|" + sealedJsonHash), idleState, hitState, deathState, comboContainer);
        }
    }
    public sealed class PilotSkill
    {
        public readonly ComboData Source;
        public readonly RuntimeSkill Skill;
        public readonly AnimationClip Clip;
        public readonly string State;
        public readonly RootMotionClip Motion;
        public readonly int MotionEnd, DistancePermille, PreInput, Ready, LinkEnd, MoveCancel;
        internal PilotSkill(ComboData source, RuntimeSkill skill, AnimationClip clip, string state, RootMotionClip motion, int end, int scale, List<SkillEvent> events)
        {
            Source = source; Skill = skill; Clip = clip; State = state; Motion = motion; MotionEnd = end; DistancePermille = scale;
            PreInput = Gate(events, "combo.preinput", true); Ready = Gate(events, "combo.attack-ready", true);
            LinkEnd = events.Any(e => e.eventId == "combo.link") ? Gate(events, "combo.link", false) : skill.Duration;
            MoveCancel = Gate(events, "combo.move-interrupt", true);
            if (PreInput > Ready || Ready >= LinkEnd || LinkEnd > skill.Duration || MoveCancel > skill.Duration) throw new InvalidOperationException("Invalid logical gate order: " + skill.SkillId);
            Debug.Log("[PilotGates] " + skill.SkillId + " pre=" + PreInput + " ready=" + Ready + " linkEnd=" + LinkEnd + " move=" + MoveCancel + " D=" + skill.Duration);
        }
        static int Gate(List<SkillEvent> events, string name, bool enabled)
        {
            var found = events.Where(e => e.eventId == name).ToArray();
            if (found.Length != 1 || found[0].comboGate == null || found[0].comboGate.enabled != enabled) throw new InvalidOperationException("Unsupported gate sequence " + name);
            return found[0].frame;
        }
    }
    public sealed class PilotConfiguration
    {
        public readonly System.Collections.ObjectModel.ReadOnlyCollection<PilotSkill> Skills;
        public readonly HurtCircle Hurt;
        public readonly int HP;
        public readonly string Digest, Idle, Hit, Death;
        public readonly ComboContainerData Container;
        internal PilotConfiguration(PilotSkill[] skills, HurtCircle hurt, int hp, string digest, string idle, string hit, string death, ComboContainerData container)
        { Skills = Array.AsReadOnly(skills); Hurt = hurt; HP = hp; Digest = digest; Idle = idle; Hit = hit; Death = death; Container = container; }
    }
}
