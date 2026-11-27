// Diagnostic (batch): why are the contact shadows not visible? Renders Wukong's feet with the paving on the shadow-receiver
// shader and on plain URP/Unlit, and logs the blob renderers.
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GardenDiag4
{
    public static void Run()
    {
        ShaderUtil.allowAsyncCompilation = false;
        var sb = new StringBuilder();
        EditorSceneManager.OpenScene(InterrogationGardenSetup.ScenePath, OpenSceneMode.Single);
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(r => r.name == "Contact Shadow").Take(5))
            sb.AppendLine($"{r.transform.parent.name}: active {r.gameObject.activeInHierarchy} enabled {r.enabled} pos {r.transform.position:F3} up {r.transform.forward:F2} lossy {r.transform.lossyScale:F2} mat {r.sharedMaterial?.name} shader {r.sharedMaterial?.shader.name} queue {r.sharedMaterial?.renderQueue}");
        var wk = GameObject.Find("wukong_rigged").transform; var cam = Camera.main;
        cam.fieldOfView = 22f; cam.transform.rotation = Quaternion.LookRotation(wk.position + Vector3.up * 0.3f - cam.transform.position);
        var outDir = Path.Combine(GardenSetup.AtlasDir, "capture");
        Shot(cam, Path.Combine(outDir, "diag4_receiver.jpg"));
        var pave = GameObject.Find("Paving (tiled from photo)").GetComponent<MeshRenderer>();
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); m.SetTexture("_BaseMap", pave.sharedMaterial.GetTexture("_BaseMap")); m.SetTextureScale("_BaseMap", pave.sharedMaterial.GetTextureScale("_BaseMap"));
        pave.sharedMaterial = m;
        Shot(cam, Path.Combine(outDir, "diag4_unlit.jpg"));
        File.WriteAllText(Path.Combine(outDir, "diag4_log.txt"), sb.ToString());
        EditorApplication.Exit(0);
    }
    static void Shot(Camera c, string p)
    {
        var rt = new RenderTexture(960, 960, 24); c.targetTexture = rt; c.Render(); c.targetTexture = null;
        RenderTexture.active = rt; var t = new Texture2D(960, 960, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, 960, 960), 0, 0); t.Apply();
        File.WriteAllBytes(p, t.EncodeToJPG(90));
    }
}
