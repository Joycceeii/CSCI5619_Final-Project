// Builds Assets/Scenes/WuzhuangGarden.unity from the World Labs Atlas export (see _models/courtyard_bake.py, courtyard_mesh25d.py):
//   Courtyard (root, uniform scale = metres)
//     Splat (pure)    GaussianSplatRenderer  StreamingAssets/Courtyard/courtyard_full.spz   (cleaned, levelled, Unity axes)
//     Splat (hybrid)  GaussianSplatRenderer  StreamingAssets/Courtyard/courtyard_hybrid.spz (minus the splats lying on the photo layer)
//     Photo 2.5D      unlit mesh: the original 2048 image displaced by its f00 depth (hall, corridor, tree; floor snapped flat)
//     Paving          tiled stone paving rectified from the same photo
//   Wukong / Bajie / Shaseng in an interrogation line-up facing the player camera, slight depth of field, SMAA.
// Only touches: this scene, Assets/Garden/**, Assets/Settings/Garden_Renderer.asset (new) and one extra entry in
// PC_RPAsset's renderer list (the default renderer, index 0, is unchanged -> every other scene renders exactly as before).
// Tools > Garden > Build WuzhuangGarden   |   batch: -executeMethod GardenSetup.BatchBuild
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GaussianSplatting.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class GardenSetup
{
    public const string ScenePath = "Assets/Scenes/WuzhuangGarden.unity";
    const string RendererPath = "Assets/Settings/Garden_Renderer.asset";
    const string RPAssetPath = "Assets/Settings/PC_RPAsset.asset";
    const string Gen = "Assets/Garden/Generated";
    public const float CharScale = 1.4f;          // the rigs are ~1.1 m tall; 1.4 makes them read as adults next to the doors
    public const float LineupDistance = 4.3f, LineupSpacing = 1.35f, EyeHeight = 1.5f;
    public static string AtlasDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../_models/courtyard_atlas"));

    [System.Serializable] public class Cam { public string name; public float[] pos, fwd, up; public float vfov; }
    [System.Serializable] public class Meta { public float scale; public Cam[] cameras; }
    public static Meta LoadMeta() => JsonUtility.FromJson<Meta>(File.ReadAllText(Path.Combine(AtlasDir, "garden_meta.json")));
    public static Vector3 V(float[] a) => new Vector3(a[0], a[1], a[2]);
    public static Cam F00(Meta m) => m.cameras.First(c => c.name.Contains("f00"));

    public static void BatchBuild() { var log = Build(); Debug.Log(log); EditorApplication.Exit(log.Contains("ERROR") ? 1 : 0); }

    [MenuItem("Tools/Garden/Build WuzhuangGarden")]
    public static string Build()
    {
        var log = new StringBuilder();
        try
        {
            AssetDatabase.Refresh();
            int rendererIndex = EnsureRenderer(log);
            var meta = LoadMeta(); float s = meta.scale;
            var f00 = F00(meta);
            log.AppendLine($"meta: scale {s:F3}, f00 pos {V(f00.pos) * s:F2}, vfov {f00.vfov:F1}");

            var active = SceneManager.GetActiveScene();
            bool batch = Application.isBatchMode;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, batch ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                BuildContent(meta, rendererIndex, log);
                Directory.CreateDirectory("Assets/Scenes");
                EditorSceneManager.SaveScene(scene, ScenePath);
                log.AppendLine("saved " + ScenePath);
            }
            finally
            {
                if (!batch)
                {
                    if (active.IsValid()) SceneManager.SetActiveScene(active);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
        catch (System.Exception e) { log.AppendLine("ERROR " + e); }
        File.WriteAllText(Path.Combine(AtlasDir, "garden_build_log.txt"), log.ToString());
        return log.ToString();
    }

    // ---------------------------------------------------------------- renderer with the splat feature
    static int EnsureRenderer(StringBuilder log)
    {
        var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (!rd)
        {
            AssetDatabase.CopyAsset("Assets/Settings/PC_Renderer.asset", RendererPath);
            rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            log.AppendLine("created " + RendererPath + " (copy of PC_Renderer)");
        }
        var featType = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("GaussianSplatting.Runtime.GaussianSplatURPFeature")).FirstOrDefault(t => t != null);
        if (featType == null) throw new System.Exception("GaussianSplatURPFeature type not found (GS_ENABLE_URP?)");
        if (!rd.rendererFeatures.Any(f => f && f.GetType() == featType))
        {
            var f = (ScriptableRendererFeature)ScriptableObject.CreateInstance(featType);
            f.name = "GaussianSplats";
            AssetDatabase.AddObjectToAsset(f, rd);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(f, out string _, out long lid);
            var so = new SerializedObject(rd);
            var feats = so.FindProperty("m_RendererFeatures"); var map = so.FindProperty("m_RendererFeatureMap");
            feats.arraySize++; feats.GetArrayElementAtIndex(feats.arraySize - 1).objectReferenceValue = f;
            map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = lid;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rd);
            log.AppendLine("added GaussianSplatURPFeature to Garden_Renderer");
        }
        log.AppendLine("Garden_Renderer features: " + string.Join(", ", rd.rendererFeatures.Where(x => x).Select(x => x.GetType().Name)));

        var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(RPAssetPath);
        var rso = new SerializedObject(rp);
        var list = rso.FindProperty("m_RendererDataList");
        int idx = -1;
        for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == rd) idx = i;
        if (idx < 0)
        {
            list.arraySize++; idx = list.arraySize - 1;
            list.GetArrayElementAtIndex(idx).objectReferenceValue = rd;
            rso.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(rp);
            log.AppendLine($"PC_RPAsset: Garden_Renderer added as renderer #{idx} (default stays #{rso.FindProperty("m_DefaultRendererIndex").intValue})");
        }
        AssetDatabase.SaveAssets();
        return idx;
    }

    // ---------------------------------------------------------------- scene content
    static void BuildContent(Meta meta, int rendererIndex, StringBuilder log)
    {
        float s = meta.scale; var f00 = F00(meta);
        var root = new GameObject("Courtyard (Atlas)");
        root.transform.localScale = Vector3.one * s;

        GameObject Splat(string name, string file)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform, false);
            var r = go.AddComponent<GaussianSplatRenderer>();
            r.m_SourcePath = "Courtyard/" + file; r.m_SHOrder = 3; r.m_SplatScale = 1f; r.m_OpacityScale = 1f;
            return go;
        }
        var full = Splat("Splat (pure)", "courtyard_full.spz");
        var hyb = Splat("Splat (hybrid)", "courtyard_hybrid.spz");
        var opts = new GameObject("Splat Rendering Options").AddComponent<GaussianSplatRenderingOptions>();
        opts.m_Transparency = TransparencyMode.SortedBlended;   // exact back-to-front blending (best quality, deterministic)

        // ---- 2.5D photo layer
        var photoTex = ImportTex($"{Gen}/courtyard_f00_clean.png", TextureWrapMode.Clamp, 8192);   // 2048 original or 8192 Real-ESRGAN
        var mesh = BuildMesh(log);
        var photoMat = UnlitMat("Garden_Photo25D", photoTex, Color.white, Vector2.one);
        // feathered edges: alpha-blended, but still writing depth (layer occludes itself / splats / DOF correctly)
        photoMat.SetFloat("_Surface", 1); photoMat.SetFloat("_Blend", 0); photoMat.SetFloat("_ZWrite", 1);
        photoMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); photoMat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        photoMat.SetOverrideTag("RenderType", "Transparent"); photoMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        photoMat.renderQueue = (int)RenderQueue.Transparent - 20;
        var photo = new GameObject("Photo 2.5D (f00)"); photo.transform.SetParent(root.transform, false);
        photo.AddComponent<MeshFilter>().sharedMesh = mesh;
        var pr = photo.AddComponent<MeshRenderer>(); pr.sharedMaterial = photoMat; pr.shadowCastingMode = ShadowCastingMode.Off; pr.receiveShadows = false;

        // ---- paving (under/around the photo floor)
        var paveTex = ImportTex($"{Gen}/paving_tile.png", TextureWrapMode.Repeat, 2048);
        const float paveSize = 60f, tileMetres = 4.5f;   // Jimeng paving: ~45 cm stones
        var paveMat = UnlitMat("Garden_Paving", paveTex, new Color(1f, 1f, 1f), Vector2.one * (paveSize / tileMetres));
        var pave = GameObject.CreatePrimitive(PrimitiveType.Quad); pave.name = "Paving (tiled from photo)";
        Object.DestroyImmediate(pave.GetComponent<Collider>());
        pave.transform.position = new Vector3(V(f00.pos).x * s, -0.012f, V(f00.pos).z * s);
        pave.transform.rotation = Quaternion.Euler(90, 0, 0); pave.transform.localScale = Vector3.one * paveSize;
        paveMat.shader = Shader.Find("Garden/UnlitShadowReceiver"); paveMat.SetFloat("_ShadowStrength", 0.62f); paveMat.SetTextureScale("_BaseMap", Vector2.one * (paveSize / tileMetres)); EditorUtility.SetDirty(paveMat);
        var pvr = pave.GetComponent<MeshRenderer>(); pvr.sharedMaterial = paveMat; pvr.shadowCastingMode = ShadowCastingMode.Off; pvr.receiveShadows = true;

        var far = Splat("Splat (far background only)", "courtyard_far.spz");
        var rebuilt = BuildRebuilt(log);

        var view = root.AddComponent<GardenView>();
        view.splatFull = full; view.splatHybrid = hyb; view.splatFar = far;
        view.hybridOnly = new[] { photo }; view.pavingObjects = new[] { pave }; view.rebuiltOnly = new[] { rebuilt };
        view.mode = GardenView.Mode.Rebuilt; view.showFarSplat = false; view.Apply();
        SkyAndMountains(rebuilt.transform, log);

        // ---- player camera: f00 position, standing eye height, horizontal view toward the line-up
        Vector3 camPos = V(f00.pos) * s; camPos.y = EyeHeight;
        Vector3 fh = Vector3.ProjectOnPlane(V(f00.fwd), Vector3.up).normalized;
        var cam = new GameObject("Player Camera").AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.transform.position = camPos;
        Vector3 lineup = camPos + fh * LineupDistance; lineup.y = 0;
        cam.transform.rotation = Quaternion.LookRotation((lineup + Vector3.up * 0.85f) - camPos, Vector3.up);
        cam.fieldOfView = f00.vfov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 400f;
        cam.clearFlags = CameraClearFlags.Skybox; cam.backgroundColor = new Color(0.74f, 0.84f, 0.94f);   // clean panoramic sky (RenderSettings.skybox)
        cam.allowMSAA = false;   // splats need MSAA off
        cam.allowHDR = true;
        var cd = cam.GetUniversalAdditionalCameraData();
        cd.SetRenderer(rendererIndex);
        cd.renderPostProcessing = true; cd.renderShadows = true;
        cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; cd.antialiasingQuality = AntialiasingQuality.High;
        cam.gameObject.AddComponent<AudioListener>();
        log.AppendLine($"player camera {camPos:F2} looking {fh:F2}, line-up centre {lineup:F2}");

        // ---- lights (photo: low warm sun behind the tree, back-right)
        Vector3 right = Vector3.Cross(Vector3.up, fh).normalized;
        var sun = new GameObject("Sun (warm, back-right)").AddComponent<Light>();
        sun.type = LightType.Directional; sun.color = new Color(1f, 0.86f, 0.66f); sun.intensity = 1.5f;
        sun.transform.rotation = Quaternion.LookRotation(SunForward(fh, right));   // from screen-right/front: the ginseng tree throws its shadow across the floor onto the south wall
        sun.shadows = LightShadows.Soft; sun.shadowStrength = 1f;
        var fill = new GameObject("Fill (sky bounce, from camera)").AddComponent<Light>();
        fill.type = LightType.Directional; fill.color = new Color(0.86f, 0.9f, 1f); fill.intensity = 1.15f;
        fill.transform.rotation = Quaternion.LookRotation((fh + right * 0.35f + Vector3.down * 0.45f).normalized); fill.shadows = LightShadows.None;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.95f, 0.97f, 1.05f);
        RenderSettings.ambientEquatorColor = new Color(0.85f, 0.78f, 0.68f);
        RenderSettings.ambientGroundColor = new Color(0.42f, 0.38f, 0.33f);
        RenderSettings.ambientIntensity = 1f;

        // ---- characters: Wukong (screen-left), Bajie, Shaseng (screen-right), facing the camera
        var chars = new[] { ("Wukong", "wukong"), ("Bajie", "bajie"), ("Shaseng", "shaseng") };
        var blob = BlobMaterial();
        var placed = new List<GameObject>();
        var lineRoot = new GameObject("Suspects (line-up)");
        for (int i = 0; i < chars.Length; i++)
        {
            var (folder, key) = chars[i];
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Characters/{folder}/{key}_rigged.fbx");
            if (!asset) { log.AppendLine("missing " + key); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.transform.SetParent(lineRoot.transform, true);
            Vector3 p = lineup + right * ((i - 1) * LineupSpacing) + fh * (i == 1 ? 0.25f : 0f);   // Bajie half a step back
            go.transform.position = p;
            Vector3 toCam = Vector3.ProjectOnPlane(camPos - p, Vector3.up).normalized;
            go.transform.rotation = Quaternion.LookRotation(toCam, Vector3.up);
            go.transform.localScale = Vector3.one * CharScale;
            CharacterDynamics.Add(go, key);
            CharacterVoiceSetup.Add(go, key);
            if (false && key == "wukong")   // GripTilt tried (v20): in Play the staff ended up vertical and off the hand -> disabled, back to the v19 pose
            {
                // staff leans out ~12 deg and forward ~16 deg from the hanging hand (no finger bones: the fist cannot be closed further)
                var grip = go.GetComponent<GripTilt>(); if (!grip) grip = go.AddComponent<GripTilt>();
                grip.hand = go.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.RightHand); grip.character = go.transform;
            }
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>()) { smr.updateWhenOffscreen = true; smr.shadowCastingMode = ShadowCastingMode.Off; }   // the ginseng tree is the only shadow caster
            // soft contact shadow (the photo floor is unlit and does not receive real shadows)
            var sh = GameObject.CreatePrimitive(PrimitiveType.Quad); sh.name = "Contact Shadow";
            Object.DestroyImmediate(sh.GetComponent<Collider>());
            sh.transform.SetParent(go.transform, false);
            sh.transform.localPosition = new Vector3(0, 0.015f, 0); sh.transform.localRotation = Quaternion.Euler(90, 0, 0);
            sh.transform.localScale = new Vector3(0.8f, 0.65f, 1f);
            var shr = sh.GetComponent<MeshRenderer>(); shr.sharedMaterial = blob; shr.shadowCastingMode = ShadowCastingMode.Off;
            placed.Add(go);
            log.AppendLine($"placed {key} at {p:F2}, scale {CharScale}");
        }
        CharacterVoiceSetup.LinkAskBox(placed);

        // ---- post: slight depth of field behind the suspects (scene-local global volume)
        const string profPath = "Assets/Garden/Garden_PostFX.asset";
        AssetDatabase.DeleteAsset(profPath);
        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        var dof = prof.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Gaussian);
        dof.gaussianStart.Override(LineupDistance + 6f); dof.gaussianEnd.Override(LineupDistance + 30f);   // very light: only the far background softens
        dof.gaussianMaxRadius.Override(0.5f); dof.highQualitySampling.Override(true);
        var tm = prof.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.None);   // keep the photo's colours
        AssetDatabase.CreateAsset(prof, profPath);
        foreach (var c in prof.components) AssetDatabase.AddObjectToAsset(c, prof);
        AssetDatabase.SaveAssets();
        var vol = new GameObject("PostFX (DOF)").AddComponent<Volume>(); vol.isGlobal = true; vol.sharedProfile = prof; vol.priority = 5;
    }

    // ---------------------------------------------------------------- courtyard rebuilt from the Jimeng pieces
    // Layout in world metres, measured from the Atlas splat / f00 depth: hall facade x=-8.7 (centre z=-2.8), east corridor x=7.25,
    // south wall (with gate) z=-10.6, north wall z=9; ginseng tree trunk (-2.4,-3.4), peach tree (-4.5,3.6).
    const float X0 = -8.7f, X1 = 7.25f, Z0 = -10.6f, Z1 = 9.0f, HallZ = -2.8f;
    [System.Serializable] class JmSize { public float width, height; }
    public static Vector3 SunForward(Vector3 fh, Vector3 right) => (-right * 0.75f + fh * 0.2f + Vector3.down * 0.85f).normalized;   // ~47 deg high: the canopy shadow lands on the floor beside the tree
    static GameObject BuildRebuilt(StringBuilder log)
    {
        var root = new GameObject("Courtyard (Jimeng rebuild)");
        var sizes = File.ReadAllText($"{Gen}/jm_sizes.json");
        JmSize Size(string key)
        {
            int i = sizes.IndexOf($"\"{key}\""); var sub = sizes.Substring(i, sizes.IndexOf('}', i) - i + 1);
            return JsonUtility.FromJson<JmSize>(sub.Substring(sub.IndexOf('{')));
        }
        Material Cut(string key, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            string tp_ = File.Exists($"{Gen}/jm_{key}.png") ? $"{Gen}/jm_{key}.png" : $"{Gen}/{key}.png";
            var tex = ImportTex(tp_, wrap, 4096);
            var m = UnlitMat("Garden_JM_" + key, tex, Color.white, Vector2.one);
            if (tex && ((TextureImporter)AssetImporter.GetAtPath(tp_)).DoesSourceTextureHaveAlpha())
            {
                m.SetFloat("_AlphaClip", 1); m.SetFloat("_Cutoff", 0.45f); m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            EditorUtility.SetDirty(m); return m;
        }
        GameObject Quad(string name, Vector3 centre, Vector3 outward, float w, float h, Material m)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = name;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(root.transform, false);
            q.transform.position = centre; q.transform.rotation = Quaternion.LookRotation(outward, Vector3.up);
            q.transform.localScale = new Vector3(w, h, 1);
            var r = q.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return q;
        }
        GameObject Box(string name, Vector3 centre, Vector3 size, Material m)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube); b.name = name;
            Object.DestroyImmediate(b.GetComponent<Collider>());
            b.transform.SetParent(root.transform, false); b.transform.position = centre; b.transform.localScale = size;
            var r = b.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off;
            return b;
        }
        // plain wall run from a to b (on the ground), built from whole tiles of the plain segment
        void PlainRun(string name, Vector3 a, Vector3 b, Vector3 outward, Material m, JmSize sz)
        {
            float len = Vector3.Distance(a, b); if (len < 0.05f) return;
            int n = Mathf.Max(1, Mathf.RoundToInt(len / sz.width)); float w = len / n;
            for (int i = 0; i < n; i++)
            {
                var c = Vector3.Lerp(a, b, (i + 0.5f) / n); c.y = sz.height / 2;
                Quad($"{name} {i + 1}", c, outward, w + 0.002f, sz.height, m);
            }
        }
        var deco = File.ReadAllText($"{Gen}/deco_sizes.json");
        JmSize DSize(string key)
        {
            int i = deco.IndexOf($"\"{key}\""); var sub = deco.Substring(i, deco.IndexOf('}', i) - i + 1);
            return JsonUtility.FromJson<JmSize>(sub.Substring(sub.IndexOf('{')));
        }
        var wallTex = ImportTex($"{Gen}/jm_wall_d.png", TextureWrapMode.Repeat, 4096);
        var wallM = UnlitMat("Garden_JM_wall_d", wallTex, Color.white, Vector2.one);
        wallM.shader = Shader.Find("Garden/UnlitShadowReceiver"); wallM.SetFloat("_ShadowStrength", 0.55f); EditorUtility.SetDirty(wallM);
        var wallSz = DSize("wall_d");
        var bodyM = UnlitMat("Garden_JM_body", null, new Color(0.80f, 0.78f, 0.74f), Vector2.one);
        var roofTex = ImportTex($"{Gen}/jm_roof_tiles.png", TextureWrapMode.Repeat, 2048);

        // ---- hall (west): facade card + body + roof slopes behind
        var hs = Size("hall");
        Quad("Hall facade (Jimeng)", new Vector3(X0, hs.height / 2, HallZ), Vector3.left, hs.width, hs.height, Cut("hall"));
        Box("Hall body", new Vector3(X0 - 4.05f, 1.8f, HallZ), new Vector3(8f, 3.6f, hs.width - 0.4f), bodyM);
        Roof("Hall roof", root, new Vector3(X0 - 0.05f, 3.5f, HallZ), Vector3.right, hs.width - 0.2f, 4f, 1.9f, roofTex);   // outward = toward the courtyard: slopes rise west, over the hall body
        PlainRun("West wall N", new Vector3(X0, 0, HallZ + hs.width / 2), new Vector3(X0, 0, Z1), Vector3.left, wallM, wallSz);
        PlainRun("West wall S", new Vector3(X0, 0, Z0), new Vector3(X0, 0, HallZ - hs.width / 2), Vector3.left, wallM, wallSz);

        // ---- south wall with the gate in the middle
        var gs = DSize("gate_core"); float gateX = -0.5f;
        Quad("South wall gate (Jimeng)", new Vector3(gateX, gs.height / 2, Z0 + 0.04f), Vector3.back, gs.width, gs.height, Cut("gate_core"));
        // one continuous wall (same cap height everywhere); the gate building sits in front of it, so no seam in the tile cap
        PlainRun("South wall", new Vector3(X0, 0, Z0), new Vector3(X1, 0, Z0), Vector3.back, wallM, wallSz);

        // ---- east corridor: two corridor cards, body and a lean-to roof
        var cs = Size("corridor"); var corM = Cut("corridor");
        float runZ = Z1 - Z0; int nc = Mathf.Max(1, Mathf.RoundToInt(runZ / cs.width)); float cw = runZ / nc;
        for (int i = 0; i < nc; i++)
            Quad($"East corridor (Jimeng) {i + 1}", new Vector3(X1, cs.height / 2, Z0 + (i + 0.5f) * cw), Vector3.right, cw + 0.002f, cs.height, corM);
        Box("Corridor body", new Vector3(X1 + 1.55f, 1.6f, (Z0 + Z1) / 2), new Vector3(3f, 3.2f, runZ), bodyM);
        Roof("Corridor roof", root, new Vector3(X1 + 0.05f, cs.height - 0.2f, (Z0 + Z1) / 2), Vector3.left, runZ, 3.2f, 1.1f, roofTex, oneSided: true);   // lean-to rises east, over the corridor body (was poking into the courtyard)

        // ---- north wall (behind the player)
        PlainRun("North wall", new Vector3(X1, 0, Z1), new Vector3(X0, 0, Z1), Vector3.forward, wallM, wallSz);

        // ---- trees: one card each, facing the player camera
        var meta = LoadMeta(); var f00 = F00(meta); Vector3 camPos = V(f00.pos) * meta.scale; camPos.y = 0;
        void Tree(string key, Vector3 at)
        {
            var sz = Size(key); var m = Cut(key);
            Vector3 toCam = (camPos - at); toCam.y = 0; toCam.Normalize();
            var card = Quad($"{key} card A", at + Vector3.up * sz.height / 2, -toCam, sz.width, sz.height, m);
            card.AddComponent<YBillboard>();   // turns about Y to face the camera: never seen edge-on when walking around
            // (a crossed second card smeared when seen edge-on from the player position -> one card facing the player)
        }
        Vector3 ginseng = new Vector3(-2.4f, 0, -3.4f);
        Tree("ginseng_tree", ginseng);
        Tree("peach_tree", new Vector3(-4.5f, 0, 3.6f));

        // ---- the one shadow in the courtyard: the ginseng tree, cast by an invisible card turned square to the sun
        Vector3 fhz = Vector3.ProjectOnPlane(V(f00.fwd), Vector3.up).normalized, rgt = Vector3.Cross(Vector3.up, fhz).normalized;
        Vector3 sunH = Vector3.ProjectOnPlane(SunForward(fhz, rgt), Vector3.up).normalized;
        {
            var sz = Size("ginseng_tree");
            const string sp = "Assets/Garden/Garden_TreeShadowCaster.mat";
            var sm = AssetDatabase.LoadAssetAtPath<Material>(sp);
            if (!sm) { sm = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(sm, sp); }
            sm.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Gen}/jm_ginseng_tree.png"));
            sm.SetFloat("_AlphaClip", 1); sm.SetFloat("_Cutoff", 0.45f); sm.EnableKeyword("_ALPHATEST_ON"); sm.SetFloat("_Cull", 0);
            EditorUtility.SetDirty(sm);
            var caster = Quad("ginseng_tree shadow caster (invisible)", ginseng + Vector3.up * sz.height / 2, sunH, sz.width, sz.height, sm);
            var cr = caster.GetComponent<MeshRenderer>(); cr.shadowCastingMode = ShadowCastingMode.ShadowsOnly; cr.receiveShadows = false;
        }

        // ---- decorations (Jimeng sheets A/B/C): wall pieces sit on their wall, free-standing pieces are cards turned to the player
        var blob = BlobMaterial();
        GameObject Card(string key, Vector3 at, float heightScale = 1f, bool mirror = false, Vector3? facing = null, float lift = 0f)
        {
            var sz = DSize(key); var m = Cut("deco_" + key);
            Vector3 toCam = camPos - at; toCam.y = 0; toCam.Normalize();
            Vector3 outward = facing ?? -toCam;
            float h = sz.height * heightScale, w = sz.width * heightScale;
            var q = Quad($"deco {key}", at + Vector3.up * (h / 2 + lift), outward, mirror ? -w : w, h, m);
            if (lift <= 0f)
            {
                var sh = GameObject.CreatePrimitive(PrimitiveType.Quad); sh.name = "Contact Shadow"; Object.DestroyImmediate(sh.GetComponent<Collider>());
                sh.transform.SetParent(root.transform, false); sh.transform.position = at + Vector3.up * 0.02f; sh.transform.rotation = Quaternion.Euler(90, 0, 0);
                sh.transform.localScale = new Vector3(w * 0.9f, Mathf.Max(0.35f, w * 0.45f), 1);
                var r = sh.GetComponent<MeshRenderer>(); r.sharedMaterial = blob; r.shadowCastingMode = ShadowCastingMode.Off;
            }
            return q;
        }
        GameObject OnWall(string key, Vector3 at, Vector3 outward, float heightScale = 1f)
        {
            var sz = DSize(key); var m = Cut("deco_" + key);
            return Quad($"deco {key}", at, outward, sz.width * heightScale, sz.height * heightScale, m);
        }
        // hall: plaque under the eave, stone lanterns at the steps, incense burner on the axis
        OnWall("plaque", new Vector3(X0 + 0.06f, 2.85f, HallZ), Vector3.left, 0.78f);
        Card("stone_lantern_a", new Vector3(X0 + 1.4f, 0, HallZ - 2.5f));
        Card("stone_lantern_c", new Vector3(X0 + 1.4f, 0, HallZ + 2.5f));
        Card("incense_burner", new Vector3(X0 + 3.2f, 0, HallZ), 0.85f);
        IncenseSmoke(root.transform, new Vector3(X0 + 3.2f, 0.64f, HallZ));
        // gate: couplets (upper line on the visitor's right = -x when facing the gate), lanterns, lions
        float gw = gs.width / 2;
        OnWall("couplet_right", new Vector3(gateX - gw - 0.32f, 1.45f, Z0 + 0.03f), Vector3.back, 0.87f);
        OnWall("couplet_left", new Vector3(gateX + gw + 0.32f, 1.45f, Z0 + 0.03f), Vector3.back, 0.87f);
        Card("lantern_red", new Vector3(gateX - gw - 0.95f, 0, Z0 + 0.35f), 1f, false, null, 2.05f);
        Card("lantern_red", new Vector3(gateX + gw + 0.95f, 0, Z0 + 0.35f), 1f, false, null, 2.05f);
        Card("stone_lion", new Vector3(gateX - gw - 1.7f, 0, Z0 + 0.9f), 1f, false, Vector3.back);
        Card("stone_lion", new Vector3(gateX + gw + 1.7f, 0, Z0 + 0.9f), 1f, true, Vector3.back);
        // corridor lanterns, one every bay
        for (int i = 0; i < 6; i++)
            Card("lantern_red", new Vector3(X1 - 0.5f, 0, Z0 + 1.7f + i * (runZ - 3.4f) / 5f), 1f, false, null, 2.45f);
        // leak windows on the plain walls (alternating ice-crack / begonia)
        OnWall("window_ice", new Vector3(-5.6f, 1.6f, Z0 + 0.02f), Vector3.back, 0.9f);
        OnWall("window_flower", new Vector3(4.6f, 1.6f, Z0 + 0.02f), Vector3.back, 0.9f);
        OnWall("window_flower", new Vector3(-5.4f, 1.6f, Z1 - 0.02f), Vector3.forward, 0.9f);
        OnWall("window_ice", new Vector3(-1.2f, 1.6f, Z1 - 0.02f), Vector3.forward, 0.9f);
        OnWall("window_flower", new Vector3(4.6f, 1.6f, Z1 - 0.02f), Vector3.forward, 0.9f);
        OnWall("window_ice", new Vector3(X0 + 0.02f, 1.6f, 6.6f), Vector3.left, 0.9f);
        // stone table + drum stools under the ginseng tree (in the gap between Bajie and Shaseng, behind them)
        Card("table_set", new Vector3(-4.6f, 0, -2.2f));
        // corners and the corridor front: bamboo, water vat, bonsai, shelf
        Card("bamboo", new Vector3(-7.6f, 0, 8.0f));
        Card("bamboo", new Vector3(6.1f, 0, 8.1f), 1f, true);
        Card("bamboo", new Vector3(-7.7f, 0, -9.6f), 0.9f);
        Card("water_vat", new Vector3(5.9f, 0, -9.3f));
        Card("bonsai_blue", new Vector3(6.0f, 0, -3.2f));
        Card("bonsai_pair_low", new Vector3(5.9f, 0, 5.4f));
        Card("bonsai_celadon", new Vector3(-7.5f, 0, 1.6f));
        Card("bonsai_pair_top", new Vector3(-7.8f, 0, -8.3f));
        Card("shelf", new Vector3(6.4f, 0, 2.2f), 1f, false, Vector3.right);
        log.AppendLine("decorations: plaque, 2 couplets, 8 red lanterns, 2 stone lions, 6 leak windows, 2 stone lanterns, incense burner, stone table set, 3 bamboo, water vat, 4 bonsai, shelf");
        log.AppendLine($"Jimeng rebuild: hall {hs.width}x{hs.height} @ x={X0}, gate wall {gs.width}x{gs.height} @ z={Z0}, corridor {nc}x{cw:F2} m @ x={X1}, walls {wallSz.width}x{wallSz.height} tiles");
        return root;
    }

    // clean sky (equirect panorama, procedural: _models/sky_mountains.py) + a ring of ink-wash distant mountains beyond the walls.
    // Replaces the blurry Atlas splat that used to show above the walls in Rebuilt mode. No shadows from either.
    static void SkyAndMountains(Transform parent, System.Text.StringBuilder log)
    {
        const string skyTex = "Assets/Garden/Generated/garden_sky_pano.png", mtTex = "Assets/Garden/Generated/garden_mountains.png";
        AssetDatabase.ImportAsset(skyTex); AssetDatabase.ImportAsset(mtTex);
        var si = (TextureImporter)AssetImporter.GetAtPath(skyTex);
        si.mipmapEnabled = false; si.wrapModeU = TextureWrapMode.Repeat; si.wrapModeV = TextureWrapMode.Clamp; si.maxTextureSize = 4096;
        si.textureCompression = TextureImporterCompression.CompressedHQ; si.SaveAndReimport();
        var mi = (TextureImporter)AssetImporter.GetAtPath(mtTex);
        mi.alphaIsTransparency = true; mi.wrapModeU = TextureWrapMode.Repeat; mi.wrapModeV = TextureWrapMode.Clamp; mi.maxTextureSize = 4096;
        mi.textureCompression = TextureImporterCompression.CompressedHQ; mi.SaveAndReimport();

        const string skyMatPath = "Assets/Garden/Garden_Sky.mat";
        var sky = AssetDatabase.LoadAssetAtPath<Material>(skyMatPath);
        if (!sky) { sky = new Material(Shader.Find("Skybox/Panoramic")); AssetDatabase.CreateAsset(sky, skyMatPath); }
        sky.shader = Shader.Find("Skybox/Panoramic");
        sky.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(skyTex));
        sky.SetFloat("_Mapping", 1); sky.SetFloat("_ImageType", 0); sky.SetFloat("_Exposure", 1f); sky.SetFloat("_Rotation", 0);
        sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
        EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky;

        const string mtMatPath = "Assets/Garden/Garden_Mountains.mat";
        var mm = AssetDatabase.LoadAssetAtPath<Material>(mtMatPath);
        if (!mm) { mm = new Material(Shader.Find("Garden/DistantMountains")); AssetDatabase.CreateAsset(mm, mtMatPath); }
        mm.shader = Shader.Find("Garden/DistantMountains");
        mm.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(mtTex)); mm.SetColor("_Tint", Color.white);
        EditorUtility.SetDirty(mm);

        // open cylinder, normals inward, texture tiled 3x around
        const int N = 128; const float R = 40f, Y0 = 0.5f, Hgt = 18f, Tiles = 3f;
        var verts = new Vector3[(N + 1) * 2]; var uvs = new Vector2[verts.Length]; var tris = new int[N * 6];
        for (int i = 0; i <= N; i++)
        {
            float a = i / (float)N * Mathf.PI * 2; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * R;
            verts[i * 2] = d + Vector3.up * Y0; verts[i * 2 + 1] = d + Vector3.up * (Y0 + Hgt);
            uvs[i * 2] = new Vector2(i / (float)N * Tiles, 0); uvs[i * 2 + 1] = new Vector2(i / (float)N * Tiles, 1);
            if (i < N) { int k = i * 6, b = i * 2; tris[k] = b; tris[k + 1] = b + 1; tris[k + 2] = b + 2; tris[k + 3] = b + 1; tris[k + 4] = b + 3; tris[k + 5] = b + 2; }
        }
        const string meshPath = "Assets/Garden/Generated/garden_mountain_ring.asset";
        var mesh = new Mesh { name = "garden_mountain_ring", vertices = verts, uv = uvs, triangles = tris };
        mesh.RecalculateBounds();
        AssetDatabase.DeleteAsset(meshPath); AssetDatabase.CreateAsset(mesh, meshPath);
        var go = new GameObject("Distant mountains (ring)");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3((X0 + X1) / 2, 0, (Z0 + Z1) / 2);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mm;
        mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
        log.AppendLine($"sky: panoramic {skyTex}; mountains ring r={R} m, y {Y0}..{Y0 + Hgt} m, {Tiles}x tiled; far Atlas splat hidden in Rebuilt");
    }

    // thin, slow, translucent incense smoke rising from the burner (replaces the painted smoke of the Jimeng picture)
    static void IncenseSmoke(Transform parent, Vector3 at)
    {
        const string tp = "Assets/Garden/Generated/smoke_puff.png";
        if (!File.Exists(tp))
        {
            const int N = 128; var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var rnd = new System.Random(7);
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2 - 1, dy = (y + 0.5f) / N * 2 - 1; float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1 - r); a = a * a * (3 - 2 * a);
                    float n = Mathf.PerlinNoise(x * 0.06f, y * 0.06f) * 0.6f + 0.4f;
                    t.SetPixel(x, y, new Color(1, 1, 1, a * n));
                }
            File.WriteAllBytes(tp, t.EncodeToPNG()); Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(tp);
            var ti = (TextureImporter)AssetImporter.GetAtPath(tp); ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport();
        }
        const string mp = "Assets/Garden/Garden_IncenseSmoke.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, mp); }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tp)); m.SetColor("_BaseColor", new Color(0.86f, 0.87f, 0.9f, 1f));
        m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_ZWrite", 0);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetOverrideTag("RenderType", "Transparent"); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);

        var go = new GameObject("Incense smoke (particles)"); go.transform.SetParent(parent, false); go.transform.position = at;
        go.transform.rotation = Quaternion.Euler(-90, 0, 0);   // cone points up
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.loop = true; main.prewarm = true; main.maxParticles = 60;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.18f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        main.startColor = new Color(0.9f, 0.9f, 0.92f, 1f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 5;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 5; sh.radius = 0.04f;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 3.2f));
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.22f, 0.2f), new GradientAlphaKey(0.12f, 0.6f), new GradientAlphaKey(0, 1) });
        col.color = g;
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.06f; noise.frequency = 0.5f; noise.scrollSpeed = 0.2f;
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0.02f, 0.05f); vel.y = new ParticleSystem.MinMaxCurve(0f, 0f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var pr = go.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = m; pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.shadowCastingMode = ShadowCastingMode.Off; pr.receiveShadows = false;
        ps.Play();
    }

    // a gabled roof: front slope rising from the eave line (at 'eave', going 'inward' = -outward) by 'rise' over 'run', and the back slope down again
    static void Roof(string name, GameObject parent, Vector3 eave, Vector3 outward, float length, float run, float rise, Texture tex, bool oneSided = false)
    {
        var m = UnlitMat("Garden_JM_roof_" + (oneSided ? "corr" : "hall"), tex, new Color(0.9f, 0.9f, 0.9f), new Vector2(length / 3f, Mathf.Sqrt(run * run + rise * rise) / 3f));
        Vector3 inward = -outward; Vector3 along = Vector3.Cross(Vector3.up, outward).normalized;
        void Slope(string n, Vector3 lowEdgeCentre, Vector3 dirUp)
        {
            float slant = Mathf.Sqrt(run * run + rise * rise);
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = n; Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent.transform, false);
            Vector3 upDir = (dirUp * run + Vector3.up * rise).normalized;
            q.transform.position = lowEdgeCentre + upDir * slant / 2;
            Vector3 normal = Vector3.Cross(along, upDir).normalized; if (normal.y < 0) normal = -normal;
            q.transform.rotation = Quaternion.LookRotation(-normal, upDir);
            q.transform.localScale = new Vector3(length, slant, 1);
            var r = q.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off;
        }
        Slope(name + " front", eave, inward);
        if (!oneSided) Slope(name + " back", eave + inward * (2 * run), outward);
    }

    // ---------------------------------------------------------------- assets
    static Texture2D ImportTex(string path, TextureWrapMode wrap, int max)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true; ti.mipmapEnabled = true;
        ti.wrapMode = wrap; ti.anisoLevel = 16; ti.filterMode = FilterMode.Trilinear;
        ti.maxTextureSize = max;
        ti.mipMapBias = -0.6f;   // keep the receding floor crisp
        ti.textureCompression = max > 4096 ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;   // BC7 for the 8192 photo
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material UnlitMat(string name, Texture tex, Color tint, Vector2 tiling)
    {
        string p = $"Assets/Garden/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name }; AssetDatabase.CreateAsset(m, p); }
        m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", tint); m.SetTextureScale("_BaseMap", tiling);
        m.SetFloat("_Cull", 0f); m.doubleSidedGI = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material BlobMaterial()
    {
        string tp = "Assets/Garden/Generated/contact_shadow.png";
        if (true)   // regenerate (v2: darker core)
        {
            const int N = 128; var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2 - 1, dy = (y + 0.5f) / N * 2 - 1; float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1 - r); a = a * a * (3 - 2 * a);
                    float core = Mathf.Clamp01(1 - r / 0.45f); core = core * core;
                    t.SetPixel(x, y, new Color(0, 0, 0, Mathf.Clamp01(a * 0.5f + core * 0.35f)));
                }
            File.WriteAllBytes(tp, t.EncodeToPNG()); Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(tp);
        }
        var ti = (TextureImporter)AssetImporter.GetAtPath(tp); ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport();
        string p = "Assets/Garden/Garden_ContactShadow.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, p); }
        m.shader = Shader.Find("Garden/ContactShadow");   // own multiply-blend blob shader (URP/Unlit transparent blobs did not show)
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tp)); m.SetFloat("_Strength", 1.1f);
        m.renderQueue = (int)RenderQueue.Geometry + 60;   // drawn in the opaque pass right after the floor (the transparent pass did not show it)
        EditorUtility.SetDirty(m); return m;
        m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_ZWrite", 0); m.SetFloat("_Cull", 0);
        m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = (int)RenderQueue.Transparent - 10;
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        EditorUtility.SetDirty(m);
        return m;
    }

    static Mesh BuildMesh(StringBuilder log)
    {
        var bytes = File.ReadAllBytes(Path.GetFullPath($"{Gen}/garden25d.bytes"));
        int nv = System.BitConverter.ToInt32(bytes, 0), nt = System.BitConverter.ToInt32(bytes, 4);
        int o = 8;
        var v = new Vector3[nv]; var uv = new Vector2[nv]; var tri = new int[nt * 3];
        for (int i = 0; i < nv; i++, o += 12) v[i] = new Vector3(System.BitConverter.ToSingle(bytes, o), System.BitConverter.ToSingle(bytes, o + 4), System.BitConverter.ToSingle(bytes, o + 8));
        for (int i = 0; i < nv; i++, o += 8) uv[i] = new Vector2(System.BitConverter.ToSingle(bytes, o), System.BitConverter.ToSingle(bytes, o + 4));
        System.Buffer.BlockCopy(bytes, o, tri, 0, nt * 12);
        string p = $"{Gen}/garden25d_mesh.asset";
        AssetDatabase.DeleteAsset(p);
        var mesh = new Mesh { name = "garden25d", indexFormat = IndexFormat.UInt32 };
        mesh.vertices = v; mesh.uv = uv; mesh.triangles = tri;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, p);
        log.AppendLine($"2.5D mesh: {nv} verts, {nt} tris, bounds {mesh.bounds.size:F1}");
        return mesh;
    }
}













