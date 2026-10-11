using UnityEngine;

namespace SkillRuntime.Integration
{
    // Add to the GameManager object in a DEVELOPMENT battle scene. Absent/default means Legacy.
    public sealed class PilotMatchSettings : MonoBehaviour
    {
        public BattleAuthorityMode mode = BattleAuthorityMode.Legacy;
        public PilotBattleProfile profile;
        public PilotConfiguration Freeze()
        {
            if (mode == BattleAuthorityMode.Legacy) return null;
            if (!profile) throw new System.InvalidOperationException("DeterministicPilot selected without a profile; no Legacy fallback.");
            return profile.ValidateAndFreeze();
        }
        void OnGUI()
        {
            var session = GetComponent<RoleManager>()?.Pilot;
            if (session == null) return;
            GUILayout.BeginArea(new Rect(10, 10, 720, 115), GUI.skin.box);
            GUILayout.Label("DEVELOPMENT DeterministicPilot / test HP and HurtBox / " + session.TerminalResult);
            for (int id = 1; id <= 2; id++)
            {
                var health = session.Health(id);
                GUILayout.Label("P" + id + " HP " + health.DisplayHP + "/" + health.DisplayMaxHP + (health.IsDead ? " KO" : ""));
            }
            GUILayout.Label(session.Faulted ? session.Fault : session.LastStateHash);
            GUILayout.EndArea();
        }
    }
}
