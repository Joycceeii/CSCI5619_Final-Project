using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WhoseStory;

namespace WhoseStory.EditorTools
{
    // 一次性把审讯场景搭出来：设置资产、管理物体、三个角色，引用全部接好。
    // 重复执行会覆盖同名场景，所以场景里手工调过的位置会丢，改完布局就别再点。
    public static class BuildInterrogationScene
    {
        const string Folder = "Assets/WhoseStory";
        const string SettingsPath = Folder + "/OpenAiSettings.asset";
        const string ScenePath = Folder + "/Interrogation.unity";

        [MenuItem("Whose Story/Build Interrogation Scene")]
        public static void Build()
        {
            OpenAiSettings settings = AssetDatabase.LoadAssetAtPath<OpenAiSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<OpenAiSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
                AssetDatabase.SaveAssets();
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single);

            Camera camera = Object.FindFirstObjectByType<Camera>();
            XrRigBuilder.Setup(camera);

            CharacterModels.PrepareFolders();
            RoomBuilder.Build();

            var manager = new GameObject("Interrogation");
            OpenAiClient client = manager.AddComponent<OpenAiClient>();
            client.settings = settings;
            MicrophoneCapture microphone = manager.AddComponent<MicrophoneCapture>();
            manager.AddComponent<TranscriptLogger>();
            InterrogationManager interrogation = manager.AddComponent<InterrogationManager>();

            InterrogationCharacter snow = MakeCharacter(
                "SnowWhite", "Snow White", "snow_white.txt", "coral",
                new Vector3(-1.5f, 0f, 3f), 1.6f, CharacterModels.BuildSnowWhite);
            snow.voicePitch = 1.18f; // coral 原声偏低，升调到小女孩的音高
            InterrogationCharacter queen = MakeCharacter(
                "Queen", "Queen", "queen.txt", "sage",
                new Vector3(0f, 0f, 3.4f), 1.75f, CharacterModels.BuildQueen);
            InterrogationCharacter pinocchio = MakeCharacter(
                "Pinocchio", "Pinocchio", "pinocchio.txt", "ballad",
                new Vector3(1.5f, 0f, 3f), 1.25f, CharacterModels.BuildPinocchio);

            interrogation.client = client;
            interrogation.microphone = microphone;
            interrogation.playerHead = camera.transform;
            interrogation.characters = new[] { snow, queen, pinocchio };

            EditorSceneManager.MarkSceneDirty(scene);
            Directory.CreateDirectory(Folder);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[WhoseStory] scene built at {ScenePath}. Press Play and hold a controller trigger (or Space in the editor) to talk.");
        }

        static InterrogationCharacter MakeCharacter(string objectName, string displayName,
            string promptFile, string voice, Vector3 position, float height,
            System.Func<Transform, CharacterModels.Result> buildModel)
        {
            // 根物体放在胸口高度，InterrogationManager 用它的位置判断玩家在看谁。
            // 外观全在 Model 子物体里，换成正式模型时删掉 Model、把新模型放进来即可。
            var root = new GameObject(objectName);
            root.transform.position = position + new Vector3(0f, 1f, 0f);
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = new Vector3(0f, -1f, 0f);

            InterrogationCharacter character = root.AddComponent<InterrogationCharacter>();
            character.characterName = displayName;
            character.promptFileName = promptFile;
            character.voice = voice;

            AudioSource audio = root.GetComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.minDistance = 1f;
            audio.maxDistance = 20f;

            // Art/<角色名>/idle.png 存在就用漫剧立牌，否则用代码拼的 3D 角色
            string artDir = MangaArtImporter.ArtFolder + objectName + "/";
            Sprite idle = AssetDatabase.LoadAssetAtPath<Sprite>(artDir + "idle.png");
            if (idle != null)
            {
                BuildStandee(root, model.transform, artDir, idle, height, character);
                return character;
            }

            CharacterModels.Result parts = buildModel(model.transform);
            CharacterPuppet puppet = root.AddComponent<CharacterPuppet>();
            puppet.body = model.transform;
            puppet.head = parts.head;
            puppet.mouth = parts.mouth;
            puppet.eyes = parts.eyes;

            if (parts.nose != null)
            {
                // 鼻子只有匹诺曹有。换成正式模型后把 nose 指向鼻子骨骼，或填 blendShapeMesh
                NoseGrower grower = root.AddComponent<NoseGrower>();
                grower.nose = parts.nose;
                character.nose = grower;
            }

            return character;
        }

        // 立牌：一张立绘按身高缩放，表情差分交给 MangaStandee 切换。
        // 可选的 nose.png 是单独的鼻子贴图（轴心在鼻根），nose.txt 写鼻根在立绘上的位置：
        // "u v" 为 0-1 的比例坐标（左下角为原点），第三项写 left 表示鼻子朝左。
        static void BuildStandee(GameObject root, Transform model, string artDir, Sprite idle,
            float height, InterrogationCharacter character)
        {
            // 立绘正面朝 -Z，根物体不转就正好对着站在原点的玩家；运行时 MangaStandee 会一直转向玩家
            root.transform.rotation = Quaternion.identity;

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(model, false);
            var body = bodyGo.AddComponent<SpriteRenderer>();
            body.sprite = idle;
            Vector2 size = idle.bounds.size;
            bodyGo.transform.localScale = Vector3.one * (height / size.y);

            MangaStandee standee = root.AddComponent<MangaStandee>();
            standee.body = body;
            standee.idle = idle;
            standee.blink = AssetDatabase.LoadAssetAtPath<Sprite>(artDir + "blink.png");
            var talk = new System.Collections.Generic.List<Sprite>();
            for (int i = 1; i <= 4; i++)
            {
                Sprite frame = AssetDatabase.LoadAssetAtPath<Sprite>(artDir + "talk" + i + ".png");
                if (frame != null) talk.Add(frame);
            }
            standee.talk = talk.ToArray();

            Sprite noseSprite = AssetDatabase.LoadAssetAtPath<Sprite>(artDir + "nose.png");
            string noseInfoPath = artDir + "nose.txt";
            if (noseSprite != null && File.Exists(noseInfoPath))
            {
                string[] info = File.ReadAllText(noseInfoPath).Trim().Split(new[] { ' ', '	', ',' },
                    System.StringSplitOptions.RemoveEmptyEntries);
                float u = float.Parse(info[0], System.Globalization.CultureInfo.InvariantCulture);
                float v = float.Parse(info[1], System.Globalization.CultureInfo.InvariantCulture);
                bool left = info.Length > 2 && info[2].ToLowerInvariant() == "left";

                var pivot = new GameObject("Nose");
                pivot.transform.SetParent(bodyGo.transform, false);
                // 立牌正面朝 -Z，鼻子往 -Z 挪一点点，压在脸上面
                pivot.transform.localPosition = new Vector3((u - 0.5f) * size.x, v * size.y, -0.002f);
                var noseRenderer = new GameObject("NoseSprite").AddComponent<SpriteRenderer>();
                noseRenderer.transform.SetParent(pivot.transform, false);
                noseRenderer.sprite = noseSprite;
                noseRenderer.flipX = left;
                noseRenderer.sortingOrder = 1;

                NoseGrower grower = root.AddComponent<NoseGrower>();
                grower.nose = pivot.transform;
                grower.growAxis = Vector3.right;
                character.nose = grower;
            }
        }
    }
}
