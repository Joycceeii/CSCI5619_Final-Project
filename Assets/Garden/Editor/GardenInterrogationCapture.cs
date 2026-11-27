// Offline Play test of Interrogation_Garden (batch): builds the scene, enters Play mode and records from the player's eye.
// No OpenAI call is made (that would spend the user's API credit): the question goes through the same bridge entry
// (GardenInterrogationBridge.OnQuestion, as InterrogationManager.QuestionPut does) and the "reply" is Wukong's pre-recorded
// test line played on his InterrogationCharacter AudioSource, exactly like a TTS reply clip.
//   Unity -batchmode -force-d3d12 -projectPath <WuzhuangCase> -executeMethod GardenInterrogationCapture.BatchRun
// Output: _models/courtyard_atlas/capture/interrogation_play/NNNN.jpg + interrogation_play_log.txt + gate_closeup_v18.jpg
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WhoseStory;

[InitializeOnLoad]
public static class GardenInterrogationCapture
{
    const string Key = "GardenInterrogationCapture.active";
    public const string Question = "你那天晚上有没有偷人参果？";
    static string Out => Path.Combine(GardenSetup.AtlasDir, "capture");
    static string Frames => Path.Combine(Out, "interrogation_play");
    static GardenInterrogationCapture() { EditorApplication.update += Tick; }

