using System;
using System.Collections.Generic;
using System.IO;
using Unity.Burst.Intrinsics;

/// <summary>
/// 为什么要这样定义？
/// 为了tcp使用，因为tcp发的是字节流协议，定义包体结构可以根据消息id和消息包长度方便拆分数据包
/// </summary>
//包头结构
public struct PackageConstant
{
    public static int PackMessageIdOffset = 0;
    // 消息id (1个字节)
    public static int PacklengthOffset = 1;
    //消息包长度 (2个字节)
    public static int PacketHeadLength = 3;
    //包头长度
}

//完整数据包
//┌─────────────── 包头 3字节 ───────────────┐┌────── 包体 ──────┐
//│ MessageId 1字节 │ Length 2字节          ││ Protobuf数据      │
//└────────────────┴────────────────────────┘└───────────────────┘


public class NetPacker
{
    /// <summary>
    /// 将包体数据打包
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="pb_Body"></param>
    /// <param name="messageID"></param>
    /// <returns></returns>
    public static byte[] GetSendMessage<T>(T pb_Body, GameProtocol.CSID messageID)
    {
        //包体数据
        byte[] packageBody = NetPacker.SerializeData<T>(pb_Body);
        //消息ID
        byte packMessageId = (byte)messageID; //消息id (1个字节)

        int packlength = PackageConstant.PacketHeadLength + packageBody.Length; //消息包长度 (2个字节) 
                                                                                //BitConverter.GetBytes 会按照当前机器的字节序来转换
        byte[] packlengthByte = BitConverter.GetBytes((ushort)packlength);//short两个字节，int4个字节，Bitconverter.GetBytes的作用是使用当前计算机本身的字节序将入参拆成多个字节
        //大部分现代 PC、Android 手机所使用的 CPU 环境通常都是小端序
        //网络字节序通常是什么？
        //传统网络协议规定：
        //网络字节序 = 大端序 Big Endian
        //了解大小端序
        //大端：大的那一端（高位）先放
        //小端：小的那一端（低位）先放

        List<byte> packageHeadList = new List<byte>();
        //包头信息
        packageHeadList.Add(packMessageId);
        packageHeadList.AddRange(packlengthByte);
        //包体
        packageHeadList.AddRange(packageBody);

        return packageHeadList.ToArray();
    }

    /// <summary>
    /// 把一个对象序列化为字节数组
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="instance"></param>
    /// <returns></returns>
    public static byte[] SerializeData<T>(T instance)
    {
        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            ProtoBuf.Serializer.Serialize(ms, instance);
            bytes = new byte[ms.Position];
            var fullBytes = ms.GetBuffer();
            Array.Copy(fullBytes, bytes, bytes.Length);
        }
        return bytes;
    }

    /// <summary>
    /// 反序列化protobuf数据
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="bytes"></param>
    /// <returns></returns>
    public static T DeserializeData<T>(byte[] bytes)
    {
        using (Stream ms = new MemoryStream(bytes))
        {
            return ProtoBuf.Serializer.Deserialize<T>(ms);
        }
    }
}


