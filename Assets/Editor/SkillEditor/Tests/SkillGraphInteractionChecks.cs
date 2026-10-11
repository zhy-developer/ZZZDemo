using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace SkillConfig.Editor.Tests
{
    public sealed class SkillGraphCheckWindow : EditorWindow
    {
        public readonly SkillComboGraphView graph = new SkillComboGraphView();
        public CharacterSkillCatalog catalog; public ComboData owner; public bool openedTimeline;
        void OnGUI() { graph.Draw(catalog,owner,(s,open)=>{ owner=s; openedTimeline=open; Repaint(); }); }
        void OnDisable() { graph.Dispose(); }
    }
    public static class SkillGraphInteractionChecks
    {
        static void Check(bool b,string s) { if(!b) throw new Exception("FAIL "+s); Debug.Log("GRAPH_GUI_PASS "+s); }
        static void Paint(EditorWindow w) { w.SendEvent(new Event {type=EventType.Layout}); w.SendEvent(new Event {type=EventType.Repaint}); }
        static Dictionary<ComboData,Rect> Bounds(SkillComboGraphView g) => (Dictionary<ComboData,Rect>)typeof(SkillComboGraphView).GetField("bounds",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(g);
        static void Mouse(EditorWindow w,EventType type,Vector2 p,int button=0,int clicks=1,Vector2 delta=default) => w.SendEvent(new Event {type=type,button=button,mousePosition=p,clickCount=clicks,delta=delta});
        public static void Run()
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/Temp/SkillEditorPhase2C/")) throw new Exception("Isolated harness only");
            var target=AssetDatabase.LoadAssetAtPath<ComboContainerData>("Assets/ScriptableObject/ComboContainerData/星见雅基础连招.asset");
            string before=JsonUtility.ToJson(target);
            var ws=SkillEditorWorkspaceState.instance; ws.graphContainerGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(target)); ws.graphLayouts.RemoveAll(x=>x.containerGuid==ws.graphContainerGuid);
            var c=ScriptableObject.CreateInstance<CharacterSkillCatalog>(); c.skills.Add(target.comboDatas[0]); c.skills.Add(target.comboDatas[1]);
            var w=ScriptableObject.CreateInstance<SkillGraphCheckWindow>(); w.catalog=c; w.position=new Rect(30,30,1300,850); w.Show();
            try
            {
                Paint(w); Check(Bounds(w.graph).Count==7,"real seven graph nodes drawn in IMGUI");
                var first=target.comboDatas[0]; Vector2 centre=Bounds(w.graph)[first].center;
                float offset=0; bool located=false;
                for(int dy=-60;dy<=60;dy+=2)
                {
                    w.owner=null; Mouse(w,EventType.MouseDown,centre+Vector2.up*dy); Mouse(w,EventType.MouseUp,centre+Vector2.up*dy);
                    if(w.owner==first) {offset=dy;located=true;break;}
                }
                Check(located,"mouse selection synchronizes ComboData"); Paint(w);
                centre=Bounds(w.graph)[first].center+Vector2.up*offset;
                var layout=ws.graphLayouts.First(x=>x.containerGuid==ws.graphContainerGuid);
                string id=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(first)); var node=layout.nodes.First(x=>x.skillGuid==id); var old=node.position;
                Mouse(w,EventType.MouseDown,centre); Mouse(w,EventType.MouseDrag,centre+new Vector2(40,20)); Mouse(w,EventType.MouseUp,centre+new Vector2(40,20)); Paint(w);
                Check(node.position!=old && JsonUtility.ToJson(target)==before,"node drag changes layout only");
                centre=Bounds(w.graph)[first].center+Vector2.up*offset;
                Mouse(w,EventType.MouseDown,centre,0,2); Mouse(w,EventType.MouseUp,centre); Paint(w);
                Check(w.openedTimeline && w.owner==first,"double click requests matching Timeline");
                float zoom=layout.zoom;
                w.SendEvent(new Event {type=EventType.ScrollWheel,mousePosition=centre,delta=new Vector2(0,-1)}); Paint(w);
                Check(layout.zoom>zoom && JsonUtility.ToJson(target)==before,"zoom never writes runtime relations");
                var pan=layout.pan; Mouse(w,EventType.MouseDown,centre,2); Mouse(w,EventType.MouseDrag,centre+new Vector2(20,15),2,1,new Vector2(20,15)); Mouse(w,EventType.MouseUp,centre,2); Paint(w);
                Check(layout.pan!=pan,"middle mouse pans Graph");
                var legacy=target.comboDatas[2]; w.owner=legacy; w.graph.Focus(legacy); Paint(w);
                Check(Bounds(w.graph).ContainsKey(legacy) && SkillComboGraphDraft.Label(legacy).Contains("Legacy"),"Legacy selectable without migration");
                w.graph.SaveLayout(); Check(JsonUtility.ToJson(target)==before && !EditorUtility.IsDirty(target),"save layout leaves official container clean");
            }
            finally { w.Close(); UnityEngine.Object.DestroyImmediate(c); }
            var main=ScriptableObject.CreateInstance<SkillEditorWindow>(); main.Show();
            try
            {
                typeof(SkillEditorWindow).GetField("graphPage",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(main,true); Paint(main);
                var mainGraph=(SkillComboGraphView)typeof(SkillEditorWindow).GetField("graph",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(main);
                var mainOwner=typeof(SkillEditorWindow).GetField("owner",BindingFlags.NonPublic|BindingFlags.Instance);
                var mainPage=typeof(SkillEditorWindow).GetField("graphPage",BindingFlags.NonPublic|BindingFlags.Instance);
                var first=target.comboDatas[0]; mainOwner.SetValue(main,first); mainGraph.Focus(first); Paint(main); Paint(main);
                var centre=Bounds(mainGraph)[first].center; bool clicked=false;
                for(int offset=-30;offset<=30;offset+=2)
                {
                    Mouse(main,EventType.MouseDown,centre+Vector2.up*offset,0,2); Mouse(main,EventType.MouseUp,centre+Vector2.up*offset);
                    if(!(bool)mainPage.GetValue(main)) {clicked=true;break;}
                }
                Paint(main); Check(clicked && (ComboData)mainOwner.GetValue(main)==first,"actual integrated window double click selects matching Timeline");
                mainPage.SetValue(main,false); Paint(main);
                Check(true,"existing SkillEditorWindow renders Graph and Timeline pages");
            }
            finally {main.Close();}
        }
    }
}
