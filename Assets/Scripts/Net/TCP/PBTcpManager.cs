using GameProtocol;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PBTcpManager : Singleton<PBTcpManager>
{
    //返回给游戏的delegate
    public delegate void DelegateReceiveMessage<T>(T message);

    public DelegateReceiveMessage<TcpResponseLogin> mes_login_result { get; set; }
    public DelegateReceiveMessage<TcpResponseRequestMatch> mes_request_match_result { get; set; }
    public DelegateReceiveMessage<TcpResponseCancelMatch> mes_cancel_match_result { get; set; }
    public DelegateReceiveMessage<TcpEnterBattle> mes_enter_battle { get; set; }

    public void AnalyzeMessage(SCID messageId, byte[] bodyData)
    {

        Debug.Log("messageId  " + messageId);
        switch (messageId)
        {
            case SCID.TCP_RESPONSE_LOGIN:
                {
                    TcpResponseLogin pb_ReceiveMes = PackageHandler.DeserializeData<TcpResponseLogin>(bodyData);
                    NetGlobal.Instance.AddAction(() =>
                    {
                        mes_login_result(pb_ReceiveMes);
                    });
                }
                break;
            case SCID.TCP_RESPONSE_REQUEST_MATCH:
                {
                    TcpResponseRequestMatch pb_ReceiveMes = PackageHandler.DeserializeData<TcpResponseRequestMatch>(bodyData);
                    NetGlobal.Instance.AddAction(() =>
                    {
                        mes_request_match_result(pb_ReceiveMes);
                    });
                }
                break;
            case SCID.TCP_RESPONSE_CANCEL_MATCH:
                {
                    TcpResponseCancelMatch pb_ReceiveMes = PackageHandler.DeserializeData<TcpResponseCancelMatch>(bodyData);
                    NetGlobal.Instance.AddAction(() =>
                    {
                        mes_cancel_match_result(pb_ReceiveMes);
                    });
                }
                break;
            case SCID.TCP_ENTER_BATTLE:
                {
                    TcpEnterBattle pb_ReceiveMes = PackageHandler.DeserializeData<TcpEnterBattle>(bodyData);
                    NetGlobal.Instance.AddAction(() =>
                    {
                        mes_enter_battle(pb_ReceiveMes);
                    });
                }
                break;
            default:
                break;
        }

    }
}
