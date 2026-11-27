// "What does the game look like now" capture (batch, no paid API): Interrogation_Garden in Play mode.
//   wide.jpg (player view, all three), close_wukong/bajie/shaseng.jpg, env_hall/gate/corridor/north.jpg, orbit/NNNN.jpg (18 s look-around)
//   Unity -batchmode -force-d3d12 -projectPath <WuzhuangCase> -executeMethod CurrentStateCapture.BatchRun
// Output: _models/courtyard_atlas/capture/current_state/
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WhoseStory;

[InitializeOnLoad]
public static class CurrentStateCapture
{
    const string Key = "CurrentStateCapture.active";
    static string Out => Path.Combine(GardenSetup.AtlasDir, "capture", "current_state");
    const int OrbitFrames = 540;
    static CurrentStateCapture() { EditorApplication.update += Tick; }

    public static void BatchRun()
    {
        Directory.CreateDirectory(Path.Combine(Out, "orbit"));
        foreach (var f in Directory.GetFiles(Path.Combine(Out, "orbit"), "*.jpg")) File.Delete(f);
        ShaderUtil.allowAsyncCompilation = false;
        EditorSceneManager.OpenScene(InterrogationGardenSetup.ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    static int step, frame, wait; static Vector3 p0; static Quaternion r0; static float fov0; static bool init, done;

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
        var view = Object.FindAnyObjectByType<GardenView>(); var cam = Camera.main; var mgr = Object.FindAnyObjectByType<InterrogationManager>();
        if (!view || !cam || !mgr) return;
        var sp = view.ActiveSplat ? view.ActiveSplat.GetComponent<GaussianSplatting.Runtime.GaussianSplatRenderer>() : null;
        if (sp && (sp.isLoading || sp.splatCount == 0)) return;
        if (!init) { init = true; p0 = cam.transform.position; r0 = cam.transform.rotation; fov0 = cam.fieldOfView; }
        if (wait++ < 30) return;   // idle animation running, everything settled
        var c = mgr.characters;
        Vector3 mid = (c[0].transform.position + c[2].transform.position) / 2;
        void Shot(string name, Vector3 eye, Vector3 look, float fov)
        {
            cam.fieldOfView = fov; cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye));
            Save(cam, Path.Combine(Out, name + ".jpg"));
        }
        switch (step)
        {
            case 0: Shot("wide", p0, mid + Vector3.up * 0.8f, 44f); break;
            case 1: case 2: case 3:
            {
                var ch = c[step - 1].transform; var head = ch.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head).position;
                Vector3 toCam = Vector3.ProjectOnPlane(p0 - ch.position, Vector3.up).normalized;
                Shot("close_" + ch.name.Replace("_rigged", ""), ch.position + toCam * 2.2f + Vector3.up * 1.05f, ch.position + Vector3.up * 0.75f, 30f);
                break;
            }
            case 4: Shot("env_hall", new Vector3(-2.5f, 1.6f, -2.8f), new Vector3(-8.7f, 2.4f, -2.8f), 60f); break;
            case 5: Shot("env_gate", new Vector3(-0.5f, 1.6f, -4.5f), new Vector3(-0.5f, 1.6f, -10.6f), 60f); break;
            case 6: Shot("env_corridor", new Vector3(1.5f, 1.6f, 0.5f), new Vector3(7.25f, 2.0f, 0.5f), 60f); break;
            case 7: Shot("env_north", new Vector3(0.5f, 1.6f, 3.0f), new Vector3(0.5f, 1.6f, 9.0f), 60f); break;
            case 8: Shot("trees", p0, mid + Vector3.up * 2.6f, 64f); break;   // player position, head tilted up: both tree crowns, the hall and the three suspects
            default:
            {
                // look-around from the player position: one slow full turn
                float u = frame / (float)(OrbitFrames - 1); float e = u * u * (3 - 2 * u);
                cam.fieldOfView = 50f;
                cam.transform.SetPositionAndRotation(p0, Quaternion.Euler(4f, r0.eulerAngles.y + 360f * e, 0));
                Save(cam, Path.Combine(Out, "orbit", $"{frame:0000}.jpg"));
                if (++frame >= OrbitFrames)
                {
                    done = true; cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = fov0;
                    SessionState.SetBool(Key + ".done", true); EditorApplication.ExitPlaymode();
                }
                return;
            }
        }
        step++;
    }

    static void Save(Camera cam, string path)
    {
        const int W = 1920, H = 1080;
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
