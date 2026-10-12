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
    public enum BattleAuthorityMode { Legacy = 0, Deterministic = 1 }

    // Match setup/allowlist only. Skills remain in the original ComboData.skill and exported JSON.
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "SkillRuntime.Integration", "Assembly-CSharp", "PilotBattleProfile")]
    public sealed class BattleProfile : ScriptableObject
    {
        public const string Rules = "battle-world-v3/expanded-actions-v1";
        [Tooltip("Normal entry for the explicitly listed skill scope. Uses the catalog HurtBox; does not promote the entire partial catalog to BattleReady.")]
        public bool normalGameplay;
        public CharacterSkillCatalog catalog;
        public ComboContainerData comboContainer;
        public ComboData[] allowedCombos;
        public ComboData skillCombo, ultimateCombo, dodgeForward, dodgeBack;
        public int dodgeCooldownFrames = 16;
        public bool HasExpandedActions => skillCombo || ultimateCombo || dodgeForward || dodgeBack;
        public ComboData[] ReachableCombos() => allowedCombos.Concat(HasExpandedActions
            ? new[] { skillCombo, ultimateCombo, dodgeForward, dodgeBack, comboContainer.DodgeATKData, comboContainer.BackDodgeATKData ?? comboContainer.DodgeATKData }
            : new ComboData[0]).Where(c => c).Distinct().ToArray();
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
            if (normalGameplay) b.Append("normal-gameplay-scope-v1|");
            // Historical hash schema token is persisted compatibility data, not a runtime type name.
            // Keep the authoring seal stable; Rules separately versions the simulation protocol.
            b.Append("phase3c-v1/pilot4b-v1").Append('|').Append(testHP).Append('|').Append(testHurtRadius).Append('|').Append(JsonUtility.ToJson(testHurtOffset));
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
                            b.Append('|').Append(e.functionName).Append(':').Append(TimeUs(e.time))
                                .Append(':').Append(e.stringParameter).Append(':').Append(e.intParameter)
                                .Append(':').Append(e.floatParameter.ToString("R", CultureInfo.InvariantCulture));
                    }
                    var root = clip.rootSource == RootBindingSource.LegacyCombo ? combo.rootMotion : new RootMotionSettings { json = clip.additionalRootMotion, endFrameExclusive = clip.additionalEndFrameExclusive, distancePermille = clip.additionalDistancePermille };
                    b.Append('|').Append(root.endFrameExclusive).Append('|').Append(root.distancePermille).Append('|').Append(root.json ? SkillConfigHash.Sha256(root.json.text) : "none");
                }
            }
            foreach (var c in comboContainer.comboDatas) b.Append('|').Append(c ? c.skill?.skillId : "missing").Append(':').Append(c ? c.comboName : "missing");
            foreach (var c in new[] { comboContainer.DodgeATKData, comboContainer.BackDodgeATKData })
                b.Append("|entry:").Append(c ? c.skill?.skillId : "missing").Append(':').Append(c ? c.comboName : "missing");
            foreach (var resource in catalog.resources.OrderBy(r => r.id, StringComparer.Ordinal))
                b.Append("|resource:").Append(resource.id).Append(':').Append((int)resource.kind).Append(':').Append(resource.asset ? resource.asset.name : "missing");
            foreach (var c in allowedCombos) b.Append('|').Append(c ? c.skill?.skillId : "missing");
            if (HasExpandedActions)
            {
                b.Append("|expanded-v1:").Append(dodgeCooldownFrames);
                foreach (var c in new[] { skillCombo, ultimateCombo, dodgeForward, dodgeBack }) b.Append('|').Append(c ? c.skill?.skillId : "missing");
            }
            return SkillConfigHash.Sha256(b.ToString());
        }
        internal static long TimeUs(float seconds) => (long)Math.Round(seconds * 1000000.0, MidpointRounding.AwayFromZero);

        public BattleProfileSnapshot ValidateAndFreeze()
        {
            if (!normalGameplay && !Debug.isDebugBuild) throw new InvalidOperationException("Development profiles require a development build.");
            if (!catalog) throw new InvalidOperationException(name + ": Catalog is missing. Restore the referenced CharacterSkillCatalog asset and its original .meta GUID, or explicitly assign a replacement catalog.");
            if (!comboContainer) throw new InvalidOperationException(name + ": Combo Container is missing. Assign the real character's ComboContainerData.");
            if (!exportedJson) throw new InvalidOperationException(name + ": Exported Json is missing. Assign this catalog's exported skill JSON.");
            if (allowedCombos == null || allowedCombos.Length == 0 || allowedCombos.Any(c => !c)
                || allowedCombos.Distinct().Count() != allowedCombos.Length || !comboContainer.comboDatas.Take(allowedCombos.Length).SequenceEqual(allowedCombos))
                throw new InvalidOperationException("Battle profile requires a mapped consecutive prefix of the real combo container; missing successors remain rejected.");
            if (testHP <= 0 || testHP > 16777216 || testHurtRadius <= 0 || string.IsNullOrEmpty(testParameterNotice)) throw new InvalidOperationException("Explicit bounded test HP/HurtBox required.");
            if (HasExpandedActions && (!skillCombo || !ultimateCombo || !dodgeForward || !dodgeBack || !comboContainer.DodgeATKData || dodgeCooldownFrames <= 0))
                throw new InvalidOperationException("Expanded actions require skill, ultimate, front/back dodge, dodge attack and cooldown bindings.");
            if (sealedJsonHash != SkillConfigHash.Sha256(exportedJson.text) || sealedSourceHash != SourceHash())
                throw new InvalidOperationException("Battle profile source/export seal changed. Use Save and Export in the Skill Editor to update the profile.");
            if (!SkillConfigLoader.TryLoad(catalog.characterId, out var snapshot, out var issues)) throw new InvalidOperationException(string.Join("\n", issues));
            var dto = snapshot.CopyData();
            if (normalGameplay && (!dto.hurtBoxConfigured || dto.hurtBox == null || dto.hurtBox.radius <= 0))
                throw new InvalidOperationException("Normal gameplay requires an exported catalog HurtBox; test radius is not a fallback.");
            if (catalog.resources.Count != dto.resources.Count) throw new InvalidOperationException("Resource catalog/export count differs.");
            foreach (var resource in catalog.resources)
            {
                var exported = dto.resources.SingleOrDefault(r => r.id == resource.id);
                if (exported == null || exported.kind != resource.kind || !resource.asset || Resources.Load(exported.path) != resource.asset)
                    throw new InvalidOperationException("Resource catalog/export asset mismatch: " + resource.id);
            }
            if (JsonUtility.ToJson(dto) != JsonUtility.ToJson(JsonUtility.FromJson<SkillConfigDto>(exportedJson.text))) throw new InvalidOperationException("Loaded JSON differs from sealed export.");
            if (!RuntimeSkillCatalog.TryCreateIsolated(dto, NetConfig.frameTime, ToolMethod.Render2LogicScale, out var index, out var error)) throw new InvalidOperationException(error);
            var moves = new List<BattleSkillBinding>();
            foreach (var combo in ReachableCombos())
            {
                if (!catalog.skills.Contains(combo) || !combo.skill.registered) throw new InvalidOperationException("Allowlist skill absent from catalog.");
                var s = dto.skills.Single(v => v.skillId == combo.skill.skillId);
                var expectedType = combo == skillCombo ? SkillType.Skill : combo == ultimateCombo ? SkillType.Ultimate
                    : combo == dodgeForward || combo == dodgeBack ? SkillType.Dodge : SkillType.Normal;
                if (s.animations.Count != combo.skill.animations.Count || s.skillType != expectedType
                    || s.events.Any(e => !SkillEventRegistry.IsValid(e)))
                    throw new InvalidOperationException(s.skillId + ": unsupported event/presentation capability; never fall back to legacy combat.");
                if (s.hitGroups.Any(g => !string.IsNullOrEmpty(g.hitVfxResourceId) || !string.IsNullOrEmpty(g.hitSfxResourceId)))
                    throw new InvalidOperationException(s.skillId + ": configured hit-feedback resources need a binding/lifetime adapter; timeline presentation alone does not satisfy this capability.");
                if (normalGameplay) ValidatePresentation(s, dto);
                var animations = new List<BattleAnimationBinding>(); var mapped = new List<SkillMotionSegment>();
                RootMotionClip firstMotion = null; int firstEnd = 0, firstScale = 1000;
                for (int i = 0; i < s.animations.Count; i++)
                {
                    var a = s.animations[i]; var source = combo.skill.animations[i];
                    if (!source.clip || a.id != source.id || a.clipName != source.clip.name || Math.Abs(a.sourceLengthUs - TimeUs(source.clip.length)) > 1)
                        throw new InvalidOperationException(s.skillId + ": source/export Clip mismatch.");
                    RootMotionJsonData data = null;
                    if (a.rootMotionBound)
                    {
                        var json = Resources.Load<TextAsset>(a.rootMotion.resourcesPath);
                        if (!json || SkillConfigHash.Sha256(json.text) != a.rootMotion.contentHash) throw new InvalidOperationException(s.skillId + ": invalid motion JSON/hash.");
                        data = JsonUtility.FromJson<RootMotionJsonData>(json.text);
                        if (i == 0)
                        {
                            if (!RootMotionClip.TryCreate(data, NetConfig.frameTime, 10000, out firstMotion, out error)) throw new InvalidOperationException(error);
                            firstEnd = a.rootMotion.endFrameExclusive == 0 ? firstMotion.FrameCount : a.rootMotion.endFrameExclusive;
                            firstScale = a.rootMotion.distancePermille;
                        }
                    }
                    mapped.Add(new SkillMotionSegment(a, data)); animations.Add(new BattleAnimationBinding(source.clip, a));
                }
                moves.Add(new BattleSkillBinding(combo, index.Skill(s.skillId), animations[0].Clip, animations[0].State, firstMotion, firstEnd, firstScale, s.events,
                    new SkillMotionTimeline(mapped), animations.ToArray(), s));
            }
            Debug.Log(normalGameplay
                ? "[GameplayReady] scopeReady=true wholeCatalogReady=false skills=" + string.Join(",", moves.Select(m => m.Skill.SkillId)) + " HP=" + testHP + " HurtRadius=" + dto.hurtBox.radius + " rules=" + Rules
                : "[BattleReady] restricted development allowlist ONLY; whole catalog BattleReady=" + index.BattleReady + "; " + testParameterNotice);
            foreach (var issue in index.ReadinessIssues) Debug.Log("[BattleReadiness] " + issue.Code + " " + issue.Location + ": " + issue.Message);
            var hurt = normalGameplay ? new HurtCircle(dto.hurtBox.radius, dto.hurtBox.offset.x, dto.hurtBox.offset.y, dto.hurtBox.offset.z)
                : new HurtCircle(testHurtRadius, testHurtOffset.x, testHurtOffset.y, testHurtOffset.z);
            int At(ComboData source) => moves.FindIndex(m => m.Source == source);
            var actions = HasExpandedActions ? new BattleActionBindings(At(skillCombo), At(ultimateCombo), At(dodgeForward), At(dodgeBack),
                At(comboContainer.DodgeATKData), At(comboContainer.BackDodgeATKData ?? comboContainer.DodgeATKData), dodgeCooldownFrames) : null;
            return new BattleProfileSnapshot(moves.ToArray(), hurt, testHP,
                SkillConfigHash.Sha256(Rules + "|" + sealedSourceHash + "|" + sealedJsonHash), idleState, hitState, deathState, comboContainer, dto.resources, allowedCombos.Length, actions);
        }
        // Capability validation is local to this binding, not a relaxation of the general loader's
        // incomplete-catalog guard. Unsupported feedback/resources fail before the Ready handshake.
        void ValidatePresentation(SkillDto skill, SkillConfigDto dto)
        {
            foreach (var cue in skill.vfx)
            {
                var r = dto.resources.Single(x => x.id == cue.resourceId);
                var prefab = Resources.Load<GameObject>(r.path);
                SkillPresentationSlots.ValidateVfx(prefab);
                BattlePresentation.ParticleTailFrames(prefab);
                if (cue.lifetime == VfxLifetime.AutoComplete && cue.maxLifetimeFrames <= 0)
                    throw new InvalidOperationException(skill.skillId + "/" + cue.id + ": finite VFX lifetime required.");
                // Character-dependent bone existence is checked against each actual Player at bind time.
            }
        }
    }
    public sealed class BattleAnimationBinding
    {
        public readonly AnimationClip Clip;
        public readonly string State;
        public readonly SkillClipTime Time;
        internal BattleAnimationBinding(AnimationClip clip, AnimationDto dto) { Clip = clip; State = dto.statePaths[0]; Time = new SkillClipTime(dto); }
    }
    public sealed class BattleSkillBinding
    {
        public readonly ComboData Source;
        public readonly RuntimeSkill Skill;
        public readonly AnimationClip Clip;
        public readonly string State;
        public readonly RootMotionClip Motion;
        public readonly int MotionEnd, DistancePermille, PreInput, Ready, LinkEnd, MoveCancel;
        public readonly SkillMotionTimeline TimelineMotion;
        public readonly System.Collections.ObjectModel.ReadOnlyCollection<BattleAnimationBinding> Animations;
        readonly string presentationJson;
        internal SkillDto CopyPresentation() => presentationJson == null ? new SkillDto() : JsonUtility.FromJson<SkillDto>(presentationJson);
        internal BattleSkillBinding(ComboData source, RuntimeSkill skill, AnimationClip clip, string state, RootMotionClip motion, int end, int scale, List<SkillEvent> events,
            SkillMotionTimeline timeline = null, BattleAnimationBinding[] animations = null, SkillDto presentation = null)
        {
            Source = source; Skill = skill; Clip = clip; State = state; Motion = motion; MotionEnd = end; DistancePermille = scale;
            TimelineMotion = timeline; Animations = Array.AsReadOnly(animations ?? new BattleAnimationBinding[0]);
            presentationJson = presentation == null ? null : JsonUtility.ToJson(presentation);
            bool comboGates = presentation == null || presentation.skillType == SkillType.Normal;
            PreInput = comboGates ? Gate(events, "combo.preinput", true) : skill.Duration - 1;
            Ready = comboGates ? Gate(events, "combo.attack-ready", true) : skill.Duration - 1;
            LinkEnd = events.Any(e => e.eventId == "combo.link") ? Gate(events, "combo.link", false) : skill.Duration;
            MoveCancel = comboGates || events.Any(e => e.eventId == "combo.move-interrupt") ? Gate(events, "combo.move-interrupt", true) : skill.Duration;
            if (PreInput > Ready || Ready >= LinkEnd || LinkEnd > skill.Duration || MoveCancel > skill.Duration) throw new InvalidOperationException("Invalid logical gate order: " + skill.SkillId);
            Debug.Log("[BattleGates] " + skill.SkillId + " pre=" + PreInput + " ready=" + Ready + " linkEnd=" + LinkEnd + " move=" + MoveCancel + " D=" + skill.Duration);
        }
        static int Gate(List<SkillEvent> events, string name, bool enabled)
        {
            var found = events.Where(e => e.eventId == name).ToArray();
            if (found.Length != 1 || found[0].comboGate == null || found[0].comboGate.enabled != enabled) throw new InvalidOperationException("Unsupported gate sequence " + name);
            return found[0].frame;
        }
    }
    public sealed class BattleProfileSnapshot
    {
        public BattleConfiguration CreateLogicConfiguration() => new BattleConfiguration(Skills.Select(s =>
            new BattleSkill(s.Skill, s.PreInput, s.Ready, s.LinkEnd, s.MoveCancel, s.TimelineMotion, s.Motion, s.MotionEnd, s.DistancePermille)),
            Hurt, HP, ContainerCount, Digest, NormalCount, Actions);
        public readonly System.Collections.ObjectModel.ReadOnlyCollection<BattleSkillBinding> Skills;
        public readonly HurtCircle Hurt;
        public readonly int HP;
        public readonly string Digest, Idle, Hit, Death;
        public readonly ComboContainerData Container;
        public readonly int ContainerCount;
        public readonly int NormalCount;
        public readonly BattleActionBindings Actions;
        readonly Dictionary<string, ResourceDto> resources;
        internal ResourceDto Resource(string id) => id != null && resources.TryGetValue(id, out var value) ? new ResourceDto { id = value.id, kind = value.kind, path = value.path } : null;
        internal BattleProfileSnapshot(BattleSkillBinding[] skills, HurtCircle hurt, int hp, string digest, string idle, string hit, string death, ComboContainerData container,
            IEnumerable<ResourceDto> resourceRefs = null, int normalCount = -1, BattleActionBindings actions = null)
        {
            Skills = Array.AsReadOnly(skills); Hurt = hurt; HP = hp; Digest = digest; Idle = idle; Hit = hit; Death = death; Container = container;
            ContainerCount = container.comboDatas.Count;
            NormalCount = normalCount < 0 ? skills.Length : normalCount; Actions = actions;
            resources = (resourceRefs ?? new ResourceDto[0]).ToDictionary(r => r.id, r => new ResourceDto { id = r.id, kind = r.kind, path = r.path }, StringComparer.Ordinal);
        }
    }
}
