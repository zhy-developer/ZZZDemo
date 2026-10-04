using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Unity.Burst.Intrinsics;
using UnityEngine;

public class TcpClientConnection : Singleton<TcpClientConnection>
{
    private static readonly object padlock = new object();

    private Socket clientSocket;

    public bool isRun = false;

    private Action<bool> ac_connect;


    public void EndClient()
    {

        isRun = false;

        if (clientSocket != null)
        {
            try
            {
                clientSocket.Close();
                clientSocket = null;
                Debug.Log("关闭tcp连接");
            }
            catch (Exception ex)
            {
                Debug.Log("关闭tcp连接异常111:" + ex.Message);
            }
        }
    }

    public void ConnectServer(string _ip, Action<bool> _result)
    {
        //设定服务器IP地址  
        ac_connect = _result;
        IPAddress ip;
        bool _isRight = IPAddress.TryParse(_ip, out ip);

        if (!_isRight)
        {
            Debug.Log("无效地址......" + _ip);
            _result(false);
            return;
        }
        clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        IPEndPoint _endpoint = new IPEndPoint(ip, NetConfig.TcpServerPort);
        Debug.Log("开始连接tcp~");
        clientSocket.BeginConnect(_endpoint, requestConnectCallBack, clientSocket);
    }

    private void requestConnectCallBack(IAsyncResult iar)
    {
        try
        {
            //还原原始的TcpClient对象
            Socket client = (Socket)iar.AsyncState;
            //
            client.EndConnect(iar);

            Debug.Log("连接服务器成功:" + client.RemoteEndPoint.ToString());
            isRun = true;

            NetGlobal.Instance.AddAction(() => {
                if (ac_connect != null)
                {
                    ac_connect(true);
                }
            });


            Thread myThread = new Thread(ReceiveMessage);
            myThread.Start();
        }
        catch (Exception e)
        {
            NetGlobal.Instance.AddAction(() => {
                if (ac_connect != null)
                {
                    ac_connect(false);
                }
            });

            Debug.Log("tcp连接异常:" + e.Message);
        }
        finally
        {

        }
    }

    private void ReceiveMessage()
    {

        while (isRun)
        {
            try
            {

                if (!clientSocket.Connected)
                {
                    throw new Exception("tcp客户端关闭了~~~");
                }

                //通过clientSocket接收数据  
                byte[] header = new byte[PackageConstant.PacketHeadLength];
                if (!ReceiveExactly(clientSocket, header)) throw new Exception("TCP connection closed");
                byte packMessageId = header[PackageConstant.PackMessageIdOffset];
                int packlength = BitConverter.ToUInt16(header, PackageConstant.PacklengthOffset);
                if (packlength < header.Length) throw new Exception("Invalid TCP packet length");
                byte[] bodyData = new byte[packlength - header.Length];
                if (!ReceiveExactly(clientSocket, bodyData)) throw new Exception("TCP connection closed mid-packet");
                TcpMessageDispatcher.Instance.AnalyzeMessage((GameProtocol.SCID)packMessageId, bodyData);
            }
            catch (Exception ex)
            {
                Debug.Log("接收服务端数据异常:" + ex.Message);
                EndClient();
                break;
            }
        }
    }

    private static bool ReceiveExactly(Socket socket, byte[] data)
    {
        int offset = 0;
        while (offset < data.Length)
        {
            int count = socket.Receive(data, offset, data.Length - offset, SocketFlags.None);
            if (count == 0) return false;
            offset += count;
        }
        return true;
    }
    public void SendMessage(byte[] _mes)
    {
        if (isRun)
        {
            try
            {
                clientSocket.Send(_mes);
            }
            catch (Exception ex)
            {
                EndClient();
                Debug.Log("发送数据异常:" + ex.Message);
            }
        }
    }
}
