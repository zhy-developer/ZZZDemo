using UnityEngine;

namespace ZZZ
{
    [System.Serializable]
    public class PlayerComboData
    {
        [field: SerializeField, Header("招式配置")] public PlayerComboSOData comboData { get; private set; }

        [field: SerializeField, Header("敌人检测")] public PlayerEnemyDetectionData playerEnemyDetectionData { get; private set;}

    

    }
}

