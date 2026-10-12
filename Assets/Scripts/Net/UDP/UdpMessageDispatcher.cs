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
    private int generation;

    public void Destory()
    {
        generation++;
        if (_upd != null)
        {
            _upd.EndClientUdp();
            _upd = null;
        }

        mes_frame_operation = null;
        mes_battle_start = null;
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
        generation++;
        mesNum = 0;
        _upd = new UdpClientTransport();
        int epoch = generation;
        _upd.delegate_analyze_message = (id, bytes) => AnalyzeMessage(epoch, id, bytes);
        _upd.StartClientUdp(NetGlobal.Instance.serverIP);
    }


    public void SendBattleReady(int _uid, int _battleID)
    {
        UdpBattleReady _ready = new UdpBattleReady();
        _ready.uid = _uid;
        _ready.battleID = _battleID;
        var data = BattleData.Instance;
        _ready.matchId = data.MatchId; _ready.roundId = data.RoundId; _ready.protocolVersion = 1; _ready.joinTicket = data.JoinTicket;
        var session = BattleManager.roleManage?.Session;
        if (session == null) return;
        _ready.authorityMode = 1;
        _ready.rules = SkillRuntime.Integration.BattleProfile.Rules;
        _ready.configHash = session.Digest;
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
        _up.matchId = BattleData.Instance.MatchId; _up.roundId = BattleData.Instance.RoundId;

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
        _framespb.matchId = BattleData.Instance.MatchId; _framespb.roundId = BattleData.Instance.RoundId;
        _framespb.frames.AddRange(_frames);

        _upd.SendMessage(PackageHandler.PackSendMessage<UdpUpDeltaFrames>(_framespb, CSID.UDP_UP_DELTA_FRAMES));
    }


    public void SendFinal(UdpUpGameOver result) => _upd?.SendMessage(PackageHandler.PackSendMessage(result, CSID.UDP_UP_GAME_OVER));


    public void AnalyzeMessage(SCID messageId, byte[] bodyData)
    {
        AnalyzeMessage(generation, messageId, bodyData);
    }
    void AnalyzeMessage(int epoch, SCID messageId, byte[] bodyData)
    {
        if (epoch != generation) return;

        switch (messageId)
        {
            case SCID.UDP_BATTLE_START:
                {
                    UdpBattleStart pb_ReceiveMes = PackageHandler.DeserializeData<UdpBattleStart>(bodyData);
                    NetGlobal.Instance.AddAction(() =>
                    {
                        if (epoch == generation) mes_battle_start?.Invoke(pb_ReceiveMes);
                    });
                }
                break;
            case SCID.UDP_DOWN_FRAME_OPERATIONS: //UDP_UP_PLAYER_OPERATIONS
                {
                    UdpDownFrameOperations pb_ReceiveMes = PackageHandler.DeserializeData<UdpDownFrameOperations>(bodyData);
                    NetGlobal.Instance.AddAction(() =>
                    {
                        if (epoch == generation) mes_frame_operation?.Invoke(pb_ReceiveMes);
                    });
                }
                break;
            case SCID.UDP_DOWN_DELTA_FRAMES:
                {
                    UdpDownDeltaFrames pb_ReceiveMes = PackageHandler.DeserializeData<UdpDownDeltaFrames>(bodyData);
                    NetGlobal.Instance.AddAction(() =>
                    {
                        if (epoch == generation) mes_delta_frame_data?.Invoke(pb_ReceiveMes);
                    });
                }
                break;
            case SCID.UDP_DOWN_GAME_OVER:
                {
                    UdpDownGameOver pb_ReceiveMes = PackageHandler.DeserializeData<UdpDownGameOver>(bodyData);
                    NetGlobal.Instance.AddAction(() =>
                    {
                        if (epoch == generation) mes_down_game_over?.Invoke(pb_ReceiveMes);
                    });
                }
                break;
            default:
                break;
        }
    }
}
