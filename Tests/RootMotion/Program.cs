using System.Text.Json;
using FrameSync.RootMotion;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    passed++;
}
RootMotionJsonData Make(params (int x, int z)[] steps)
{
    var data = new RootMotionJsonData { logicFrameIntervalMs = 33, precision = 10000,
        clipLengthMs = steps.Length * 33, rootMotionEndTimeMs = steps.Length * 33, frameCount = steps.Length };
    int x = 0, z = 0;
    for (int i = 0; i < steps.Length; i++)
    {
        x += steps[i].x; z += steps[i].z;
        data.frames.Add(new RootMotionJsonFrame { logicFrame = i, sampleEndTimeMs = (i + 1) * 33, sampleDurationMs = 33,
            deltaX = steps[i].x, deltaZ = steps[i].z, positionX = x, positionZ = z });
    }
    data.totalX = x; data.totalZ = z;
    return data;
}
RootMotionClip Clip(RootMotionJsonData data)
{
    Check(RootMotionClip.TryCreate(data, 33, 10000, out var clip, out var error), error);
    return clip;
}
void Reject(RootMotionJsonData data, string name) => Check(
    !RootMotionClip.TryCreate(data, 33, 10000, out _, out _), name);

var source = Make((10, 100), (-10, 200));
var clip = Clip(source);
var playback = new RootMotionPlayback();
Check(playback.TryStart(clip, 0, 1000, 0, 100, 100), "Start full clip facing +Z");
Check(playback.TryAdvance(out int dx, out int dz) && dx == 10 && dz == 100 && playback.IsPlaying,
    "Consume precisely one frame");
Check(playback.TryAdvance(out dx, out dz) && dx == -10 && dz == 200 && !playback.IsPlaying,
    "Last sample is applied and completes immediately");
Check(!playback.TryAdvance(out _, out _), "No extra frame after completion");
Check(playback.TryStart(clip, 1, 1000, 100, 0, 100), "Start cutoff facing +X");
Check(playback.TryAdvance(out dx, out dz) && dx == 100 && dz == -10 && !playback.IsPlaying,
    "Local right maps to -Z when facing +X; cutoff is exclusive");
Check(playback.TryStart(clip, 0, 1000, 0, -100, 100), "Face -Z");
Check(playback.TryAdvance(out dx, out dz) && dx == -10 && dz == -100, "Rotate negative facing");
playback.Stop();
Check(!playback.TryAdvance(out _, out _), "Cancellation discards remaining displacement");
playback.TryStart(clip, 0, 1000, 0, 100, 100);
playback.TryAdvance(out _, out _);
playback.TryStart(clip, 0, 1000, 0, 100, 100);
Check(playback.TryAdvance(out dx, out dz) && dx == 10 && dz == 100, "Replacement restarts at frame zero");
Check(!playback.TryStart(clip, 3, 1000, 0, 100, 100) && playback.IsPlaying,
    "Invalid replacement preserves current playback");

var fractional = Clip(Make((0, 1), (0, 1), (0, 1), (0, 1)));
playback.TryStart(fractional, 0, 500, 60, 80, 100);
int totalX = 0, totalZ = 0;
while (playback.TryAdvance(out dx, out dz)) { totalX += dx; totalZ += dz; }
Check(totalX == 1 && totalZ == 1, "Scale and rotation retain cumulative fractional displacement");

source.frames[0].deltaZ = 9999;
var other = new RootMotionPlayback();
other.TryStart(clip, 0, 1000, 0, 100, 100);
Check(other.TryAdvance(out _, out dz) && dz == 100, "Validated clip owns an immutable copy");
playback.Stop();
Check(other.TryAdvance(out _, out dz) && dz == 200, "Each role has an independent cursor");

Reject(null, "Null data");
Reject(Make(), "Empty data");
var bad = Make((0, 1)); bad.logicFrameIntervalMs = 20; Reject(bad, "Wrong tick interval");
bad = Make((0, 1)); bad.precision = 100; Reject(bad, "Wrong units");
bad = Make((0, 1)); bad.frameCount = 2; Reject(bad, "Wrong count");
bad = Make((0, 1)); bad.frames[0].positionZ = 10; Reject(bad, "Wrong cumulative position");
bad = Make((0, 1)); bad.frames[0].sampleEndTimeMs = 32; Reject(bad, "Wrong sample time");
bad = Make((0, 1)); bad.frames[0].logicFrame = 1; Reject(bad, "Wrong frame index");
bad = Make((0, 1)); bad.totalZ = 5; Reject(bad, "Wrong total");
bad = Make((0, 1)); bad.frames[0].sampleDurationMs = 20; Reject(bad, "Wrong sample duration");

var baked = JsonSerializer.Deserialize<RootMotionJsonData>(File.ReadAllText(
    Path.Combine(AppContext.BaseDirectory, "dodge.json")), new JsonSerializerOptions { IncludeFields = true });
var bakedClip = Clip(baked);
playback.TryStart(bakedClip, 10, 1000, 0, 100, 100);
totalX = totalZ = 0; int count = 0;
while (playback.TryAdvance(out dx, out dz)) { totalX += dx; totalZ += dz; count++; }
Check(count == 10 && totalX == 126 && totalZ == 34595, "Optional real dodge cutoff");
playback.TryStart(bakedClip, 0, 1000, 0, 100, 100);
totalZ = 0; count = 0;
while (playback.TryAdvance(out dx, out dz)) { totalZ += dz; count++; }
Check(count == 23 && totalZ == 59191, "Full real clip including short final interval");
Console.WriteLine($"PASS: {passed} root motion checks");
