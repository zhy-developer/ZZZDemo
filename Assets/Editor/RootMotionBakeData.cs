using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RootMotionFrameData
{
    /// <summary>
    /// 当前帧结束时间
    /// </summary>
    public float time;

    /// <summary>
    /// 这一逻辑帧产生的局部位移
    /// </summary>
    public Vector3 deltaPosition;

    /// <summary>
    /// 从动画开始累计到当前帧的位置
    /// </summary>
    public Vector3 position;

    /// <summary>
    /// 整数版本的这一帧位移
    /// </summary>
    public Vector3Int deltaPositionInt;

    /// <summary>
    /// 整数版本的累计位置
    /// </summary>
    public Vector3Int positionInt;
}


[CreateAssetMenu(
    fileName = "RootMotionBakeData",
    menuName = "FrameSync/RootMotionBakeData")]
public class RootMotionBakeData : ScriptableObject
{
    public AnimationClip clip;

    /// <summary>
    /// 每秒逻辑帧数量
    /// </summary>
    public int sampleRate;

    /// <summary>
    /// 浮点数转整数倍率。
    /// 例如 10000 表示 1 Unity Unit = 10000
    /// </summary>
    public int precision;

    public List<RootMotionFrameData> frames = new();
}