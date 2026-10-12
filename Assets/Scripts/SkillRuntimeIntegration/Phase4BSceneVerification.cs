#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using FrameSync.RootMotion;
using GameProtocol;
using SkillConfig;
using UnityEngine;

namespace SkillRuntime.Integration
{
    public static class Phase4BSceneVerification
    {
        static int checks;
        static void Check(bool ok, string name) { if (!ok) throw new Exception("PHASE4B_SCENE_FAIL " + name); checks++; Debug.Log("PHASE4B_SCENE_PASS " + name); }
        static PlayerOperation Attack(int serial) => new PlayerOperation { battleID = 1, move = 121, rightOperation = RightOpType.rop1, operationID = serial, operationValue1 = 0 };
        static void Tick(PilotDevelopmentArena a, params PlayerOperation[] inputs)
        { var ops = new AllPlayerOperation(); ops.operations.AddRange(inputs); if (!a.Deliver(BattleData.Instance.NextFrameID, ops)) throw new Exception(a.Roles.Pilot.Fault); }
        public static IEnumerator Run(PilotDevelopmentArena a)
        {
            checks = 0; var work = Cases(a);
            while (true)
            {
                bool more;
                try { more = work.MoveNext(); }
                catch (Exception ex) { Debug.LogException(ex); UnityEditor.EditorApplication.Exit(1); yield break; }
                if (!more) break; yield return work.Current;
            }
            Debug.Log("PHASE4B_SCENE_COMPLETE checks=" + checks + " retained4A=" + PilotSceneVerification.PassedChecks + " realPlayers=true twoClients=false");
            UnityEditor.EditorApplication.Exit(0);
        }
        static IEnumerator Cases(PilotDevelopmentArena a)
        {
            var originalProfile = a.profile; var original = a.Configuration;
            // Run the accepted 4A matrix against a transient two-skill allowlist. Never rewrite SO/JSON.
            var prefix = UnityEngine.Object.Instantiate(a.profile);
            prefix.allowedCombos = prefix.allowedCombos.Take(2).ToArray(); prefix.sealedSourceHash = prefix.SourceHash();
            a.profile = prefix; a.UseTestConfiguration(prefix.ValidateAndFreeze());
            var prior = PilotSceneVerification.Cases(a);
            while (prior.MoveNext()) yield return prior.Current;
            a.profile = originalProfile; UnityEngine.Object.Destroy(prefix);
            a.UseTestConfiguration(original); yield return null;
            Check(original.Skills.Count == 5 && original.ContainerCount == 6, "approved Normal1-5 prefix, missing Normal6 still restricted");
            a.Players[1].objShape.SetPosition(new GameVector2(180000, 180000));
            CastKey? previous = null; int serial = 0;
            for (int index = 0; index < 5; index++)
            {
                var bound = original.Skills[index]; var origin = a.Players[0].LogicalPosition;
                var reference = new RootMotionPlayback(); reference.TryStart(bound.Motion, bound.MotionEnd, bound.DistancePermille, 100, 0, 100);
                long x = origin.x, z = origin.y;
                int stop = index == 4 ? bound.Skill.Duration : bound.Ready;
                for (int k = 0; k < stop; k++)
                {
                    Tick(a, k == 0 ? new[] { Attack(++serial) } : new PlayerOperation[0]);
                    if (reference.TryAdvance(out int dx, out int dz)) { x += dx; z += dz; }
                    Check(a.Players[0].LogicalPosition.x == x && a.Players[0].LogicalPosition.y == z, "real Normal" + (index + 1) + " identity displacement k=" + k);
                    if (k == 0)
                    {
                        var cast = a.Roles.Pilot.Cast(1);
                        Check(cast.Skill.SkillId == bound.Skill.SkillId && !Nullable.Equals(previous, cast.Key), "container successor accepts with new CastKey Normal" + (index + 1));
                        if (previous.HasValue) { a.Players[0].PilotStopMotion(previous.Value); a.Roles.Pilot.Runtime.Exit(previous.Value, CastExitReason.Interrupted); }
                        Check(a.Players[0].IsPlayingRootMotion, "late previous Cast cleanup leaves current motion"); previous = cast.Key;
                    }
                    var events = a.Roles.Pilot.Runtime.Events;
                    Check(events.All(e => e.Cast.Equals(previous.Value) && e.SkillId == bound.Skill.SkillId && e.Event.Frame == k), "event owner/frame Normal" + (index + 1) + " k=" + k);
                    string hash = a.Roles.Pilot.LastStateHash;
                    Check(!a.Roles.Pilot.Advance(BattleData.Instance.NextFrameID - 1, new AllPlayerOperation()) && hash == a.Roles.Pilot.LastStateHash, "repeated frame has no side effects");
                }
            }
            Check(a.Roles.Pilot.Cast(1) == null, "Normal5 D-1 resolves then completes");
            a.UseTestConfiguration(original); yield return null;
            a.Players[1].objShape.SetPosition(new GameVector2(180000, 180000));
            Tick(a, Attack(1)); var first = a.Roles.Pilot.Cast(1).Key; var s1 = original.Skills[0];
            for (int k = 1; k < s1.PreInput; k++) Tick(a, k == s1.PreInput - 1 ? new[] { Attack(2) } : new PlayerOperation[0]);
            Check(a.Roles.Pilot.Cast(1).Key.Equals(first), "before preinput rejected");
            Tick(a, Attack(3));
            Check(a.Roles.Pilot.Cast(1).Key.Equals(first), "at preinput start caches without immediate switch");
            for (int k = s1.PreInput + 1; k < s1.Ready; k++) Tick(a);
            Check(a.Roles.Pilot.Cast(1).Key.Equals(first), "cached input waits through Ready-1");
            Tick(a); Check(a.Roles.Pilot.Cast(1).Skill.SkillId == original.Skills[1].Skill.SkillId, "cached input consumed on Ready frame without new packet");
            string before = a.Roles.Pilot.DescribeState();
            Check(!a.Roles.Pilot.Advance(BattleData.Instance.NextFrameID + 1, new AllPlayerOperation()) && before == a.Roles.Pilot.DescribeState(), "gap frame does not advance cast/events/cache");

            // Explicit temporary multi-Clip test configuration, not formal authored gameplay.
            var dto = JsonUtility.FromJson<SkillConfigDto>(a.profile.exportedJson.text);
            var one = dto.skills.Single(s => s.skillId == original.Skills[0].Skill.SkillId);
            var two = dto.skills.Single(s => s.skillId == original.Skills[1].Skill.SkillId);
            var firstClip = one.animations[0]; var secondClip = JsonUtility.FromJson<AnimationDto>(JsonUtility.ToJson(two.animations[0]));
            firstClip.trimStartUs = 20001; firstClip.trimEndUs -= 30007; firstClip.speedPermille = 1503;
            firstClip.endFrame = Span(firstClip); firstClip.rootMotion.requiresTimeRemap = true;
            secondClip.id = "fixture-second"; secondClip.trimStartUs = 5009; secondClip.speedPermille = 500;
            secondClip.startFrame = firstClip.endFrame; secondClip.endFrame = secondClip.startFrame + Span(secondClip); secondClip.rootMotion.requiresTimeRemap = true;
            one.animations.Add(secondClip); one.durationFrames = secondClip.endFrame;
            if (!SkillConfigLoader.TryParse(JsonUtility.ToJson(dto), out var snapshot, out var issues)) throw new Exception(string.Join("\n", issues));
            if (!RuntimeSkillCatalog.TryCreateIsolated(snapshot, 33, 10000, out var catalog, out var error)) throw new Exception(error);
            var motion = new SkillMotionTimeline(one.animations.Select(d => new SkillMotionSegment(d, JsonUtility.FromJson<RootMotionJsonData>(Resources.Load<TextAsset>(d.rootMotion.resourcesPath).text))));
            var clips = new[] { new PilotAnimation(original.Skills[0].Clip, firstClip), new PilotAnimation(original.Skills[1].Clip, secondClip) };
            var mapped = new PilotSkill(original.Skills[0].Source, catalog.Skill(one.skillId), clips[0].Clip, clips[0].State, null, 0, 1000, one.events, motion, clips, one);
            a.UseTestConfiguration(new PilotConfiguration(new[] { mapped }, original.Hurt, original.HP, "explicit-multiclip-test", original.Idle, original.Hit, original.Death, original.Container, dto.resources)); yield return null;
            a.Players[1].objShape.SetPosition(new GameVector2(180000, 180000)); var start = a.Players[0].LogicalPosition;
            CastKey key = default;
            for (int k = 0; k < motion.Duration; k++)
            {
                Tick(a, k == 0 ? new[] { Attack(1) } : new PlayerOperation[0]); if (k == 0) key = a.Roles.Pilot.Cast(1).Key;
                var expected = motion.Position(k + 1, 100, 0, 100);
                Check(a.Players[0].LogicalPosition.x == start.x + expected.X && a.Players[0].LogicalPosition.y == start.y + expected.Z, "real mapped sole motion writer k=" + k);
                if (k < motion.Duration - 1) Check(a.Roles.Pilot.Cast(1).Key.Equals(key), "Clip changes preserve CastKey k=" + k);
                if (k == firstClip.endFrame)
                    Check(a.Players[0].Player.characterAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash == Animator.StringToHash(clips[1].State), "second actual Animator Clip state sampled at seam");
            }
            Check(a.Roles.Pilot.Cast(1) == null && !a.Players[0].IsPlayingRootMotion, "mapped last partial frame applied before exit");
            a.UseTestConfiguration(original); yield return null;
            PresentationCases(a, original);
        }
        static void PresentationCases(PilotDevelopmentArena a, PilotConfiguration original)
        {
            var bound = original.Skills[0]; var dto = bound.CopyPresentation(); dto.vfx.Clear(); dto.sfx.Clear();
            var resourceDtos = JsonUtility.FromJson<SkillConfigDto>(a.profile.exportedJson.text).resources;
            string sound = resourceDtos.First(r => r.kind == ResourceKind.AudioClip).id;
            foreach (var kind in new[] { VfxClass.Precast, VfxClass.AttackCritical })
                dto.vfx.Add(new VfxCue { id = "test-" + kind, resourceId = "miyabi-legacy-slash06", startFrame = 0, endFrame = 2,
                    classification = kind, lifetime = VfxLifetime.AutoComplete, attachment = AttachmentMode.Root, maxLifetimeFrames = 260, scalePermille = 1000 });
            foreach (var mode in new[] { SoundInterrupt.StopImmediately, SoundInterrupt.PlayToEnd, SoundInterrupt.FadeOut })
                dto.sfx.Add(new SfxCue { id = "test-" + mode, resourceId = sound, startFrame = 0, endFrame = 8,
                    mode = SoundMode.Loop, onInterrupt = mode, fadeOutFrames = mode == SoundInterrupt.FadeOut ? 3 : 0 });
            var skill = new PilotSkill(bound.Source, bound.Skill, bound.Clip, bound.State, bound.Motion, bound.MotionEnd, bound.DistancePermille, dto.events, bound.TimelineMotion, bound.Animations.ToArray(), dto);
            var config = new PilotConfiguration(new[] { skill }, original.Hurt, original.HP, "presentation-fixture-only", original.Idle, original.Hit, original.Death, original.Container, resourceDtos);
            var cast1 = new CastKey(new EntityKey(888, 1, 1, 1), 1); var cast2 = new CastKey(new EntityKey(888, 1, 2, 1), 1);
            using (var presentation = new PilotPresentation(config, a.Players))
            {
                var casts = new[] { new CastFrame(cast1, bound.Skill, 1, 1), new CastFrame(cast2, bound.Skill, 1, 1) };
                presentation.Tick(1, casts, new CastExit[0], new HitFeedback[0]);
                Check(presentation.ActiveVfx == 4 && presentation.ActiveSfx == 6, "same resource supports independent event IDs and mirror owners");
                string requests = presentation.LastRequests; presentation.Tick(1, casts, new CastExit[0], new HitFeedback[0]);
                Check(presentation.ActiveVfx == 4 && presentation.ActiveSfx == 6 && presentation.LastRequests == requests, "duplicate presentation frame creates no extra instances");
                presentation.Exit(cast1, CastExitReason.Interrupted, 2);
                Check(presentation.ActiveVfx == 3 && presentation.ActiveSfx == 5, "interrupt removes only owner Precast/StopImmediately; critical tail remains");
                presentation.Exit(cast1, CastExitReason.Interrupted, 2);
                Check(presentation.ActiveVfx == 3 && presentation.ActiveSfx == 5, "repeated old exit leaves other owner intact");
                for (int frame = 2; frame <= 5; frame++) presentation.Tick(frame, new CastFrame[0], new CastExit[0], new HitFeedback[0]);
                Check(presentation.ActiveSfx == 4, "FadeOut releases precisely at its finite endpoint");
                for (int frame = 6; frame <= 9; frame++) presentation.Tick(frame, new CastFrame[0], new CastExit[0], new HitFeedback[0]);
                Check(presentation.ActiveSfx == 0, "Loop PlayToEnd has finite original End, other owner expires independently");
                for (int frame = 10; frame <= 262; frame++) presentation.Tick(frame, new CastFrame[0], new CastExit[0], new HitFeedback[0]);
                Check(presentation.ActiveVfx == 0, "actual slash particle/sub-emitter instances finish within configured bound");
                presentation.Dispose(); presentation.Dispose();
                Check(presentation.ActiveVfx == 0 && presentation.ActiveSfx == 0, "session disposal is idempotent");
            }
            var clip = Resources.Load<AudioClip>(original.Resource(sound).path);
            var old = SFX_PoolManager.Instance.RentSkillSound(clip); var pooled = old.Object; old.Dispose();
            using (var next = SFX_PoolManager.Instance.RentSkillSound(clip))
            { next.Object.SetActive(true); old.Dispose(); Check(next.Object == pooled && next.Object.activeSelf, "late disposed lease cannot recycle object after a new owner rents it"); }
        }
        static int Span(AnimationDto a) => checked((int)(((a.trimEndUs - a.trimStartUs) * 1000 + 33000L * a.speedPermille - 1) / (33000L * a.speedPermille)));
    }
}
#endif
