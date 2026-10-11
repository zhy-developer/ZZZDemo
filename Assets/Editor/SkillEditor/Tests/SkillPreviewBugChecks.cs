using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SkillConfig.Editor.Tests
{
    public static class SkillPreviewBugChecks
    {
        public static void VisualSources()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Temp/SkillEditorPhase2C/")) throw new Exception("Isolated harness only");
            var owner = AssetDatabase.LoadAssetAtPath<ComboData>(SkillPilotSetup.ComboPath(1));
            foreach (var path in new[] { "Assets/Art/Model/雅/Model/星见雅.fbx", "Assets/Art/Model/雅/雅_攻击动作.fbx" })
            {
                using (var session = new SkillPreviewSession(owner, null, AssetDatabase.LoadAssetAtPath<GameObject>(path)))
                {
                    var avatar = session.Proxy.Animator.avatar;
                    Debug.Log("AVATAR_PROBE " + path + " avatar=" + (avatar ? avatar.name : "null") + " valid=" + (avatar && avatar.isValid) + " human=" + (avatar && avatar.isHuman) + " path=" + AssetDatabase.GetAssetPath(avatar));
                    foreach (int frame in new[] { 0, 9, 15 })
                    {
                        session.Seek(frame);
                        var bone = session.Proxy.Root.GetComponentsInChildren<Transform>(true).First(t => t.name == "Bip001");
                        Debug.Log("SOURCE_PROBE " + path + " F=" + frame + " logic=" + session.Proxy.Root.transform.position.ToString("F6") + " animatorLocal=" + session.Proxy.Animator.transform.localPosition.ToString("F6") + " boneWorld=" + bone.position.ToString("F6") + " boneRelative=" + session.Proxy.Root.transform.InverseTransformPoint(bone.position).ToString("F6"));
                        if (Math.Abs(session.Proxy.Root.transform.InverseTransformPoint(bone.position).z) > .1f) throw new Exception("FAIL visual source retains extracted forward root motion: " + path);
                    }
                }
            }
        }
        public static void Probe()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Temp/SkillEditorPhase2C/")) throw new Exception("Isolated harness only");
            foreach (int number in new[] { 1, 2 })
            {
            var owner = AssetDatabase.LoadAssetAtPath<ComboData>(SkillPilotSetup.ComboPath(number));
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Model/雅/Model/星见雅.fbx");
            using (var session = new SkillPreviewSession(owner, null, source))
            {
                var proxy = session.Proxy;
                var bone = proxy.Root.GetComponentsInChildren<Transform>(true).First(t => t.name == "Bip001");
                session.Seek(0); var start = proxy.Animator.transform.InverseTransformPoint(bone.position);
                session.Seek(15); Debug.Log("BUG_PROBE current bone delta=" + (proxy.Animator.transform.InverseTransformPoint(bone.position) - start).ToString("F6") + " table=" + session.Motion.Position(15).World.ToString("F6"));
                var actual = proxy.Animator.transform.InverseTransformPoint(bone.position);
                foreach (bool root in new[] { false, true })
                {
                    proxy.RestoreRest(); proxy.Animator.applyRootMotion = root; proxy.Animator.Rebind(); proxy.RestoreRest();
                    var copy = UnityEngine.Object.Instantiate(owner.skill.animations[0].clip); AnimationUtility.SetAnimationEvents(copy, Array.Empty<AnimationEvent>());
                    var graph = PlayableGraph.Create(); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var play = AnimationClipPlayable.Create(graph, copy); play.SetTime(.495); play.SetSpeed(0);
                    AnimationPlayableOutput.Create(graph, "probe", proxy.Animator).SetSourcePlayable(play); graph.Play(); graph.Evaluate(0);
                    Debug.Log("BUG_PROBE apply=" + root + " bone=" + (proxy.Animator.transform.InverseTransformPoint(bone.position) - start).ToString("F6") + " root=" + proxy.Animator.transform.localPosition.ToString("F6"));
                    if (root && Vector3.Distance(actual, proxy.Animator.transform.InverseTransformPoint(bone.position)) > .0001f) throw new Exception("FAIL sampled bone pose must have extracted root translation, not duplicate baked displacement");
                    graph.Destroy(); UnityEngine.Object.DestroyImmediate(copy);
                }
                Debug.Log("BUG_PASS Normal" + number + " sampled pose extracts root translation");
            }
            }
            Drag();
        }
        public static void Drag()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Temp/SkillEditorPhase2C/")) throw new Exception("Isolated harness only");
            var originalTool = UnityEditor.Tools.current;
            var owner = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ComboData>(SkillPilotSetup.ComboPath(1)));
            owner.skill.hitBoxes.Clear(); var track = SkillEditCommands.AddTrack(owner, TrackKind.HitBox);
            string id = SkillEditCommands.AddItem(owner, track.id, 0);
            var box = owner.skill.hitBoxes[0]; box.offset = new Int3 { y = 10000 }; box.size = new Int3 { x = 10000, y = 10000, z = 10000 };
            string before = JsonUtility.ToJson(box);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Model/雅/Model/星见雅.fbx");
            using (var session = new SkillPreviewSession(owner, null, source))
            using (var handles = new SkillShapeHandles())
            {
                var view = EditorWindow.GetWindow<SceneView>(); view.position = new Rect(0,0,1000,800); view.LookAt(new Vector3(0,1,0), Quaternion.identity, 4, true, true); Selection.objects = Array.Empty<UnityEngine.Object>(); UnityEditor.Tools.current = Tool.None;
                Vector2 point = Vector2.zero, chromeOffset = Vector2.zero;
                Action<SceneView> draw = v => {
                    if (v != view) return;
                    // Target the X shaft, not the centre where the Z axis points into the camera.
                    point = HandleUtility.WorldToGUIPoint(Vector3.up + Vector3.right * HandleUtility.GetHandleSize(Vector3.up) * .6f);
                    var type = Event.current.type; var raw = Event.current.rawType;
                    if (type == EventType.MouseMove) chromeOffset = point - Event.current.mousePosition;
                    handles.Draw(session, id, false);
                    var edit = (SkillShapeEdit)typeof(SkillShapeHandles).GetField("edit", BindingFlags.NonPublic|BindingFlags.Instance).GetValue(handles);
                    Debug.Log("DRAG_PROBE " + type + "/" + raw + " -> " + Event.current.type + "/" + Event.current.rawType + " hot=" + GUIUtility.hotControl + " nearest=" + HandleUtility.nearestControl + " p=" + point + " eventp=" + Event.current.mousePosition + " draft=" + edit.Active + " x=" + box.offset.x);
                };
                SceneView.duringSceneGui += draw;
                try
                {
                    view.SendEvent(new Event { type=EventType.Layout }); view.SendEvent(new Event { type=EventType.Repaint });
                    view.SendEvent(new Event { type=EventType.MouseMove, mousePosition=point });
                    var click = point + chromeOffset;
                    view.SendEvent(new Event { type=EventType.Layout, mousePosition=click });
                    view.SendEvent(new Event { type=EventType.MouseDown, button=0, mousePosition=click });
                    view.SendEvent(new Event { type=EventType.MouseDrag, button=0, mousePosition=click+new Vector2(40,0), delta=new Vector2(40,0) });
                    if (JsonUtility.ToJson(box) != before) throw new Exception("FAIL drag must remain temporary before release");
                    view.SendEvent(new Event { type=EventType.MouseUp, button=0, mousePosition=click+new Vector2(40,0) });
                    if (JsonUtility.ToJson(box) == before) throw new Exception("FAIL actual Scene Handle drag must commit changed geometry");
                    if (box.offset.x == 0 || box.size.x != 10000 || box.size.y != 10000 || box.size.z != 10000)
                        throw new Exception("FAIL Move tool must change position without changing size");
                    Debug.Log("BUG_PASS actual Scene Handle drag commits");
                    string after = JsonUtility.ToJson(box);
                    Undo.PerformUndo();
                    if (JsonUtility.ToJson(owner.skill.hitBoxes[0]) != before) throw new Exception("FAIL Handle Undo");
                    Undo.PerformRedo();
                    if (JsonUtility.ToJson(owner.skill.hitBoxes[0]) != after) throw new Exception("FAIL Handle Redo");
                    Debug.Log("BUG_PASS actual Handle Undo and Redo");
                }
                finally { SceneView.duringSceneGui -= draw; UnityEditor.Tools.current = originalTool; }
            }
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }
}
