using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Google.Protobuf;
using System;
using UdpStreamProtocol;
using ChatProto;
using Cysharp.Threading.Tasks;

public class ChatDataHandler : MonoBehaviour
{
    public static ChatDataHandler Instance;

    private bool IsStartChat;
    public int ChunkLength = 50000;    //udp分包长度<66500，对于个别平台对长度有限制，适当降低长度(10000)

    private long udpPacketIndex;
    private Queue<VideoPacket> videoPacketQueue = new Queue<VideoPacket>();

    private void Start()
    {
        Instance = this;
    }

    //开始聊天后，在FixedUpdate会把捕捉到的音频和视频通过网络传输
    //SDK会判断音视频的刷新率，自动识别声音大小，判断视频画面是否静止，优化数据大小
    //注意：FixedUpdate Time的值需要小于 1/(Framerate+5),可设置为0.02或更小
    //private void FixedUpdate()
    //{
    //    if (!IsStartChat)
    //        return;

    //    SendVideo();
    //}

    /// <summary>
    /// 开始聊天
    /// </summary>
    public void StartChat()
    {
        OnStartChat();
    }

    /// <summary>
    /// 停止聊天
    /// </summary>
    public void StopChat()
    {
        StartCoroutine(OnStopChat());
    }

    /// <summary>
    /// 发送视频数据
    /// </summary>
    private async void SendVideo()
    {
        //获取SDK捕捉的视频数据
        VideoPacket packet = await CameraHelper.Instance.GetVideo();

        if (ConfigManager.ENABLE_SYNC)
        {
            if (packet != null)
                videoPacketQueue.Enqueue(packet);

            if (videoPacketQueue.Count >= ConfigManager.FRAME_RATE / ConfigManager.AUDIO_SAMPLE)
            {
                packet = videoPacketQueue.Dequeue();
            }
            else
            {
                return;
            }
        }

        if (packet != null)
        {
            packet.Id = ConfigManager.LOCAL_ID;
            byte[] video = VideoPack2ProtobufPack(packet).ToByteArray();

            udpPacketIndex++;
            List<UdpPacket> list = UdpPacketSpliter.Split(udpPacketIndex, video, ChunkLength);
            for (int i = 0; i < list.Count; i++)
            {
                UdplDataModel model = new UdplDataModel();
                model.Request = RequestByte.REQUEST_VIDEO;

                CallInfo info = new CallInfo();
                info.UserID = ConfigManager.LOCAL_ID;
                info.CallID = ConfigManager.CALL_ID;
                info.PeerList.Add(ConfigManager.REMOTE_ID);

                model.ChatInfoData = info.ToByteArray();
                model.ChatData = UdpPacketEncode(list[i]);

                UdpSocketManager.Instance.Send(UdpMessageCodec.Encode(model));
            }
        }
    }

    private PbVideoPacket VideoPack2ProtobufPack(VideoPacket video)
    {
        PbVideoPacket pbPacket = new PbVideoPacket();

        pbPacket.Id = video.Id;
        pbPacket.Width = video.Width;
        pbPacket.Height = video.Height;
        pbPacket.Timestamp = video.Timestamp;

        if (video.Data != null)
            pbPacket.Data = ByteString.CopyFrom(video.Data);

        if (video.FloatData != null)
            pbPacket.FloatData.AddRange(video.FloatData);
        return pbPacket;
    }

    private byte[] UdpPacketEncode(UdpPacket packet)
    {
        byte[] newByte = new byte[packet.Chunk.Length + 20];
        Buffer.BlockCopy(BitConverter.GetBytes(packet.Sequence), 0, newByte, 0, 8);
        Buffer.BlockCopy(BitConverter.GetBytes(packet.Total), 0, newByte, 8, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(packet.Index), 0, newByte, 12, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(packet.ChunkLength), 0, newByte, 16, 4);
        Buffer.BlockCopy(packet.Chunk, 0, newByte, 20, packet.Chunk.Length);
        return newByte;
    }

    private void OnStartChat()
    {
        try
        {
            UdpSocketManager.Instance.StartListening();
            IsStartChat = true;
            udpPacketIndex = 0;
            Debug.Log("OnStartChat");
        }
        catch (Exception e)
        {
            Debug.LogError("OnStartChat error:" + e.Message);
        }
    }

    private IEnumerator OnStopChat()
    {
        yield return new WaitForEndOfFrame();
        try
        {
            Hang();

            UdpSocketManager.Instance.StopListening();
            videoPacketQueue.Clear();
            IsStartChat = false;
            Debug.Log("OnStopChat");
        }
        catch (Exception e)
        {
            Debug.LogError("OnStopChat error:" + e.Message);
        }
    }

    private static void Hang()
    {
        //send udp hang
        CallInfo callInfo = new CallInfo();
        callInfo.UserID = ConfigManager.LOCAL_ID;
        callInfo.CallID = ConfigManager.CALL_ID;

        UdplDataModel model = new UdplDataModel();
        model.Request = RequestByte.REQUEST_HANG;
        model.ChatInfoData = callInfo.ToByteArray();
        byte[] data = UdpMessageCodec.Encode(model);

        UdpSocketManager.Instance.Send(data);
    }

}
