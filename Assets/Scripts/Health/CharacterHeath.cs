
using UnityEngine;
using ZZZ;
public class CharacterHeath : CharacterHealthBase
{
    /// <summary>
    /// 敌人的伤害处理
    /// </summary>
    /// <param name="damage"></param>
    /// <param name="hitName"></param>
    /// <param name="parryName"></param>
    protected override void CharacterHitAction(float damage, string hitName, string parryName)
    {
        base.CharacterHitAction(damage, hitName, parryName);
        // Abort the AI action before its animation is replaced by Hit or Parry.
        GetComponent<EnemyAIMovementController>()?.NotifyHitReaction();
        if (healthInfo.onDead.Value) {
            return;
        }

        if (healthInfo.hasStrength.Value)//格挡
        {
            healthInfo.TakeStrength(damage);
            animator.CrossFadeInFixedTime(parryName, 0.1f, 0);
           // SFX_PoolManager.MainInstance.TryGetSoundPool("PARRY",transform.position,Quaternion.identity);
            
        }
        else//挨打
        {
         
            healthInfo.TakeDamage(damage);
            animator.CrossFadeInFixedTime(hitName, 0.1f, 0);
            //SFX_PoolManager.MainInstance.TryGetSoundPool("HIT", transform.position, Quaternion.identity);
        }
        healthInfo.TakeDefenseValue(damage);
    }

    /// <summary>
    /// 处理敌人的失衡逻辑   
    /// </summary>
    /// <param name="value"></param>
    protected override void OnUpdateDefenseValue(float value)
    {
        base.OnUpdateDefenseValue(value);
        if (currentEnemy == null) { return; }
        if (value <= 0)
        {
            GameEventsManager.Instance.CallEvent("达到QTE条件", currentEnemy);
            healthInfo.ReDefenseValue();
            return;
        }
    }
    //处理受击音效
    protected override void SetHitSFX(CharacterNameList characterNameList)
    {

        SFX_PoolManager.Instance.TryGetSoundPool(SoundStyle.HIT, characterNameList.ToString(), transform.position);
    }
}
