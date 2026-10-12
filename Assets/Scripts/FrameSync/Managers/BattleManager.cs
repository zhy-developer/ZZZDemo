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
    private SkillRuntime.Integration.BattleResultFlow resultFlow;

    [HideInInspector]
    public static RoleManager roleManage;


    void Start()
    {
        UdpMessageDispatcher.Instance.StartClientUdp();
        UdpMessageDispatcher.Instance.mes_battle_start = Message_Battle_Start;
        UdpMessageDispatcher.Instance.mes_frame_operation = Message_Frame_Operation;
        UdpMessageDispatcher.Instance.mes_delta_frame_data = Message_Delta_Frame_Data;

        isBattleStart = false;
        StartCoroutine("WaitInitData");

    }

    IEnumerator WaitInitData()
    {
        yield return new WaitUntil(() => {
            return roleManage.initFinish;
        });
        if (roleManage.Session != null) resultFlow = SkillRuntime.Integration.BattleResultFlow.Open(roleManage.Session,
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
        if (roleManage?.Session == null || !string.IsNullOrEmpty(_mes.error) || _mes.protocolVersion != 1 || _mes.authorityMode != 1 ||
            _mes.configHash != roleManage.Session.Digest || _mes.rules != SkillRuntime.Integration.BattleProfile.Rules)
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
    float nextMissingFrameRetry;
    void LogicUpdate()
    {
        if (destroyed || roleManage == null || roleManage.Session?.TerminalSnapshot != null) return;
        var data = BattleData.Instance;
        // Bounded catch-up drains already received consecutive frames. Every step is still
        // exactly one server tick; render cadence/wall time never supplies authority inputs.
        int budget = roleManage.Session != null ? 8 : 1;
        for (int i = 0; i < budget; i++)
        {
            if (!data.TryGetNextPlayerOp(out var op))
            {
                if (roleManage.Session != null && data.HighestReceivedFrame >= data.NextFrameID && Time.realtimeSinceStartup >= nextMissingFrameRetry)
                {
                    nextMissingFrameRetry = Time.realtimeSinceStartup + .25f;
                    int end = (int)Math.Min((long)data.HighestReceivedFrame, (long)data.NextFrameID + 31);
                    UdpMessageDispatcher.Instance.SendDeltaFrames(data.battleID, new List<int> { data.NextFrameID, end });
                }
                return;
            }
            if (!roleManage.ConsumeFrame(data.NextFrameID, op)) return;
            data.RunOpSucces();
            if (roleManage.Session?.TerminalSnapshot != null) return;
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
        resultFlow?.RequestReturn();
    }


    protected override void OnDestroy()
    {
        base.OnDestroy();
        destroyed = true; CancelInvoke(); StopAllCoroutines();
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
