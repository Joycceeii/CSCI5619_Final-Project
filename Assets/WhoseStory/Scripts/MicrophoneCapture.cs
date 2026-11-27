using UnityEngine;

namespace WhoseStory
{
    // 按住说话。开始录制时记下时间，松开后把实际长度裁出来。
    public class MicrophoneCapture : MonoBehaviour
    {
        public int sampleRate = 16000;
        public int maxSeconds = 30;

        AudioClip recording;
        string deviceName;
        float startTime;

        public bool IsRecording { get; private set; }

        public string DeviceName => deviceName;

        void Awake()
        {
            if (Microphone.devices.Length == 0)
            {
                throw new System.Exception("no microphone device found");
            }

            deviceName = Microphone.devices[0];
            Debug.Log($"[WhoseStory] microphone: {deviceName} "
                      + $"(of {Microphone.devices.Length} device(s))");
        }

        public void Begin()
        {
            recording = Microphone.Start(deviceName, false, maxSeconds, sampleRate);
            startTime = Time.realtimeSinceStartup;
            IsRecording = true;
        }

        // 返回这一次说话的 WAV 字节
        public byte[] End()
        {
            int position = Microphone.GetPosition(deviceName);
            Microphone.End(deviceName);
            IsRecording = false;

            float elapsed = Time.realtimeSinceStartup - startTime;
            if (position <= 0 || elapsed < 0.3f)
            {
                return null;
            }

            float[] samples = new float[position * recording.channels];
            recording.GetData(samples, 0);

            AudioClip trimmed = AudioClip.Create("speech", position, recording.channels, sampleRate, false);
            trimmed.SetData(samples, 0);

            return WavWriter.FromClip(trimmed);
        }
    }
}
