using UnityEngine;

namespace SkillConfig.Editor.Tests
{
    // Test sentinel only; never installed by the production proxy builder.
    [ExecuteAlways]
    public sealed class SkillPreviewSideEffectProbe : MonoBehaviour
    {
        public static int LifecycleCalls, EventCalls;
        void Awake() { LifecycleCalls++; }
        void OnEnable() { LifecycleCalls++; }
        public void Attack() { EventCalls++; }
    }
}
