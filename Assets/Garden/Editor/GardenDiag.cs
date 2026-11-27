// Diagnostic (batch): opens WuzhuangGarden and renders a top-down view of the courtyard to check the ginseng-tree shadow.
// Unity -batchmode -force-d3d12 -projectPath <WuzhuangCase> -executeMethod GardenDiag.Run
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class GardenDiag
{
    public static void Run()
    {
        var sb = new StringBuilder();
        ShaderUtil.allowAsyncCompilation = false;
        EditorSceneManager.OpenScene(GardenSetup.ScenePath, OpenSceneMode.Single);
        var rp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        sb.AppendLine($"pipeline {rp?.name} shadowDistance {rp?.shadowDistance} mainLightShadows {rp?.supportsMainLightShadows} quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            sb.AppendLine($"light {l.name} type {l.type} int {l.intensity} shadows {l.shadows} fwd {l.transform.forward:F2}");
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => r.shadowCastingMode != ShadowCastingMode.Off))
            sb.AppendLine($"caster {r.name} mode {r.shadowCastingMode} bounds {r.bounds.center:F1} {r.bounds.size:F1} mat {r.sharedMaterial?.shader.name} kw {string.Join(",", r.sharedMaterial.shaderKeywords)}");
        var pave = GameObject.Find("Paving (tiled from photo)");
        sb.AppendLine($"paving shader {pave?.GetComponent<MeshRenderer>().sharedMaterial.shader.name} receive {pave?.GetComponent<MeshRenderer>().receiveShadows}");
        var cam = Camera.main;
        var go = new GameObject("diag cam"); var c = go.AddComponent<Camera>();
        c.CopyFrom(cam);
        c.transform.position = new Vector3(-1, 30, -1); c.transform.rotation = Quaternion.Euler(90, 0, 0);
        c.orthographic = true; c.orthographicSize = 12; c.farClipPlane = 100;
        var cd = c.GetUniversalAdditionalCameraData(); cd.renderPostProcessing = false; cd.renderShadows = true;
        var rt = new RenderTexture(1024, 1024, 24); c.targetTexture = rt; c.Render();
        RenderTexture.active = rt; var tex = new Texture2D(1024, 1024, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0); tex.Apply();
        var outDir = Path.Combine(GardenSetup.AtlasDir, "capture"); Directory.CreateDirectory(outDir);
        File.WriteAllBytes(Path.Combine(outDir, "diag_topdown.jpg"), tex.EncodeToJPG(90));
        File.WriteAllText(Path.Combine(outDir, "diag_log.txt"), sb.ToString());
        EditorApplication.Exit(0);
    }
}
