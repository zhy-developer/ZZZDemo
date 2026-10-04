using GameProtocol;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoginManager : MonoSingleton<LoginManager>
{
    // 同机联调：每个客户端进程使用独立身份，进程内重连保持不变。
    private static readonly string sessionToken = System.Guid.NewGuid().ToString("N");
    private void OnEnable()
    {
        TcpMessageDispatcher.Instance.mes_login_result += OnLoginResponse;
    }

    private void OnDisable()
    {
        TcpMessageDispatcher.Instance.mes_login_result -= OnLoginResponse;
    }
    public void OnClickLogin()
    {
        string _ip = NetConfig.ServerIP;
        TcpClientConnection.Instance.ConnectServer(_ip, (_result) => {
            if (_result)
            {
                Debug.Log("连接成功");
                NetGlobal.Instance.serverIP = _ip;
                TcpLogin _loginInfo = new TcpLogin();
                _loginInfo.token = SystemInfo.deviceUniqueIdentifier + ":" + sessionToken;
                                                                      // 连接成功后发送消息
                TcpClientConnection.Instance.SendMessage(PackageHandler.PackSendMessage<TcpLogin>(_loginInfo, CSID.TCP_LOGIN));
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
            Debug.Log("登录成功，当前客户端 UID: " + message.uid);
            NetGlobal.Instance.udpSendPort = message.udpPort;
            ClearSceneData.LoadScene(GameConfig.MAIN_SCENE);
                
        }
        else {
            DeLogger.LogErrorTrace("登录失败");

        }
    }
}
