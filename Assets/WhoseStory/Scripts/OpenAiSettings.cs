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

        string cachedKey;
        string cachedElevenKey;

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
