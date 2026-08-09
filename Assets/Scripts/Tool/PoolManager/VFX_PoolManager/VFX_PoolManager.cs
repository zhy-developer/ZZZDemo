using System.Collections.Generic;
using UnityEngine;

public class VFX_PoolManager : MonoSingleton<VFX_PoolManager>
{
    [System.Serializable]
    public class effectData
    {
        public CharacterNameList style;    
        public VFXItemData effectItemData;
    }

    [SerializeField]private List<effectData> effectDatas=new List<effectData>();
    private Dictionary<CharacterNameList, Dictionary<string, Queue<GameObject>>>effectPool =new Dictionary<CharacterNameList, Dictionary<string, Queue<GameObject>>>();


    protected override void Awake()
    {
        base.Awake();
        InitEffectPools();
    }
    private void InitEffectPools()
    {
        if (effectDatas.Count == 0) { return; }

        for (int i = 0; i < effectDatas.Count; i++)//循环特效数据类型
        {
            //先创建一个该类型的字典
            if (!effectPool.ContainsKey(effectDatas[i].style))
            {
                effectPool.Add(effectDatas[i].style, new Dictionary<string, Queue<GameObject>>());
            }

            for (int j = 0; j < effectDatas[i].effectItemData.effectItems.Count; j++)//循环每个特效类型中的多个项目
            {
                effectDatas[i].effectItemData.effectItems[j].effectRotation = Quaternion.Euler(effectDatas[i].effectItemData.effectItems[j].effectEulerAngle);

                for (int k = 0; k < effectDatas[i].effectItemData.effectItems[j].count; k++)
                {
                    //创建实例
                    GameObject go = Instantiate(effectDatas[i].effectItemData.effectItems[j].VFXPrefab);
                    if (effectDatas[i].effectItemData.effectItems[j].applyParentPos)
                    {
                        //设置父级点
                        go.transform.parent = effectDatas[i].effectItemData.effectItems[j].parentPos;
                    }
                    else
                    {
                        go.transform.parent = this.transform;
                    }
                    //位置
                    go.transform.localPosition = Vector3.zero;
                    //旋转
                    go.transform.localRotation = effectDatas[i].effectItemData.effectItems[j].effectRotation;
                    //隐藏
                    go.SetActive(false);
                    //放入字典
                    if (!effectPool[effectDatas[i].style].ContainsKey(effectDatas[i].effectItemData.effectItems[j].VFXName))
                    {
                        effectPool[effectDatas[i].style].Add(effectDatas[i].effectItemData.effectItems[j].VFXName, new Queue<GameObject>());
                    }
                    effectPool[effectDatas[i].style][effectDatas[i].effectItemData.effectItems[j].VFXName].Enqueue(go);
                }
            }
        }
    }
    /// <summary>
    /// 只能设置有父级的特效
    /// </summary>
    /// <param name="characterName"></param>
    /// <param name="effectName"></param>
    public void TryGetVFX(CharacterNameList characterName, string effectName )
    {
        if (effectPool.ContainsKey(characterName) && effectPool[characterName].ContainsKey(effectName) && effectPool[characterName][effectName].Count > 0)
        {
            GameObject go = effectPool[characterName][effectName].Dequeue();
            PlayEffect(go);
            effectPool[characterName][effectName].Enqueue(go);
        }
        else
        {
            Debug.LogWarning(characterName + "类型"+ effectName + "名字的"+"对象池不存在");
        }
    }
    /// <summary>
    /// 用来设置没有父级的特效
    /// </summary>
    /// <param name="characterName"></param>
    /// <param name="effectName"></param>
    /// <param name="worldPos"></param>
    /// <param name="quaternion"></param>
    public void GetVFX(CharacterNameList characterName, string effectName, Vector3 worldPos = default(Vector3), Quaternion quaternion = default(Quaternion))
    {
        if (effectPool.ContainsKey(characterName) && effectPool[characterName].ContainsKey(effectName) && effectPool[characterName][effectName].Count > 0)
        {
            GameObject go = effectPool[characterName][effectName].Dequeue();
            go.transform.position = worldPos;
            if (quaternion.x == 0f && quaternion.y == 0f && quaternion.z == 0f && quaternion.w == 0f)
            {
                quaternion = Quaternion.identity;
            }
            go.transform.rotation = quaternion;
            PlayEffect(go);
            effectPool[characterName][effectName].Enqueue(go);
        }
        else
        {
            Debug.LogWarning(characterName + "类型" + effectName + "名字的" + "对象池不存在");
        }

    }

    private void PlayEffect(GameObject effect)
    {
        if (effect.TryGetComponent<EffectItem>(out var effectItem))
        {
            effectItem.PlayFromPool();
            return;
        }

        effect.SetActive(false);
        effect.SetActive(true);
    }

}
