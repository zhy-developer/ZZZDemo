using GameProtocol;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MatchManager : MonoSingleton<MatchManager>
{
    private void OnEnable()
    {
        PBTcpManager.Instance.mes_request_match_result += OnResponseMatchRequestResult;
        PBTcpManager.Instance.mes_cancel_match_result += OnResponseMatchCancelResult;
    }

    private void OnDisable()
    {
        PBTcpManager.Instance.mes_request_match_result -= OnResponseMatchRequestResult;
        PBTcpManager.Instance.mes_cancel_match_result -= OnResponseMatchCancelResult;
    }

    public void SendRequestMatchRequest() {
        TcpRequestMatch mes = new TcpRequestMatch();
        mes.uid = NetGlobal.Instance.userUid;
        mes.roleId = (int)GameBlackboard.Instance.GetGameData<CharacterNameList>(GameConfig.ROLE_CURRENTNAME);
        ClientTCP.Instance.SendMessage(PackageHandler.PackSendMessage(mes,GameProtocol.CSID.TCP_REQUEST_MATCH));
    }

    public void SendCancelMatchRequest() { 
    
    }
    private void OnResponseMatchRequestResult(TcpResponseRequestMatch message)
    {
        ClearSceneData.LoadScene(GameConfig.BATTLE_SCENE);
    }

    private void OnResponseMatchCancelResult(TcpResponseCancelMatch message)
    {
        throw new NotImplementedException();
    }
}
