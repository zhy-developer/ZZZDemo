using GameProtocol;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using ZZZ;
using ZZZ.SkillRuntime;

public class RoleManager : MonoBehaviour
{

    public bool initFinish;
    public Player LocalPlayer { get; private set; }
    private GameObject pre_roleBase;
    private GameObject pre_roleUI;

    private Transform roleParent;
    private Dictionary<int, RoleBase> dic_role;
    private FrameCombatSession combat;
    private CombatStatusView combatStatus;
    public CombatWorld CombatWorld => combat?.World;
    public string CombatError { get; private set; }

    public void InitData(Transform _roleParent)
    {
        initFinish = false;
        roleParent = _roleParent;

        dic_role = new Dictionary<int, RoleBase>();
        CombatError = null;
        combatStatus = gameObject.AddComponent<CombatStatusView>();
        combatStatus.Bind(this);

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

        // 两个角色全部创建后再构造同一个战斗世界；配置失败时不发送 ready，也不回退到旧战斗。
        try
        {
            combat = new FrameCombatSession(dic_role.Values);
            initFinish = true;
        }
        catch (System.Exception exception)
        {
            CombatError = exception.Message;
            Debug.LogError("帧战斗初始化失败：" + exception, this);
        }
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

    public bool Logic_Step(AllPlayerOperation operations)
    {
        if (combat == null || !string.IsNullOrEmpty(CombatError)) return false;
        try
        {
            combat.Step(operations);
            combatStatus.OnLogicFrame();
            return true;
        }
        catch (System.Exception exception)
        {
            CombatError = exception.Message;
            Debug.LogError("帧战斗停止，需检查配置/调用链：" + exception, this);
            return false;
        }
    }

    void OnDestroy()
    {
        combat?.Dispose(); combat = null;
        LocalPlayer = null;
        if (BattleManager.roleManage == this) BattleManager.roleManage = null;
    }

}
