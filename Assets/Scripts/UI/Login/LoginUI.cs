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
        if (loginBtn != null) {
            loginBtn.onClick.AddListener(() =>
            {
                LoginManager.Instance.OnClickLogin();
            });
        }
    }


    // Update is called once per frame
    void Update()
    {
        
    }
}
