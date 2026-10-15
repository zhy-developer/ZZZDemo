
using System.Collections.Generic;
using UnityEngine;

public class SFX_PoolManager : MonoSingleton<SFX_PoolManager>
{
    //有SoundName的代表分的更细的音频
    [System.Serializable]
    public class SoundItem
    {
        public SoundStyle soundStyle;
        public string soundName;   
        public GameObject soundPrefab;
        public int soundCount;
        public bool ApplyBigCenter;
    }
    
    [SerializeField] private List<SoundItem> soundPools = new List<SoundItem>();
    private Dictionary<SoundStyle, Queue<GameObject>> soundCenter = new Dictionary<SoundStyle, Queue<GameObject>>();
    private Dictionary<string, Dictionary<SoundStyle, Queue<GameObject>>> bigSoundCenter = new Dictionary<string, Dictionary<SoundStyle, Queue<GameObject>>>();
    protected override void Initialize()
    {
        // 单例基类只初始化被保留的实例；重复对象销毁后不能继续创建音效。
        base.Initialize();
        InitSoundPool();
    }
    private void InitSoundPool()
    {
        if (soundPools == null || soundPools.Count == 0) { return; }
        for (int i = 0; i < soundPools.Count; i++)
        {
            var item = soundPools[i];
            // Unity 的 == null 同时识别空引用和底层对象已销毁的引用，必须在 Instantiate 前检查。
            if (item == null || item.soundPrefab == null)
            {
                Debug.LogWarning($"[SFX_PoolManager] 跳过音效池条目 {i + 1}（{item?.soundStyle} / {item?.soundName}）：音效 Prefab 未绑定或已删除，请重新绑定或移除此条目。", this);
                continue;
            }
            if (item.soundCount <= 0) continue;
            if (item.ApplyBigCenter && string.IsNullOrWhiteSpace(item.soundName))
            {
                Debug.LogWarning($"[SFX_PoolManager] 跳过音效池条目 {i + 1}：分类音效名称不能为空。", this);
                continue;
            }
            if (soundPools[i].ApplyBigCenter)
            {
                for (int j = 0; j < soundPools[i].soundCount;j++)
                {
                    //实例化
                    var go = Instantiate(soundPools[i].soundPrefab);
                    //设置父级点
                    go.transform.parent = this.transform;
                    //掩藏
                    go.SetActive(false);
                    if (!bigSoundCenter.ContainsKey(soundPools[i].soundName))
                    {
                        Debug.Log(soundPools[i].soundName + "加入对象池");
                        bigSoundCenter.Add(soundPools[i].soundName, new Dictionary<SoundStyle, Queue<GameObject>>());
                    }
                    if (!bigSoundCenter[soundPools[i].soundName].ContainsKey(soundPools[i].soundStyle))
                    {
                        bigSoundCenter[soundPools[i].soundName].Add(soundPools[i].soundStyle, new Queue<GameObject>());
                    }
                    bigSoundCenter[soundPools[i].soundName][soundPools[i].soundStyle].Enqueue(go);
                }
            }
            else
            {
                for (int j = 0; j < soundPools[i].soundCount; j++)
                {
                    //实例化
                    var go = Instantiate(soundPools[i].soundPrefab);
                    //设置父级点
                    go.transform.parent = this.transform;
                    //掩藏
                    go.SetActive(false);
                    //放入字典
                    if (!soundCenter.ContainsKey(soundPools[i].soundStyle))
                    {
                        //加入Kay
                        soundCenter.Add(soundPools[i].soundStyle, new Queue<GameObject>());
                        //把新实例加入Value，而不是预制体
                        soundCenter[soundPools[i].soundStyle].Enqueue(go);
                    }
                    else
                    {
                        //只用加Value
                        soundCenter[soundPools[i].soundStyle].Enqueue(go);
                    }
                }
            }

        }

    }

    public void TryGetSoundPool(SoundStyle soundStyle, string soundName, Vector3 position)
    {
        if (!string.IsNullOrEmpty(soundName) && bigSoundCenter.ContainsKey(soundName))
        {
            if (bigSoundCenter[soundName].TryGetValue(soundStyle, out var Q))
            {
                if (!TryTakeAlive(Q, out var go)) return;
                go.transform.position = position;
                go.gameObject.SetActive(true);
                Q.Enqueue(go);
               // Debug.Log("播放音乐"+ soundName+"类型是"+soundStyle);
              
            }
            else
            {
               // Debug.LogWarning(soundStyle + "找不到");
            }
        }
        else
        {
           // Debug.LogWarning(soundName + "找不到");
        }

    }
    public void TryGetSoundPool( SoundStyle soundStye, Vector3 position, Quaternion quaternion)
    {
        if (soundCenter.TryGetValue(soundStye, out var sound))
        {
           // Debug.Log(soundStye + "播放");
            if (!TryTakeAlive(sound, out var go)) return;
            go.transform.position = position;
            go.gameObject.SetActive(true);
            soundCenter[soundStye].Enqueue(go);
        }
        else
        {
           // Debug.Log(soundStye + "不存在");
        }
    }
    // 实例可能随场景对象被移除；丢弃失效项，空池直接跳过播放，不再访问已销毁对象。
    private static bool TryTakeAlive(Queue<GameObject> pool, out GameObject instance)
    {
        while (pool.Count > 0)
        {
            instance = pool.Dequeue();
            if (instance != null) return true;
        }
        instance = null;
        return false;
    }


}
