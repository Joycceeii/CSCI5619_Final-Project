// Builds the Wukong flight setup (Tools > Characters > Build WukongFlight):
//  - imports Animations/wukong_ride_idle.fbx + wukong_stand_idle.fbx as Humanoid clips (avatar copied from wukong_rigged), looping
//  - Animator controller (Stand <-> Ride on bool "Flying", IK pass on), cloud puff texture/material
//  - prefab Assets/Characters/Wukong/Flight/WukongFlight.prefab and scene Assets/Scenes/WukongFlight.unity (other open scenes untouched)
// Batch: Unity -batchmode -projectPath <WuzhuangCase> -executeMethod WukongFlightSetup.BatchBuildAndCapture
//  -> builds, plays the scene for a few seconds and saves _models/rigged/wukong_flight_unity.png, then quits.
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class WukongFlightSetup
{
    const string Dir = "Assets/Characters/Wukong";
    const string FlightDir = Dir + "/Flight";
    const string AnimDir = Dir + "/Animations";
    const string ScenePath = "Assets/Scenes/WukongFlight.unity";
    const string PrefabPath = FlightDir + "/WukongFlight.prefab";
    static string Models => Path.GetFullPath(Path.Combine(Application.dataPath, "../../_models/rigged"));
    static string LogPath => Path.Combine(Models, "unity_flight_log.txt");
    const string CaptureKey = "WukongFlight.capture";
    const string CaptureStartKey = "WukongFlight.captureStart";

    [System.Serializable] class Attach { public float[] delta_unity_rowmajor; public float[] left_wrist_unity; }

    static WukongFlightSetup()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.update += CaptureTick;
    }

    static void Log(StringBuilder sb, string s) { sb.AppendLine(s); }

    [MenuItem("Tools/Characters/Build WukongFlight")]
    public static void Build() => Build(false);

    static void Build(bool forBatch)
    {
        var log = new StringBuilder();
        try
        {
            AssetDatabase.Refresh();
            Directory.CreateDirectory(FlightDir);
            // ---- clips
            var rigged = $"{Dir}/wukong_rigged.fbx";
            var avatar = AssetDatabase.LoadAllAssetsAtPath(rigged).OfType<Avatar>().FirstOrDefault();
            Log(log, $"wukong avatar: {(avatar ? avatar.name : "MISSING")} human={avatar && avatar.isHuman} valid={avatar && avatar.isValid}");
            AnimationClip ImportClip(string name, bool loop = true)
            {
                string p = $"{AnimDir}/{name}.fbx";
                var mi = (ModelImporter)AssetImporter.GetAtPath(p);
                if (mi == null) { Log(log, $"missing {p}"); return null; }
                mi.animationType = ModelImporterAnimationType.Human;
                // own avatar built with the same human mapping as wukong_rigged (Chest<-Spine1 etc.); a copied avatar
                // refuses armature-only files ("transform hierarchy does not match")
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.sourceAvatar = null;
                mi.humanDescription = ((ModelImporter)AssetImporter.GetAtPath(rigged)).humanDescription;
                mi.importAnimation = true;
                mi.clipAnimations = new ModelImporterClipAnimation[0];
                mi.SaveAndReimport();                                   // default takes are only known after an import with these settings
                mi = (ModelImporter)AssetImporter.GetAtPath(p);
                var src = mi.defaultClipAnimations;
                if (src.Length == 0) { Log(log, $"{p}: no takes after reimport; importTakes={mi.importedTakeInfos.Length} avatar={mi.avatarSetup}"); }
                if (src.Length == 0) { Log(log, $"{p}: no takes"); return null; }
                var c = src[0];
                c.name = name; c.loopTime = loop; c.loopPose = false;
                c.lockRootRotation = true; c.lockRootHeightY = true; c.lockRootPositionXZ = true;
                c.keepOriginalOrientation = true; c.keepOriginalPositionY = true; c.keepOriginalPositionXZ = true;
                // humanoid clips drop non-human bones (the keyed tail!) unless the clip mask lists those transforms
                c.maskType = ClipAnimationMaskType.CopyFromOther; c.maskSource = AllTransformsMask(rigged);
                mi.clipAnimations = new[] { c };
                mi.SaveAndReimport();
                var clip = AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>().FirstOrDefault(a => !a.name.StartsWith("__preview__"));
                Log(log, $"clip {name}: {(clip ? $"{clip.length:F2}s humanMotion={clip.humanMotion} loop={clip.isLooping} tailCurves={AnimationUtility.GetCurveBindings(clip).Count(b => b.path.Contains("Tail_"))}" : "NOT IMPORTED")} (take '{src[0].takeName}', {src.Length} take(s))");
                return clip;
            }
            var stand = ImportClip("wukong_stand_idle");
            var ride = ImportClip("wukong_ride_idle");
            var walk = ImportClip("wukong_walk");
            var takeoff = ImportClip("wukong_takeoff", false);

            // ---- animator controller
            string ctrlPath = $"{FlightDir}/WukongFlight.controller";
            AssetDatabase.DeleteAsset(ctrlPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            ctrl.AddParameter("Flying", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Walking", AnimatorControllerParameterType.Bool);
            var sm = ctrl.layers[0].stateMachine;
            var sStand = sm.AddState("Stand"); sStand.motion = stand;
            var sWalk = sm.AddState("Walk"); sWalk.motion = walk;
            var sTake = sm.AddState("TakeOff"); sTake.motion = takeoff;
            var sRide = sm.AddState("Ride"); sRide.motion = ride;
            sm.defaultState = sRide;
            AnimatorStateTransition T(AnimatorState a, AnimatorState b, float dur) { var t = a.AddTransition(b); t.hasExitTime = false; t.duration = dur; return t; }
            T(sStand, sWalk, 0.25f).AddCondition(AnimatorConditionMode.If, 0, "Walking");
            var tw = T(sWalk, sStand, 0.25f); tw.AddCondition(AnimatorConditionMode.IfNot, 0, "Walking");
            T(sStand, sTake, 0.15f).AddCondition(AnimatorConditionMode.If, 0, "Flying");
            T(sWalk, sTake, 0.15f).AddCondition(AnimatorConditionMode.If, 0, "Flying");
            var tr = sTake.AddTransition(sRide); tr.hasExitTime = true; tr.exitTime = 0.97f; tr.duration = 0.05f;
            T(sTake, sStand, 0.3f).AddCondition(AnimatorConditionMode.IfNot, 0, "Flying");
            T(sRide, sStand, 0.35f).AddCondition(AnimatorConditionMode.IfNot, 0, "Flying");
            var layers = ctrl.layers; layers[0].iKPass = true; ctrl.layers = layers;
            EditorUtility.SetDirty(ctrl); AssetDatabase.SaveAssets();
            Log(log, $"controller {ctrlPath}: Stand/Ride, IK pass on");

            // ---- cloud puff texture + material
            string texPath = $"{FlightDir}/cloud_puff.png";
            if (!File.Exists(texPath)) { File.WriteAllBytes(texPath, MakePuff(128).EncodeToPNG()); AssetDatabase.ImportAsset(texPath); }
            var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
            ti.alphaIsTransparency = true; ti.alphaSource = TextureImporterAlphaSource.FromInput; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport();
            var puff = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            string matPath = $"{FlightDir}/CloudPuff.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (!mat) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, matPath); }
            mat.shader = sh;
            mat.SetTexture("_BaseMap", puff); mat.SetColor("_BaseColor", new Color(1f, 0.97f, 0.9f, 1f));
            mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", 0);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); mat.SetFloat("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(mat);

            // ---- prefab
            var attach = JsonUtility.FromJson<Attach>(File.ReadAllText(Path.Combine(Models, "wukong_ride_attach.json")));
            var D = new Matrix4x4();
            for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) D[r, c] = attach.delta_unity_rowmajor[r * 4 + c];
            var root = new GameObject("WukongFlight");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(rigged));
            model.transform.SetParent(root.transform, false);
            var anim = model.GetComponent<Animator>(); if (!anim) anim = model.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl; anim.avatar = avatar; anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            model.AddComponent<WukongFlightIK>();
            var fl = root.AddComponent<WukongFlight>();
            fl.animator = anim; fl.character = model.transform; fl.staffRideDelta = D;
            fl.leftWristRoot = new Vector3(attach.left_wrist_unity[0], attach.left_wrist_unity[1], attach.left_wrist_unity[2]);
            fl.cloudTrail = MakeTrail(model.transform, mat);
            fl.cloudSeat = MakeSeat(model.transform, mat);
            CharacterDynamics.Add(model, "wukong", fl);                       // tail: wag + springs, straightens out in flight
            Directory.CreateDirectory(FlightDir);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Log(log, $"prefab {PrefabPath} saved");

            // ---- scene (built additively; the scene the user has open is never modified)
            var active = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, forBatch ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                inst.transform.position = Vector3.zero;
                var cam = new GameObject("Flight Camera").AddComponent<Camera>();
                cam.tag = "MainCamera"; cam.fieldOfView = 45; cam.nearClipPlane = 0.05f; cam.farClipPlane = 300;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.58f, 0.74f, 0.92f);
                var cd = cam.GetUniversalAdditionalCameraData(); cd.renderPostProcessing = true; cd.renderShadows = true;
                cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; cd.antialiasingQuality = AntialiasingQuality.High;   // SMAA on top of MSAA 4x
                var fc = cam.gameObject.AddComponent<FlightCamera>(); fc.target = inst.GetComponent<WukongFlight>().character;
                cam.transform.position = new Vector3(2.5f, 2.8f, 2.5f); cam.transform.LookAt(new Vector3(0, 2.3f, 0));
                var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.5f;
                sun.color = new Color(1f, 0.93f, 0.82f); sun.shadows = LightShadows.Soft; sun.shadowNormalBias = 0.3f; sun.shadowBias = 0.03f; sun.transform.rotation = Quaternion.Euler(42, -140, 0);
                var fill = new GameObject("Sky Fill").AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = 0.45f;
                fill.color = new Color(0.7f, 0.82f, 1f); fill.shadows = LightShadows.None; fill.transform.rotation = Quaternion.Euler(25, 60, 0);
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.88f); RenderSettings.ambientEquatorColor = new Color(0.55f, 0.58f, 0.6f);
                RenderSettings.ambientGroundColor = new Color(0.32f, 0.33f, 0.3f);
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = new Color(0.62f, 0.76f, 0.92f);
                RenderSettings.fogStartDistance = 15; RenderSettings.fogEndDistance = 80;
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.name = "Ground"; ground.transform.localScale = new Vector3(8, 1, 8);
                var gm = AssetDatabase.LoadAssetAtPath<Material>($"{FlightDir}/FlightGround.mat");
                if (!gm) { gm = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(gm, $"{FlightDir}/FlightGround.mat"); }
                gm.SetColor("_BaseColor", new Color(0.42f, 0.5f, 0.36f)); gm.SetFloat("_Smoothness", 0.1f);
                ground.GetComponent<Renderer>().sharedMaterial = gm;
                var prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Characters/CharacterTest_PostFX.asset");
                if (prof) { var v = new GameObject("PostFX (global)").AddComponent<Volume>(); v.isGlobal = true; v.sharedProfile = prof; }
                Directory.CreateDirectory("Assets/Scenes");
                EditorSceneManager.SaveScene(scene, ScenePath);
                Log(log, $"scene {ScenePath} saved");
            }
            finally
            {
                if (active.IsValid() && active != scene) SceneManager.SetActiveScene(active);
                if (!forBatch) EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.SaveAssets();
        }
        catch (System.Exception e) { Log(log, "ERROR " + e); }
        Directory.CreateDirectory(Models);
        File.WriteAllText(LogPath, log.ToString());
        Debug.Log("[WukongFlightSetup]\n" + log);
    }

    static AvatarMask AllTransformsMask(string modelPath)
    {
        const string p = FlightDir + "/WukongAllTransforms.mask";
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(p);
        if (!mask) { mask = new AvatarMask(); Directory.CreateDirectory(FlightDir); AssetDatabase.CreateAsset(mask, p); }
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath).transform;
        var all = root.GetComponentsInChildren<Transform>(true).Where(t => t != root).ToArray();
        mask.transformCount = all.Length;
        for (int i = 0; i < all.Length; i++) { mask.SetTransformPath(i, AnimationUtility.CalculateTransformPath(all[i], root)); mask.SetTransformActive(i, true); }
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, true);
        EditorUtility.SetDirty(mask); AssetDatabase.SaveAssets();
        return mask;
    }

    static Texture2D MakePuff(int n)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var rnd = new System.Random(7);
        // a few overlapping soft blobs -> cauliflower cloud puff
        var blobs = Enumerable.Range(0, 7).Select(i => (x: 0.5f + (float)(rnd.NextDouble() - 0.5) * 0.35f, y: 0.5f + (float)(rnd.NextDouble() - 0.5) * 0.3f,
                                                         r: 0.18f + (float)rnd.NextDouble() * 0.12f)).ToArray();
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n, a = 0, shade = 0;
                foreach (var b in blobs)
                {
                    float d = Mathf.Sqrt((u - b.x) * (u - b.x) + (v - b.y) * (v - b.y)) / b.r;
                    float k = Mathf.Clamp01(1 - d); k = k * k * (3 - 2 * k);
                    a = 1 - (1 - a) * (1 - k); shade = Mathf.Max(shade, k * (0.75f + 0.25f * (v - b.y + b.r) / (2 * b.r)));
                }
                float edge = Mathf.Clamp01(1 - Mathf.Max(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f)) * 2); // keep away from the border
                a *= Mathf.Clamp01(edge * 4);
                float c = 0.82f + 0.18f * shade;
                t.SetPixel(x, y, new Color(c, c * 0.98f, c * 0.93f, a));
            }
        t.Apply(); return t;
    }

    static ParticleSystemRenderer SetupRenderer(ParticleSystem ps, Material mat)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sortMode = ParticleSystemSortMode.Distance; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        return r;
    }

    static ParticleSystem MakeTrail(Transform parent, Material mat)
    {
        var go = new GameObject("CloudTrail (xiangyun)"); go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0, 0.08f, -0.5f);      // behind/below the seat (character root space)
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main; m.duration = 5; m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.4f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f); m.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        m.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2); m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.9f), new Color(1f, 0.93f, 0.78f, 0.9f));
        m.maxParticles = 600; m.gravityModifier = -0.02f; m.playOnAwake = true;
        var e = ps.emission; e.rateOverTime = 6; e.rateOverDistance = 9;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.12f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.98f, 0.92f), 0), new GradientColorKey(new Color(1f, 0.9f, 0.72f), 0.5f), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.9f, 0.12f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0, 1) });
        col.color = g;
        var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 0.6f, 1, 1.8f));
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.15f; noise.frequency = 0.6f;
        SetupRenderer(ps, mat);
        ps.Play();
        return ps;
    }

    static ParticleSystem MakeSeat(Transform parent, Material mat)
    {
        var go = new GameObject("CloudSeat"); go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0, 0.06f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main; m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.6f); m.startSpeed = 0.05f;
        m.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.38f); m.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        m.simulationSpace = ParticleSystemSimulationSpace.Local; m.maxParticles = 120;
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.85f), new Color(1f, 0.95f, 0.85f, 0.85f));
        var e = ps.emission; e.rateOverTime = 26;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(0.35f, 0.06f, 0.8f);
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.85f, 0.25f), new GradientAlphaKey(0, 1) });
        col.color = g;
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.08f; noise.frequency = 1f;
        SetupRenderer(ps, mat);
        ps.Play();
        return ps;
    }

    // ------------------------------------------------------------------ batch: build, play, capture, quit
    public static void BatchBuildAndCapture()
    {
        Build(true);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(CaptureKey, true);
        SessionState.SetFloat(CaptureStartKey, -1);
        EditorApplication.EnterPlaymode();
    }

    static void OnPlayMode(PlayModeStateChange s)
    {
        if (!SessionState.GetBool(CaptureKey, false)) return;
        if (s == PlayModeStateChange.EnteredPlayMode) SessionState.SetFloat(CaptureStartKey, (float)EditorApplication.timeSinceStartup);
        if (s == PlayModeStateChange.EnteredEditMode) { SessionState.EraseBool(CaptureKey); EditorApplication.Exit(0); }
    }

    static void CaptureTick()
    {
        if (!SessionState.GetBool(CaptureKey, false) || !EditorApplication.isPlaying) return;
        float t0 = SessionState.GetFloat(CaptureStartKey, -1);
        if (t0 < 0 || Time.time < 7f) return;
        var sb = new StringBuilder(File.Exists(LogPath) ? File.ReadAllText(LogPath) : "");
        try
        {
            var cam = Camera.main;
            const int W = 1920, H = 1080;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            cam.targetTexture = rt; cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
            RenderTexture.active = prev; cam.targetTexture = null;
            string outPath = Path.Combine(Models, "wukong_flight_unity.png");
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            var fl = Object.FindAnyObjectByType<WukongFlight>();
            var an = fl ? fl.animator : null;
            var st = an ? an.GetCurrentAnimatorStateInfo(0) : default;
            var staff = fl ? fl.character.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == "wukong_staff") : null;
            sb.AppendLine($"play t={Time.time:F1}s frames={Time.frameCount}: state Ride={st.IsName("Ride")} t={st.normalizedTime:F2}; " +
                          $"character at {fl?.character.position.ToString("F2")}; staff parent={staff?.parent?.name}; " +
                          $"trail particles={fl?.cloudTrail?.particleCount} seat={fl?.cloudSeat?.particleCount}");
            sb.AppendLine($"flight screenshot -> {outPath}");
        }
        catch (System.Exception e) { sb.AppendLine("CAPTURE ERROR " + e); }
        File.WriteAllText(LogPath, sb.ToString());
        SessionState.SetFloat(CaptureStartKey, -1);
        EditorApplication.ExitPlaymode();
    }
}
