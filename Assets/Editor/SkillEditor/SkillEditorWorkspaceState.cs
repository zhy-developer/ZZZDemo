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
        public List<string> hiddenTracks = new List<string>();
        [System.Serializable] public sealed class VisualBinding { public string catalogGuid, sourceGuid; }
        public List<VisualBinding> visualBindings = new List<VisualBinding>();
        [System.Serializable] public sealed class GraphNode { public string skillGuid; public Vector2 position; }
        [System.Serializable] public sealed class GraphLayout { public string containerGuid; public float zoom = 1; public Vector2 pan = new Vector2(20, 15); public List<GraphNode> nodes = new List<GraphNode>(); }
        public string graphContainerGuid;
        public List<GraphLayout> graphLayouts = new List<GraphLayout>();
        public void Persist() => Save(true);
    }
}
