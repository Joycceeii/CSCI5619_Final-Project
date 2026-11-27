// Diagnostic (batch): list every renderer near Wukong in Interrogation_Garden (to find a stray dark square on the floor).
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GardenDiag2
{
    public static void Run()
    {
        var sb = new StringBuilder();
        EditorSceneManager.OpenScene(InterrogationGardenSetup.ScenePath, OpenSceneMode.Single);
        var wk = GameObject.Find("wukong_rigged").transform.position;
        sb.AppendLine($"wukong at {wk:F2}");
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var b = r.bounds; float d = Vector2.Distance(new Vector2(b.center.x, b.center.z), new Vector2(wk.x, wk.z));
            if (d < 4f && b.size.magnitude < 3f)
                sb.AppendLine($"{d:F2} m  {GetPath(r.transform)}  {r.GetType().Name} active={r.gameObject.activeInHierarchy} enabled={r.enabled} center {b.center:F2} size {b.size:F2} mat {r.sharedMaterial?.name} cast {r.shadowCastingMode}");
        }
        File.WriteAllText(Path.Combine(GardenSetup.AtlasDir, "capture", "diag2_log.txt"), sb.ToString());
        EditorApplication.Exit(0);
    }
    static string GetPath(Transform t) => t.parent ? GetPath(t.parent) + "/" + t.name : t.name;
}
