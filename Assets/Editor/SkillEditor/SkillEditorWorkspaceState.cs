using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SkillConfig.Editor
{
    [FilePath("Library/SkillEditor/Workspace.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class SkillEditorWorkspaceState : ScriptableSingleton<SkillEditorWorkspaceState>
    {
        public string catalogGuid, skillGuid;
        public float pixelsPerFrame = 14;
        public Vector2 timelineScroll;
        public List<string> collapsed = new List<string>();
        [System.Serializable] public sealed class VisualBinding { public string catalogGuid, sourceGuid; }
        public List<VisualBinding> visualBindings = new List<VisualBinding>();
        public void Persist() => Save(true);
    }
}
