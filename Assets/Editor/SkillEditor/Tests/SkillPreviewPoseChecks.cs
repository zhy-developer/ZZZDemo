using System;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Debug = UnityEngine.Debug;

namespace SkillConfig.Editor.Tests
{
    // Execute only in the disposable project. Real model/clip inputs; no production assets edited.
    public static class SkillPreviewPoseChecks
    {
        static void Guard()
        {
            if (!Application.dataPath.Replace((char)92, '/').Contains("/Temp/SkillEditorPhase2C/"))
                throw new Exception("Isolated harness only");
        }
        static void Check(bool valid, string label)
        {
            if (!valid) throw new Exception("POSE_FAIL " + label);
            Debug.Log("POSE_PASS " + label);
        }
        static Vector3[][] VisibleVertices(SkillVisualProxy proxy)
        {
            return proxy.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s => {
                var filter = s.transform.Find("SkillPreview::PoseRenderer")?.GetComponent<MeshFilter>();
                if (s.enabled || !filter || !filter.sharedMesh) throw new Exception("Skin is not displayed through an owned pose snapshot: " + s.name);
                return filter.sharedMesh.vertices.Select(v => filter.transform.TransformPoint(v)).ToArray();
            }).ToArray();
        }
        static void AssertCurrentPose(SkillVisualProxy proxy)
        {
            var skins = proxy.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var actual = VisibleVertices(proxy);
            var mesh = new Mesh();
            try
            {
                for (int n = 0; n < skins.Length; n++)
                {
                    skins[n].BakeMesh(mesh, false);
                    var expected = mesh.vertices;
                    if (actual[n].Length != expected.Length) throw new Exception("Vertex count changed");
                    for (int i = 0; i < expected.Length; i++)
                        if (Vector3.Distance(actual[n][i], skins[n].transform.TransformPoint(expected[i])) > .00001f)
                            throw new Exception("Displayed pose differs from current skeleton: " + skins[n].name + " vertex=" + i);
                }
            }
            finally { Object.DestroyImmediate(mesh); }
        }
        static bool Same(Vector3[][] a, Vector3[][] b) => a.Length == b.Length && a.Select((v, i) =>
            v.Length == b[i].Length && v.Select((p, j) => Vector3.Distance(p, b[i][j]) < .00001f).All(x => x)).All(x => x);
        static void Clean()
        {
            Check(SkillPreviewSession.LiveCount == 0 && !Resources.FindObjectsOfTypeAll<Mesh>().Any(m => m.name.StartsWith("SkillPreview::Pose::")), "session releases all pose meshes");
        }
        public static void Run()
        {
            Guard();
            var watch = Stopwatch.StartNew();
            foreach (var path in new[] { "Assets/Art/Model/雅/Model/星见雅.fbx", "Assets/Art/Model/雅/雅_攻击动作.fbx" })
            foreach (int number in new[] { 1, 2 })
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var owner = AssetDatabase.LoadAssetAtPath<ComboData>(SkillPilotSetup.ComboPath(number));
                string sourceBefore = EditorJsonUtility.ToJson(source), ownerBefore = EditorJsonUtility.ToJson(owner);
                using (var session = new SkillPreviewSession(owner, null, source))
                {
                    Check(session.Error == null, path + " Normal" + number + " opens");
                    var expected = new Vector3[session.Duration][][];
                    for (int f = 0; f < session.Duration; f++)
                    {
                        session.Seek(f); AssertCurrentPose(session.Proxy); expected[f] = VisibleVertices(session.Proxy);
                    }
                    Check(true, "all frames: body, face, hair and other skins match sampled skeleton");
                    session.Seek(0); session.Loop = true; session.SetPlaying(true);
                    for (int f = 0; f < session.Duration * 3; f++)
                    {
                        session.Advance(.033); AssertCurrentPose(session.Proxy);
                        if (session.Error != null || !Same(expected[session.Frame], VisibleVertices(session.Proxy))) throw new Exception("Loop differs from direct Seek at " + session.Frame);
                    }
                    Check(true, "three loops equal direct Seek at every logical frame");
                    session.SetPlaying(false); session.Seek(7); var paused = VisibleVertices(session.Proxy); session.Advance(1);
                    Check(Same(paused, VisibleVertices(session.Proxy)), "pause preserves complete displayed pose");
                    session.Seek(3); session.Seek(7);
                    Check(Same(paused, VisibleVertices(session.Proxy)), "backward and repeated Seek preserve complete pose");
                    session.Interrupt(7); session.Seek(15);
                    Check(session.Frame == 7 && Same(paused, VisibleVertices(session.Proxy)), "interruption freezes all mesh parts at the same frame");
                    int count = Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m.name.StartsWith("SkillPreview::Pose::"));
                    session.Invalidate();
                    Check(count == Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m.name.StartsWith("SkillPreview::Pose::")), "refresh reuses bounded mesh storage");
                    Check(ownerBefore == EditorJsonUtility.ToJson(owner) && sourceBefore == EditorJsonUtility.ToJson(source), "source model and skill asset remain unchanged");
                }
                Clean();
            }
            Debug.Log("POSE_TIMING full regression milliseconds=" + watch.ElapsedMilliseconds);
        }
        public static void Playback()
        {
            Guard();
            var owner = AssetDatabase.LoadAssetAtPath<ComboData>(SkillPilotSetup.ComboPath(2));
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Model/雅/Model/星见雅.fbx");
            var session = new SkillPreviewSession(owner, null, source);
            session.Loop = true; session.SetPlaying(true);
            double deadline = EditorApplication.timeSinceStartup + 5; int samples = 0, wraps = 0, previous = 0;
            EditorApplication.CallbackFunction tick = null;
            tick = () => {
                try
                {
                    if (session.Error != null) throw new Exception(session.Error);
                    AssertCurrentPose(session.Proxy); samples++;
                    if (session.Frame < previous) wraps++; previous = session.Frame;
                    if (EditorApplication.timeSinceStartup < deadline) return;
                    Check(wraps >= 2, "real EditorApplication playback: consistent complete pose across " + samples + " updates and " + wraps + " loop wraps");
                    EditorApplication.update -= tick; session.Dispose(); Clean(); EditorApplication.Exit(0);
                }
                catch (Exception e) { Debug.LogException(e); EditorApplication.update -= tick; session.Dispose(); EditorApplication.Exit(1); }
            };
            EditorApplication.update += tick;
        }
    }
}
