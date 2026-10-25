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
        public CombatSkill DodgeFront { get; private set; }
        public CombatSkill DodgeBack { get; private set; }
        public CombatSkill Parry { get; private set; }
        public CombatSkill ParrySuccess { get; private set; }
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
                if (Profile.dodgeCooldownFrames < 0 || Profile.dodgeCooldownFrames > 10000)
                    throw new ArgumentException("闪避冷却必须在 0～10000 帧之间。");
                if (Profile.skillChargeFrames < 1 || Profile.skillChargeFrames > 10000)
                    throw new ArgumentException("E 蓄力时间必须在 1～10000 帧之间。");
                if (Profile.parryBrokenStunFrames < 0 || Profile.parryBrokenStunFrames > 10000)
                    throw new ArgumentException("被振硬直必须在 0～10000 帧之间。");
                if (Profile.parryBrokenClip != null) ValidateClip(Profile.parryBrokenClip, "被振");
                SkillValidation.ValidateHitFeedback(Profile.parryFeedback,
                    message => throw new ArgumentException("振刀反馈：" + message),
                    message => Debug.LogWarning("振刀反馈：" + message, source));
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
                if (commands.ContainsKey(CombatCharge.Command)) ValidateClip(Profile.skillChargeClip, "E 蓄力");
                commands.TryGetValue(CombatParry.Command, out var parry);
                Parry = parry;
                ParrySuccess = Profile.parrySuccessSkill != null ? Register(Profile.parrySuccessSkill) : null;
                if (ParrySuccess != null && Parry == null)
                    throw new ArgumentException("配置振刀成功技能前，请先在 Commands / Rop 5 绑定空格招架技能。");
                if (Parry != null)
                {
                    if (Parry == ParrySuccess) throw new ArgumentException("招架与振刀成功需要独立技能资产。");
                    ValidateDefensiveSkill(Parry, true);
                    if (ParrySuccess != null) ValidateDefensiveSkill(ParrySuccess, false);
                }
                commands.TryGetValue(CombatDodge.Command, out var front);
                DodgeFront = front;
                DodgeBack = Profile.dodgeBackSkill != null ? Register(Profile.dodgeBackSkill) : null;
                if ((DodgeFront == null) != (DodgeBack == null))
                    throw new ArgumentException("请成对配置闪避：Commands / Rop 4 绑定前闪，Dodge Back Skill 绑定后撤；未启用时两者都留空。");
                if (DodgeFront != null && DodgeFront == DodgeBack)
                    throw new ArgumentException("前闪与后撤需要两个独立技能资产，分别配置向前和向后的根位移。");
                foreach (var binding in commands)
                {
                    if (binding.Key != CombatDodge.Command && IsDodge(binding.Value))
                        throw new ArgumentException("闪避资产只能通过 Rop 4 入口释放，不能重复绑定到其他指令以绕过冷却。");
                    if (IsParry(binding.Value) && (binding.Key != CombatParry.Command || binding.Value != Parry))
                        throw new ArgumentException("招架只允许绑定 Rop 5；振刀成功技能只允许通过成功事件进入。");
                    if (binding.Key != CombatCharge.Command && commands.TryGetValue(CombatCharge.Command, out var charged)
                        && binding.Value == charged)
                        throw new ArgumentException("E 释放技能不能重复绑定到其他指令以绕过蓄力。");
                }
                // 收录完成后再解析跨技能引用，支持循环连段，并在开局直接指出拼错/漏配的 ID。
                foreach (var skill in skills)
                    if (skill.Data.windows != null) foreach (var window in skill.Data.windows)
                        if (window.kind == SkillWindowKind.ComboExecute && !string.IsNullOrWhiteSpace(window.nextSkillId))
                        {
                            if (!byId.TryGetValue(window.nextSkillId, out var target))
                                throw new ArgumentException(skill.Data.skillId + "：连段目标未加入角色技能列表：" + window.nextSkillId);
                            if (IsDodge(target))
                                throw new ArgumentException(skill.Data.skillId + "：连段不能直接跳到闪避技能；请配置 DodgeCancel 窗口并使用闪避键。");
                            if (IsParry(target) || (commands.TryGetValue(CombatCharge.Command, out var charged) && target == charged))
                                throw new ArgumentException(skill.Data.skillId + "：连段不能绕过 E 蓄力或直接进入招架/振刀成功技能。");
                        }
            }
            catch { Dispose(); throw; }
        }

        private static void ValidateClip(AnimationClip clip, string label)
        {
            if (clip == null || clip.legacy || clip.length <= 0) throw new ArgumentException(label + "动画必须绑定有效的非 Legacy 片段。");
        }
        private static void ValidateDefensiveSkill(CombatSkill skill, bool needsWindow)
        {
            if (skill.Data.hitRegions != null) foreach (var region in skill.Data.hitRegions)
                if (region.enabled) throw new ArgumentException(skill.Data.skillId + "：第一版招架/成功动作不允许启用攻击区域。");
            bool hasWindow = false;
            if (skill.Data.windows != null) foreach (var window in skill.Data.windows)
            {
                hasWindow |= window.kind == SkillWindowKind.Parry;
                if (window.kind == SkillWindowKind.ComboExecute || window.kind == SkillWindowKind.PreInput)
                    throw new ArgumentException(skill.Data.skillId + "：招架/成功动作不使用连段窗口，请删除 PreInput/ComboExecute。");
                if (!needsWindow && window.kind == SkillWindowKind.Parry)
                    throw new ArgumentException(skill.Data.skillId + "：振刀成功动作不能再次开启招架窗口。");
            }
            if (needsWindow && !hasWindow) throw new ArgumentException(skill.Data.skillId + "：招架技能需要配置 Parry 窗口。");
        }
        public bool TryGet(int command, out CombatSkill skill) => commands.TryGetValue(command, out skill);
        public bool IsDodge(CombatSkill skill) => skill != null && (skill == DodgeFront || skill == DodgeBack);
        public bool IsParry(CombatSkill skill) => skill != null && (skill == Parry || skill == ParrySuccess);
        public bool TryGet(string skillId, out CombatSkill skill)
        {
            skill = null;
            return !string.IsNullOrWhiteSpace(skillId) && byId.TryGetValue(skillId, out skill);
        }
        public void Dispose()
        {
            foreach (var skill in skills) skill.Dispose();
            skills.Clear(); commands.Clear(); byId.Clear();
            DodgeFront = null; DodgeBack = null;
            Parry = null; ParrySuccess = null;
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
                    if (region.shape != SkillRegionShape.Circle && region.shape != SkillRegionShape.Rectangle && region.shape != SkillRegionShape.Sector)
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
            => SkillWindowTiming.IsActive(Data, kind, frame);

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
