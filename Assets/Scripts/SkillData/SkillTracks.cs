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
        [InspectorName("结束收刀动画"), Tooltip("可选：技能自然结束且无后续输入时播放。仅表现，不延长技能、不产生根位移；移动、攻击或受击会打断。")]
        public AnimationClip endClip;
        [InspectorName("收刀过渡时间（秒）"), Min(0), Tooltip("进入收刀与收刀返回移动 FSM 的混合时间，使用渲染时间。")]
        public float endBlendSeconds = 0.14f;
    }

    [Serializable]
    public sealed class SkillMotionTrack
    {
        [Min(0)] public int startFrame;
        public RootMotionSettings settings = new RootMotionSettings();
    }

    public enum SkillRegionShape { Rectangle = 0, Circle = 1, [InspectorName("扇形")] Sector = 2 }
    public enum SkillHitMode
    {
        [InspectorName("区间内一次")] Once = 0,
        [InspectorName("指定多个命中帧")] Frames = 1,
        [InspectorName("固定间隔命中")] Interval = 2
    }

    public enum SkillRegionAnchorMode
    {
        [InspectorName("跟随角色")] FollowCharacter = 0,
        [InspectorName("固定在释放位置")] CastPosition = 1,
        [InspectorName("固定在区域生效位置")] ActivationPosition = 2
    }

    [Serializable]
    public sealed class SkillHitRegion
    {
        public string label = "攻击区域";
        public bool enabled = true;
        [Tooltip("人工摆放并确认攻击区域后取消此标记。")]
        public bool needsReview = true;
        public int startFrame;
        public int endFrame = 1;
        public int hitGroupId = 1;
        [InspectorName("命中方式"), Tooltip("默认沿用区间内首次碰到时命中一次；多段模式只在指定帧检测，不延后补打。")]
        public SkillHitMode hitMode;
        [InspectorName("命中帧（技能绝对帧）"), Tooltip("指定帧模式使用。每项必须位于 [Start Frame, End Frame) 内且不能重复。")]
        public int[] hitFrames = Array.Empty<int>();
        [InspectorName("命中间隔（帧）"), Min(1), Tooltip("固定间隔模式使用，从 Start Frame 起每隔 N 帧检测，不包含 End Frame。")]
        public int hitIntervalFrames = 3;
        public SkillRegionShape shape;
        [InspectorName("区域定位方式"), Tooltip("固定模式同时锁定位置与朝向；区域生效位置在 Start Frame 锁定，不等待实际命中。")]
        public SkillRegionAnchorMode anchorMode;
        [Tooltip("相对区域定位基准的局部 XZ 偏移；10000 = 1 Unity 单位。")]
        public Vector2Int center = new Vector2Int(0, 10000);
        [Tooltip("矩形完整宽度 X / 长度 Z，不是半径；10000 = 1 Unity 单位。")]
        public Vector2Int size = new Vector2Int(10000, 10000);
        [Min(1)] public int radius = 5000;
        [Range(1, 360), InspectorName("扇形张角（度）"), Tooltip("完整张角，不是半角；沿局部 Z 正方向展开。360 度等同圆形，偏移 XZ 指定扇形顶点。")]
        public int sectorAngleDegrees = 90;
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
        [InspectorName("命中反馈（实际命中时）")]
        public SkillHitFeedback feedback = new SkillHitFeedback();
        // 单次模式整次释放只结算一次；多段模式同组同目标在同一命中帧只结算一次。
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

    public enum SkillEffectLifetimeMode
    {
        [InspectorName("自然播完")] Natural = 0,
        [InspectorName("指定帧区间")] FrameRange = 1
    }

    [Serializable]
    public sealed class SkillPresentationCue
    {
        public SkillPresentationKind kind;
        public int frame;
        [InspectorName("特效存活方式")]
        public SkillEffectLifetimeMode lifetimeMode;
        [Tooltip("指定帧区间时使用技能绝对帧号；包含开始帧，不包含结束帧。到结束帧清除剩余粒子。")]
        public int endFrame = 1;
        [Range(.01f, 10f), Tooltip("粒子播放速度倍率，叠加 Prefab 原有 Simulation Speed；不改变存活区间或音效速度。")]
        public float playbackSpeed = 1f;
        public GameObject effectPrefab;
        public AudioClip audioClip;
        [Tooltip("相对角色 Animator 节点的路径；空表示 Animator 根节点。")]
        public string bindingPath;
        public Vector3 localOffset;
        public Vector3 localEulerAngles;
        [Tooltip("特效实例的 XYZ 缩放倍数，默认 1；各轴至少 0.01，不修改原始 Prefab。")]
        public Vector3 localScale = Vector3.one;
        [Tooltip("勾选后跟随绑定节点；否则停留在触发时的世界位置。")]
        public bool followBinding = true;
        [Min(0.01f), Tooltip("仅自然播完模式使用，限制粒子特效最长存活秒数（包含循环特效）；指定帧区间忽略此值，音效按片段长度播放。")]
        public float maxLifetimeSeconds = 10f;
        [Range(0f, 1f)] public float volume = 1f;
        public bool stopOnInterrupt = true;
    }
}
