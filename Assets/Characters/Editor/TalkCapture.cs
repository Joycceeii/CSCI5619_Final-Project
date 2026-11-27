// Lip-sync capture (batch): Unity -batchmode -projectPath <WuzhuangCase> -executeMethod TalkCapture.BatchRun
// Rebuilds CharacterTest (adds CharacterVoice + T key), plays it at a fixed 30 fps, makes Wukong say the test line
// (same call as pressing T), saves face close-up frames to _models/rigged/talk_frames and a weight log.
// _models/make_talk_video.py then muxes the frames with the wav -> wukong_talk.mp4.
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class TalkCapture
{
    const string Key = "TalkCapture.active";
    static string Models => Path.GetFullPath(Path.Combine(Application.dataPath, "../../_models/rigged"));
    static string Frames => Path.Combine(Models, "talk_frames");
    const int FPS = 30, StartFrame = 10, SayFrame = 25;

    static TalkCapture() { EditorApplication.update += Tick; }

    public static void BatchRun()
    {
        Directory.CreateDirectory(Frames);
        foreach (var f in Directory.GetFiles(Frames, "*.png")) File.Delete(f);
        if (System.Environment.GetEnvironmentVariable("TALK_SKIP_PROFILE") != "1") LipSyncProfileBuilder.Build();
        EditorSceneManager.OpenScene("Assets/Scenes/CharacterTest.unity", OpenSceneMode.Single);
        CharacterTestSetup.Run();
        var go = GameObject.Find("wukong_rigged");
        var smr = go ? go.GetComponentsInChildren<SkinnedMeshRenderer>().First() : null;
        var sb = new StringBuilder();
        sb.AppendLine($"wukong blendshapes: {(smr ? smr.sharedMesh.blendShapeCount : -1)} [{(smr ? string.Join(",", Enumerable.Range(0, smr.sharedMesh.blendShapeCount).Select(i => smr.sharedMesh.GetBlendShapeName(i))) : "")}]");
        var avatar = AssetDatabase.LoadAllAssetsAtPath("Assets/Characters/Wukong/wukong_rigged.fbx").OfType<Avatar>().FirstOrDefault();
        sb.AppendLine($"avatar human={avatar && avatar.isHuman} valid={avatar && avatar.isValid}");
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(CharacterVoiceSetup.TestLine);
        sb.AppendLine($"test line: {(clip ? $"{clip.length:F2}s {clip.frequency}Hz loadType={clip.loadType}" : "MISSING")}");
        sb.AppendLine($"voice components: {(go ? $"CharacterVoice={go.GetComponent<CharacterVoice>() != null} VoiceTestKey={go.GetComponent<VoiceTestKey>() != null} uLipSync={go.GetComponent<uLipSync.uLipSync>() != null} bridge={go.GetComponent<ULipSyncVoice>() != null}" : "no wukong")}");
        File.WriteAllText(Path.Combine(Models, "talk_log.txt"), sb.ToString());
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    static int lastFrame = -1, saved; static bool said, done;
    static readonly StringBuilder wlog = new StringBuilder();

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (!EditorApplication.isPlaying)
        {
            if (SessionState.GetBool(Key + ".done", false)) { SessionState.EraseBool(Key); SessionState.EraseBool(Key + ".done"); EditorApplication.Exit(0); }
            return;
        }
        Time.captureFramerate = FPS;
        int f = Time.frameCount;
        if (f == lastFrame) return;
        lastFrame = f;
        var go = GameObject.Find("wukong_rigged"); if (!go) return;
        var cam = Camera.main;
        var root = go.transform.position;
        cam.transform.position = root + new Vector3(0.10f, 0.67f, 0.62f);
        cam.transform.LookAt(root + new Vector3(0.0f, 0.62f, 0.12f)); cam.fieldOfView = 26;
        var tk = go.GetComponent<VoiceTestKey>();
        if (!said && f >= SayFrame && tk) { tk.Say(); said = true; File.AppendAllText(Path.Combine(Models, "talk_log.txt"), $"Say() at frame {f}, t={Time.time:F3}\n"); }
        if (f < StartFrame) return;
        var voice = go.GetComponent<CharacterVoice>();
        float len = tk && tk.line ? tk.line.length : 5f;
        if (f > SayFrame + (len + 0.8f) * FPS)
        {
            if (done) return; done = true;
            File.AppendAllText(Path.Combine(Models, "talk_log.txt"), $"frames saved {saved} (first frame index {StartFrame}, say at {SayFrame}, {FPS} fps)\n" + wlog);
            SessionState.SetBool(Key + ".done", true); EditorApplication.ExitPlaymode(); return;
        }
        const int W = 1280, H = 720;
        var rt = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 4);
        cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        File.WriteAllBytes(Path.Combine(Frames, $"f_{saved++:0000}.png"), tex.EncodeToPNG()); Object.DestroyImmediate(tex);
        var smr = voice ? voice.face : null;
        if (smr) wlog.AppendLine($"frame {f} t={Time.time:F2} " + string.Join(" ", Enumerable.Range(0, smr.sharedMesh.blendShapeCount).Select(i => $"{smr.sharedMesh.GetBlendShapeName(i)}={smr.GetBlendShapeWeight(i):F0}")));
    }
}
