// MiniMax voice design for the three suspects (billed per call: run it yourself, only after putting your key in
// Assets/StreamingAssets/WhoseStory/minimax_key.txt).
//   Tools > Garden > Design MiniMax voices   -> one voice_design call per character in Assets/Garden/Voices/minimax_voice_prompts.json,
//      saves the trial clips to _models/voices/<key>_trial.mp3 and the voice ids to Assets/Garden/Voices/minimax_voice_ids.json
//   Tools > Garden > Apply MiniMax voices to Interrogation_Garden -> writes the saved voice ids (+ speed/pitch/emotion) onto the suspects
// InterrogationGardenSetup also applies the saved ids whenever it rebuilds the scene.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WhoseStory;

public static class MiniMaxVoiceDesigner
{
    const string PromptsPath = "Assets/Garden/Voices/minimax_voice_prompts.json";
    const string IdsPath = "Assets/Garden/Voices/minimax_voice_ids.json";
    static string TrialDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../_models/voices"));

    [MenuItem("Tools/Garden/Design MiniMax voices (billed)")]
    public static void Design()
    {
        var settings = AssetDatabase.LoadAssetAtPath<OpenAiSettings>("Assets/WhoseStory/OpenAiSettings.asset");
        string keyPath = Path.Combine(Application.streamingAssetsPath, "WhoseStory", settings.minimaxKeyFileName);
        if (!File.Exists(keyPath)) { EditorUtility.DisplayDialog("MiniMax", "Put your MiniMax API key (first line) in\n" + keyPath, "OK"); return; }
        var prompts = JObject.Parse(File.ReadAllText(PromptsPath))["voices"] as JArray;
        if (!EditorUtility.DisplayDialog("MiniMax voice design",
                $"This calls the paid MiniMax voice design API {prompts.Count} times (one per character). Continue?", "Design", "Cancel")) return;
        Directory.CreateDirectory(TrialDir);
        var ids = File.Exists(IdsPath) ? JObject.Parse(File.ReadAllText(IdsPath)) : new JObject();
        foreach (JObject v in prompts)
        {
            string key = v["key"].Value<string>();
            using var req = OpenAiClient.MiniMaxDesignRequest(settings, v["prompt"].Value<string>(), v["preview_text"].Value<string>());
            var op = req.SendWebRequest();
            while (!op.isDone) System.Threading.Thread.Sleep(50);
            if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success) { Debug.LogError($"[MiniMax] {key}: {req.responseCode} {req.error}\n{req.downloadHandler.text}"); continue; }
            var body = JObject.Parse(req.downloadHandler.text);
            OpenAiClient.RequireMiniMaxOk(body, "MiniMax voice design " + key);
            string vid = body["voice_id"].Value<string>();
            var trial = body["trial_audio"]?.Value<string>();
            if (!string.IsNullOrEmpty(trial)) File.WriteAllBytes(Path.Combine(TrialDir, $"{key}_trial.mp3"), OpenAiClient.HexToBytes(trial));
            ids[key] = vid;
            Debug.Log($"[MiniMax] {key} -> {vid}");
        }
        File.WriteAllText(IdsPath, ids.ToString());
        AssetDatabase.ImportAsset(IdsPath);
        Apply();
        EditorUtility.RevealInFinder(TrialDir);
    }

    [MenuItem("Tools/Garden/Apply MiniMax voices to Interrogation_Garden")]
    public static void Apply()
    {
        var scene = EditorSceneManager.OpenScene(InterrogationGardenSetup.ScenePath, OpenSceneMode.Single);
        ApplyToOpenScene();
        EditorSceneManager.SaveScene(scene);
    }

    /// <summary>Writes saved voice ids (if any) onto the suspects of the open scene. Returns how many were set.</summary>
    public static int ApplyToOpenScene()
    {
        if (!File.Exists(IdsPath)) return 0;
        var ids = JObject.Parse(File.ReadAllText(IdsPath));
        var prompts = (JObject.Parse(File.ReadAllText(PromptsPath))["voices"] as JArray).Cast<JObject>().ToDictionary(v => v["key"].Value<string>());
        int n = 0;
        foreach (var kv in ids)
        {
            if (!prompts.TryGetValue(kv.Key, out var p)) continue;
            var go = GameObject.Find(p["object"].Value<string>()); if (!go) continue;
            var c = go.GetComponent<InterrogationCharacter>(); if (!c) continue;
            c.minimaxVoiceId = kv.Value.Value<string>();
            c.minimaxSpeed = p["speed"]?.Value<float>() ?? 1f;
            c.minimaxPitch = p["pitch"]?.Value<int>() ?? 0;
            c.minimaxEmotion = p["emotion"]?.Value<string>() ?? "";
            c.minimaxLyingEmotion = p["lying_emotion"]?.Value<string>() ?? "";
            c.minimaxLanguage = "English";
            c.voicePitch = 1f;   // MiniMax voices are designed at the right pitch; no extra AudioSource pitch shift
            EditorUtility.SetDirty(c); n++;
        }
        return n;
    }
}
