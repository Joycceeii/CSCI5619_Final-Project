// WuzhuangGarden capture (batch): Unity -batchmode -force-d3d12 -projectPath <WuzhuangCase> -executeMethod GardenCapture.BatchRun
// Builds the scene (GardenSetup), enters Play mode and renders with the player camera at 30 fps:
//   align_<mode>.jpg : exact f00 camera, no characters (compare with the original image)
//   shot_<mode>.jpg  : player camera, 1920x1080
//   push_<mode>/NNNN.jpg : 10 s slow push-in toward the line-up
// mode = pure (splat only) / hybrid (splat + 2.5D photo layer + paving). Output: _models/courtyard_atlas/capture/
using System.IO;
using System.Linq;
using System.Text;
using GaussianSplatting.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class GardenCapture
{
    const string Key = "GardenCapture.active";
    static string Out => Path.Combine(GardenSetup.AtlasDir, "capture");
    static readonly int PushFrames = System.Environment.GetEnvironmentVariable("GARDEN_SHOTS_ONLY") == "1" ? 2 : 300; const float PushMetres = 1.4f;
    static GardenCapture() { EditorApplication.update += Tick; }

    public static void BatchRun()
    {
        Directory.CreateDirectory(Out);
        // only clear what this run rewrites (keeps finished mp4 / comparison images)
        foreach (var d in Directory.GetDirectories(Out, "push_*").Concat(Directory.GetDirectories(Out, "orbit_*"))) Directory.Delete(d, true);
        foreach (var f in Directory.GetFiles(Out, "*.jpg").Concat(Directory.GetFiles(Out, "capture_log.txt"))) File.Delete(f);
        ShaderUtil.allowAsyncCompilation = false;   // no placeholder shaders in the first captured frames
        var blog = GardenSetup.Build();
        File.WriteAllText(Path.Combine(Out, "capture_log.txt"), blog);
        if (blog.Contains("ERROR")) { EditorApplication.Exit(1); return; }
        EditorSceneManager.OpenScene(GardenSetup.ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    // shot list: (kind, mode)
    static readonly (string kind, GardenView.Mode mode)[] Shots =
    {
        ("close_plaque", GardenView.Mode.Rebuilt), ("close_gate", GardenView.Mode.Rebuilt), ("orbit", GardenView.Mode.Rebuilt),
    };
    static int shot = 0, wait = 0, frame = 0; static bool done; static float loadStart = -1;
    static Vector3 startPos; static Quaternion startRot; static float startFov; static bool haveStart;
    static readonly StringBuilder log = new StringBuilder();

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (!EditorApplication.isPlaying)
        {
            if (SessionState.GetBool(Key + ".done", false)) { SessionState.EraseBool(Key); SessionState.EraseBool(Key + ".done"); EditorApplication.Exit(0); }
            return;
        }
        Time.captureFramerate = 30;
        if (done) return;
        var view = Object.FindAnyObjectByType<GardenView>();
        var cam = Camera.main;
        if (!view || !cam) return;
        if (!haveStart) { startPos = cam.transform.position; startRot = cam.transform.rotation; startFov = cam.fieldOfView; haveStart = true; }
        if (shot >= Shots.Length) { Finish(); return; }
        var (kind, mode) = Shots[shot];
        if (view.mode != mode) { view.mode = mode; view.Apply(); wait = 0; frame = 0; loadStart = Time.realtimeSinceStartup; return; }
        var sp = view.ActiveSplat ? view.ActiveSplat.GetComponent<GaussianSplatRenderer>() : null;
        if (sp && (sp.isLoading || sp.splatCount == 0))
        {
            if (!string.IsNullOrEmpty(sp.loadError)) { log.AppendLine($"LOAD ERROR {sp.name}: {sp.loadError}"); Finish(); }
            else if (Time.realtimeSinceStartup - loadStart > 240) { log.AppendLine($"LOAD TIMEOUT {sp.name}"); Finish(); }
            return;
        }
        if (wait == 0 && sp) log.AppendLine($"{sp.name}: {sp.splatCount:N0} splats loaded in {Time.realtimeSinceStartup - loadStart:F1}s, bounds {sp.localBounds.size:F1}");
        if (wait++ < 20) return;   // let sorting / animation settle

        var meta = GardenSetup.LoadMeta(); var f00 = GardenSetup.F00(meta);
        var suspects = GameObject.Find("Suspects (line-up)");
        string tag = mode == GardenView.Mode.Hybrid ? "hybrid" : mode == GardenView.Mode.Rebuilt ? "rebuilt" : "pure";
        if (kind == "align")
        {
            if (suspects) suspects.SetActive(false);
            var vol = Object.FindAnyObjectByType<UnityEngine.Rendering.Volume>(); if (vol) vol.enabled = false;   // alignment check: no depth of field
            cam.transform.position = GardenSetup.V(f00.pos) * meta.scale;
            cam.transform.rotation = Quaternion.LookRotation(GardenSetup.V(f00.fwd), GardenSetup.V(f00.up));
            cam.fieldOfView = f00.vfov;
            Save(cam, 1280, 720, Path.Combine(Out, $"align_{tag}.jpg"));
            if (suspects) suspects.SetActive(true);
            if (vol) vol.enabled = true;
            cam.transform.SetPositionAndRotation(startPos, startRot); cam.fieldOfView = startFov;
            log.AppendLine($"align_{tag}.jpg");
            shot++; wait = 0; return;
        }
        if (kind.StartsWith("close_"))
        {
            // close-ups of the seal-script plaque (hall, west) and of the gate couplets (south wall)
            bool plaque = kind == "close_plaque";
            Vector3 eye = plaque ? new Vector3(-4.6f, 2.3f, -2.8f) : new Vector3(-0.5f, 1.65f, -5.4f);
            Vector3 look = plaque ? new Vector3(-8.64f, 2.75f, -2.8f) : new Vector3(-0.5f, 1.55f, -10.6f);
            float fov0 = cam.fieldOfView; cam.fieldOfView = plaque ? 32f : 52f;
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye));
            Save(cam, 1920, 1080, Path.Combine(Out, $"{kind}.jpg"));
            cam.fieldOfView = fov0; cam.transform.SetPositionAndRotation(startPos, startRot);
            log.AppendLine($"{kind}.jpg"); shot++; wait = 0; return;
        }
        if (kind == "orbit")
        {
            // look-around from the player position: one slow full turn, level horizon
            float uo = frame / (float)(PushFrames - 1);
            float yaw0 = startRot.eulerAngles.y;
            float yaw = yaw0 + 360f * (uo * uo * (3 - 2 * uo));
            cam.transform.SetPositionAndRotation(startPos, Quaternion.Euler(4f, yaw, 0));
            string od = Path.Combine(Out, $"orbit_{tag}"); Directory.CreateDirectory(od);
            Save(cam, 1920, 1080, Path.Combine(od, $"{frame:0000}.jpg"));
            if (frame == PushFrames - 1) { log.AppendLine($"orbit_{tag}: {PushFrames} frames"); shot++; wait = 0; frame = 0; cam.transform.SetPositionAndRotation(startPos, startRot); return; }
            frame++; return;
        }
        // push-in: ease in/out, 1.4 m forward, keep the look-at target
        float u = Mathf.Clamp01(frame / (float)(PushFrames - 1)); float e = u * u * (3 - 2 * u);
        Vector3 fwdH = Vector3.ProjectOnPlane(startRot * Vector3.forward, Vector3.up).normalized;
        cam.transform.position = startPos + fwdH * (PushMetres * e) + Vector3.down * (0.08f * e);
        cam.transform.rotation = startRot;
        string dir = Path.Combine(Out, $"push_{tag}"); Directory.CreateDirectory(dir);
        Save(cam, 1920, 1080, Path.Combine(dir, $"{frame:0000}.jpg"));
        if (frame == 0) File.Copy(Path.Combine(dir, "0000.jpg"), Path.Combine(Out, $"shot_{tag}.jpg"), true);
        if (frame == PushFrames - 1) { File.Copy(Path.Combine(dir, $"{frame:0000}.jpg"), Path.Combine(Out, $"shot_{tag}_end.jpg"), true); log.AppendLine($"push_{tag}: {PushFrames} frames"); shot++; wait = 0; frame = 0; cam.transform.SetPositionAndRotation(startPos, startRot); return; }
        frame++;
    }

    static void Finish()
    {
        if (done) return; done = true;
        File.AppendAllText(Path.Combine(Out, "capture_log.txt"), log.ToString());
        SessionState.SetBool(Key + ".done", true); EditorApplication.ExitPlaymode();
    }

    static void Save(Camera cam, int W, int H, string path)
    {
        // 2x supersampling: render at double size, box-filter down
        var big = RenderTexture.GetTemporary(new RenderTextureDescriptor(W * 2, H * 2, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
        cam.targetTexture = big; cam.Render(); cam.targetTexture = null;
        var rt = RenderTexture.GetTemporary(new RenderTextureDescriptor(W, H, RenderTextureFormat.ARGB32, 0) { sRGB = true });
        big.filterMode = FilterMode.Bilinear; Graphics.Blit(big, rt); RenderTexture.ReleaseTemporary(big);
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        File.WriteAllBytes(path, tex.EncodeToJPG(94)); Object.DestroyImmediate(tex);
    }
}







