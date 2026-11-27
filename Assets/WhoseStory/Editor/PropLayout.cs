using UnityEngine;
using UnityEditor;

namespace WhoseStory.EditorTools
{
    // 把 Blender 做的摆件（ArtSource/Props，导出到 Models/PR_*.fbx）摆进审讯室。
    // 玩家站在原点面朝 +Z，三个嫌疑人在 z≈3 一排（x = -1.5 / 0 / 1.5），他们周围和玩家到他们之间留空。
    // 墙：左 x=-4，右 x=4，后 z=6，前 z=-2.5。后墙 x=-1.6 是窗户，x=1.9 是镜子挂过的印子。
    // yaw：道具正面默认朝 -Z（朝玩家）；-90 朝 +X（靠左墙），90 朝 -X（靠右墙），180 朝 +Z。
    // 某个 FBX 不在就跳过它；程序拼的书架、画像、窗边小桌只在对应模型放成功后才删掉。
    public static class PropLayout
    {
        struct Item
        {
            public string asset;
            public Vector3 position;
            public float yaw;
            public float scale;
            public string replaces;   // 放成功后删掉的程序拼版本（RoomBuilder 里的名字），可以为空

            public Item(string asset, float x, float y, float z, float yaw, float scale = 1f, string replaces = null)
            {
                this.asset = asset;
                position = new Vector3(x, y, z);
                this.yaw = yaw;
                this.scale = scale;
                this.replaces = replaces;
            }
        }

        const float TableTop = 0.72f;
        const float BenchTop = 0.78f;
        const float MantelTop = 1.16f;

        static readonly Item[] Items =
        {
            // 左墙：书架、落地钟、衣柜，书架前的地上摊着魔法书
            new("PR_Bookshelf", -3.8f, 0f, 2.7f, -90f, 1f, "Bookshelf"),
            new("PR_GrandfatherClock", -3.8f, 0f, 4.4f, -90f),
            new("PR_Wardrobe", -3.7f, 0f, 0.6f, -90f),
            new("PR_SpellBook", -3.2f, 0f, 3.7f, -60f),
            new("PR_DressForm", -3.3f, 0f, 5.3f, -40f),
            new("PR_RockingHorse", -2.7f, 0f, 1.2f, -130f),
            // 左前角：老木匠的工作台，上面放蟋蟀罐和音乐盒
            new("PR_Workbench", -3.5f, 0f, -1.3f, -90f),
            new("PR_CricketJar", -3.45f, BenchTop, -1.6f, -70f),
            new("PR_MusicBox", -3.5f, BenchTop, -1.0f, -110f),
            // 窗边小圆桌：被咬了一口的苹果、烛台、绣花蕾丝
            new("PR_RoundTable", -2.8f, 0f, 5.2f, 0f, 1f, "SideTable"),
            new("PR_ApplePlate", -2.72f, TableTop, 5.12f, 20f),
            new("PR_Candlestick", -2.98f, TableTop, 5.32f, 0f),
            new("PR_BodiceLace", -2.62f, TableTop, 5.36f, 30f, 0.8f),
            // 后墙：七把小椅子、木偶剧场、布谷鸟钟、小丑盒、鸟笼、叠起来的箱子
            new("PR_SevenChairs", -0.75f, 0f, 5.55f, 10f),
            new("PR_CuckooClock", -0.45f, 2.0f, 5.98f, 0f),
            new("PR_PuppetTheatre", 0.75f, 0f, 5.6f, 0f),
            new("PR_JackInTheBox", 1.65f, 0f, 5.45f, -15f),
            new("PR_Trunks", 3.35f, 0f, 5.5f, 200f),
            new("PR_Birdcage", 2.55f, 0f, 5.6f, 0f),
            // 右墙：两幅画像（老国王、杰佩托）、壁炉（壁炉台上放油灯和毒药瓶）、壁炉前的高背椅（椅面上是毒梳子）
            new("PR_PortraitKing", 3.97f, 1.85f, 2.1f, 90f, 1f, "Portrait0"),       // 老国王（白雪的父亲）
            new("PR_PortraitGeppetto", 3.97f, 1.85f, 3.35f, 90f, 0.9f, "Portrait1"), // 杰佩托（匹诺曹的木匠爸爸）
            new("PR_Fireplace", 3.98f, 0f, 4.7f, 90f),
            new("PR_OilLamp", 3.76f, MantelTop, 4.35f, 90f),
            new("PR_PoisonBottle", 3.78f, MantelTop, 5.05f, 120f),
            new("PR_HighChair", 3.0f, 0f, 3.95f, 60f),
            new("PR_PoisonComb", 3.0f, 0.5f, 3.95f, 60f, 0.8f),
            // 右前：留声机、穿衣镜框、锡兵，第二张圆桌上放插着刀的木盒
            new("PR_RoundTable", 3.0f, 0f, 1.7f, 0f),
            new("PR_KnifeBox", 3.0f, TableTop, 1.7f, 70f),
            new("PR_Gramophone", 3.45f, 0f, 0.4f, 120f),
            new("PR_StandingMirror", 3.4f, 0f, -0.9f, 110f),
            new("PR_TinSoldier", 2.4f, 0f, 0.3f, 200f, 1.2f),
        };

        public static void Place(Transform room)
        {
            int placed = 0;
            foreach (Item item in Items)
            {
                GameObject go = DollModel.InstantiateProp(item.asset, room, item.position, Quaternion.Euler(0f, item.yaw, 0f), item.scale);
                if (go == null)
                {
                    continue;
                }
                placed++;
                if (!string.IsNullOrEmpty(item.replaces))
                {
                    Transform old = room.Find(item.replaces);
                    if (old != null)
                    {
                        Object.DestroyImmediate(old.gameObject);
                    }
                }
            }
            Debug.Log($"[WhoseStory] placed {placed} modelled props");
        }

        // 不重建整个场景：删掉已经摆过的模型摆件再摆一遍
        [MenuItem("WhoseStory/Place Blender Props In Open Scene")]
        public static void PlaceInOpenScene()
        {
            GameObject room = GameObject.Find("Room");
            if (room == null)
            {
                Debug.LogError("[WhoseStory] no 'Room' in the open scene");
                return;
            }
            for (int i = room.transform.childCount - 1; i >= 0; i--)
            {
                Transform c = room.transform.GetChild(i);
                if (c.name.StartsWith("PR_"))
                {
                    Object.DestroyImmediate(c.gameObject);
                }
            }
            Place(room.transform);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(room.scene);
            AssetDatabase.SaveAssets();
        }
    }
}
