
using UnityEngine;
namespace ZZZ
{
    [CreateAssetMenu(fileName ="Player",menuName ="Create/Character/Player")]
    
    public class PlayerSO:ScriptableObject
    {
        [Header("1v1 帧战斗")]
        public ZZZ.SkillAuthoring.SkillCombatProfile frameCombat;
        //field会字段显示在编辑面板上，哪怕是一个私有字段
        [field: SerializeField] public PlayerMovementData movementData{ get; private set; }

        [field: SerializeField] public PlayerComboData ComboData { get; private set; }
    }
}
