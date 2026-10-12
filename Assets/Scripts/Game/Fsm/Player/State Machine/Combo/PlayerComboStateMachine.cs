namespace ZZZ
{
    public enum PlayerRenderAction { None, Attack, Hit, Dead, Skill, Dodge }
    public struct PlayerRenderFrame
    {
        public PlayerRenderAction Action;
        public string Animation, IdleState;
        public float NormalizedTime;
        public long CastSequence;
        public string AnimationSegment;
        public float ClipLength, PlaybackSpeed, EndNormalizedTime;
        public bool Moving;
    }
    // The old combo FSM now selects visual states exclusively from committed snapshots.
    public sealed class PlayerComboStateMachine : StateMachine
    {
        public Player Player { get; }
        public PlayerRenderFrame Frame { get; private set; }
        readonly PlayerNullState idle;
        readonly PlayerATKIngState attack;
        readonly PlayerHitState hit;
        readonly PlayerDeadState dead;
        readonly PlayerSkillViewState skill;
        readonly PlayerDodgeViewState dodge;
        public PlayerComboStateMachine(Player player)
        {
            Player = player; 
            idle = new PlayerNullState(this);
            attack = new PlayerATKIngState(this); 
            hit = new PlayerHitState(this); 
            dead = new PlayerDeadState(this);
            skill = new PlayerSkillViewState(this); 
            dodge = new PlayerDodgeViewState(this); }
        public void Apply(PlayerRenderFrame frame)
        {
            Frame = frame;
            switch (frame.Action)
            {
                case PlayerRenderAction.Attack: ChangeState(attack); break;
                case PlayerRenderAction.Hit: ChangeState(hit); break;
                case PlayerRenderAction.Dead: ChangeState(dead); break;
                case PlayerRenderAction.Skill: ChangeState(skill); break;
                case PlayerRenderAction.Dodge: ChangeState(dodge); break;
                default: ChangeState(idle); break;
            }
            Update();
        }
    }
}
