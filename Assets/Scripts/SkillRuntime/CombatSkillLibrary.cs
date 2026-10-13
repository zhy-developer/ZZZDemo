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
        private readonly Dictionary<string, CombatSkill> byId = new Dictionary<string, CombatSkill>(StringComparer.Ordinal);

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
                var byAsset = new Dictionary<SkillDefinition, CombatSkill>();
                // 显式收录角色可用技能，不扫描 Resources；两端只根据同一配置构建技能库。
                CombatSkill Register(SkillDefinition asset)
                {
                    if (asset == null) throw new ArgumentException("角色技能列表存在空资产。");
                    if (byAsset.TryGetValue(asset, out var existing)) return existing;
                    var registered = new CombatSkill(asset);
                    skills.Add(registered);
                    if (byId.ContainsKey(registered.Data.skillId))
                        throw new ArgumentException("不同技能资产不能使用相同 skillId：" + registered.Data.skillId);
                    byId.Add(registered.Data.skillId, registered);
                    byAsset.Add(asset, registered);
                    return registered;
                }
                if (Profile.commands == null || Profile.commands.Count == 0) throw new ArgumentException("至少绑定一个技能指令。");
                foreach (var binding in Profile.commands)
                {
                    if (binding == null || binding.skill == null) throw new ArgumentException("存在空技能指令绑定。");
                    int command = (int)binding.command;
                    if (command < 1 || command > 6 || commands.ContainsKey(command)) throw new ArgumentException("技能指令必须在 1～6 中且不能重复。");
                    commands.Add(command, Register(binding.skill));
                }
                if (Profile.additionalSkills != null)
                    foreach (var asset in Profile.additionalSkills) Register(asset);
                // 收录完成后再解析跨技能引用，支持循环连段，并在开局直接指出拼错/漏配的 ID。
                foreach (var skill in skills)
                    if (skill.Data.windows != null) foreach (var window in skill.Data.windows)
                        if (window.kind == SkillWindowKind.ComboExecute && !string.IsNullOrWhiteSpace(window.nextSkillId)
                            && !byId.ContainsKey(window.nextSkillId))
                            throw new ArgumentException(skill.Data.skillId + "：连段目标未加入角色技能列表：" + window.nextSkillId);
            }
            catch { Dispose(); throw; }
        }

        private static void ValidateClip(AnimationClip clip, string label)
        {
            if (clip == null || clip.legacy) throw new ArgumentException(label + "动画必须绑定非 Legacy 片段。");
        }
        public bool TryGet(int command, out CombatSkill skill) => commands.TryGetValue(command, out skill);
        public bool TryGet(string skillId, out CombatSkill skill)
        {
            skill = null;
            return !string.IsNullOrWhiteSpace(skillId) && byId.TryGetValue(skillId, out skill);
        }
        public void Dispose()
        {
            foreach (var skill in skills) skill.Dispose();
            skills.Clear(); commands.Clear(); byId.Clear();
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
            => HasWindow(SkillWindowKind.MoveCancel, frame);

        public bool HasWindow(SkillWindowKind kind, int frame)
        {
            if (Data.windows == null) return false;
            foreach (var window in Data.windows)
                if (window != null && window.kind == kind && frame >= window.startFrame && frame < window.endFrame) return true;
            return false;
        }

        public string ComboTarget(int frame)
        {
            if (Data.windows != null) foreach (var window in Data.windows)
                if (window.kind == SkillWindowKind.ComboExecute && frame >= window.startFrame && frame < window.endFrame)
                    return window.nextSkillId;
            return null;
        }
        public void Dispose() { if (Data != null) UnityEngine.Object.Destroy(Data); Data = null; }
    }
}
