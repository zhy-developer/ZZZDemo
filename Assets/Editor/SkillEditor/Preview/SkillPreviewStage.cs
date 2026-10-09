using System;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SkillConfig.Editor
{
    public sealed class SkillPreviewStage : PreviewSceneStage
    {
        public Action Closing;
        protected override GUIContent CreateHeaderContent() => new GUIContent("Skill Preview · isolated");
        protected override void OnCloseStage()
        {
            try { var callback = Closing; Closing = null; callback?.Invoke(); }
            finally { base.OnCloseStage(); }
        }
    }
}
