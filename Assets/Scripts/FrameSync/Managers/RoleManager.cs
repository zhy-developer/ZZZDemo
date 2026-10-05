using GameProtocol;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using ZZZ;

public class RoleManager : MonoBehaviour
{

    public bool initFinish;
    public Player LocalPlayer { get; private set; }
    private GameObject pre_roleBase;
    private GameObject pre_roleUI;

    private Transform roleParent;
    private Dictionary<int, RoleBase> dic_role;

    public void InitData(Transform _roleParent, out GameVector2[] roleGrid)
    {
        initFinish = false;
        roleParent = _roleParent;

        dic_role = new Dictionary<int, RoleBase>();

        pre_roleBase = Resources.Load<GameObject>("BattleScene/Role/RoleBase");
        pre_roleUI = Resources.Load<GameObject>("BattleScene/Role/RoleUI");

        int _roleNum = BattleData.Instance.list_battleUser.Count;
        roleGrid = new GameVector2[_roleNum];
        for (int i = 0; i < roleGrid.Length; i++)
        {
            roleGrid[i] = BattleData.Instance.GetMapGridFromRand(ToolRandom.rand_10000()); // 确定一个随机位置
        }

        StartCoroutine(CreatRole(roleGrid));
    }

    IEnumerator CreatRole(GameVector2[] _roleGrid)
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

            GameVector2 _grid = _roleGrid[_info.battleID - 1];
            GameVector2 _pos = BattleData.Instance.GetMapGridCenterPosition(_grid.x, _grid.y);

            RoleBase _roleCon = _base.GetComponent<RoleBase>();
            _roleCon.InitData(_ui, _modle, _info.battleID, _pos); // 初始化
            dic_role[_info.battleID] = _roleCon;
            if (isLocalPlayer && player != null)
            {
                LocalPlayer = player;
                BindLocalCamera(player);
            }
        }

        initFinish = true;
    }

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

    public void Logic_Operation(AllPlayerOperation _allOp)
    {
        // Advance cooldowns before applying this frame's new actions.
        foreach (var role in dic_role.Values) role.Logic_Tick();
        //	Debug.Log("操作数" + _allOp.operations.Count);
        foreach (var item in _allOp.operations)
        {
            ///玩家执行移动
            dic_role[item.battleID].Logic_UpdateMoveDir(item.move);

            if (item.rightOperation == RightOpType.noop || item.operationID == 0)
            {
                //无操作
            }
            else
            {
                if (BattleData.Instance.IsValidRightOp(item.battleID, item.operationID))
                {
                    dic_role[item.battleID].Logic_ApplyAction(item);

                    BattleData.Instance.UpdateRightOperationID(item.battleID, item.operationID, item.rightOperation);
                }
            }
        }
    }

    public void Logic_Move()
    { // 逻辑移动。遍历每一个角色完成移动
        foreach (var item in dic_role)
        {
            item.Value.Logic_Move();
        }
    }

    void OnDestroy()
    {
        LocalPlayer = null;
        if (BattleManager.roleManage == this) BattleManager.roleManage = null;
    }

}
