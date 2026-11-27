// Diagnostic (batch): is the contact-shadow quad rendered at all? Top-down renders of Wukong's blob: as built, raised 5 cm,
// with an opaque red material, and on the default renderer.
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class GardenDiag5
{
    public static void Run()
    {
        ShaderUtil.allowAsyncCompilation = false;
        var sb = new StringBuilder();
        EditorSceneManager.OpenScene(InterrogationGardenSetup.ScenePath, OpenSceneMode.Single);
        var wk = GameObject.Find("wukong_rigged").transform;
        var blob = wk.GetComponentsInChildren<MeshRenderer>(true).First(r => r.name == "Contact Shadow");
        sb.AppendLine($"blob {blob.transform.position:F3} mat {blob.sharedMaterial.name} shader {blob.sharedMaterial.shader.name} supported {blob.sharedMaterial.shader.isSupported} queue {blob.sharedMaterial.renderQueue} layer {blob.gameObject.layer} tex {blob.sharedMaterial.GetTexture("_BaseMap")?.name}");
        var cam = Camera.main; var outDir = Path.Combine(GardenSetup.AtlasDir, "capture");
        var top = new GameObject("top").AddComponent<Camera>(); top.CopyFrom(cam);
        top.GetUniversalAdditionalCameraData().SetRenderer(1);
        top.fieldOfView = 24f; top.cullingMask = cam.cullingMask;
        top.transform.position = cam.transform.position; top.transform.rotation = Quaternion.LookRotation(wk.position + Vector3.up * 0.25f - cam.transform.position);   // front view at the feet, like the screenshot
        blob.sharedMaterial.renderQueue = 2060;
        Shot(top, Path.Combine(outDir, "diag5_a_asbuilt.jpg"));
        blob.transform.position += Vector3.up * 0.05f;
        Shot(top, Path.Combine(outDir, "diag5_b_raised.jpg"));
        var red = new Material(Shader.Find("Universal Render Pipeline/Unlit")); red.SetColor("_BaseColor", Color.red);
        var keep = blob.sharedMaterial; blob.sharedMaterial = red;
        Shot(top, Path.Combine(outDir, "diag5_c_red.jpg"));
        blob.sharedMaterial = keep; blob.transform.position -= Vector3.up * 0.05f;
        top.GetUniversalAdditionalCameraData().SetRenderer(0);
        Shot(top, Path.Combine(outDir, "diag5_d_default_renderer.jpg"));
        File.WriteAllText(Path.Combine(outDir, "diag5_log.txt"), sb.ToString());
        EditorApplication.Exit(0);
    }
    static void Shot(Camera c, string p)
    {
        var rt = new RenderTexture(512, 512, 24); c.targetTexture = rt; c.Render(); c.targetTexture = null;
        RenderTexture.active = rt; var t = new Texture2D(512, 512, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); t.Apply();
        File.WriteAllBytes(p, t.EncodeToJPG(90));
    }
}
