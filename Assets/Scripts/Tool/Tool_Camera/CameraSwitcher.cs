using Cinemachine;
using System.Collections.Generic;
using UnityEngine;
using ZZZ;

public class CameraSwitcher : MonoSingleton<CameraSwitcher>
{
    // 使用实例作为键，同一种角色可以由不同玩家分别创建。
    private readonly Dictionary<Player, CharacterSkillCameraGroup> owners = new Dictionary<Player, CharacterSkillCameraGroup>();

    private readonly Dictionary<Player, Dictionary<AttackStyle, CinemachineStateDrivenCamera>> stateCameraPool = new Dictionary<Player, Dictionary<AttackStyle, CinemachineStateDrivenCamera>>();

    private void Start()
    {
        // 支持相机管理器晚于角色生成；后续生成的角色也会主动注册。
        foreach (var group in FindObjectsOfType<CharacterSkillCameraGroup>(true)) {
            if (group.isActiveAndEnabled) group.RegisterWith(this);
        }
    }

    public void RegisterCharacter(CharacterSkillCameraGroup group)
    {

        if (group == null || !group.IsLocalPlayer || group.Player == null) return;
        var player = group.Player;
        if (owners.TryGetValue(player, out var existing))
        {
            if (existing == group) return;
            Debug.LogWarning($"角色 {player.name} 已有技能相机配置，忽略重复组件。", group);
            return;
        }

        var cameras = new Dictionary<AttackStyle, CinemachineStateDrivenCamera>();
        foreach (var entry in group.Cameras)
        {
            if (entry == null || entry.camera == null) continue;
            if (cameras.ContainsKey(entry.attackStyle))
            {
                Debug.LogWarning($"角色 {player.name} 重复配置技能相机：{entry.attackStyle}", group);
                continue;
            }
            entry.camera.m_AnimatedTarget = player.GetComponent<Animator>();
            entry.camera.Priority = 0;
            cameras.Add(entry.attackStyle, entry.camera);
        }
        group.TrackRegistration(this);
        owners.Add(player, group);
        stateCameraPool.Add(player, cameras);
    }

    public void UnregisterCharacter(CharacterSkillCameraGroup group)
    {
        // Player 已销毁时也要清理它的实例键。
        Player key = null;
        foreach (var entry in owners)
            if (entry.Value == group) { key = entry.Key; break; }
        if (ReferenceEquals(key, null)) return;
        foreach (var camera in stateCameraPool[key].Values)
            if (camera != null) camera.Priority = 0;
        stateCameraPool.Remove(key);
        owners.Remove(key);
    }

    public void ActiveStateCamera(Player player, AttackStyle attackStyle)
    {
        SetStateCameraPriority(player, attackStyle, 20);
    }

    public void UnActiveStateCamera(Player player, AttackStyle attackStyle)
    {
        SetStateCameraPriority(player, attackStyle, 0);
    }

    private void SetStateCameraPriority(Player player, AttackStyle attackStyle, int priority)
    {
        if (player != null && stateCameraPool.TryGetValue(player, out var cameras)
            && cameras.TryGetValue(attackStyle, out var camera) && camera != null)
            camera.Priority = priority;
    }
}
