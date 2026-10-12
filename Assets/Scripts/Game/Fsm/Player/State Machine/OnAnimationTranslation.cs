using UnityEngine;
// Serialized compatibility component. Animator transitions never drive combat or the visual FSM.
public class OnAnimationTranslation : StateMachineBehaviour
{
    public enum OnEnterAnimationPlayerState { Idle, Walk, Run, Sprint, Dash, DashBack, TurnBack, Switch, SwitchOut, ATK, Null }
    [SerializeField] public OnEnterAnimationPlayerState onEnterAnimationState;
}
