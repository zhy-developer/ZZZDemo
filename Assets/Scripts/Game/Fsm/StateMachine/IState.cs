
namespace ZZZ
{
   
    public interface IState 
    {
        /// <summary>
        /// 状态机的状态接口，那么每个状态都应该有进入、退出、动画参数更新、状态更新、动画事件等方法。
        /// </summary>
        public void Enter();

        public void Exit();

        public void UpdateAnimationParameters();

        public void Update();

        public void OnAnimationTranslateEvent(IState state);

        public void OnAnimationExitEvent();
    }
}
