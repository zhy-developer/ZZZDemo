using System;
using System.Collections.Generic;
using UnityEngine;
using ZZZ.SkillAuthoring;

namespace ZZZ.SkillRuntime
{
    /// <summary>开局配置快照和技能位移缓存。未完成的草稿拒绝进入战斗，不在运行时猜测默认硬直。</summary>
    public sealed class CombatSkillLibrary : IDisposable
    {
        public SkillCombatProfile Profile { get; private set; }
        private readonly Dictionary<int, CombatSkill> commands = new Dictionary<int, CombatSkill>();
        private readonly List<CombatSkill> skills = new List<CombatSkill>();

        public CombatSkillLibrary(SkillCombatProfile source)
        {
            try
            {
                if (source == null) throw new ArgumentException("角色未绑定 SkillCombatProfile。");
                Profile = UnityEngine.Object.Instantiate(source);
                Profile.hideFlags = HideFlags.HideAndDontSave;
                if (Profile.maxHealth < 1 || Profile.maxHealth > 1000000000 || Profile.hurtRadius < 1
                    || !CombatMath.InRange(Profile.hurtRadius) || Profile.movePerFrame < 0 || !CombatMath.InRange(Profile.movePerFrame))
                    throw new ArgumentException("生命、受击圆或每帧移动距离超出支持范围。");
                // 待机和移动来自角色原 Animator Controller；只校验仍由时间轴采样的受击片段。
                ValidateClip(Profile.hitClip, "受击");
                var byId = new Dictionary<string, CombatSkill>(StringComparer.Ordinal);
                var byAsset = new Dictionary<SkillDefinition, CombatSkill>();
                if (Profile.commands == null || Profile.commands.Count == 0) throw new ArgumentException("至少绑定一个技能指令。");
                foreach (var binding in Profile.commands)
                {
                    if (binding == null || binding.skill == null) throw new ArgumentException("存在空技能指令绑定。");
                    int command = (int)binding.command;
                    if (command < 1 || command > 6 || commands.ContainsKey(command)) throw new ArgumentException("技能指令必须在 1～6 中且不能重复。");
                    if (!byAsset.TryGetValue(binding.skill, out var skill))
                    {
                        skill = new CombatSkill(binding.skill);
                        skills.Add(skill);
                        if (byId.ContainsKey(skill.Data.skillId)) throw new ArgumentException("不同技能资产不能使用相同 skillId：" + skill.Data.skillId);
                        byId.Add(skill.Data.skillId, skill); byAsset.Add(binding.skill, skill);
                    }
                    commands.Add(command, skill);
                }
            }
            catch { Dispose(); throw; }
        }

        private static void ValidateClip(AnimationClip clip, string label)
        {
            if (clip == null || clip.legacy) throw new ArgumentException(label + "动画必须绑定非 Legacy 片段。");
        }
        public bool TryGet(int command, out CombatSkill skill) => commands.TryGetValue(command, out skill);
        public void Dispose()
        {
            foreach (var skill in skills) skill.Dispose();
            skills.Clear(); commands.Clear();
            if (Profile != null) UnityEngine.Object.Destroy(Profile);
            Profile = null;
        }
    }

    public sealed class CombatSkill : IDisposable
    {
        public SkillDefinition Data { get; private set; }
        private readonly Vector2Int[] motion;
        private readonly Dictionary<int, SkillHitGroup> groups = new Dictionary<int, SkillHitGroup>();

        public CombatSkill(SkillDefinition source)
        {
            try
            {
                Data = UnityEngine.Object.Instantiate(source);
                Data.hideFlags = HideFlags.HideAndDontSave;
                var sampler = new SkillFrameSampler(Data);
                foreach (var issue in SkillValidation.Collect(Data, sampler))
                    if (issue.Severity == SkillIssueSeverity.Error) throw new ArgumentException(source.name + "：" + issue.Message);
                if (Data.hitGroups != null) foreach (var group in Data.hitGroups)
                {
                    if (group.hitStunFrames < 0 || group.hitStunFrames > 10000 || group.hitStopFrames > 10000 || group.damage > 1000000000)
                        throw new ArgumentException(source.name + "：命中组硬直必须已配置，顿帧/硬直最多 10000 帧，伤害最多 10 亿。");
                    groups.Add(group.id, group);
                }
                if (Data.hitRegions != null) foreach (var region in Data.hitRegions)
                {
                    if (region.enabled && region.needsReview) throw new ArgumentException(source.name + "：请先确认攻击区域 " + region.label);
                    if (!CombatMath.InRange(region.center.x) || !CombatMath.InRange(region.center.y)
                        || !CombatMath.InRange(region.size.x) || !CombatMath.InRange(region.size.y) || !CombatMath.InRange(region.radius))
                        throw new ArgumentException(source.name + "：攻击区域坐标/尺寸超出整数安全范围。");
                    if (region.shape != SkillRegionShape.Circle && region.shape != SkillRegionShape.Rectangle)
                        throw new ArgumentException(source.name + "：未知攻击区域类型。");
                    if (region.yawDegrees < -180 || region.yawDegrees > 180)
                        throw new ArgumentException(source.name + "：攻击区域角度必须在 -180～180 度之间。");
                }
                motion = new Vector2Int[Data.totalFrames + 1];
                for (int i = 0; i < motion.Length; i++)
                {
                    motion[i] = sampler.SampleLocalPosition(Data, i);
                    if (!CombatMath.InRange(motion[i].x) || !CombatMath.InRange(motion[i].y))
                        throw new ArgumentException(source.name + "：累计位移超出整数安全范围。");
                }
            }
            catch { Dispose(); throw; }
        }

        public SkillHitGroup Group(int id) => groups[id];
        public Vector2Int MotionDelta(int frame, int facing) => CombatMath.Rotate(motion[frame + 1], facing) - CombatMath.Rotate(motion[frame], facing);
        public bool CanMoveCancel(int frame)
        {
            if (Data.windows == null) return false;
            foreach (var window in Data.windows)
                if (window != null && window.kind == SkillWindowKind.MoveCancel && frame >= window.startFrame && frame < window.endFrame) return true;
            return false;
        }
        public void Dispose() { if (Data != null) UnityEngine.Object.Destroy(Data); Data = null; }
    }
}
