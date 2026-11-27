using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WhoseStory.EditorTools
{
    // Blender 做的木偶角色（源文件在 ArtSource/CH_*，导出到 Assets/WhoseStory/Models/CH_*.fbx）。
    // FBX 不在就返回 false，CharacterModels 会退回代码拼的版本。
    // 模型是一串刚性零件的层级：Head（脖子轴心）、Mouth、Eye_L/R、Arm_L/R，匹诺曹另有 Nose（鼻根轴心，沿本地 -Y 伸出）。
    // 颜色：CH_*_colors.txt 每行 "材质名 r g b"（sRGB），由 Blender 导出脚本写出；匹诺曹用 PinocchioModel 里的表。
    public static class DollModel
    {
        public const string ModelFolder = "Assets/WhoseStory/Models/";
        const string MaterialFolder = "Assets/WhoseStory/Generated/Materials";
        const float OutlineWidth = 5f;

        // 材质名里带这些词的零件太小或太细，描边会糊成一团
        static readonly string[] NoOutlineKeys = { "Pupil", "Line", "Blush", "String", "Button", "Lips", "Bead", "Shadow" };

        public static bool TryBuild(string asset, Transform model, Dictionary<string, Color32> fallbackColors,
            bool hasNose, out CharacterModels.Result result)
        {
            result = default;
            string fbx = ModelFolder + asset + ".fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (prefab == null)
            {
                return false;
            }

            Dictionary<string, Color32> colors = ReadColors(ModelFolder + asset + "_colors.txt") ?? fallbackColors;

            // FBX 里角色朝 -Z，转 180 度和 Model 的 +Z 朝向对齐
            var wrapper = new GameObject(asset);
            wrapper.transform.SetParent(model, false);
            wrapper.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, wrapper.transform);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                // 投影上色的网格有三个子网格（正/侧/背三张设定图），每个都换
                Material[] sources = renderer.sharedMaterials;
                var toon = new Material[sources.Length];
                for (int i = 0; i < sources.Length; i++)
                {
                    toon[i] = ToonMaterial(sources[i] != null ? sources[i].name : "", colors);
                }
                renderer.sharedMaterials = toon;
            }

            Transform Find(string name)
            {
                // 头会被挪到 HeadPivot 下面，所以在整个 Model 里找
                foreach (var t in model.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == name) return t;
                }
                Debug.LogWarning($"[WhoseStory] {fbx} has no '{name}'");
                return null;
            }

            // FBX 节点的本地轴是 Blender 的（Z 向上）。CharacterPuppet 假定 Y 向上、Z 朝前，
            // 所以头、嘴、眼睛外面各套一个和 Model 对齐的轴心物体
            Transform Pivot(Transform target, Transform parent, string name)
            {
                var pivot = new GameObject(name).transform;
                pivot.SetParent(parent, false);
                pivot.position = target.position;
                pivot.rotation = model.rotation;
                target.SetParent(pivot, true);
                return pivot;
            }

            Transform head = Find("Head");
            Transform headPivot = Pivot(head, model, "HeadPivot");
            Transform mouth = Pivot(Find("Mouth"), head, "MouthPivot");
            Transform eyeL = Pivot(Find("Eye_L"), head, "EyePivot_L");
            Transform eyeR = Pivot(Find("Eye_R"), head, "EyePivot_R");

            Transform nosePivot = null;
            if (hasNose)
            {
                // 鼻子沿 Nose 节点的本地 -Y 伸出去；套一个本地 Z 指向鼻尖的轴心，NoseGrower 默认的 Z 轴就能用
                Transform nose = Find("Nose");
                nosePivot = new GameObject("NosePivot").transform;
                nosePivot.SetParent(head, false);
                nosePivot.position = nose.position;
                nosePivot.rotation = Quaternion.LookRotation(-nose.up, model.up);
                nose.SetParent(nosePivot, true);
            }

            result = new CharacterModels.Result
            {
                head = headPivot,
                mouth = mouth,
                nose = nosePivot,
                eyes = new[] { eyeL, eyeR },
            };
            return true;
        }

        // 静态摆件：放一个 Blender 导出的道具，材质换成 Toon。FBX 不在就返回 null，调用方用程序拼的版本。
        // 道具在 Blender 里朝 -Y，进来以后朝 -Z；rotation 里自己转
        public static GameObject InstantiateProp(string asset, Transform parent, Vector3 localPosition, Quaternion localRotation,
            float scale = 1f)
        {
            string fbx = ModelFolder + asset + ".fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (prefab == null)
            {
                return null;
            }
            Dictionary<string, Color32> colors = ReadColors(ModelFolder + asset + "_colors.txt");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            // FBX 根节点自带一个 -90/90 的轴向旋转，套一层空物体再摆，旋转参数就是直观的世界朝向
            var holder = new GameObject(asset).transform;
            holder.SetParent(parent, false);
            holder.localPosition = localPosition;
            holder.localRotation = localRotation;
            holder.localScale = Vector3.one * scale;
            instance.transform.SetParent(holder, false);
            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                // 投影上色的网格有三个子网格（正/侧/背三张设定图），每个都换
                Material[] sources = renderer.sharedMaterials;
                var toon = new Material[sources.Length];
                for (int i = 0; i < sources.Length; i++)
                {
                    toon[i] = ToonMaterial(sources[i] != null ? sources[i].name : "", colors);
                }
                renderer.sharedMaterials = toon;
            }
            return holder.gameObject;
        }

        static Dictionary<string, Color32> ReadColors(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }
            var colors = new Dictionary<string, Color32>();
            foreach (string line in File.ReadAllLines(path))
            {
                string[] p = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (p.Length == 4)
                {
                    colors[p[0]] = new Color32(byte.Parse(p[1]), byte.Parse(p[2]), byte.Parse(p[3]), 255);
                }
            }
            return colors;
        }

        static Material ToonMaterial(string name, Dictionary<string, Color32> colors)
        {
            bool outline = true;
            foreach (string k in NoOutlineKeys)
            {
                if (name.Contains(k)) outline = false;
            }
            string assetName = name + (outline ? "_FbxOl" : "_Fbx");
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/" + assetName + ".mat");
            if (existing != null)
            {
                // 模型改过颜色就跟着改，材质资源保留（场景里的引用不断）
                if (colors != null && colors.TryGetValue(name, out Color32 known) && existing.HasProperty("_1st_ShadeColor")
                    && AssetDatabase.LoadAssetAtPath<Texture2D>(ModelFolder + name + ".png") == null)
                {
                    Color k = known;
                    existing.SetColor("_BaseColor", k);
                    Color shade = new Color(k.r * 0.88f, k.g * 0.85f, Mathf.Min(1f, k.b * 0.9f + 0.02f));
                    existing.SetColor("_1st_ShadeColor", shade);
                    existing.SetColor("_2nd_ShadeColor", shade);
                    EditorUtility.SetDirty(existing);
                }
                return existing;
            }

            Color color = colors != null && colors.TryGetValue(name, out Color32 c) ? (Color)c : Color.magenta;
            if (name.Contains("Clear"))
            {
                // 玻璃（罐子、灯罩）：半透明的 URP Lit，不描边，能看见里面的东西
                string glassPath = MaterialFolder + "/" + name + "_Fbx.mat";
                var glass = AssetDatabase.LoadAssetAtPath<Material>(glassPath);
                if (glass == null)
                {
                    glass = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name + "_Fbx" };
                    glass.SetFloat("_Surface", 1f);
                    glass.SetFloat("_Blend", 0f);
                    glass.SetOverrideTag("RenderType", "Transparent");
                    glass.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    glass.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    glass.SetInt("_ZWrite", 0);
                    glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    glass.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    glass.SetColor("_BaseColor", new Color(color.r, color.g, color.b, 0.22f));
                    glass.SetFloat("_Smoothness", 0.9f);
                    AssetDatabase.CreateAsset(glass, glassPath);
                }
                return glass;
            }
            Material baseMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/" + name + "_Fbx.mat");
            if (baseMat == null)
            {
                baseMat = CharacterModels.Mat(name + "_Fbx", color, 0.3f);
                // Models/<材质名>.png 存在就当贴图用（比如镜子玻璃上画好的裂纹和鬼脸），亮面暗面都铺
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ModelFolder + name + ".png");
                if (tex != null && baseMat.HasProperty("_MainTex"))
                {
                    baseMat.SetTexture("_MainTex", tex);
                    baseMat.SetTexture("_1st_ShadeMap", tex);
                    baseMat.SetTexture("_2nd_ShadeMap", tex);
                    baseMat.SetTextureScale("_MainTex", Vector2.one);
                    EditorUtility.SetDirty(baseMat);
                }
            }
            if (!outline || !baseMat.HasProperty("_Outline_Width"))
            {
                return baseMat;
            }

            var mat = new Material(baseMat) { name = assetName };
            mat.SetFloat("_Outline_Width", OutlineWidth);
            AssetDatabase.CreateAsset(mat, MaterialFolder + "/" + assetName + ".mat");
            return mat;
        }

        // 不重建整个场景，只把当前打开场景里三个角色的 Model 换成 Blender 模型
        [MenuItem("WhoseStory/Swap Characters To Blender Models")]
        public static void SwapInOpenScene()
        {
            Swap("SnowWhite", CharacterModels.BuildSnowWhite);
            Swap("Queen", CharacterModels.BuildQueen);
            Swap("Pinocchio", CharacterModels.BuildPinocchio);
            AssetDatabase.SaveAssets();
        }

        static void Swap(string rootName, System.Func<Transform, CharacterModels.Result> build)
        {
            GameObject root = GameObject.Find(rootName);
            if (root == null)
            {
                Debug.LogError($"[WhoseStory] no '{rootName}' in the open scene");
                return;
            }
            Transform model = root.transform.Find("Model");
            if (model.GetComponentInChildren<SpriteRenderer>() != null)
            {
                Debug.LogWarning($"[WhoseStory] {rootName} is a 2D standee, left alone");
                return;
            }
            for (int i = model.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(model.GetChild(i).gameObject);
            }

            CharacterModels.Result parts = build(model);
            CharacterPuppet puppet = root.GetComponent<CharacterPuppet>();
            if (puppet != null)
            {
                puppet.body = model;
                puppet.head = parts.head;
                puppet.mouth = parts.mouth;
                puppet.eyes = parts.eyes;
            }
            NoseGrower grower = root.GetComponent<NoseGrower>();
            if (grower != null && parts.nose != null)
            {
                grower.nose = parts.nose;
                grower.growAxis = Vector3.forward;
                grower.blendShapeMesh = null;
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[WhoseStory] {rootName} model rebuilt");
        }
    }

    public static class PinocchioModel
    {
        // 匹诺曹的模型早于颜色文件，颜色放在这里（sRGB，取自设定图）
        static readonly Dictionary<string, Color32> Colors = new()
        {
            { "PinoWood", new Color32(214, 162, 105, 255) },
            { "PinoNose", new Color32(206, 138, 82, 255) },
            { "PinoJoint", new Color32(96, 56, 36, 255) },
            { "PinoVest", new Color32(168, 56, 40, 255) },
            { "PinoCollar", new Color32(78, 44, 30, 255) },
            { "PinoShorts", new Color32(62, 102, 142, 255) },
            { "PinoSuspender", new Color32(44, 54, 74, 255) },
            { "PinoButton", new Color32(204, 154, 62, 255) },
            { "PinoHat", new Color32(72, 108, 56, 255) },
            { "PinoHatBand", new Color32(92, 60, 40, 255) },
            { "PinoFeather", new Color32(236, 186, 62, 255) },
            { "PinoShoe", new Color32(122, 70, 42, 255) },
            { "PinoSole", new Color32(66, 38, 24, 255) },
            { "PinoEyeWhite", new Color32(246, 242, 232, 255) },
            { "PinoPupil", new Color32(18, 14, 14, 255) },
            { "PinoLine", new Color32(42, 26, 20, 255) },
            { "PinoBlush", new Color32(220, 128, 100, 255) },
            { "PinoString", new Color32(214, 186, 128, 255) },
        };

        public static bool TryBuild(Transform model, out CharacterModels.Result result)
        {
            return DollModel.TryBuild("CH_Pinocchio", model, Colors, true, out result);
        }
    }
}
