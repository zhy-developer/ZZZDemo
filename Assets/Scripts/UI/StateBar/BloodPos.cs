using Cinemachine;
using UnityEngine;

[DefaultExecutionOrder(10000)]
public class BloodPos : MonoBehaviour
{
    public StateBarUI stateBarUI;
    private Transform followTarget;
    private Vector3 followOffset;
    private bool hasBoundTarget;

    public void Bind(StateBarUI bar, Transform target, Vector3 offset)
    {
        stateBarUI = bar;
        followTarget = target;
        followOffset = offset;
        hasBoundTarget = true;
        SyncBloodUI();
    }

    private void OnEnable()
    {
        CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
    }

    private void OnDisable()
    {
        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
    }

    private void OnCameraUpdated(CinemachineBrain brain)
    {
        // 使用 Brain 更新后的实际主相机姿态，包含技能镜头和混合过渡。
        if (brain != null && brain.OutputCamera == Camera.main)
            SyncBloodUI();
    }

    private void LateUpdate()
    {
        // 兼容不使用 Cinemachine 的相机，并在角色显示位置更新后跟随。
        SyncBloodUI();
    }

    private void SyncBloodUI()
    {
        if (stateBarUI == null || !stateBarUI.isActiveAndEnabled) return;
        if (hasBoundTarget && followTarget == null) return;

        // 保留原用法：手动挂在锚点且未 Bind 时，跟随自身位置。
        Transform target = hasBoundTarget ? followTarget : transform;
        stateBarUI.ShowAt(target.position + followOffset);
    }
}
