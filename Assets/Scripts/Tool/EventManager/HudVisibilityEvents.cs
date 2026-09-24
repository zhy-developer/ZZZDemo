using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通过现有事件中心广播 HUD 状态，并保留当前状态供晚订阅的 UI 同步。
/// </summary>
public static class HudVisibilityEvents
{
    public const string Changed = "HUD显隐状态变化";
    private static readonly HashSet<int> hiddenRequests = new HashSet<int>();
    public static bool IsHidden => hiddenRequests.Count > 0;

    public static void SetHiddenRequest(int sourceId, bool hidden)
    {
        bool wasHidden = IsHidden;
        if (hidden) hiddenRequests.Add(sourceId);
        else hiddenRequests.Remove(sourceId);

        // 多个请求存在时，只有全部解除才恢复 HUD；仅状态变化时广播。
        if (wasHidden != IsHidden)
        {
            GameEventsManager.Instance.TryCallEvent(Changed, IsHidden);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        hiddenRequests.Clear();
    }
}
