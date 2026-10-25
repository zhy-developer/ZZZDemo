using System;
using System.Collections.Generic;
using GameProtocol;
using UnityEngine;

namespace ZZZ.SkillAuthoring
{
    /// <summary>角色的对战配置：生命、受击圆、输入到技能的绑定及基础表现，不保存战斗状态。</summary>
    [CreateAssetMenu(fileName = "CombatProfile", menuName = "ZZZ/技能制作/角色对战配置")]
    public sealed class SkillCombatProfile : ScriptableObject
    {
        [Min(1)] public int maxHealth = 100;
        [Min(1), Tooltip("逻辑单位；10000 = 1 Unity 单位。受击圆始终位于角色逻辑根节点。")]
        public int hurtRadius = 4000;
        [Min(0), Tooltip("每个 33ms 逻辑帧的普通移动距离；不依赖渲染帧率。")]
        public int movePerFrame = 3000;
        public List<SkillCommandBinding> commands = new List<SkillCommandBinding>();
        [Tooltip("连段后续技能放在这里；按键入口技能已自动收录，无需重复添加。nextSkillId 在本角色技能库内查找。")]
        public List<SkillDefinition> additionalSkills = new List<SkillDefinition>();
        [Header("闪避（前闪绑定在 Commands / Rop 4）")]
        [Tooltip("无方向输入时使用的后撤技能，自动注册到技能库。根位移应沿角色局部 -Z；保留释放时朝向。与 Rop 4 前闪成对配置，不启用闪避时两者都留空。")]
        public SkillDefinition dodgeBackSkill;
        [Min(0), Tooltip("前闪与后撤共用的逻辑帧冷却；成功释放时开始，受击和技能结束不重置。24 帧 = 792ms。")]
        public int dodgeCooldownFrames = 24;
        [Header("E 蓄力（释放技能绑定在 Commands / Rop 2）")]
        [Min(1), Tooltip("按下 E 开始蓄力，达到此逻辑帧数自动释放；提前松开取消。12 帧约 396ms。")]
        public int skillChargeFrames = 12;
        [Tooltip("配置了 Rop 2 时必填。蓄力动画只提供表现，不产生根位移或动画事件。")]
        public AnimationClip skillChargeClip;
        [Tooltip("勾选时循环整个蓄力片段；否则播完保持末姿势。逻辑蓄力时间独立于动画长度。")]
        public bool loopSkillCharge;
        [Header("空格招架（招架技能绑定在 Commands / Rop 5）")]
        [Tooltip("可选，成功后切入此技能资产；不填则继续招架动作，关闭本次剩余招架机会。不可含伤害区域。")]
        public SkillDefinition parrySuccessSkill;
        [Tooltip("可选，被振刀时播放；留空使用普通受击片段。动画长度不决定硬直。")]
        public AnimationClip parryBrokenClip;
        [Min(0), Tooltip("本角色攻击被振刀后承受的硬直，默认 18 帧约 594ms。")]
        public int parryBrokenStunFrames = 18;
        [Tooltip("本角色成功招架时的特效和 2D 音效。生成在自身根节点，沿自身朝向，触发后自然播完。")]
        public SkillHitFeedback parryFeedback = new SkillHitFeedback();
        // 保留旧资产序列化数据，当前待机/移动由预制体 Animator Controller 提供。
        [HideInInspector] public AnimationClip idleClip;
        [HideInInspector] public AnimationClip moveClip;
        [Header("受击动作（仅表现，不决定硬直结束）")]
        public AnimationClip hitClip;
    }

    [Serializable]
    public sealed class SkillCommandBinding
    {
        [Tooltip("rop1 普攻；rop2 E 蓄力完成后的技能；rop3 大招；rop4 前闪；rop5 空格招架；rop6 重击。")]
        public RightOpType command = RightOpType.rop1;
        public SkillDefinition skill;
    }
}
