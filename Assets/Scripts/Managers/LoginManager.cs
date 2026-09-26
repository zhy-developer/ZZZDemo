using GameProtocol;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoginManager : MonoSingleton<LoginManager>
{
    private void OnEnable()
    {
        PBTcpManager.Instance.mes_login_result += OnLoginResponse;
    }

    private void OnDisable()
    {
        PBTcpManager.Instance.mes_login_result -= OnLoginResponse;
    }
    public void OnClickLogin()
    {
        string _ip = NetConfig.ServerIP;
        ClientTCP.Instance.ConnectServer(_ip, (_result) => {
            if (_result)
            {
                Debug.Log("连接成功");
                NetGlobal.Instance.serverIP = _ip;
                TcpLogin _loginInfo = new TcpLogin();
                _loginInfo.token = SystemInfo.deviceUniqueIdentifier; // 客户端凭证
                                                                      // 连接成功后发送消息
                ClientTCP.Instance.SendMessage(PackageHandler.PackSendMessage<TcpLogin>(_loginInfo, CSID.TCP_LOGIN));
            }
            else
            {
                Debug.Log("连接失败");
            }
        });
    }

    public void OnLoginResponse(TcpResponseLogin message) {
        if (message.result)
        {
            NetGlobal.Instance.userUid = message.uid;
            NetGlobal.Instance.udpSendPort = message.udpPort;
            ClearSceneData.LoadScene(GameConfig.MAIN_SCENE);
                
        }
        else {
            DeLogger.LogErrorTrace("登录失败");

        }
    }
}
