using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Rendering.DebugUI;

public class LoginUI : MonoBehaviour
{
    public UnityEngine.UI.Button loginBtn;
    void Start()
    {
        IsNetworkReachability();
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Application.targetFrameRate = 30;

        _ = NetGlobal.Instance;

        if (loginBtn != null) {
            loginBtn.onClick.AddListener(() =>
            {
                LoginManager.Instance.OnClickLogin();
            });
        }
    }

    public bool IsNetworkReachability()
    {
        switch (Application.internetReachability)
        {
            case NetworkReachability.ReachableViaLocalAreaNetwork:
                Debug.Log(" WiFi ");
                return true;
            case NetworkReachability.ReachableViaCarrierDataNetwork:
                Debug.Log(" 移动网络 ");
                return true;
            default:
                Debug.Log(" 没有联网 ");
                return false;
        }
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
