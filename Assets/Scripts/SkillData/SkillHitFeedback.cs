using System;
using UnityEngine;

namespace ZZZ.SkillAuthoring
{
    /// <summary>命中组的纯表现配置。空资源表示关闭该项，不影响伤害、硬直或命中次数。</summary>
    [Serializable]
    public sealed class SkillHitFeedback
    {
        [InspectorName("命中特效 Prefab"), Tooltip("实际命中后播放；支持纯粒子/网格 Prefab，留空不播放。")]
        public GameObject effectPrefab;
        [InspectorName("命中偏移 XYZ（米）"), Tooltip("以受击者根节点为起点，按攻击者命中时的朝向解释：X 向右、Y 向上、Z 向前。生成后停留在命中位置。")]
        public Vector3 localOffset = new Vector3(0, 1, 0);
        [InspectorName("特效额外旋转 XYZ（度）")]
        public Vector3 localEulerAngles;
        [InspectorName("特效缩放 XYZ"), Tooltip("额外缩放倍数，各轴至少 0.01，不修改 Prefab。")]
        public Vector3 localScale = Vector3.one;
        [InspectorName("特效最长存活（秒）"), Range(.01f, 60f)]
        public float maxLifetimeSeconds = 3f;
        [InspectorName("命中音效"), Tooltip("实际命中后播放一次 2D 音效；留空不播放。与挥刀音效独立配置。")]
        public AudioClip audioClip;
        [InspectorName("命中音量"), Range(0, 1)]
        public float volume = 1f;
    }
}
