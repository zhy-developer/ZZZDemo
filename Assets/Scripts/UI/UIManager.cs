using System;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoSingleton<UIManager>
{
    [SerializeField] public StateBarUI stateBarUI;
    [SerializeField] public SwitchTimeUI switchTimeUI;
     GameObject uIRoot;
    private Dictionary<Type, IUI> uiDictionary = new Dictionary<Type, IUI>();
    public void RegisterUI<T>(IUI uI) where T : IUI
    { 
        Type type = typeof(T);
        if (!uiDictionary.ContainsKey(type))
        {
            uiDictionary.Add(type, uI);
        }
        else
        {
            uiDictionary[type] = uI;
        }
    }
    public T Get<T>() where T : class,IUI
    { 
        Type t = typeof(T);
        if (uiDictionary.TryGetValue(t,out var uI))
        {
            return uI as T;
        }
        return default(T);
    }
    private CanvasGroup hudGroup;
    private bool isHudHidden;
    private float originalHudAlpha;
    private bool originalHudInteractable;
    private bool originalHudBlocksRaycasts;

    private void OnEnable()
    {
        hudGroup = GetComponent<CanvasGroup>();
        if (hudGroup == null) hudGroup = gameObject.AddComponent<CanvasGroup>();
        GameEventsManager.Instance.AddEventListening<bool>(HudVisibilityEvents.Changed, OnHudVisibilityChanged);
        // UI 可能晚于镜头启用，订阅后立即同步当前状态。
        OnHudVisibilityChanged(HudVisibilityEvents.IsHidden);
    }

    private void OnDisable()
    {
        GameEventsManager.Instance.ReMoveEvent<bool>(HudVisibilityEvents.Changed, OnHudVisibilityChanged);
        RestoreHudVisibility();
    }

    private void OnHudVisibilityChanged(bool hidden)
    {
        if (!hidden)
        {
            RestoreHudVisibility();
            return;
        }
        if (hudGroup == null || isHudHidden) return;

        originalHudAlpha = hudGroup.alpha;
        originalHudInteractable = hudGroup.interactable;
        originalHudBlocksRaycasts = hudGroup.blocksRaycasts;
        isHudHidden = true;
        hudGroup.alpha = 0f;
        hudGroup.interactable = false;
        hudGroup.blocksRaycasts = false;
    }

    private void RestoreHudVisibility()
    {
        if (!isHudHidden) return;
        if (hudGroup != null)
        {
            hudGroup.alpha = originalHudAlpha;
            hudGroup.interactable = originalHudInteractable;
            hudGroup.blocksRaycasts = originalHudBlocksRaycasts;
        }
        isHudHidden = false;
    }

    protected override void Awake()
    {
        base.Awake();
        if(stateBarUI == null || switchTimeUI == null)
        {
            uIRoot = GameObject.Find("UIRoot");
            if (uIRoot != null)
            {
                stateBarUI = uIRoot.transform.Find("State Bar").GetComponent<StateBarUI>();
                switchTimeUI = uIRoot.transform.Find("Switch Time").GetComponent<SwitchTimeUI>();
            }
        }
    }

}
