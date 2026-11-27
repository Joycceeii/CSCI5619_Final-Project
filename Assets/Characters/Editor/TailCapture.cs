// Tail / ear dynamics check + capture (batch):
//   Unity -batchmode -projectPath <WuzhuangCase> -executeMethod TailCapture.BatchRun
// 1) rebuilds CharacterTest (Tools > Characters > Rebuild CharacterTest) and the WukongFlight prefab/scene (Build WukongFlight)
// 2) plays WukongFlight: 5 s standing, take-off, 5 s flying; then CharacterTest with Bajie's head turning, 3 s
//    -> _models/rigged/tail_frames/{stand,fly,ears}_NNN.png
// 3) every captured Wukong frame: tail-vs-body (legs/cape/back) and tail-vs-staff clearance -> _models/rigged/tail_clip_report.txt
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class TailCapture
{
    const string Key = "TailCapture.active";
    static string Models => Path.GetFullPath(Path.Combine(Application.dataPath, "../../_models/rigged"));
    static string FramesDir => Path.Combine(Models, "tail_frames");
    static string MaskPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/wukong_tailmask.bin"));
    static string Report => Path.Combine(Models, "tail_clip_report.txt");
    const float FPS = 30f;
    // schedule (play time, seconds)
    const float StandA = 2f, StandB = 7f, TakeOff = 7.2f, FlyA = 11f, FlyB = 16f, EarsLoad = 16.5f, EarsA = 18f, EarsB = 21.5f;

    static TailCapture() { EditorApplication.update += Tick; }

    public static void BatchRun()
    {
        Directory.CreateDirectory(FramesDir);
        foreach (var f in Directory.GetFiles(FramesDir, "*.png")) File.Delete(f);
        File.WriteAllText(Report, "tail clipping report (TailWag on)\n");
        EditorSceneManager.OpenScene("Assets/Scenes/CharacterTest.unity", OpenSceneMode.Single);
        CharacterTestSetup.Run();           // first: refreshes wukong_rigged's avatar (skeleton)
        WukongFlightSetup.Build();          // then: re-imports the clips with that avatar description + tail mask
        CharacterTestSetup.Run();           // and the CharacterTest Wukong picks up the fresh stand clip
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/WukongFlight.unity", OpenSceneMode.Single);
        var fl = Object.FindAnyObjectByType<WukongFlight>();
        fl.flying = false;                       // start standing (scene is not saved)
        EditorUtility.SetDirty(fl); PrefabUtility.RecordPrefabInstancePropertyModifications(fl);
        // tail-vertex mask from the bind pose: verts within 3.4 cm of the tail chain
        var smr = fl.character.GetComponentsInChildren<SkinnedMeshRenderer>().First();
        var baked = new Mesh(); smr.BakeMesh(baked, true);
        var v = baked.vertices; var M = smr.transform.localToWorldMatrix;
        var tail = TailPoints(fl.character);
        var mask = new byte[v.Length]; int nt = 0;
        for (int i = 0; i < v.Length; i++)
        {
            var p = M.MultiplyPoint3x4(v[i]);
            if (DistToChain(p, tail, 0) < 0.034f) { mask[i] = 1; nt++; }
        }
        File.WriteAllBytes(MaskPath, mask);
        File.AppendAllText(Report, $"mesh verts {v.Length}, tail verts {nt}\n");
        // baseline: same metric in the bind pose (no TailWag): contacts the mesh already has where the tail meets cape/pants
        int bi = 0; float bd = 0; var tp = TailPoints(fl.character);
        for (int i = 0; i < v.Length; i++) { if (mask[i] == 1) continue; float d = DistToChain(M.MultiplyPoint3x4(v[i]), tp, 1); if (d < 0.02f) { bi++; bd = Mathf.Max(bd, 0.02f - d); } }
        File.AppendAllText(Report, $"bind pose baseline (no TailWag): body verts inside tail {bi} (deepest {bd * 1000:F1} mm)\n");
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    static List<Vector3> TailPoints(Transform root)
    {
        var bones = Enumerable.Range(1, 20).Select(i => root.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == $"Tail_{i:00}")).Where(t => t).ToList();
        var pts = bones.Select(b => b.position).ToList();
        if (bones.Count > 1) pts.Add(pts[^1] + (pts[^1] - pts[^2]));
        return pts;
    }
    static float DistToChain(Vector3 p, List<Vector3> pts, int fromSeg)
    {
        float best = float.MaxValue;
        for (int s = fromSeg; s < pts.Count - 1; s++)
        {
            Vector3 a = pts[s], ab = pts[s + 1] - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-8f));
            best = Mathf.Min(best, Vector3.Distance(p, a + ab * t));
        }
        return best;
    }
    static float SegSeg(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
    {
        float best = float.MaxValue;
        for (int i = 0; i <= 20; i++)
        {
            var p = Vector3.Lerp(p1, q1, i / 20f); var ab = q2 - p2;
            float t = Mathf.Clamp01(Vector3.Dot(p - p2, ab) / Mathf.Max(ab.sqrMagnitude, 1e-8f));
            best = Mathf.Min(best, Vector3.Distance(p, p2 + ab * t));
        }
        return best;
    }

    static float next = -1; static int frame; static string phase = ""; static byte[] maskCache; static bool loading;
    static readonly List<string> stats = new List<string>();

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (!EditorApplication.isPlaying)
        {
            if (SessionState.GetBool(Key + ".done", false)) { SessionState.EraseBool(Key); SessionState.EraseBool(Key + ".done"); EditorApplication.Exit(0); }
            return;
        }
        Time.captureFramerate = 30;
        string diag = System.Environment.GetEnvironmentVariable("TAIL_DIAG") ?? "";
        if (System.Environment.GetEnvironmentVariable("TAIL_NO_WAG") == "1" || diag.Contains("nowag"))
            foreach (var tw in Object.FindObjectsByType<TailWag>(FindObjectsSortMode.None)) if (tw.enabled) tw.enabled = false;
        if (System.Environment.GetEnvironmentVariable("TAIL_STYLE") == "minimal")
            foreach (var tc in Object.FindObjectsByType<TailCurve>(FindObjectsSortMode.None)) tc.style = TailCurve.Style.Minimal;
        if (diag.Contains("nocurve"))
            foreach (var tc in Object.FindObjectsByType<TailCurve>(FindObjectsSortMode.None)) if (tc.enabled) tc.enabled = false;
        if (diag.Contains("standonly") && Time.time > StandB + 0.2f) { Finish(); return; }
        float t = Time.time;
        var fl = Object.FindAnyObjectByType<WukongFlight>();
        if (fl && t >= TakeOff && !fl.flying) fl.flying = true;
        var twv = fl ? fl.character.GetComponent<TailWag>() : null;
        string[] VARS = (System.Environment.GetEnvironmentVariable("TAIL_FLY_VARIANTS") ?? "").Split(new[] { ";" }, System.StringSplitOptions.RemoveEmptyEntries);
        if (twv && VARS.Length > 0 && t >= FlyA - 1.5f && t < FlyB)
        {
            int k = Mathf.Min(VARS.Length - 1, (int)((t - (FlyA - 1.5f)) / ((FlyB - FlyA + 1.5f) / VARS.Length)));
            var c = VARS[k].Split(',').Select(float.Parse).ToArray(); twv.flyDirection = new Vector3(c[0], c[1], c[2]);
        }
        string ph = t >= StandA && t < StandB ? "stand" : t >= FlyA && t < FlyB ? "fly" : t >= EarsA && t < EarsB ? "ears" : "";
        if (ph != phase) { phase = ph; frame = 0; next = t; }
        // camera framing per phase
        var cam = Camera.main;
        var fc = cam ? cam.GetComponent<FlightCamera>() : null;
        if (fc)
        {
            // side view from his right, framed on body + tail (the tail sits ~40 cm behind him)
            if (t < TakeOff + 1.5f) { fc.offset = new Vector3(1.55f, 0.28f, -0.25f); fc.lookOffset = new Vector3(0.12f, 0.32f, -0.2f); }
            else { fc.offset = new Vector3(1.9f, 0.3f, -0.35f); fc.lookOffset = new Vector3(0.1f, 0.25f, -0.25f); }
        }
        if (t >= EarsLoad && !loading && SceneManager.GetActiveScene().name != "CharacterTest")
        {
            loading = true;
            EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/CharacterTest.unity", new LoadSceneParameters(LoadSceneMode.Single));
            return;
        }
        if (SceneManager.GetActiveScene().name == "CharacterTest" && !Object.FindAnyObjectByType<HeadTurnDemo>())
        {
            var bajie = GameObject.Find("bajie_rigged");
            if (bajie)
            {
                var d = bajie.AddComponent<HeadTurnDemo>();
                d.bone = bajie.GetComponentsInChildren<Transform>().First(x => x.name == "Head");
                var c = Camera.main; c.transform.position = bajie.transform.position + new Vector3(0.35f, 0.85f, 1.05f);
                c.transform.LookAt(bajie.transform.position + new Vector3(0, 0.74f, 0)); c.fieldOfView = 35;
            }
        }
        if (phase == "" || t < next) { if (t > EarsB + 0.5f) Finish(); return; }
        next += 1f / FPS;
        Capture($"{phase}_{frame++:000}.png");
        if (phase != "ears" && fl) Measure(fl, phase);
    }

    static void Capture(string name)
    {
        var cam = Camera.main; if (!cam) return;
        const int W = 1280, H = 720;
        var rt = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 4);
        cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        File.WriteAllBytes(Path.Combine(FramesDir, name), tex.EncodeToPNG()); Object.DestroyImmediate(tex);
    }

    static void Measure(WukongFlight fl, string ph)
    {
        maskCache ??= File.Exists(MaskPath) ? File.ReadAllBytes(MaskPath) : null;
        var smr = fl.character.GetComponentsInChildren<SkinnedMeshRenderer>().First();
        var m = new Mesh(); smr.BakeMesh(m, true);
        var v = m.vertices; var M = smr.transform.localToWorldMatrix; Object.DestroyImmediate(m);
        var pts = TailPoints(fl.character);
        const float tailR = 0.02f;
        int inside = 0; float deepest = 0; var region = new Dictionary<string, int>();
        var bones = smr.bones;
        for (int i = 0; i < v.Length; i++)
        {
            if (maskCache != null && maskCache[i] == 1) continue;
            var p = M.MultiplyPoint3x4(v[i]);
            float d = DistToChain(p, pts, 1);           // Tail_01 grows out of the body: skip the root segment
            if (d < tailR) { inside++; deepest = Mathf.Max(deepest, tailR - d); int sg = 1; float bd2 = 9; for (int q = 1; q < pts.Count - 1; q++) { float dq = DistToChain(p, pts.GetRange(q, 2), 0); if (dq < bd2) { bd2 = dq; sg = q + 1; } } region[$"seg{sg}"] = region.TryGetValue($"seg{sg}", out var cc) ? cc + 1 : 1; }
        }
        // staff: axis from mesh bounds
        string staffTxt = "";
        var staff = fl.character.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == "wukong_staff");
        var mf = staff ? staff.GetComponent<MeshFilter>() : null;
        if (mf)
        {
            var b = mf.sharedMesh.bounds; var e = b.extents;
            Vector3 ax = e.x > e.y && e.x > e.z ? Vector3.right * e.x : e.y > e.z ? Vector3.up * e.y : Vector3.forward * e.z;
            Vector3 s0 = staff.TransformPoint(b.center - ax), s1 = staff.TransformPoint(b.center + ax);
            float best = float.MaxValue;
            for (int s = 1; s < pts.Count - 1; s++) best = Mathf.Min(best, SegSeg(pts[s], pts[s + 1], s0, s1));
            staffTxt = $"staff clearance {(best - 0.026f - tailR) * 1000:F1} mm";
        }
        var twm = fl.character.GetComponent<TailWag>();
        stats.Add($"{ph} t={Time.time:F2}: tip(root space) {fl.character.InverseTransformPoint(pts[pts.Count - 2]).ToString("F3")} mid {fl.character.InverseTransformPoint(pts[7]).ToString("F3")} hipsRot {fl.animator.GetBoneTransform(HumanBodyBones.Hips).rotation.eulerAngles.ToString("F0")}; body verts inside tail {inside} (deepest {deepest * 1000:F1} mm), {staffTxt} flyDir {twm?.flyDirection} segs {string.Join(",", region.Select(kv => kv.Key + ":" + kv.Value))}");
    }

    static bool finished;
    static void Finish()
    {
        if (finished) return; finished = true;
        var sb = new StringBuilder();
        foreach (var ph in new[] { "stand", "fly" })
        {
            var rows = stats.Where(s => s.StartsWith(ph)).ToList();
            if (rows.Count == 0) continue;
            int Inside(string s) => int.Parse(s.Split(new[] { "inside tail " }, System.StringSplitOptions.None)[1].Split(' ')[0]);
            float Clr(string s) { var k = s.Split(new[] { "staff clearance " }, System.StringSplitOptions.None); return k.Length > 1 ? float.Parse(k[1].Split(' ')[0]) : 999; }
            sb.AppendLine($"== {ph}: {rows.Count} frames, frames with body verts inside tail: {rows.Count(r => Inside(r) > 0)}, max {rows.Max(Inside)}; " +
                          $"min staff clearance {rows.Min(Clr):F1} mm");
        }
        sb.AppendLine(); foreach (var s in stats) sb.AppendLine(s);
        File.AppendAllText(Report, sb.ToString());
        SessionState.SetBool(Key + ".done", true);
        EditorApplication.ExitPlaymode();
    }
}
