using System;
using System.Collections.Generic;

/// <summary>
/// 一个 AnimationClip 烘焙后的 Root Motion 数据。
/// </summary>
[Serializable]
public class RootMotionJsonData
{
    /// <summary>
    /// 动画名称。
    /// </summary>
    public string clipName;

    /// <summary>
    /// 源 AnimationClip 的帧率。
    ///
    /// 这里只用于记录信息、以及把“动画帧”转换为“时间”。
    /// 不决定 RootMotion 的采样频率。
    /// </summary>
    public int sourceAnimationFrameRate;

    /// <summary>
    /// 游戏逻辑帧间隔。
    ///
    /// 例如：
    /// 33 = 每 33ms 更新一次逻辑。
    /// </summary>
    public int logicFrameIntervalMs;

    /// <summary>
    /// 浮点位置转换整数位置的倍率。
    ///
    /// 10000：
    /// 1 Unity Unit = 10000 个逻辑单位。
    /// </summary>
    public int precision;

    /// <summary>
    /// 整个 AnimationClip 长度。
    /// 单位：毫秒。
    /// </summary>
    public int clipLengthMs;

    /// <summary>
    /// RootMotion 结束的动画帧。
    ///
    /// -1 表示烘焙整个 AnimationClip。
    ///
    /// 例如：
    /// 60FPS 动画，第45帧结束：
    ///
    /// 45 / 60 = 0.75秒。
    /// </summary>
    public int rootMotionEndAnimationFrame;

    /// <summary>
    /// RootMotion 实际结束时间。
    /// 单位：毫秒。
    /// </summary>
    public int rootMotionEndTimeMs;

    /// <summary>
    /// 最终生成了多少个“逻辑帧位移”。
    /// </summary>
    public int frameCount;

    /// <summary>
    /// 整个 RootMotion 最终累计位移。
    /// 整数坐标。
    /// </summary>
    public int totalX;
    public int totalY;
    public int totalZ;

    /// <summary>
    /// 每一个逻辑帧的数据。
    /// </summary>
    public List<RootMotionJsonFrame> frames =
        new List<RootMotionJsonFrame>();
}


/// <summary>
/// 一个逻辑帧对应的 RootMotion 数据。
/// </summary>
[Serializable]
public class RootMotionJsonFrame
{
    /// <summary>
    /// 第几个逻辑帧。
    /// 从 0 开始。
    /// </summary>
    public int logicFrame;

    /// <summary>
    /// 当前采样结束时间。
    ///
    /// 例如：
    ///
    /// 33
    /// 66
    /// 99
    /// ...
    ///
    /// 单位：毫秒。
    /// </summary>
    public int sampleEndTimeMs;

    /// <summary>
    /// 本次实际采样了多少毫秒。
    ///
    /// 正常情况下是 33。
    ///
    /// 最后一帧可能不足 33ms。
    ///
    /// 例如：
    /// RootMotion 在 750ms 结束，
    /// 前一帧已经到了 726ms，
    ///
    /// 那最后一次只采：
    ///
    /// 750 - 726 = 24ms。
    /// </summary>
    public int sampleDurationMs;

    /// <summary>
    /// 当前逻辑帧产生的整数位移。
    /// </summary>
    public int deltaX;
    public int deltaY;
    public int deltaZ;

    /// <summary>
    /// 从 RootMotion 开始到当前帧的累计整数位置。
    ///
    /// 主要方便调试和校验。
    /// </summary>
    public int positionX;
    public int positionY;
    public int positionZ;
}