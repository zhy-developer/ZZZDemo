using UnityEngine;

namespace SkillRuntime.Integration
{
    // Saved on BattleScene's GameManager. There is one combat authority.
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "SkillRuntime.Integration", "Assembly-CSharp", "PilotMatchSettings")]
    public sealed class BattleMatchSettings : MonoBehaviour
    {
        public BattleAuthorityMode mode = BattleAuthorityMode.Deterministic;
        public BattleProfile profile;
        public BattleProfileSnapshot Freeze()
        {
            if (mode != BattleAuthorityMode.Deterministic) throw new System.InvalidOperationException("Legacy combat was removed. Select Deterministic and assign a profile.");
            if (!profile) throw new System.InvalidOperationException("Deterministic selected without a profile; no Legacy fallback.");
            return profile.ValidateAndFreeze();
        }
        void OnGUI()
        {
            var session = GetComponent<RoleManager>()?.Session;
            if (session == null) return;
            GUILayout.BeginArea(new Rect(10, 10, 720, 115), GUI.skin.box);
            GUILayout.Label((profile && profile.normalGameplay ? "战斗系统" : "DEVELOPMENT Deterministic / test HP and HurtBox") + " / " + session.TerminalResult);
            if (profile && profile.normalGameplay) GUILayout.Label(profile.HasExpandedActions
                ? "左键：普攻／闪避普攻  E：技能  Q：终结技  Shift：有方向前闪，无方向后闪"
                : "当前配置仅开放普通攻击第1～5段。");
            for (int id = 1; id <= 2; id++)
            {
                var health = session.Latest.Actors[id - 1];
                GUILayout.Label("P" + id + " HP " + health.HP + "/" + health.MaxHP + (health.IsDead ? " KO" : ""));
            }
            GUILayout.Label(session.Faulted ? session.Fault : session.LastStateHash);
            GUILayout.EndArea();
        }
    }
}
