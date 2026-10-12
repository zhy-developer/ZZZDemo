using UnityEngine;

// Health display mirror. Only BattleWorld decides damage, defense and death.
public class CharacterHealthBase : MonoBehaviour
{
    [SerializeField] protected CharacterHealthInfo characterHealthInfo;
    protected CharacterHealthInfo healthInfo;
    [SerializeField] float currentHP;
    public bool IsDead => healthInfo != null && healthInfo.onDead.Value;
    public bool IsDeterministicPilot { get; private set; }
    public float DisplayHP => healthInfo ? healthInfo.currentHP.Value : 0;
    public float DisplayMaxHP => healthInfo ? healthInfo.maxHP : 0;
    protected virtual void Awake()
    {
        if (characterHealthInfo && !healthInfo) ResetHealthForSpawn();
    }
    public void ResetHealthForSpawn()
    {
        if (IsDeterministicPilot) throw new System.InvalidOperationException("A bound view must consume a battle snapshot.");
        if (!healthInfo) healthInfo = characterHealthInfo ? Instantiate(characterHealthInfo) : ScriptableObject.CreateInstance<CharacterHealthInfo>();
        healthInfo.InitHealthData(); currentHP = healthInfo.currentHP.Value;
    }
    public void InitializePilotHealth(int maxHP)
    {
        if (!healthInfo) healthInfo = ScriptableObject.CreateInstance<CharacterHealthInfo>();
        IsDeterministicPilot = true; ApplySnapshot(maxHP, maxHP);
    }
    public void ApplySnapshot(long hp, int maxHP)
    {
        if (hp < 0 || hp > maxHP || maxHP > 16777216) throw new System.ArgumentOutOfRangeException(nameof(hp));
        bool changed = DisplayHP != hp || DisplayMaxHP != maxHP || IsDead != (hp == 0);
        healthInfo.maxHP = maxHP;
        healthInfo.currentHP.SetWithoutNotify((float)hp);
        healthInfo.onDead.SetWithoutNotify(hp == 0);
        currentHP = (float)hp;
        if (changed) { healthInfo.currentHP.NotifyCurrentValue(); healthInfo.onDead.NotifyCurrentValue(); }
    }
    protected virtual void OnDestroy() { if (healthInfo) Destroy(healthInfo); }
}
