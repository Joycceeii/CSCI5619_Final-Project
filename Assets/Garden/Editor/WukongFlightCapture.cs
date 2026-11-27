// Take-off + flight check (batch) for the new holding pose: WukongFlight scene, standing 1.5 s, then take off and fly ~8 s.
// Logs per frame: staff renderers count, staff jump per frame (flicker / double staff check), staff-to-right-hand distance.
//   Unity -batchmode -force-d3d12 -projectPath <WuzhuangCase> -executeMethod WukongFlightCapture.BatchRun
// Output: _models/courtyard_atlas/capture/flight_v21/NNNN.jpg + flight_v21_log.txt
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class WukongFlightCapture
{
    const string Key = "WukongFlightCapture.active";
    static string Out => Path.Combine(GardenSetup.AtlasDir, "capture");
    static string Frames => Path.Combine(Out, System.Environment.GetEnvironmentVariable("FLIGHT_TAG") ?? "flight_v21");
    static WukongFlightCapture() { EditorApplication.update += Tick; }

    public static void BatchRun()
    {
        Directory.CreateDirectory(Frames);
        foreach (var f in Directory.GetFiles(Frames, "*.jpg")) File.Delete(f);
        ShaderUtil.allowAsyncCompilation = false;
        AssetDatabase.Refresh();
        if (System.Environment.GetEnvironmentVariable("FLIGHT_REBUILD") == "1") WukongFlightSetup.Build();   // re-import clips + rebuild the flight scene
        EditorSceneManager.OpenScene("Assets/Scenes/WukongFlight.unity", OpenSceneMode.Single);
        var fl = Object.FindAnyObjectByType<WukongFlight>(); fl.flying = false; fl.walking = false;   // start standing (not saved)
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    static int frame; static float t0 = -1; static bool launched, done; static Vector3 lastStaff; static bool haveLast;
    static float maxJump; static int maxStaffRenderers;
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
        var fl = Object.FindAnyObjectByType<WukongFlight>(); var cam = Camera.main;
        if (!fl || !cam) return;
        if (t0 < 0) { t0 = Time.time; fl.flying = false; }
        float t = Time.time - t0;
        if (!launched && t >= 1.5f) { launched = true; fl.flying = true; log.AppendLine($"t={t:F2} take off"); }
        var staffs = fl.GetComponentsInChildren<Renderer>(true).Where(r => r.name.ToLower().Contains("staff") && r.enabled && r.gameObject.activeInHierarchy).ToArray();
        maxStaffRenderers = Mathf.Max(maxStaffRenderers, staffs.Length);
        var st = fl.character.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == fl.staffName);
        var hand = fl.animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (st)
        {
            Vector3 p = st.position;
            if (haveLast) { float j = (p - lastStaff).magnitude; maxJump = Mathf.Max(maxJump, j); if (j > 0.25f) log.AppendLine($"t={t:F2} STAFF JUMP {j:F3} m"); }
            lastStaff = p; haveLast = true;
        }
        if (frame % 15 == 0 && st && hand)
            log.AppendLine($"t={t:F2} hand-head {(hand.position - fl.animator.GetBoneTransform(HumanBodyBones.Head).position).magnitude:F3} m; staff renderers {staffs.Length} ({string.Join(",", staffs.Select(r => r.name))}) parent {st.parent?.name} staff-hand {(st.position - hand.position).magnitude:F3} m");
        if (System.Environment.GetEnvironmentVariable("FLIGHT_CLOSE") == "1")
        {
            // close-up on the left hand (it holds the staff in flight)
            var lh = fl.animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
            cam.fieldOfView = 14f; cam.transform.rotation = Quaternion.LookRotation(lh.position - cam.transform.position);
            Save(cam, Path.Combine(Frames, $"{frame++:0000}.jpg"));
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0;
        }
        else Save(cam, Path.Combine(Frames, $"{frame++:0000}.jpg"));
        if (t > 10f)
        {
            done = true;
            log.AppendLine($"max staff renderers at once: {maxStaffRenderers}; max staff move per frame: {maxJump:F3} m");
            File.WriteAllText(Path.Combine(Out, (System.Environment.GetEnvironmentVariable("FLIGHT_TAG") ?? "flight_v21") + "_log.txt"), log.ToString());
            SessionState.SetBool(Key + ".done", true); EditorApplication.ExitPlaymode();
        }
    }

    static void Save(Camera cam, string path)
    {
        const int W = 1920, H = 1080;
        var rt = RenderTexture.GetTemporary(new RenderTextureDescriptor(W, H, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 4 });
        var prevT = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prevT;
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        File.WriteAllBytes(path, tex.EncodeToJPG(90)); Object.DestroyImmediate(tex);
    }
}
