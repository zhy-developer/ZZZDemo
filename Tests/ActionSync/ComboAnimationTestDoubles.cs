// Minimal animation snapshots for testing the production combo completion policy.
// These tests do not simulate Unity's animation evaluation or event delivery.
namespace UnityEngine
{
    public struct AnimatorStateInfo
    {
        public string Tag;
        public string Name;
        public bool IsTag(string tag) => Tag == tag;
        public bool IsName(string name) => Name == name;
    }
    public sealed class Animator
    {
        public AnimatorStateInfo Current;
        public AnimatorStateInfo Next;
        public bool Transition;
        public bool IsInTransition(int layer) => Transition;
        public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layer) => Current;
        public AnimatorStateInfo GetNextAnimatorStateInfo(int layer) => Next;
    }
}
namespace ZZZ
{
    public sealed class PlayerComboReusableData { }
    public sealed class PlayerComboData { }
    public sealed class PlayerSO { public PlayerComboData ComboData = new PlayerComboData(); }
    public sealed class Player
    {
        public UnityEngine.Animator characterAnimator = new UnityEngine.Animator();
        public PlayerSO playerSO = new PlayerSO();
        public bool IsPlayingAnimationTag(string tag)
        {
            var state = characterAnimator.Transition ? characterAnimator.Next : characterAnimator.Current;
            return state.IsTag(tag) || state.IsName(tag);
        }
    }
    public sealed class CharacterCombo
    {
        public void AddEventAction() { }
        public void RemoveEventActon() { }
        public void UpdateComboAnimation() { }
        public void CheckCanLinkCombo() { }
    }
    public sealed class PlayerComboStateMachine : StateMachine
    {
        public Player Player = new Player();
        public PlayerComboReusableData ReusableData = new PlayerComboReusableData();
        public CharacterCombo Combo = new CharacterCombo();
    }
    public sealed class ComboCompletionProbe : PlayerComboState
    {
        public ComboCompletionProbe(PlayerComboStateMachine machine) : base(machine) { }
        public bool Finished(string tag) => HasAnimationFinished(tag);
    }
}
