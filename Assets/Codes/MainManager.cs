using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainManager : MonoBehaviour
{
    public static MainManager Instance { get; private set; }

    public bool EchoCancellation;
    public bool EnableDetection;
    public Camera CaptureCamera;

    public GameObject videoPrefab;
    public Transform videoContainer;
    private Dictionary<int, RawImage> videoDict = new Dictionary<int, RawImage>();

    private void Awake()
    {
        Instance = this;
    }

    public void UpdateVideo(int userID, Texture2D tex)
    {
        if (!videoDict.TryGetValue(userID, out RawImage image))
        {
            GameObject go = Instantiate(videoPrefab, videoContainer);
            image = go.GetComponent<RawImage>();
            videoDict.Add(userID, image);
        }
        image.texture = tex;
    }
}
