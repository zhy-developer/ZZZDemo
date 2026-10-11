using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SkillConfig;
using SkillRuntime;
using UnityEngine;

namespace SkillRuntimeChecks
{
    // Shared source executed by .NET stubs AND by the isolated real Unity editor. No NUnit claims.
    public static class Phase3CChecks
    {
        static Action<string> output;
        static int checks;
        static readonly EntityKey A = new EntityKey(81, 1, 1, 1), B = new EntityKey(81, 1, 2, 1);
        static void Check(bool value, string scenario)
        { if (!value) throw new Exception("PHASE3C_FAIL " + scenario); checks++; output("PHASE3C_PASS " + scenario); }
        static void Throws(Action action, string scenario)
        { bool failed = false; try { action(); } catch (ArgumentException) { failed = true; } catch (InvalidOperationException) { failed = true; } catch (OverflowException) { failed = true; } Check(failed, scenario); }
        static SkillConfigDto Data(int duration = 6)
        {
            var data = Phase3BChecks.Fixture(duration); var s = data.skills[0];
            s.baseDamage = 0; s.hitGroups.RemoveAll(g => g.id != "g"); s.hitBoxes.RemoveAll(b => b.hitGroupId != "g");
            return data;
        }
        static RuntimeSkill Build(SkillConfigDto data)
        {
            if (!SkillConfigLoader.TryParse(JsonUtility.ToJson(data), out var snapshot, out var diagnostics)) throw new ArgumentException(string.Join("\n", diagnostics));
            if (!RuntimeSkillCatalog.TryCreateIsolated(snapshot, 33, 10000, out var catalog, out var error)) throw new ArgumentException(error);
            return catalog.Skills[0];
        }
        static RuntimeSkill Skill(Action<SkillDto> edit = null, int duration = 6)
        { var data = Data(duration); edit?.Invoke(data.skills[0]); return Build(data); }
        static void Window(SkillDto s, bool invincible, int start, int end)
        {
            string id = invincible ? "inv" : "interrupt";
            s.tracks.Add(new SkillTrack { id = id + "-track", kind = invincible ? TrackKind.Invincibility : TrackKind.Interrupt });
            (invincible ? s.invincibility : s.interrupts).Add(new FrameWindow { id = id, trackId = id + "-track", startFrame = start, endFrame = end });
        }
        static HitGroup Extra(SkillDto s, string id, int damage, int stun = 0, int x = 0, int z = 0, int n = 0)
        {
            var g = new HitGroup { id = id, damageSource = DamageSource.Explicit, explicitDamage = damage, hitStunFrames = stun, knockback = new Int3 { x = x, z = z }, knockbackFrames = n };
            s.hitGroups.Add(g);
            s.hitBoxes.Add(new HitBox { id = id + "-box", hitGroupId = id, trackId = "track", startFrame = 0, endFrame = s.durationFrames, shape = ShapeKind.Sphere, radius = 10000, height = 20000 });
            return g;
        }

