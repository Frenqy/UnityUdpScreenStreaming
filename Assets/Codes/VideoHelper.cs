using ChatProto;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class VideoHelper : MonoBehaviour
{
    public int UserID;

    private Queue<byte[]> ReceivedVideoDataQueue = new Queue<byte[]>();
    private ConcurrentDictionary<long, List<UdpPacket>> packetCache = new ConcurrentDictionary<long, List<UdpPacket>>();

    private Texture2D lastTex;

    private void Update()
    {
        lock (ReceivedVideoDataQueue)
        {
            if (ReceivedVideoDataQueue.Count > 0)
            {
                VideoHandler(ReceivedVideoDataQueue.Dequeue());
            }
            if (ReceivedVideoDataQueue.Count > 15)
            {
                ReceivedVideoDataQueue.Clear();
            }
        }
    }

    public void Enqueue(byte[] data)
    {
        ReceivedVideoDataQueue.Enqueue(data);
    }

    private void VideoHandler(byte[] message)
    {
        UdpPacket packet = UdpPacketDecode(message);

        if (packet.Total == 1)
        {
            ReceiveVideo(packet.Chunk);
        }
        else if (packet.Total > 1)//需要组包
        {
            //超时未收到完整包，清理
            lock (packetCache)
            {
                if (packetCache.Count > 15 && packet.Index == 0) packetCache.Clear();
            }

            byte[] data = AddPacket(packet);
            if (data != null) ReceiveVideo(data);
        }
    }

    private byte[] AddPacket(UdpPacket udpPacket)
    {
        if (packetCache.ContainsKey(udpPacket.Sequence))
        {
            List<UdpPacket> udpPackets = null;
            if (packetCache.TryGetValue(udpPacket.Sequence, out udpPackets))
            {
                udpPackets.Add(udpPacket);

                if (udpPackets.Count == udpPacket.Total)
                {
                    packetCache.TryRemove(udpPacket.Sequence, out udpPackets);

                    udpPackets = udpPackets.OrderBy(u => u.Index).ToList();
                    int allLength = udpPackets.Sum(u => u.Chunk.Length);

                    //int maxPacketLength = udpPackets.Select(u => u.Chunk.Length).Max();

                    byte[] wholePacket = new byte[allLength];
                    foreach (var item in udpPackets)
                    {
                        Buffer.BlockCopy(item.Chunk, 0, wholePacket, item.Index * udpPacket.ChunkLength, item.Chunk.Length);
                    }
                    return wholePacket;
                }
            }
            return null;
        }
        else
        {
            List<UdpPacket> udpPackets = new List<UdpPacket>();
            udpPackets.Add(udpPacket);
            packetCache.AddOrUpdate(udpPacket.Sequence, udpPackets, (k, v) => { return udpPackets; });
            return null;
        }
    }

    private static UdpPacket UdpPacketDecode(byte[] data)
    {
        byte[] sequenceByte = new byte[8];
        Buffer.BlockCopy(data, 0, sequenceByte, 0, 8);

        byte[] totalByte = new byte[4];
        Buffer.BlockCopy(data, 8, totalByte, 0, 4);

        byte[] indexByte = new byte[4];
        Buffer.BlockCopy(data, 12, indexByte, 0, 4);

        byte[] chunkLengthByte = new byte[4];
        Buffer.BlockCopy(data, 16, chunkLengthByte, 0, 4);

        byte[] chunkByte = new byte[data.Length - 20];
        Buffer.BlockCopy(data, 20, chunkByte, 0, data.Length - 20);

        UdpPacket packet = new UdpPacket();

        packet.Sequence = BitConverter.ToInt64(sequenceByte, 0);
        packet.Total = BitConverter.ToInt32(totalByte, 0);
        packet.Index = BitConverter.ToInt32(indexByte, 0);
        packet.ChunkLength = BitConverter.ToInt32(chunkLengthByte, 0);
        packet.Chunk = chunkByte;

        return packet;
    }

    public static VideoPacket ProtobufPack2VideoPack(PbVideoPacket packet)
    {
        VideoPacket video = new VideoPacket();
        video.Id = packet.Id;
        video.Width = packet.Width;
        video.Height = packet.Height;
        video.Timestamp = packet.Timestamp;

        if (packet.Data != null)
            video.Data = packet.Data.ToByteArray();

        if (packet.FloatData != null)
            video.FloatData.AddRange(packet.FloatData);
        return video;
    }

    public void ReceiveVideo(byte[] data)
    {
        // TODO object pool
        //SDK进行视频数据的解码及视频渲染
        if (lastTex != null) Destroy(lastTex);
        PbVideoPacket packet = PbVideoPacket.Parser.ParseFrom(data);
        lastTex = CameraHelper.Instance.DecodeVideoData(ProtobufPack2VideoPack(packet));
        MainManager.Instance.UpdateVideo(UserID, lastTex);
        //Debug.Log($"Received data length: {data.Length}");
    }

}
