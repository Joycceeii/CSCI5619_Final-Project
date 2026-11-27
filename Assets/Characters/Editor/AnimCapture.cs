// Walk + take-off capture (batch): Unity -batchmode -projectPath <WuzhuangCase> -executeMethod AnimCapture.BatchRun
// Rebuilds WukongFlight (Build WukongFlight), plays it: walk on the ground, stop, take off -> _models/rigged/anim_frames/{walk,takeoff}_NNN.png
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class AnimCapture
{
    const string Key = "AnimCapture.active";
    static string Models => Path.GetFullPath(Path.Combine(Application.dataPath, "../../_models/rigged"));
    static string Frames => Path.Combine(Models, "anim_frames");
    const float FPS = 30f, WalkA = 1.5f, WalkB = 5.0f, StopWalk = 5.2f, Fly = 6.0f, TakeA = 5.8f, TakeB = 10.0f;

    static AnimCapture() { EditorApplication.update += Tick; }

    public static void BatchRun()
    {
        Directory.CreateDirectory(Frames);
        foreach (var f in Directory.GetFiles(Frames, "*.png")) File.Delete(f);
        EditorSceneManager.OpenScene("Assets/Scenes/CharacterTest.unity", OpenSceneMode.Single);   // a saved scene is active -> Build can add its scene additively
        WukongFlightSetup.Build();
        EditorSceneManager.OpenScene("Assets/Scenes/WukongFlight.unity", OpenSceneMode.Single);
        var fl = Object.FindAnyObjectByType<WukongFlight>();
        fl.flying = false; fl.walking = true;
        EditorUtility.SetDirty(fl); PrefabUtility.RecordPrefabInstancePropertyModifications(fl);
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    static float next = -1; static int frame; static string phase = ""; static bool finished;

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
        var fl = Object.FindAnyObjectByType<WukongFlight>(); if (!fl) return;
        if (System.Environment.GetEnvironmentVariable("TAIL_STYLE") == "minimal")
            foreach (var tc in Object.FindObjectsByType<TailCurve>(FindObjectsSortMode.None)) tc.style = TailCurve.Style.Minimal;
        if (t >= StopWalk) fl.walking = false;
        if (t >= Fly) fl.flying = true;
        var fc = Camera.main ? Camera.main.GetComponent<FlightCamera>() : null;
        if (fc)
        {
            fc.smooth = 2.5f;
            if (t < Fly) { fc.offset = new Vector3(1.55f, 0.25f, 0.25f); fc.lookOffset = new Vector3(0, 0.42f, 0); }
            else { fc.offset = new Vector3(1.9f, 0.1f, 0.6f); fc.lookOffset = new Vector3(0, 0.35f, 0); }
        }
        string ph = t >= WalkA && t < WalkB ? "walk" : t >= TakeA && t < TakeB ? "takeoff" : "";
        if (ph != phase) { phase = ph; frame = 0; next = t; }
        if (phase == "" ) { if (t > TakeB + 0.3f && !finished) { finished = true; SessionState.SetBool(Key + ".done", true); EditorApplication.ExitPlaymode(); } return; }
        if (t < next) return;
        next += 1f / FPS;
        var cam = Camera.main;
        const int W = 1280, H = 720;
        var rt = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 4);
        cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        File.WriteAllBytes(Path.Combine(Frames, $"{phase}_{frame++:000}.png"), tex.EncodeToPNG()); Object.DestroyImmediate(tex);
        var st = fl.animator.GetCurrentAnimatorStateInfo(0);
        string sname = new[] { "Stand", "Walk", "TakeOff", "Ride" }.FirstOrDefault(n => st.IsName(n)) ?? "?";
        File.AppendAllText(Path.Combine(Models, "anim_capture_log.txt"), $"{phase} t={t:F2} state={sname} pos={fl.character.position.ToString("F2")}\n");
    }
}
