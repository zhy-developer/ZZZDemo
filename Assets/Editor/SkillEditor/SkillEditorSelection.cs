using System;

namespace ZZZ.SkillEditor
{
    internal enum SkillSelectionKind { General, Animation, Motion, Region, Window, Presentation, HitGroups }

    /// <summary>窗口选中项仅属于编辑会话，不写入技能资产。</summary>
    [Serializable]
    internal struct SkillEditorSelection
    {
        public SkillSelectionKind kind;
        public int index;
        public SkillEditorSelection(SkillSelectionKind kind, int index = -1) { this.kind = kind; this.index = index; }
    }
}
