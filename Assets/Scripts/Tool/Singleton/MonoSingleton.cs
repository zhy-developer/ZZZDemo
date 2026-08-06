using UnityEngine;

/// <summary>
/// 基于 MonoBehaviour 的单例基类。
/// 适用于需要挂载到场景中，或需要依赖 Unity 生命周期的管理器。
/// </summary>
public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
{
    private static T _instance;
    private static readonly object _lock = new object();
    private static bool _applicationIsQuitting = false;

    [SerializeField]
    private bool _isPersistent = true;

    /// <summary>
    /// 获取单例实例。
    /// 若场景中不存在实例，会自动创建一个新的 GameObject 并挂载组件。
    /// </summary>
    public static T Instance
    {
        get
        {
            if (_applicationIsQuitting)
            {
                Debug.LogWarning($"[MonoSingleton] Application is quitting, skip creating {typeof(T).Name}.");
                return null;
            }

            lock (_lock)
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<T>();

                    if (_instance == null)
                    {
                        var singletonObject = new GameObject();
                        _instance = singletonObject.AddComponent<T>();
                        singletonObject.name = $"[MonoSingleton] {typeof(T).Name}";

                        DeLogger.LogTrace($"[MonoSingleton] Created instance: {typeof(T).Name}");
                    }
                }

                return _instance;
            }
        }
    }

    /// <summary>
    /// Unity 生命周期入口。
    /// 负责建立唯一实例并根据配置决定是否跨场景保留。
    /// </summary>
    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;

            if (_isPersistent)
            {
                DontDestroyOnLoad(gameObject);
            }

            Initialize();
        }
        else if (_instance != this)
        {
            Debug.LogWarning($"[MonoSingleton] Duplicate instance detected: {typeof(T).Name}, destroying current object.");
            DestroyImmediate(gameObject);
        }
    }

    /// <summary>
    /// 子类初始化入口。
    /// 如无必要，不要直接重写 Awake，而是在此编写初始化逻辑。
    /// </summary>
    protected virtual void Initialize() { }

    /// <summary>
    /// 组件销毁时清理静态实例引用。
    /// </summary>
    protected virtual void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>
    /// 应用退出时标记状态，避免退出阶段再次创建单例。
    /// </summary>
    private void OnApplicationQuit()
    {
        _applicationIsQuitting = true;
    }
}
