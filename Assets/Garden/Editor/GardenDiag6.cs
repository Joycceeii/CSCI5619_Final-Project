// Diagnostic (batch): Wukong's staff with/without GripTilt, front + side, on a sampled idle frame (edit mode, no splats).
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class GardenDiag6
{
    public static void Run()
    {
        ShaderUtil.allowAsyncCompilation = false;
        EditorSceneManager.OpenScene(InterrogationGardenSetup.ScenePath, OpenSceneMode.Single);
        var wk = GameObject.Find("wukong_rigged"); var anim = wk.GetComponent<Animator>();
        var clip = anim.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name.ToLower().Contains("stand")) ?? anim.runtimeAnimatorController.animationClips[0];
        var outDir = Path.Combine(GardenSetup.AtlasDir, "capture");
        var cam = new GameObject("c").AddComponent<Camera>(); cam.CopyFrom(Camera.main); cam.GetUniversalAdditionalCameraData().SetRenderer(1); cam.fieldOfView = 30;
        var hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        float s = wk.transform.localScale.y; Vector3 c0 = wk.transform.position + Vector3.up * 0.6f * s;
        foreach (var tilt in new[] { false, true })
        {
            AnimationMode.StartAnimationMode(); AnimationMode.BeginSampling(); AnimationMode.SampleAnimationClip(wk, clip, 1.0f); AnimationMode.EndSampling();
            if (tilt) { var g = wk.GetComponent<GripTilt>() ?? wk.AddComponent<GripTilt>(); g.hand = hand; g.character = wk.transform; g.Apply(); }
            foreach (var (n, dir) in new[] { ("front", wk.transform.forward), ("side", wk.transform.right) })
            {
                cam.transform.position = c0 + dir * 2.6f; cam.transform.LookAt(c0);
                var rt = new RenderTexture(800, 800, 24); cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
                RenderTexture.active = rt; var t = new Texture2D(800, 800, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, 800, 800), 0, 0); t.Apply();
                File.WriteAllBytes(Path.Combine(outDir, $"diag6_{(tilt ? "tilt" : "plain")}_{n}.jpg"), t.EncodeToJPG(90));
            }
            AnimationMode.StopAnimationMode();
        }
        File.WriteAllText(Path.Combine(outDir, "diag6_log.txt"), $"clip {clip.name}");
        EditorApplication.Exit(0);
    }
}
