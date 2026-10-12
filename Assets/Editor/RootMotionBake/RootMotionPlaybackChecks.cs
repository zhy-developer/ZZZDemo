using System;
using FrameSync.RootMotion;
using UnityEditor;
using UnityEngine;

public static class RootMotionPlaybackChecks
{
    [MenuItem("Tools/Root Motion/Verify Playback Integration")]
    public static void Run()
    {
        // Motion is now owned by the pure simulation, never by RoleBase or an animation state.
        var info = AssetDatabase.LoadAssetAtPath<ZZZ.PlayerSO>("Assets/ScriptableObject/PlayerInfo/PlayerInfo（星见雅）.asset");
        var settings = info.movementData.dashData.frontRootMotion;
        if (settings == null || !settings.TryGetClip(out var clip)) throw new InvalidOperationException("Missing baked motion.");
        var first = new RootMotionPlayback(); var replay = new RootMotionPlayback();
        if (!first.TryStart(clip, settings.endFrameExclusive, settings.distancePermille, 0, 100, 100) ||
            !replay.TryStart(clip, settings.endFrameExclusive, settings.distancePermille, 0, 100, 100)) throw new InvalidOperationException("Invalid motion binding.");
        int samples = 0;
        while (first.TryAdvance(out int x, out int z))
        {
            if (!replay.TryAdvance(out int rx, out int rz) || x != rx || z != rz) throw new InvalidOperationException("Motion replay differs.");
            samples++;
        }
        if (samples == 0 || replay.IsPlaying) throw new InvalidOperationException("Motion did not finish.");
        Debug.Log("[RootMotion] Pure playback replay passed: " + samples + " samples; no role/Animator authority.");
    }
}
