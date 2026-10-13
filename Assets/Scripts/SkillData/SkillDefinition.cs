using System.Collections.Generic;
using UnityEngine;

namespace ZZZ.SkillAuthoring
{
    /// <summary>技能制作资产。只保存配置，不保存角色进度、命中记录或顿帧计数。</summary>
    [CreateAssetMenu(fileName = "Skill", menuName = "ZZZ/技能制作/技能资产")]
    public sealed class SkillDefinition : ScriptableObject
    {
        public const int CurrentVersion = 1;
        [HideInInspector] public int schemaVersion = CurrentVersion;
        [Header("基本信息")]
        public string skillId;
        public string displayName;
        public CharacterNameList character;
        public AttackStyle category;
        [Min(1), Tooltip("有效技能帧为 [0, totalFrames)，结束边界可以预览，但不触发攻击。")]
        public int totalFrames = 20;

        [Header("预览绑定")]
        public GameObject previewPrefab;
        [Tooltip("相对预览 Prefab 根节点的 Animator 路径；空字符串表示根节点。")]
        public string animatorPath = "";

        [Header("轨道")]
        public SkillAnimationTrack animation = new SkillAnimationTrack();
        public SkillMotionTrack motion = new SkillMotionTrack();
        public List<SkillHitGroup> hitGroups = new List<SkillHitGroup>();
        public List<SkillHitRegion> hitRegions = new List<SkillHitRegion>();
        public List<SkillWindow> windows = new List<SkillWindow>();
        public List<SkillPresentationCue> presentation = new List<SkillPresentationCue>();

        [Header("迁移记录（不执行旧动画事件）")]
        public ComboData legacySource;
        public List<SkillImportedEvent> importedEvents = new List<SkillImportedEvent>();
        [TextArea(3, 12)] public string importNotes;
    }
}
