using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace WhoseStory
{
    // 每一轮一行，边玩边写。这是评估用的数据，事后补不回来。
    public class TranscriptLogger : MonoBehaviour
    {
        public static TranscriptLogger Instance { get; private set; }

        string path;
        int turn;
        float sessionStart;

        void Awake()
        {
            Instance = this;
            sessionStart = Time.realtimeSinceStartup;

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string folder = Path.Combine(Application.persistentDataPath, "transcripts");
            Directory.CreateDirectory(folder);
            path = Path.Combine(folder, $"session_{stamp}.jsonl");

            Debug.Log($"[WhoseStory] transcript: {path}");
        }

        public void Write(string character, string question, string reply, bool lied,
            float latencySeconds)
        {
            turn++;

            var line = new JObject
            {
                ["turn"] = turn,
                ["t"] = Math.Round(Time.realtimeSinceStartup - sessionStart, 3),
                ["character"] = character,
                ["question"] = question,
                ["reply"] = reply,
                ["lied"] = lied,
                ["latency"] = Math.Round(latencySeconds, 3)
            };

            File.AppendAllText(path, line.ToString(Newtonsoft.Json.Formatting.None) + "\n");
        }

        public void WriteVerdict(string accused, string reasoning)
        {
            var line = new JObject
            {
                ["turn"] = ++turn,
                ["t"] = Math.Round(Time.realtimeSinceStartup - sessionStart, 3),
                ["event"] = "verdict",
                ["accused"] = accused,
                ["reasoning"] = reasoning
            };

            File.AppendAllText(path, line.ToString(Newtonsoft.Json.Formatting.None) + "\n");
        }
    }
}
