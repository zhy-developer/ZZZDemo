using ZZZ;

public class PlayerSkillState : PlayerComboState
{
    public PlayerSkillState(PlayerComboStateMachine stateMachine) : base(stateMachine) { }
    public override void Enter()
    {
        base.Enter();
        player.movementStateMachine.ChangeState(player.movementStateMachine.playerMovementNullState);
        if (!player.IsLocalPlayer) return;
        CameraSwitcher.Instance.ActiveStateCamera(player, reusableData.currentSkill.attackStyle);
        if (reusableData.currentSkill.attackStyle == AttackStyle.FinishSkill) player.PlayFinishSkillTimeline();
    }
    public override void Update()
    {
        if (HasAnimationFinished("Skill"))
        {
            comboStateMachine.ChangeState(comboStateMachine.NullState);
            player.movementStateMachine.ReturnToLocomotion();
        }
    }
    public override void Exit()
    {
        if (player.IsLocalPlayer && reusableData.currentSkill != null)
            CameraSwitcher.Instance.UnActiveStateCamera(player, reusableData.currentSkill.attackStyle);
        base.Exit();
    }
}
