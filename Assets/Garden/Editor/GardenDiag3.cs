// Diagnostic (batch): render Wukong from the player position (like the user's screenshot) with/without the tree shadow caster
// and from above, to find the stray dark square and check the staff / contact shadow. No splats in edit mode.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class GardenDiag3
{
    public static void Run()
    {
        ShaderUtil.allowAsyncCompilation = false;
        EditorSceneManager.OpenScene(InterrogationGardenSetup.ScenePath, OpenSceneMode.Single);
        var wk = GameObject.Find("wukong_rigged").transform;
        var cam = Camera.main;
        var outDir = Path.Combine(GardenSetup.AtlasDir, "capture");
        cam.fieldOfView = 26f; cam.transform.rotation = Quaternion.LookRotation(wk.position + Vector3.up * 0.75f - cam.transform.position);
        Shot(cam, Path.Combine(outDir, "diag3_player.jpg"));
        var caster = GameObject.Find("ginseng_tree shadow caster (invisible)");
        if (caster) caster.SetActive(false);
        Shot(cam, Path.Combine(outDir, "diag3_nocaster.jpg"));
        if (caster) caster.SetActive(true);
        var top = new GameObject("top").AddComponent<Camera>(); top.CopyFrom(cam);
        top.GetUniversalAdditionalCameraData().SetRenderer(1);
        top.orthographic = true; top.orthographicSize = 2.2f;
        top.transform.SetPositionAndRotation(wk.position + Vector3.up * 10 + new Vector3(0.6f, 0, -0.6f), Quaternion.Euler(90, 0, 0));
        Shot(top, Path.Combine(outDir, "diag3_top.jpg"));
        EditorApplication.Exit(0);
    }
    static void Shot(Camera c, string p)
    {
        var rt = new RenderTexture(1280, 1280, 24); c.targetTexture = rt; c.Render(); c.targetTexture = null;
        RenderTexture.active = rt; var t = new Texture2D(1280, 1280, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, 1280, 1280), 0, 0); t.Apply();
        File.WriteAllBytes(p, t.EncodeToJPG(90));
    }
}
