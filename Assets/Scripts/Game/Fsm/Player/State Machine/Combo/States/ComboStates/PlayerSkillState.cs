
using ZZZ;
using UnityEngine;

public class PlayerSkillState : PlayerComboState
{
    public PlayerSkillState(PlayerComboStateMachine comboStateMachine) : base(comboStateMachine)
    {
    }
    /// <summary>
    /// 通过输入事件和执行条件触发
    /// </summary>
    public override void Enter()
    {
        base.Enter();
        comboStateMachine.Player.movementStateMachine.ChangeState(comboStateMachine.Player.movementStateMachine.playerMovementNullState);
        // 按角色实例切换，避免同角色 ID 的其他玩家触发本地技能相机。
        CameraSwitcher.Instance.ActiveStateCamera(player, reusableData.currentSkill.attackStyle);

        if (reusableData.currentSkill.attackStyle == AttackStyle.FinishSkill) {
            player.PlayFinishSkillTimeline();
        }
      
    }

    public override void Update()
    {
        characterCombo.UpdateAttackLookAtEnemy();
    }
    public override void Exit()
    {
        CameraSwitcher.Instance.UnActiveStateCamera(player,reusableData.currentSkill.attackStyle);
        base.Exit();
      
    }
    /// <summary>
    /// 通过动画脚本触发：动画播放完退出
    /// </summary>
    public override void OnAnimationExitEvent()
    {
        comboStateMachine.ChangeState(comboStateMachine.NullState);
       
    }
    /// <summary>
    /// 进入switchout动画退出
    /// </summary>
    /// <param name="state"></param>
    public override void OnAnimationTranslateEvent(IState state)
    {
        comboStateMachine.ChangeState(state);
    }
}
