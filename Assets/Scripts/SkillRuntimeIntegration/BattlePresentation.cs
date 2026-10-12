using System;
using System.Collections.Generic;
using System.Linq;
using SkillConfig;
using UnityEngine;

namespace SkillRuntime.Integration
{
    // Non-authoritative consumer, invoked once for every committed logical frame, including
    // catch-up frames. Synchronous Resources loading means there is no outstanding async
    // request that can resurrect a cancelled cast. Nothing here feeds the combat hash.
    public sealed class BattlePresentation : IDisposable
    {
        sealed class Effect
        {
            public CastKey Cast; public string Id; public VfxCue Cue; public SkillPresentationLease Lease;
            public long Start, Stop, Bound; public bool EmissionStopped; public Matrix4x4 World;
            public ParticleSystem[] Systems, Roots;
        }
        sealed class Sound
        {
            public CastKey Cast; public string Id; public SfxCue Cue; public SkillPresentationLease Lease;
            public long Start, End, FadeStart = -1, FadeEnd; public float FadeVolume;
        }
        readonly BattleProfileSnapshot config;
        readonly Dictionary<int, RoleBase> roles;
        readonly Dictionary<string, SkillDto> skills;
        readonly List<Effect> effects = new List<Effect>();
        readonly List<Sound> sounds = new List<Sound>();
        readonly HashSet<string> diagnosed = new HashSet<string>(StringComparer.Ordinal);
        long lastFrame = -1;
        bool disposed;
        readonly List<string> requests = new List<string>();
        public string LastRequests => string.Join(";", requests);
        public int ActiveVfx => effects.Count;
        public int ActiveSfx => sounds.Count;
        public BattlePresentation(BattleProfileSnapshot config, IEnumerable<RoleBase> roles)
        { this.config = config; this.roles = roles.ToDictionary(r => r.Player.BattleID); skills = config.Skills.ToDictionary(s => s.Skill.SkillId, s => s.CopyPresentation(), StringComparer.Ordinal); }
        void Diagnose(string key, Exception ex)
        { if (diagnosed.Add(key)) Debug.LogWarning("[BattlePresentation] " + key + ": " + ex.Message + " (authority unaffected)"); }
        T Resource<T>(string id, ResourceKind kind) where T : UnityEngine.Object
        {
            var r = config.Resource(id);
            if (r == null || r.kind != kind) throw new ArgumentException("Missing/wrong-kind resource " + id);
            var asset = Resources.Load<T>(r.path);
            if (!asset) throw new ArgumentException("Unresolvable Resources path " + r.path);
            return asset;
        }
        Matrix4x4 Anchor(CastKey cast, VfxCue cue)
        {
            var role = roles[cast.Entity.BattleId];
            if (!role || !role.Player) throw new InvalidOperationException("Presentation owner destroyed.");
            if (cue.attachment == AttachmentMode.Bone)
            {
                var root = role.Player.characterAnimator.transform;
                var bone = root.Find(cue.bone);
                if (!bone && !string.IsNullOrEmpty(cue.bone) && !cue.bone.Contains("/"))
                { var found = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == cue.bone).ToArray(); if (found.Length == 1) bone = found[0]; }
                if (!bone) throw new ArgumentException("Missing/ambiguous bone " + cue.bone);
                return Matrix4x4.TRS(bone.position, bone.rotation, Vector3.one);
            }
            var p = role.LogicalPosition;
            // +Z in the asset is forward; logical facing is measured from world +X.
            return Matrix4x4.TRS(new Vector3(p.x / 10000f, role.transform.position.y, p.y / 10000f), Quaternion.Euler(0, 90 - role.LogicalFacing, 0), Vector3.one);
        }
        static Vector3 Vector(Int3 v, float scale) => new Vector3(v.x / scale, v.y / scale, v.z / scale);
        void Pose(Effect e)
        {
            if (!e.Lease.Object) throw new InvalidOperationException("VFX instance destroyed externally.");
            var anchor = e.Cue.attachment == AttachmentMode.World ? e.World : Anchor(e.Cast, e.Cue);
            var t = e.Lease.Object.transform;
            t.position = anchor.MultiplyPoint3x4(Vector(e.Cue.offset, 10000));
            t.rotation = anchor.rotation * Quaternion.Euler(Vector(e.Cue.rotationMilliDegrees, 1000));
            t.localScale = Vector3.one * (e.Cue.scalePermille / 1000f);
        }
        void Spawn(CastKey cast, VfxCue cue, long frame)
        {
            SkillPresentationLease lease = null;
            try
            {
                var anchor = Anchor(cast, cue); var prefab = Resource<GameObject>(cue.resourceId, ResourceKind.VfxPrefab);
                SkillPresentationSlots.ValidateVfx(prefab);
                lease = VFX_PoolManager.Instance.RentSkillEffect(prefab);
                var effect = new Effect { Cast = cast, Id = cue.id, Cue = cue, Lease = lease, Start = frame,
                    Stop = checked(frame + cue.endFrame - cue.startFrame), World = anchor };
                // TimelineControlled stops emission at end, then a bounded particle tail; no cast extension.
                int tail = ParticleTailFrames(prefab);
                effect.Bound = cue.lifetime == VfxLifetime.AutoComplete ? checked(frame + cue.maxLifetimeFrames) : checked(effect.Stop + tail);
                effect.Systems = lease.Object.GetComponentsInChildren<ParticleSystem>(true);
                effect.Roots = effect.Systems.Where(p => !effect.Systems.Any(other => other != p && p.transform.IsChildOf(other.transform))).ToArray();
                var sourceSystems = prefab.GetComponentsInChildren<ParticleSystem>(true);
                Pose(effect); lease.Object.SetActive(true);
                for (int i = 0; i < effect.Systems.Length; i++)
                {
                    var ps = effect.Systems[i]; var emission = ps.emission; emission.enabled = sourceSystems[i].emission.enabled;
                    var main = ps.main; main.stopAction = ParticleSystemStopAction.None; main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.useAutoRandomSeed = false; ps.randomSeed = Seed(cast + "/" + cue.id + "/" + i);
                }
                foreach (var root in effect.Roots) { root.Simulate(0, true, true, false); root.Pause(true); }
                effects.Add(effect);
            }
            catch (Exception ex) { lease?.Dispose(); Diagnose(cast + "/" + cue.id, ex); }
        }
        static uint Seed(string text) { unchecked { uint h = 2166136261; foreach (char c in text) h = (h ^ c) * 16777619; return h == 0 ? 1u : h; } }
        internal static int ParticleTailFrames(GameObject prefab)
        {
            double seconds = 0;
            foreach (var ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                if (main.simulationSpeed <= 0) throw new ArgumentException("Unbounded particle simulation speed.");
                // Unity curves may overshoot keys; a resource requiring curves needs an explicit
                // AutoComplete bound rather than an invented duration.
                if (main.startLifetime.mode != ParticleSystemCurveMode.Constant && main.startLifetime.mode != ParticleSystemCurveMode.TwoConstants)
                    throw new ArgumentException("Curve lifetime requires an explicit supported lifetime adapter.");
                // Sum every system's finite emission/lifetime bound: conservative even when
                // descendant sub-emitters trigger on their parent's last particle death.
                if (main.startDelay.mode != ParticleSystemCurveMode.Constant && main.startDelay.mode != ParticleSystemCurveMode.TwoConstants)
                    throw new ArgumentException("Unbounded particle start delay.");
                double systemTail = (main.duration + main.startDelay.constantMax + main.startLifetime.constantMax) / main.simulationSpeed;
                for (int i = 0; ps.subEmitters.enabled && i < ps.subEmitters.subEmittersCount; i++)
                {
                    var sub = ps.subEmitters.GetSubEmitterSystem(i);
                    if (!sub || sub == ps || !sub.transform.IsChildOf(ps.transform) || sub.main.loop)
                        throw new ArgumentException("Sub-emitter must be a finite strict descendant; cycles/external references are unsupported.");
                    var kind = ps.subEmitters.GetSubEmitterType(i);
                    if (kind != ParticleSystemSubEmitterType.Birth && kind != ParticleSystemSubEmitterType.Death)
                        throw new ArgumentException("Only automatic Birth/Death sub-emitters have supported history.");
                }
                if (ps.collision.enabled || ps.trigger.enabled) throw new ArgumentException("Particle collision/trigger side effects are unsupported.");
                if (ps.trails.enabled)
                {
                    if (ps.trails.lifetime.mode != ParticleSystemCurveMode.Constant && ps.trails.lifetime.mode != ParticleSystemCurveMode.TwoConstants)
                        throw new ArgumentException("Curve trail lifetime has no validated upper bound.");
                    systemTail += main.startLifetime.constantMax * ps.trails.lifetime.constantMax / main.simulationSpeed;
                }
                seconds += systemTail;
            }
            if (double.IsNaN(seconds) || seconds > 3600) throw new ArgumentException("Particle tail exceeds bounded presentation capability.");
            return checked((int)Math.Ceiling(seconds / .033) + 1);
        }
        public static int NaturalLifetimeBound(GameObject prefab)
        {
            SkillPresentationSlots.ValidateVfx(prefab);
            int tail = ParticleTailFrames(prefab); double emission = 0;
            foreach (var ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                var m = ps.main;
                if (m.loop || (m.startDelay.mode != ParticleSystemCurveMode.Constant && m.startDelay.mode != ParticleSystemCurveMode.TwoConstants))
                    throw new ArgumentException("Natural AutoComplete requires non-looping particles and a bounded start delay.");
                emission = Math.Max(emission, (m.duration + m.startDelay.constantMax) / m.simulationSpeed);
            }
            if (double.IsNaN(emission) || emission > 3600) throw new ArgumentException("Unbounded emission duration.");
            return checked((int)Math.Ceiling(emission / .033) + tail);
        }
        void Spawn(CastKey cast, SfxCue cue, long frame)
        {
            SkillPresentationLease lease = null;
            try
            {
                var clip = Resource<AudioClip>(cue.resourceId, ResourceKind.AudioClip);
                lease = SFX_PoolManager.Instance.RentSkillSound(clip);
                var source = lease.Object.GetComponent<AudioSource>(); source.clip = clip; source.loop = cue.mode == SoundMode.Loop;
                source.pitch = 1; source.spatialBlend = 0; source.volume = cue.fadeInFrames > 0 ? 0 : cue.volumePermille / 1000f;
                lease.Object.SetActive(true); source.Play();
                sounds.Add(new Sound { Cast = cast, Id = cue.id, Cue = cue, Lease = lease, Start = frame, End = checked(frame + cue.endFrame - cue.startFrame) });
            }
            catch (Exception ex) { lease?.Dispose(); Diagnose(cast + "/" + cue.id, ex); }
        }
        public void Tick(long frame, IEnumerable<CastFrame> capturedCasts, IEnumerable<CastExit> exits, IEnumerable<HitFeedback> feedback)
        {
            if (disposed || frame <= lastFrame) return;
            lastFrame = frame; requests.Clear();
            foreach (var e in effects.ToArray())
            {
                try
                {
                    if (frame >= e.Bound) { Release(e); continue; }
                    Pose(e);
                    foreach (var ps in e.Systems)
                    {
                        if (!e.EmissionStopped && e.Cue.lifetime == VfxLifetime.TimelineControlled && frame >= e.Stop)
                        { var emission = ps.emission; emission.enabled = false; ps.Stop(false, ParticleSystemStopBehavior.StopEmitting); }
                    }
                    foreach (var root in e.Roots) { root.Simulate(.033f, true, false, false); root.Pause(true); }
                    if (frame >= e.Stop && e.Cue.lifetime == VfxLifetime.TimelineControlled) e.EmissionStopped = true;
                    if (!e.Roots.Any(p => p.IsAlive(true))) Release(e);
                }
                catch (Exception ex) { Diagnose(e.Cast + "/" + e.Id, ex); Release(e); }
            }
            foreach (var sound in sounds.ToArray())
            {
                try
                {
                if (!sound.Lease.Object || frame >= sound.End || (sound.FadeStart >= 0 && frame >= sound.FadeEnd)) { Release(sound); continue; }
                var source = sound.Lease.Object.GetComponent<AudioSource>();
                float gain = sound.Cue.volumePermille / 1000f;
                if (sound.Cue.fadeInFrames > 0) gain *= Mathf.Clamp01((float)(frame - sound.Start) / sound.Cue.fadeInFrames);
                if (sound.FadeStart >= 0) gain = sound.FadeVolume * (float)(sound.FadeEnd - frame) / (sound.FadeEnd - sound.FadeStart);
                else if (sound.Cue.fadeOutFrames > 0) gain *= Mathf.Clamp01((float)(sound.End - frame) / sound.Cue.fadeOutFrames);
                source.volume = gain;
                if (!source.loop && !source.isPlaying) Release(sound);
                }
                catch (Exception ex) { Diagnose(sound.Cast + "/" + sound.Id, ex); Release(sound); }
            }
            foreach (var cast in capturedCasts.OrderBy(c => c.Key))
            {
                var skill = skills[cast.Skill.SkillId];
                foreach (var cue in skill.vfx.Where(c => c.startFrame == cast.SkillFrame).OrderBy(c => c.id, StringComparer.Ordinal))
                { requests.Add(frame + "|" + cast.Key + "|vfx|" + cue.id + "|" + cue.resourceId); Spawn(cast.Key, cue, frame); }
                foreach (var cue in skill.sfx.Where(c => c.startFrame == cast.SkillFrame).OrderBy(c => c.id, StringComparer.Ordinal))
                { requests.Add(frame + "|" + cast.Key + "|sfx|" + cue.id + "|" + cue.resourceId); Spawn(cast.Key, cue, frame); }
            }
            // Feedback descriptors are consumed only after the accepted CombatCommitReceipt.
            // Their legacy schema contains only resource IDs (no lifetime/binding). Do not guess
            // those missing policies; diagnose configured IDs until an explicit adapter exists.
            foreach (var hit in feedback)
                if (!string.IsNullOrEmpty(hit.VfxResourceId) || !string.IsNullOrEmpty(hit.SfxResourceId))
                    Diagnose(hit.Source.Cast + "/hit/" + hit.Source.GroupId, new NotSupportedException("Hit feedback resource requires explicit binding/lifetime policy; receipt retained, playback not guessed."));
            foreach (var exit in exits) Exit(exit.Cast, exit.Reason, frame);
        }
        public void Exit(CastKey cast, CastExitReason reason, long frame)
        {
            if (reason == CastExitReason.Completed) return;
            foreach (var effect in effects.Where(e => e.Cast.Equals(cast)).ToArray())
                if (reason == CastExitReason.Destroyed || effect.Cue.classification == VfxClass.Precast) Release(effect);
            foreach (var sound in sounds.Where(s => s.Cast.Equals(cast)).ToArray())
            {
                if (reason == CastExitReason.Destroyed || sound.Cue.onInterrupt == SoundInterrupt.StopImmediately) Release(sound);
                else if (sound.Cue.onInterrupt == SoundInterrupt.FadeOut && sound.FadeStart < 0)
                {
                    sound.FadeVolume = sound.Lease.Object.GetComponent<AudioSource>().volume;
                    sound.FadeStart = frame; sound.FadeEnd = Math.Min(sound.End, checked(frame + sound.Cue.fadeOutFrames));
                }
                // PlayToEnd, including Loop, retains its original finite End frame.
            }
        }
        void Release(Effect effect) { effects.Remove(effect); effect.Lease.Dispose(); }
        void Release(Sound sound) { sounds.Remove(sound); sound.Lease.Dispose(); }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (var e in effects.ToArray()) Release(e);
            foreach (var s in sounds.ToArray()) Release(s);
        }
    }
}
