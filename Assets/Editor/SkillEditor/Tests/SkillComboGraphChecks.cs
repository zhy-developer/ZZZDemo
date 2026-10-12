using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
namespace SkillConfig.Editor.Tests
{
    public static class SkillComboGraphChecks
    {
        const string Root="Assets/GraphChecks", Path=Root+"/Container.asset";
        static void Check(bool condition,string label) { if(!condition) throw new Exception("FAIL "+label); Debug.Log("GRAPH_PASS "+label); }
        static void Guard() { if (!Application.dataPath.Replace('\\','/').Contains("/Temp/SkillEditorPhase2C/")) throw new Exception("Isolated harness only"); }
        public static void Run()
        {
            Guard();
            var source=AssetDatabase.LoadAssetAtPath<ComboContainerData>("Assets/ScriptableObject/ComboContainerData/星见雅基础连招.asset");
            Check(source && source.comboDatas.Count==6,"real Miyabi six-step container");
            Check(source.comboDatas.Select(x=>x.name).SequenceEqual(Enumerable.Range(1,6).Select(i=>"Unagi_Normal_"+i)),"real reference order Normal1 through Normal6");
            Check(source.DodgeATKData && source.DodgeATKData.name=="Unagi_Rush_ATK" && !source.BackDodgeATKData,"real dodge entry and absent back entry");
            Check(SkillComboGraphDraft.Label(source.comboDatas[0]).Contains("miyabi-normal-1") &&
                SkillComboGraphDraft.Label(source.comboDatas[2]).Contains("miyabi-normal-3") &&
                SkillComboGraphDraft.Label(source.comboDatas[5]).Contains("Legacy"),"Phase4B registered Normal1/3 IDs and unregistered Normal6 Legacy label");
            if(!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets","GraphChecks");
            if(AssetDatabase.LoadAssetAtPath<ComboContainerData>(Path)) AssetDatabase.DeleteAsset(Path);
            var copy=Object.Instantiate(source); AssetDatabase.CreateAsset(copy,Path); AssetDatabase.SaveAssetIfDirty(copy);
            var before=JsonUtility.ToJson(copy); var sourceBefore=JsonUtility.ToJson(source);
            var d=new SkillComboGraphDraft(); d.Load(copy);
            Check(d.Nodes.Count()==7 && !d.Dirty && !d.Conflict,"seven real asset nodes; clean draft");
            d.Move(0,1); Check(d.Dirty && JsonUtility.ToJson(copy)==before && !EditorUtility.IsDirty(copy),"reorder is transient; no dirty asset");
            Check(d.Describe().Contains("之前") && d.Describe().Contains("之后") && d.Describe().Contains(Path) && d.Describe().Contains("Legacy: 是"),"review describes asset before after Legacy and entry impact");
            d.Load(copy); Check(!d.Dirty && JsonUtility.ToJson(copy)==before,"cancel leaves bytes intact");
            Check(d.ConnectAfter(copy.comboDatas[0],copy.comboDatas[2])==null && d.Steps[1]==copy.comboDatas[2],"edge interaction reorders existing linear slots");
            Check(d.ConnectAfter(copy.DodgeATKData,copy.comboDatas[0])!=null,"arbitrary branch blocked");
            d.Load(copy); d.Steps.Clear(); Check(d.ApplyConfirmed()!=null && JsonUtility.ToJson(copy)==before,"empty sequence blocked atomically");
            d.Load(copy); d.Steps[1]=null; Check(d.ApplyConfirmed()!=null,"missing slot blocked");
            d.Load(copy); d.Steps[1]=d.Steps[0]; Check(d.ApplyConfirmed()!=null,"ambiguous duplicate asset blocked");
            var duplicate=Object.Instantiate(source.comboDatas[0]); string duplicatePath=Root+"/Duplicate.asset";
            if(AssetDatabase.LoadAssetAtPath<ComboData>(duplicatePath)) AssetDatabase.DeleteAsset(duplicatePath);
            AssetDatabase.CreateAsset(duplicate,duplicatePath);
            d.Load(copy); d.Steps[2]=duplicate; Check(d.ApplyConfirmed()!=null && JsonUtility.ToJson(copy)==before,"distinct assets with duplicate SkillID blocked atomically");
            duplicate.skill.skillId=""; Check(d.ApplyConfirmed()!=null,"registered empty SkillID blocked");
            duplicate.skill.skillId="unique-graph-test"; var properties=new SerializedObject(duplicate); properties.FindProperty("_comboName").stringValue=""; properties.ApplyModifiedPropertiesWithoutUndo();
            Check(d.ApplyConfirmed()!=null,"empty Animator entry blocked");
            AssetDatabase.DeleteAsset(duplicatePath);
            d.Load(copy); d.Dodge=null; Check(d.ApplyConfirmed()!=null,"unsafe removal of active dodge entry blocked");
            d.Load(copy); d.Move(0,1); copy.comboDatas.Reverse(); var externallyChanged=JsonUtility.ToJson(copy);
            Check(d.ApplyConfirmed()!=null && JsonUtility.ToJson(copy)==externallyChanged,"stale draft cannot overwrite external changes");
            copy.comboDatas=source.comboDatas.ToList(); EditorUtility.SetDirty(copy); AssetDatabase.SaveAssetIfDirty(copy);
            d.Load(copy); d.Move(0,1); d.Dodge=source.comboDatas[2];
            var catalog=ScriptableObject.CreateInstance<CharacterSkillCatalog>(); catalog.characterId="graph-check"; catalog.skills.Add(source.comboDatas[0]); catalog.skills.Add(source.comboDatas[1]); catalog.animatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(SkillPilotSetup.ControllerPath);
            string jsonBefore=JsonUtility.ToJson(SkillAssetValidator.Build(catalog,out var ignored));
            var ids=source.comboDatas.Select(x=>x.skill?.skillId).ToArray();
            Check(d.ApplyConfirmed()==null && copy.comboDatas[0]==source.comboDatas[1] && copy.DodgeATKData==source.comboDatas[2],"explicit apply persists only target relations");
            Undo.PerformUndo(); SkillEditCommands.Flush(); Check(JsonUtility.ToJson(copy)==before,"one Undo restores order and dodge");
            Undo.PerformRedo(); SkillEditCommands.Flush(); Check(copy.comboDatas[0]==source.comboDatas[1] && copy.DodgeATKData==source.comboDatas[2],"one Redo restores whole graph change");
            Check(sourceBefore==JsonUtility.ToJson(source) && ids.SequenceEqual(source.comboDatas.Select(x=>x.skill?.skillId)),"source assets and SkillIDs unchanged");
            Check(JsonUtility.ToJson(SkillAssetValidator.Build(catalog,out ignored))==jsonBefore,"graph apply does not change Phase1 DTO or schema"); Object.DestroyImmediate(catalog);
            Check(!copy.BackDodgeATKData,"unwired back entry not mutated");
            var runtime=Object.Instantiate(copy); runtime.comboDatas=copy.comboDatas.ToList(); runtime.Init(); runtime.SwitchDodgeATK();
            Check(runtime.comboDatas[0]==copy.DodgeATKData && runtime.comboDatas[1]==copy.comboDatas[1],"real container replaces slot0 only");
            runtime.ResetComboDatas(); Check(runtime.comboDatas[0]==copy.comboDatas[0],"real container resets cached first slot"); Object.DestroyImmediate(runtime);
            var ws=SkillEditorWorkspaceState.instance; string guid=AssetDatabase.AssetPathToGUID(Path);
            ws.graphLayouts.RemoveAll(x=>x.containerGuid==guid);
            ws.graphLayouts.Add(new SkillEditorWorkspaceState.GraphLayout {containerGuid=guid,zoom=.75f,pan=new Vector2(12,34),nodes=new List<SkillEditorWorkspaceState.GraphNode>{new SkillEditorWorkspaceState.GraphNode {skillGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(copy.comboDatas[0])),position=new Vector2(222,111)}}});
            ws.Persist(); Check(!EditorUtility.IsDirty(copy),"layout save does not dirty combo container");
            AssetDatabase.ImportAsset(Path,ImportAssetOptions.ForceUpdate); Check(AssetDatabase.LoadAssetAtPath<ComboContainerData>(Path).comboDatas[0]==source.comboDatas[1],"saved asset survives reimport");
            Debug.Log("GRAPH_RESULT data checks completed; full game Play regression not executed");
        }
        public static void Reopen()
        {
            Guard(); var c=AssetDatabase.LoadAssetAtPath<ComboContainerData>(Path);
            Check(c && c.comboDatas[0].name=="Unagi_Normal_2" && c.comboDatas[1].name=="Unagi_Normal_1","fresh process restores order");
            Check(c.DodgeATKData.name=="Unagi_Normal_3" && !c.BackDodgeATKData,"fresh process restores dodge; back unchanged");
            var l=SkillEditorWorkspaceState.instance.graphLayouts.First(x=>x.containerGuid==AssetDatabase.AssetPathToGUID(Path));
            Check(l.zoom==.75f && l.pan==new Vector2(12,34) && l.nodes[0].position==new Vector2(222,111),"fresh process restores Library layout");
        }
    }
}
