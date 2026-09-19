using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName ="ComboContainerData",menuName ="Create/Asset/CoomboContainerData")]
public class ComboContainerData : ScriptableObject
{
    [SerializeField] public List<ComboData> comboDatas=new List<ComboData>();
    [SerializeField, Header("闪A")] public ComboData DodgeATKData;
    [SerializeField, Header("后闪A")] public ComboData BackDodgeATKData;

   private ComboData firstComboData;
    
    public void Init()
    {

        if (comboDatas.Count == 0) { return; }
        //缓存第一个连招
        firstComboData = comboDatas[0];
        Debug.Log("初始化");
    }

    public string GetComboName(int index)
    {
        if (comboDatas.Count == 0) { return null; }
        if (comboDatas[index].comboName == null) { Debug.LogWarning(index+"的索引值下没有连招名"); }
        return comboDatas[index].comboName;
    }
  
    public void SwitchDodgeATK()
    { 
        if (DodgeATKData==null) {
            DeLogger.LogErrorTrace("闪A数据为空");
            return; 
        }
        comboDatas[0]= DodgeATKData;
    }
    public void SwitchBackDodgeATK()
    {
        if (BackDodgeATKData == null) { return; }
        comboDatas[0] = BackDodgeATKData;
    }
    public void ResetComboDatas()
    {
        if (comboDatas == null) { Debug.Log(comboDatas + "是空的"); return; }
        if (comboDatas[0] != firstComboData)
        {
            comboDatas[0] = firstComboData;
            Debug.Log("切换的结果为" + comboDatas[0].name);
        }
        
    }
    public float GetComboColdTime(int index)
    {
        if (comboDatas.Count == 0) { return 0; }
        if (comboDatas[index].comboColdTime == 0) { Debug.LogWarning(index + "的索引值下没有设置连招的冷却时间"); }
        return comboDatas[index].comboColdTime;
    }

    public float GetComboDistance(int index)
    {
        if (comboDatas.Count == 0) { return 0; }
        if (comboDatas[index].attackDistance== 0) { Debug.LogWarning(index + "的索引值下没有设置连招的攻击距离"); }
        return comboDatas[index].attackDistance;
    }
    public float GetComboOffset(int index)
    {
        if (comboDatas.Count == 0) { return 0; }
        if (comboDatas[index].comboOffset == 0) { Debug.LogWarning(index + "的索引值下没有设置连招的攻击距离偏差量"); }
        return comboDatas[index].comboOffset;
    }
    //public AudioClip  GetComboSound(int index)
    //{
    //    if (comboDates.Count == 0) { return null; }
    //    if (comboDates[index].weaponSound == null) { Debug.LogWarning(index + "的索引值下没有设置音效的"); }
    //    return comboDates[index].weaponSound;
    //}
    //public AudioClip GetCharacterVoice(int index)
    //{
    //    if (comboDates.Count == 0) { return null; }
    //    if (comboDates[index].characterVoice == null) { Debug.LogWarning(index + "的索引值下没有设置音效的"); }
    //    return comboDates[index].characterVoice;
    //}
    //public GameObject GetCharacterVoicePrefab(int index)
    //{
    //    if (comboDates.Count == 0) { return null; }
    //    if (comboDates[index].characterVoicePrefab == null) { Debug.LogWarning(index + "的索引值下没有设置音效的"); }
    //    return comboDates[index].characterVoicePrefab;
    //}

    //public GameObject GetComboSoundPrefab(int index)
    //{
    //    if (comboDates.Count == 0) { return null; }
    //    if (comboDates[index].weaponSoundPrefab == null) { Debug.LogWarning(index + "的索引值下没有设置音效的"); }
    //    return comboDates[index].weaponSoundPrefab;
    //}

    public string GetComboHitName(int index)
    {
        if (comboDatas.Count == 0) { return null; }
        if (comboDatas[index].hitName == null) { Debug.LogWarning(index + "的索引值下没有受伤名"); }
        return comboDatas[index].hitName;
    }
    public string GetComboParryName(int index)
    {
        if (comboDatas.Count == 0) { return null; }
        if (comboDatas[index].parryName == null) { Debug.LogWarning(index + "的索引值下没有格挡名"); }
        return comboDatas[index].parryName;
    }
    public int GetComboMaxCount()
    {
        if (comboDatas.Count == 0) { return 0; }
        return comboDatas.Count;
    } 
    public float GetComboDamage(int index)
    { 
        if(comboDatas.Count == 0) { return 0f; }
        if (comboDatas[index].comboDamage == 0) { Debug.LogWarning(index + "的索引值下没有伤害"); }
        return comboDatas[index].comboDamage;
    }

    public SoundStyle GetComboSoundStyle(int index)
    {
      
        if (comboDatas[index].comboDamage == 0) { Debug.LogWarning(index + "的索引值下没有设置音效Style"); }
        return comboDatas[index].universalSound;
    }

    public float GetComboShakeForce(int index, int ATKIndex)
    {
        //  Debug.Log("ATKIndex为" + ATKIndex);
        // Debug.Log("comboDates[index].shakeForce.Length为" + (comboDates[index].shakeForce.Length ));
      
        if (comboDatas[index].shakeForce == null||ATKIndex > comboDatas[index].shakeForce.Length )
        {
         //说明我不设置Force或者没有设置全Force，代表每该ATK都没有震屏
            return 0;
        }
        return comboDatas[index].shakeForce[ATKIndex-1];
    }
   
    public int GetComboATKCount(int index)
    {
        return comboDatas[index].ATKCount;
    }
    public float GetPauseFrameTime(int index,int ATKIndex)
    {
        if (comboDatas[index].pauseFrameTimeList== null|| ATKIndex > comboDatas[index].pauseFrameTimeList.Length)
        {
           return GetComboPauseFrameTime(index);
        }
       
        return comboDatas[index].pauseFrameTimeList[ATKIndex-1];

    }
    private float GetComboPauseFrameTime(int index)
    {
        return comboDatas[index].pauseFrameTime;
    }
}
