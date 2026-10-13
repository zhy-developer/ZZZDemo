using System;
using FrameSync.RootMotion;
using UnityEngine;

namespace ZZZ.SkillAuthoring
{
    // 所有区间统一为 [startFrame, endFrame)。这里只定义序列化数据，不执行事件。
    [Serializable]
    public sealed class SkillAnimationTrack
    {
        public AnimationClip clip;
        [Min(0)] public int startFrame;
        [Min(0)] public float sourceStartSeconds;
        [Tooltip("0 表示动画末尾；单位为原动画秒数。")]
        [Min(0)] public float sourceEndSeconds;
        [Min(0.01f)] public float speed = 1f;
        [Min(0)] public float blendInSeconds = 0.111f;
    }

    [Serializable]
    public sealed class SkillMotionTrack
    {
        [Min(0)] public int startFrame;
        public RootMotionSettings settings = new RootMotionSettings();
    }

    public enum SkillRegionShape { Rectangle, Circle }

    [Serializable]
    public sealed class SkillHitRegion
    {
        public string label = "攻击区域";
        public bool enabled = true;
        [Tooltip("旧距离判定不能自动变成精确攻击盒；人工摆放确认后取消此标记。")]
        public bool needsReview = true;
        public int startFrame;
        public int endFrame = 1;
        public int hitGroupId = 1;
        public SkillRegionShape shape;
        [Tooltip("相对角色逻辑根节点的 XZ 偏移；10000 = 1 Unity 单位。")]
        public Vector2Int center = new Vector2Int(0, 10000);
        [Tooltip("矩形完整宽度 X / 长度 Z，不是半径；10000 = 1 Unity 单位。")]
        public Vector2Int size = new Vector2Int(10000, 10000);
        [Min(1)] public int radius = 5000;
        [Range(-180, 180)] public int yawDegrees;

        public bool IsActive(int frame) => enabled && frame >= startFrame && frame < endFrame;
    }

    [Serializable]
    public sealed class SkillHitGroup
    {
        [Min(1)] public int id = 1;
        [Min(0)] public int damage = 20;
        [Min(0), Tooltip("顿帧暂时全局停用，此值仅保留配置，不产生暂停。")]
        public int hitStopFrames = 3;
        [Tooltip("-1 = 待人工配置。硬直不包含顿帧，不跟随受击动画长度。")]
        public int hitStunFrames = -1;
        public string[] hitAnimationNames = Array.Empty<string>();
        public string[] parryAnimationNames = Array.Empty<string>();
        [Min(0)] public float cameraShake;
        // 帧战斗使用：同一释放序号 + 命中组 + 目标，只结算一次。
    }

    public enum SkillWindowKind { PreInput, ComboExecute, MoveCancel }

    [Serializable]
    public sealed class SkillWindow
    {
        public SkillWindowKind kind;
        public int startFrame;
        public int endFrame = 1;
        [Tooltip("仅 ComboExecute 使用：下一段技能的稳定 ID，须加入角色对战配置的技能列表。留空表示不接段。")]
        public string nextSkillId;
    }

    public enum SkillPresentationKind { Vfx, WeaponAudio, VoiceAudio }

    [Serializable]
    public sealed class SkillPresentationCue
    {
        public SkillPresentationKind kind;
        public int frame;
        public string legacyKey;
        public GameObject effectPrefab;
        public AudioClip audioClip;
        public string bindingPath;
        public Vector3 localOffset;
        [Range(0f, 1f)] public float volume = 1f;
        public bool stopOnInterrupt = true;
    }

    [Serializable]
    public sealed class SkillImportedEvent
    {
        public string functionName;
        public float sourceTimeSeconds;
        public int suggestedFrame;
        public string stringParameter;
        public int intParameter;
        public float floatParameter;
        public UnityEngine.Object objectParameter;
        public bool outsideClip;
    }
}
