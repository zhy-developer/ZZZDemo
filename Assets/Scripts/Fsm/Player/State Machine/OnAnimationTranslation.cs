using System.Collections;
using System.Collections.Generic;
using ZZZ;
using UnityEngine;

public class OnAnimationTranslation : StateMachineBehaviour
{
      Player player;
    public enum OnEnterAnimationPlayerState
    { 
     Idle,
     Walk,
     Run,
     /// <summary>
     /// 冲刺
     /// </summary>
     Sprint,
     /// <summary>
     /// 往前闪
     /// </summary>
     Dash,
     /// <summary>
     /// 往后山
     /// </summary>
     DashBack,
     /// <summary>
     /// 掉头
     /// </summary>
     TurnBack,
     /// <summary>
     /// 切入
     /// </summary>
     Switch,
     /// <summary>
     /// 切出
     /// </summary>
     SwitchOut,
     /// <summary>
     /// 攻击
     /// </summary>
     ATK,
     Null
    
    }
   [SerializeField] public OnEnterAnimationPlayerState onEnterAnimationState;
      override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (onEnterAnimationState == OnEnterAnimationPlayerState.Null)
        {
            return;
        }
        if (animator.TryGetComponent<Player>(out player))
        {
            player.OnAnimationTranslateEvent(onEnterAnimationState);
        }
    }

   
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (animator.TryGetComponent<Player>(out player))
        {
            player.OnAnimationExitEvent();
        }
    }
}