        // The ONLY HP owner added this phase lives in Tests, not Assets. Whole-state compare + stage + swap.
        sealed class TestStateAdapter : ICombatStateAdapter
        {
            internal CombatFrameSnapshot Current;
            internal bool FailApply, ThrowDuringStage, ReverseRows;
            internal Action BeforeStage;
            internal int Publications;
            public bool TryApply(CombatFrameSnapshot expected, ReadOnlyCollection<TargetResolution> results, out string error)
            {
                error = null;
                // Identity of this immutable, revisioned snapshot is a stronger compare than only comparing HP.
                if (!ReferenceEquals(Current, expected)) { error = "Whole frame baseline invalidated"; return false; }
                if (results.Count != 2 || results.Select(r => r.Target).Distinct().Count() != 2
                    || results.Any(r => !ReferenceEquals(expected.Entities.SingleOrDefault(e => e.Entity.Equals(r.Target)), r.Expected)))
                { error = "Incomplete/mismatched target results"; return false; }
                BeforeStage?.Invoke();
                var staged = new List<CombatantSnapshot>();
                foreach (var r in ReverseRows ? results.Reverse() : results)
                {
                    staged.Add(new CombatantSnapshot(r.Expected.Geometry, r.ExitReason.HasValue ? null : r.Expected.Cast, r.FinalHP, r.Stun, r.Knockback));
                    if (staged.Count == 1 && ThrowDuringStage) throw new InvalidOperationException("Injected failure after staging first target (not published)");
                }
                var next = new CombatFrameSnapshot(expected.Frame, checked(expected.Revision + 1), staged);
                if (FailApply) { error = "Injected apply rejection"; return false; }
                Current = next; Publications++; return true;
            }
            internal CombatantSnapshot Row(EntityKey key) => Current.Entities.Single(e => e.Entity.Equals(key));
        }
        sealed class Host : IDisposable
        {
            internal readonly SkillFrameRuntime Runtime;
            internal readonly TestStateAdapter State = new TestStateAdapter();
            internal long AHP, BHP, Ax, Az, Bx = 5000, Bz;
            internal int FacingA = 90000, FacingB = -90000;
            internal Host(long aHP = 100, long bHP = 100, long previousFrame = 99)
            { AHP = aHP; BHP = bHP; Runtime = new SkillFrameRuntime(A, B, previousFrame); }
            internal void Begin(RuntimeSkill a = null, RuntimeSkill b = null)
            {
                if (Runtime.TryBeginFrame(Runtime.LastCompletedFrame + 1) != FrameBeginResult.Started) throw new Exception("Test host frame failure");
                if (a != null) Runtime.StartAccepted(A, a); if (b != null) Runtime.StartAccepted(B, b);
            }
            internal void Capture(bool reverse = false, long y = 0)
            {
                var geo = new[] { new EntityGeometrySnapshot(A, new LogicPose(Ax, y, Az, FacingA), new HurtCircle(5000, 0, y, 0)),
                    new EntityGeometrySnapshot(B, new LogicPose(Bx, -y, Bz, FacingB), new HurtCircle(5000, 0, -y, 0)) };
                Runtime.CapturePostMove(reverse ? geo.Reverse() : geo);
                var states = geo.Select(g => {
                    var old = State.Current == null ? null : State.Row(g.Entity); var cast = Runtime.Current(g.Entity);
                    return new CombatantSnapshot(g, cast == null ? (CastKey?)null : cast.Key, old == null ? (g.Entity.Equals(A) ? AHP : BHP) : old.HP,
                        old == null ? default(StunInterval) : old.Stun, old == null ? null : old.Knockback);
                }).ToArray();
                State.Current = new CombatFrameSnapshot(Runtime.OpenFrame.Value, State.Current == null ? 0 : State.Current.Revision + 1, reverse ? states.Reverse() : states);
            }
            internal CombatBatch Resolve(bool reverse = false) => CombatResolver.Resolve(Runtime, State.Current, reverse ? Runtime.Candidates.Reverse() : Runtime.Candidates);
            internal CombatCommitReceipt Commit(CombatBatch batch = null)
            {
                if (!(batch ?? Resolve()).TryCommit(State, out var receipt, out var error)) throw new Exception("Commit failed: " + error);
                return receipt;
            }
            internal TargetResolution Target(CombatBatch batch, EntityKey key) => batch.Targets.Single(t => t.Target.Equals(key));
            internal void Finish() => Runtime.CompleteFrame();
            public void Dispose() => Runtime.Dispose();
        }
        public static void Run(Action<string> write)
        {
            output = write; checks = 0;
            Config(); Defenses(); Stuns(); Knockback(); Batches(); Lifetimes(); Replay();
            output("PHASE3C_ASSERTIONS " + checks.ToString(CultureInfo.InvariantCulture));
        }
        static void Config()
        {
            var d = Data(); var s = d.skills[0]; s.baseDamage = 7; Extra(s, "explicit", 3);
            var skill = Build(d); s.baseDamage = 99; s.hitGroups[1].explicitDamage = 88;
            Check(skill.Group("g").Damage == 7 && skill.Group("explicit").Damage == 3, "immutable damage projection: base7 OR explicit3, never add; DTO edits cannot change runtime");
            d = Data(); s = d.skills[0]; s.hitGroups[0].knockback = new Int3 { x = 1, y = 2 }; s.hitGroups[0].knockbackFrames = 3;
            string original = JsonUtility.ToJson(d); Throws(() => Build(d), "nonzero knockback Y rejects runtime capability");
            Check(original == JsonUtility.ToJson(d), "Y capability rejection does not zero or mutate source DTO");
            s.hitGroups[0].knockback.y = 0; s.hitGroups[0].knockbackFrames = 0; Throws(() => Build(d), "nonzero displacement with N0 explicitly rejected");
            s.hitGroups[0].knockbackFrames = 1; s.hitGroups[0].knockback.x = 1000000001; Throws(() => Build(d), "knockback local coordinate beyond 1e9 rejects");
            s.hitGroups[0].knockback.x = 0; s.skillType = SkillType.Unspecified; Throws(() => Build(d), "Unspecified has no guessed Normal/Ultimate policy");
        }
        static void Defenses()
        {
            var zero = Skill();
            using (var h = new Host())
            {
                h.Begin(zero, Skill(s => s.skillType = SkillType.Ultimate)); h.Capture(); var batch = h.Resolve();
                Check(h.Target(batch, B).Defense == DefenseState.Invincible && batch.Accepted.All(c => !c.Target.Equals(B)), "F100 Ultimate k0 ignores geometry; no accepted hit into Ultimate");
                var r = h.Commit(batch); Check(r.Feedback.All(f => !f.Source.Target.Equals(B)) && h.Runtime.RecordCount == 1, "invincible target has no feedback/record; other side independent");
                h.Finish();
                for (int i = 1; i < 6; i++) { h.Begin(); h.Capture(); batch = h.Resolve(); Check(h.Target(batch, B).Defense == DefenseState.Invincible, "Ultimate invincible active k=" + i); h.Commit(batch); h.Finish(); }
                Check(h.Runtime.Current(B) == null, "Ultimate D-1 protected before normal completion");
            }
            using (var h = new Host())
            {
                h.Begin(Skill(s => s.hitGroups[0].mode = HitMode.Single), Skill(s => { Window(s, true, 0, 2); Window(s, false, 0, 4); s.hitBoxes.Clear(); }));
                for (int k = 0; k < 3; k++)
                {
                    if (k > 0) h.Begin(); h.Capture(); var b = h.Resolve();
                    Check((k < 2 ? b.Accepted.Count == 0 && h.Runtime.RecordCount == 0 : b.Accepted.Count == 1), "Single invincibility [0,2) boundary k=" + k + "; prior rejection does not consume group");
                    var r = h.Commit(b);
                    if (k == 2) Check(r.Targets.Single(t => t.Target.Equals(B)).ExitReason == CastExitReason.Interrupted && r.Feedback.Count == 1, "zero damage/stun/knockback still interrupts Normal at invincibility end");
                    h.Finish();
                }
            }
            using (var h = new Host())
            {
                h.Begin(Skill(s => { s.hitGroups[0].mode = HitMode.Repeat; s.hitGroups[0].repeatIntervalFrames = 3; }),
                    Skill(s => { Window(s, true, 1, 4); s.hitBoxes.Clear(); }));
                for (int k = 0; k < 6; k++)
                {
                    if (k > 0) h.Begin(); h.Capture(); var b = h.Resolve();
                    Check(b.Accepted.Count == (k == 0 || k == 4 ? 1 : 0), "Repeat3: accepted k0, invincible k1..3 cannot refresh, accepts k4, rejects k5; actual k=" + k);
                    h.Commit(b); h.Finish();
                }
            }
            foreach (int kWanted in new[] { 0, 1, 2, 3 }) using (var h = new Host())
            {
                h.Begin(null, Skill(s => { s.hitBoxes.Clear(); Window(s, false, 1, 3); }));
                for (int k = 0; k < kWanted; k++) { h.Capture(); h.Commit(); h.Finish(); h.Begin(); }
                h.Runtime.StartAccepted(A, Skill(s => { s.baseDamage = 5; s.hitGroups[0].hitStunFrames = 3; s.hitGroups[0].knockback = new Int3 { z = 10 }; s.hitGroups[0].knockbackFrames = 2; }));
                h.Capture(); var b = h.Resolve(); var t = h.Target(b, B); bool interruptible = kWanted == 1 || kWanted == 2;
                Check(t.FinalHP == 95 && b.Accepted.Count == 1 && (interruptible ? t.ExitReason == CastExitReason.Interrupted && t.Knockback != null : !t.ExitReason.HasValue && t.Stun.End == 0 && t.Knockback == null),
                    "Normal window [1,3): HP100 ->95; inside interrupt, outside armor suppresses only control k=" + kWanted);
                h.Commit(b); h.Finish();
            }
            using (var h = new Host())
            {
                h.Begin(zero); h.Capture(); var r = h.Commit();
                Check(r.Feedback.Count == 1 && h.Runtime.RecordCount == 1 && h.State.Row(B).HP == 100 && !r.Targets.Single(t => t.Target.Equals(B)).ExitReason.HasValue,
                    "idle zero-effect effective hit records and feeds back; no fake interrupted cast"); h.Finish();
            }
            using (var h = new Host())
            {
                var d = Data(); d.resources.Add(new ResourceDto { id = "feedback-vfx", kind = ResourceKind.VfxPrefab, path = "test/fx" });
                d.resources.Add(new ResourceDto { id = "feedback-sfx", kind = ResourceKind.AudioClip, path = "test/sfx" });
                d.skills[0].hitGroups[0].hitVfxResourceId = "feedback-vfx"; d.skills[0].hitGroups[0].hitSfxResourceId = "feedback-sfx";
                h.Begin(Build(d)); h.Capture(); var r = h.Commit();
                Check(r.Feedback.Count == 1 && r.Feedback[0].Damage == 0 && r.Feedback[0].VfxResourceId == "feedback-vfx" && r.Feedback[0].SfxResourceId == "feedback-sfx"
                    && r.Feedback[0].Source.HitBoxId == "a-box", "zero-damage feedback retains configured resource IDs and stable representative source; no resource playback"); h.Finish();
            }
        }
        static void Stuns()
        {
            foreach (int n in new[] { 0, 1, 3 }) using (var h = new Host())
            {
                h.Begin(Skill(s => s.hitGroups[0].hitStunFrames = n)); h.Capture(); var r = h.Commit(); var stun = h.State.Row(B).Stun;
                Check(!stun.Contains(100) && (n == 0 ? stun.End == 0 : stun.Start == 101 && stun.End == 101 + n && stun.Contains(100 + n) && !stun.Contains(101 + n)), "hit F100 stun N=" + n + " covers [101,101+N), no hit-frame restriction"); h.Finish();
            }
            using (var h = new Host())
            {
                h.Begin(Skill(s => { s.hitGroups[0].hitStunFrames = 1; Extra(s, "longer", 0, 3); })); h.Capture(); h.Commit();
                Check(h.State.Row(B).Stun.End == 104, "F100 stun1+stun3 -> end104, not105"); h.Finish();
                h.Begin(Skill()); h.Capture(); h.Commit(); Check(h.State.Row(B).Stun.End == 104, "F101 zero stun preserves existing end104"); h.Finish();
                h.Begin(Skill(s => s.hitGroups[0].hitStunFrames = 3)); h.Capture(); h.Commit(); Check(h.State.Row(B).Stun.End == 106, "F102 new3 extends existing end104 to106"); h.Finish();
                h.Begin(Skill(s => s.hitGroups[0].hitStunFrames = 1)); h.Capture(); h.Commit(); Check(h.State.Row(B).Stun.End == 106, "F103 new1 cannot shorten existing end106"); h.Finish();
            }
        }
        static RuntimeSkill MotionSkill(int x, int z, int n) => Skill(s => { s.hitGroups[0].knockback = new Int3 { x = x, z = z }; s.hitGroups[0].knockbackFrames = n; });
        static void Knockback()
        {
            using (var h = new Host())
            {
                h.Begin(Skill(s => { s.hitGroups[0].knockback = new Int3 { x = 3, z = 4 }; s.hitGroups[0].knockbackFrames = 2;
                    Extra(s, "strong", 0, 0, -10, 0, 3); Extra(s, "tie", 0, 0, 0, 10, 9); })); h.Capture(); h.Commit(); var p = h.State.Row(B).Knockback;
                Check(p.Source.GroupId == "strong" && p.Duration == 3 && p.WorldTotal.X == -10 && p.WorldTotal.Z == 0, "largest length wins; equal100 strong before tie Ordinal; direction/total/duration same source");
                Check(p.DeltaAt(100).X == 0 && p.DeltaAt(101).X == -3 && p.DeltaAt(102).X == -3 && p.DeltaAt(103).X == -4 && p.DeltaAt(104).X == 0, "F100 total-10/N3 starts101; deltas -3,-3,-4 and exact finite tail");
                h.Finish(); h.Bx += p.DeltaAt(101).X; long moved = h.Bx;
                h.FacingA = 0; h.Begin(MotionSkill(0, 7, 2)); h.Capture(); h.Commit(); var replacement = h.State.Row(B).Knockback;
                Check(h.Bx == moved && replacement.Source.Frame == 101 && replacement.WorldTotal.X == 7 && replacement.WorldTotal.Z == 0, "F101 new knockback replaces remainder only; attacker facing0 +Zlocal -> +Xworld; applied -3 not rolled back");
                h.Finish(); h.FacingA = 180000; h.Begin(Skill()); h.Capture(); h.Commit();
                Check(ReferenceEquals(replacement, h.State.Row(B).Knockback) && replacement.WorldTotal.X == 7, "F102 zero knockback preserves old plan; later attacker facing cannot turn it"); h.Finish();
            }
            foreach (var pair in new[] { new[] { 7, -11, 6 }, new[] { -7, 11, 100 }, new[] { 1000000000, -1000000000, 3 } }) using (var h = new Host())
            {
                h.FacingA = 349018; h.Begin(MotionSkill(pair[0], pair[1], pair[2])); h.Capture(); h.Commit(); var p = h.State.Row(B).Knockback;
                long x = 0, z = 0; for (int n = 0; n < p.Duration; n++) { var d = p.DeltaAt(p.StartFrame + n); x = checked(x + d.X); z = checked(z + d.Z); }
                Check(x == p.WorldTotal.X && z == p.WorldTotal.Z, "nonaxis349018 cumulative exact sum local=" + pair[0] + "," + pair[1] + " N=" + pair[2]); h.Finish();
            }
            using (var h = new Host())
            {
                h.FacingA = 0; h.Begin(MotionSkill(3, 4, int.MaxValue)); h.Capture(); h.Commit(); var p = h.State.Row(B).Knockback;
                Check(p.WorldTotal.X == 4 && p.WorldTotal.Z == -3 && p.CumulativeAfter(int.MaxValue).X == 4, "axis exact rotation, int.MaxValue duration bounded product no overflow"); h.Finish();
            }
            foreach (int facing in new[] { 45000, 225000 }) using (var h = new Host())
            {
                h.FacingA = facing; h.Begin(MotionSkill(0, 1, 3)); h.Capture(); h.Commit(); var p = h.State.Row(B).Knockback; long sign = facing == 45000 ? 1 : -1;
                Check(p.WorldTotal.X == sign && p.WorldTotal.Z == sign && p.DeltaAt(101).X == 0 && p.DeltaAt(103).X == sign,
                    "sqrt(0.5) total rounds once to signed1; N3 preserves substep residual, facing=" + facing); h.Finish();
            }
        }
        static void Batches()
        {
            using (var h = new Host(100, 10))
            {
                h.Begin(Skill(s => { s.baseDamage = 6; Extra(s, "other", 6); })); h.Capture(); var batch = h.Resolve();
                Check(h.Runtime.RecordCount == 0 && h.State.Row(B).HP == 10, "Resolve is read-only, HP10 and ledger0 unchanged");
                var r = h.Commit(batch);
                Check(r.Targets.Single(t => t.Target.Equals(B)).TotalDamage == 12 && h.State.Row(B).HP == 0 && h.Runtime.RecordCount == 2 && r.Feedback.Count == 2 && h.State.Publications == 1, "HP10 two groups6 aggregate once ->0, two accepted records/feedback, one state publish");
                string before = h.Runtime.DescribeState(); var state = h.State.Current;
                Check(!batch.TryCommit(h.State, out var repeat, out _) && repeat == null && ReferenceEquals(state, h.State.Current) && before == h.Runtime.DescribeState(), "same batch duplicate no HP/ledger/exit/feedback writes"); h.Finish();
            }
            using (var h = new Host(10, 10))
            {
                var s = Skill(d => { d.baseDamage = 20; d.hitGroups[0].hitStunFrames = 3; d.hitGroups[0].knockback = new Int3 { x = 10 }; d.hitGroups[0].knockbackFrames = 3; Extra(d, "extra", 6); Window(d, false, 0, 6); });
                h.Begin(s, s); var ka = h.Runtime.Current(A).Key; var kb = h.Runtime.Current(B).Key; h.Runtime.OwnHandle(ka, "a-motion"); h.Runtime.OwnHandle(kb, "b-motion"); h.Capture(); var r = h.Commit();
                Check(r.Round == RoundResult.Draw && !r.Winner.HasValue && r.Feedback.Count == 4 && h.Runtime.RecordCount == 4 && r.Targets.All(t => t.FinalHP == 0 && t.TotalDamage == 26), "same-frame both lethal -> unique Draw, first lethal never cancels other three effective hits");
                Check(r.Targets.All(t => t.ExitReason == CastExitReason.Death && t.Stun.End == 0 && t.Knockback == null), "Death beats Interrupted; dead targets do not start stun/knockback");
                h.Finish(); Check(h.Runtime.Exits.Count == 2 && h.Runtime.Exits.All(e => e.Reason == CastExitReason.Death && e.ReleasedHandles.Count == 1), "one Death exit/cleanup per cast after batch");
                h.Begin(); h.Capture(); r = h.Commit();
                Check(r.Round == RoundResult.None && r.Feedback.Count == 0 && h.Runtime.RecordCount == 0, "frame after double KO does not emit another Draw or replay dead casts"); h.Finish();
            }
            using (var h = new Host(100, 5))
            {
                h.Begin(Skill(s => s.baseDamage = 6), Skill(s => s.hitBoxes.Clear())); h.Capture(); var r = h.Commit();
                Check(r.Round == RoundResult.Winner && r.Winner.Value.Equals(A) && r.Targets.Single(t => t.Target.Equals(B)).ExitReason == CastExitReason.Death, "armor HP5 hit6 dies despite no interrupt window"); h.Finish();
            }
            using (var h = new Host(long.MaxValue, long.MaxValue))
            {
                h.Begin(Skill(s => { s.baseDamage = int.MaxValue; Extra(s, "other", int.MaxValue); })); h.Capture(); var r = h.Commit();
                Check(h.State.Row(B).HP == long.MaxValue - 4294967294L && r.Targets.Single(t => t.Target.Equals(B)).TotalDamage == 4294967294L, "two int.MaxValue damages accumulate in checked Int64 before subtraction/clamp"); h.Finish();
            }
            using (var h = new Host(0, 100))
            {
                h.Begin(Skill(s => s.baseDamage = 20)); h.Capture(); var r = h.Commit();
                Check(r.Feedback.Count == 0 && h.State.Row(B).HP == 100 && r.Round == RoundResult.None, "already dead snapshot cannot source new hits or emit repeated round result"); h.Finish();
            }
            foreach (string fault in new[] { "hp", "revision", "frame", "identity", "cast", "pose", "hurt", "stun", "apply-false", "stage-throw", "reentrant", "runtime-exit", "runtime-ledger", "runtime-dispose", "runtime-complete" }) using (var h = new Host())
            {
                h.Begin(Skill(s => s.baseDamage = 3), Skill(s => { s.baseDamage = 4; Window(s, false, 0, 6); })); h.Capture(); var batch = h.Resolve();
                if (fault == "apply-false") h.State.FailApply = true;
                else if (fault == "stage-throw") h.State.ThrowDuringStage = true;
                else if (fault == "reentrant") h.State.BeforeStage = () => h.Runtime.CompleteFrame();
                else if (fault == "runtime-exit") h.Runtime.Exit(h.Runtime.Current(A).Key, CastExitReason.Completed);
                else if (fault == "runtime-ledger") h.Runtime.TryCommitAccepted(h.Runtime.Candidates[0]);
                else if (fault == "runtime-dispose") h.Runtime.Dispose();
                else if (fault == "runtime-complete") h.Runtime.CompleteFrame();
                else
                {
                    var old = h.State.Current; var rows = old.Entities.ToArray(); var t = rows[1]; var geo = t.Geometry;
                    if (fault == "identity") geo = new EntityGeometrySnapshot(new EntityKey(81, 1, 3, 1), geo.Pose, geo.Hurt);
                    if (fault == "pose") geo = new EntityGeometrySnapshot(B, new LogicPose(5001, 0, 0, 90000), geo.Hurt);
                    if (fault == "hurt") geo = new EntityGeometrySnapshot(B, geo.Pose, new HurtCircle(1));
                    rows[1] = new CombatantSnapshot(geo, fault == "identity" ? null : fault == "cast" ? (CastKey?)new CastKey(B, 999) : t.Cast,
                        fault == "hp" ? 99 : t.HP, fault == "stun" ? new StunInterval(101, 104) : t.Stun, t.Knockback);
                    h.State.Current = new CombatFrameSnapshot(fault == "frame" ? 101 : old.Frame, fault == "revision" ? old.Revision + 1 : old.Revision, rows);
                }
                string runtimeBefore = h.Runtime.DescribeState(); var stateBefore = h.State.Current;
                bool committed = batch.TryCommit(h.State, out var receipt, out _);
                Check(!committed && receipt == null && ReferenceEquals(stateBefore, h.State.Current) && h.State.Publications == 0 && runtimeBefore == h.Runtime.DescribeState(), "atomic reject " + fault + ": neither side HP/ledger/exits/feedback partially published");
            }
            using (var h = new Host())
            {
                h.Begin(Skill()); h.Capture(); var batch = h.Resolve(); h.State.FailApply = true;
                batch.TryCommit(h.State, out _, out _); h.State.FailApply = false;
                Check(batch.TryCommit(h.State, out var r, out _) && r.Feedback.Count == 1, "rejected apply leaves Single eligible; unchanged baseline can retry successfully"); h.Finish();
            }
            using (var h = new Host(previousFrame: long.MaxValue - 1))
            {
                h.Begin(Skill(s => s.hitGroups[0].hitStunFrames = 1)); h.Capture(); var before = h.Runtime.DescribeState();
                Throws(() => h.Resolve(), "frame-end overflow explicitly fails proposal"); Check(h.Runtime.DescribeState() == before && h.State.Publications == 0, "arithmetic rejection leaves baseline intact");
            }
            using (var h = new Host())
            {
                h.Begin(Skill()); h.Capture(); var initial = h.State.Current;
                h.State.Current = new CombatFrameSnapshot(initial.Frame, long.MaxValue, initial.Entities); var b = h.Resolve(); var baseline = h.State.Current; string runtimeBefore = h.Runtime.DescribeState();
                Check(!b.TryCommit(h.State, out var r, out _) && r == null && ReferenceEquals(baseline, h.State.Current) && runtimeBefore == h.Runtime.DescribeState(), "host revision overflow in staging cannot publish HP or consume ledger");
            }
            using (var h = new Host())
            {
                h.Begin(Skill()); h.Capture();
                Throws(() => CombatResolver.Resolve(h.Runtime, h.State.Current, new HitCandidate[0]), "omitting captured candidate is rejected, not a silent partial frame");
                Throws(() => CombatResolver.Resolve(h.Runtime, h.State.Current, new[] { h.Runtime.Candidates[0], h.Runtime.Candidates[0] }), "duplicate candidate permutation is rejected");
                var forged = new CombatFrameSnapshot(100, 0, h.State.Current.Entities.Select(e => new CombatantSnapshot(e.Geometry, null, e.HP)));
                Throws(() => CombatResolver.Resolve(h.Runtime, forged), "forged cast baseline rejected before calculation even with correct HP/pose");
            }
        }
        static void Lifetimes()
        {
            using (var h = new Host())
            {
                h.Begin(Skill(s => s.baseDamage = 1, 1), Skill(s => { s.hitBoxes.Clear(); Window(s, false, 0, 1); }, 1));
                var a = h.Runtime.Current(A).Key; var b = h.Runtime.Current(B).Key; h.Runtime.OwnHandle(a, "motion-a"); h.Runtime.OwnHandle(b, "motion-b"); h.Capture(); var r = h.Commit();
                Check(h.Runtime.Current(A) != null && h.Runtime.RecordCount == 1 && h.State.Row(B).HP == 99, "D1 k0=D-1 damage/ledger settle before completion cleanup"); h.Finish();
                Check(h.Runtime.Exits.Single(e => e.Cast.Equals(a)).Reason == CastExitReason.Completed && h.Runtime.Exits.Single(e => e.Cast.Equals(b)).Reason == CastExitReason.Interrupted && r.Feedback.Count == 1, "Interrupted beats Completed at final frame; receipt survives ledger cleanup");
            }
            using (var h = new Host())
            {
                h.Begin(null, Skill(s => { s.hitBoxes.Clear(); Window(s, false, 0, 6); s.events[0].frame = 3; s.events[1].frame = 4; }));
                var old = h.Runtime.Current(B).Key; h.Runtime.OwnHandle(old, "old-future-motion"); h.Runtime.OwnHandle(old, "old-future-events");
                h.Runtime.StartAccepted(A, Skill()); h.Bx += 123; long moved = h.Bx; h.Capture(); h.Commit(); h.Finish();
                Check(h.Bx == moved && !h.Runtime.OwnsHandle(old, "old-future-motion") && !h.Runtime.OwnsHandle(old, "old-future-events"), "interrupt cleans old future ownership without undoing post-move position");
                h.Begin(); h.Capture(); Check(h.Runtime.Events.All(e => !e.Cast.Equals(old)), "next frame has no events from interrupted cast"); h.Commit(); h.Finish();
                h.Begin(null, Skill(s => s.skillType = SkillType.Ultimate)); var newer = h.Runtime.Current(B).Key; h.Runtime.OwnHandle(newer, "old-future-motion");
                Check(!h.Runtime.Exit(old, CastExitReason.Death) && h.Runtime.OwnsHandle(newer, "old-future-motion"), "late Death cleanup on old cast cannot clear reused handle of new cast");
                h.Runtime.StartAccepted(A, Skill()); h.Capture(); Check(h.Resolve().Accepted.All(c => !c.Target.Equals(B)), "same-frame accepted switch to Ultimate uses new defense immediately"); h.Commit(); h.Finish();
            }
            using (var h = new Host())
            {
                var attack = Skill(s => { s.hitGroups[0].mode = HitMode.Repeat; s.hitGroups[0].repeatIntervalFrames = 2; });
                h.Begin(attack); var key = h.Runtime.Current(A).Key;
                for (int k = 0; k < 6; k++)
                {
                    if (k > 0) h.Begin(); h.Capture(); var before = h.Runtime.DescribeState();
                    Check(h.Runtime.TryBeginFrame(h.Runtime.OpenFrame.Value) == FrameBeginResult.Duplicate && h.Runtime.DescribeState() == before, "empty-operation cast k=" + k + " duplicate delivery does not change state");
                    var b = h.Resolve(); Check(b.Accepted.Count == (k % 2 == 0 ? 1 : 0) && h.Runtime.Current(A).Key.Equals(key), "Repeat2 exact eligibility and same cast across clips k=" + k);
                    h.Commit(b); h.Finish(); before = h.Runtime.DescribeState();
                    Check(h.Runtime.TryBeginFrame(h.Runtime.LastCompletedFrame + 2) == FrameBeginResult.Gap && before == h.Runtime.DescribeState(), "missing continuous frame after k=" + k + " cannot advance");
                }
            }
        }
        static string ReplayOnce(bool reverse, long y)
        {
            var d = Data(4); var s = d.skills[0]; s.baseDamage = 2; s.hitGroups[0].mode = HitMode.Repeat; s.hitGroups[0].repeatIntervalFrames = 2;
            s.hitGroups[0].hitStunFrames = 3; s.hitGroups[0].knockback = new Int3 { x = -3, z = 7 }; s.hitGroups[0].knockbackFrames = 3;
            Window(s, false, 0, 1);
            if (reverse) s.hitBoxes.Reverse(); var skill = Build(d); var text = new StringBuilder();
            using (var h = new Host(200, 200))
            {
                h.State.ReverseRows = reverse;
                for (int f = 0; f < 16; f++)
                {
                    h.Begin(f % 4 == 0 ? skill : null, f % 4 == 0 ? skill : null);
                    if (h.State.Current != null)
                    {
                        var da = h.State.Row(A).Knockback?.DeltaAt(h.Runtime.OpenFrame.Value) ?? new XzDelta(0, 0);
                        var db = h.State.Row(B).Knockback?.DeltaAt(h.Runtime.OpenFrame.Value) ?? new XzDelta(0, 0);
                        h.Ax += da.X; h.Az += da.Z; h.Bx += db.X; h.Bz += db.Z;
                    }
                    h.Capture(reverse, y);
                    var batch = h.Resolve(reverse); var receipt = h.Commit(batch); h.Finish();
                    text.Append(h.Runtime.DescribeState()).Append('\n');
                    foreach (var t in receipt.Targets) text.Append(FormattableString.Invariant($"{t.Target}:{t.ExpectedOldHP}>{t.FinalHP}/{t.Stun.Start}/{t.Stun.End}/{t.ExitReason};"));
                    foreach (var row in h.State.Current.Entities)
                    {
                        text.Append(FormattableString.Invariant($"{row.Entity}:{row.Cast}/{row.Geometry.Pose.X}/{row.Geometry.Pose.Z}/{row.Geometry.Pose.FacingMilliDegrees};"));
                        var p = row.Knockback;
                        if (p != null) text.Append(FormattableString.Invariant($"{p.Source}/{p.StartFrame}/{p.EndFrame}/{p.WorldTotal.X}/{p.WorldTotal.Z};"));
                    }
                    foreach (var feedback in receipt.Feedback) text.Append(feedback.Source).Append('/').Append(feedback.Damage.ToString(CultureInfo.InvariantCulture)).Append(';');
                    text.Append(receipt.Round).Append('/').Append(receipt.Winner).Append('\n');
                }
            }
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "").ToLowerInvariant();
        }
        static void Replay()
        {
            string expected = ReplayOnce(false, 0);
            for (int i = 0; i < 20; i++) if (ReplayOnce(i % 2 == 0, i % 2 == 0 ? long.MaxValue : 0) != expected) throw new Exception("Phase3C replay mismatch " + i);
            Check(true, "20 x 16 frames same initial/input: reversed entities/boxes/candidates/result application + Y changes identical per-frame state/output");
            output("PHASE3C_REPLAY_SHA256 " + expected);
        }
        public static void Measure(Action<string> write)
        {
            var skill = Skill(s => { s.baseDamage = 1; Extra(s, "extra", 1); Window(s, false, 0, 6); });
            Action run = () => { using (var h = new Host()) { h.Begin(skill, skill); h.Capture(); h.Commit(); h.Finish(); } };
            for (int i = 0; i < 5; i++) run();
            var counter = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
            Func<long> allocated = counter == null ? (Func<long>)null : () => (long)counter.Invoke(null, null);
            long probeBefore = allocated == null ? 0 : allocated(); var probe = new byte[4096]; GC.KeepAlive(probe);
            bool supported = allocated != null && allocated() - probeBefore >= 4096;
            long before = supported ? allocated() : 0; var timer = Stopwatch.StartNew();
            for (int i = 0; i < 200; i++) run(); timer.Stop(); long bytes = supported ? allocated() - before : -1;
            write("PHASE3C_PERF 200 isolated 1v1 frames, four accepted hits, projection prebuilt: ms=" + timer.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture)
                + " allocatedBytes=" + (supported ? bytes.ToString(CultureInfo.InvariantCulture) : "UNAVAILABLE (counter probe failed)") + "; includes host snapshots/collector/resolve/commit/exit; not production budget");
        }
    }
}
