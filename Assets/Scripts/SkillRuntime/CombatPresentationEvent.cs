using ZZZ.SkillAuthoring;

namespace ZZZ.SkillRuntime
{
    /// <summary>逻辑帧到表现层的一次性通知；不在此创建 GameObject 或播放资源。</summary>
    public readonly struct CombatPresentationEvent
    {
        public readonly int CastSerial;
        public readonly SkillPresentationCue Cue;
        public readonly bool Interrupted;
        // 单个区间特效的结束通知；Cue 非空，不能被动画表现层误当成整个技能结束。
        public readonly bool EffectEnded;
        // 结束通知携带配置快照，避免技能清空或一帧追赶多个逻辑帧后丢失收刀片段。
        public readonly SkillDefinition EndedSkill;
        public bool IsEnd => Cue == null;

        public CombatPresentationEvent(int castSerial, SkillPresentationCue cue, bool interrupted = false,
            SkillDefinition endedSkill = null, bool effectEnded = false)
        { CastSerial = castSerial; Cue = cue; Interrupted = interrupted; EndedSkill = endedSkill; EffectEnded = effectEnded; }
    }
}
