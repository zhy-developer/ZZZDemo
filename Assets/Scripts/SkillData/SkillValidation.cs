using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZZZ.SkillAuthoring
{
    public enum SkillIssueSeverity { Warning, Error }
    public enum SkillIssueTarget { General, Animation, Motion, HitGroup, HitRegion, Window, Presentation }

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
                if (!Finite(animation.endBlendSeconds) || animation.endBlendSeconds < 0) error("收刀过渡时间必须是有效非负数。");
                if (animation.endClip != null && (animation.endClip.legacy || animation.endClip.length <= 0))
                    error("结束收刀动画需要有效的非 Legacy 片段。");
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
                ValidateHitFeedback(group.feedback,
                    message => error($"命中组 {group.id}：{message}"),
                    message => warning($"命中组 {group.id}：{message}"));
            }

            target = SkillIssueTarget.HitRegion;
            var groupRepeatModes = new Dictionary<int, bool>();
            if (skill.hitRegions != null) for (index = 0; index < skill.hitRegions.Count; index++)
            {
                var region = skill.hitRegions[index];
                if (region == null) { error("存在空攻击区域。"); continue; }
                if (!RangeValid(region.startFrame, region.endFrame, skill.totalFrames)) error(region.label + "：帧区间无效。");
                if (!ids.Contains(region.hitGroupId)) error(region.label + "：命中组不存在。");
                if (region.size.x <= 0 || region.size.y <= 0 || region.radius <= 0) error(region.label + "：尺寸必须大于零。");
                if (!Enum.IsDefined(typeof(SkillRegionShape), region.shape)) error(region.label + "：未知攻击区域形状。");
                if (!Enum.IsDefined(typeof(SkillRegionAnchorMode), region.anchorMode)) error(region.label + "：未知区域定位方式。");
                if (region.shape == SkillRegionShape.Sector && (region.sectorAngleDegrees < 1 || region.sectorAngleDegrees > 360))
                    error(region.label + "：扇形完整张角必须在 1～360 度之间。");
                if (region.needsReview) warning(region.label + "：攻击区域尚未人工确认。");
                ValidateHitTiming(region, error);
                if (region.enabled && Enum.IsDefined(typeof(SkillHitMode), region.hitMode))
                {
                    bool repeat = region.hitMode != SkillHitMode.Once;
                    if (groupRepeatModes.TryGetValue(region.hitGroupId, out bool previousRepeat) && previousRepeat != repeat)
                        error(region.label + "：同一命中组不能混用区间内一次和多段模式，请使用不同命中组。两种多段模式可共用命中组，同帧只结算一次。");
                    else groupRepeatModes[region.hitGroupId] = repeat;
                }
            }


            target = SkillIssueTarget.Window;
            if (skill.windows != null) for (index = 0; index < skill.windows.Count; index++)
            {
                var window = skill.windows[index];
                if (window == null || !RangeValid(window.startFrame, window.endFrame, skill.totalFrames))
                { error("存在无效的输入/动作窗口。"); continue; }
                if (!Enum.IsDefined(typeof(SkillWindowKind), window.kind)) error("未知的窗口类型。");
                if (window.kind == SkillWindowKind.ComboExecute)
                {
                    if (string.IsNullOrWhiteSpace(window.nextSkillId)) warning("连段执行窗口未指定下一段技能，不会接段。");
                    // 禁止重叠的执行窗口，避免通过列表顺序隐式决定同一帧接哪一段。
                    for (int previous = 0; previous < index; previous++)
                    {
                        var other = skill.windows[previous];
                        if (other != null && other.kind == SkillWindowKind.ComboExecute
                            && window.startFrame < other.endFrame && other.startFrame < window.endFrame)
                        { error("连段执行窗口不能互相重叠。"); break; }
                    }
                }
                else if (!string.IsNullOrWhiteSpace(window.nextSkillId))
                    warning("此窗口不使用下一段技能 ID；请将目标填写在连段执行窗口。");
            }


            target = SkillIssueTarget.Presentation;
            if (skill.presentation != null) for (index = 0; index < skill.presentation.Count; index++)
            {
                var cue = skill.presentation[index];
                if (cue == null || cue.frame < 0 || cue.frame >= skill.totalFrames) { error("存在越界的表现事件。"); continue; }
                if (cue.kind == SkillPresentationKind.Vfx ? cue.effectPrefab == null : cue.audioClip == null)
                    warning($"表现事件 {index + 1}（{cue.kind}，第 {cue.frame} 帧）尚未绑定资源。");
                if (!Enum.IsDefined(typeof(SkillPresentationKind), cue.kind)) error("未知的表现事件类型。");
                if (!Finite(cue.volume) || cue.volume < 0 || cue.volume > 1) error("表现事件音量必须在 0～1 之间。");
                if (!Finite(cue.localOffset) || !Finite(cue.localEulerAngles)) error("表现事件偏移和旋转必须为有效数值。");
                if (cue.kind == SkillPresentationKind.Vfx)
                {
                    if (!Enum.IsDefined(typeof(SkillEffectLifetimeMode), cue.lifetimeMode)) error("未知的特效存活方式。");
                    if (SkillEffectTiming.UsesFrameRange(cue) && !RangeValid(cue.frame, cue.endFrame, skill.totalFrames))
                        error("特效帧区间无效：结束帧必须大于开始帧，且不超过技能总帧数。");
                    if (!Finite(cue.playbackSpeed) || cue.playbackSpeed < .01f || cue.playbackSpeed > 10f)
                        error("特效播放速度倍率必须在 0.01～10 之间。");
                    if (!Finite(cue.localScale) || cue.localScale.x < .01f || cue.localScale.y < .01f || cue.localScale.z < .01f)
                        error("特效缩放 XYZ 必须为有效数值，且各轴至少为 0.01。");
                    if (!SkillEffectTiming.UsesFrameRange(cue)
                        && (!Finite(cue.maxLifetimeSeconds) || cue.maxLifetimeSeconds < .01f || cue.maxLifetimeSeconds > 60))
                        error("特效最长存活时间必须在 0.01～60 秒之间。");
                    if (cue.effectPrefab != null)
                    {
                        string reason = EffectPrefabIssue(cue.effectPrefab);
                        if (reason != null) warning(reason + "（此特效将跳过播放）");
                    }
                }
                if (skill.previewPrefab != null)
                {
                    var root = string.IsNullOrEmpty(skill.animatorPath) ? skill.previewPrefab.transform : skill.previewPrefab.transform.Find(skill.animatorPath);
                    if (root != null && !string.IsNullOrEmpty(cue.bindingPath) && root.Find(cue.bindingPath) == null)
                        warning("表现事件绑定节点不存在：" + cue.bindingPath);
                }
            }

            return issues;
        }

        private static void ValidateHitTiming(SkillHitRegion region, Action<string> error)
        {
            if (!Enum.IsDefined(typeof(SkillHitMode), region.hitMode)) { error(region.label + "：未知命中方式。"); return; }
            if (region.hitMode == SkillHitMode.Interval && region.hitIntervalFrames < 1)
                error(region.label + "：命中间隔必须至少为 1 帧。");
            if (region.hitMode != SkillHitMode.Frames) return;
            if (region.hitFrames == null || region.hitFrames.Length == 0)
            { error(region.label + "：至少配置一个命中帧。"); return; }
            if (region.hitFrames.Length > SkillFrameUtility.MaxAuthoringFrames)
                error(region.label + "：命中帧数量不能超过 10000。");
            var frames = new HashSet<int>();
            foreach (int frame in region.hitFrames)
            {
                if (frame < region.startFrame || frame >= region.endFrame)
                    error($"{region.label}：命中帧 {frame} 必须位于 [{region.startFrame},{region.endFrame}) 内。");
                if (!frames.Add(frame)) error($"{region.label}：命中帧 {frame} 重复。");
            }
        }

        public static void ValidateHitFeedback(SkillHitFeedback feedback, Action<string> error, Action<string> warning)
        {
            // 反馈可完全关闭；只验证已启用资源使用的参数，旧资产无需补齐资源才能进入战斗。
            if (feedback == null) return;
            if (feedback.effectPrefab != null)
            {
                if (!Finite(feedback.localOffset) || !Finite(feedback.localEulerAngles)) error("命中特效偏移和旋转必须为有效数值。");
                if (!Finite(feedback.localScale) || feedback.localScale.x < .01f || feedback.localScale.y < .01f || feedback.localScale.z < .01f)
                    error("命中特效缩放各轴必须至少为 0.01。");
                if (!Finite(feedback.maxLifetimeSeconds) || feedback.maxLifetimeSeconds < .01f || feedback.maxLifetimeSeconds > 60)
                    error("命中特效最长存活时间必须在 0.01～60 秒之间。");
                string reason = EffectPrefabIssue(feedback.effectPrefab);
                if (reason != null) warning("命中特效：" + reason + "（此特效将跳过播放，命中音效仍可播放）");
            }
            if (feedback.audioClip != null && (!Finite(feedback.volume) || feedback.volume < 0 || feedback.volume > 1))
                error("命中音量必须在 0～1 之间。");
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        public static string EffectPrefabIssue(GameObject prefab)
        {
            if (prefab == null) return "未绑定特效 Prefab。";
            if (prefab.GetComponentInChildren<ParticleSystem>(true) == null) return "当前特效需要至少一个 ParticleSystem。";
            // 只接受纯表现组件，避免预览或战斗特效激活脚本、碰撞体、音源等隐式行为。
            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
                if (component == null || !(component is Transform || component is ParticleSystem
                    || component is ParticleSystemRenderer || component is MeshRenderer || component is MeshFilter))
                    return "特效包含暂不支持的组件：" + (component == null ? "Missing Script" : component.GetType().Name);
            return null;
        }
        private static bool RangeValid(int start, int end, int total) => start >= 0 && end > start && end <= total;
    }
}
