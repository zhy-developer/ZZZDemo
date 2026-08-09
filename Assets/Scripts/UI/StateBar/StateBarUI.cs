using UnityEngine;
using UnityEngine.UI;

public class StateBarUI : MonoBehaviour,IUI
{
   private enum FollowMode
   {
       WorldSpace,
       ScreenSpace
   }

   [SerializeField] private Image RedBloodBar;
   [SerializeField] private Image GreenBloodBar;
   [SerializeField] private WavySlantFillImage RedBloodFill;
   [SerializeField] private WavySlantFillImage GreenBloodFill;
   [SerializeField] private float redBloodDelaySpeed = 0.05f;
   [SerializeField] private FollowMode followMode = FollowMode.WorldSpace;
   [SerializeField] private Vector3 worldOffset = Vector3.zero;
   [SerializeField] private Vector3 worldScale = new Vector3(0.01f, 0.01f, 0.01f);
   [SerializeField] private bool faceCamera = true;
   [SerializeField] private bool hideOutsideCameraView = true;
   [SerializeField] private float viewportMargin = 0.2f;
   [SerializeField] private bool keepConstantScreenSize;
   [SerializeField] private float screenSpaceBaseScale = 8;
   [SerializeField] private float screenSpaceBaseDistance = 3;
   [SerializeField] private bool ensureWorldSpaceCanvas = true;
   [SerializeField] private bool detachFromScreenSpaceCanvas = true;
   [SerializeField] private int worldSpaceSortingOrder = 10;
   private Transform cam;
   private Camera cachedCamera;
   private RectTransform rectTransform;
   private Canvas worldCanvas;
   private CanvasGroup canvasGroup;
    public void Init()
    {
        //GetUIImage();
    }
    private void Awake()
    {
        GetUIImage();
        if (RedBloodBar == null || GreenBloodBar == null)
        {
            Debug.LogError("RedBloodBar or GreenBloodBar is not initialized.");
        }
        BindFillComponents();
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        RefreshCamera();
        PrepareWorldSpaceCanvas();
    }

    private void GetUIImage()
    {
       // RedBloodBar = transform.Find("HP Red").GetComponent<Image>();
       // GreenBloodBar = transform.Find("HP Green").GetComponent<Image>();
    }
    private void Update()
    {
        //Debug.Log(GreenBloodBar.fillAmount);
        //Debug.Log(RedBloodBar.fillAmount);
        float greenAmount = GetFillAmount(GreenBloodFill, GreenBloodBar);
        float redAmount = GetFillAmount(RedBloodFill, RedBloodBar);
        if (greenAmount < redAmount)
        {
            SetFillAmount(RedBloodFill, RedBloodBar, Mathf.Max(greenAmount, redAmount - Time.deltaTime * redBloodDelaySpeed));
        }
    }
    public void UpdateBlood(float percentage)
    {
        SetFillAmount(GreenBloodFill, GreenBloodBar, percentage);
    }
    public void ShowAt(Vector3 worldPos)
    {
        RefreshCamera();
        if (cam == null || cachedCamera == null)
        {
            return;
        }

        Vector3 targetPos = worldPos + worldOffset;
        Vector3 viewPos = cachedCamera.WorldToViewportPoint(targetPos);
        bool isVisible = viewPos.x > -viewportMargin &&
                         viewPos.x < 1 + viewportMargin &&
                         viewPos.y > -viewportMargin &&
                         viewPos.y < 1 + viewportMargin &&
                         viewPos.z > 0;

        SetVisible(!hideOutsideCameraView || isVisible);
        if (hideOutsideCameraView && !isVisible)
        {
            return;
        }

        if (followMode == FollowMode.WorldSpace)
        {
            FollowInWorldSpace(targetPos);
        }
        else
        {
            FollowInScreenSpace(targetPos);
        }
        
    }

    private void FollowInWorldSpace(Vector3 targetPos)
    {
        transform.position = targetPos;

        if (faceCamera)
        {
            transform.LookAt(transform.position + cam.rotation * Vector3.forward, cam.rotation * Vector3.up);
        }

        if (keepConstantScreenSize)
        {
            float distance = Vector3.Distance(targetPos, cam.position);
            float scale = screenSpaceBaseScale * screenSpaceBaseDistance / distance;
            transform.localScale = worldScale * scale;
        }
        else
        {
            transform.localScale = worldScale;
        }
    }

    private void FollowInScreenSpace(Vector3 targetPos)
    {
        Vector3 screenPos = cachedCamera.WorldToScreenPoint(targetPos);
        transform.position = screenPos;

        float distance = Vector3.Distance(targetPos, cam.position);
        float scale = screenSpaceBaseScale * screenSpaceBaseDistance / distance;
        transform.localScale = new Vector3(scale, scale, scale);
    }

    private void BindFillComponents()
    {
        if (GreenBloodBar != null && GreenBloodFill == null)
        {
            GreenBloodFill = GreenBloodBar.GetComponent<WavySlantFillImage>();
            if (GreenBloodFill == null)
            {
                GreenBloodFill = GreenBloodBar.gameObject.AddComponent<WavySlantFillImage>();
            }
        }

        if (RedBloodBar != null && RedBloodFill == null)
        {
            RedBloodFill = RedBloodBar.GetComponent<WavySlantFillImage>();
            if (RedBloodFill == null)
            {
                RedBloodFill = RedBloodBar.gameObject.AddComponent<WavySlantFillImage>();
            }
        }
    }

    private float GetFillAmount(WavySlantFillImage fillImage, Image fallbackImage)
    {
        if (fillImage != null)
        {
            return fillImage.FillAmount;
        }

        return fallbackImage != null ? fallbackImage.fillAmount : 0f;
    }

    private void SetFillAmount(WavySlantFillImage fillImage, Image fallbackImage, float amount)
    {
        amount = Mathf.Clamp01(amount);

        if (fillImage != null)
        {
            fillImage.SetFill(amount);
            return;
        }

        if (fallbackImage != null)
        {
            fallbackImage.fillAmount = amount;
        }
    }

    private void RefreshCamera()
    {
        if (cachedCamera != null && cam != null)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            cachedCamera = mainCamera;
            cam = mainCamera.transform;
            if (worldCanvas != null)
            {
                worldCanvas.worldCamera = cachedCamera;
            }
        }
    }

    private void PrepareWorldSpaceCanvas()
    {
        if (followMode != FollowMode.WorldSpace || !ensureWorldSpaceCanvas)
        {
            return;
        }

        Canvas parentCanvas = transform.parent != null ? transform.parent.GetComponentInParent<Canvas>() : null;
        if (detachFromScreenSpaceCanvas && parentCanvas != null && parentCanvas.renderMode != RenderMode.WorldSpace)
        {
            transform.SetParent(null, true);
        }

        worldCanvas = GetComponent<Canvas>();
        if (worldCanvas == null)
        {
            worldCanvas = gameObject.AddComponent<Canvas>();
        }

        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.overrideSorting = true;
        worldCanvas.sortingOrder = worldSpaceSortingOrder;

        if (cachedCamera != null)
        {
            worldCanvas.worldCamera = cachedCamera;
        }

        if (rectTransform != null)
        {
            rectTransform.localScale = worldScale;
        }
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }
}
