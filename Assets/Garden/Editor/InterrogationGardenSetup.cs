// Builds Assets/Scenes/Interrogation_Garden.unity: the WuzhuangGarden courtyard (rebuilt version + decorations + suspects)
// with the WhoseStory interrogation system on top. The original Assets/WhoseStory/Interrogation.unity is NOT modified.
//   - XR rig (same builder as the original game) at the player position, facing the line-up
//   - Interrogation object: OpenAiClient (existing OpenAiSettings asset), MicrophoneCapture, TranscriptLogger, InterrogationManager
//   - InterrogationCharacter on Wukong / Bajie / Shaseng (prompts: StreamingAssets/WhoseStory/wuzhuang_preamble.txt + wukong/bajie/shaseng.txt)
//   - GardenInterrogationBridge: tells on questions, lip sync on replies
// Tools > Garden > Build Interrogation_Garden   |   batch: -executeMethod InterrogationGardenSetup.BatchBuild
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WhoseStory;

public static class InterrogationGardenSetup
{
    public const string ScenePath = "Assets/Scenes/Interrogation_Garden.unity";

    public static void BatchBuild() { var l = Build(true); Debug.Log(l); EditorApplication.Exit(l.Contains("ERROR") ? 1 : 0); }

    [MenuItem("Tools/Garden/Build Interrogation_Garden")]
    public static string BuildMenu() => Build(true);

    public static string Build(bool rebuildGarden)
    {
        var log = new StringBuilder();
        try
        {
            if (rebuildGarden) log.Append(GardenSetup.Build());
            if (!Application.isBatchMode && EditorSceneManager.GetActiveScene().isDirty) EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            AssetDatabase.DeleteAsset(ScenePath);
            if (!AssetDatabase.CopyAsset(GardenSetup.ScenePath, ScenePath)) throw new System.Exception("copy failed");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var cam = Camera.main;
            Vector3 camPos = cam.transform.position;
            float yaw = cam.transform.eulerAngles.y;
            // same XR rig as the original game: XR Origin (floor) -> Camera Offset (1.6 m) -> camera with TrackedPoseDriver
            WhoseStory.EditorTools.XrRigBuilder.Setup(cam);
            var origin = cam.transform.parent.parent;
            origin.SetPositionAndRotation(new Vector3(camPos.x, 0, camPos.z), Quaternion.Euler(0, yaw, 0));
            log.AppendLine($"XR Origin at {origin.position:F2} yaw {yaw:F0}");

            var settings = AssetDatabase.LoadAssetAtPath<OpenAiSettings>("Assets/WhoseStory/OpenAiSettings.asset");
            var go = new GameObject("Interrogation");
            var client = go.AddComponent<OpenAiClient>(); client.settings = settings;
            var mic = go.AddComponent<MicrophoneCapture>();
            go.AddComponent<TranscriptLogger>();
            var manager = go.AddComponent<InterrogationManager>();

            InterrogationCharacter Suspect(string obj, string name, string prompt, string voice, float pitch)
            {
                var m = GameObject.Find(obj);
                if (!m) throw new System.Exception("missing " + obj);
                var c = m.AddComponent<InterrogationCharacter>();
                c.characterName = name; c.promptFileName = prompt; c.preambleFileName = "wuzhuang_preamble.txt";
                c.voice = voice; c.voicePitch = pitch;
                EditorUtility.SetDirty(c);
                return c;
            }
            var wukong = Suspect("wukong_rigged", "Wukong 悟空", "wukong.txt", "verse", 1.08f);
            var bajie = Suspect("bajie_rigged", "Bajie 八戒", "bajie.txt", "ash", 0.88f);
            var shaseng = Suspect("shaseng_rigged", "Shaseng 沙僧", "shaseng.txt", "onyx", 0.95f);
            manager.client = client; manager.microphone = mic; manager.playerHead = cam.transform;
            manager.characters = new[] { wukong, bajie, shaseng };
            var bridge = go.AddComponent<GardenInterrogationBridge>(); bridge.characters = manager.characters;
            EditorUtility.SetDirty(manager);

            int mm = MiniMaxVoiceDesigner.ApplyToOpenScene();   // saved MiniMax voice ids (if the voices were designed)
            log.AppendLine(mm > 0 ? $"MiniMax voices applied to {mm} suspects" : "no MiniMax voice ids yet (OpenAI voices in use)");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine("saved " + ScenePath);

            // Build Settings: add the new scene, keep everything that is there
            var list = EditorBuildSettings.scenes.ToList();
            if (!list.Any(s => s.path == ScenePath)) { list.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = list.ToArray(); log.AppendLine("added to Build Settings"); }
            log.AppendLine("Build Settings: " + string.Join(", ", EditorBuildSettings.scenes.Select(s => s.path)));
        }
        catch (System.Exception e) { log.AppendLine("ERROR " + e); }
        File.WriteAllText(Path.Combine(GardenSetup.AtlasDir, "interrogation_garden_build_log.txt"), log.ToString());
        return log.ToString();
    }
}
