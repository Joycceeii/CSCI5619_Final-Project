// Bajie / Shaseng capture (batch): Unity -batchmode -projectPath <WuzhuangCase> -executeMethod BSCapture.BatchRun
// Builds the lip-sync profiles, rebuilds CharacterTest, plays at 30 fps:
//   bajie_talk, shaseng_talk : face close-ups while each says his test line (same call as G / H)
//   bajie_tell, shaseng_tell : an unrelated question, then a sensitive one (same path as the question box)
// Frames -> _models/rigged/bs_frames/<phase>_NNNN.png, timeline + ear/weight data -> bs_log.txt
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BSCapture
{
    const string Key = "BSCapture.active";
    static string Models => Path.GetFullPath(Path.Combine(Application.dataPath, "../../_models/rigged"));
    static string Frames => Path.Combine(Models, "bs_frames");
    // phases: (name, start, end) in play seconds
    static readonly (string n, float a, float b)[] Phases = { ("bajie_talk", 0.5f, 7.5f), ("shaseng_talk", 8.0f, 15.0f), ("bajie_tell", 15.5f, 24.5f), ("shaseng_tell", 25.0f, 34.0f) };
    public const string BQ1 = "你平时最喜欢做什么？", BQ2 = "你昨晚是不是偷吃了人参果？";
    public const string SQ1 = "你们取经走了多久了？", SQ2 = "那天晚上你守着行李，有没有离开过？";
    static BSCapture() { EditorApplication.update += Tick; }

    public static void BatchRun()
    {
        Directory.CreateDirectory(Frames);
        foreach (var f in Directory.GetFiles(Frames, "*.png")) File.Delete(f);
        LipSyncProfileBuilder.Build();
        EditorSceneManager.OpenScene("Assets/Scenes/CharacterTest.unity", OpenSceneMode.Single);
        CharacterTestSetup.Run();
        var sb = new StringBuilder();
        foreach (var n in new[] { "bajie_rigged", "shaseng_rigged" })
        {
            var go = GameObject.Find(n);
            var smr = go.GetComponentsInChildren<SkinnedMeshRenderer>().First();
            var anim = go.GetComponent<Animator>();
            sb.AppendLine($"{n}: blendshapes {smr.sharedMesh.blendShapeCount}; controller {(anim && anim.runtimeAnimatorController ? anim.runtimeAnimatorController.name : "none")}; " +
                          $"voice {go.GetComponent<CharacterVoice>() != null} uLipSync {go.GetComponent<uLipSync.uLipSync>() != null} tells {go.GetComponent<SuspectTells>()?.defaultTell} words {string.Join("/", go.GetComponent<SuspectTells>()?.sensitiveWords ?? new System.Collections.Generic.List<string>())}");
        }
        File.WriteAllText(Path.Combine(Models, "bs_log.txt"), sb.ToString());
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    static int lastPhase = -1, frame; static bool done; static readonly bool[] fired = new bool[8];
    static readonly StringBuilder log = new StringBuilder();
    static void Fire(int i, System.Func<string> f) { if (!fired[i]) { fired[i] = true; log.AppendLine(f()); } }

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
        var bajie = GameObject.Find("bajie_rigged"); var sha = GameObject.Find("shaseng_rigged");
        if (!bajie || !sha) return;
        var cam = Camera.main; cam.fieldOfView = 30;
        int ph = System.Array.FindIndex(Phases, p => t >= p.a && t < p.b);
        // events
        if (t >= 1.0f) Fire(0, () => { bajie.GetComponent<VoiceTestKey>().Say(); return $"t={t:F2} bajie says line"; });
        if (t >= 8.5f) Fire(1, () => { sha.GetComponent<VoiceTestKey>().Say(); return $"t={t:F2} shaseng says line"; });
        if (t >= 16.5f) Fire(2, () => $"t={t:F2} BQ1 \"{BQ1}\" -> {bajie.GetComponent<SuspectTells>().OnPlayerQuestion(BQ1)}");
        if (t >= 19.5f) Fire(3, () => $"t={t:F2} BQ2 \"{BQ2}\" -> {bajie.GetComponent<SuspectTells>().OnPlayerQuestion(BQ2)} match {bajie.GetComponent<SuspectTells>().LastMatch}");
        if (t >= 26.0f) Fire(4, () => $"t={t:F2} SQ1 \"{SQ1}\" -> {sha.GetComponent<SuspectTells>().OnPlayerQuestion(SQ1)}");
        if (t >= 29.0f) Fire(5, () => $"t={t:F2} SQ2 \"{SQ2}\" -> {sha.GetComponent<SuspectTells>().OnPlayerQuestion(SQ2)} match {sha.GetComponent<SuspectTells>().LastMatch}");
        if (t > Phases[Phases.Length - 1].b)
        {
            if (done) return; done = true;
            File.AppendAllText(Path.Combine(Models, "bs_log.txt"), log.ToString());
            SessionState.SetBool(Key + ".done", true); EditorApplication.ExitPlaymode(); return;
        }
        if (ph < 0) return;
        if (ph != lastPhase) { lastPhase = ph; frame = 0; }
        string name = Phases[ph].n;
        Vector3 rb = bajie.transform.position, rs = sha.transform.position;
        switch (name)
        {
            case "bajie_talk": cam.transform.position = rb + new Vector3(0.22f, 0.86f, 0.95f); cam.transform.LookAt(rb + new Vector3(0, 0.77f, 0.1f)); break;
            case "shaseng_talk": cam.transform.position = rs + new Vector3(-0.2f, 0.78f, 0.95f); cam.transform.LookAt(rs + new Vector3(0.03f, 0.69f, 0.1f)); break;
            case "bajie_tell": cam.transform.position = rb + new Vector3(0.3f, 0.95f, 1.45f); cam.transform.LookAt(rb + new Vector3(0, 0.76f, 0)); break;
            case "shaseng_tell": cam.transform.position = rs + new Vector3(-0.55f, 0.75f, 1.9f); cam.transform.LookAt(rs + new Vector3(-0.05f, 0.52f, 0)); cam.fieldOfView = 34; break;
        }
        const int W = 1280, H = 720;
        var rt = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 4);
        cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        File.WriteAllBytes(Path.Combine(Frames, $"{name}_{frame++:0000}.png"), tex.EncodeToPNG()); Object.DestroyImmediate(tex);
        // data: Bajie's ear tips (jitter check) + mouth weights
        string Tip(GameObject g, string chain)
        {
            var b = g.GetComponentsInChildren<Transform>().Where(x => x.name.StartsWith(chain)).OrderBy(x => x.name).LastOrDefault();
            return b ? g.transform.InverseTransformPoint(b.position).ToString("F4") : "-";
        }
        var smr = (name.StartsWith("bajie") ? bajie : sha).GetComponentsInChildren<SkinnedMeshRenderer>().First();
        string w = string.Join(" ", Enumerable.Range(0, smr.sharedMesh.blendShapeCount).Select(i => $"{smr.sharedMesh.GetBlendShapeName(i)}={smr.GetBlendShapeWeight(i):F0}"));
        log.AppendLine($"frame {name} t={t:F3} earL={Tip(bajie, "Ear_L_")} earR={Tip(bajie, "Ear_R_")} head={sha.transform.InverseTransformPoint(sha.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head).position).ToString("F4")} {w}");
    }
}
