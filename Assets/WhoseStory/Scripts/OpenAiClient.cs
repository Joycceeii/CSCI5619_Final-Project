using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace WhoseStory
{
    // 三个接口的调用：语音识别、对话、语音合成。
    // 出错就地抛出，不捕获也不退化，这样问题停在发生的地方。
    public class OpenAiClient : MonoBehaviour
    {
        const string ChatUrl = "https://api.openai.com/v1/chat/completions";
        const string TranscribeUrl = "https://api.openai.com/v1/audio/transcriptions";
        const string SpeechUrl = "https://api.openai.com/v1/audio/speech";
        const string ElevenSpeechUrl = "https://api.elevenlabs.io/v1/text-to-speech/";

        public OpenAiSettings settings;

        // 回答的 JSON 结构约束，与提示词测试中使用的完全一致
        static JObject BuildSchema()
        {
            return JObject.Parse(@"{
              ""name"": ""character_reply"",
              ""strict"": true,
              ""schema"": {
                ""type"": ""object"",
                ""properties"": {
                  ""reply"": { ""type"": ""string"" },
                  ""lied"": { ""type"": ""boolean"" }
                },
                ""additionalProperties"": false,
                ""required"": [""reply"", ""lied""]
              }
            }");
        }

        public IEnumerator Transcribe(byte[] wavBytes, Action<string> onDone)
        {
            var form = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("model", settings.transcribeModel),
                new MultipartFormFileSection("file", wavBytes, "speech.wav", "audio/wav")
            };

            using UnityWebRequest request = UnityWebRequest.Post(TranscribeUrl, form);
            request.SetRequestHeader("Authorization", "Bearer " + settings.ApiKey);
            yield return request.SendWebRequest();
            RequireSuccess(request, "transcription");

            JObject body = JObject.Parse(request.downloadHandler.text);
            onDone(body["text"].Value<string>());
        }

        public IEnumerator Chat(List<JObject> messages, Action<CharacterReply> onDone)
        {
            var payload = new JObject
            {
                ["model"] = settings.chatModel,
                ["messages"] = new JArray(messages),
                ["temperature"] = settings.temperature,
                ["max_tokens"] = settings.maxTokens,
                ["response_format"] = new JObject
                {
                    ["type"] = "json_schema",
                    ["json_schema"] = BuildSchema()
                }
            };

            byte[] bytes = Encoding.UTF8.GetBytes(payload.ToString(Formatting.None));

            using UnityWebRequest request = new UnityWebRequest(ChatUrl, "POST");
            request.uploadHandler = new UploadHandlerRaw(bytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + settings.ApiKey);
            yield return request.SendWebRequest();
            RequireSuccess(request, "chat completion");

            JObject body = JObject.Parse(request.downloadHandler.text);
            string content = body["choices"][0]["message"]["content"].Value<string>();
            onDone(JsonConvert.DeserializeObject<CharacterReply>(content));
        }

        public IEnumerator Speak(string text, string voice, string instructions, Action<AudioClip> onDone)
        {
            var payload = new JObject
            {
                ["model"] = settings.speechModel,
                ["voice"] = voice,
                ["input"] = text,
                ["response_format"] = "mp3"
            };
            // 语气、语速、情绪的说明，只有 gpt-4o-mini-tts 系列支持
            if (!string.IsNullOrWhiteSpace(instructions))
            {
                payload["instructions"] = instructions;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(payload.ToString(Formatting.None));

            byte[] audio;
            using (UnityWebRequest request = new UnityWebRequest(SpeechUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + settings.ApiKey);
                yield return request.SendWebRequest();
                RequireSuccess(request, "speech synthesis");
                audio = request.downloadHandler.data;
            }

            yield return DecodeMp3(audio, onDone);
        }

        // ElevenLabs 语音合成：音色在 ElevenLabs 网页上设计好，这里只传 voice id
        public IEnumerator SpeakElevenLabs(string text, string voiceId, Action<AudioClip> onDone)
        {
            var payload = new JObject
            {
                ["text"] = text,
                ["model_id"] = settings.elevenModel
            };

            byte[] bytes = Encoding.UTF8.GetBytes(payload.ToString(Formatting.None));

            byte[] audio;
            string url = ElevenSpeechUrl + voiceId + "?output_format=mp3_44100_128";
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("xi-api-key", settings.ElevenApiKey);
                yield return request.SendWebRequest();
                RequireSuccess(request, "ElevenLabs speech synthesis");
                audio = request.downloadHandler.data;
            }

            yield return DecodeMp3(audio, onDone);
        }

        IEnumerator DecodeMp3(byte[] audio, Action<AudioClip> onDone)
        {
            // 交给 Unity 自己的解码器，不自行解析音频容器
            string path = Path.Combine(Application.temporaryCachePath, "line.mp3");
            File.WriteAllBytes(path, audio);

            using UnityWebRequest load = UnityWebRequestMultimedia.GetAudioClip(
                "file://" + path, AudioType.MPEG);
            yield return load.SendWebRequest();
            RequireSuccess(load, "audio decode");

            onDone(DownloadHandlerAudioClip.GetContent(load));
        }

        static void RequireSuccess(UnityWebRequest request, string what)
        {
            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new Exception(
                    $"{what} failed: {request.responseCode} {request.error}\n{request.downloadHandler?.text}");
            }
        }
    }
}
