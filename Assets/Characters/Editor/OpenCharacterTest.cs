// One-shot helper: opens Assets/Scenes/CharacterTest.unity in the editor, selects the three characters and frames them.
// Never saves the previously open scene. If that scene has unsaved changes, CharacterTest is opened additively instead
// (so nothing is lost). Re-run via Tools > Characters > Open CharacterTest.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class OpenCharacterTest
{
    const string ScenePath = "Assets/Scenes/CharacterTest.unity";
    const string Marker = "Assets/Characters/Editor/.open_once_1";   // dot-file: ignored by the asset database
    static string LogPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../../_models/rigged/unity_open_log.txt"));

    static OpenCharacterTest()
    {
        if (File.Exists(Marker) || !File.Exists(ScenePath)) return;
        EditorApplication.delayCall += () => { File.WriteAllText(Marker, "done"); Open(); };
    }

    [MenuItem("Tools/Characters/Open CharacterTest")]
    public static void Open()
    {
        var sb = new System.Text.StringBuilder();
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { sb.AppendLine("editor is in play mode - not switching scenes"); return; }
            var dirty = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s.isDirty).ToList();
            foreach (var s in dirty) sb.AppendLine($"scene '{s.name}' has unsaved changes -> NOT saved, NOT closed");
            Scene scene;
            if (dirty.Count == 0)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                sb.AppendLine("opened CharacterTest (single)");
            }
            else
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                sb.AppendLine("opened CharacterTest additively (previous scene kept open because it was modified)");
            }
            var chars = scene.GetRootGameObjects().Where(g => g.name.EndsWith("_rigged")).ToArray();
            Selection.objects = chars;
            var sv = SceneView.lastActiveSceneView ?? (SceneView.sceneViews.Count > 0 ? (SceneView)SceneView.sceneViews[0] : null);
            if (sv != null) { sv.FrameSelected(); sv.Repaint(); }
            var cam = scene.GetRootGameObjects().Select(g => g.GetComponent<Camera>()).FirstOrDefault(c => c != null);
            sb.AppendLine($"selected {chars.Length}: {string.Join(", ", chars.Select(c => c.name))}; scene view framed: {sv != null}");
            sb.AppendLine($"camera: {(cam ? cam.name + " tag=" + cam.tag + " enabled=" + cam.enabled : "none")}; " +
                          $"cameras in loaded scenes: {Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length}");
            // make sure the Game view repaints with the CharacterTest camera
            var gvType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gvType != null) foreach (var w in Resources.FindObjectsOfTypeAll(gvType)) ((EditorWindow)w).Repaint();
        }
        catch (System.Exception e) { sb.AppendLine("ERROR " + e); }
        finally { File.WriteAllText(LogPath, sb.ToString()); Debug.Log("[OpenCharacterTest]\n" + sb); }
    }
}
