using Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 为当前生效的虚拟镜头配置输出画面，不负责切换镜头。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachineVirtualCamera))]
public sealed class VirtualCameraRenderSettings : MonoBehaviour
{
    [SerializeField, Tooltip("此镜头生效时，实际 Camera 渲染的物体层。Overlay UI 不受此设置控制。")]
    private LayerMask cullingMask = ~0;
    [SerializeField] private Color backgroundColor = new Color32(102, 121, 133, 255);
    [SerializeField, Tooltip("关闭后处理以保持纯色背景，也会影响人物的后处理效果。")]
    private bool disablePostProcessing = false;

    private CinemachineVirtualCamera virtualCamera;
    private Camera overriddenCamera;
    private int originalCullingMask;
    private CameraClearFlags originalClearFlags;
    private Color originalBackgroundColor;
    private UniversalAdditionalCameraData overriddenCameraData;
    private bool originalPostProcessing;

    private void OnEnable()
    {
        virtualCamera = GetComponent<CinemachineVirtualCamera>();
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        RestoreCameraRendering();
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
    {
        if (!renderingCamera.TryGetComponent(out CinemachineBrain brain)
            || !brain.isActiveAndEnabled || brain.OutputCamera != renderingCamera) return;

        // GameObject 激活不代表镜头生效；状态驱动相机需要继续找到实际子镜头。
        ICinemachineCamera activeCamera = brain.ActiveVirtualCamera;
        while (activeCamera is CinemachineStateDrivenCamera stateDrivenCamera)
        {
            activeCamera = stateDrivenCamera.LiveChild;
        }

        if (virtualCamera == null || !ReferenceEquals(activeCamera, virtualCamera)) return;

        // Cinemachine 也用输出 Camera 的 Culling Mask 选择镜头，因此只在渲染期间覆盖。
        RestoreCameraRendering();
        overriddenCamera = renderingCamera;
        originalCullingMask = renderingCamera.cullingMask;
        originalClearFlags = renderingCamera.clearFlags;
        originalBackgroundColor = renderingCamera.backgroundColor;
        overriddenCameraData = renderingCamera.GetComponent<UniversalAdditionalCameraData>();
        if (overriddenCameraData != null)
        {
            originalPostProcessing = overriddenCameraData.renderPostProcessing;
            if (disablePostProcessing) overriddenCameraData.renderPostProcessing = false;
        }

        renderingCamera.cullingMask = cullingMask;
        renderingCamera.clearFlags = CameraClearFlags.SolidColor;
        renderingCamera.backgroundColor = backgroundColor;
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
    {
        if (renderingCamera == overriddenCamera) RestoreCameraRendering();
    }

    private void RestoreCameraRendering()
    {
        if (overriddenCamera != null)
        {
            overriddenCamera.cullingMask = originalCullingMask;
            overriddenCamera.clearFlags = originalClearFlags;
            overriddenCamera.backgroundColor = originalBackgroundColor;
        }

        if (overriddenCameraData != null)
        {
            overriddenCameraData.renderPostProcessing = originalPostProcessing;
        }

        overriddenCamera = null;
        overriddenCameraData = null;
    }
}
