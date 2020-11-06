using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;
using Object = System.Object;
using Cysharp.Threading.Tasks;

public class CameraHelper : MonoBehaviour
{
    const int width = 1666 / 2;
    const int height = 1666 / 2;

    public static CameraHelper Instance { get; private set; }

    private RenderTexture localRenderTexture;

    private Color32[] rawData;
    private byte[] jpgData;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        localRenderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, 0);
    }

    private void Update()
    {
        if (Input.GetKeyUp(KeyCode.R))
        {
            GetVideo();
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(source, localRenderTexture);
        Graphics.Blit(source, destination);
    }

    public async UniTask<VideoPacket> GetVideo()
    {
        await UniTask.WaitForEndOfFrame();
        var request = AsyncGPUReadback.Request(localRenderTexture);
        await UniTask.WaitUntil(() => request.done);
        rawData = request.GetData<Color32>().ToArray();

        await UniTask.SwitchToThreadPool();
        await UniTask.Run(() =>
        {
            jpgData = ImageConversion.EncodeArrayToJPG(rawData, GraphicsFormat.R8G8B8A8_UNorm, width, height);
        });

        byte[] data = jpgData;

        VideoPacket packet = new VideoPacket();
        packet.Id = ConfigManager.LOCAL_ID;
        packet.Width = width;
        packet.Height = height;
        packet.Timestamp = ConvertDateTimeToLong(DateTime.Now);
        packet.Data = data;

        return packet;
    }

    public Texture2D DecodeVideoData(VideoPacket packet)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGB565, false);
        texture.LoadImage(packet.Data);
        return texture;
    }

    private long ConvertDateTimeToLong(DateTime time)
    {
        DateTime startTime = TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1));
        return (long)(time - startTime).TotalSeconds;
    }
}
