// Character voice + lip sync.
//   PlayLine(clip)            play a line; the mouth follows it, closes when silent
//   BeginStream/PushSamples/EndStream   streaming PCM (e.g. a TTS service) -> same lip sync
//   SetVisemes(a,i,u,e,o)     external driver (e.g. a uLipSync bridge) instead of the built-in analyzer
// Built-in analyzer: loudness opens the mouth; two formant-band centroids (F1 ~250-1000 Hz, F2 ~900-3000 Hz) pick the vowel
// shape (A/I/U/E/O) by distance to per-vowel prototypes (Mandarin vowels, editable). It reads the clip samples at the
// playback time, so it also works when audio output is off (batch capture).
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class CharacterVoice : MonoBehaviour
{
    public enum Driver { BuiltIn, External }

    [Header("Face")]
    public SkinnedMeshRenderer face;
    public string[] visemeShapes = { "A", "I", "U", "E", "O" };
    [Range(0, 100)] public float maxWeight = 100f;

    [Header("Lip sync")]
    public Driver driver = Driver.BuiltIn;
    public float silenceRms = 0.012f;          // below: mouth closed
    public float fullRms = 0.09f;              // at/above: fully open
    public float attack = 0.045f, release = 0.09f;   // smoothing time constants (s)
    [Tooltip("Formant prototypes (Hz): F1, F2 per vowel A I U E O (Mandarin, adult female/child-ish voice)")]
    public Vector2[] prototypes = { new Vector2(850, 1350), new Vector2(330, 2400), new Vector2(380, 850), new Vector2(560, 2000), new Vector2(560, 1000) };
    public float f1Scale = 160f, f2Scale = 420f;

    AudioSource src;
    int[] idx;
    readonly float[] target = new float[5], cur = new float[5];
    float[] clipData; int clipCh = 1, clipRate = 44100; double playStart = -1; AudioClip playing;
    // streaming
    readonly Queue<float> stream = new Queue<float>(); readonly List<float> recent = new List<float>(); bool streaming; int streamRate = 24000;
    const int N = 2048; readonly float[] re = new float[N], im = new float[N], win = new float[N];

    public AudioClip CurrentClip => playing;
    public double PlaybackTime => playing ? Time.timeAsDouble - playStart : 0;
    public bool IsSpeaking => (playing && Time.timeAsDouble - playStart < playing.length) || streaming || stream.Count > 0;

    void Awake()
    {
        src = GetComponent<AudioSource>();
        src.playOnAwake = false; src.spatialBlend = 0.6f;
        if (!face) foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>()) if (r.sharedMesh && r.sharedMesh.blendShapeCount > 0) { face = r; break; }
        idx = new int[visemeShapes.Length];
        for (int i = 0; i < idx.Length; i++) idx[i] = face ? face.sharedMesh.GetBlendShapeIndex(visemeShapes[i]) : -1;
        for (int i = 0; i < N; i++) win[i] = 0.5f - 0.5f * Mathf.Cos(2 * Mathf.PI * i / (N - 1));
    }

    // ------------------------------------------------------------------ public API
    public void PlayLine(AudioClip clip)
    {
        if (!clip) return;
        StopStream();
        clipCh = clip.channels; clipRate = clip.frequency;
        clipData = new float[clip.samples * clip.channels];
        if (clip.loadType != AudioClipLoadType.DecompressOnLoad || !clip.GetData(clipData, 0)) clipData = null;
        playing = clip; playStart = Time.timeAsDouble;
        src.clip = clip; src.Play();
    }

    public void Stop() { src.Stop(); playing = null; StopStream(); }

    /// <summary>Start a streamed line (e.g. TTS). Push mono float PCM with PushSamples; call EndStream when done.</summary>
    public void BeginStream(int sampleRate)
    {
        Stop(); streamRate = sampleRate; streaming = true; stream.Clear(); recent.Clear();
        var clip = AudioClip.Create("voice_stream", sampleRate * 2, 1, sampleRate, true, OnStreamRead);
        src.clip = clip; src.loop = true; src.Play();
    }
    public void PushSamples(float[] pcm) { lock (stream) foreach (var s in pcm) stream.Enqueue(s); }
    public void EndStream() { streaming = false; }
    void StopStream() { streaming = false; lock (stream) stream.Clear(); if (src && src.loop) { src.Stop(); src.loop = false; } }
    void OnStreamRead(float[] data)
    {
        lock (stream)
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = stream.Count > 0 ? stream.Dequeue() : 0f;
                lock (recent) { recent.Add(data[i]); if (recent.Count > N * 2) recent.RemoveRange(0, recent.Count - N); }
            }
    }

    /// <summary>External driver (uLipSync bridge etc.): weights 0..1.</summary>
    public void SetVisemes(float a, float i, float u, float e, float o)
    {
        target[0] = a; target[1] = i; target[2] = u; target[3] = e; target[4] = o;
    }

    // ------------------------------------------------------------------ analysis
    void Update()
    {
        if (driver == Driver.BuiltIn)
        {
            for (int k = 0; k < 5; k++) target[k] = 0;
            if (IsSpeaking && FillWindow(out int rate)) Analyze(rate);
        }
        float dt = Time.deltaTime;
        for (int k = 0; k < 5; k++)
        {
            float tau = target[k] > cur[k] ? attack : release;
            cur[k] += (target[k] - cur[k]) * (1 - Mathf.Exp(-dt / Mathf.Max(tau, 1e-3f)));
            if (face && idx[k] >= 0) face.SetBlendShapeWeight(idx[k], cur[k] * maxWeight);
        }
        if (playing && !IsSpeaking) playing = null;
    }

    bool FillWindow(out int rate)
    {
        rate = clipRate;
        if (playing && clipData != null)
        {
            long center = (long)((Time.timeAsDouble - playStart) * clipRate);
            for (int i = 0; i < N; i++)
            {
                long s = center - N / 2 + i; float v = 0;
                if (s >= 0 && s < clipData.Length / clipCh) for (int c = 0; c < clipCh; c++) v += clipData[s * clipCh + c];
                re[i] = v / clipCh * win[i]; im[i] = 0;
            }
            return true;
        }
        if (streaming || stream.Count > 0)
        {
            rate = streamRate;
            lock (recent)
            {
                if (recent.Count < N) return false;
                for (int i = 0; i < N; i++) { re[i] = recent[recent.Count - N + i] * win[i]; im[i] = 0; }
            }
            return true;
        }
        return false;
    }

    void Analyze(int rate)
    {
        double e = 0; for (int i = 0; i < N; i++) e += re[i] * re[i];
        float rms = Mathf.Sqrt((float)(e / N) / 0.375f);              // Hann window power correction
        float open = Mathf.Clamp01((rms - silenceRms) / Mathf.Max(1e-4f, fullRms - silenceRms));
        if (open <= 0) return;
        FFT(re, im);
        float hz = (float)rate / N;
        float Centroid(float lo, float hi)
        {
            double s = 0, w = 0;
            for (int b = Mathf.CeilToInt(lo / hz); b <= Mathf.FloorToInt(hi / hz) && b < N / 2; b++)
            { double m = re[b] * re[b] + im[b] * im[b]; s += m * b * hz; w += m; }
            return w > 0 ? (float)(s / w) : (lo + hi) / 2;
        }
        float f1 = Centroid(250, 1000), f2 = Centroid(900, 3000);
        float sum = 0; var w5 = new float[5];
        for (int k = 0; k < 5; k++)
        {
            float d1 = (f1 - prototypes[k].x) / f1Scale, d2 = (f2 - prototypes[k].y) / f2Scale;
            w5[k] = Mathf.Exp(-0.5f * (d1 * d1 + d2 * d2)); sum += w5[k];
        }
        for (int k = 0; k < 5; k++) target[k] = sum > 0 ? open * w5[k] / sum : 0;
        // open vowels read bigger on screen: keep at least a little "A" while loud
        target[0] = Mathf.Max(target[0], open * 0.25f);
    }

    static void FFT(float[] xr, float[] xi)
    {
        int n = xr.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1; for (; (j & bit) != 0; bit >>= 1) j ^= bit; j ^= bit;
            if (i < j) { (xr[i], xr[j]) = (xr[j], xr[i]); (xi[i], xi[j]) = (xi[j], xi[i]); }
        }
        for (int len = 2; len <= n; len <<= 1)
        {
            float ang = -2 * Mathf.PI / len; float wr = Mathf.Cos(ang), wi = Mathf.Sin(ang);
            for (int i = 0; i < n; i += len)
            {
                float cr = 1, ci = 0;
                for (int k = 0; k < len / 2; k++)
                {
                    int a = i + k, b = a + len / 2;
                    float tr = xr[b] * cr - xi[b] * ci, ti = xr[b] * ci + xi[b] * cr;
                    xr[b] = xr[a] - tr; xi[b] = xi[a] - ti; xr[a] += tr; xi[a] += ti;
                    float ncr = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = ncr;
                }
            }
        }
    }
}
