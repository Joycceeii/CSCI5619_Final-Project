// Builds the uLipSync profile for Mandarin A/I/U/E/O from calibration recordings of the voice actually used
// (Assets/Characters/Voice/Calibration/calib_{A,I,U,E,O}.wav, generated with Windows SAPI Huihui).
// Tools > Characters > Build LipSync Profile. Output: Assets/Characters/Voice/wukong_zh_lipsync_profile.asset (+ .json)
// and _models/rigged/lipsync_profile_log.txt (per-vowel self-test + vowel histogram of the test line).
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

public static class LipSyncProfileBuilder
{
    public const string ProfilePath = "Assets/Characters/Voice/wukong_zh_lipsync_profile.asset";
    public static string ProfileFor(string key) => $"Assets/Characters/Voice/{key}_zh_lipsync_profile.asset";
    static string CalibDir(string key) => key == "wukong" ? "Assets/Characters/Voice/Calibration" : $"Assets/Characters/Voice/Calibration/{key}";
    static string LineFor(string key) => $"Assets/Audio/Test/{key}_line_01.wav";
    static readonly string[] Vowels = { "A", "I", "U", "E", "O" };
    const int PerVowel = 48;

    [MenuItem("Tools/Characters/Build LipSync Profile")]
    public static void Build()
    {
        var all = new StringBuilder();
        foreach (var k in new[] { "wukong", "bajie", "shaseng" })
            if (File.Exists($"{CalibDir(k)}/calib_A.wav")) all.Append(Build(k));
        var outp = Path.GetFullPath(Path.Combine(Application.dataPath, "../../_models/rigged/lipsync_profile_log.txt"));
        File.WriteAllText(outp, all.ToString());
    }

    public static string Build(string key)
    {
        var log = new StringBuilder($"== {key}\n");
        string ProfilePath = ProfileFor(key);
        AssetDatabase.DeleteAsset(ProfilePath);
        var profile = ScriptableObject.CreateInstance<uLipSync.Profile>();
        profile.mfccDataCount = PerVowel;
        foreach (var v in Vowels) profile.AddMfcc(v);
        AssetDatabase.CreateAsset(profile, ProfilePath);

        // ---- collect MFCCs from the loud (vowel nucleus) frames of each calibration file
        var go = new GameObject("uLipSync calibration"); var ls = go.AddComponent<uLipSync.uLipSync>();
        ls.OnBakeStart(profile);
        for (int k = 0; k < Vowels.Length; k++)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{CalibDir(key)}/calib_{Vowels[k]}.wav");
            if (!clip) { log.AppendLine($"missing calib_{Vowels[k]}.wav"); continue; }
            var frames = Frames(clip);
            var list = new List<(float rms, float[] mfcc)>();
            foreach (var f in frames)
            {
                ls.OnBakeUpdate(f, clip.channels);
                list.Add((Rms(f), ls.mfcc.ToArray()));
            }
            // skip the first frames of every syllable onset: keep the loudest 45% (vowel nuclei)
            var thr = list.Select(x => x.rms).OrderByDescending(x => x).ElementAt(Mathf.Min(list.Count - 1, (int)(list.Count * 0.45f)));
            var voiced = list.Where(x => x.rms >= thr && x.rms > 0.02f).ToList();
            var pick = Enumerable.Range(0, PerVowel).Select(i => voiced[(int)((i + 0.5f) * voiced.Count / PerVowel)]).ToList();
            foreach (var p in pick)
            {
                var na = new NativeArray<float>(p.mfcc, Allocator.Temp);
                profile.UpdateMfcc(k, na, true); na.Dispose();
            }
            log.AppendLine($"{Vowels[k]}: {frames.Count} frames, {voiced.Count} voiced, used {pick.Count}");
        }
        ls.OnBakeEnd(); Object.DestroyImmediate(go);
        profile.UpdateMeansAndStandardization();
        EditorUtility.SetDirty(profile); AssetDatabase.SaveAssets();
        profile.Export(Path.GetFullPath(ProfilePath.Replace(".asset", ".json")));

        // ---- self-test: classify each calibration file and the test line with the new profile
        foreach (var v in Vowels)
            log.AppendLine($"self-test calib_{v}: {Histogram(profile, AssetDatabase.LoadAssetAtPath<AudioClip>($"{CalibDir(key)}/calib_{v}.wav"))}");
        log.AppendLine($"test line: {Histogram(profile, AssetDatabase.LoadAssetAtPath<AudioClip>(LineFor(key)))}");
        Debug.Log("[LipSyncProfileBuilder]\n" + log);
        return log.ToString();
    }

    static List<float[]> Frames(AudioClip clip)
    {
        var buf = new float[clip.samples * clip.channels]; clip.GetData(buf, 0);
        int spf = clip.frequency / 60 * clip.channels;
        var res = new List<float[]>();
        for (int o = 0; o + spf < buf.Length; o += spf) { var t = new float[spf]; System.Array.Copy(buf, o, t, 0, spf); res.Add(t); }
        return res;
    }
    static float Rms(float[] f) { double s = 0; foreach (var x in f) s += x * x; return Mathf.Sqrt((float)(s / f.Length)); }

    static string Histogram(uLipSync.Profile profile, AudioClip clip)
    {
        if (!clip) return "missing";
        var go = new GameObject("uLipSync test"); var ls = go.AddComponent<uLipSync.uLipSync>(); ls.OnBakeStart(profile);
        var cnt = new Dictionary<string, int>(); int loud = 0;
        foreach (var f in Frames(clip))
        {
            ls.OnBakeUpdate(f, clip.channels);
            if (Rms(f) < 0.03f) continue; loud++;
            var best = ls.result.phonemeRatios.OrderByDescending(kv => kv.Value).First().Key;
            cnt[best] = cnt.TryGetValue(best, out var c) ? c + 1 : 1;
        }
        ls.OnBakeEnd(); Object.DestroyImmediate(go);
        return $"{loud} loud frames -> " + string.Join(" ", Vowels.Select(v => $"{v}:{(cnt.TryGetValue(v, out var c) ? 100f * c / Mathf.Max(1, loud) : 0):F0}%"));
    }
}
