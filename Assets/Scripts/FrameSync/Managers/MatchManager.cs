using GameProtocol;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MatchManager : MonoSingleton<MatchManager>
{
    private void OnEnable()
    {
        TcpMessageDispatcher.Instance.mes_request_match_result += OnResponseMatchRequestResult;
        TcpMessageDispatcher.Instance.mes_cancel_match_result += OnResponseMatchCancelResult;
        TcpMessageDispatcher.Instance.mes_enter_battle += OnResponseEnterBattle;
    }

    private void OnDisable()
    {
        TcpMessageDispatcher.Instance.mes_request_match_result -= OnResponseMatchRequestResult;
        TcpMessageDispatcher.Instance.mes_cancel_match_result -= OnResponseMatchCancelResult;
        TcpMessageDispatcher.Instance.mes_enter_battle -= OnResponseEnterBattle;
    }

    public void SendRequestMatchRequest() {
        TcpRequestMatch mes = new TcpRequestMatch();
        mes.uid = NetGlobal.Instance.userUid;
        mes.roleId = (int)GameBlackboard.Instance.GetGameData<CharacterNameList>(GameConfig.ROLE_CURRENTNAME);
        TcpClientConnection.Instance.SendMessage(PackageHandler.PackSendMessage(mes,GameProtocol.CSID.TCP_REQUEST_MATCH));
    }

    private void OnResponseMatchRequestResult(TcpResponseRequestMatch message)
    {
        //ClearSceneData.LoadScene(GameConfig.BATTLE_SCENE);
        DeLogger.LogTrace("匹配成功,等待对手进入");
    }

    private void OnResponseMatchCancelResult(TcpResponseCancelMatch message)
    {
        throw new NotImplementedException();
    }

    private void OnResponseEnterBattle(TcpEnterBattle message)
    {
        if (SkillRuntime.Integration.PilotResultFlow.WasRetired(message.matchId)) return;
        if (BattleData.Instance.MatchId == message.matchId) return; // duplicate enter cannot initialize twice
        if (SkillRuntime.Integration.PilotResultFlow.Active != null)
        { Debug.LogError("Cannot enter another match while previous result scope is retained."); return; }
        BattleData.Instance.SetScope(message);
        DeLogger.LogNoticeTrace(message.battleUserInfo + "进入战场");
        BattleData.Instance.UpdateBattleInfo(message.randSeed, message.battleUserInfo);
        ClearSceneData.LoadScene(GameConfig.BATTLE_SCENE);
    }
}
