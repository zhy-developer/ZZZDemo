using UnityEngine;


namespace ZZZ
{
    public class PlayerComboStateMachine : StateMachine
    {
        public Player Player { get; }//只能在构造函数中修改{get;private set}只能在本类中修改
        public PlayerATKIngState ATKIngState { get; }
        public PlayerNullState NullState { get; }
        public PlayerComboReusableData ReusableData { get; }

        public PlayerSkillState SkillState { get; }
        public CharacterCombo Combo { get; }
        public PlayerComboStateMachine(Player player)
        {
            Player = player;

            ReusableData = new PlayerComboReusableData();
            Combo = new CharacterCombo(player.characterAnimator, player.transform, player.camera,
                ReusableData, player.playerSO.ComboData.comboData,
                player.playerSO.ComboData.playerEnemyDetectionData, player);

            ATKIngState =new PlayerATKIngState(this);

            NullState=new PlayerNullState(this);

            SkillState =new PlayerSkillState(this);
        }

    }
}
