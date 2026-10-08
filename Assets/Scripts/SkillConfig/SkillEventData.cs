using System;

namespace SkillConfig
{
    public enum EventExecution { Presentation, Authoritative }
    public enum EventParameterKind { Unspecified, Marker, ComboGate }
    [Serializable] public sealed class MarkerParameters { public string text; }
    [Serializable] public sealed class ComboGateParameters { public bool enabled; }
    [Serializable] public sealed class SkillEvent
    {
        public string id, trackId, eventId;
        public int frame, order, version = 1;
        public EventExecution execution;
        public EventParameterKind parameterKind;
        public MarkerParameters marker;
        public ComboGateParameters comboGate;
    }
    // Definition registry only; intentionally contains no gameplay delegates.
    public static class SkillEventRegistry
    {
        public static bool IsValid(SkillEvent value)
        {
            if (value == null || value.version != 1) return false;
            switch (value.eventId)
            {
                case "presentation.marker":
                    return value.execution == EventExecution.Presentation && value.parameterKind == EventParameterKind.Marker && value.marker != null && (value.comboGate == null || !value.comboGate.enabled);
                case "combo.preinput":
                case "combo.attack-ready":
                case "combo.link":
                case "combo.move-interrupt":
                    return value.execution == EventExecution.Authoritative && value.parameterKind == EventParameterKind.ComboGate && value.comboGate != null && (value.marker == null || string.IsNullOrEmpty(value.marker.text));
                default: return false;
            }
        }
    }
}
