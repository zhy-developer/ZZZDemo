using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FrameSync.RootMotion;
using SkillConfig;
using SkillRuntime;
using UnityEngine;

namespace SkillRuntimeChecks
{
    public static class Phase4BChecks
    {
        static int count;
        static Action<string> log;
        static void Check(bool ok, string name) { if (!ok) throw new Exception(name); count++; log("PHASE4B_PASS " + name); }
        static void Throws(Action action, string name) { bool threw = false; try { action(); } catch (ArgumentException) { threw = true; } Check(threw, name); }
        static RootMotionJsonData Table()
        {
            return new RootMotionJsonData { clipName = "fixture", logicFrameIntervalMs = 33, precision = 10000, clipLengthMs = 80, rootMotionEndTimeMs = 80, frameCount = 3, totalX = -11, totalZ = 29,
                frames = new List<RootMotionJsonFrame> {
                    new RootMotionJsonFrame { logicFrame = 0, sampleEndTimeMs = 33, sampleDurationMs = 33, deltaX = -3, deltaZ = 7, positionX = -3, positionZ = 7 },
                    new RootMotionJsonFrame { logicFrame = 1, sampleEndTimeMs = 66, sampleDurationMs = 33, deltaX = -5, deltaZ = 9, positionX = -8, positionZ = 16 },
                    new RootMotionJsonFrame { logicFrame = 2, sampleEndTimeMs = 80, sampleDurationMs = 14, deltaX = -3, deltaZ = 13, positionX = -11, positionZ = 29 } } };
        }
        static AnimationDto Binding(RootMotionJsonData table, int speed = 1000, long trimStart = 0, long trimEnd = -1)
        {
            if (trimEnd < 0) trimEnd = table.clipLengthMs * 1000L;
            return new AnimationDto { id = "a", clipName = table.clipName, sourceLengthUs = table.clipLengthMs * 1000L, trimStartUs = trimStart, trimEndUs = trimEnd, speedPermille = speed,
                endFrame = (int)(((trimEnd - trimStart) * 1000 + 33000L * speed - 1) / (33000L * speed)), rootMotionBound = true,
                rootMotion = new MotionBindingDto { clipName = table.clipName, sourceClipLengthMs = table.clipLengthMs, coverageEndMs = table.rootMotionEndTimeMs,
                    sampleCount = table.frameCount, distancePermille = 1000, requiresTimeRemap = speed != 1000 || trimStart != 0 || trimEnd != table.clipLengthMs * 1000L } };
        }
        static decimal Reference(RootMotionJsonData table, decimal us, bool x)
        {
            decimal result = 0;
            foreach (var f in table.frames)
            {
                decimal start = (f.sampleEndTimeMs - f.sampleDurationMs) * 1000m;
                decimal amount = Math.Min(1m, Math.Max(0m, (us - start) / (f.sampleDurationMs * 1000m)));
                result += amount * (x ? f.deltaX : f.deltaZ);
            }
            return result;
        }
        public static void Run(Action<string> output, string repo)
        {
            log = output; count = 0;
            var data = Table();
            foreach (int speed in new[] { 500, 1000, 1500, 1753 })
            {
                var a = Binding(data, speed, 5000, 73501);
                var time = new SkillClipTime(a); var timeline = new SkillMotionTimeline(new[] { new SkillMotionSegment(a, data) });
                bool equal = true;
                for (int f = 0; f <= a.endFrame; f++)
                {
                    decimal us = Math.Min(a.trimEndUs, a.trimStartUs + f * 33000m * speed / 1000m);
                    var p = timeline.Position(f, 0, 100, 100);
                    equal &= p.X == (long)(Reference(data, us, true) - Reference(data, a.trimStartUs, true));
                    equal &= p.Z == (long)(Reference(data, us, false) - Reference(data, a.trimStartUs, false));
                    equal &= time.SourceNs(f) == us * 1000;
                }
                Check(equal, "Trim/speed/fractional-source matches independent decimal piecewise reference speed=" + speed);
            }
            var identity = Binding(data); var motion = new SkillMotionTimeline(new[] { new SkillMotionSegment(identity, data) });
            foreach (int scale in new[] { 1, 333, 1000, 10000 }) foreach (int facingX in new[] { -100, -73, 0, 100 })
            {
                var a = Binding(data); a.rootMotion.distancePermille = scale;
                var mapped = new SkillMotionTimeline(new[] { new SkillMotionSegment(a, data) });
                RootMotionClip.TryCreate(data, 33, 10000, out var old, out _); var player = new RootMotionPlayback(); player.TryStart(old, 0, scale, facingX, 67, 100);
                long x = 0, z = 0; bool same = true;
                for (int k = 0; k < data.frameCount; k++) { player.TryAdvance(out int dx, out int dz); x += dx; z += dz; var p = mapped.Position(k + 1, facingX, 67, 100); same &= p.X == x && p.Z == z; }
                Check(same, "Identity every frame/partial last frame/negative rotation scale=" + scale + " facing=" + facingX);
            }
            foreach (var path in Directory.GetFiles(Path.Combine(repo, "Assets/Resources/RootMotion"), "*_Attack_*_RootMotion.json"))
            {
                var table = JsonUtility.FromJson<RootMotionJsonData>(File.ReadAllText(path)); var a = Binding(table);
                var mapped = new SkillMotionTimeline(new[] { new SkillMotionSegment(a, table) });
                RootMotionClip.TryCreate(table, 33, 10000, out var old, out _); var player = new RootMotionPlayback(); player.TryStart(old, 0, 1000, 73, -68, 100);
                long x = 0, z = 0; bool same = true;
                for (int k = 0; k < a.endFrame; k++) { if (player.TryAdvance(out int dx, out int dz)) { x += dx; z += dz; } var p = mapped.Position(k + 1, 73, -68, 100); same &= x == p.X && z == p.Z; }
                Check(same, "Actual bake identity per-frame " + table.clipName);
            }
            var second = Binding(data); second.id = "b"; second.startFrame = identity.endFrame; second.endFrame += second.startFrame;
            var multi = new SkillMotionTimeline(new[] { new SkillMotionSegment(identity, data), new SkillMotionSegment(second, data) });
            var end = multi.Position(multi.Duration, 0, 100, 100);
            Check(end.X == -22 && end.Z == 58 && multi.Position(3, 0, 100, 100).Z == 29, "Two clips preserve cumulative offset at seam, no reset/double delta");
            long sumX = 0, sumZ = 0;
            for (int f = 0; f < multi.Duration; f++) { var p = multi.Position(f, 73, -68, 100); var q = multi.Position(f + 1, 73, -68, 100); sumX += q.X - p.X; sumZ += q.Z - p.Z; }
            var seek = multi.Position(multi.Duration, 73, -68, 100);
            Check(sumX == seek.X && sumZ == seek.Z, "Sequential increments equal direct seek and preserve rounding remainder");
            var tail = Binding(data); tail.sourceLengthUs = tail.trimEndUs = 132000; tail.endFrame = 4;
            Throws(() => new SkillMotionSegment(tail, data), "Unknown uncovered tail rejects runtime sampling");
            tail.rootMotion.tailPolicy = MotionTailPolicy.HoldAtCoverageEnd;
            var stationary = new SkillMotionTimeline(new[] { new SkillMotionSegment(tail, data) });
            Check(stationary.Position(4, 0, 100, 100).Z == 29, "Explicit stationary tail holds position");
            var limited = Binding(data, 1500); limited.rootMotion.endFrameExclusive = 1; limited.rootMotion.coverageEndMs = 33;
            limited.rootMotion.tailPolicy = MotionTailPolicy.HoldAtCoverageEnd;
            var limitedMotion = new SkillMotionTimeline(new[] { new SkillMotionSegment(limited, data) });
            Check(limitedMotion.Position(limitedMotion.Duration, 0, 100, 100).X == -3 && limitedMotion.Position(limitedMotion.Duration, 0, 100, 100).Z == 7, "Explicit partial bake endpoint validates effective coverage and keeps nonzero tail");
            var box = new HitBox { id = "outline-box", hitGroupId = "g", endFrame = 1, shape = ShapeKind.Box, size = new Int3 { x = 3, y = 1, z = 13005 }, offset = new Int3 { x = 25, y = -1000, z = -37 }, yawMilliDegrees = 349018 };
            var outline = XzOutline.Attack(box, new LogicPose(-55, 9000, 76, 90000));
            double radians = 349018 / 1000.0 * Math.PI / 180;
            double ex = -55 + 25 - 1.5 * Math.Cos(radians) - 6502.5 * Math.Sin(radians);
            double ez = 76 - 37 + 1.5 * Math.Sin(radians) - 6502.5 * Math.Cos(radians);
            Check(outline.Count == 5 && Math.Abs(outline[0].X - ex) < 1e-7 && Math.Abs(outline[0].Z - ez) < 1e-7, "XZ outline shares rational non-axis rotation and odd half-size, independent trig reference");
            box.offset.y = 999999; var otherOutline = XzOutline.Attack(box, new LogicPose(-55, -9000, 76, 90000));
            Check(outline.Zip(otherOutline, (p,q) => p.X == q.X && p.Z == q.Z).All(v => v), "XZ visualization ignores both pose and offset Y exactly");
            var invalid = Binding(data); invalid.speedPermille = 0; Throws(() => new SkillClipTime(invalid), "Zero speed rejected");
            invalid = Binding(data); invalid.speedPermille = 100001; Throws(() => new SkillClipTime(invalid), "Schema speed upper bound retained");
            Throws(() => motion.Position(-1, 0, 100, 100), "Negative seek rejected");
            Throws(() => motion.Position(4, 0, 100, 100), "Out-of-duration seek rejected");
            second.startFrame++; second.endFrame++; Throws(() => new SkillMotionTimeline(new[] { new SkillMotionSegment(identity, data), new SkillMotionSegment(second, data) }), "Clip gap rejected");
            second.startFrame -= 2; second.endFrame -= 2; Throws(() => new SkillMotionTimeline(new[] { new SkillMotionSegment(identity, data), new SkillMotionSegment(second, data) }), "Clip overlap rejected");
            var sw = Stopwatch.StartNew(); long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) motion.Position(i % 4, 73, -68, 100);
            log("PHASE4B_MOTION_PERF queries=10000 ms=" + sw.ElapsedMilliseconds + " bytes=" + (GC.GetAllocatedBytesForCurrentThread() - before));
            log("PHASE4B_MOTION_COMPLETE checks=" + count + " environment=standalone-or-explicit-Unity-host");
        }
    }
}
