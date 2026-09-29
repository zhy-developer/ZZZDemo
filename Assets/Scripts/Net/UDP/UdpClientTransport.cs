using UnityEngine;
using System;
using System.Text;
using System.Threading;

using System.Net;
using System.Net.Sockets;
using GameProtocol;
public class UdpClientTransport {
	public delegate void DelegateAnalyzeMessage(SCID messageId,byte[] bodyData);
	public DelegateAnalyzeMessage delegate_analyze_message;

	private UdpClient sendClient = null;

	private bool isRun;

	private bool isRecv;
	public void StartClientUdp(string _ip){
			
		if (isRun) {
			Debug.Log ("客户端udp已经启动~");
			return;
		}
		isRun = true;

		sendClient = UdpManager.Instance.GetClient();
		StartRecvMessage ();
	}
		
	private void StartRecvMessage(){
		Thread t = new Thread(new ThreadStart(RecvThread));
		t.Start();
	}

	public void StopRecvMessage(){
		isRecv = false;
	}

	public void EndClientUdp(){
		try {
			isRun = false;
			isRecv = false;
			
			Debug.LogError ("udp连接关闭  "  );
			UdpManager.Instance.CloseUdpClient();
			sendClient = null;
			delegate_analyze_message = null;	
		} catch (Exception ex) {
			Debug.Log ("udp连接关闭异常:" + ex.Message);
		}

	}
	public void MyEndClientUdp()
	{
		try
		{
		 

			Debug.LogError("udp连接关闭  ");
			UdpManager.Instance.CloseUdpClient();
		 
		}
		catch (Exception ex)
		{
			Debug.Log("udp连接关闭异常:" + ex.Message);
		}

	}

	public void SendMessage(byte[] _mes){
		if (isRun) {
			try {
				sendClient.Send(_mes,_mes.Length);
				BattleData.Instance.sendNum+=_mes.Length;
			} catch (Exception ex) {
				Debug.Log ("udp发送失败:" + ex.Message);
			}
		}	}


	private void RecvThread()
	{
		isRecv = true;
		IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse(NetGlobal.Instance.serverIP), UdpManager.Instance.localPort);
		while (isRecv)
		{
			try {
				byte[] buf = sendClient.Receive(ref endpoint);

				byte packMessageId = buf[PackageConstant.PackMessageIdOffset];     //消息id (1个字节)
				Int16 packlength = BitConverter.ToInt16(buf,PackageConstant.PacklengthOffset);  //消息包长度 (2个字节)
				int bodyDataLenth = packlength - PackageConstant.PacketHeadLength;
				byte[] bodyData = new byte[bodyDataLenth];
				Array.Copy(buf, PackageConstant.PacketHeadLength, bodyData, 0, bodyDataLenth);
			//	Debug.Log(" 收到 packMessageId: " + packMessageId);
				delegate_analyze_message((SCID)packMessageId,bodyData);

				//是客户端,统计接收量
				BattleData.Instance.recvNum+=buf.Length;
 	
			} catch (Exception ex) {
				Debug.LogError ("udpClient接收数据异常:" + ex.Message);
			}
		}
		Debug.Log ("udp接收线程退出~~~~~");
	}


	void OnDestroy()
	{
		EndClientUdp ();
	}
}