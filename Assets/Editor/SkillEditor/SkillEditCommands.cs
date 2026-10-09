using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SkillConfig.Editor
{
    [InitializeOnLoad]
    public static class SkillEditCommands
    {
        static readonly HashSet<Object> watched = new HashSet<Object>();
        static readonly HashSet<Object> pending = new HashSet<Object>();
        static double saveAt;
        static int gestures;
        public static event Action Changed;
        static SkillEditCommands()
        {
            Undo.undoRedoPerformed += OnUndo;
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += Flush;
            EditorApplication.quitting += Flush;
        }
        public static void Apply(Object owner, string label, Action mutation)
        {
            if (!owner || EditorApplication.isPlayingOrWillChangePlaymode) return;
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(label);
            Undo.RegisterCompleteObjectUndo(owner, label); // BEFORE any mutation, including reflow.
            mutation();
            EditorUtility.SetDirty(owner); watched.Add(owner); pending.Add(owner);
            Undo.CollapseUndoOperations(group); Invalidate();
        }
        public static void ApplyProperties(SerializedObject properties, bool reflow)
        {
            var owner = properties.targetObject;
            Apply(owner, "Edit skill configuration", () => {
                properties.ApplyModifiedPropertiesWithoutUndo();
                if (reflow && owner is ComboData combo) Reflow(combo);
            });
        }
        public static void Invalidate() { saveAt = EditorApplication.timeSinceStartup + .7; Changed?.Invoke(); }
        static void OnUndo()
        {
            foreach (var o in watched.Where(o => o)) { EditorUtility.SetDirty(o); pending.Add(o); }
            Invalidate();
        }
        public static void BeginGesture() { gestures++; }
        public static void EndGesture() { gestures = Math.Max(0, gestures - 1); }
        static void Update() { if (gestures == 0 && pending.Count > 0 && EditorApplication.timeSinceStartup >= saveAt && !EditorApplication.isPlayingOrWillChangePlaymode) Flush(); }
        public static void Flush()
        {
            foreach (var o in pending.Where(o => o).ToArray()) if (AssetDatabase.Contains(o)) AssetDatabase.SaveAssetIfDirty(o);
            pending.Clear();
        }
        public static string NewId() => Guid.NewGuid().ToString("N");
        public static SkillTrack AddTrack(ComboData owner, TrackKind kind)
        {
            var track = new SkillTrack { id = NewId(), kind = kind, name = kind.ToString(), displayOrder = owner.skill.tracks.Count };
            Apply(owner, "Create track", () => owner.skill.tracks.Add(track)); return track;
        }
        public static string AddAnimation(ComboData owner, AnimationClip clip)
        {
            var a = new AnimationSegment { id = NewId(), clip = clip, trimEndUs = clip ? (long)Math.Round(clip.length * 1000000.0) : 33000 };
            Apply(owner, "Create animation segment", () => { owner.skill.animations.Add(a); Reflow(owner); }); return a.id;
        }
        public static string AddGroup(ComboData owner)
        {
            var group = new HitGroup { id = NewId() };
            Apply(owner, "Create HitGroup", () => owner.skill.hitGroups.Add(group)); return group.id;
        }
        public static string AddItem(ComboData owner, string trackId, int frame)
        {
            var track = owner.skill.tracks.First(t => t.id == trackId); string id = NewId();
            int start = Math.Max(0, Math.Min(frame, Math.Max(0, SkillTimelineAdapters.Duration(owner) - 1)));
            Apply(owner, "Create " + track.kind, () => {
                var s = owner.skill;
                switch (track.kind)
                {
                    case TrackKind.HitBox:
                        if (s.hitGroups.Count == 0) s.hitGroups.Add(new HitGroup { id = NewId() });
                        s.hitBoxes.Add(new HitBox { id = id, trackId = trackId, hitGroupId = s.hitGroups[0].id, startFrame = start, endFrame = start + 1, size = new Int3 { x = 10000, y = 10000, z = 10000 }, radius = 5000, height = 10000, sectorAngleMilliDegrees = 90000 }); break;
                    case TrackKind.Invincibility: s.invincibility.Add(new FrameWindow { id = id, trackId = trackId, startFrame = start, endFrame = start + 1 }); break;
                    case TrackKind.Interrupt: s.interrupts.Add(new FrameWindow { id = id, trackId = trackId, startFrame = start, endFrame = start + 1 }); break;
                    case TrackKind.Vfx: s.vfx.Add(new VfxCue { id = id, trackId = trackId, startFrame = start, endFrame = start + 1, maxLifetimeFrames = 30 }); break;
                    case TrackKind.Sfx: s.sfx.Add(new SfxCue { id = id, trackId = trackId, startFrame = start, endFrame = start + 1 }); break;
                    case TrackKind.Event: s.events.Add(new SkillEvent { id = id, trackId = trackId, frame = start, eventId = "presentation.marker", parameterKind = EventParameterKind.Marker, marker = new MarkerParameters() }); break;
                }
            }); return id;
        }
        public static void DeleteItem(ComboData owner, string id) => Apply(owner, "Delete skill item", () => {
            var s = owner.skill;
            s.animations.RemoveAll(a => a.id == id); s.hitBoxes.RemoveAll(a => a.id == id);
            s.invincibility.RemoveAll(a => a.id == id); s.interrupts.RemoveAll(a => a.id == id);
            s.vfx.RemoveAll(a => a.id == id); s.sfx.RemoveAll(a => a.id == id); s.events.RemoveAll(a => a.id == id);
            Reflow(owner);
        });
        public static void DeleteTrack(ComboData owner, string id) => Apply(owner, "Delete track and items", () => {
            var s = owner.skill; s.tracks.RemoveAll(t => t.id == id);
            s.hitBoxes.RemoveAll(a => a.trackId == id); s.invincibility.RemoveAll(a => a.trackId == id); s.interrupts.RemoveAll(a => a.trackId == id);
            s.vfx.RemoveAll(a => a.trackId == id); s.sfx.RemoveAll(a => a.trackId == id); s.events.RemoveAll(a => a.trackId == id);
        });
        public static void DeleteGroup(ComboData owner, string id) => Apply(owner, "Delete HitGroup and boxes", () => {
            owner.skill.hitGroups.RemoveAll(g => g.id == id); owner.skill.hitBoxes.RemoveAll(b => b.hitGroupId == id);
        });
        // Invalid source data remains editable and is rejected by the Phase 1 validator. Never retime other tracks.
        public static void Reflow(ComboData owner)
        {
            int next = 0;
            foreach (var a in owner.skill.animations)
            {
                if (a == null) continue;
                a.startFrame = next;
                long length = a.trimEndUs - a.trimStartUs;
                long divisor = 33000L * a.speedPermille;
                long frames = length > 0 && length <= 86400000000L && divisor > 0 ? (length * 1000 + divisor - 1) / divisor : 1;
                a.endFrame = next = (int)Math.Min(100000000, (long)next + Math.Max(1, frames));
            }
        }
        public static void SetRange(ComboData owner, string id, int start, int end)
        {
            var item = SkillTimelineAdapters.Find(owner, id); if (item == null) return;
            if (start < 0 || end <= start) throw new ArgumentException("Invalid frame interval");
            Apply(owner, "Move / trim skill item", () => {
                var so = new SerializedObject(owner); var p = so.FindProperty(item.path);
                if (item.animation)
                {
                    var a = owner.skill.animations.First(x => x.id == id);
                    if (a.speedPermille <= 0) return;
                    long unit = 33000L * a.speedPermille;
                    a.trimStartUs += (start - item.start) * unit / 1000;
                    a.trimEndUs += (end - item.end) * unit / 1000;
                    if (a.clip) a.trimEndUs = Math.Min(a.trimEndUs, (long)Math.Round(a.clip.length * 1000000.0));
                    a.trimStartUs = Math.Max(0, Math.Min(a.trimStartUs, a.trimEndUs - 1));
                    Reflow(owner);
                }
                else
                {
                    p.FindPropertyRelative(item.point ? "frame" : "startFrame").intValue = start;
                    if (!item.point)
                    {
                        if (item.path.Contains(".vfx.") && item.tail) p.FindPropertyRelative("maxLifetimeFrames").intValue = end - start;
                        else p.FindPropertyRelative("endFrame").intValue = end;
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            });
        }
    }
}
