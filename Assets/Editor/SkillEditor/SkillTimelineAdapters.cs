using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SkillConfig.Editor
{
    // View descriptors reference authoring objects; they are never persisted or exported.
    public sealed class SkillTimelineItem
    {
        public string id, trackId, path, label;
        public int start, end;
        public bool point, tail, animation;
    }
    public static class SkillTimelineAdapters
    {
        public const string AnimationTrack = "@animation";
        public static int Duration(ComboData owner) => owner?.skill?.animations?.Where(a => a != null).Select(a => a.endFrame).DefaultIfEmpty(0).Max() ?? 0;
        public static IEnumerable<SkillTimelineItem> Items(ComboData owner)
        {
            var s = owner?.skill; if (s == null) yield break;
            for (int i = 0; i < s.animations.Count; i++) { var a = s.animations[i]; if (a != null) yield return Item(a.id, AnimationTrack, "animations", i, a.startFrame, a.endFrame, a.clip ? a.clip.name : "Missing Clip", animation: true); }
            for (int i = 0; i < s.hitBoxes.Count; i++) { var a = s.hitBoxes[i]; if (a != null) yield return Item(a.id, a.trackId, "hitBoxes", i, a.startFrame, a.endFrame, a.shape + " / " + a.hitGroupId); }
            for (int i = 0; i < s.invincibility.Count; i++) { var a = s.invincibility[i]; if (a != null) yield return Item(a.id, a.trackId, "invincibility", i, a.startFrame, a.endFrame, "Invincibility"); }
            for (int i = 0; i < s.interrupts.Count; i++) { var a = s.interrupts[i]; if (a != null) yield return Item(a.id, a.trackId, "interrupts", i, a.startFrame, a.endFrame, "Interrupt"); }
            for (int i = 0; i < s.vfx.Count; i++) { var a = s.vfx[i]; if (a != null) yield return Item(a.id, a.trackId, "vfx", i, a.startFrame, a.lifetime == VfxLifetime.AutoComplete ? a.startFrame + Math.Max(1, a.maxLifetimeFrames) : a.endFrame, a.resourceId ?? "VFX", tail: a.lifetime == VfxLifetime.AutoComplete); }
            for (int i = 0; i < s.sfx.Count; i++) { var a = s.sfx[i]; if (a != null) yield return Item(a.id, a.trackId, "sfx", i, a.startFrame, a.endFrame, a.resourceId ?? "SFX", tail: true); }
            for (int i = 0; i < s.events.Count; i++) { var a = s.events[i]; if (a != null) yield return Item(a.id, a.trackId, "events", i, a.frame, a.frame + 1, a.eventId, point: true); }
        }
        static SkillTimelineItem Item(string id, string track, string list, int index, int start, int end, string label, bool point = false, bool tail = false, bool animation = false) =>
            new SkillTimelineItem { id = id, trackId = track, path = "skill." + list + ".Array.data[" + index + "]", label = label, start = start, end = end, point = point, tail = tail, animation = animation };
        public static SkillTimelineItem Find(ComboData owner, string id) => Items(owner).FirstOrDefault(i => i.id == id);
        public static bool OutOfRange(SkillTimelineItem item, int duration) => item.start < 0 || item.start >= duration || (!item.point && (item.end <= item.start || (!item.tail && item.end > duration)));
    }

    // Phase 2C boundary only. Phase 2B does not instantiate players, evaluate animation or execute events.
    public interface ISkillPreviewSession : IDisposable
    {
        void Seek(int logicFrame);
        void Invalidate();
    }
}
