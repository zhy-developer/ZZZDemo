using GameProtocol;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleManager : MonoSingleton<BattleManager>
{

    public delegate void DelegateEvent();
    public DelegateEvent delegate_readyOver;
    public DelegateEvent delegate_gameOver;

    private bool isBattleStart;
    private bool isBattleFinish;
    private bool destroyed;
    private SkillRuntime.Integration.PilotResultFlow resultFlow;

    [HideInInspector]
    public static RoleManager roleManage;


    void Start()
    {
        UdpMessageDispatcher.Instance.StartClientUdp();
        UdpMessageDispatcher.Instance.mes_battle_start = Message_Battle_Start;
        UdpMessageDispatcher.Instance.mes_frame_operation = Message_Frame_Operation;
        UdpMessageDispatcher.Instance.mes_delta_frame_data = Message_Delta_Frame_Data;
        UdpMessageDispatcher.Instance.mes_down_game_over += Message_Down_Game_Over;

        isBattleStart = false;
        StartCoroutine("WaitInitData");

    }

    IEnumerator WaitInitData()
    {
        yield return new WaitUntil(() => {
            return roleManage.initFinish;
        });
        if (roleManage.Pilot != null) resultFlow = SkillRuntime.Integration.PilotResultFlow.Open(roleManage.Pilot,
            () => { if (!destroyed) delegate_gameOver?.Invoke(); },
            () => { if (this) { destroyed = true; CancelInvoke(); StopAllCoroutines(); } });
        this.InvokeRepeating("Send_BattleReady", 0.5f, 0.2f);
    }

    public void InitData(Transform _map,Renderer mapPlane)
    {
        BattleData.Instance.InitMapBounds(mapPlane.bounds);

        ToolRandom.srand((ulong)BattleData.Instance.randSeed); // 设置随机数种子。
        roleManage = gameObject.AddComponent<RoleManager>();  //角色管理器 生成角色

        roleManage.InitData(_map.Find("Role")); // 按 battleID 分配固定的对称出生点
    }

    void Send_BattleReady()
    {
        UdpMessageDispatcher.Instance.SendBattleReady(NetGlobal.Instance.userUid, BattleData.Instance.battleID);
    }

    void Message_Battle_Start(UdpBattleStart _mes)
    {
        if (destroyed || !BattleData.Instance.InScope(_mes.matchId, _mes.roundId)) return;
        int mode = roleManage.Pilot == null ? 0 : 1;
        if (!string.IsNullOrEmpty(_mes.error) || _mes.protocolVersion != 1 || _mes.authorityMode != mode ||
            mode == 1 && (_mes.configHash != roleManage.Pilot.Digest || _mes.rules != SkillRuntime.Integration.PilotBattleProfile.Rules))
        { Debug.LogError("[MatchRejected] " + (_mes.error ?? "mode/version mismatch")); CancelInvoke("Send_BattleReady"); return; }
        BattleStart();
    }

    void BattleStart()
    {
        Debug.Log("BattleStart isBattleStart " + isBattleStart);
        if (isBattleStart)
        {
            return;
        }

        isBattleStart = true;
        this.CancelInvoke("Send_BattleReady");

        float _time = NetConfig.frameTime * 0.001f;  // 66ms
        this.InvokeRepeating("Send_operation", _time, _time);  // 循环调用 Send_operation 方法

        StartCoroutine("WaitForFirstMessage");
    }

    void Send_operation()
    {
        if (destroyed || resultFlow != null && resultFlow.Agreement.Local != null) return;
        UdpMessageDispatcher.Instance.SendOperation();
    }

    IEnumerator WaitForFirstMessage()
    {
        yield return new WaitUntil(() => {
            //Debug.Log("frameDataNum >0 *** " + BattleData.Instance.GetFrameDataNum());
            return BattleData.Instance.GetFrameDataNum() > 0; // 在这里等待第一帧，第一帧没更新之前不会做更新。
        });

        DeLogger.LogTrace("获取到服务器下发的第一帧，开始执行逻辑更新");
        this.InvokeRepeating("LogicUpdate", 0f, 0.020f);

        if (delegate_readyOver != null)
        {
            delegate_readyOver();    // 关闭对局等待界面
        }
    }

    /// <summary>
    /// 接受服务器下发的帧
    /// </summary>
    /// <param name="_mes"></param>
    void Message_Frame_Operation(UdpDownFrameOperations _mes)
    {
        if (destroyed || !BattleData.Instance.InScope(_mes.matchId, _mes.roundId)) return;
        DeLogger.LogTrace("服务器下发帧");
        BattleData.Instance.AddNewFrameData(_mes.frameID, _mes.operations);
        BattleData.Instance.netPack++;
    }

    //逻辑帧更新
    void LogicUpdate()
    {
        if (destroyed || roleManage == null || roleManage.Pilot?.TerminalSnapshot != null) return;
        AllPlayerOperation _op;
        if (BattleData.Instance.TryGetNextPlayerOp(out _op))
        {
            if (!roleManage.ConsumeFrame(BattleData.Instance.NextFrameID, _op)) return;
            //roleManage.Logic_Move_Correction();
            BattleData.Instance.RunOpSucces();
        }
    }

    void Message_Delta_Frame_Data(UdpDownDeltaFrames _mes)
    {
        if (_mes.framesData.Count > 0)
        {
            foreach (var item in _mes.framesData)
            {
                if (destroyed || !BattleData.Instance.InScope(item.matchId, item.roundId)) continue;
                BattleData.Instance.AddLackFrameData(item.frameID, item.operations);
            }
        }
    }

    public void OnClickGameOver()
    {
        BeginGameOver();
    }

    void BeginGameOver()
    {
        if (roleManage != null && roleManage.AuthorityMode == SkillRuntime.Integration.BattleAuthorityMode.DeterministicPilot)
        { resultFlow?.RequestReturn(); return; }
        this.CancelInvoke("Send_operation");
        this.InvokeRepeating("SendGameOver", 0f, 0.5f);
    }

    void SendGameOver()
    {
        UdpMessageDispatcher.Instance.SendGameOver(BattleData.Instance.battleID);
    }

    void Message_Down_Game_Over(UdpDownGameOver _mes)
    {
        if (roleManage != null && roleManage.AuthorityMode == SkillRuntime.Integration.BattleAuthorityMode.DeterministicPilot) return;
        this.CancelInvoke("SendGameOver");
        Debug.Log("游戏结束咯～～～～～～");
        if (delegate_gameOver != null)
        {
            delegate_gameOver();
        }
    }


    protected override void OnDestroy()
    {
        base.OnDestroy();
        destroyed = true; CancelInvoke(); StopAllCoroutines();
        UdpMessageDispatcher.Instance.mes_down_game_over -= Message_Down_Game_Over;
        UdpMessageDispatcher.Instance.mes_battle_start = null;
        UdpMessageDispatcher.Instance.mes_frame_operation = null;
        UdpMessageDispatcher.Instance.mes_delta_frame_data = null;
        // Persistent result owner keeps retries/network alive across an unexpected scene unload.
        if (resultFlow != null && !resultFlow.Retired) return;
        //	Debug.Log("清理之前 "  + BattleData.dic_frameDate_Static.Count);
        BattleData.Instance.ClearData();
        //	Debug.Log("清理之后 " + BattleData.dic_frameDate_Static.Count);
        UdpMessageDispatcher.Instance.Destory();
    }

    void Update()
    {

    }


}
