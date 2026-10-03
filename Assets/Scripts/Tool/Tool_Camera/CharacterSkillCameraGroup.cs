using System;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using ZZZ;

/// <summary>挂在角色预制体上，保存该实例的技能相机引用。</summary>
[DisallowMultipleComponent]
public class CharacterSkillCameraGroup : MonoBehaviour
{
    [Serializable]
    public class SkillCamera
    {
        public AttackStyle attackStyle;
        public CinemachineStateDrivenCamera camera;
    }

    [SerializeField] private Player player;
    [SerializeField] private List<SkillCamera> cameras = new List<SkillCamera>();

    private CameraSwitcher registeredSwitcher;
    private bool started;
    private readonly Dictionary<CinemachineVirtualCameraBase, bool> remoteCameraStates = new Dictionary<CinemachineVirtualCameraBase, bool>();

    public Player Player => player;
    public IReadOnlyList<SkillCamera> Cameras => cameras;
    public bool IsLocalPlayer { get; private set; } = true;

    private void Awake()
    {
        if (player == null) player = GetComponentInChildren<Player>(true);
        foreach (var entry in cameras)
            if (entry != null && entry.camera != null) entry.camera.Priority = 0;
    }

    /// <summary>生成器在 Instantiate 后立即调用；远端角色不能接管本地镜头。</summary>
    public void Initialize(bool isLocalPlayer)
    {
        Release();
        IsLocalPlayer = isLocalPlayer;
        if (player == null) player = GetComponentInChildren<Player>(true);
        if (!isLocalPlayer)
        {
            foreach (var camera in GetComponentsInChildren<CinemachineVirtualCameraBase>(true))
            {
                if (!remoteCameraStates.ContainsKey(camera)) remoteCameraStates.Add(camera, camera.enabled);
                camera.enabled = false;
            }
        }
        else
        {
            foreach (var entry in remoteCameraStates)
                if (entry.Key != null) entry.Key.enabled = entry.Value;
            remoteCameraStates.Clear();
            TryRegister();
        }
    }

    private void Start()
    {
        started = true;
        TryRegister();
    }

    private void OnEnable()
    {
        if (started) TryRegister();
    }

    private void TryRegister()
    {
        //如果是本地角色并且已激活，开始注册CameraSwitcher
        if (IsLocalPlayer && isActiveAndEnabled)
            RegisterWith(FindObjectOfType<CameraSwitcher>());
    }

    public void RegisterWith(CameraSwitcher switcher)
    {
        if (!IsLocalPlayer || switcher == null) return;
        switcher.RegisterCharacter(this);
    }

    internal void TrackRegistration(CameraSwitcher switcher)
    {
        if (registeredSwitcher != null && registeredSwitcher != switcher) Release();
        registeredSwitcher = switcher;
    }

    private void OnDisable() => Release();
    private void OnDestroy() => Release();

    private void Release()
    {
        if (registeredSwitcher != null) registeredSwitcher.UnregisterCharacter(this);
        registeredSwitcher = null;
    }
}
