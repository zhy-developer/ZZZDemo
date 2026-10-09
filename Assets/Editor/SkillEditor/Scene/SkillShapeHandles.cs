using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SkillConfig.Editor
{
    public sealed class SkillShapeHandles : IDisposable
    {
        readonly SkillShapeEdit edit = new SkillShapeEdit();
        string selection;
        int hotControl;
        public void Cancel()
        {
            if (hotControl != 0 && GUIUtility.hotControl == hotControl) GUIUtility.hotControl = 0;
            hotControl = 0; edit.Cancel();
        }
        public void Dispose() => Cancel();
        public void Draw(SkillPreviewSession session, string selected, bool editHurt)
        {
            if (session == null || session.IsDisposed || !session.Stage || StageUtility.GetCurrentStage() != session.Stage) { Cancel(); return; }
            string key = editHurt ? "@hurt" : selected;
            if (selection != key) { Cancel(); selection = key; }
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && edit.Active) { Cancel(); e.Use(); }
            if (!string.IsNullOrEmpty(session.Error)) { Cancel(); Handles.Label(Vector3.zero, session.Error); return; }
            DrawTrajectory(session);
            using (new Handles.DrawingScope(Matrix4x4.TRS(session.Proxy.Root.transform.position, Quaternion.identity, Vector3.one)))
            {
                if (!session.Interrupted)
                {
                    foreach (var box in session.Owner.skill.hitBoxes.Where(b => b != null))
                    {
                        bool active = session.Active(box), picked = !editHurt && box.id == selected;
                        if (!active && !picked) continue;
                        var value = picked && edit.Box != null ? edit.Box : box;
                        using (new Handles.DrawingScope(active ? picked ? Color.yellow : Color.red : Color.gray)) DrawShape(value);
                        if (picked) DrawEditor(session, value, false);
                    }
                }
                if (session.Catalog && session.Catalog.hurtBoxConfigured && session.Catalog.hurtBox != null)
                {
                    var h = editHurt && edit.Hurt != null ? edit.Hurt : session.Catalog.hurtBox;
                    var box = new HitBox { shape = ShapeKind.Capsule, offset = h.offset, radius = h.radius, height = h.height };
                    using (new Handles.DrawingScope(Color.cyan)) DrawShape(box);
                    if (editHurt) DrawEditor(session, box, true);
                }
                Handles.Label(Vector3.up * 2.2f, session.Interrupted ? "Interrupted @ " + session.Frame : "Frame " + session.Frame + " · " + session.ClipName + " @ " + session.SourceSeconds.ToString("F6") + "s");
            }
            // Native Handles consume MouseUp: rawType preserves the gesture's commit boundary.
            if (edit.Active && e.rawType == EventType.MouseUp) { hotControl = 0; edit.Commit(); SceneView.RepaintAll(); }
            else if (edit.Active && e.type == EventType.Layout && GUIUtility.hotControl == 0) Cancel();
        }
        void DrawEditor(SkillPreviewSession session, HitBox box, bool hurt)
        {
            if (!Valid(box)) { Handles.Label(SkillShapeEdit.World(box.offset), "Invalid geometry: repair in Inspector"); return; }
            var p = SkillShapeEdit.World(box.offset); var size = SkillShapeEdit.World(box.size);
            float radius = box.radius / 10000f, height = box.height / 10000f, yaw = box.yawMilliDegrees / 1000f, angle = box.sectorAngleMilliDegrees / 1000f;
            var rotation = Quaternion.Euler(0, yaw, 0); float handleSize = HandleUtility.GetHandleSize(p);
            EditorGUI.BeginChangeCheck();
            p = Handles.PositionHandle(p, Quaternion.identity);
            if (box.shape == ShapeKind.Box)
            {
                size = Handles.ScaleHandle(size, p, rotation, handleSize);
                yaw = Handles.Disc(rotation, p, Vector3.up, handleSize * .8f, false, 0).eulerAngles.y;
            }
            else
            {
                radius = Handles.RadiusHandle(Quaternion.identity, p, radius);
                if (box.shape != ShapeKind.Sphere) height = Handles.ScaleSlider(height, p + Vector3.up * height * .5f, Vector3.up, Quaternion.identity, handleSize, .0001f);
                if (box.shape == ShapeKind.Sector)
                {
                    yaw = Handles.Disc(rotation, p, Vector3.up, Math.Max(radius, handleSize), false, 0).eulerAngles.y;
                    var edge = Quaternion.Euler(0, yaw + angle * .5f, 0) * Vector3.forward;
                    angle = Handles.ScaleSlider(angle, p + edge * radius, Vector3.Cross(Vector3.up, edge), Quaternion.identity, handleSize, .001f);
                }
            }
            if (!EditorGUI.EndChangeCheck()) return;
            session.SetPlaying(false);
            if (!edit.Active) { if (hurt) edit.BeginHurt(session.Catalog); else edit.BeginBox(session.Owner, box.id); }
            hotControl = GUIUtility.hotControl;
            try
            {
                if (hurt)
                {
                    edit.Hurt.offset = SkillShapeEdit.Quantize(p); edit.Hurt.radius = Math.Max(1, SkillShapeEdit.Quantize(radius));
                    edit.Hurt.height = Math.Max(checked(2 * edit.Hurt.radius), SkillShapeEdit.Quantize(height));
                }
                else
                {
                    edit.Box.offset = SkillShapeEdit.Quantize(p); edit.Box.size = SkillShapeEdit.Quantize(Vector3.Max(size, Vector3.one * .0001f));
                    edit.Box.radius = Math.Max(1, SkillShapeEdit.Quantize(radius)); edit.Box.height = Math.Max(1, SkillShapeEdit.Quantize(height));
                    if (box.shape == ShapeKind.Capsule) edit.Box.height = Math.Max(checked(2 * edit.Box.radius), edit.Box.height);
                    edit.Box.yawMilliDegrees = SkillShapeEdit.Quantize(yaw, 1000); edit.Box.sectorAngleMilliDegrees = SkillShapeEdit.Quantize(Mathf.Clamp(angle, .001f, 360), 1000);
                }
            }
            catch (Exception ex) { Cancel(); Debug.LogWarning("Shape edit cancelled: " + ex.Message); }
            SceneView.RepaintAll();
        }
        static bool Valid(HitBox b) => b.shape == ShapeKind.Box ? b.size.x > 0 && b.size.y > 0 && b.size.z > 0 : b.radius > 0 && (b.shape == ShapeKind.Sphere || (b.height > 0 && (b.shape != ShapeKind.Capsule || b.height >= 2L * b.radius))) && (b.shape != ShapeKind.Sector || b.sectorAngleMilliDegrees > 0 && b.sectorAngleMilliDegrees <= 360000);
        public static void DrawShape(HitBox b)
        {
            if (!Valid(b)) return;
            using (new Handles.DrawingScope(Handles.matrix * Matrix4x4.TRS(SkillShapeEdit.World(b.offset), Quaternion.Euler(0, b.yawMilliDegrees / 1000f, 0), Vector3.one)))
            {
                float r = b.radius / 10000f, h = b.height / 10000f;
                if (b.shape == ShapeKind.Box) { Handles.DrawWireCube(Vector3.zero, SkillShapeEdit.World(b.size)); return; }
                if (b.shape == ShapeKind.Sphere) { foreach (var axis in new[] { Vector3.up, Vector3.right, Vector3.forward }) Handles.DrawWireDisc(Vector3.zero, axis, r); return; }
                float cap = b.shape == ShapeKind.Capsule ? Math.Max(0, h * .5f - r) : h * .5f;
                var top = Vector3.up * cap; var bottom = -top;
                if (b.shape == ShapeKind.Sector)
                {
                    float angle = b.sectorAngleMilliDegrees / 1000f;
                    var left = Quaternion.Euler(0, -angle * .5f, 0) * Vector3.forward;
                    var right = Quaternion.Euler(0, angle * .5f, 0) * Vector3.forward;
                    foreach (var c in new[] { top, bottom }) { Handles.DrawWireArc(c, Vector3.up, left, angle, r); Handles.DrawLine(c, c + left * r); Handles.DrawLine(c, c + right * r); }
                    foreach (var v in new[] { Vector3.zero, left * r, right * r }) Handles.DrawLine(top + v, bottom + v);
                    return;
                }
                Handles.DrawWireDisc(top, Vector3.up, r); Handles.DrawWireDisc(bottom, Vector3.up, r);
                foreach (var axis in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back }) Handles.DrawLine(top + axis * r, bottom + axis * r);
                if (b.shape == ShapeKind.Capsule)
                {
                    Handles.DrawWireArc(top, Vector3.forward, Vector3.right, 180, r); Handles.DrawWireArc(bottom, Vector3.forward, Vector3.left, 180, r);
                    Handles.DrawWireArc(top, Vector3.right, Vector3.back, 180, r); Handles.DrawWireArc(bottom, Vector3.right, Vector3.forward, 180, r);
                }
            }
        }
        static void DrawTrajectory(SkillPreviewSession session)
        {
            var motion = session.Motion; if (!motion.Supported) { Handles.Label(Vector3.zero, "Root Motion preview unsupported — displacement disabled"); return; }
            int end = session.Interrupted ? session.InterruptFrame : motion.Duration;
            int step = Math.Max(1, end / 2000);
            using (new Handles.DrawingScope(Color.green))
                for (int f = 0; f < end; f += step) Handles.DrawLine(motion.Position(f).World, motion.Position(Math.Min(end, f + step)).World);
        }
    }
}
