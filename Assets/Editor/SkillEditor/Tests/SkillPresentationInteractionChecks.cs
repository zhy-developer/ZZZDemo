using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace SkillConfig.Editor.Tests
{
    public static class SkillPresentationInteractionChecks
    {
        static void Check(bool ok,string text){if(!ok)throw new Exception("PRESENTATION_GUI_FAIL "+text);Debug.Log("PRESENTATION_GUI_PASS "+text);}
        public static void Run()
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/Temp/SkillEditorPhase2C/"))throw new Exception("Harness only");
            var c=AssetDatabase.LoadAssetAtPath<CharacterSkillCatalog>("Assets/Resources/Phase2D3/SavedCatalog.asset");
            var owner=UnityEngine.Object.Instantiate(c.skills[0]);owner.skill.vfx[0].offset=new Int3 {y=10000};owner.skill.vfx[0].rotationMilliDegrees=new Int3();
            var actual=owner.skill.vfx[0];string before=JsonUtility.ToJson(actual);var oldTool=UnityEditor.Tools.current;
            using(var session=new SkillPreviewSession(owner,c,SkillPresentationChecks.Model()))
            using(var handles=new SkillVfxHandles())
            {
                var view=EditorWindow.GetWindow<SceneView>();view.position=new Rect(0,0,1000,800);view.LookAt(Vector3.up,Quaternion.identity,4,true,true);
                Selection.objects=Array.Empty<UnityEngine.Object>();UnityEditor.Tools.current=Tool.Move;
                Vector2 point=Vector2.zero,chrome=Vector2.zero;
                Action<SceneView> draw=v=>{
                    if(v!=view)return;
                    point=HandleUtility.WorldToGUIPoint(Vector3.up+Vector3.right*HandleUtility.GetHandleSize(Vector3.up)*.6f);
                    if(Event.current.type==EventType.MouseMove)chrome=point-Event.current.mousePosition;
                    handles.Draw(session,actual.id);
                };
                SceneView.duringSceneGui+=draw;
                try
                {
                    view.SendEvent(new Event {type=EventType.Layout});view.SendEvent(new Event {type=EventType.Repaint});
                    view.SendEvent(new Event {type=EventType.MouseMove,mousePosition=point});var click=point+chrome;
                    view.SendEvent(new Event {type=EventType.Layout,mousePosition=click});view.SendEvent(new Event {type=EventType.MouseDown,button=0,mousePosition=click});
                    view.SendEvent(new Event {type=EventType.MouseDrag,button=0,mousePosition=click+Vector2.right*40,delta=Vector2.right*40});
                    Check(JsonUtility.ToJson(actual)==before,"native Scene drag keeps VFX data temporary");
                    view.SendEvent(new Event {type=EventType.MouseUp,button=0,mousePosition=click+Vector2.right*40});
                    Check(actual.offset.x!=0 && actual.scalePermille==1500,"native MouseUp commits VFX position without scaling");
                    string after=JsonUtility.ToJson(actual);Undo.PerformUndo();Check(JsonUtility.ToJson(owner.skill.vfx[0])==before,"native VFX drag Undo");
                    Undo.PerformRedo();Check(JsonUtility.ToJson(owner.skill.vfx[0])==after,"native VFX drag Redo");
                    handles.Begin(owner,actual.id);handles.Set(Vector3.zero,Vector3.zero,1);view.SendEvent(new Event {type=EventType.KeyDown,keyCode=KeyCode.Escape});
                    Check(!handles.Active && JsonUtility.ToJson(owner.skill.vfx[0])==after,"Scene Escape cancels VFX gesture");
                }
                finally{SceneView.duringSceneGui-=draw;UnityEditor.Tools.current=oldTool;}
            }
            UnityEngine.Object.DestroyImmediate(owner);
        }
        public static void Regression()
        {
            SkillPresentationChecks.Run();
            SkillEditorPhase2CChecks.Integration();
            SkillEditorPhase2D1Checks.Run();
            SkillTimelineInteractionChecks.Run();
            SkillComboGraphChecks.Run();
            SkillGraphInteractionChecks.Run();
            SkillPreviewBugChecks.Drag();
            Run();
        }
    }
}
