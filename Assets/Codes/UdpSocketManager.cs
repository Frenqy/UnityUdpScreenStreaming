using ChatProto;
using Google.Protobuf;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using UdpStreamProtocol;
using UnityEngine;

/// <summary>
/// udp通讯管理类
/// </summary>
public class UdpSocketManager : MonoBehaviour
{
    public static UdpSocketManager Instance;

    //public Queue<byte[]> ReceivedVideoDataQueue = new Queue<byte[]>();
    public int UdpOutTime = 20;

    private GameObject helperContainer;
    private Queue<UdplDataModel> dataModelsQueue = new Queue<UdplDataModel>();
    private Dictionary<int, VideoHelper> clientDict = new Dictionary<int, VideoHelper>();
    //private ConcurrentDictionary<long, List<UdpPacket>> packetCache = new ConcurrentDictionary<long, List<UdpPacket>>();

    private bool isRunning = false;
    private DateTime udpHeratTime;
    private Socket socket;
    private UdpSendSocket udpSendSocket;
    private UdpReceiveSocket udpReceiveSocket;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        udpSendSocket = new UdpSendSocket();
        udpReceiveSocket = new UdpReceiveSocket();
        helperContainer = new GameObject("HelperContainer");
    }

    private void Update() //FixedUpdate()  
    {
        if (isRunning && (DateTime.Now - udpHeratTime).TotalSeconds > UdpOutTime)
        {
            // TODO
            Debug.LogError("HeartBeat Error");
            //ChatUIManager.Instance.Hang();
        }
        lock (dataModelsQueue)
        {
            while (dataModelsQueue.Count > 0) 
            {
                ResolveModel(dataModelsQueue.Dequeue());
            }
        }
    }

    private void OnDestroy()
    {
        StopListening();
    }

    //发送udp心跳包
    private IEnumerator SendHeart()
    {
        Debug.Log("start heart...");
        while (isRunning)
        {
            yield return new WaitForSeconds(2);
            CallInfo callInfo = new CallInfo();
            // TODO
            callInfo.UserID = ConfigManager.LOCAL_ID;

            UdplDataModel model = new UdplDataModel();
            model.ChatInfoData = callInfo.ToByteArray();
            model.Request = RequestByte.REQUEST_HEART;

            byte[] data = UdpMessageCodec.Encode(model);
            Send(data);
        }
        Debug.Log("stop heart...");
    }

    public void OnReceiveData(byte[] data)
    {
        try
        {
            UdplDataModel model = UdpMessageCodec.Decode(data);
            switch (model.Request)
            {
                case RequestByte.REQUEST_HEART:
                    udpHeratTime = DateTime.Now;
                    break;
                case RequestByte.REQUEST_AUDIO:
                    //ReceivedAudioDataQueue.Enqueue(model.ChatData);
                    break;
                case RequestByte.REQUEST_VIDEO:
                    dataModelsQueue.Enqueue(model);
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("ReceivedDataQueue decode error:" + e.Message + "," + e.StackTrace);
        }
    }

    private void ResolveModel(UdplDataModel model)
    {
        CallInfo info = CallInfo.Parser.ParseFrom(model.ChatInfoData);
        if (!clientDict.TryGetValue(info.UserID, out VideoHelper helper)) 
        {
            helper = helperContainer.AddComponent<VideoHelper>();
            helper.UserID = info.UserID;
            clientDict.Add(info.UserID, helper);
        }
        helper.Enqueue(model.ChatData);
    }

    /// <summary>
    /// 开始udp监听
    /// </summary>
    public void StartListening()
    {
        if (isRunning) return;
        isRunning = true;

        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        udpSendSocket.IniSocket(socket, ConfigManager.ServerIP, ConfigManager.ServerPort);
        udpReceiveSocket.InitSocket(socket, ConfigManager.ServerIP, ConfigManager.ServerPort);
        udpReceiveSocket.OnReceiveData += OnReceiveData;

        Debug.Log("Start listening");
        StartCoroutine(SendHeart());

        udpHeratTime = DateTime.Now;
    }

    /// <summary>
    /// 停止udp监听
    /// </summary>
    public void StopListening()
    {
        if (!isRunning) return;
        isRunning = false;

        try
        {
            socket.Dispose();
            socket = null;

            udpSendSocket.UnInit();
            udpReceiveSocket.OnReceiveData -= OnReceiveData;
        }
        catch (Exception e)
        {
            Debug.LogError("!UnInitUdpNet error: " + e.Message + ", " + e.StackTrace);
        }
        Debug.Log("UnInitUdpNet OK!");
    }

    /// <summary>
    /// 发送udp数据
    /// </summary>
    /// <param name="buff"></param>
    public void Send(byte[] buff)
    {
        udpSendSocket.SendToAsyncByUDP(buff);
    }

}
