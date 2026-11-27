// One-off close-up of Shaseng's grip in CharacterTest (edit mode, bind pose) -> _models/rigged/shaseng_grip_unity.png
using System.IO; using UnityEditor; using UnityEditor.SceneManagement; using UnityEngine;
public static class GripShot
{
    public static void Run()
    {
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/CharacterTest.unity", OpenSceneMode.Single);
        CharacterTestSetup.Run();
        var sha = GameObject.Find("shaseng_rigged"); var cam = Camera.main;
        var shots = new[] { (new Vector3(0.62f, 0.6f, 0.5f), "front_right"), (new Vector3(0.05f, 0.62f, 0.75f), "front"), (new Vector3(0.75f, 0.55f, -0.1f), "side") };
        foreach (var (off, n) in shots)
        {
            var root = sha.transform.position;
            cam.transform.position = root + off; cam.transform.LookAt(root + new Vector3(0.27f, 0.5f, 0.05f)); cam.fieldOfView = 22;
            var rt = new RenderTexture(900, 900, 24) { antiAliasing = 4 }; cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt; var tex = new Texture2D(900, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 900, 900), 0, 0); tex.Apply();
            RenderTexture.active = null; cam.targetTexture = null;
            File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, $"../../_models/rigged/shaseng_grip_unity_{n}.png")), tex.EncodeToPNG());
        }
        EditorApplication.Exit(0);
    }
}
