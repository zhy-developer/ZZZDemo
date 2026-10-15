using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ZZZ.SkillAuthoring;

namespace ZZZ.SkillEditor
{
    /// <summary>IMGUI 多轨时间轴。只处理选中、拖动和整数帧吸附，不采样动画或执行战斗。</summary>
    internal sealed class SkillTimelineView
    {
        private const float LabelWidth = 150, RowHeight = 28, RulerHeight = 24;
        private Vector2 scroll;
        private Row dragging;
        private int dragMode, dragStart, dragEnd, dragControl;
        private float dragMouseX;
        private bool moved;
        private int undoGroup;
        private SkillEditorSelection? revealSelection;

        public void RevealSelection(SkillEditorSelection value) => revealSelection = value;

        public void CancelDrag()
        {
            if (dragging != null && moved) Undo.CollapseUndoOperations(undoGroup);
            if (GUIUtility.hotControl == dragControl) GUIUtility.hotControl = 0;
            dragging = null;
        }

        private sealed class Row
        {
            public string Label;
            public SkillEditorSelection Selection;
            public int Start, End;
            public bool Resize, Point;
            public SkillHitRegion HitRegion;
            public Action<int, int, bool> SetRange;
        }

        public void Draw(SkillDefinition skill, SkillFrameSampler sampler, int frame, float pixelsPerFrame,
            SkillEditorSelection selection, Action<SkillEditorSelection> select, Action<int> seek, Action changed, float height)
        {
            var rows = BuildRows(skill, sampler);
            int total = Mathf.Clamp(skill.totalFrames, 1, SkillFrameUtility.MaxAuthoringFrames);
            if (revealSelection.HasValue)
            {
                var target = revealSelection.Value;
                int rowIndex = rows.FindIndex(row => row.Selection.kind == target.kind && row.Selection.index == target.index);
                if (rowIndex >= 0)
                {
                    // 同时定位轨道行和片段起点，解决目标在横向/纵向滚动区域之外的情况。
                    scroll.y = rowIndex * RowHeight;
                    scroll.x = Mathf.Max(0, Mathf.Clamp(rows[rowIndex].Start, 0, total) * pixelsPerFrame - 24);
                }
                revealSelection = null;
            }
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(height));
            Rect content = GUILayoutUtility.GetRect(LabelWidth + total * pixelsPerFrame + 30,
                RulerHeight + rows.Count * RowHeight, GUILayout.ExpandWidth(true));
            float originX = content.x + LabelWidth;
            int control = GUIUtility.GetControlID(FocusType.Passive);
            Event evt = Event.current;
            EditorGUI.DrawRect(content, EditorGUIUtility.isProSkin ? new Color(.13f, .13f, .13f) : new Color(.86f, .86f, .86f));
            GUI.Label(new Rect(content.x + 4, content.y, LabelWidth - 8, RulerHeight), "逻辑帧 / 33ms");
            int stride = Math.Max(1, Mathf.CeilToInt(48 / pixelsPerFrame));
            // 仅绘制可见刻度，大技能不会每次 Repaint 绘制上万个标签。
            int firstTick = Math.Max(0, Mathf.FloorToInt(scroll.x / pixelsPerFrame) / stride * stride - stride * 4);
            int lastTick = Math.Min(total, Mathf.CeilToInt((scroll.x + EditorGUIUtility.currentViewWidth) / pixelsPerFrame));
            for (int tick = firstTick; tick <= lastTick; tick += stride)
            {
                float x = originX + tick * pixelsPerFrame;
                EditorGUI.DrawRect(new Rect(x, content.y + RulerHeight, 1, content.height - RulerHeight), new Color(.5f, .5f, .5f, .18f));
                GUI.Label(new Rect(x + 2, content.y, 55, RulerHeight), tick.ToString(), EditorStyles.miniLabel);
            }
            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                float y = content.y + RulerHeight + i * RowHeight;
                bool selected = selection.kind == row.Selection.kind && selection.index == row.Selection.index;
                if (GUI.Button(new Rect(content.x + 2, y + 2, LabelWidth - 5, RowHeight - 4), row.Label, selected ? EditorStyles.miniButtonMid : EditorStyles.miniButton))
                    select(row.Selection);
                float startX = originX + Mathf.Clamp(row.Start, 0, total) * pixelsPerFrame;
                float endX = originX + Mathf.Clamp(row.End, 0, total) * pixelsPerFrame;
                Rect bar = new Rect(startX, y + 4, Math.Max(row.Point ? 8 : 5, endX - startX), RowHeight - 8);
                Color color = row.Selection.kind == SkillSelectionKind.Region ? new Color(.86f, .48f, .22f, selected ? 1 : .65f) : new Color(.28f, .55f, .76f, selected ? 1 : .6f);
                EditorGUI.DrawRect(bar, color);
                if (bar.width > 38) GUI.Label(bar, row.Point ? row.Start.ToString() : $"[{row.Start},{row.End})", EditorStyles.miniLabel);
                if (row.HitRegion != null && row.HitRegion.hitMode != SkillHitMode.Once)
                {
                    // 下沿短刻度表示真正检测命中的帧，与战斗和 Scene 共用时序查询。
                    int first = Math.Max(row.Start, firstTick);
                    int last = Math.Min(Math.Min(row.End, total), lastTick + 1);
                    for (int hitFrame = first; hitFrame < last; hitFrame++)
                        if (SkillHitTiming.IsHitFrame(row.HitRegion, hitFrame))
                            EditorGUI.DrawRect(new Rect(originX + hitFrame * pixelsPerFrame, bar.yMax - 7, 2, 7), Color.yellow);
                }
                if (evt.type == EventType.MouseDown && evt.button == 0 && bar.Contains(evt.mousePosition))
                {
                    select(row.Selection);
                    if (row.SetRange != null)
                    {
                        dragging = row; dragStart = row.Start; dragEnd = row.End; dragMouseX = evt.mousePosition.x;
                        dragMode = row.Resize && evt.mousePosition.x < bar.x + 6 ? -1 : row.Resize && evt.mousePosition.x > bar.xMax - 6 ? 1 : 0;
                        dragControl = control; GUIUtility.hotControl = control; moved = false;
                    }
                    evt.Use();
                }
            }
            float cursorX = originX + frame * pixelsPerFrame;
            EditorGUI.DrawRect(new Rect(cursorX, content.y, 2, content.height), new Color(1f, .35f, .25f));
            Rect ruler = new Rect(originX, content.y, total * pixelsPerFrame + 8, RulerHeight);
            if (evt.type == EventType.MouseDown && evt.button == 0 && ruler.Contains(evt.mousePosition))
            { seek(Mathf.Clamp(Mathf.RoundToInt((evt.mousePosition.x - originX) / pixelsPerFrame), 0, total)); evt.Use(); }
            if (dragging != null && GUIUtility.hotControl == dragControl)
            {
                if (evt.type == EventType.MouseDrag)
                {
                    int delta = Mathf.RoundToInt((evt.mousePosition.x - dragMouseX) / pixelsPerFrame);
                    int start = dragStart, end = dragEnd;
                    if (dragMode < 0) start = Mathf.Clamp(dragStart + delta, 0, Math.Min(total - 1, dragEnd - 1));
                    else if (dragMode > 0) end = Mathf.Clamp(dragEnd + delta, dragStart + 1, total);
                    else
                    {
                        int length = Math.Max(1, dragEnd - dragStart);
                        start = Mathf.Clamp(dragStart + delta, 0, Math.Max(0, total - length));
                        end = start + length;
                    }
                    if (!moved) { Undo.IncrementCurrentGroup(); undoGroup = Undo.GetCurrentGroup(); Undo.RegisterCompleteObjectUndo(skill, "调整技能轨道"); moved = true; }
                    dragging.SetRange(start, end, dragMode == 0);
                    EditorUtility.SetDirty(skill); changed(); evt.Use();
                }
                else if (evt.type == EventType.MouseUp || evt.type == EventType.Ignore)
                {
                    if (moved) Undo.CollapseUndoOperations(undoGroup);
                    GUIUtility.hotControl = 0; dragging = null; evt.Use();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private static List<Row> BuildRows(SkillDefinition skill, SkillFrameSampler sampler)
        {
            var rows = new List<Row>();
            if (skill.animation != null)
            {
                int end;
                try { end = SkillFrameSampler.AnimationEndFrame(skill.animation); } catch { end = skill.animation.startFrame + 1; }
                rows.Add(new Row { Label = "动画", Selection = new SkillEditorSelection(SkillSelectionKind.Animation), Start = skill.animation.startFrame,
                    End = Math.Max(skill.animation.startFrame + 1, end), SetRange = (s, e, move) => skill.animation.startFrame = s });
            }
            if (skill.motion?.settings != null)
            {
                int count = skill.motion.settings.endFrameExclusive == 0 ? sampler.MotionFrameCount : skill.motion.settings.endFrameExclusive;
                rows.Add(new Row { Label = "根位移", Selection = new SkillEditorSelection(SkillSelectionKind.Motion), Start = skill.motion.startFrame,
                    End = skill.motion.startFrame + Math.Max(1, count), SetRange = (s, e, move) => skill.motion.startFrame = s });
            }
            if (skill.hitRegions != null) for (int i = 0; i < skill.hitRegions.Count; i++)
            {
                var region = skill.hitRegions[i]; if (region == null) continue;
                rows.Add(new Row { Label = region.label, Selection = new SkillEditorSelection(SkillSelectionKind.Region, i), Start = region.startFrame,
                    End = region.endFrame, Resize = true, HitRegion = region, SetRange = (s, e, move) =>
                    {
                        int offset = s - region.startFrame;
                        // 整体移动保留帧在片段内的相对位置；缩放边界只改范围，越界帧交由配置提示报告。
                        if (move && offset != 0 && region.hitMode == SkillHitMode.Frames && region.hitFrames != null)
                            for (int n = 0; n < region.hitFrames.Length; n++)
                                region.hitFrames[n] = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, (long)region.hitFrames[n] + offset));
                        region.startFrame = s; region.endFrame = e;
                    } });
            }
            if (skill.windows != null) for (int i = 0; i < skill.windows.Count; i++)
            {
                var window = skill.windows[i]; if (window == null) continue;
                rows.Add(new Row { Label = window.kind.ToString(), Selection = new SkillEditorSelection(SkillSelectionKind.Window, i), Start = window.startFrame,
                    End = window.endFrame, Resize = true, SetRange = (s, e, move) => { window.startFrame = s; window.endFrame = e; } });
            }
            if (skill.presentation != null) for (int i = 0; i < skill.presentation.Count; i++)
            {
                var cue = skill.presentation[i]; if (cue == null) continue;
                UnityEngine.Object resource = cue.kind == SkillPresentationKind.Vfx ? (UnityEngine.Object)cue.effectPrefab : cue.audioClip;
                bool range = SkillEffectTiming.UsesFrameRange(cue);
                rows.Add(new Row { Label = cue.kind + " · " + (resource != null ? resource.name : "未绑定资源"), Selection = new SkillEditorSelection(SkillSelectionKind.Presentation, i),
                    Start = cue.frame, End = range ? cue.endFrame : cue.frame + 1, Point = !range, Resize = range,
                    SetRange = (s, e, move) => { cue.frame = s; if (range) cue.endFrame = e; } });
            }
            return rows;
        }
    }
}
