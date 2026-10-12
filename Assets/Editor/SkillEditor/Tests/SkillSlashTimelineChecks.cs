using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace SkillConfig.Editor.Tests
{
    public static class SkillSlashTimelineChecks
    {
        const string Folder="Assets/SlashDragChecks";
        static void Check(bool ok,string label){if(!ok)throw new Exception("SLASH_DRAG_FAIL "+label);Debug.Log("SLASH_DRAG_PASS "+label);}
        static void Guard(){if(!Application.dataPath.Replace('\\','/').Contains("/Temp/SkillEditorPhase2C/"))throw new Exception("Isolated harness only");}
        static void Setup(out ComboData owner,out CharacterSkillCatalog catalog)
        {
            Guard(); Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            owner=Object.Instantiate(AssetDatabase.LoadAssetAtPath<ComboData>(SkillPilotSetup.ComboPath(2)));owner.skill.skillId="slash-drag-check";
            owner.skill.tracks.Clear();owner.skill.hitBoxes.Clear();owner.skill.hitGroups.Clear();owner.skill.vfx.Clear();owner.skill.sfx.Clear();owner.skill.interrupts.Clear();owner.skill.events.Clear();owner.skill.invincibility.Clear();
            var track=SkillEditCommands.AddTrack(owner,TrackKind.Vfx);
            owner.skill.vfx.Add(new VfxCue{id="slash-event",trackId=track.id,resourceId="slash_1",startFrame=6,endFrame=8,lifetime=VfxLifetime.TimelineControlled});
            catalog=ScriptableObject.CreateInstance<CharacterSkillCatalog>();catalog.characterId="slash-drag-check";catalog.animatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(SkillPilotSetup.ControllerPath);catalog.skills.Add(owner);
            // The real prefab moved into Resources after the original bug. Keep the
            // regression's deliberate, resolvable but non-exportable binding explicit.
            const string source="Assets/Resources/Prefabs/VFX/FX_slash_01.prefab", previewOnly=Folder+"/PreviewOnlySlash.prefab";
            Check(AssetDatabase.LoadAssetAtPath<GameObject>(source),"current real slash resource exists");
            if(!AssetDatabase.LoadAssetAtPath<GameObject>(previewOnly)) Check(AssetDatabase.CopyAsset(source,previewOnly),"isolated non-Resources fixture copied");
            catalog.resources.Add(new SkillResourceBinding{id="slash_1",kind=ResourceKind.VfxPrefab,asset=AssetDatabase.LoadAssetAtPath<GameObject>(previewOnly)});
            AssetDatabase.CreateAsset(owner,Folder+"/Skill.asset");AssetDatabase.CreateAsset(catalog,Folder+"/Catalog.asset");AssetDatabase.SaveAssets();
        }
        public static void Reproduce()
        {
            Setup(out var owner,out var catalog);
            var error=SkillTimelineBatch.Move(owner,catalog,new[]{"slash-event"},2);
            Check(error!=null && error.Contains("RESOURCES_PATH") && owner.skill.vfx[0].startFrame==6,"pre-fix valid [6,8) to [8,10) is blocked by export resource path: "+error);
        }
        static Dictionary<string,Rect> Bounds(SkillTimelineCheckWindow w)=>(Dictionary<string,Rect>)typeof(SkillTimelineView).GetField("bounds",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(w.timeline);
        static void Paint(EditorWindow w){w.SendEvent(new Event{type=EventType.Layout});w.SendEvent(new Event{type=EventType.Repaint});}
        static void Mouse(EditorWindow w,EventType t,Vector2 p){w.SendEvent(new Event{type=t,button=0,mousePosition=p});}
        public static void Run()
        {
            Setup(out var owner,out var catalog);Check(catalog.resources[0].asset,"real slash prefab loaded");
            var window=ScriptableObject.CreateInstance<SkillTimelineCheckWindow>();window.owner=owner;window.catalog=catalog;window.position=new Rect(20,20,1200,700);window.Show();
            var ws=SkillEditorWorkspaceState.instance;ws.timelineScroll=Vector2.zero;ws.pixelsPerFrame=30;ws.collapsed.Clear();ws.hiddenTracks.Clear();
            try
            {
                Paint(window);var centre=Bounds(window)["slash-event"].center;bool located=false;float offset=0;
                for(int y=0;y<=60 && !located;y+=2){Mouse(window,EventType.MouseDown,centre+Vector2.up*y);Mouse(window,EventType.MouseUp,centre+Vector2.up*y);Paint(window);if(window.timeline.Selection.Contains("slash-event")){offset=y+8;located=true;}}
                Check(located,"Timeline selects slash_1");
                var point=Bounds(window)["slash-event"].center+Vector2.up*offset;
                Mouse(window,EventType.MouseDown,point);Mouse(window,EventType.MouseDrag,point+Vector2.right*60);
                Check(owner.skill.vfx[0].startFrame==6,"drag remains temporary until release");
                Mouse(window,EventType.MouseUp,point+Vector2.right*60);Paint(window);
                Check(owner.skill.vfx[0].startFrame==8 && owner.skill.vfx[0].endFrame==10 && window.timeline.LastError==null,"actual Timeline body drag commits valid times: "+window.timeline.LastError);
                Undo.PerformUndo();Check(owner.skill.vfx[0].startFrame==6,"drag Undo");Undo.PerformRedo();Check(owner.skill.vfx[0].startFrame==8,"drag Redo");
                SkillEditCommands.Flush();Check(!EditorUtility.IsDirty(owner) && File.ReadAllText(Folder+"/Skill.asset").Contains("startFrame: 8"),"command flush persists changed time to asset");
                string before=JsonUtility.ToJson(owner.skill);
                Check(SkillTimelineBatch.Move(owner,catalog,new[]{"slash-event"},-99)!=null && JsonUtility.ToJson(owner.skill)==before,"negative-frame drag rejected atomically");
                Check(SkillTimelineBatch.Move(owner,catalog,new[]{"slash-event"},99)!=null && JsonUtility.ToJson(owner.skill)==before,"out-of-duration drag rejected atomically");
                var resource=catalog.resources[0].asset;catalog.resources[0].asset=null;
                Check(SkillTimelineBatch.Move(owner,catalog,new[]{"slash-event"},1)!=null && JsonUtility.ToJson(owner.skill)==before,"missing resource still blocks timing change");
                catalog.resources[0].asset=resource;
                owner.skill.vfx[0].resourceId="missing";
                Check(SkillTimelineBatch.Move(owner,catalog,new[]{"slash-event"},1)!=null,"unknown resource ID still blocked");owner.skill.vfx[0].resourceId="slash_1";
                var duplicate=Object.Instantiate(owner);AssetDatabase.CreateAsset(duplicate,Folder+"/Duplicate.asset");
                Check(SkillTimelineBatch.Move(owner,catalog,new[]{"slash-event"},1)!=null,"duplicate global SkillID still blocked");AssetDatabase.DeleteAsset(Folder+"/Duplicate.asset");
                SkillTimelineBatch.Copy(owner,new[]{"slash-event"});
                Check(SkillTimelineBatch.Paste(owner,catalog,1,PasteGroups.Copy,out _)!=null,"paste retains full validation");
                // Produce a real valid JSON first, then restore the non-Resources preview reference.
                const string previewCopy="Assets/Resources/SlashDragEffect.prefab";
                if(!AssetDatabase.LoadAssetAtPath<GameObject>(previewCopy))AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(resource),previewCopy);
                catalog.resources[0].asset=AssetDatabase.LoadAssetAtPath<GameObject>(previewCopy);EditorUtility.SetDirty(catalog);
                Check(!SkillConfigExporter.Export(catalog).Any(d=>d.severity==DiagnosticSeverity.Error),"valid resource exports baseline JSON");
                var path=SkillConfigExporter.PathFor(catalog);var json=File.ReadAllText(path);catalog.resources[0].asset=resource;EditorUtility.SetDirty(catalog);
                Check(SkillConfigExporter.Export(catalog).Any(d=>d.code=="RESOURCES_PATH") && File.ReadAllText(path)==json,"non-Resources export still fails and preserves old JSON");
                SkillEditCommands.Flush();AssetDatabase.SaveAssets();
            }
            finally{window.timeline.Cancel();window.Close();}
        }
        public static void Reopen()
        {
            Guard();var owner=AssetDatabase.LoadAssetAtPath<ComboData>(Folder+"/Skill.asset");
            Check(owner && owner.skill.vfx[0].startFrame==8 && owner.skill.vfx[0].endFrame==10,"fresh Unity process reopens dragged interval [8,10)");
        }
        public static void AutoSave()
        {
            Reopen();
            var owner=AssetDatabase.LoadAssetAtPath<ComboData>(Folder+"/Skill.asset");
            var catalog=AssetDatabase.LoadAssetAtPath<CharacterSkillCatalog>(Folder+"/Catalog.asset");
            Check(SkillTimelineBatch.Move(owner,catalog,new[]{"slash-event"},1)==null,"schedule automatic save");
            double deadline=EditorApplication.timeSinceStartup+15;
            int expected=9;
            EditorApplication.CallbackFunction tick=null;
            tick=()=> {
                try
                {
                    if(EditorApplication.timeSinceStartup>deadline)throw new Exception("automatic save timeout");
                    if(EditorUtility.IsDirty(owner) || !File.ReadAllText(Folder+"/Skill.asset").Contains("startFrame: "+expected))return;
                    Check(true,"automatic save writes frame "+expected+" without explicit Flush");
                    if(expected==9)
                    {
                        expected=8;
                        Check(SkillTimelineBatch.Move(owner,catalog,new[]{"slash-event"},-1)==null,"restore interval through normal command");
                        return;
                    }
                    EditorApplication.update-=tick;EditorApplication.Exit(0);
                }
                catch(Exception e){EditorApplication.update-=tick;Debug.LogException(e);EditorApplication.Exit(1);}
            };
            EditorApplication.update+=tick;
        }
        public static void Regression()
        {
            Reopen();
            SkillEditorPhase2D1Checks.Run();
            SkillTimelineInteractionChecks.Run();
        }
        public static void DeleteChecks()
        {
            Setup(out var owner,out var catalog);
            var track=SkillEditCommands.AddTrack(owner,TrackKind.Event);
            string marker=SkillEditCommands.AddItem(owner,track.id,15);
            string before=JsonUtility.ToJson(owner.skill);
            var error=SkillTimelineBatch.Delete(owner,catalog,new[]{marker});
            Check(error==null && owner.skill.events.Count==0 && owner.skill.vfx.Count==1,
                "delete marker with unchanged slash preview binding: "+error);
            string deleted=JsonUtility.ToJson(owner.skill);
            Undo.PerformUndo();Check(JsonUtility.ToJson(owner.skill)==before,"delete Undo restores exact data");
            Undo.PerformRedo();Check(JsonUtility.ToJson(owner.skill)==deleted,"delete Redo restores exact data");
            Undo.PerformUndo();
            Check(SkillTimelineBatch.Delete(owner,catalog,new[]{marker,"slash-event"})==null && owner.skill.events.Count==0 && owner.skill.vfx.Count==0,"mixed batch delete commits atomically");
            Undo.PerformUndo();Check(JsonUtility.ToJson(owner.skill)==before,"mixed batch Undo restores all items and IDs");
            Check(SkillTimelineBatch.Delete(owner,catalog,new[]{"stale-id"})!=null && JsonUtility.ToJson(owner.skill)==before,"stale selection rejected without mutation");
            Check(SkillTimelineBatch.Delete(owner,catalog,new[]{owner.skill.animations[0].id})!=null && JsonUtility.ToJson(owner.skill)==before,"deleting required animation still rejected");
            var resource=catalog.resources[0].asset;catalog.resources[0].asset=null;
            Check(SkillTimelineBatch.Delete(owner,catalog,new[]{marker})!=null && JsonUtility.ToJson(owner.skill)==before,"missing resource remains blocking");
            catalog.resources[0].asset=resource;
            // Exercise the real Timeline keyboard path, which shares Delete with the context menu.
            var window=ScriptableObject.CreateInstance<SkillTimelineCheckWindow>();window.owner=owner;window.catalog=catalog;window.selected=marker;window.position=new Rect(20,20,1200,700);window.Show();
            try
            {
                Paint(window);
                typeof(SkillTimelineView).GetField("keyboardFocus",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(window.timeline,true);
                window.textEditing=true;window.SendEvent(new Event{type=EventType.KeyDown,keyCode=KeyCode.Delete});window.textEditing=false;
                Check(owner.skill.events.Count==1,"Delete does not intercept Inspector text editing");
                window.SendEvent(new Event{type=EventType.KeyDown,keyCode=KeyCode.Delete});Paint(window);
                Check(owner.skill.events.Count==0 && window.timeline.LastError==null && window.timeline.Selection.Count==0,"Timeline Delete removes marker and clears selection");
            }
            finally {window.timeline.Cancel();window.Close();}
            SkillEditCommands.Flush();
            Check(!EditorUtility.IsDirty(owner) && !File.ReadAllText(Folder+"/Skill.asset").Contains(marker),"deleted marker saved to SO");
            const string copy="Assets/Resources/SlashDragEffect.prefab";
            if(!AssetDatabase.LoadAssetAtPath<GameObject>(copy))AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(resource),copy);
            catalog.resources[0].asset=AssetDatabase.LoadAssetAtPath<GameObject>(copy);
            Check(!SkillConfigExporter.Export(catalog).Any(d=>d.severity==DiagnosticSeverity.Error),"valid reference exports baseline JSON");
            string path=SkillConfigExporter.PathFor(catalog),json=File.ReadAllText(path);catalog.resources[0].asset=resource;EditorUtility.SetDirty(catalog);
            Check(SkillConfigExporter.Export(catalog).Any(d=>d.code=="RESOURCES_PATH") && File.ReadAllText(path)==json,"delete allowance does not relax Export or overwrite old JSON");
            AssetDatabase.SaveAssets();
        }
        public static void DeleteReopen()
        {
            Guard();var owner=AssetDatabase.LoadAssetAtPath<ComboData>(Folder+"/Skill.asset");
            Check(owner && owner.skill.events.Count==0 && owner.skill.vfx.Count==1,"fresh Unity process preserves deletion and remaining VFX");
            SkillEditorPhase2D1Checks.Run();SkillTimelineInteractionChecks.Run();
        }
    }
}
