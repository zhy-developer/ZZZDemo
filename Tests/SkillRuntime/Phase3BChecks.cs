using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using SkillConfig;
using SkillRuntime;
using UnityEngine;

namespace SkillRuntimeChecks
{
    // Shared assertion suite: run under .NET stubs AND real Unity JsonUtility/runtime assemblies.
    public static class Phase3BChecks
    {
        static Action<string> output;
        static int checks;
        static void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; output("PHASE3B_PASS " + name); }
        static void Throws(Action action, string name)
        { bool threw = false; try { action(); } catch (ArgumentException) { threw = true; } catch (InvalidOperationException) { threw = true; } Check(threw, name); }
        static readonly EntityKey A = new EntityKey(71, 2, 1, 1), B = new EntityKey(71, 2, 2, 1);
        static LogicPose Pose(long x = 0, long z = 0, int facing = 90000, long y = 0) => new LogicPose(x, y, z, facing);
        static HitBox Box(ShapeKind kind = ShapeKind.Sphere, int radius = 10000, int yaw = 0) => new HitBox {
            id = "box", hitGroupId = "g", trackId = "track", startFrame = 0, endFrame = 6, shape = kind,
            radius = radius, height = checked(radius * 2), size = new Int3 { x = 20000, y = 10000, z = 20000 }, yawMilliDegrees = yaw, sectorAngleMilliDegrees = 180000 };
        static bool Hit(HitBox box, long x, long z, long r = 5000, LogicPose? source = null, int targetFacing = 90000, long targetY = 0, long hurtX = 0, long hurtY = 0, long hurtZ = 0)
            => XzGeometry.Intersects(new AttackShape(box), source ?? Pose(), new HurtCircle(r, hurtX, hurtY, hurtZ), Pose(x, z, targetFacing, targetY));
        public static SkillConfigDto Fixture(int duration = 6)
        {
            var s = new SkillDto { skillId = "normal", displayName = "Normal", skillType = SkillType.Normal, baseDamage = 0, durationFrames = duration };
            int split = duration / 2;
            if (split > 0) s.animations.Add(Segment("clip-a", 0, split));
            s.animations.Add(Segment("clip-b", split, duration));
            s.tracks.Add(new SkillTrack { id = "track", kind = TrackKind.HitBox });
            s.tracks.Add(new SkillTrack { id = "event-track", kind = TrackKind.Event });
            s.hitGroups.Add(new HitGroup { id = "g", mode = HitMode.Single });
            s.hitGroups.Add(new HitGroup { id = "r", mode = HitMode.Repeat, repeatIntervalFrames = 3 });
            foreach (string id in new[] { "z-box", "a-box", "repeat-box" }) { var b = Box(); b.id = id; b.endFrame = duration; if (id == "repeat-box") b.hitGroupId = "r"; s.hitBoxes.Add(b); }
            s.events.Add(new SkillEvent { id = "event-z", eventId = "presentation.marker", trackId = "event-track", frame = 0, order = 1, parameterKind = EventParameterKind.Marker, marker = new MarkerParameters() });
            s.events.Add(new SkillEvent { id = "event-a", eventId = "presentation.marker", trackId = "event-track", frame = 0, order = 1, parameterKind = EventParameterKind.Marker, marker = new MarkerParameters() });
            return new SkillConfigDto { schemaVersion = 1, characterId = "test", logicFrameIntervalMs = 33, precision = 10000, completeness = CatalogCompleteness.PartialPilot, skills = new List<SkillDto> { s } };
        }
        static AnimationDto Segment(string id, int start, int end) => new AnimationDto {
            id = id, clipGuid = "fixture-guid", clipLocalId = 1, clipName = id, startFrame = start, endFrame = end, speedPermille = 1000,
            sourceLengthUs = (end - start) * 33000L, trimEndUs = (end - start) * 33000L, statePaths = new List<string> { "Base Layer." + id } };
        static RuntimeSkillCatalog Build(SkillConfigDto data)
        {
            if (!SkillConfigLoader.TryParse(JsonUtility.ToJson(data), out var snapshot, out var diagnostics)) throw new Exception(string.Join("\n", diagnostics));
            if (!RuntimeSkillCatalog.TryCreateIsolated(snapshot, 33, 10000, out var result, out var error)) throw new Exception(error);
            return result;
        }
        static EntityGeometrySnapshot[] Snapshots(bool reverse = false, long y = 0)
        {
            var values = new[] { new EntityGeometrySnapshot(A, Pose(0, 0, 90000, y), new HurtCircle(5000)), new EntityGeometrySnapshot(B, Pose(5000, 0, -90000, -y), new HurtCircle(5000)) };
            return reverse ? values.Reverse().ToArray() : values;
        }
        public static void Run(Action<string> write)
        {
            output = write; checks = 0;
            Geometry(); Config(); Frames(); Ledgers(); Replay();
            output("PHASE3B_ASSERTIONS " + checks.ToString(CultureInfo.InvariantCulture));
        }
        static void Geometry()
        {
            foreach (var kind in new[] { ShapeKind.Sphere, ShapeKind.Capsule, ShapeKind.Cylinder })
            {
                Check(Hit(Box(kind), 14999, 0), kind + " interior overlap");
                Check(Hit(Box(kind), 15000, 0), kind + " closed tangent");
                Check(!Hit(Box(kind), 15001, 0), kind + " outside");
            }
            var b = Box(ShapeKind.Box);
            Check(Hit(b, 15000, 0), "Box edge full Hurt radius");
            Check(Hit(b, 13000, 14000), "Box 3-4-5 corner tangent");
            Check(!Hit(b, 13001, 14000), "Box corner outside");
            b.size.x = 10000; b.size.z = 20000; b.yawMilliDegrees = 90000;
            Check(Hit(b, 15000, 0) && !Hit(b, 0, 15000), "non-square Box rotates width and depth");
            b.yawMilliDegrees = 0; b.size.x = 11779; b.size.z = 13005;
            Check(Hit(b, 0, 6503, 1) && !Hit(b, 0, 6504, 1), "odd depth 13005 retains half unit");
            Check(Hit(b, 5890, 0, 1) && !Hit(b, 5891, 0, 1), "odd width retains half unit");
            b.offset = new Int3 { x = 10000, y = int.MinValue, z = 20000 }; b.size.x = 2000; b.size.z = 4000; b.yawMilliDegrees = 90000;
            Check(Hit(b, -10000, -15000, 1, Pose(-30000, -5000, 0)), "offset rotated by owner once, not shape yaw");
            var s = Box(ShapeKind.Sector); s.sectorAngleMilliDegrees = 180000;
            Check(Hit(s, 5000, -1000, 1000) && !Hit(s, 5000, -1001, 1000), "Sector side tangent and outside");
            Check(Hit(s, 0, 11000, 1000) && !Hit(s, 0, 11001, 1000), "Sector arc tangent and outside");
            Check(Hit(s, 10600, -800, 1000) && !Hit(s, 10601, -800, 1000), "Sector endpoint 600-800-1000 tangent");
            Check(Hit(s, 0, -1000, 1000) && !Hit(s, 0, -1001, 1000), "Sector origin boundary");
            s.sectorAngleMilliDegrees = 270000;
            Check(Hit(s, 8000, -2000, 1) && !Hit(s, 0, -8000, 1), "reflex Sector 270deg uses union, not intersection");
            s.sectorAngleMilliDegrees = 360000;
            Check(Hit(s, 0, -11000, 1000) && !Hit(s, 0, -11001, 1000), "360deg is full disk");
            s.sectorAngleMilliDegrees = 90001; s.radius = 100000000; s.height = 1;
            // Half angle is 45.0005 degrees, not truncated to 45 degrees. At r~1e8 this is hundreds of units.
            Check(Hit(s, 70711000, 70710000, 1) && !Hit(s, 70712000, 70710000, 1), "odd millidegree half angle retains .0005deg");
            b = Box(ShapeKind.Box); b.yawMilliDegrees = 349018; b.size.x = 11778; b.size.z = 13005; b.offset = new Int3 { x = -1512, y = 5358, z = 9470 };
            var p = Pose(-23456, 34567, 123000);
            Check(Hit(b, -14776, 30678, 3000, p) == Reference(b, p, Pose(-14776, 30678), new HurtCircle(3000)), "asset yaw349018/depth13005 composite transform");
            foreach (int angle in new[] { int.MinValue, -360001, -1, 0, 1, 359999, 360000, int.MaxValue })
            {
                b.yawMilliDegrees = angle;
                Check(Hit(b, 0, 0, 5000, Pose()) == Hit(b, 0, 0, 5000, Pose(0, 0, 450000)), "angle normalization " + angle);
            }
            var rng = new System.Random(302013); int compared = 0;
            for (int i = 0; i < 600; i++)
            {
                var kind = (ShapeKind)(i % 5); var box = Box(kind); box.yawMilliDegrees = rng.Next(-720000, 720000);
                box.sectorAngleMilliDegrees = rng.Next(1, 360001); box.size.x = rng.Next(1, 30000); box.size.z = rng.Next(1, 30000);
                box.offset = new Int3 { x = rng.Next(-15000, 15000), y = int.MaxValue, z = rng.Next(-15000, 15000) };
                var source = Pose(rng.Next(-30000, 30000), rng.Next(-30000, 30000), rng.Next(-720000, 720000));
                var target = Pose(rng.Next(-30000, 30000), rng.Next(-30000, 30000), rng.Next(-720000, 720000));
                var hurt = new HurtCircle(rng.Next(1, 10000), rng.Next(-5000, 5000), long.MinValue, rng.Next(-5000, 5000));
                bool actual = XzGeometry.Intersects(new AttackShape(box), source, hurt, target);
                if (actual != Reference(box, source, target, hurt)) throw new Exception("Independent geometry mismatch case " + i);
                box.offset.y = int.MinValue;
                bool yChanged = XzGeometry.Intersects(new AttackShape(box), Pose(source.X, source.Z, source.FacingMilliDegrees, long.MaxValue), new HurtCircle(hurt.Radius, hurt.OffsetX, long.MaxValue, hurt.OffsetZ), Pose(target.X, target.Z, target.FacingMilliDegrees, long.MinValue));
                if (actual != yChanged) throw new Exception("Y affected case " + i);
                compared++;
            }
            Check(compared == 600, "600 independent double-reference cases + Y invariance all five shapes");
            var huge = Box(ShapeKind.Sphere, 1000000000);
            Check(Hit(huge, 1000000000, 0, 1000000000, Pose(-1000000000, 0)), "large distance 2e9 exact tangent without int overflow");
            huge.offset.x = -1000000000;
            Check(!Hit(huge, 1000000000, 1000000000, 1, Pose(-1000000000, -1000000000)), "large transformed center and squared distance");
            Throws(() => Pose(1000000001, 0), "out-of-contract coordinate rejected");
            Throws(() => new HurtCircle(1000000001), "out-of-contract radius rejected");
            Throws(() => new HurtCircle(0), "zero radius rejected");
            Throws(() => XzGeometry.Intersects(new AttackShape(Box()), default(LogicPose), new HurtCircle(1), Pose()), "default pose rejected");
            Throws(() => XzGeometry.Intersects(new AttackShape(Box()), Pose(), default(HurtCircle), Pose()), "default Hurt rejected");
            b = Box(ShapeKind.Box); b.size.z = 0; Throws(() => new AttackShape(b), "zero Box depth rejected");
            b.size.z = 1000000001; Throws(() => new AttackShape(b), "out-of-contract full depth rejected");
            b = Box(ShapeKind.Capsule); b.height = b.radius; Throws(() => new AttackShape(b), "illegal Capsule rejected");
            b.height = b.radius * 2; Check(Hit(b, 15000, 0), "Capsule at diameter legal");
            b = Box(ShapeKind.Sector); b.sectorAngleMilliDegrees = 0; Throws(() => new AttackShape(b), "zero Sector angle rejected");
            b.sectorAngleMilliDegrees = 360001; Throws(() => new AttackShape(b), "Sector angle over360 rejected");
        }
        // Independent floating reference: ideal trig, explicit local transform and segment/arc distance.
        // Only a test oracle; never compiled into production SkillRuntime.
        static bool Reference(HitBox b, LogicPose a, LogicPose t, HurtCircle h)
        {
            double ar = (90.0 - a.FacingMilliDegrees / 1000.0) * Math.PI / 180.0;
            double tr = (90.0 - t.FacingMilliDegrees / 1000.0) * Math.PI / 180.0;
            double cx = a.X + Math.Cos(ar) * b.offset.x + Math.Sin(ar) * b.offset.z;
            double cz = a.Z - Math.Sin(ar) * b.offset.x + Math.Cos(ar) * b.offset.z;
            double dx = t.X + Math.Cos(tr) * h.OffsetX + Math.Sin(tr) * h.OffsetZ - cx;
            double dz = t.Z - Math.Sin(tr) * h.OffsetX + Math.Cos(tr) * h.OffsetZ - cz;
            double angle = ar + b.yawMilliDegrees / 1000.0 * Math.PI / 180.0;
            double x = Math.Cos(angle) * dx - Math.Sin(angle) * dz, z = Math.Sin(angle) * dx + Math.Cos(angle) * dz;
            double distance;
            if (b.shape == ShapeKind.Box) distance = Math.Sqrt(Math.Pow(Math.Max(0, Math.Abs(x) - b.size.x / 2.0), 2) + Math.Pow(Math.Max(0, Math.Abs(z) - b.size.z / 2.0), 2));
            else if (b.shape != ShapeKind.Sector || b.sectorAngleMilliDegrees == 360000) distance = Math.Max(0, Math.Sqrt(dx * dx + dz * dz) - b.radius);
            else
            {
                double half = b.sectorAngleMilliDegrees / 2000.0 * Math.PI / 180.0;
                double theta = Math.Atan2(x, z), radius = Math.Sqrt(x * x + z * z);
                distance = Math.Abs(theta) <= half ? Math.Max(0, radius - b.radius) : double.PositiveInfinity;
                foreach (double edge in new[] { -half, half })
                {
                    double ex = Math.Sin(edge) * b.radius, ez = Math.Cos(edge) * b.radius;
                    double u = Math.Max(0, Math.Min(1, (x * ex + z * ez) / (ex * ex + ez * ez)));
                    distance = Math.Min(distance, Math.Sqrt(Math.Pow(x - u * ex, 2) + Math.Pow(z - u * ez, 2)));
                }
            }
            return distance <= h.Radius;
        }
        static void Config()
        {
            var dto = Fixture(); var catalog = Build(dto); var skill = catalog.Skill("normal");
            dto.skills[0].hitBoxes[0].radius = 1; dto.skills[0].hitGroups[0].mode = HitMode.Repeat; dto.skills.Clear();
            Check(skill.Shapes.Count == 3 && skill.Group("g").Mode == HitMode.Single, "compiled catalog detached from editable DTO");
            Check(!catalog.BattleReady && catalog.ReadinessIssues.Any(i => i.Code == "HURTBOX_REQUIRED") && catalog.ReadinessIssues.Any(i => i.Code == "PARTIAL_CATALOG"), "missing formal Hurt reports readiness without blocking explicit isolated tests");
            Check(skill.Duration == 6 && skill.SegmentAt(2).Id == "clip-a" && skill.SegmentAt(3).Id == "clip-b", "D and half-open contiguous multiClip lookup");
            Throws(() => skill.SegmentAt(6), "frame D is not an animation frame");
            foreach (string mode in new[] { "gap", "overlap", "end", "duplicate-skill", "duplicate-box", "duplicate-group", "duplicate-event", "bad-ref" })
            {
                var d = Fixture(); var s = d.skills[0];
                switch (mode)
                {
                    case "gap": s.animations[1].startFrame++; break;
                    case "overlap": s.animations[1].startFrame--; break;
                    case "end": s.durationFrames++; break;
                    case "duplicate-skill": d.skills.Add(s); break;
                    case "duplicate-box": s.hitBoxes[1].id = s.hitBoxes[0].id; break;
                    case "duplicate-group": s.hitGroups[1].id = "g"; break;
                    case "duplicate-event": s.events[1].id = s.events[0].id; break;
                    case "bad-ref": s.hitBoxes[0].hitGroupId = "absent"; break;
                }
                Check(!SkillConfigLoader.TryParse(JsonUtility.ToJson(d), out _, out _), "existing validator rejects " + mode);
            }
            SkillConfigLoader.TryParse(JsonUtility.ToJson(Fixture()), out var snapshot, out _);
            Check(!RuntimeSkillCatalog.TryCreateIsolated(snapshot, 30, 10000, out _, out _), "host frame interval mismatch rejected");
            Check(!RuntimeSkillCatalog.TryCreateIsolated(snapshot, 33, 100, out _, out _), "host precision mismatch rejected");
            Check(!SkillConfigLoader.TryParse(JsonUtility.ToJson(Fixture()), out _, out _, true), "formal battle loader remains protected");
            var range = Fixture(); range.skills[0].hitBoxes[0].offset.x = int.MaxValue;
            SkillConfigLoader.TryParse(JsonUtility.ToJson(range), out var wide, out _);
            Check(!RuntimeSkillCatalog.TryCreateIsolated(wide, 33, 10000, out _, out _), "runtime capability range rejects beyond geometry contract");
        }
        static void Frames()
        {
            var skill = Build(Fixture()).Skill("normal"); var w = new SkillFrameRuntime(A, B, 99);
            Check(w.TryBeginFrame(101) == FrameBeginResult.Gap && w.LastCompletedFrame == 99, "missing next frame does not advance");
            Throws(() => w.StartAccepted(A, skill), "cast cannot start outside accepted logical frame");
            Check(w.TryBeginFrame(100) == FrameBeginResult.Started, "first contiguous frame starts");
            var a = w.StartAccepted(A, skill); var b = w.StartAccepted(B, skill);
            Check(a.Sequence == 1 && b.Sequence == 1 && !a.Equals(b) && w.Current(A).SkillFrame == 0, "mirror casts distinguished; first skill frame zero");
            w.OwnHandle(a, "motion-A");
            string before = w.DescribeState();
            Check(w.TryBeginFrame(100) == FrameBeginResult.Duplicate && w.DescribeState() == before, "duplicate open frame no mutation");
            Check(w.TryBeginFrame(101) == FrameBeginResult.PreviousFrameOpen, "cannot consume next frame before completing current");
            Throws(() => w.CompleteFrame(), "cannot finish before capture");
            w.CapturePostMove(Snapshots());
            Check(w.Events.Count == 4 && w.Events[0].Event.Id == "event-a" && w.Events[2].Cast.Equals(b), "event order local order/id then full entity/cast ownership");
            Throws(() => w.CapturePostMove(Snapshots()), "duplicate capture cannot replay event outputs");
            foreach (var c in w.Candidates) w.TryCommitAccepted(c);
            w.CompleteFrame(); before = w.DescribeState();
            Check(w.TryBeginFrame(100) == FrameBeginResult.Duplicate && w.DescribeState() == before, "consumed frame duplicate no advance/side effects");
            for (int f = 101; f <= 105; f++)
            {
                Check(w.TryBeginFrame(f) == FrameBeginResult.Started && w.Current(A).SkillFrame == f - 100, "empty-operation frame advances " + f);
                if (f == 103) Check(w.Current(A).Key.Equals(a) && w.Current(A).Segment.Id == "clip-b" && w.RecordCount == 4, "clip transition retains cast and ledger");
                w.CapturePostMove(Snapshots()); w.CompleteFrame();
            }
            Check(w.Current(A) == null && w.Current(B) == null && w.RecordCount == 0 && !w.OwnsHandle(a, "motion-A"), "D frames normal exit clears owned records/handles");
            var one = Build(Fixture(1)).Skill("normal"); var last = new SkillFrameRuntime(A, B);
            last.TryBeginFrame(1); var lc = last.StartAccepted(A, one); last.CapturePostMove(Snapshots());
            Check(last.Current(A) != null && last.TryCommitAccepted(last.Candidates[0]), "D-1 remains active through settlement");
            last.CompleteFrame();
            Check(last.Committed.Count == 1 && last.Current(A) == null && last.RecordCount == 0, "last-frame output survives normal cleanup");
            w.TryBeginFrame(106); var old = w.StartAccepted(A, skill); w.OwnHandle(old, "reused"); var newer = w.StartAccepted(A, skill); w.OwnHandle(newer, "reused");
            Check(newer.Sequence > old.Sequence && !w.Exit(old, CastExitReason.Interrupted) && w.OwnsHandle(newer, "reused"), "late old-cast exit cannot release replacement handle");
            w.CapturePostMove(Snapshots()); w.TryCommitAccepted(w.Candidates[0]); before = w.DescribeState();
            Check(!w.Exit(old, CastExitReason.Destroyed) && before == w.DescribeState(), "late repeated cleanup preserves new-cast records");
            Check(w.Exit(newer, CastExitReason.Interrupted) && !w.Exit(newer, CastExitReason.Interrupted), "deferred exit idempotent");
            Check(w.RecordCount == 1 && w.OwnsHandle(newer, "reused"), "captured frame retains ownership until explicit completion");
            Check(w.Exit(newer, CastExitReason.Death) && !w.Exit(newer, CastExitReason.Completed), "Phase3C approved exit priority: Death > Interrupted > Completed");
            w.CompleteFrame(); Check(w.RecordCount == 0 && !w.OwnsHandle(newer, "reused") && w.Committed.Count == 1, "explicit exit cleans future state but retains accepted output");
            w.TryBeginFrame(107); var shutdown = w.StartAccepted(A, skill); w.OwnHandle(shutdown, "shutdown");
            w.CapturePostMove(Snapshots()); w.TryCommitAccepted(w.Candidates[0]); w.Dispose(); before = w.DescribeState(); w.Dispose();
            Check(w.IsDisposed && w.Current(A) == null && w.RecordCount == 0 && !w.OwnsHandle(shutdown, "shutdown") && before == w.DescribeState(), "session teardown clears owned resources exactly once");
            Throws(() => w.TryBeginFrame(108), "disposed session cannot resume");
            var generation = new EntityKey(A.Session, A.Round, A.BattleId, 2);
            Check(!new CastKey(generation, 1).Equals(new CastKey(A, 1)), "respawn generation distinguishes reused battle slot");
            var moved = new SkillFrameRuntime(A, B); moved.TryBeginFrame(1); moved.StartAccepted(A, skill);
            moved.CapturePostMove(new[] { new EntityGeometrySnapshot(A, Pose(0, 0), new HurtCircle(1)), new EntityGeometrySnapshot(B, Pose(100000, 0), new HurtCircle(1)) });
            Check(moved.Candidates.Count == 0, "collector uses supplied post-move positions, no prior Transform fallback");
            Throws(() => new SkillFrameRuntime(A, new EntityKey(A.Session, A.Round, A.BattleId, 2)), "duplicate battle slot rejected even with different generation");
            var deferred = new SkillFrameRuntime(A, B); deferred.TryBeginFrame(1); var ca = deferred.StartAccepted(A, skill); var cb = deferred.StartAccepted(B, skill); deferred.CapturePostMove(Snapshots());
            deferred.Exit(cb, CastExitReason.Interrupted);
            Check(deferred.Candidates.Where(c => c.Cast.Equals(cb)).All(deferred.TryCommitAccepted), "external exit after snapshot cannot revoke that cast's already collected candidates");
            deferred.CompleteFrame();
        }
        static void Ledgers()
        {
            var skill = Build(Fixture()).Skill("normal"); var w = new SkillFrameRuntime(A, B, 99);
            w.TryBeginFrame(100); var old = w.StartAccepted(A, skill); w.StartAccepted(B, skill); w.CapturePostMove(Snapshots());
            Check(w.Candidates.Count == 4 && w.Candidates.Where(c => c.GroupId == "g").All(c => c.HitBoxId == "a-box"), "same-group dedup chooses smallest actual intersecting box; groups independent");
            string before = w.DescribeState();
            Check(w.Candidates.All(w.CanAccept) && w.Candidates.All(w.CanAccept) && before == w.DescribeState() && w.RecordCount == 0, "eligibility/repeated queries do not write ledger");
            w.CompleteFrame(); Check(w.RecordCount == 0 && w.Committed.Count == 0, "all rejected candidates consume no records");
            w.TryBeginFrame(101); w.CapturePostMove(Snapshots());
            foreach (var c in w.Candidates) { Check(w.TryCommitAccepted(c), "accepted commit " + c.Cast.Entity.BattleId + c.GroupId); Check(!w.TryCommitAccepted(c), "repeat commit rejected " + c.Cast.Entity.BattleId + c.GroupId); }
            Check(w.RecordCount == 4, "mirror targets/casts isolated records");
            var stale = w.Candidates[0]; w.CompleteFrame();
            for (int f = 102; f <= 104; f++)
            {
                w.TryBeginFrame(f); w.CapturePostMove(Snapshots());
                Check(!w.CanAccept(stale), "stale candidate rejected at " + f);
                Check(w.Candidates.Where(c => c.GroupId == "g").All(c => !w.CanAccept(c)), "Single remains consumed " + f);
                Check(w.Candidates.Where(c => c.GroupId == "r").All(c => w.CanAccept(c) == (f == 104)), "Repeat exact N=3 boundary " + f);
                w.CompleteFrame();
            }
            w.TryBeginFrame(105); var replacement = w.StartAccepted(A, skill); w.CapturePostMove(Snapshots());
            Check(replacement.Sequence == old.Sequence + 1 && w.Candidates.Where(c => c.Cast.Equals(replacement)).All(w.CanAccept), "same skill recast does not inherit history");
            w.CompleteFrame();
            var d = Fixture(); d.skills[0].hitBoxes.First(b => b.id == "a-box").offset.x = 1000000;
            var select = new SkillFrameRuntime(A, B); select.TryBeginFrame(1); select.StartAccepted(A, Build(d).Skill("normal")); select.CapturePostMove(Snapshots());
            Check(select.Candidates.First(c => c.GroupId == "g").HitBoxId == "z-box", "representative comes from intersecting boxes only");
            Check(select.Events.All(e => e.Cast.Entity.Equals(A)), "single-cast events retain owner without executing handlers");
            select.CompleteFrame();
            var noOp = new SkillFrameRuntime(A, B); noOp.TryBeginFrame(1); noOp.CapturePostMove(Snapshots()); noOp.CompleteFrame();
            noOp.TryBeginFrame(2); var first = noOp.StartAccepted(A, skill);
            Check(first.Sequence == 1 && noOp.Current(A).SkillFrame == 0, "empty frames and snapshots do not allocate cast identity");
            Throws(() => noOp.CapturePostMove(new[] { Snapshots()[0], Snapshots()[0] }), "duplicate entity snapshot rejected before mutation");
            noOp.CapturePostMove(Snapshots()); noOp.CompleteFrame();
        }
        static string RunReplay(bool reversed)
        {
            var d = Fixture(); if (reversed) { d.skills[0].hitBoxes.Reverse(); d.skills[0].hitGroups.Reverse(); d.skills[0].events.Reverse(); }
            var s = Build(d).Skill("normal"); var world = new SkillFrameRuntime(reversed ? B : A, reversed ? A : B); var lines = new List<string>();
            for (int f = 1; f <= 15; f++)
            {
                world.TryBeginFrame(f);
                if (f == 1 || f == 7) foreach (var e in reversed ? new[] { B, A } : new[] { A, B }) world.StartAccepted(e, s);
                world.CapturePostMove(Snapshots(reversed, f * 100));
                foreach (var c in reversed ? world.Candidates.Reverse() : world.Candidates.AsEnumerable()) if ((f & 1) != 0) world.TryCommitAccepted(c);
                world.CompleteFrame(); lines.Add(world.DescribeState());
            }
            return string.Join("\n---\n", lines);
        }
        static void Replay()
        {
            string canonical = RunReplay(false);
            Check(canonical == RunReplay(true), "reversed entities/boxes/events/commit enumeration canonical state identical");
            for (int i = 0; i < 25; i++) if (canonical != RunReplay((i & 1) != 0)) throw new Exception("Replay drift " + i);
            Check(true, "25 replays x15 frames exact state/output equality");
            output("PHASE3B_REPLAY_SHA256 " + SkillConfigHash.Sha256(canonical));
            var oldCulture = CultureInfo.CurrentCulture;
            try { CultureInfo.CurrentCulture = new CultureInfo("ar-SA"); Check(canonical == RunReplay(false), "state text culture invariant"); }
            finally { CultureInfo.CurrentCulture = oldCulture; }
        }
        public static void RealExport(string json, Action<string> write)
        {
            output = write;
            Check(SkillConfigLoader.TryParse(json, out var snapshot, out _), "existing Miyabi exported JSON parses without schema change");
            Check(RuntimeSkillCatalog.TryCreateIsolated(snapshot, 33, 10000, out var catalog, out _), "real export builds isolated index");
            Check(catalog.Skill("miyabi-normal-1").Duration == 20 && catalog.Skill("miyabi-normal-2").Duration == 18, "real Normal1/2 authoritative D from exported DTO");
            Check(!SkillConfigLoader.TryParse(json, out _, out _, true), "real partial export still rejected for formal battle");
            Check(!catalog.BattleReady && catalog.ReadinessIssues.Any(i => i.Code == "HURTBOX_REQUIRED"), "real missing Hurt remains explicit");
        }
        public static void Measure(Action<string> write)
        {
            long probeBefore = GC.GetAllocatedBytesForCurrentThread(); var allocationProbe = new byte[4096];
            long probeAfter = GC.GetAllocatedBytesForCurrentThread(); GC.KeepAlive(allocationProbe);
            bool allocationCounterWorks = probeAfter > probeBefore;
            var box = Box(ShapeKind.Box); box.yawMilliDegrees = 349018; box.size.z = 13005;
            var shape = new AttackShape(box); var a = Pose(-10000, 10000, 123000); var b = Pose(-9000, 11000, -12000); var hurt = new HurtCircle(5000, 100, 0, 300);
            for (int i = 0; i < 100; i++) XzGeometry.Intersects(shape, a, hurt, b);
            const int count = 4000; long memory = GC.GetAllocatedBytesForCurrentThread(); var timer = Stopwatch.StartNew(); int hits = 0;
            for (int i = 0; i < count; i++) if (XzGeometry.Intersects(shape, a, hurt, b)) hits++;
            timer.Stop(); long allocated = GC.GetAllocatedBytesForCurrentThread() - memory;
            write("PHASE3B_PERF geometry non-axis Box: calls=" + count + " ms=" + timer.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + " allocatedBytes=" + (allocationCounterWorks ? allocated.ToString(CultureInfo.InvariantCulture) : "UNAVAILABLE_counter_did_not_observe_probe") + " hits=" + hits + " (cached poses/shapes; host-specific, not battle FPS)");
            var fixture = Fixture(); foreach (var item in fixture.skills[0].hitBoxes) { item.shape = ShapeKind.Box; item.yawMilliDegrees = 349018; item.size.z = 13005; }
            var skill = Build(fixture).Skill("normal"); const int frames = 100;
            memory = GC.GetAllocatedBytesForCurrentThread(); timer.Restart();
            var world = new SkillFrameRuntime(A, B);
            for (int f = 1; f <= frames; f++) { world.TryBeginFrame(f); world.StartAccepted(A, skill); world.StartAccepted(B, skill);
                world.CapturePostMove(new[] { new EntityGeometrySnapshot(A, Pose(0, 0, 123000), new HurtCircle(5000, 100, 0, 300)), new EntityGeometrySnapshot(B, Pose(5000, 0, -12000), new HurtCircle(5000, 100, 0, 300)) });
                foreach (var c in world.Candidates) world.TryCommitAccepted(c); world.CompleteFrame(); }
            timer.Stop(); allocated = GC.GetAllocatedBytesForCurrentThread() - memory;
            write("PHASE3B_PERF 1v1 transaction 6 non-axis boxes: frames=" + frames + " ms=" + timer.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + " allocatedBytes=" + (allocationCounterWorks ? allocated.ToString(CultureInfo.InvariantCulture) : "UNAVAILABLE_counter_did_not_observe_probe") + " (includes casts, snapshots, collection, commits)");
        }
    }
}
