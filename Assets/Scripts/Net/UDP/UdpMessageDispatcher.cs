using GameProtocol;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class UdpMessageDispatcher : Singleton<UdpMessageDispatcher>
{
    //返回给游戏的delegate
    public delegate void DelegateReceiveMessage<T>(T message);

    public DelegateReceiveMessage<UdpBattleStart> mes_battle_start { get; set; }
    public DelegateReceiveMessage<UdpDownFrameOperations> mes_frame_operation { get; set; }
    public DelegateReceiveMessage<UdpDownDeltaFrames> mes_delta_frame_data { get; set; }
    public DelegateReceiveMessage<UdpDownGameOver> mes_down_game_over { get; set; }

    private UdpClientTransport _upd;
    private int mesNum = 0; // 计数器

    public void Destory()
    {
        if (_upd != null)
        {
            _upd.EndClientUdp();
            _upd = null;
        }

        mes_frame_operation = null;
        mes_delta_frame_data = null;
        mes_down_game_over = null;
    }
    public void MyDestory()
    {
        if (_upd != null)
        {
            _upd.MyEndClientUdp();
            _upd = null;
        }

    }


    public void StartClientUdp()
    {
        mesNum = 0;
        _upd = new UdpClientTransport();
        _upd.StartClientUdp(NetGlobal.Instance.serverIP);

        _upd.delegate_analyze_message = AnalyzeMessage;
    }


    public void SendBattleReady(int _uid, int _battleID)
    {
        UdpBattleReady _ready = new UdpBattleReady();
        _ready.uid = _uid;
        _ready.battleID = _battleID;
        Debug.Log("SendBattleReady   _upd== " + _upd);
        _upd.SendMessage(PackageHandler.PackSendMessage<UdpBattleReady>(_ready, CSID.UDP_BATTLE_READY));
    }

    // 发送玩家操作
    public void SendOperation()
    {
        mesNum++;

        UdpUpPlayerOperations _up = new UdpUpPlayerOperations();
        _up.mesID = mesNum;  // 操作序列号

        _up.operation = BattleData.Instance.selfOperation;

        _upd.SendMessage(PackageHandler.PackSendMessage<UdpUpPlayerOperations>(_up, CSID.UDP_UP_PLAYER_OPERATIONS));
    }


    public PlayerOperation ClonePlayerOperation()
    {
        PlayerOperation _operation = new PlayerOperation();
        _operation.battleID = BattleData.Instance.selfOperation.battleID;
        _operation.move = BattleData.Instance.selfOperation.move;
        _operation.operationID = BattleData.Instance.selfOperation.operationID;
        _operation.rightOperation = BattleData.Instance.selfOperation.rightOperation;
        _operation.operationValue1 = BattleData.Instance.selfOperation.operationValue1;
        _operation.operationValue2 = BattleData.Instance.selfOperation.operationValue2;
        return _operation;
    }

    public void SendDeltaFrames(int _battleID, List<int> _frames)
    {

        Debug.Log("SendDeltaFrames 111111111111111111111 ");
        UdpUpDeltaFrames _framespb = new UdpUpDeltaFrames();

        _framespb.battleID = _battleID;
        _framespb.frames.AddRange(_frames);

        _upd.SendMessage(PackageHandler.PackSendMessage<UdpUpDeltaFrames>(_framespb, CSID.UDP_UP_DELTA_FRAMES));
    }


    public void SendGameOver(int _battleID)
    {
        UdpUpGameOver _gameOver = new UdpUpGameOver();
        _gameOver.battleID = _battleID;
        _upd.SendMessage(PackageHandler.PackSendMessage<UdpUpGameOver>(_gameOver, CSID.UDP_UP_GAME_OVER));
    }


    public void AnalyzeMessage(SCID messageId, byte[] bodyData)
    {


        switch (messageId)
        {
            case SCID.UDP_BATTLE_START:
                {
                    UdpBattleStart pb_ReceiveMes = PackageHandler.DeserializeData<UdpBattleStart>(bodyData);
                    NetGlobal.Instance.AddAction(() =>
                    {
                        mes_battle_start(pb_ReceiveMes);
                    });
                }
                break;
            case SCID.UDP_DOWN_FRAME_OPERATIONS: //UDP_UP_PLAYER_OPERATIONS
                {
                    UdpDownFrameOperations pb_ReceiveMes = PackageHandler.DeserializeData<UdpDownFrameOperations>(bodyData);
                    NetGlobal.Instance.AddAction(() =>
                    {
                        mes_frame_operation(pb_ReceiveMes);
                    });
                }
                break;
            case SCID.UDP_DOWN_DELTA_FRAMES:
                {
                    UdpDownDeltaFrames pb_ReceiveMes = PackageHandler.DeserializeData<UdpDownDeltaFrames>(bodyData);
                    NetGlobal.Instance.AddAction(() =>
                    {
                        mes_delta_frame_data(pb_ReceiveMes);
                    });
                }
                break;
            case SCID.UDP_DOWN_GAME_OVER:
                {
                    UdpDownGameOver pb_ReceiveMes = PackageHandler.DeserializeData<UdpDownGameOver>(bodyData);
                    NetGlobal.Instance.AddAction(() =>
                    {
                        mes_down_game_over(pb_ReceiveMes);
                    });
                }
                break;
            default:
                break;
        }
    }
}
