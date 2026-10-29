using System;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoSingleton<UIManager>
{
    [SerializeField] public StateBarUI stateBarUI;
    private RoleBase localHealthRole;
    private StateBarUI localHealthBar;

    public void BindLocalRole(RoleBase role)
    {
        var bar = role != null ? FindLocalHealthBar() : null;
        if (localHealthRole == role && localHealthBar == bar) return;
        UnbindLocalHealthBar();
        if (role == null) return;
        if (bar == null)
        {
            Debug.LogWarning("MainUI 内未找到唯一的本地血条：请检查 BattleRoleInfoPanel 和 StateBarUI 的红/绿 Image 引用。组件可以挂在 BattleUI 或面板本身。", this);
            return;
        }
        localHealthRole = role;
        localHealthBar = bar;
        role.RegisterHealthBar(bar);
    }

    private StateBarUI FindLocalHealthBar()
    {
        // 面板可以位于 BattleUI 等容器内，组件也可以位于面板父节点上。
        // 以实际控制的 Image 为依据，只搜索当前 MainUI，避免误绑头顶血条。
        StateBarUI result = null;
        var bars = GetComponentsInChildren<StateBarUI>(true);
        foreach (var child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name != "BattleRoleInfoPanel") continue;
            foreach (var bar in bars)
            {
                if (!bar.DisplaysHealthUnder(child)) continue;
                if (result != null && result != bar) return null;
                result = bar;
            }
        }
        return result;
    }

    private void UnbindLocalHealthBar()
    {
        if (localHealthRole != null) localHealthRole.UnregisterHealthBar(localHealthBar);
        localHealthRole = null;
        localHealthBar = null;
    }

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
        // 兼容 UI 晚于角色创建或重新启用；普通血量更新由角色推送。
        if (BattleManager.roleManage != null)
            BindLocalRole(BattleManager.roleManage.LocalRole);
    }

    private void OnDisable()
    {
        GameEventsManager.Instance.ReMoveEvent<bool>(HudVisibilityEvents.Changed, OnHudVisibilityChanged);
        RestoreHudVisibility();
        UnbindLocalHealthBar();
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
        uIRoot = GameObject.Find("UIRoot");
        if (uIRoot != null)
        {
            if (stateBarUI == null)
                stateBarUI = uIRoot.transform.Find("State Bar")?.GetComponent<StateBarUI>();
            // 单角色对战不显示候补角色选择 UI。
            var switchUI = uIRoot.transform.Find("Switch Time");
            if (switchUI != null) switchUI.gameObject.SetActive(false);
        }
    }

}
