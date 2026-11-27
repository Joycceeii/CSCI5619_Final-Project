// Keeps the MiniMax key file out of player builds: StreamingAssets is copied verbatim into a build, so the key file is moved
// aside before the build and put back afterwards (also after a failed build, via the editor update hook).
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class KeepKeysOutOfBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    static readonly string[] Keys = { "minimax_key.txt" };
    static string Dir => Path.Combine(Application.streamingAssetsPath, "WhoseStory");
    static string Aside => Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/keys_out_of_build"));
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        Directory.CreateDirectory(Aside);
        foreach (var k in Keys)
            foreach (var f in new[] { k, k + ".meta" })
            {
                string src = Path.Combine(Dir, f);
                if (File.Exists(src)) { File.Copy(src, Path.Combine(Aside, f), true); File.Delete(src); }
            }
        AssetDatabase.Refresh();
        EditorApplication.update -= RestoreWhenIdle; EditorApplication.update += RestoreWhenIdle;
    }

    public void OnPostprocessBuild(BuildReport report) => Restore();

    static void RestoreWhenIdle()
    {
        if (BuildPipeline.isBuildingPlayer) return;
        EditorApplication.update -= RestoreWhenIdle;
        Restore();
    }

    static void Restore()
    {
        if (!Directory.Exists(Aside)) return;
        foreach (var f in Directory.GetFiles(Aside))
        {
            string dst = Path.Combine(Dir, Path.GetFileName(f));
            if (!File.Exists(dst)) File.Copy(f, dst);
            File.Delete(f);
        }
        AssetDatabase.Refresh();
    }
}
