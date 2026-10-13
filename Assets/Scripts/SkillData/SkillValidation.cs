using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZZZ.SkillAuthoring
{
    public enum SkillIssueSeverity { Warning, Error }
    public enum SkillIssueTarget { General, Animation, Motion, HitGroup, HitRegion, Window, Presentation, ImportedEvent }

    public readonly struct SkillIssue
    {
        public readonly SkillIssueSeverity Severity;
        public readonly string Message;
        // 只描述数据位置，不依赖 UnityEditor；编辑器将目标和列表下标映射为选中项。
        public readonly SkillIssueTarget Target;
        public readonly int Index;
        public SkillIssue(SkillIssueSeverity severity, string message, SkillIssueTarget target = SkillIssueTarget.General, int index = -1)
        { Severity = severity; Message = message; Target = target; Index = index; }
    }

    /// <summary>只报告问题，不隐式修复或改写资产。草稿可保存，错误必须在接入战斗前解决。</summary>
    public static class SkillValidation
    {
        public static List<SkillIssue> Collect(SkillDefinition skill, SkillFrameSampler sampler)
        {
            var issues = new List<SkillIssue>();
            SkillIssueTarget target = SkillIssueTarget.General;
            int index = -1;
            // 在产生提示时记录来源，避免依靠提示文案或重复的资源名称猜测跳转目标。
            Action<string> error = text => issues.Add(new SkillIssue(SkillIssueSeverity.Error, text, target, index));
            Action<string> warning = text => issues.Add(new SkillIssue(SkillIssueSeverity.Warning, text, target, index));
            if (skill == null) { error("未选择技能资产。"); return issues; }
            if (skill.schemaVersion != SkillDefinition.CurrentVersion) error("技能资产版本不受支持。");
            if (string.IsNullOrWhiteSpace(skill.skillId)) error("技能 ID 不能为空。");
            if (skill.totalFrames < 1 || skill.totalFrames > SkillFrameUtility.MaxAuthoringFrames)
                error("总帧数必须在 1～10000 之间。");
            if (SkillFrameUtility.IntervalMs != 33) error("本轮技能制作约定为 33ms；当前网络配置不一致。");
            if (skill.previewPrefab == null) warning("未配置预览角色。");
            target = SkillIssueTarget.Animation;
            var animation = skill.animation;
            if (animation?.clip == null) error("未配置动画片段。");
            else
            {
                if (animation.clip.legacy) error("预览使用 Animator，不支持 Legacy 动画。");
                if (animation.startFrame < 0 || animation.startFrame >= skill.totalFrames) error("动画开始帧越界。");
                if (!Finite(animation.speed) || animation.speed <= 0) error("动画速度必须是有效正数。");
                if (!Finite(animation.blendInSeconds) || animation.blendInSeconds < 0) error("动画淡入时间必须是有效非负数。");
                if (!Finite(animation.sourceStartSeconds) || !Finite(animation.sourceEndSeconds)
                    || animation.sourceStartSeconds < 0 || animation.sourceEndSeconds < 0
                    || animation.sourceStartSeconds >= SkillFrameSampler.AnimationEndSeconds(animation)
                    || animation.sourceEndSeconds > animation.clip.length + 0.0001f) error("动画裁剪范围无效。");
                if (Finite(animation.speed) && animation.speed > 0)
                {
                    try { if (SkillFrameSampler.AnimationEndFrame(animation) > skill.totalFrames) warning("技能结束帧早于动画末尾，动画将被截断。"); }
                    catch (Exception exception) { error("动画帧数无效：" + exception.Message); }
                }
            }


            target = SkillIssueTarget.Motion;
            var settings = skill.motion?.settings;
            if (settings?.json != null)
            {
                if (!string.IsNullOrEmpty(sampler?.MotionError)) error("根位移数据无效：" + sampler.MotionError);
                if (settings.distancePermille <= 0 || settings.distancePermille > 10000) error("位移倍率必须在 1～10000 千分比之间。");
                int count = sampler?.MotionFrameCount ?? 0;
                int end = settings.endFrameExclusive == 0 ? count : settings.endFrameExclusive;
                if (end <= 0 || end > count) error("根位移结束采样超出数据范围。");
                if (skill.motion.startFrame < 0 || (long)skill.motion.startFrame + end > skill.totalFrames) error("位移轨超出技能帧范围。");
                if (animation?.clip != null)
                {
                    if (Mathf.Abs(animation.speed - 1f) > 0.0001f || animation.sourceStartSeconds != 0
                        || animation.sourceEndSeconds > 0 || skill.motion.startFrame != animation.startFrame)
                        error("现有位移按完整动画 1 倍速烘焙。裁剪、变速或错开起点需要重新烘焙；第一轮不自动重定时。");
                    var metadata = sampler?.MotionMetadata;
                    if (metadata != null)
                    {
                        if (metadata.clipName != animation.clip.name) error("根位移源动画名称与当前动画不一致。");
                        if (Math.Abs(metadata.clipLengthMs - animation.clip.length * 1000.0) > 1.1)
                            warning($"动画实际时长 {animation.clip.length * 1000:F2}ms，位移记录 {metadata.clipLengthMs}ms，请人工确认或重新烘焙。");
                    }
                }
            }
            var ids = new HashSet<int>();

            // 校验命中组并保留具体列表下标，重复 ID 的条目也能独立定位。
            target = SkillIssueTarget.HitGroup;
            if (skill.hitGroups != null) for (index = 0; index < skill.hitGroups.Count; index++)
            {
                var group = skill.hitGroups[index];
                if (group == null) { error("存在空命中组。"); continue; }
                if (group.id <= 0 || !ids.Add(group.id)) error("命中组 ID 必须为不重复的正数。");
                if (group.damage < 0 || group.hitStopFrames < 0 || group.hitStunFrames < -1) error($"命中组 {group.id} 数值无效。");
                if (group.hitStunFrames == -1) warning($"命中组 {group.id} 的硬直待配置；-1 不是实际硬直时长。");
            }

            target = SkillIssueTarget.HitRegion;
            if (skill.hitRegions != null) for (index = 0; index < skill.hitRegions.Count; index++)
            {
                var region = skill.hitRegions[index];
                if (region == null) { error("存在空攻击区域。"); continue; }
                if (!RangeValid(region.startFrame, region.endFrame, skill.totalFrames)) error(region.label + "：帧区间无效。");
                if (!ids.Contains(region.hitGroupId)) error(region.label + "：命中组不存在。");
                if (region.size.x <= 0 || region.size.y <= 0 || region.radius <= 0) error(region.label + "：尺寸必须大于零。");
                if (region.needsReview) warning(region.label + "：攻击区域尚未人工确认。");
            }


            target = SkillIssueTarget.Window;
            if (skill.windows != null) for (index = 0; index < skill.windows.Count; index++)
            {
                var window = skill.windows[index];
                if (window == null || !RangeValid(window.startFrame, window.endFrame, skill.totalFrames)) error("存在无效的输入/动作窗口。");
            }


            target = SkillIssueTarget.Presentation;
            if (skill.presentation != null) for (index = 0; index < skill.presentation.Count; index++)
            {
                var cue = skill.presentation[index];
                if (cue == null || cue.frame < 0 || cue.frame >= skill.totalFrames) { error("存在越界的表现事件。"); continue; }
                if (cue.kind == SkillPresentationKind.Vfx ? cue.effectPrefab == null : cue.audioClip == null)
                    warning("表现事件尚未绑定资源：" + cue.legacyKey);
            }

            target = SkillIssueTarget.ImportedEvent;
            if (skill.importedEvents != null) for (index = 0; index < skill.importedEvents.Count; index++)
            {
                var evt = skill.importedEvents[index];
                if (evt != null && (evt.outsideClip || evt.suggestedFrame >= skill.totalFrames))
                    warning("旧动画事件越界，仅保留迁移记录：" + evt.functionName);
            }
            return issues;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool RangeValid(int start, int end, int total) => start >= 0 && end > start && end <= total;
    }
}
