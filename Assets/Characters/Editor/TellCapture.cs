// Tell capture (batch): Unity -batchmode -projectPath <WuzhuangCase> -executeMethod TellCapture.BatchRun
// CharacterTest, 30 fps: an unrelated question (tail keeps its idle sway), then "你昨晚是不是去了后园？" (tail shoots up, holds,
// settles). Frames -> _models/rigged/tell_frames, timeline + per-frame tail data -> tell_log.txt
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class TellCapture
{
    const string Key = "TellCapture.active";
    static string Models => Path.GetFullPath(Path.Combine(Application.dataPath, "../../_models/rigged"));
    static string Frames => Path.Combine(Models, "tell_frames");
    public const string Q1 = "师父最近身体还好吗？", Q2 = "你昨晚是不是去了后园？";
    const float T1 = 1.5f, T2 = 5.0f, End = 10.5f;
    static TellCapture() { EditorApplication.update += Tick; }

    public static void BatchRun()
    {
        Directory.CreateDirectory(Frames);
        foreach (var f in Directory.GetFiles(Frames, "*.png")) File.Delete(f);
        EditorSceneManager.OpenScene("Assets/Scenes/CharacterTest.unity", OpenSceneMode.Single);
        CharacterTestSetup.Run();
        File.WriteAllText(Path.Combine(Models, "tell_log.txt"), "");
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    static int saved; static bool a1, a2, done; static readonly StringBuilder log = new StringBuilder();
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (!EditorApplication.isPlaying)
        {
            if (SessionState.GetBool(Key + ".done", false)) { SessionState.EraseBool(Key); SessionState.EraseBool(Key + ".done"); EditorApplication.Exit(0); }
            return;
        }
        Time.captureFramerate = 30;
        float t = Time.time;
        var go = GameObject.Find("wukong_rigged"); if (!go) return;
        var st = go.GetComponent<SuspectTells>(); var tc = go.GetComponent<TailCurve>();
        var cam = Camera.main; var root = go.transform.position;
        cam.transform.position = root + new Vector3(1.4f, 0.78f, 0.8f);
        cam.transform.LookAt(root + new Vector3(0.08f, 0.5f, -0.1f)); cam.fieldOfView = 34;
        if (!a1 && t >= T1) { a1 = true; log.AppendLine($"Q1 t={t:F2} \"{Q1}\" -> tell={st.OnPlayerQuestion(Q1)}"); }
        if (!a2 && t >= T2) { a2 = true; log.AppendLine($"Q2 t={t:F2} \"{Q2}\" -> tell={st.OnPlayerQuestion(Q2)} match={st.LastMatch}"); }
        if (t > End)
        {
            if (done) return; done = true;
            File.AppendAllText(Path.Combine(Models, "tell_log.txt"), log.ToString());
            SessionState.SetBool(Key + ".done", true); EditorApplication.ExitPlaymode(); return;
        }
        if (t < 0.5f) return;
        const int W = 1280, H = 720;
        var rt = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 4);
        cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        File.WriteAllBytes(Path.Combine(Frames, $"f_{saved++:0000}.png"), tex.EncodeToPNG()); Object.DestroyImmediate(tex);
        var tip = go.GetComponentsInChildren<Transform>().Where(x => x.name.StartsWith("Tail_")).OrderBy(x => x.name).Last();
        log.AppendLine($"frame t={t:F3} alert={tc.AlertWeight:F3} tip={go.transform.InverseTransformPoint(tip.position).ToString("F4")}");
    }
}
