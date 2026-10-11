using System;
using System.Linq;
using System.Collections.Generic;
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
        readonly HashSet<string> selection = new HashSet<string>();
        readonly Dictionary<string, Rect> bounds = new Dictionary<string, Rect>();
        ComboData selectionOwner;
        CharacterSkillCatalog catalog;
        string anchor;
        bool marquee, additive, keyboardFocus;
        Vector2 marqueeStart;
        HashSet<string> marqueeBase;
        int moveDelta;
        public string LastError { get; private set; }
        public IReadOnlyCollection<string> Selection => selection;
        public int Cursor { get; private set; }
        public event Action<int> CursorChanged;
        public void SetCursor(int frame, bool notify = false) { int value = Math.Max(0, frame); if (value == Cursor) return; Cursor = value; if (notify) CursorChanged?.Invoke(Cursor); }
        public bool IsDragging => dragging != null || marquee;
        public void Cancel() { if (dragging != null) SkillEditCommands.EndGesture(); if (control != 0 && GUIUtility.hotControl == control) GUIUtility.hotControl = 0; control = 0; dragging = null; dragOwner = null; marquee = false; }
        void Report(string error) { LastError = error; }
        IEnumerable<SkillTimelineItem> VisibleItems(ComboData owner, SkillTimelineItem[] all)
        {
            var state = SkillEditorWorkspaceState.instance;
            string prefix = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(owner)) + ":";
            if (!state.collapsed.Contains(prefix + SkillTimelineAdapters.AnimationTrack) && !state.hiddenTracks.Contains(prefix + SkillTimelineAdapters.AnimationTrack))
                foreach (var item in all.Where(i => i.animation)) yield return item;
            foreach (var group in owner.skill.tracks.Where(t => t != null).OrderBy(t => t.displayOrder).GroupBy(t => t.group ?? ""))
            {
                if (group.Key.Length > 0 && state.collapsed.Contains(prefix + "group:" + group.Key)) continue;
                foreach (var track in group)
                {
                    if (state.hiddenTracks.Contains(prefix + track.id) || state.collapsed.Contains(prefix + track.id)) continue;
                    foreach (var item in all.Where(i => i.trackId == track.id)) yield return item;
                }
            }
        }
        void Paste(ComboData owner, Action<string> select)
        {
            var policy = PasteGroups.Copy;
            if (SkillTimelineBatch.IsCrossSkill(owner))
            {
                int choice = EditorUtility.DisplayDialogComplex("跨技能粘贴", "选择 HitGroup 策略。基础伤害使用目标 ComboData；资源 ID 使用目标角色目录。动画插入会后移后续动画，不会移动其他事件。全部数据须通过 Validator 才提交。", "复制组", "取消", "复用等价组");
                if (choice == 1) return; policy = choice == 2 ? PasteGroups.ReuseEquivalent : PasteGroups.Copy;
            }
            Report(SkillTimelineBatch.Paste(owner, catalog, Cursor, policy, out var added));
            if (LastError == null) { selection.Clear(); selection.UnionWith(added); select(added.FirstOrDefault() ?? ""); }
        }
        public void Draw(ComboData owner, string selected, Action<string> select, CharacterSkillCatalog character = null)
        {
            catalog = character;
            if (selectionOwner != owner) { Cancel(); selection.Clear(); selectionOwner = owner; anchor = null; keyboardFocus = false; LastError = null; }
            if (!string.IsNullOrEmpty(selected) && SkillTimelineAdapters.Find(owner, selected) != null && !selection.Contains(selected)) { selection.Clear(); selection.Add(selected); }
            selection.RemoveWhere(id => SkillTimelineAdapters.Find(owner, id) == null);
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
                if (GUILayout.Button("显示全部轨道", EditorStyles.toolbarButton)) { state.hiddenTracks.Clear(); state.Persist(); }
            }
            Event e = Event.current;
            bool released = e.rawType == EventType.MouseUp;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && IsDragging) { Cancel(); e.Use(); }
            if (keyboardFocus && !EditorGUIUtility.editingTextField && GUIUtility.keyboardControl == 0 && e.type == EventType.KeyDown && !IsDragging)
            {
                if ((e.control || e.command) && e.keyCode == KeyCode.C) { try { SkillTimelineBatch.Copy(owner, selection); Report(null); } catch (Exception ex) { Report(ex.Message); } e.Use(); }
                else if ((e.control || e.command) && e.keyCode == KeyCode.V) { Paste(owner, select); e.Use(); }
                else if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) { Report(SkillTimelineBatch.Delete(owner, catalog, selection.ToArray())); if (LastError == null) { selection.Clear(); select(""); } e.Use(); }
            }
            var items = SkillTimelineAdapters.Items(owner).ToArray();
            bounds.Clear();
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
                string groupKey = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(owner)) + ":group:" + group.Key;
                if (group.Key.Length > 0)
                {
                    bool open = !state.collapsed.Contains(groupKey);
                    bool next = EditorGUILayout.Foldout(open, group.Key, true);
                    if (next != open) { if (next) state.collapsed.Remove(groupKey); else state.collapsed.Add(groupKey); state.Persist(); }
                    if (!next) continue;
                }
                foreach (var track in group) DrawTrack(owner, track.id, track.name, items, width, selected, select);
            }
            // Invalid/deleted track references remain visible and recoverable, not silently hidden.
            foreach (var orphan in items.Where(i => i.trackId != SkillTimelineAdapters.AnimationTrack && !owner.skill.tracks.Any(t => t.id == i.trackId)).GroupBy(i => i.trackId))
                DrawTrack(owner, orphan.Key, "Missing track", items, width, selected, select);
            if (marquee)
            {
                var rect = Rect.MinMaxRect(Math.Min(marqueeStart.x, e.mousePosition.x), Math.Min(marqueeStart.y, e.mousePosition.y), Math.Max(marqueeStart.x, e.mousePosition.x), Math.Max(marqueeStart.y, e.mousePosition.y));
                if (e.type == EventType.MouseDrag || released)
                { selection.Clear(); if (additive) selection.UnionWith(marqueeBase); selection.UnionWith(bounds.Where(b => rect.Overlaps(b.Value)).Select(b => b.Key)); select(selection.FirstOrDefault() ?? ""); }
                EditorGUI.DrawRect(rect, new Color(.2f, .6f, 1, .2f));
                if (released) { Cancel(); e.Use(); } else if (e.type == EventType.MouseDrag) e.Use();
            }
            GUILayout.EndVertical(); EditorGUILayout.EndScrollView();
            if (dragging != null && released)
            {
                var current = SkillTimelineAdapters.Find(dragOwner, dragging.id);
                if (current != null && current.start == dragging.start && current.end == dragging.end && (dragStart != current.start || dragEnd != current.end))
                {
                    if (dragMode == 0) Report(SkillTimelineBatch.Move(dragOwner, catalog, selection.ToArray(), moveDelta));
                    else SkillEditCommands.SetRange(dragOwner, dragging.id, dragStart, dragEnd);
                }
                Cancel(); e.Use();
            }
            EditorGUILayout.LabelField("当前帧 " + Cursor + " / " + duration + " · 选中 " + selection.Count + " · Ctrl/Shift 多选，空白框选；Ctrl+C/V、Delete；Escape 取消。", EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(LastError)) EditorGUILayout.HelpBox(LastError, MessageType.Error);
        }
        void DrawTrack(ComboData owner, string track, string title, SkillTimelineItem[] all, float width, string selected, Action<string> select)
        {
            var state = SkillEditorWorkspaceState.instance; float scale = state.pixelsPerFrame;
            string foldKey = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(owner)) + ":" + track;
            if (state.hiddenTracks.Contains(foldKey)) return;
            bool collapsed = state.collapsed.Contains(foldKey);
            var items = all.Where(i => i.trackId == track).ToArray();
            Rect row = GUILayoutUtility.GetRect(width, Row * (collapsed ? 1 : Math.Max(1, items.Length + 1)));
            EditorGUI.DrawRect(row, new Color(.19f, .19f, .19f));
            bool open = EditorGUI.Foldout(new Rect(3, row.y, 17, Row), !collapsed, GUIContent.none);
            if (open == collapsed) { if (open) state.collapsed.Remove(foldKey); else state.collapsed.Add(foldKey); state.Persist(); }
            if (Event.current.type == EventType.ContextClick && new Rect(0, row.y, Header, Row).Contains(Event.current.mousePosition))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("隐藏（显示全部轨道可恢复）"), false, () => { state.hiddenTracks.Add(foldKey); state.Persist(); });
                if (track != SkillTimelineAdapters.AnimationTrack)
                {
                    menu.AddItem(new GUIContent("属性 / 重命名 / 分组"), false, () => select("track:" + track));
                    foreach (int direction in new[] { -1, 1 }) { int offset = direction; menu.AddItem(new GUIContent(direction < 0 ? "上移" : "下移"), false, () => {
                        Report(SkillTimelineBatch.ReorderTrack(owner, catalog, track, offset));
                    }); }
                }
                menu.ShowAsContext(); Event.current.Use();
            }
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
                bool bulk = dragging != null && dragMode == 0 && selection.Contains(item.id);
                int start = bulk ? item.start + moveDelta : active ? dragStart : item.start, end = bulk ? item.end + moveDelta : active ? dragEnd : item.end;
                Rect box = new Rect(Header + start * scale, row.y + (n + 1) * Row, Math.Max(8, (end - start) * scale), Row - 3);
                bounds[item.id] = box;
                var color = SkillTimelineAdapters.OutOfRange(item, duration) ? new Color(.65f, .2f, .2f) : selection.Contains(item.id) ? new Color(.22f, .55f, .7f) : new Color(.27f, .36f, .45f);
                EditorGUI.DrawRect(box, color);
                GUI.Label(box, (item.point ? "◆ " : "") + item.label + (item.tail ? " · tail" : ""), EditorStyles.whiteMiniLabel);
                if (!item.point) { EditorGUI.DrawRect(new Rect(box.x, box.y, 3, box.height), Color.gray); EditorGUI.DrawRect(new Rect(box.xMax - 3, box.y, 3, box.height), Color.gray); }
                Event e = Event.current;
                if (e.type == EventType.ContextClick && box.Contains(e.mousePosition))
                {
                    string id = item.id; var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("选中"), false, () => select(id));
                    menu.AddItem(new GUIContent("复制所选"), false, () => { try { SkillTimelineBatch.Copy(owner, selection.Contains(id) ? selection : new[] { id }); } catch (Exception ex) { Report(ex.Message); } });
                    menu.AddItem(new GUIContent("粘贴到指针"), false, () => Paste(owner, select));
                    menu.AddItem(new GUIContent("删除所选"), false, () => { Report(SkillTimelineBatch.Delete(owner, catalog, selection.Contains(id) ? selection.ToArray() : new[] { id })); if (LastError == null) { selection.Clear(); select(""); } }); menu.ShowAsContext(); e.Use();
                }
                if (e.type == EventType.MouseDown && e.button == 0 && box.Contains(e.mousePosition))
                {
                    keyboardFocus = true; GUIUtility.keyboardControl = 0;
                    if (e.control || e.command) { if (!selection.Add(item.id)) selection.Remove(item.id); anchor = item.id; select(selection.Contains(item.id) ? item.id : selection.FirstOrDefault() ?? ""); e.Use(); continue; }
                    if (e.shift && anchor != null)
                    {
                        var ordered = VisibleItems(owner, all).ToArray();
                        int a = Array.FindIndex(ordered, i => i.id == anchor), b = Array.FindIndex(ordered, i => i.id == item.id);
                        if (a >= 0 && b >= 0) { selection.Clear(); selection.UnionWith(ordered.Skip(Math.Min(a,b)).Take(Math.Abs(a-b)+1).Select(i => i.id)); }
                        else { selection.Clear(); selection.Add(item.id); }
                    }
                    else if (!selection.Contains(item.id)) { selection.Clear(); selection.Add(item.id); }
                    anchor = item.id; select(item.id);
                    dragMode = item.point ? 0 : e.mousePosition.x < box.x + 6 ? -1 : e.mousePosition.x > box.xMax - 6 ? 1 : 0;
                    if (selection.Count > 1) dragMode = 0;
                    // Animation order/position is contiguous; its body selects, edges trim source time.
                    if (!item.animation || dragMode != 0 || selection.Count > 1)
                    {
                        dragging = item; dragOwner = owner; dragStart = item.start; dragEnd = item.end; mouseStart = e.mousePosition.x;
                        moveDelta = 0;
                        SkillEditCommands.BeginGesture();
                        control = GUIUtility.GetControlID(FocusType.Passive); GUIUtility.hotControl = control;
                    }
                    e.Use();
                }
                if (active && e.type == EventType.MouseDrag)
                {
                    int delta = Mathf.RoundToInt((e.mousePosition.x - mouseStart) / scale);
                    if (dragMode == 0) { int minimum = all.Where(i => selection.Contains(i.id)).Min(i => i.start); delta = Math.Max(-minimum, delta); moveDelta = delta; dragStart = item.start + delta; dragEnd = item.end + delta; }
                    else if (dragMode < 0) dragStart = Math.Max(0, Math.Min(item.end - 1, item.start + delta));
                    else dragEnd = Math.Max(item.start + 1, item.end + delta);
                    e.Use();
                }
            }
            if (!IsDragging && Event.current.type == EventType.MouseDown && Event.current.button == 0 && row.Contains(Event.current.mousePosition) && Event.current.mousePosition.x >= Header)
            {
                keyboardFocus = true; GUIUtility.keyboardControl = 0; marquee = true; marqueeStart = Event.current.mousePosition; additive = Event.current.control || Event.current.command; marqueeBase = new HashSet<string>(selection);
                control = GUIUtility.GetControlID(FocusType.Passive); GUIUtility.hotControl = control; Event.current.Use();
            }
        }
    }
}
