using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameSync.RootMotion
{
    /// <summary>Reusable binding for dodges, attacks or skills. Source clips are baked at playback speed 1.</summary>
    [Serializable]
    public sealed class RootMotionSettings
    {
        [Tooltip("RootMotionBakerWindow exported JSON. Empty keeps the action's original behavior.")]
        public TextAsset json;
        [Min(0), Tooltip("Exclusive end frame; 0 uses the entire clip. One sample is one logical tick.")]
        public int endFrameExclusive;
        [Range(1, 10000), Tooltip("1000 = original distance. Does not change playback duration.")]
        public int distancePermille = 1000;

        private static readonly Dictionary<TextAsset, RootMotionClip> Clips = new Dictionary<TextAsset, RootMotionClip>();
        private static readonly HashSet<TextAsset> InvalidClips = new HashSet<TextAsset>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearCache() { Clips.Clear(); InvalidClips.Clear(); }

        public bool TryGetClip(out RootMotionClip clip)
        {
            clip = null;
            if (json == null || InvalidClips.Contains(json)) return false;
            if (Clips.TryGetValue(json, out clip)) return true;
            string error;
            try
            {
                var data = JsonUtility.FromJson<RootMotionJsonData>(json.text);
                if (RootMotionClip.TryCreate(data, NetConfig.frameTime, ToolMethod.Render2LogicScale, out clip, out error))
                {
                    Clips.Add(json, clip);
                    return true;
                }
            }
            catch (Exception exception) { error = exception.Message; }
            InvalidClips.Add(json);
            Debug.LogError("Cannot load root motion '" + json.name + "': " + error, json);
            return false;
        }
    }
}