    public static void BatchRun()
    {
        Directory.CreateDirectory(Frames);
        foreach (var f in Directory.GetFiles(Frames, "*.jpg")) File.Delete(f);
        ShaderUtil.allowAsyncCompilation = false;
        var l = InterrogationGardenSetup.Build(true);
        File.WriteAllText(Path.Combine(Out, "interrogation_play_log.txt"), l);
        if (l.Contains("ERROR")) { EditorApplication.Exit(1); return; }
        EditorSceneManager.OpenScene(InterrogationGardenSetup.ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    static int frame; static bool asked, answered, gate, done; static float t0 = -1;
    // live mode (GARDEN_LIVE=1): exactly these three rounds, nothing more
    static bool Live => System.Environment.GetEnvironmentVariable("GARDEN_LIVE") == "1";
    static readonly (int who, string q)[] Rounds =
    {
        (2, "How long have you been travelling with your master?"),
        (0, "Did you steal the ginseng fruit from the garden last night?"),
        (1, "Bajie, did you eat any of the ginseng fruit?"),
    };
    static readonly (float at, int who, string q, string clip)[] Offline =
    {
        (0.8f, 2, "How long have you been travelling with your master?", null),
        (1.6f, 2, null, "Assets/Audio/Test/en/shaseng_line_en.wav"),
        (8.0f, 0, "Did you steal the ginseng fruit from the garden last night?", null),
        (8.8f, 0, null, "Assets/Audio/Test/en/wukong_line_en.wav"),
        (14.2f, 1, "Bajie, did you eat any of the ginseng fruit?", null),
        (15.0f, 1, null, "Assets/Audio/Test/en/bajie_line_en.wav"),
    };
    static readonly bool[] offFired = new bool[6];
    static int round, apiCalls, clipN; static float nextAt = 1.0f; static readonly AudioClip[] lastClip = new AudioClip[3];
    static void LogMouths(InterrogationManager mgr, float t)
    {
        var sb = new StringBuilder($"t={t:F2} mouth");
        foreach (var c in mgr.characters)
        {
            var v = c.GetComponent<CharacterVoice>(); var smr = v.face; float open = 0;
            for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++) open = Mathf.Max(open, smr.GetBlendShapeWeight(i));
            sb.Append("  " + c.characterName.Split(' ')[0] + " " + open.ToString("F0") + (v.IsSpeaking ? "*" : ""));
        }
        log.AppendLine(sb.ToString());
    }
    static readonly StringBuilder log = new StringBuilder();
    static bool hooked;

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (!EditorApplication.isPlaying)
        {
            if (SessionState.GetBool(Key + ".done", false)) { SessionState.EraseBool(Key); SessionState.EraseBool(Key + ".done"); EditorApplication.Exit(0); }
            return;
        }
        if (!hooked) { hooked = true; Application.logMessageReceived += (m, st, type) => { if (type == LogType.Error || type == LogType.Exception) log.AppendLine($"[{type}] {m}"); }; }
        Time.captureFramerate = 30;
        if (done) return;
        var view = Object.FindAnyObjectByType<GardenView>(); var cam = Camera.main;
        var mgr = Object.FindAnyObjectByType<InterrogationManager>(); var bridge = Object.FindAnyObjectByType<GardenInterrogationBridge>();
        if (!view || !cam || !mgr || !bridge) return;
        var sp = view.ActiveSplat ? view.ActiveSplat.GetComponent<GaussianSplatting.Runtime.GaussianSplatRenderer>() : null;
        if (sp && (sp.isLoading || sp.splatCount == 0)) return;
        if (t0 < 0) { t0 = Time.time; log.AppendLine($"play: manager ok, {mgr.characters.Length} suspects, playerHead {mgr.playerHead.name}, XR origin {cam.transform.root.name}"); }
        float t = Time.time - t0;
        var wukong = mgr.characters[0];
        var tells = wukong.GetComponent<SuspectTells>();

        if (!gate)
        {
            // gate close-up (seam check) once, then back to the player view
            gate = true;
            var p = cam.transform.position; var r = cam.transform.rotation; float fov = cam.fieldOfView;
            cam.fieldOfView = 52f; var eye = new Vector3(-0.5f, 1.65f, -5.4f);
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(new Vector3(-0.5f, 1.7f, -10.6f) - eye));
            Save(cam, 1920, 1080, Path.Combine(Out, "gate_closeup_v18.jpg"));
            cam.transform.SetPositionAndRotation(p, r); cam.fieldOfView = fov;
            log.AppendLine("gate_closeup_v18.jpg");
            return;
        }
        if (Live)
        {
            // live English rounds through the real interrogation path (typed question -> OpenAI chat -> OpenAI TTS -> AudioSource)
            cam.fieldOfView = 40f;
            Vector3 mid = (mgr.characters[0].transform.position + mgr.characters[2].transform.position) / 2 + Vector3.up * 0.8f;
            cam.transform.rotation = Quaternion.LookRotation(mid - cam.transform.position);
            var cur = round > 0 ? mgr.characters[Rounds[round - 1].who] : null;
            bool idle = cur == null || (!cur.IsBusy && !cur.IsSpeaking);
            if (idle && round < Rounds.Length && t >= nextAt)
            {
                var (who, q) = Rounds[round]; var c = mgr.characters[who];
                log.AppendLine($"t={t:F2} LIVE Q{round + 1} to {c.characterName}: {q}");
                var ask = typeof(InterrogationManager).GetMethod("AskTyped", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                mgr.StartCoroutine((System.Collections.IEnumerator)ask.Invoke(mgr, new object[] { q, c }));
                apiCalls += 2;   // chat completion + speech synthesis
                round++; nextAt = float.MaxValue;
            }
            cur = round > 0 ? mgr.characters[Rounds[round - 1].who] : null;
            idle = cur == null || (!cur.IsBusy && !cur.IsSpeaking);
            if (cur != null && idle && nextAt == float.MaxValue)
            {
                if (!string.IsNullOrEmpty(cur.LastReply)) log.AppendLine($"t={t:F2}    {cur.characterName} said (lied={cur.LastLied}): {cur.LastReply}");
                nextAt = t + 1.0f;
            }
            foreach (var c in mgr.characters)
            {
                var src = c.GetComponent<AudioSource>();
                if (src.isPlaying && src.clip && src.clip != lastClip[System.Array.IndexOf(mgr.characters, c)])
                {
                    int k = System.Array.IndexOf(mgr.characters, c); lastClip[k] = src.clip;
                    string wav = Path.Combine(Frames, $"reply_{++clipN}_{c.characterName.Split(' ')[0]}.wav");
                    File.WriteAllBytes(wav, WavWriter.FromClip(src.clip));
                    log.AppendLine($"AUDIO {t:F3} {Path.GetFileName(wav)} {src.clip.length:F2}s");
                }
            }
            Save(cam, 1920, 1080, Path.Combine(Frames, $"{frame++:0000}.jpg"));
            if (frame % 15 == 0) LogMouths(mgr, t);
            if ((round >= Rounds.Length && idle && t > nextAt + 0.5f && nextAt != float.MaxValue) || t > 90f)
            {
                done = true; log.AppendLine($"LIVE done: {round} rounds, {apiCalls} OpenAI calls (no transcription: questions typed)");
                File.AppendAllText(Path.Combine(Out, "interrogation_play_log.txt"), log.ToString());
                SessionState.SetBool(Key + ".done", true); EditorApplication.ExitPlaymode();
            }
            return;
        }
        // offline English exchange (no API): same bridge entry for the questions, pre-made English clips as the replies
        bool wukongCam = System.Environment.GetEnvironmentVariable("GARDEN_WUKONG") == "1";   // the user's screenshot framing
        bool faceCam = System.Environment.GetEnvironmentVariable("GARDEN_FACE") == "1";        // face close-up
        cam.fieldOfView = faceCam ? 9f : wukongCam ? 26f : 40f;
        Vector3 midO = faceCam ? wukong.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head).position + Vector3.up * 0.06f
                     : wukongCam ? wukong.transform.position + Vector3.up * 0.75f
                                 : (mgr.characters[0].transform.position + mgr.characters[2].transform.position) / 2 + Vector3.up * 0.8f;
        cam.transform.rotation = Quaternion.LookRotation(midO - cam.transform.position);
        for (int i = 0; i < Offline.Length; i++)
        {
            var (at, who, q, clipPath) = Offline[i];
            if (offFired[i] || t < at) continue;
            offFired[i] = true;
            var c = mgr.characters[who];
            if (q != null)
            {
                log.AppendLine($"t={t:F2} Q to {c.characterName}: {q}");
                bridge.OnQuestion(c, q);   // same call InterrogationManager.QuestionPut makes
            }
            else
            {
                var src = c.GetComponent<AudioSource>();
                src.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                src.Play();                // what InterrogationCharacter.Ask does with the TTS reply clip
                log.AppendLine($"AUDIO {t:F3} {Path.GetFileName(clipPath)} {src.clip.length:F2}s ({c.characterName})");
            }
        }
        Save(cam, 1920, 1080, Path.Combine(Frames, $"{frame++:0000}.jpg"));
        if (frame % 15 == 0) LogMouths(mgr, t);
        if (t > 21.5f)
        {
            done = true;
            File.AppendAllText(Path.Combine(Out, "interrogation_play_log.txt"), log.ToString());
            SessionState.SetBool(Key + ".done", true); EditorApplication.ExitPlaymode();
        }
    }

    static void Save(Camera cam, int W, int H, string path)
    {
        var big = RenderTexture.GetTemporary(new RenderTextureDescriptor(W * 2, H * 2, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
        cam.targetTexture = big; cam.Render(); cam.targetTexture = null;
        var rt = RenderTexture.GetTemporary(new RenderTextureDescriptor(W, H, RenderTextureFormat.ARGB32, 0) { sRGB = true });
        big.filterMode = FilterMode.Bilinear; Graphics.Blit(big, rt); RenderTexture.ReleaseTemporary(big);
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        File.WriteAllBytes(path, tex.EncodeToJPG(92)); Object.DestroyImmediate(tex);
    }
}
