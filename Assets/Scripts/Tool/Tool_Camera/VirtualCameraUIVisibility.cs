using Cinemachine;
using UnityEngine;

/// <summary>
/// 镜头生效时发布 HUD 隐藏请求，不持有 UI 对象引用。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachineVirtualCamera))]
public sealed class VirtualCameraUIVisibility : MonoBehaviour
{
    [SerializeField, Tooltip("驱动游戏主摄像机的 Brain。")]
    private CinemachineBrain brain;

    private CinemachineVirtualCamera virtualCamera;
    private bool isRequestingHidden;

    private void OnEnable()
    {
        virtualCamera = GetComponent<CinemachineVirtualCamera>();
        CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
        UpdateVisibility();
    }

    private void OnDisable()
    {
        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        SetHiddenRequest(false);
    }

    private void LateUpdate()
    {
        // Brain/实际 Camera 停用后不会再发更新事件，仍需解除隐藏请求。
        if (brain == null || !brain.isActiveAndEnabled || brain.OutputCamera == null
            || !brain.OutputCamera.isActiveAndEnabled)
        {
            SetHiddenRequest(false);
        }
    }

    private void OnCameraUpdated(CinemachineBrain updatedBrain)
    {
        if (updatedBrain == brain) UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        if (brain == null || !brain.isActiveAndEnabled || brain.OutputCamera == null
            || !brain.OutputCamera.isActiveAndEnabled)
        {
            SetHiddenRequest(false);
            return;
        }

        ICinemachineCamera activeCamera = brain.ActiveVirtualCamera;
        while (activeCamera is CinemachineStateDrivenCamera stateDrivenCamera)
        {
            activeCamera = stateDrivenCamera.LiveChild;
        }

        SetHiddenRequest(virtualCamera != null && ReferenceEquals(activeCamera, virtualCamera));
    }

    private void SetHiddenRequest(bool hidden)
    {
        if (isRequestingHidden == hidden) return;
        isRequestingHidden = hidden;
        HudVisibilityEvents.SetHiddenRequest(GetInstanceID(), hidden);
    }
}
