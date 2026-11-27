using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace WhoseStory
{
    // 一个角色一份对话历史。系统提示词由三段拼成：
    // 共用的开头、这个角色自己的部分、共用的输出约定。
    [RequireComponent(typeof(AudioSource))]
    public class InterrogationCharacter : MonoBehaviour
    {
        [Header("身份")]
        public string characterName = "Snow White";

        [Tooltip("StreamingAssets/WhoseStory 下的文件名")]
        public string promptFileName = "snow_white.txt";

        [Tooltip("StreamingAssets/WhoseStory 下的共用开头；默认 preamble.txt（镜子案），五庄观后园用 wuzhuang_preamble.txt")]
        public string preambleFileName = "preamble.txt";

        [Tooltip("语音合成的音色")]
        public string voice = "shimmer";

        [Tooltip("播放语音时的音高（1 = 原声）。大于 1 更高，同时也会略快")]
        [Range(0.5f, 2f)] public float voicePitch = 1f;

        [Tooltip("填了就改用 ElevenLabs 的这个声音（在 ElevenLabs 网页上设计好后复制 voice id）；留空则用 OpenAI")]
        public string elevenLabsVoiceId = "";

        [Header("MiniMax（填了 voice id 就优先用 MiniMax 发声）")]
        [Tooltip("MiniMax 语音设计生成的 voice_id（Tools > Garden > Design MiniMax voices）；留空则不用 MiniMax")]
        public string minimaxVoiceId = "";
        [Range(0.5f, 2f)] public float minimaxSpeed = 1f;
        [Range(-12, 12)] public int minimaxPitch = 0;
        [Tooltip("happy / sad / angry / fearful / disgusted / surprised / calm；留空 = 模型自己判断")]
        public string minimaxEmotion = "";
        [Tooltip("撒谎那一句用的情绪（留空 = 同上）")]
        public string minimaxLyingEmotion = "";
        [Tooltip("MiniMax language_boost，例如 English / Chinese / auto")]
        public string minimaxLanguage = "English";

        [Tooltip("StreamingAssets/WhoseStory 下的语气说明文件；留空则按 prompt 文件名加 _voice 自动查找")]
        public string voiceFileName = "";

        [Header("表现")]
        public NoseGrower nose;

        AudioSource audioSource;
        readonly List<JObject> history = new();

        public bool IsBusy { get; private set; }
        public bool IsSpeaking => audioSource.isPlaying;
        public string LastReply { get; private set; } = "";
        public bool LastLied { get; private set; }

        // 语气说明分两段：平时怎么说，以及撒谎/否认那一句怎么说（用 [lying] 一行隔开）
        string voiceNormal = "";
        string voiceLying = "";

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.pitch = voicePitch;
            history.Add(new JObject
            {
                ["role"] = "system",
                ["content"] = BuildSystemPrompt()
            });
            LoadVoiceInstructions();
        }

        void LoadVoiceInstructions()
        {
            string folder = Path.Combine(Application.streamingAssetsPath, "WhoseStory");
            string file = string.IsNullOrEmpty(voiceFileName)
                ? Path.GetFileNameWithoutExtension(promptFileName) + "_voice.txt"
                : voiceFileName;
            string path = Path.Combine(folder, file);
            if (!File.Exists(path))
            {
                return;
            }
            string text = File.ReadAllText(path).Replace("\r\n", "\n");
            int split = text.IndexOf("[lying]");
            if (split < 0)
            {
                voiceNormal = text.Trim();
                voiceLying = voiceNormal;
            }
            else
            {
                voiceNormal = text.Substring(0, split).Trim();
                voiceLying = (voiceNormal + "\n\n" + text.Substring(split + "[lying]".Length).Trim()).Trim();
            }
        }

        string BuildSystemPrompt()
        {
            string folder = Path.Combine(Application.streamingAssetsPath, "WhoseStory");
            string preamble = File.ReadAllText(Path.Combine(folder, string.IsNullOrEmpty(preambleFileName) ? "preamble.txt" : preambleFileName)).TrimEnd();
            string part = File.ReadAllText(Path.Combine(folder, promptFileName)).TrimEnd();
            string contract = File.ReadAllText(Path.Combine(folder, "output_contract.txt")).TrimEnd();
            return preamble + "\n\n" + part + "\n\n" + contract;
        }

        public IEnumerator Ask(OpenAiClient client, string question)
        {
            IsBusy = true;
            float started = Time.realtimeSinceStartup;

            history.Add(new JObject { ["role"] = "user", ["content"] = question });

            CharacterReply reply = null;
            yield return client.Chat(history, r => reply = r);

            // 原样回填，下一轮模型看到的历史与它自己产出的一致
            history.Add(new JObject
            {
                ["role"] = "assistant",
                ["content"] = new JObject
                {
                    ["reply"] = reply.Reply,
                    ["lied"] = reply.Lied
                }.ToString(Newtonsoft.Json.Formatting.None)
            });

            LastReply = reply.Reply;
            LastLied = reply.Lied;

            float latency = Time.realtimeSinceStartup - started;
            TranscriptLogger.Instance.Write(characterName, question, reply.Reply, reply.Lied, latency);
            Debug.Log($"[WhoseStory] {characterName} | lied={reply.Lied} | {reply.Reply}");

            if (reply.Lied && nose != null)
            {
                nose.OnLie();
            }

            AudioClip clip = null;
            string tone = reply.Lied ? voiceLying : voiceNormal;
            if (!string.IsNullOrEmpty(minimaxVoiceId))
            {
                string emotion = reply.Lied && !string.IsNullOrEmpty(minimaxLyingEmotion) ? minimaxLyingEmotion : minimaxEmotion;
                yield return client.SpeakMiniMax(reply.Reply, minimaxVoiceId, minimaxSpeed, minimaxPitch, emotion, minimaxLanguage, c => clip = c);
            }
            else if (!string.IsNullOrEmpty(elevenLabsVoiceId))
            {
                yield return client.SpeakElevenLabs(reply.Reply, elevenLabsVoiceId, c => clip = c);
            }
            else
            {
                yield return client.Speak(reply.Reply, voice, tone, c => clip = c);
            }

            audioSource.clip = clip;
            audioSource.Play();

            while (audioSource.isPlaying)
            {
                yield return null;
            }

            IsBusy = false;
        }
    }
}
