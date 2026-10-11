using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace SkillConfig.Editor
{
    public sealed class SkillVfxHandles : IDisposable
    {
        SkillPresentationPreview.Effect editingEffect; ComboData owner; VfxCue draft; string selection; int hot; Tool tool;
        public bool Active=>draft!=null;
        public void Cancel() {if(editingEffect?.Proxy.Root) SkillPresentationPreview.Pose(editingEffect,editingEffect.Cue);editingEffect=null;if(Active) SkillEditCommands.EndGesture();draft=null;owner=null;if(hot!=0 && GUIUtility.hotControl==hot) GUIUtility.hotControl=0;hot=0;}
        public void Dispose()=>Cancel();
        public void Begin(ComboData target,string id) {Cancel();owner=target;draft=JsonUtility.FromJson<VfxCue>(JsonUtility.ToJson(target.skill.vfx.First(c=>c.id==id)));SkillEditCommands.BeginGesture();}
        public void Set(Vector3 offset,Vector3 rotation,float scale) {draft.offset=SkillShapeEdit.Quantize(offset);draft.rotationMilliDegrees=new Int3 {x=SkillShapeEdit.Quantize(rotation.x,1000),y=SkillShapeEdit.Quantize(rotation.y,1000),z=SkillShapeEdit.Quantize(rotation.z,1000)};draft.scalePermille=Math.Max(1,SkillShapeEdit.Quantize(scale,1000));}
        public void Commit()
        {
            var target=owner;var value=draft;Cancel();if(!target || value==null) return;
            var actual=target.skill.vfx.FirstOrDefault(c=>c.id==value.id);if(actual==null)return;
            SkillEditCommands.Apply(target,"Edit VFX transform",()=>{actual.offset=value.offset;actual.rotationMilliDegrees=value.rotationMilliDegrees;actual.scalePermille=value.scalePermille;});
        }
        public void Draw(SkillPreviewSession session,string selected)
        {
            if(session==null || session.IsDisposed || !session.Stage || StageUtility.GetCurrentStage()!=session.Stage) {Cancel();return;}
            if(selection!=selected || tool!=UnityEditor.Tools.current) {Cancel();selection=selected;tool=UnityEditor.Tools.current;}
            var actual=session.Owner.skill.vfx.FirstOrDefault(c=>c!=null && c.id==selected);if(actual==null) {Cancel();return;}
            var e=Event.current;bool release=e.rawType==EventType.MouseUp;
            if(e.type==EventType.KeyDown && e.keyCode==KeyCode.Escape) {Cancel();session.Seek(session.Frame);e.Use();return;}
            Matrix4x4 anchor;
            try
            {
                if(actual.attachment==AttachmentMode.World)
                {
                    int previous=session.Frame;try {session.SamplePose(actual.startFrame);anchor=session.Presentation.Anchor(actual);} finally {session.SamplePose(previous);}
                }
                else anchor=session.Presentation.Anchor(actual);
            }
            catch(Exception ex) {session.Presentation.Note(actual.id,"Error: VFX Handles: "+ex.Message);Cancel();return;}
            var value=draft??actual;
            var offset=SkillShapeEdit.World(value.offset);var rotation=Quaternion.Euler(value.rotationMilliDegrees.x/1000f,value.rotationMilliDegrees.y/1000f,value.rotationMilliDegrees.z/1000f);float scale=value.scalePermille/1000f;
            using(new Handles.DrawingScope(Color.magenta,anchor))
            {
                Handles.Label(offset,"VFX "+value.id);
                EditorGUI.BeginChangeCheck();
                if(tool==Tool.Rotate) rotation=Handles.RotationHandle(rotation,offset);
                else if(tool==Tool.Scale) scale=Handles.ScaleSlider(scale,offset,Vector3.right,rotation,HandleUtility.GetHandleSize(offset),.001f);
                else offset=Handles.PositionHandle(offset,rotation);
                if(EditorGUI.EndChangeCheck())
                {
                    session.SetPlaying(false);if(!Active) Begin(session.Owner,actual.id);hot=GUIUtility.hotControl;
                    try {Set(offset,rotation.eulerAngles,scale);} catch(OverflowException) {Cancel();return;}
                    var effect=session.Presentation.Effects.FirstOrDefault(x=>x.Cue.id==actual.id);
                    if(effect!=null) {editingEffect=effect;SkillPresentationPreview.Pose(effect,draft);}
                }
            }
            if(Active && release) Commit();
        }
    }
}
