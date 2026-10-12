using UnityEngine;

namespace SkillRuntime.Integration
{
    // Saved on BattleScene's GameManager. Legacy is an explicit per-match rollback option.
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
            GUILayout.Label((profile && profile.normalGameplay ? "新战斗系统 · 星见雅移动 / Normal1–5" : "DEVELOPMENT DeterministicPilot / test HP and HurtBox") + " / " + session.TerminalResult);
            if (profile && profile.normalGameplay) GUILayout.Label("Normal6、闪避攻击、Skill、FinishSkill 尚未开放；第五段结束后可重新普攻。");
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
