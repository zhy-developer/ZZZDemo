using UnityEngine;

/// <summary>
/// 纯 C# 单例基类。
/// 适用于不依赖 Unity 生命周期的管理器或服务对象。
/// </summary>
public abstract class Singleton<T> where T : class, new()
{
    private static T _instance;
    private static readonly object _lock = new object();

    /// <summary>
    /// 获取单例实例。
    /// 首次访问时会自动创建实例，并调用 <see cref="Initialize"/>。
    /// </summary>
    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new T();
                        (_instance as Singleton<T>)?.Initialize();
                    }
                }
            }

            return _instance;
        }
    }

    /// <summary>
    /// 防止外部直接构造多个实例。
    /// </summary>
    protected Singleton()
    {
        if (_instance != null)
        {
            Debug.LogError($"[Singleton] Duplicate instance created: {typeof(T).Name}");
        }
    }

    /// <summary>
    /// 单例首次创建后调用。
    /// 子类可在此完成初始化逻辑。
    /// </summary>
    protected virtual void Initialize() { }

    /// <summary>
    /// 释放当前单例实例。
    /// 一般用于测试、热重载或显式重置运行时状态。
    /// </summary>
    public static void Release()
    {
        if (_instance != null)
        {
            (_instance as Singleton<T>)?.OnRelease();
            _instance = null;
        }
    }

    /// <summary>
    /// 单例被释放前调用。
    /// </summary>
    protected virtual void OnRelease() { }
}
