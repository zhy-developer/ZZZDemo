using UnityEngine;

/// <summary>
/// 框架日志工具。
/// 统一封装不同级别的输出格式，便于在 Console 中快速筛选。
/// </summary>
public class DeLogger
{
    private static bool isDebug = true;

    /// <summary>
    /// 输出系统级提示信息。
    /// </summary>
    public static void LogSysTrace(string context)
    {
        if (isDebug)
        {
            Debug.Log("-------------------><color=#8A9BA8> System: " + context + "</color><-------------------");
        }
    }

    /// <summary>
    /// 输出通知级提示信息。
    /// </summary>
    public static void LogNoticeTrace(string context)
    {
        if (isDebug)
        {
            Debug.Log("-------------------><color=#87CEEB> Notice: " + context + "</color><-------------------");
        }
    }

    /// <summary>
    /// 输出普通调试日志。
    /// </summary>
    public static void LogTrace(string context)
    {
        if (isDebug)
        {
            Debug.Log("-> <color=#90EE90>" + context + "</color>");
        }
    }

    /// <summary>
    /// 输出警告日志。
    /// </summary>
    public static void LogWarningTrace(string context)
    {
        if (isDebug)
        {
            Debug.LogWarning(context);
        }
    }

    /// <summary>
    /// 输出错误日志。
    /// </summary>
    public static void LogErrorTrace(string context)
    {
        if (isDebug)
        {
            Debug.LogError("-> " + context);
        }
    }
}
