using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SkillConfig.Editor.Tests
{
    public sealed class SkillTimelineCheckWindow : EditorWindow
    {
        public ComboData owner;
        public CharacterSkillCatalog catalog;
        public readonly SkillTimelineView timeline = new SkillTimelineView();
        public string selected = "";
        public bool textEditing;
        void OnGUI()
        {
            if (!owner) return;
            bool editing = EditorGUIUtility.editingTextField;
            if (textEditing) EditorGUIUtility.editingTextField = true;
            try { timeline.Draw(owner, selected, id => selected = id, catalog); }
            finally { EditorGUIUtility.editingTextField = editing; }
        }
    }
    public static class SkillTimelineInteractionChecks
    {
        static void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL " + label); Debug.Log("PHASE2D1_GUI_PASS " + label); }
        static void Paint(SkillTimelineCheckWindow window) { window.SendEvent(new Event { type = EventType.Layout }); window.SendEvent(new Event { type = EventType.Repaint }); }
        static Dictionary<string, Rect> Bounds(SkillTimelineCheckWindow window) => (Dictionary<string, Rect>)typeof(SkillTimelineView).GetField("bounds", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window.timeline);
        static void Mouse(SkillTimelineCheckWindow window, EventType type, Vector2 point, EventModifiers modifiers = EventModifiers.None) => window.SendEvent(new Event { type = type, button = 0, mousePosition = point, modifiers = modifiers });
        static void Click(SkillTimelineCheckWindow window, Vector2 point, EventModifiers modifiers = EventModifiers.None) { Mouse(window, EventType.MouseDown, point, modifiers); Mouse(window, EventType.MouseUp, point, modifiers); Paint(window); }
        static void Key(SkillTimelineCheckWindow window, KeyCode key, bool ctrl = false) => window.SendEvent(new Event { type = EventType.KeyDown, keyCode = key, modifiers = ctrl ? EventModifiers.Control : EventModifiers.None });
        public static void Run()
        {
            if (!Application.dataPath.Replace('\\','/').Contains("/Temp/SkillEditorPhase2C/")) throw new Exception("Isolated harness only");
            var owner = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ComboData>(SkillPilotSetup.ComboPath(1)));
            owner.skill.skillId = "interaction-" + Guid.NewGuid().ToString("N");
            owner.skill.tracks.Clear(); owner.skill.hitBoxes.Clear(); owner.skill.hitGroups.Clear(); owner.skill.invincibility.Clear(); owner.skill.interrupts.Clear(); owner.skill.events.Clear(); owner.skill.vfx.Clear(); owner.skill.sfx.Clear();
            var catalog = ScriptableObject.CreateInstance<CharacterSkillCatalog>(); catalog.characterId = "interaction"; catalog.animatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(SkillPilotSetup.ControllerPath); catalog.skills.Add(owner);
            var track = SkillEditCommands.AddTrack(owner, TrackKind.Invincibility);
            var secondTrack = SkillEditCommands.AddTrack(owner, TrackKind.Invincibility);
            string a = SkillEditCommands.AddItem(owner, track.id, 1), b = SkillEditCommands.AddItem(owner, secondTrack.id, 3);
            SkillEditCommands.SetRange(owner, a, 1, 3); SkillEditCommands.SetRange(owner, b, 4, 6);
            var window = ScriptableObject.CreateInstance<SkillTimelineCheckWindow>(); window.owner = owner; window.catalog = catalog; window.position = new Rect(20,20,1200,700); window.Show();
            var state = SkillEditorWorkspaceState.instance; state.timelineScroll = Vector2.zero; state.pixelsPerFrame = 20; state.collapsed.Clear(); state.hiddenTracks.Clear();
            try
            {
                Paint(window); var centre = Bounds(window)[a].center; float offset = 0; bool located = false;
                // Convert nested scroll-view coordinates to root-window input coordinates. Locate a
                // clickable row without assuming native window chrome heights across Unity platforms.
                for (int y = 0; y <= 60 && !located; y += 2)
                {
                    Click(window, centre + Vector2.up * y);
                    if (window.timeline.Selection.Contains(a)) { offset = y + 8; located = true; }
                }
                Check(located, "mouse can select a Timeline item");
                Func<string, Vector2> point = id => Bounds(window)[id].center + Vector2.up * offset;
                Click(window, point(a)); Click(window, point(b), EventModifiers.Control);
                Check(window.timeline.Selection.Count == 2, "Ctrl click selects multiple items");
                var start = point(a); Mouse(window, EventType.MouseDown, start); Mouse(window, EventType.MouseDrag, start + Vector2.right * 40);
                Check(owner.skill.invincibility[0].startFrame == 1, "batch drag remains temporary before MouseUp");
                Mouse(window, EventType.MouseUp, start + Vector2.right * 40); Paint(window);
                Check(owner.skill.invincibility[0].startFrame == 3 && owner.skill.invincibility[1].startFrame == 6, "mouse drag commits both relative intervals: " + window.timeline.LastError);
                Key(window, KeyCode.C, true); window.timeline.SetCursor(10); Key(window, KeyCode.V, true); Paint(window);
                Check(owner.skill.invincibility.Count == 4, "Ctrl C/V pastes batch at cursor");
                window.textEditing = true; Key(window, KeyCode.Delete); window.textEditing = false;
                Check(owner.skill.invincibility.Count == 4, "Delete ignored while text editor active");
                Key(window, KeyCode.Delete); Paint(window); Check(owner.skill.invincibility.Count == 2, "Delete removes selected batch");
                Click(window, point(a)); Click(window, point(b), EventModifiers.Shift);
                Check(window.timeline.Selection.Count == 2, "Shift selects continuous range");
                var rectangleStart = new Vector2(156, Bounds(window)[a].y + offset - 2);
                var rectangleEnd = new Vector2(350, Bounds(window)[b].yMax + offset);
                Mouse(window, EventType.MouseDown, rectangleStart); Mouse(window, EventType.MouseDrag, rectangleEnd); Mouse(window, EventType.MouseUp, rectangleEnd); Paint(window);
                Check(window.timeline.Selection.Contains(a) && window.timeline.Selection.Contains(b) && window.timeline.Selection.Count == 2, "rectangle selects both rows");
                start = point(a); string before = JsonUtility.ToJson(owner.skill);
                Mouse(window, EventType.MouseDown, start); Mouse(window, EventType.MouseDrag, start + Vector2.right * 40); Key(window, KeyCode.Escape); Mouse(window, EventType.MouseUp, start + Vector2.right * 40);
                Check(JsonUtility.ToJson(owner.skill) == before, "Escape cancels batch drag");
            }
            finally { window.timeline.Cancel(); window.Close(); Object.DestroyImmediate(owner); Object.DestroyImmediate(catalog); }
        }
    }
}
