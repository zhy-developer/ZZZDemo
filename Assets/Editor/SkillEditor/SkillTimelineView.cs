using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SkillConfig.Editor
{
    public sealed class SkillTimelineView
    {
        const float Header = 155, Row = 25;
        SkillTimelineItem dragging;
        ComboData dragOwner;
        int dragMode, dragStart, dragEnd, control;
        float mouseStart;
        public int Cursor { get; private set; }
        public event Action<int> CursorChanged;
        public void SetCursor(int frame, bool notify = false) { int value = Math.Max(0, frame); if (value == Cursor) return; Cursor = value; if (notify) CursorChanged?.Invoke(Cursor); }
        public bool IsDragging => dragging != null;
        public void Cancel() { if (dragging != null) SkillEditCommands.EndGesture(); if (control != 0 && GUIUtility.hotControl == control) GUIUtility.hotControl = 0; control = 0; dragging = null; dragOwner = null; }
        public void Draw(ComboData owner, string selected, Action<string> select)
        {
            var state = SkillEditorWorkspaceState.instance;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("逻辑帧 · 33 ms", GUILayout.Width(115));
                state.pixelsPerFrame = GUILayout.HorizontalSlider(state.pixelsPerFrame, 4, 50, GUILayout.Width(100));
                GUILayout.Label("缩放", GUILayout.Width(35));
                if (GUILayout.Button("◀", EditorStyles.toolbarButton)) SetCursor(Cursor - 1, true);
                SetCursor(EditorGUILayout.IntField(Cursor, GUILayout.Width(60)), true);
                if (GUILayout.Button("▶", EditorStyles.toolbarButton)) SetCursor(Cursor == int.MaxValue ? Cursor : Cursor + 1, true);
                if (GUILayout.Button("+ 数据轨道", EditorStyles.toolbarDropDown))
                {
                    var menu = new GenericMenu();
                    foreach (TrackKind k in Enum.GetValues(typeof(TrackKind))) { var kind = k; menu.AddItem(new GUIContent(kind.ToString()), false, () => select("track:" + SkillEditCommands.AddTrack(owner, kind).id)); }
                    menu.ShowAsContext();
                }
            }
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && IsDragging) { Cancel(); e.Use(); }
            var items = SkillTimelineAdapters.Items(owner).ToArray();
            int duration = SkillTimelineAdapters.Duration(owner);
            float width = (float)Math.Max(650, Header + ((double)Math.Max(duration, items.Select(i => i.end).DefaultIfEmpty(0).Max()) + 8) * state.pixelsPerFrame);
            state.timelineScroll = EditorGUILayout.BeginScrollView(state.timelineScroll);
            GUILayout.BeginVertical(GUILayout.Width(width));
            Rect ruler = GUILayoutUtility.GetRect(width, Row);
            EditorGUI.DrawRect(ruler, new Color(.13f, .13f, .13f));
            GUI.Label(new Rect(0, ruler.y, Header, Row), "Animation / Data tracks");
            int step = state.pixelsPerFrame < 9 ? 10 : state.pixelsPerFrame < 20 ? 5 : 1;
            int firstVisible = Math.Max(0, (int)((state.timelineScroll.x - Header) / state.pixelsPerFrame) / step * step);
            int lastVisible = (int)Math.Min(int.MaxValue - step, (state.timelineScroll.x + EditorGUIUtility.currentViewWidth) / state.pixelsPerFrame);
            for (int f = firstVisible; f <= lastVisible; f += step)
                GUI.Label(new Rect(Header + f * state.pixelsPerFrame, ruler.y, 50, Row), f.ToString(), EditorStyles.miniLabel);
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && ruler.Contains(e.mousePosition) && e.mousePosition.x >= Header && !IsDragging)
            { SetCursor(Mathf.RoundToInt((e.mousePosition.x - Header) / state.pixelsPerFrame), true); e.Use(); }
            DrawTrack(owner, SkillTimelineAdapters.AnimationTrack, "Animation（唯一）", items, width, selected, select);
            foreach (var group in owner.skill.tracks.Where(t => t != null).OrderBy(t => t.displayOrder).GroupBy(t => t.group ?? ""))
            {
                if (group.Key.Length > 0) GUILayout.Label(group.Key, EditorStyles.boldLabel);
                foreach (var track in group) DrawTrack(owner, track.id, track.name, items, width, selected, select);
            }
            // Invalid/deleted track references remain visible and recoverable, not silently hidden.
            foreach (var orphan in items.Where(i => i.trackId != SkillTimelineAdapters.AnimationTrack && !owner.skill.tracks.Any(t => t.id == i.trackId)).GroupBy(i => i.trackId))
                DrawTrack(owner, orphan.Key, "Missing track", items, width, selected, select);
            GUILayout.EndVertical(); EditorGUILayout.EndScrollView();
            if (IsDragging && e.rawType == EventType.MouseUp)
            {
                var current = SkillTimelineAdapters.Find(dragOwner, dragging.id);
                if (current != null && current.start == dragging.start && current.end == dragging.end && (dragStart != current.start || dragEnd != current.end))
                    SkillEditCommands.SetRange(dragOwner, dragging.id, dragStart, dragEnd);
                Cancel(); e.Use();
            }
            EditorGUILayout.LabelField("当前帧 " + Cursor + " / " + duration + " · 选中条目编辑参数；拖两端裁剪，Escape 取消。", EditorStyles.miniLabel);
        }
        void DrawTrack(ComboData owner, string track, string title, SkillTimelineItem[] all, float width, string selected, Action<string> select)
        {
            var state = SkillEditorWorkspaceState.instance; float scale = state.pixelsPerFrame;
            string foldKey = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(owner)) + ":" + track;
            bool collapsed = state.collapsed.Contains(foldKey);
            var items = all.Where(i => i.trackId == track).ToArray();
            Rect row = GUILayoutUtility.GetRect(width, Row * (collapsed ? 1 : Math.Max(1, items.Length + 1)));
            EditorGUI.DrawRect(row, new Color(.19f, .19f, .19f));
            bool open = EditorGUI.Foldout(new Rect(3, row.y, 17, Row), !collapsed, GUIContent.none);
            if (open == collapsed) { if (open) state.collapsed.Remove(foldKey); else state.collapsed.Add(foldKey); }
            if (GUI.Button(new Rect(22, row.y, Header - 50, Row), title ?? "Track", EditorStyles.label)) select(track == SkillTimelineAdapters.AnimationTrack ? "" : "track:" + track);
            if (GUI.Button(new Rect(Header - 27, row.y, 25, Row), "+"))
            {
                if (track == SkillTimelineAdapters.AnimationTrack) select(SkillEditCommands.AddAnimation(owner, null));
                else if (owner.skill.tracks.Any(t => t.id == track)) select(SkillEditCommands.AddItem(owner, track, Cursor));
            }
            int duration = SkillTimelineAdapters.Duration(owner);
            EditorGUI.DrawRect(new Rect(Header + duration * scale, row.y, 1, row.height), new Color(.8f, .4f, .2f));
            EditorGUI.DrawRect(new Rect(Header + Cursor * scale, row.y, 1, row.height), Color.yellow);
            if (collapsed) return;
            for (int n = 0; n < items.Length; n++)
            {
                var item = items[n]; bool active = dragging != null && dragging.id == item.id;
                int start = active ? dragStart : item.start, end = active ? dragEnd : item.end;
                Rect box = new Rect(Header + start * scale, row.y + (n + 1) * Row, Math.Max(8, (end - start) * scale), Row - 3);
                var color = SkillTimelineAdapters.OutOfRange(item, duration) ? new Color(.65f, .2f, .2f) : selected == item.id ? new Color(.22f, .55f, .7f) : new Color(.27f, .36f, .45f);
                EditorGUI.DrawRect(box, color);
                GUI.Label(box, (item.point ? "◆ " : "") + item.label + (item.tail ? " · tail" : ""), EditorStyles.whiteMiniLabel);
                if (!item.point) { EditorGUI.DrawRect(new Rect(box.x, box.y, 3, box.height), Color.gray); EditorGUI.DrawRect(new Rect(box.xMax - 3, box.y, 3, box.height), Color.gray); }
                Event e = Event.current;
                if (e.type == EventType.ContextClick && box.Contains(e.mousePosition))
                {
                    string id = item.id; var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("选中"), false, () => select(id));
                    menu.AddItem(new GUIContent("删除"), false, () => { SkillEditCommands.DeleteItem(owner, id); select(""); }); menu.ShowAsContext(); e.Use();
                }
                if (e.type == EventType.MouseDown && e.button == 0 && box.Contains(e.mousePosition))
                {
                    select(item.id);
                    dragMode = item.point ? 0 : e.mousePosition.x < box.x + 6 ? -1 : e.mousePosition.x > box.xMax - 6 ? 1 : 0;
                    // Animation order/position is contiguous; its body selects, edges trim source time.
                    if (!item.animation || dragMode != 0)
                    {
                        dragging = item; dragOwner = owner; dragStart = item.start; dragEnd = item.end; mouseStart = e.mousePosition.x;
                        SkillEditCommands.BeginGesture();
                        control = GUIUtility.GetControlID(FocusType.Passive); GUIUtility.hotControl = control;
                    }
                    e.Use();
                }
                if (active && e.type == EventType.MouseDrag)
                {
                    int delta = Mathf.RoundToInt((e.mousePosition.x - mouseStart) / scale);
                    if (dragMode == 0) { delta = Math.Max(-item.start, delta); dragStart = item.start + delta; dragEnd = item.end + delta; }
                    else if (dragMode < 0) dragStart = Math.Max(0, Math.Min(item.end - 1, item.start + delta));
                    else dragEnd = Math.Max(item.start + 1, item.end + delta);
                    e.Use();
                }
            }
        }
    }
}
