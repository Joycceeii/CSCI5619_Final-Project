// Bridge: uLipSync (hecomi) -> CharacterVoice visemes (A/I/U/E/O blendshapes, smoothing in CharacterVoice).
// uLipSync analyses the AudioSource output (OnAudioFilterRead). When there is no audio output (batch capture,
// muted editor) set feedFromClip: the bridge then feeds uLipSync the clip samples at the playback time itself.
using UnityEngine;

[RequireComponent(typeof(CharacterVoice))]
public class ULipSyncVoice : MonoBehaviour
{
    public uLipSync.uLipSync lipSync;
    public string[] phonemes = { "A", "I", "U", "E", "O" };
    [Tooltip("log10(rms) mapped to mouth opening (uLipSync defaults -2.5 .. -1.5)")]
    public float minVolume = -2.5f, maxVolume = -1.4f;
    [Tooltip("Feed uLipSync from the clip data instead of the audio output (batch mode / no audio device)")]
    public bool feedFromClip = false;

    CharacterVoice voice;
    long fedSample = -1; AudioClip fedClip; float[] clipBuf;

    void Awake()
    {
        voice = GetComponent<CharacterVoice>();
        if (!lipSync) lipSync = GetComponent<uLipSync.uLipSync>();
        voice.driver = CharacterVoice.Driver.External;
        if (Application.isBatchMode) feedFromClip = true;
        if (feedFromClip && lipSync)
        {
            // ignore OnAudioFilterRead: route uLipSync to a silent proxy
            var proxyGo = new GameObject("uLipSync silent proxy"); proxyGo.transform.SetParent(transform, false);
            lipSync.audioSourceProxy = proxyGo.AddComponent<uLipSync.uLipSyncAudioSource>();
        }
    }

    void OnEnable() { if (lipSync) lipSync.onLipSyncUpdate.AddListener(OnLipSync); }
    void OnDisable() { if (lipSync) lipSync.onLipSyncUpdate.RemoveListener(OnLipSync); }

    void Update()
    {
        if (!feedFromClip || !lipSync) return;
        var clip = voice.CurrentClip;
        if (!clip || !voice.IsSpeaking) { fedClip = null; return; }
        if (clip != fedClip)
        {
            fedClip = clip; fedSample = 0;
            clipBuf = new float[clip.samples * clip.channels]; clip.GetData(clipBuf, 0);
        }
        long now = (long)(voice.PlaybackTime * clip.frequency);
        now = System.Math.Min(now, clip.samples);
        if (now <= fedSample) return;
        int n = (int)(now - fedSample) * clip.channels;
        var chunk = new float[n];
        System.Array.Copy(clipBuf, fedSample * clip.channels, chunk, 0, n);
        lipSync.OnDataReceived(chunk, clip.channels);
        fedSample = now;
    }

    void OnLipSync(uLipSync.LipSyncInfo info)
    {
        float open = 0;
        if (info.rawVolume > 1e-6f) open = Mathf.Clamp01((Mathf.Log10(info.rawVolume) - minVolume) / (maxVolume - minVolume));
        if (!voice.IsSpeaking) open = 0;
        float R(string p) => info.phonemeRatios != null && info.phonemeRatios.TryGetValue(p, out var r) ? r : 0f;
        voice.SetVisemes(R(phonemes[0]) * open, R(phonemes[1]) * open, R(phonemes[2]) * open, R(phonemes[3]) * open, R(phonemes[4]) * open);
    }
}
