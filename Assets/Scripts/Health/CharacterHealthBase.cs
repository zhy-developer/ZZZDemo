using Tools;
using UnityEngine;
using ZZZ;
using static UnityEngine.Rendering.DebugUI;

public class CharacterHealthBase : MonoBehaviour
{
    //public StateBarUI stateBarUI;
    [SerializeField] private float currentHP;
    [SerializeField] private float currentStrength;
    [SerializeField] private float currentDefenseValue;
    protected Transform currentEnemy;
    [SerializeField]protected CharacterHealthInfo characterHealthInfo;
    protected CharacterHealthInfo healthInfo;
    // Read the runtime instance, not the shared ScriptableObject asset.
    public bool IsDead => healthInfo != null && healthInfo.onDead.Value;
    protected Animator animator;
    protected virtual void Awake()
    {
        // A pool may have already prepared this instance while its GameObject was inactive.
        if (healthInfo == null)
        {
            ResetHealthForSpawn();
        }
    }

    private void InitializeHealth()
    {
        if (healthInfo != null) { return; }
        animator = GetComponent<Animator>();
        healthInfo = Instantiate(characterHealthInfo);
        healthInfo.currentHP.OnValueChanged += OnUpdatePH;
        healthInfo.currentStrength.OnValueChanged += OnUpdateStrength;
        healthInfo.currentDefenseValue.OnValueChanged += OnUpdateDefenseValue;
    }

    /// <summary>Start a new life explicitly; ordinary enable/disable preserves health.</summary>
    public void ResetHealthForSpawn()
    {
        InitializeHealth();
        // Clear before value notifications so a previous attacker cannot trigger defense reactions.
        currentEnemy = null;
        healthInfo.InitHealthData();
        // Equal bindable values do not notify, but the Inspector mirrors must still be current.
        currentHP = healthInfo.currentHP.Value;
        currentStrength = healthInfo.currentStrength.Value;
        currentDefenseValue = healthInfo.currentDefenseValue.Value;
    }
    protected virtual void Update()
    {
        LookAtAttacker();
    }
    protected virtual void OnEnable()
    {
        GameEventsManager.Instance.AddEventListening<float,string,string,Transform,Transform, ZZZ.CharacterComboBase>("触发伤害", OnCharacterHitEventHandler);

        GameEventsManager.Instance.AddEventListening<float>("生成伤害", OnCharacterDamageAction);

    }

 

    protected virtual void OnDisable()
    {
        GameEventsManager.Instance.ReMoveEvent<float, string, string, Transform, Transform, ZZZ.CharacterComboBase>("触发伤害", OnCharacterHitEventHandler);

        GameEventsManager.Instance.ReMoveEvent<float>("生成伤害", OnCharacterDamageAction);

    }

    protected virtual void OnDestroy()
    {
        if (healthInfo == null) { return; }
        healthInfo.currentHP.OnValueChanged -= OnUpdatePH;
        healthInfo.currentStrength.OnValueChanged -= OnUpdateStrength;
        healthInfo.currentDefenseValue.OnValueChanged -= OnUpdateDefenseValue;
        Destroy(healthInfo);
        healthInfo = null;
    }

  
    //事件注册
    private void OnCharacterHitEventHandler(float Damage, string HitName, string ParryName, Transform Attacker, Transform Bearer, ZZZ.CharacterComboBase characterCombo)
    {
      
        if (Bearer != this.transform || IsDead) { return; }
        SetEnemy(Attacker);
        CharacterHitAction(Damage, HitName, ParryName);
        OnCharacterDamageAction(Damage);
        SetHitFVX(Attacker,Bearer);
        SetHitSFX(characterCombo.player.characterName);
    }

 
    protected void OnCharacterDamageAction(float damage)
    {
        if (IsDead) { return; }
        healthInfo.TakeDamage(damage);
    }
    protected void CharacterStrengthAction(float damage)
    {
        healthInfo.TakeStrength(damage);
    }

    protected virtual void CharacterHitAction(float damage, string hitName, string parryName)
    {
        
    }

    protected virtual void SetEnemy(Transform attacker)
    {
        if (currentEnemy != attacker || currentEnemy == null)
        { 
            currentEnemy = attacker;    
        }
    }
    private void LookAtAttacker()
    {
        if (currentEnemy == null || IsDead) { return; }
        if (animator.AnimationAtTag("Hit")&&animator.GetCurrentAnimatorStateInfo(0).normalizedTime<0.3f)
        {
            transform.Look(currentEnemy.position, 50);
        }
        
    
    }
    protected void SetHitFVX(Transform attacker,Transform hitter)
    { 
        Vector3 hitDir = (attacker.position- hitter.position).normalized;
        Vector3 targetPos = hitter.position + hitDir*0.8f+Vector3.up*1f;
        VFX_PoolManager.Instance.GetVFX(CharacterNameList.Enemy,"Hit", targetPos);
    }
    protected virtual void SetHitSFX(CharacterNameList characterNameList)
    {
       
    }

    private void OnUpdatePH(float value)
    {
        Debug.Log("敌人的血量为"+value);
        currentHP = value;
        if (value > 0)
        {
            healthInfo.onDead.Value = false;
            //stateBarUI.UpdateBlood(currentHP / healthInfo.maxHP);
            return;
        }
        healthInfo.onDead.Value = true;
    }
    private void OnUpdateStrength(float value)
    {
        currentStrength = value;
        if (value > 0)
        {
            healthInfo.hasStrength.Value = true;
            return;
        }
        healthInfo.hasStrength.Value = false;
    }
    protected virtual void OnUpdateDefenseValue(float value)
    {
        currentDefenseValue = value;
    }

}
