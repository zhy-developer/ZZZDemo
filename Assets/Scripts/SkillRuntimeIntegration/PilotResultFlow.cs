using System;
using GameProtocol;
using MatchLifecycle;
using UnityEngine;

namespace SkillRuntime.Integration
{
    // Match-level owner survives accidental scene/Player unload while result confirmation is pending.
    public sealed class PilotResultFlow : MonoBehaviour
    {
        public static PilotResultFlow Active { get; private set; }
        static readonly System.Collections.Generic.HashSet<string> retiredMatches = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        public static bool WasRetired(string match) => match != null && retiredMatches.Contains(match);
        public ClientAgreement Agreement { get; } = new ClientAgreement();
        public bool Retired { get; private set; }
        public bool Leaving { get; private set; }
        public string Diagnostic { get; private set; }
        public string Match { get; private set; }
        PilotBattleSession session;
        Action confirmed, retireScheduling;
        int slot;
        long sessionKey;
        string digest;
        float nextRetry, waitingSince;
        bool applied;
        public static PilotResultFlow Open(PilotBattleSession session, Action onConfirmed, Action onRetire)
        {
            if (Active != null) throw new InvalidOperationException("Previous match has not retired.");
            var go = new GameObject("Pilot result session"); DontDestroyOnLoad(go);
            var flow = go.AddComponent<PilotResultFlow>(); Active = flow; flow.session = session; flow.confirmed = onConfirmed;
            flow.retireScheduling = onRetire;
            flow.Match = BattleData.Instance.MatchId; flow.slot = BattleData.Instance.battleID;
            flow.sessionKey = BattleData.Instance.SessionKey; flow.digest = session.Digest;
            UdpMessageDispatcher.Instance.mes_down_game_over += flow.Receive;
            return flow;
        }
        void Update()
        {
            if (Retired) return;
            if (session.TerminalSnapshot != null && Agreement.Local == null)
            { Agreement.LockLocal(session.TerminalSnapshot); waitingSince = Time.realtimeSinceStartup; }
            if (Agreement.Confirmed != null && !applied)
            {
                applied = true;
                Debug.Log("[RoundConfirmed] match=" + Match + " round=1 F=" + Agreement.Confirmed.Frame + " result=" + Agreement.Confirmed.Result +
                    " winner=" + Agreement.Confirmed.Winner + " hash=" + Agreement.Confirmed.Hash + " id=" + Agreement.Confirmed.ConfirmationId);
                confirmed?.Invoke();
            }
            if (Agreement.Local != null && (Agreement.Confirmed == null || Leaving) && Time.realtimeSinceStartup >= nextRetry)
            {
                nextRetry = Time.realtimeSinceStartup + .5f;
                UdpMessageDispatcher.Instance.SendFinal(new UdpUpGameOver { battleID = slot, result = ResultWire.Write(Agreement.Local),
                    retire = Leaving, confirmationId = Leaving ? Agreement.Confirmed.ConfirmationId : "" });
            }
        }
        void Receive(UdpDownGameOver message)
        {
            if (Retired || message.matchId != Match || message.roundId != 1) return;
            try
            {
                var result = ResultWire.Read(message.result);
                if (result == null || result.Match != Match || result.Session != sessionKey || result.Rules != PilotBattleProfile.Rules || result.Config != digest)
                    throw new InvalidOperationException("Invalid confirmation scope/version/config.");
                var status = (AgreementStatus)message.status;
                if (status == AgreementStatus.Confirmed)
                {
                    if (message.confirmationId != result.ConfirmationId) throw new InvalidOperationException("Invalid confirmation identity.");
                    Agreement.Receive(result);
                }
                else if (status == AgreementStatus.Retired && Leaving && Agreement.Confirmed != null && Agreement.Confirmed.Equals(result) && message.confirmationId == result.ConfirmationId)
                {
                    Retired = true;
                    retiredMatches.Add(Match);
                    Debug.Log("[RoundRetired] match=" + Match + " round=1 slot=" + slot);
                    Release();
                    ClearSceneData.LoadScene(GameConfig.MAIN_SCENE);
                    Destroy(gameObject);
                }
                else if (status == AgreementStatus.Conflict || status == AgreementStatus.Rejected)
                { Diagnostic = message.diagnostic; Debug.LogError("[RoundDispute] match=" + Match + " " + Diagnostic); }
            }
            catch (Exception e) { Diagnostic = e.Message; Debug.LogError("[RoundDispute] " + e.Message); }
        }
        public void RequestReturn()
        {
            if (Retired || Leaving || Agreement.Confirmed == null || Agreement.Diagnostic != null) return;
            Leaving = true; nextRetry = 0;
        }
        void OnGUI()
        {
            if (Retired || Agreement.Local == null && Agreement.Confirmed == null) return;
            GUILayout.BeginArea(new Rect(10, 135, 740, 135), GUI.skin.box);
            GUILayout.Label(Agreement.Confirmed == null ? "本地终局已成立，等待双方结果确认（尚未计分）" :
                "服务端已确认：" + Agreement.Confirmed.Result + " / winner=" + Agreement.Confirmed.Winner);
            if (Diagnostic != null || Agreement.Diagnostic != null) GUILayout.Label("结果异常，保留证据，不判胜负：" + (Diagnostic ?? Agreement.Diagnostic));
            else if (Agreement.Confirmed == null && Time.realtimeSinceStartup - waitingSince > 15) GUILayout.Label("确认等待超过15秒，继续重试；没有自动判负或判平规则。");
            if (Agreement.Confirmed != null && GUILayout.Button(Leaving ? "等待离场确认…" : "返回主界面（可重新匹配）")) RequestReturn();
            GUILayout.EndArea();
        }
        void Release()
        {
            var stop = retireScheduling; retireScheduling = null; stop?.Invoke();
            UdpMessageDispatcher.Instance.mes_down_game_over -= Receive;
            session?.Dispose(); confirmed = null;
            UdpMessageDispatcher.Instance.Destory(); BattleData.Instance.ClearData();
            if (Active == this) Active = null;
        }
        void OnDestroy()
        {
            if (Active != this) return;
            Debug.LogWarning("[RoundSession] application/session owner destroyed before normal retirement; no result fabricated.");
            Release();
        }
    }
}
