// After the staff re-seat (grip_hold.py: elbow bent, staff through the fist): re-import Wukong and rebuild every scene that uses him,
// then record idle + talk + walk in Interrogation_Garden from the player's eye.
//   Unity -batchmode -force-d3d12 -projectPath <WuzhuangCase> -executeMethod WukongGripUpdate.BatchRun
// Output: _models/courtyard_atlas/capture/grip_v21/NNNN.jpg + grip_v21_log.txt, grip_v21_front.jpg / grip_v21_side.jpg
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using WhoseStory;

[InitializeOnLoad]
public static class WukongGripUpdate
{
    const string Key = "WukongGripUpdate.active";
    static string Out => Path.Combine(GardenSetup.AtlasDir, "capture");
    static string Frames => Path.Combine(Out, "grip_v21");
    static WukongGripUpdate() { EditorApplication.update += Tick; }

    public static void BatchRun()
    {
        var log = new StringBuilder();
        ShaderUtil.allowAsyncCompilation = false;
        AssetDatabase.Refresh();
        CharacterTestSetup.Run();                       // re-import wukong_rigged.fbx (+ clips) and rebuild CharacterTest
        log.AppendLine("CharacterTest rebuilt");
        WukongFlightSetup.Build();                      // flight clips/controller/prefab + WukongFlight scene
        log.AppendLine("WukongFlight rebuilt");
        log.Append(InterrogationGardenSetup.Build(true)); // WuzhuangGarden + Interrogation_Garden
        Directory.CreateDirectory(Frames);
        foreach (var f in Directory.GetFiles(Frames, "*.jpg")) File.Delete(f);
        File.WriteAllText(Path.Combine(Out, "grip_v21_log.txt"), log.ToString());
        if (log.ToString().Contains("ERROR")) { EditorApplication.Exit(1); return; }
        if (System.Environment.GetEnvironmentVariable("REBUILD_ONLY") == "1") { EditorApplication.Exit(0); return; }
        EditorSceneManager.OpenScene(InterrogationGardenSetup.ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    static int frame; static float t0 = -1; static bool talked, walking, stills, done; static PlayableGraph graph;
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
        var view = Object.FindAnyObjectByType<GardenView>(); var cam = Camera.main; var mgr = Object.FindAnyObjectByType<InterrogationManager>();
        if (!view || !cam || !mgr) return;
        var sp = view.ActiveSplat ? view.ActiveSplat.GetComponent<GaussianSplatting.Runtime.GaussianSplatRenderer>() : null;
        if (sp && (sp.isLoading || sp.splatCount == 0)) return;
        if (t0 < 0) t0 = Time.time;
        float t = Time.time - t0;
        var wk = mgr.characters[0];
        if (!stills && t > 1.5f)
        {
            stills = true;
            var p = cam.transform.position; var r = cam.transform.rotation; float fov = cam.fieldOfView;
            Vector3 c = wk.transform.position + Vector3.up * 0.75f;
            cam.fieldOfView = 30f;
            cam.transform.SetPositionAndRotation(c + wk.transform.forward * 2.6f + Vector3.up * 0.1f, Quaternion.LookRotation(-wk.transform.forward)); Save(cam, Path.Combine(Out, "grip_v21_front.jpg"));
            cam.transform.SetPositionAndRotation(c - wk.transform.right * 2.6f + Vector3.up * 0.1f, Quaternion.LookRotation(wk.transform.right)); Save(cam, Path.Combine(Out, "grip_v21_side.jpg"));
            cam.transform.SetPositionAndRotation(p, r); cam.fieldOfView = fov;
        }
        // player's eye, Wukong framed like the user's screenshot
        cam.fieldOfView = 26f;
        cam.transform.rotation = Quaternion.LookRotation(wk.transform.position + Vector3.up * 0.75f - cam.transform.position);
        if (!talked && t >= 3.0f)
        {
            talked = true;
            wk.GetComponent<SuspectTells>()?.OnPlayerQuestion("Did you steal the ginseng fruit last night?");
            var src = wk.GetComponent<AudioSource>(); src.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Test/en/wukong_line_en.wav"); src.Play();
            log.AppendLine($"t={t:F2} question + reply clip");
        }
        if (!walking && t >= 10.0f)
        {
            walking = true;
            var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/Characters/Wukong/Animations/wukong_walk.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            AnimationPlayableUtilities.PlayClip(wk.GetComponent<Animator>(), clip, out graph);
            log.AppendLine($"t={t:F2} walk clip {clip.name} (in place)");
        }
        Save(cam, Path.Combine(Frames, $"{frame++:0000}.jpg"));
        if (t > 14.5f)
        {
            done = true; if (graph.IsValid()) graph.Destroy();
            File.AppendAllText(Path.Combine(Out, "grip_v21_log.txt"), log.ToString());
            SessionState.SetBool(Key + ".done", true); EditorApplication.ExitPlaymode();
        }
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
