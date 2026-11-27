// One-shot setup for the Journey-to-the-West characters (Wukong / Bajie / Shaseng).
// - creates Toon (project style, "Toon/Toon") and URP Lit materials from the baked textures and remaps them onto the FBX
// - verifies the Humanoid avatars and logs the bone mapping
// - builds Assets/Scenes/CharacterTest.unity ADDITIVELY (the open scene, e.g. Interrogation, is never modified), saves and closes it
// - renders a screenshot from an isolated preview scene to _models/rigged/unity_test.png
// Runs automatically once (when CharacterTest.unity does not exist yet); re-run via Tools > Characters > Rebuild CharacterTest.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class CharacterTestSetup
{
    const string ScenePath = "Assets/Scenes/CharacterTest.unity";
    static readonly (string folder, string key)[] Chars = { ("Wukong", "wukong"), ("Bajie", "bajie"), ("Shaseng", "shaseng") };
    static string ModelsDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../_models/rigged"));

    // bump to make the setup run once more automatically after the next script compile
    const int Version = 7;
    const bool UseToon = false;   // true = remap the FBX onto the *_Toon materials instead of *_Lit
    const string VersionFile = "Assets/Characters/Editor/.setup_version";   // dot-file: ignored by the asset database

    static CharacterTestSetup()
    {
        if (!Directory.Exists("Assets/Characters/Wukong")) return;
        string have = File.Exists(VersionFile) ? File.ReadAllText(VersionFile).Trim() : "";
        if (have != Version.ToString())
            EditorApplication.delayCall += () => { File.WriteAllText(VersionFile, Version.ToString()); Run(); };
    }

    // Unity's auto-mapper picks Spine/Head etc. fine but skips Chest/Jaw and guessed bajie's ear roots as eyes.
    static void FixHumanMapping(ModelImporter mi, StringBuilder log, string folder)
    {
        var hd = mi.humanDescription;
        // rebuild the skeleton (bind pose) from the current FBX: a cached one goes stale when bones are added (tail 7 -> 14 bones)
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(mi.assetPath);
        if (model)
        {
            hd.skeleton = model.GetComponentsInChildren<Transform>(true).Select(t => new SkeletonBone
            { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray();
            log.AppendLine($"[{folder}] skeleton rebuilt from the FBX: {hd.skeleton.Length} transforms");
        }
        var list = hd.human.Where(h => !(h.humanName.EndsWith("Eye") && h.boneName.StartsWith("Ear"))).ToList();
        var skeleton = hd.skeleton.Select(s => s.name).ToHashSet();
        void Map(string human, string bone)
        {
            if (!skeleton.Contains(bone) || list.Any(h => h.humanName == human)) return;
            var hb = new HumanBone { humanName = human, boneName = bone }; hb.limit.useDefaultValues = true; list.Add(hb);
        }
        Map("Chest", "Spine1");
        Map("Jaw", "Jaw");
        int removed = hd.human.Length - list.Count(h => hd.human.Any(o => o.humanName == h.humanName));
        hd.human = list.ToArray();
        mi.humanDescription = hd;
        mi.SaveAndReimport();
        log.AppendLine($"[{folder}] mapping fixed: Chest<-Spine1, Jaw<-Jaw, removed {removed} ear->eye guesses");
    }

    [MenuItem("Tools/Characters/Rebuild CharacterTest")]
    public static void Run()
    {
        var log = new StringBuilder();
        try
        {
            AssetDatabase.Refresh();
            var placed = new List<string>();
            foreach (var (folder, key) in Chars)
            {
                string dir = $"Assets/Characters/{folder}";
                string fbx = $"{dir}/{key}_rigged.fbx";
                if (!File.Exists(fbx)) { log.AppendLine($"[{folder}] missing {fbx}"); continue; }
                var remaps = new Dictionary<string, Material>();
                remaps[$"{key}_mat"] = MakeMaterials(dir, key, log);
                if (File.Exists($"{dir}/Textures/{key}_staff_basecolor.png"))
                    remaps[$"{key}_staff_mat"] = MakeMaterials(dir, $"{key}_staff", log);
                var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
                // match FBX material names by prefix (Blender may have suffixed them, e.g. "wukong_staff_mat.001")
                var fbxMatNames = AssetDatabase.LoadAllAssetRepresentationsAtPath(fbx).OfType<Material>().Select(m => m.name)
                                   .Concat(mi.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)).Select(k => k.name))
                                   .Concat(remaps.Keys).Distinct().ToList();
                foreach (var n in fbxMatNames)
                {
                    var target = remaps.Where(kv => n == kv.Key || n.StartsWith(kv.Key + ".")).OrderByDescending(kv => kv.Key.Length).Select(kv => kv.Value).FirstOrDefault();
                    if (target) { mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), target); log.AppendLine($"[{folder}] remap {n} -> {target.name}"); }
                }
                mi.SaveAndReimport();
                // standalone prop fbx (wukong_staff.fbx) gets the same staff material
                string prop = $"{dir}/{key}_staff.fbx";
                if (File.Exists(prop) && remaps.ContainsKey($"{key}_staff_mat"))
                {
                    var pi = (ModelImporter)AssetImporter.GetAtPath(prop);
                    pi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), $"{key}_staff_mat"), remaps[$"{key}_staff_mat"]);
                    pi.SaveAndReimport();
                }
                FixHumanMapping(mi, log, folder);
                // avatar check
                var avatar = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Avatar>().FirstOrDefault();
                log.AppendLine($"[{folder}] avatar: {(avatar ? avatar.name : "NONE")} isHuman={avatar && avatar.isHuman} isValid={avatar && avatar.isValid}");
                var hd = mi.humanDescription;
                log.AppendLine($"[{folder}] mapped human bones ({hd.human.Length}): " +
                               string.Join(", ", hd.human.Select(h => $"{h.humanName}<-{h.boneName}")));
                string[] required = { "Hips", "Spine", "Head", "LeftUpperArm", "LeftLowerArm", "LeftHand", "RightUpperArm", "RightLowerArm",
                                      "RightHand", "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "RightUpperLeg", "RightLowerLeg", "RightFoot" };
                var missing = required.Where(r => !hd.human.Any(h => h.humanName == r)).ToArray();
                log.AppendLine($"[{folder}] missing required: {(missing.Length == 0 ? "none" : string.Join(",", missing))}; " +
                               $"Jaw mapped: {hd.human.Any(h => h.humanName == "Jaw")}");
                placed.Add(fbx);
            }

            MouthMaterials.Ensure(log);
            // ---- MSAA 4x on every URP asset (log the old values)
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(p);
                if (!urp) continue;
                int old = urp.msaaSampleCount;
                if (old != 4) { urp.msaaSampleCount = 4; EditorUtility.SetDirty(urp); }
                log.AppendLine($"URP asset {p}: MSAA {old}x -> 4x");
            }
            AssetDatabase.SaveAssets();

            // ---- CharacterTest already open in the editor: rebuild it in place, save, screenshot from its own camera
            var openScene = SceneManager.GetSceneByPath(ScenePath);
            if (openScene.IsValid() && openScene.isLoaded)
            {
                var prevActive = SceneManager.GetActiveScene();
                SceneManager.SetActiveScene(openScene);
                foreach (var r in openScene.GetRootGameObjects()) Object.DestroyImmediate(r);
                var roots = BuildContent(placed, log);
                EditorSceneManager.SaveScene(openScene);
                log.AppendLine($"rebuilt + saved open {ScenePath}");
                if (SceneManager.sceneCount == 1)
                    RenderCamera(roots.Select(r => r.GetComponent<Camera>()).First(c => c != null), log);
                else Screenshot(placed, log);
                if (prevActive.IsValid() && prevActive != openScene) SceneManager.SetActiveScene(prevActive);
                Selection.objects = roots.Where(r => r.name.EndsWith("_rigged")).ToArray();
                SceneView.lastActiveSceneView?.FrameSelected();
            }
            else
            {
                // ---- build the scene additively, save, close (active scene untouched)
                var active = SceneManager.GetActiveScene();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);   // everything below is created in the NEW scene, never in the user's scene
                try
                {
                    BuildContent(placed, log);
                    Directory.CreateDirectory("Assets/Scenes");
                    EditorSceneManager.SaveScene(scene, ScenePath);
                    log.AppendLine($"saved {ScenePath}");
                    Screenshot(placed, log);
                }
                finally
                {
                    if (active.IsValid()) SceneManager.SetActiveScene(active);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
        catch (System.Exception e) { log.AppendLine("ERROR " + e); }
        Directory.CreateDirectory(ModelsDir);
        File.WriteAllText(Path.Combine(ModelsDir, "unity_import_log.txt"), log.ToString());
        Debug.Log("[CharacterTestSetup]\n" + log);
    }

    static Material MakeMaterials(string dir, string key, StringBuilder log)
    {
        string tex = $"{dir}/Textures";
        // re-import so CharacterImportRules applies even if the textures arrived before the scripts compiled
        foreach (var suffix in new[] { "_basecolor", "_normal", "_unity_metallicSmoothness" })
            if (File.Exists($"{tex}/{key}{suffix}.png")) AssetDatabase.ImportAsset($"{tex}/{key}{suffix}.png", ImportAssetOptions.ForceUpdate);
        var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tex}/{key}_basecolor.png");
        var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tex}/{key}_normal.png");
        var ms = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tex}/{key}_unity_metallicSmoothness.png");
        Directory.CreateDirectory($"{dir}/Materials");

        // URP Lit (PBR) version
        var lit = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = $"{key}_Lit" };
        lit.SetTexture("_BaseMap", baseTex); lit.SetColor("_BaseColor", Color.white);
        if (nrm) { lit.SetTexture("_BumpMap", nrm); lit.SetFloat("_BumpScale", 1f); lit.EnableKeyword("_NORMALMAP"); }
        if (ms) { lit.SetTexture("_MetallicGlossMap", ms); lit.SetFloat("_Smoothness", 1f); lit.SetFloat("_Metallic", 1f); lit.EnableKeyword("_METALLICSPECGLOSSMAP"); }
        CreateOrReplace(lit, $"{dir}/Materials/{key}_Lit.mat");

        // Toon version (matches the project's characters, which use Unity Toon Shader)
        var toonShader = Shader.Find("Toon/Toon");
        // default look: URP Lit (felt/clay stop-motion style); the Toon versions are still created alongside
        Material main = AssetDatabase.LoadAssetAtPath<Material>($"{dir}/Materials/{key}_Lit.mat");
        if (toonShader != null)
        {
            var toon = new Material(toonShader) { name = $"{key}_Toon" };
            toon.SetColor("_BaseColor", Color.white);
            toon.SetTexture("_MainTex", baseTex);
            toon.SetTexture("_1st_ShadeMap", baseTex);
            toon.SetTexture("_2nd_ShadeMap", baseTex);
            toon.SetColor("_1st_ShadeColor", new Color(0.78f, 0.74f, 0.78f));
            toon.SetColor("_2nd_ShadeColor", new Color(0.62f, 0.58f, 0.64f));
            toon.SetFloat("_BaseColor_Step", 0.45f); toon.SetFloat("_BaseShade_Feather", 0.08f);
            toon.SetFloat("_ShadeColor_Step", 0.2f); toon.SetFloat("_1st2nd_Shades_Feather", 0.08f);
            if (nrm) { toon.SetTexture("_NormalMap", nrm); toon.SetFloat("_BumpScale", 1f); toon.SetFloat("_Is_NormalMapToBase", 1f); }
            toon.SetFloat("_GI_Intensity", 0.4f);
            toon.SetFloat("_RimLight", 0f);
            toon.SetFloat("_HighColor_Power", 0f);
            toon.SetFloat("_Outline_Width", 0f);
            CreateOrReplace(toon, $"{dir}/Materials/{key}_Toon.mat");
            if (UseToon) main = AssetDatabase.LoadAssetAtPath<Material>($"{dir}/Materials/{key}_Toon.mat");
        }
        else log.AppendLine($"Toon/Toon shader not found - using URP Lit for {key}");
        log.AppendLine($"materials for {key}: base={(baseTex ? "ok" : "MISSING")} normal={(nrm ? "ok" : "MISSING")} metalSmooth={(ms ? "ok" : "MISSING")} -> default {main.shader.name}");
        return main;
    }

    static void CreateOrReplace(Material m, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing) { existing.shader = m.shader; existing.CopyPropertiesFromMaterial(m); EditorUtility.SetDirty(existing); }
        else AssetDatabase.CreateAsset(m, path);
        AssetDatabase.SaveAssets();
    }

    static List<GameObject> BuildContent(List<string> models, StringBuilder log)
    {
        var roots = new List<GameObject>();
        float spacing = 1.1f;
        for (int i = 0; i < models.Count; i++)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(models[i]);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            // camera looks down -Z, so +X is screen-left: Wukong, Bajie, Shaseng read left to right
            go.transform.position = new Vector3(-(i - (models.Count - 1) / 2f) * spacing, 0, 0);
            go.transform.rotation = Quaternion.identity;
            roots.Add(go);
            CharacterDynamics.Add(go, Path.GetFileNameWithoutExtension(models[i]).Replace("_rigged", ""));   // tail / ear spring bones
            CharacterVoiceSetup.Add(go, Path.GetFileNameWithoutExtension(models[i]).Replace("_rigged", ""));  // voice + lip sync (T = test line)
            var b = Bounds(go);
            log.AppendLine($"placed {go.name}: bounds size {b.size.ToString("F3")} center {b.center.ToString("F3")}");
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                log.AppendLine($"   renderer {r.name} ({r.GetType().Name}) parent={r.transform.parent?.name} bounds {r.bounds.size.ToString("F3")} @ {r.bounds.center.ToString("F3")} mat={r.sharedMaterial?.name}");
        }
        CharacterVoiceSetup.LinkAskBox(roots);   // one question box asks all three suspects
        var cam = new GameObject("Camera").AddComponent<Camera>();
        cam.transform.position = new Vector3(0, 0.62f, 3.4f);
        cam.transform.rotation = Quaternion.Euler(4, 180, 0);
        cam.fieldOfView = 40; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.62f, 0.64f, 0.70f);
        cam.tag = "MainCamera"; cam.allowMSAA = true;
        var camData = cam.GetUniversalAdditionalCameraData();
        camData.renderPostProcessing = true; camData.renderShadows = true;
        camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; camData.antialiasingQuality = AntialiasingQuality.High;
        roots.Add(cam.gameObject);
        // characters face +Z, camera looks -Z: viewer-left = +X
        var key = new GameObject("Key Light (warm, front-left)").AddComponent<Light>();
        key.type = LightType.Directional; key.intensity = 1.6f; key.color = new Color(1f, 0.90f, 0.78f);
        key.transform.rotation = Quaternion.Euler(38, -145, 0); key.shadows = LightShadows.Soft; key.shadowStrength = 0.75f;
        roots.Add(key.gameObject);
        var rim = new GameObject("Rim Light (cool, back)").AddComponent<Light>();
        rim.type = LightType.Directional; rim.intensity = 1.4f; rim.color = new Color(0.68f, 0.80f, 1f);
        rim.transform.rotation = Quaternion.Euler(28, 20, 0); rim.shadows = LightShadows.None;
        roots.Add(rim.gameObject);
        var fill = new GameObject("Fill Light (front-right)").AddComponent<Light>();
        fill.type = LightType.Directional; fill.intensity = 0.35f; fill.color = new Color(0.88f, 0.9f, 1f);
        fill.transform.rotation = Quaternion.Euler(15, 150, 0); fill.shadows = LightShadows.None;
        roots.Add(fill.gameObject);
        // local post-processing volume (only affects cameras inside this box, i.e. the CharacterTest camera)
        const string profPath = "Assets/Characters/CharacterTest_PostFX.asset";
        var prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profPath);
        if (!prof)
        {
            prof = ScriptableObject.CreateInstance<VolumeProfile>();
            var tm = prof.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
            var bl = prof.Add<Bloom>(true); bl.intensity.Override(0.25f); bl.threshold.Override(1.1f); bl.scatter.Override(0.6f);
            var ca = prof.Add<ColorAdjustments>(true); ca.postExposure.Override(0.25f); ca.contrast.Override(8f);
            AssetDatabase.CreateAsset(prof, profPath);
            foreach (var c in prof.components) AssetDatabase.AddObjectToAsset(c, prof);
            AssetDatabase.SaveAssets();
        }
        var volGo = new GameObject("PostFX Volume (local)");
        var vol = volGo.AddComponent<Volume>(); vol.isGlobal = false; vol.blendDistance = 0.5f; vol.sharedProfile = prof; vol.priority = 10;
        var box = volGo.AddComponent<BoxCollider>(); box.isTrigger = true; box.center = new Vector3(0, 1.5f, 1.5f); box.size = new Vector3(10, 4, 10);
        roots.Add(volGo);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground"; ground.transform.localScale = new Vector3(0.6f, 1, 0.4f);
        const string gmPath = "Assets/Characters/Ground_Test.mat";
        var gm = AssetDatabase.LoadAssetAtPath<Material>(gmPath);
        if (!gm)
        {
            gm = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Ground_Test" };
            gm.SetColor("_BaseColor", new Color(0.32f, 0.31f, 0.33f)); gm.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(gm, gmPath);
        }
        ground.GetComponent<Renderer>().sharedMaterial = gm;
        roots.Add(ground);
        return roots;
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
    }

    static void RenderCamera(Camera cam, StringBuilder log)
    {
        const int W = 1920, H = 1080;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        cam.targetTexture = rt;
        cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        RenderTexture.active = prev; cam.targetTexture = null;
        string outPath = Path.Combine(ModelsDir, "unity_test.png");
        File.WriteAllBytes(outPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex); rt.Release();
        log.AppendLine($"screenshot (scene camera, post-processing on) -> {outPath}");
    }

    static void Screenshot(List<string> models, StringBuilder log)
    {
        var ps = EditorSceneManager.NewPreviewScene();
        try
        {
            var roots = BuildContent(models, new StringBuilder());
            foreach (var r in roots) SceneManager.MoveGameObjectToScene(r, ps);
            var cam = roots.Select(r => r.GetComponent<Camera>()).First(c => c != null);
            cam.scene = ps;
            const int W = 1920, H = 1080;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
            RenderTexture.active = prev; cam.targetTexture = null;
            string outPath = Path.Combine(ModelsDir, "unity_test.png");
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex); rt.Release();
            log.AppendLine($"screenshot -> {outPath}");
        }
        finally { EditorSceneManager.ClosePreviewScene(ps); }
    }
}



