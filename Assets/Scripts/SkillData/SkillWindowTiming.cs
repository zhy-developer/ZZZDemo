namespace ZZZ.SkillAuthoring
{
    /// <summary>窗口的纯数据查询；战斗和编辑器共用 [开始帧, 结束帧)，技能结束边界不生效。</summary>
    public static class SkillWindowTiming
    {
        public static bool IsActive(SkillDefinition skill, SkillWindowKind kind, int frame)
        {
            if (skill == null || skill.windows == null || frame < 0 || frame >= skill.totalFrames) return false;
            foreach (var window in skill.windows)
                if (window != null && window.kind == kind && frame >= window.startFrame && frame < window.endFrame)
                    return true;
            return false;
        }
    }
}
