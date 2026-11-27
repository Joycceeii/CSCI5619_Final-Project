// Adds voice + lip sync + idle + interrogation tells to a character instance (CharacterTest). Used by CharacterTestSetup.
//   wukong : CharacterVoice (T = test line), uLipSync, SuspectTells tail_up  (idle comes from CharacterDynamics)
//   bajie  : idle (bajie_idle), CharacterVoice (G = line), uLipSync, SuspectTells ear_flap   (U = tell)
//   shaseng: idle (shaseng_idle), CharacterVoice (H = line), uLipSync, SuspectTells grip_freeze (I = tell)
// The test question box (bottom of the Game view) is shown once (on Wukong) and asks all three.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public static class CharacterVoiceSetup
{
    public const string TestLine = "Assets/Audio/Test/wukong_line_01.wav";
    static string Line(string key) => $"Assets/Audio/Test/{key}_line_01.wav";

    static readonly Dictionary<string, (string tell, string name, string[] words)> Tells = new Dictionary<string, (string, string, string[])>
    {
        ["wukong"] = ("tail_up", "悟空", new[] { "人参果", "果子", "偷", "后园", "园子", "树", "推倒", "镇元", "清风", "明月", "那天晚上", "昨晚",
                                         "ginseng", "fruit", "steal", "stole", "stolen", "took", "taken", "striker", "tree", "pushed", "knocked", "orchard", "garden", "last night", "that night", "second watch", "zhenyuan", "qingfeng", "mingyue" }),
        ["bajie"] = ("ear_flap", "八戒", new[] { "人参果", "吃", "偷吃", "厨房", "肚子饿", "昨晚", "果子", "嘴",
                                         "ginseng", "fruit", "eat", "ate", "eaten", "taste", "kitchen", "hungry", "stomach", "swallow", "last night", "that night" }),
        ["shaseng"] = ("grip_freeze", "沙僧", new[] { "行李", "看守", "钥匙", "后园", "大师兄", "守夜", "包袱", "那天晚上",
                                         "luggage", "baggage", "guard", "watch", "keys", "garden", "orchard", "elder brother", "wukong", "that night", "last night", "fruit", "ginseng" }),
    };

    public static void Add(GameObject model, string key)
    {
        if (!Tells.ContainsKey(key)) return;
        var smr = model.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(r => r.sharedMesh && r.sharedMesh.GetBlendShapeIndex("A") >= 0);
        if (!smr) { Debug.LogWarning("[CharacterVoiceSetup] no A/I/U/E/O blendshapes on " + model.name); return; }
        var anim = model.GetComponent<Animator>();

        // ---- idle (Bajie / Shaseng): humanoid clip with the ears etc. in the clip mask, simple one-state controller
        if (key != "wukong" && anim)
        {
            var clip = ImportIdle(key);
            if (clip)
            {
                string cp = $"Assets/Characters/{Folder(key)}/{key}_Idle.controller";
                AssetDatabase.DeleteAsset(cp);
                var ctrl = AnimatorController.CreateAnimatorControllerAtPath(cp);
                ctrl.layers[0].stateMachine.AddState("Idle").motion = clip;
                anim.runtimeAnimatorController = ctrl; anim.applyRootMotion = false;
            }
        }

        // ---- voice + lip sync
        var src = model.GetComponent<AudioSource>(); if (!src) src = model.AddComponent<AudioSource>();   // (no ?? : Unity fake-null)
        src.playOnAwake = false; src.spatialBlend = 0.6f;
        var v = model.GetComponent<CharacterVoice>(); if (!v) v = model.AddComponent<CharacterVoice>();
        v.face = smr;
        v.maxWeight = key == "wukong" ? 55f : 100f;   // Wukong: smaller, rounder mouth opening (full A/O looked like a gaping hole)
        var t = model.GetComponent<VoiceTestKey>(); if (!t) t = model.AddComponent<VoiceTestKey>();
        t.voice = v; t.line = AssetDatabase.LoadAssetAtPath<AudioClip>(key == "wukong" ? TestLine : Line(key));
#if ENABLE_INPUT_SYSTEM
        t.key = key == "wukong" ? Key.T : key == "bajie" ? Key.G : Key.H;
#endif
        var profile = AssetDatabase.LoadAssetAtPath<uLipSync.Profile>(LipSyncProfileBuilder.ProfileFor(key));
        if (profile)
        {
            var ls = model.GetComponent<uLipSync.uLipSync>(); if (!ls) ls = model.AddComponent<uLipSync.uLipSync>();
            ls.profile = profile;
            var br = model.GetComponent<ULipSyncVoice>(); if (!br) br = model.AddComponent<ULipSyncVoice>();
            br.lipSync = ls;
            EditorUtility.SetDirty(ls); EditorUtility.SetDirty(br);
        }
        else Debug.LogWarning($"[CharacterVoiceSetup] no uLipSync profile for {key} - built-in analyzer");

        // ---- interrogation tells
        var (tell, display, words) = Tells[key];
        var st = model.GetComponent<SuspectTells>(); if (!st) st = model.AddComponent<SuspectTells>();
        st.defaultTell = tell; st.displayName = display; st.sensitiveWords = words.ToList();
        st.tail = model.GetComponent<TailCurve>(); st.animator = anim;
        st.ears = model.GetComponents<ChainFollow>();
        st.showTestBox = key == "wukong";
#if ENABLE_INPUT_SYSTEM
        st.testKey = key == "wukong" ? Key.Y : key == "bajie" ? Key.U : Key.I;
#endif
        EditorUtility.SetDirty(v); EditorUtility.SetDirty(t); EditorUtility.SetDirty(st);
    }

    /// <summary>After all three are placed: the box on Wukong also asks the other two.</summary>
    public static void LinkAskBox(IEnumerable<GameObject> models)
    {
        var all = models.Select(m => m.GetComponent<SuspectTells>()).Where(s => s).ToArray();
        var host = all.FirstOrDefault(s => s.showTestBox);
        if (host) { host.alsoAsk = all.Where(s => s != host).ToArray(); EditorUtility.SetDirty(host); }
    }

    static string Folder(string key) => char.ToUpper(key[0]) + key.Substring(1);

    static AnimationClip ImportIdle(string key)
    {
        string p = $"Assets/Characters/{Folder(key)}/Animations/{key}_idle.fbx";
        string rigged = $"Assets/Characters/{Folder(key)}/{key}_rigged.fbx";
        var mi = AssetImporter.GetAtPath(p) as ModelImporter;
        if (mi == null) { Debug.LogWarning("[CharacterVoiceSetup] missing " + p); return null; }
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.humanDescription = ((ModelImporter)AssetImporter.GetAtPath(rigged)).humanDescription;
        mi.importAnimation = true; mi.clipAnimations = new ModelImporterClipAnimation[0];
        mi.SaveAndReimport();
        mi = (ModelImporter)AssetImporter.GetAtPath(p);
        var src = mi.defaultClipAnimations; if (src.Length == 0) return null;
        var c = src[0]; c.name = $"{key}_idle"; c.loopTime = true;
        c.lockRootRotation = true; c.lockRootHeightY = true; c.lockRootPositionXZ = true;
        c.keepOriginalOrientation = true; c.keepOriginalPositionY = true; c.keepOriginalPositionXZ = true;
        c.maskType = ClipAnimationMaskType.CopyFromOther; c.maskSource = AllTransforms(key, rigged);   // keep non-human bones (ears)
        mi.clipAnimations = new[] { c };
        mi.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>().FirstOrDefault(a => !a.name.StartsWith("__preview__"));
    }

    static AvatarMask AllTransforms(string key, string modelPath)
    {
        string p = $"Assets/Characters/{Folder(key)}/Animations/{key}_AllTransforms.mask";
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(p);
        if (!mask) { mask = new AvatarMask(); AssetDatabase.CreateAsset(mask, p); }
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath).transform;
        var all = root.GetComponentsInChildren<Transform>(true).Where(x => x != root).ToArray();
        mask.transformCount = all.Length;
        for (int i = 0; i < all.Length; i++) { mask.SetTransformPath(i, AnimationUtility.CalculateTransformPath(all[i], root)); mask.SetTransformActive(i, true); }
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, true);
        EditorUtility.SetDirty(mask); AssetDatabase.SaveAssets();
        return mask;
    }
}
