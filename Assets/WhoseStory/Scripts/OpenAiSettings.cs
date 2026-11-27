using System.IO;
using UnityEngine;

namespace WhoseStory
{
    [CreateAssetMenu(fileName = "OpenAiSettings", menuName = "Whose Story/OpenAI Settings")]
    public class OpenAiSettings : ScriptableObject
    {
        [Header("模型")]
        public string chatModel = "gpt-4o-mini-2024-07-18";
        public string transcribeModel = "gpt-4o-mini-transcribe";
        public string speechModel = "gpt-4o-mini-tts";

        [Header("生成参数")]
        [Range(0f, 2f)] public float temperature = 0.7f;
        public int maxTokens = 400;

        [Header("密钥")]
        [Tooltip("StreamingAssets/WhoseStory 下的文件名，内容只有一行 key。该文件不进版本库。")]
        public string keyFileName = "openai_key.txt";

        [Header("ElevenLabs（可选，只给填了 voice id 的角色用）")]
        [Tooltip("eleven_flash_v2_5 延迟低、支持中文；eleven_multilingual_v2 音质更好但更慢")]
        public string elevenModel = "eleven_flash_v2_5";
        [Tooltip("StreamingAssets/WhoseStory 下的文件名，内容只有一行 ElevenLabs key。该文件不进版本库。")]
        public string elevenKeyFileName = "elevenlabs_key.txt";

        [Header("MiniMax（可选，只给填了 MiniMax voice id 的角色用；人民币充值）")]
        [Tooltip("MiniMax 开放平台接口地址（国内）")]
        public string minimaxHost = "https://api.minimax.cn";
        [Tooltip("speech-2.8-hd 音质最好；speech-2.8-turbo 更快更便宜")]
        public string minimaxModel = "speech-2.8-hd";
        [Tooltip("StreamingAssets/WhoseStory 下的文件名：第一行 API key（第二行可留 GroupId，当前接口不需要）。该文件不进版本库、不进构建。")]
        public string minimaxKeyFileName = "minimax_key.txt";

        string cachedKey;
        string cachedElevenKey;
        string cachedMiniMaxKey;

        public string MiniMaxApiKey
        {
            get
            {
                if (!string.IsNullOrEmpty(cachedMiniMaxKey))
                {
                    return cachedMiniMaxKey;
                }

                string path = Path.Combine(Application.streamingAssetsPath, "WhoseStory", minimaxKeyFileName);
                string[] lines = File.ReadAllLines(path);
                cachedMiniMaxKey = lines.Length > 0 ? lines[0].Trim() : "";
                return cachedMiniMaxKey;
            }
        }

        public string ElevenApiKey
        {
            get
            {
                if (!string.IsNullOrEmpty(cachedElevenKey))
                {
                    return cachedElevenKey;
                }

                string path = Path.Combine(Application.streamingAssetsPath, "WhoseStory", elevenKeyFileName);
                cachedElevenKey = File.ReadAllText(path).Trim();
                return cachedElevenKey;
            }
        }

        public string ApiKey
        {
            get
            {
                if (!string.IsNullOrEmpty(cachedKey))
                {
                    return cachedKey;
                }

                string path = Path.Combine(Application.streamingAssetsPath, "WhoseStory", keyFileName);
                cachedKey = File.ReadAllText(path).Trim();
                return cachedKey;
            }
        }
    }
}
