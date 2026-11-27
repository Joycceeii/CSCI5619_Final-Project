using System;
using System.IO;
using UnityEngine;

namespace WhoseStory
{
    // 把 AudioClip 写成 16 位 PCM 的 WAV 字节，供语音识别接口上传。
    // 只负责写出，不读取也不解析任何音频文件。
    public static class WavWriter
    {
        public static byte[] FromClip(AudioClip clip)
        {
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            short[] pcm = new short[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                pcm[i] = (short)Mathf.Clamp(Mathf.RoundToInt(samples[i] * short.MaxValue),
                    short.MinValue, short.MaxValue);
            }

            byte[] data = new byte[pcm.Length * 2];
            Buffer.BlockCopy(pcm, 0, data, 0, data.Length);

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            int channels = clip.channels;
            int rate = clip.frequency;
            int byteRate = rate * channels * 2;

            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + data.Length);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });
            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(rate);
            writer.Write(byteRate);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(data.Length);
            writer.Write(data);
            writer.Flush();

            return stream.ToArray();
        }
    }
}
