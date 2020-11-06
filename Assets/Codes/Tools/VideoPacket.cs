using System.Collections.Generic;

public class VideoPacket
{
    public int Id;
    public int Width;
    public int Height;
    public byte[] Data;
    public long Timestamp;
    public List<float> FloatData;

    public VideoPacket()
    {
        FloatData = new List<float>();
    }
}