using GameProtocol;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using ZZZ;
using System;
using System.Linq;
using SkillRuntime;
using SkillRuntime.Integration;

public class RoleManager : MonoBehaviour
{

    public bool initFinish;
    public Player LocalPlayer { get; private set; }
    private GameObject pre_roleBase;
    private GameObject pre_roleUI;

    private Transform roleParent;
    private Dictionary<int, RoleBase> dic_role;
    public SkillRuntime.Integration.PilotBattleSession Pilot { get; private set; }
    public SkillRuntime.Integration.BattleAuthorityMode AuthorityMode { get; private set; }
    private SkillRuntime.Integration.PilotConfiguration pilotConfig;
    private PilotBattleView battleView;

    public void InitData(Transform _roleParent)
    {
        initFinish = false;
        roleParent = _roleParent;

        dic_role = new Dictionary<int, RoleBase>();
        var settings = GetComponent<SkillRuntime.Integration.PilotMatchSettings>();
        if (settings == null) throw new InvalidOperationException("Battle scene requires a battle profile.");
        pilotConfig = settings.Freeze();
        AuthorityMode = BattleAuthorityMode.DeterministicPilot;

        pre_roleBase = Resources.Load<GameObject>("BattleScene/Role/RoleBase");
        pre_roleUI = Resources.Load<GameObject>("BattleScene/Role/RoleUI");

        StartCoroutine(CreatRole());
    }

    IEnumerator CreatRole()
    {

        Dictionary<string, GameObject> pre_roleModle = new Dictionary<string, GameObject>();
        List<BattleUserInfo> list_battleUser = BattleData.Instance.list_battleUser;

        for (int i = 0; i < list_battleUser.Count; i++)
        {
            yield return new WaitForEndOfFrame();
            BattleUserInfo _info = list_battleUser[i];

            GameObject _base = Instantiate(pre_roleBase, roleParent);// 角色父物体
            GameObject _ui = Instantiate(pre_roleUI, _base.transform);

            //string _modleStr = string.Format("BattleScene/Role/RoleModel{0}",_info.roleID);
            string _modleStr = "BattleScene/Role/Character";
            if (!pre_roleModle.ContainsKey(_modleStr))
            {
                pre_roleModle[_modleStr] = Resources.Load<GameObject>(_modleStr);
            }
            GameObject _modle = Instantiate(pre_roleModle[_modleStr]);  // 角色模型

            bool isLocalPlayer = _info.battleID == BattleData.Instance.battleID;
            var player = _modle.GetComponentInChildren<Player>(true);
            if (player != null)
                player.InitializeNetworkRole(_info.battleID, isLocalPlayer);

            // 相机配置随角色实例创建，只允许本地玩家的相机参与切换。
            foreach (var cameras in _modle.GetComponentsInChildren<CharacterSkillCameraGroup>(true)) {
                cameras.Initialize(isLocalPlayer);
            }

            GameVector2 _pos = BattleData.Instance.GetPvpSpawnPosition(_info.battleID);

            RoleBase _roleCon = _base.GetComponent<RoleBase>();
            _roleCon.InitData(_ui, _modle, _info.battleID, _pos); // 初始化
            dic_role[_info.battleID] = _roleCon;
            if (isLocalPlayer && player != null)
            {
                LocalPlayer = player;
                BindLocalCamera(player);
            }
        }

        var data = BattleData.Instance;
        var spawns = dic_role.Values.Select(r => new BattleSpawn(r.Player.BattleID, r.LogicalPosition.x, r.LogicalPosition.y, r.moveSpeed)).ToArray();
        var directions = Enumerable.Range(0, 120).Select(i => { var d = data.GetSpeed(i * 3); return new XzDelta(d.x, d.y); }).ToArray();
        Pilot = new PilotBattleSession(pilotConfig.CreateLogicConfiguration(), spawns,
            new BattleBounds(data.mapMinX, data.mapMaxX, data.mapMinY, data.mapMaxY), directions,
            data.SessionKey, data.MatchId, PilotBattleProfile.Rules);
        foreach (var role in dic_role.Values) role.Player.BindBattleInput(() => Pilot.Latest);
        try { battleView = new PilotBattleView(pilotConfig, dic_role.Values); battleView.Enqueue(Pilot.Latest); }
        catch (Exception e) { Debug.LogException(e); }
        initFinish = true;
    }

    public bool ConsumeFrame(long frame, AllPlayerOperation operations)
    {
        if (!initFinish || Pilot == null || !Pilot.Advance(frame, operations)) return false;
        foreach (var op in operations.operations)
            if (op.rightOperation != RightOpType.noop && op.operationID > 0)
                BattleData.Instance.UpdateRightOperationID(op.battleID, op.operationID, op.rightOperation);
        battleView?.Enqueue(Pilot.Latest);
        return true;
    }
    void LateUpdate() { battleView?.Render(Time.unscaledDeltaTime); }
    public RoleBase GetRoleFromBattleID(int _id)
    {
        return dic_role[_id];
    }

    private void BindLocalCamera(Player player)
    {
        foreach (var camera in FindObjectsOfType<CinemachineVirtualCamera>(true))
        {
            // 技能镜头保留预制体绑定，只更新场景中的普通跟随镜头。
            if (camera.GetComponentInParent<CharacterSkillCameraGroup>() != null
                || camera.CompareTag("CloseShot")) continue;
            camera.Follow = player.CameraFollowTarget;
            camera.LookAt = player.CameraLookAtTarget;
            if (camera.GetCinemachineComponent<CinemachinePOV>() != null)
                player.playerCameraUtility?.Bind(camera);
        }
    }

    void OnDestroy()
    {
        battleView?.Dispose();
        if (PilotResultFlow.Active == null) Pilot?.Dispose();
        LocalPlayer = null;
        if (BattleManager.roleManage == this) BattleManager.roleManage = null;
    }

}
