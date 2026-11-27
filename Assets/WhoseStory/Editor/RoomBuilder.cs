using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static WhoseStory.EditorTools.CharacterModels;

namespace WhoseStory.EditorTools
{
    // 童话城堡里的审讯室，和角色一样用基本体拼、Toon 描边平涂。
    // 玩家站在原点面朝 +Z，三个嫌疑人站在 z≈3 一排，碎镜子摔在他们和玩家之间的地上，
    // 后墙上留着一块挂过镜子的浅色印子和空钉子。
    public static class RoomBuilder
    {
        const float HalfWidth = 4f;
        const float Back = 6f;
        const float Front = -2.5f;
        const float Height = 3.2f;

        public static void Build()
        {
            Transform room = new GameObject("Room").transform;

            BuildShell(room);
            BuildWindow(room);
            BuildRug(room);
            BuildSconces(room);
            BuildBrokenMirror(room);
            BuildFurniture(room);
            PropLayout.Place(room);   // Blender 摆件（PropLayout.cs），模型在才替换程序拼的书架、画像、小桌
            SetupLighting();
        }

        // ---------- 地板、墙、天花板 ----------
        static void BuildShell(Transform room)
        {
            Material plankA = Mat("RoomPlankA", new Color(0.4f, 0.31f, 0.22f), 0f);
            Material plankB = Mat("RoomPlankB", new Color(0.35f, 0.27f, 0.19f), 0f);
            Material wall = Mat("RoomWall", new Color(0.56f, 0.55f, 0.42f), 0f);
            Material trim = Mat("RoomTrim", new Color(0.27f, 0.24f, 0.18f), 0f);
            Material ceiling = Mat("RoomCeiling", new Color(0.33f, 0.32f, 0.25f), 0f);

            // 地板：一条条木板，深浅交替
            float depth = Back - Front;
            int planks = 16;
            float plankWidth = HalfWidth * 2f / planks;
            for (int i = 0; i < planks; i++)
            {
                float x = -HalfWidth + plankWidth * (i + 0.5f);
                Box("Plank" + i, room, new Vector3(x, -0.05f, (Back + Front) * 0.5f),
                    new Vector3(plankWidth * 0.97f, 0.1f, depth), i % 2 == 0 ? plankA : plankB);
            }

            Box("WallBack", room, new Vector3(0f, Height * 0.5f, Back + 0.1f), new Vector3(HalfWidth * 2f + 0.4f, Height, 0.2f), wall, false);
            Box("WallFront", room, new Vector3(0f, Height * 0.5f, Front - 0.1f), new Vector3(HalfWidth * 2f + 0.4f, Height, 0.2f), wall, false);
            Box("WallLeft", room, new Vector3(-HalfWidth - 0.1f, Height * 0.5f, (Back + Front) * 0.5f), new Vector3(0.2f, Height, depth), wall, false);
            Box("WallRight", room, new Vector3(HalfWidth + 0.1f, Height * 0.5f, (Back + Front) * 0.5f), new Vector3(0.2f, Height, depth), wall, false);
            Box("Ceiling", room, new Vector3(0f, Height + 0.1f, (Back + Front) * 0.5f), new Vector3(HalfWidth * 2f + 0.4f, 0.2f, depth + 0.4f), ceiling, false);

            // 踢脚线和顶上的木梁，让房间有点城堡的结构感
            Box("BaseBack", room, new Vector3(0f, 0.1f, Back - 0.02f), new Vector3(HalfWidth * 2f, 0.2f, 0.06f), trim, false);
            Box("BaseLeft", room, new Vector3(-HalfWidth + 0.02f, 0.1f, (Back + Front) * 0.5f), new Vector3(0.06f, 0.2f, depth), trim, false);
            Box("BaseRight", room, new Vector3(HalfWidth - 0.02f, 0.1f, (Back + Front) * 0.5f), new Vector3(0.06f, 0.2f, depth), trim, false);
            for (int i = 0; i < 4; i++)
            {
                float z = Front + 1f + i * 2.2f;
                Box("Beam" + i, room, new Vector3(0f, Height - 0.15f, z), new Vector3(HalfWidth * 2f, 0.3f, 0.25f), trim, false);
            }

            // 后墙上挂过镜子的地方：一块比墙浅的椭圆印子，中间一颗空钉子
            Material ghost = Mat("RoomMirrorGhost", new Color(0.66f, 0.65f, 0.52f), 0f);
            Material nail = Mat("RoomNail", new Color(0.15f, 0.15f, 0.17f), 0f);
            Prim(PrimitiveType.Cylinder, "MirrorMark", room, new Vector3(1.9f, 1.75f, Back - 0.005f),
                new Vector3(0.75f, 0.005f, 1.0f), ghost, new Vector3(90f, 0f, 0f));
            Prim(PrimitiveType.Cylinder, "Nail", room, new Vector3(1.9f, 2.25f, Back - 0.04f),
                new Vector3(0.025f, 0.04f, 0.025f), nail, new Vector3(90f, 0f, 0f));
            // 钉子下面几道往下拖的抓痕，像镜子是被硬拽下来的
            Material scratch = Mat("RoomScratch", new Color(0.18f, 0.16f, 0.12f), 0f);
            float[] xs = { -0.09f, -0.03f, 0.03f, 0.08f };
            for (int i = 0; i < xs.Length; i++)
            {
                float len = 0.45f + i % 2 * 0.2f;
                Box("Scratch" + i, room, new Vector3(1.9f + xs[i], 2.05f - len * 0.5f, Back - 0.02f),
                    new Vector3(0.014f, len, 0.01f), scratch, false).transform.localRotation = Quaternion.Euler(0f, 0f, xs[i] * 40f);
            }
        }

        // ---------- 后墙的拱形窗，外面是夜空和月亮 ----------
        static void BuildWindow(Transform room)
        {
            Material sky = Mat("RoomNight", new Color(0.1f, 0.12f, 0.3f), 0f);
            Material frame = Mat("RoomWindowFrame", new Color(0.3f, 0.2f, 0.13f), 0f);
            Material moon = Mat("RoomMoon", new Color(1f, 0.95f, 0.65f), 0f);
            Material star = Mat("RoomStar", new Color(1f, 1f, 0.85f), 0f);

            Vector3 c = new Vector3(-1.6f, 1.6f, Back - 0.01f);
            Box("WindowSky", room, c, new Vector3(1.1f, 1.4f, 0.02f), sky, false);
            Prim(PrimitiveType.Cylinder, "WindowArch", room, c + new Vector3(0f, 0.7f, 0f),
                new Vector3(1.1f, 0.01f, 1.1f), sky, new Vector3(90f, 0f, 0f));
            Prim(PrimitiveType.Cylinder, "Moon", room, c + new Vector3(0.22f, 0.65f, -0.02f),
                new Vector3(0.32f, 0.005f, 0.32f), moon, new Vector3(90f, 0f, 0f));
            Vector3[] stars = { new(-0.3f, 0.3f, 0f), new(-0.2f, 0.9f, 0f), new(0.3f, 0.1f, 0f), new(-0.35f, -0.3f, 0f), new(0.25f, -0.45f, 0f) };
            for (int i = 0; i < stars.Length; i++)
            {
                Prim(PrimitiveType.Sphere, "Star" + i, room, c + stars[i] + new Vector3(0f, 0f, -0.02f), Vector3.one * 0.035f, star);
            }

            Box("WindowSill", room, c + new Vector3(0f, -0.75f, -0.08f), new Vector3(1.35f, 0.08f, 0.2f), frame, false);
            Box("WindowBarV", room, c + new Vector3(0f, 0.25f, -0.03f), new Vector3(0.06f, 1.9f, 0.04f), frame, false);
            Box("WindowBarH", room, c + new Vector3(0f, 0.15f, -0.03f), new Vector3(1.1f, 0.06f, 0.04f), frame, false);
            Box("WindowSideL", room, c + new Vector3(-0.58f, 0f, -0.03f), new Vector3(0.08f, 1.45f, 0.06f), frame, false);
            Box("WindowSideR", room, c + new Vector3(0.58f, 0f, -0.03f), new Vector3(0.08f, 1.45f, 0.06f), frame, false);
        }

        // ---------- 地毯 ----------
        static void BuildRug(Transform room)
        {
            Material border = Mat("RoomRugBorder", new Color(0.66f, 0.53f, 0.24f), 0f);
            Material rug = Mat("RoomRug", new Color(0.45f, 0.14f, 0.13f), 0f);
            Material pattern = Mat("RoomRugPattern", new Color(0.33f, 0.1f, 0.09f), 0f);
            Prim(PrimitiveType.Cylinder, "RugBorder", room, new Vector3(0f, 0.004f, 2.4f), new Vector3(5.6f, 0.004f, 4.2f), border);
            Prim(PrimitiveType.Cylinder, "Rug", room, new Vector3(0f, 0.009f, 2.4f), new Vector3(5.2f, 0.004f, 3.8f), rug);
            Prim(PrimitiveType.Cylinder, "RugPattern", room, new Vector3(0f, 0.013f, 2.4f), new Vector3(2.6f, 0.003f, 1.9f), pattern);
        }

        // ---------- 墙上的烛台，暖色点光源 ----------
        static void BuildSconces(Transform room)
        {
            Material iron = Mat("RoomIron", new Color(0.18f, 0.16f, 0.2f), 0f);
            Material wax = Mat("RoomWax", new Color(0.97f, 0.94f, 0.85f), 0f);
            Material flame = Mat("RoomFlame", new Color(1f, 0.75f, 0.2f), 0f);

            Vector3[] spots =
            {
                new(-HalfWidth + 0.15f, 1.8f, 1.2f), new(-HalfWidth + 0.15f, 1.8f, 4.2f),
                new(HalfWidth - 0.15f, 1.8f, 1.2f), new(HalfWidth - 0.15f, 1.8f, 4.2f),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                Transform s = Group("Sconce" + i, room, spots[i]);
                float inward = spots[i].x < 0 ? 1f : -1f;
                Box("Bracket", s, new Vector3(inward * 0.06f, -0.08f, 0f), new Vector3(0.14f, 0.03f, 0.08f), iron, false);
                Prim(PrimitiveType.Cylinder, "Cup", s, new Vector3(inward * 0.12f, -0.06f, 0f), new Vector3(0.1f, 0.015f, 0.1f), iron);
                Prim(PrimitiveType.Cylinder, "Candle", s, new Vector3(inward * 0.12f, 0.03f, 0f), new Vector3(0.05f, 0.08f, 0.05f), wax);
                Prim(PrimitiveType.Sphere, "Flame", s, new Vector3(inward * 0.12f, 0.15f, 0f), new Vector3(0.035f, 0.07f, 0.035f), flame);

                var light = new GameObject("CandleLight").AddComponent<Light>();
                light.transform.SetParent(s, false);
                light.transform.localPosition = new Vector3(inward * 0.3f, 0.2f, 0f);
                light.type = LightType.Point;
                light.color = new Color(1f, 0.72f, 0.4f);
                light.range = 4.5f;
                light.intensity = 1.6f;
                light.shadows = LightShadows.None;
            }
        }

        // ---------- 地上摔碎的魔镜 ----------
        // 怪诞卡通恐怖的味道：发暗的旧金框、大部分玻璃还在但裂成蛛网，
        // 掉了几块露出黑洞。玻璃里不放人脸（用户要求）。
        static void BuildBrokenMirror(Transform room)
        {
            // Blender 做的镜子（ArtSource/Props/build/10_mirror.py）在就用它：玻璃面朝上，实体的蛛网裂纹和掉落的碎片，不带人脸
            GameObject modelled = DollModel.InstantiateProp("PR_BrokenMirror", room, new Vector3(0.55f, 0.016f, 1.75f),
                Quaternion.Euler(-3f, 208f, 2f), 1.3f);
            if (modelled != null)
            {
                modelled.name = "BrokenMirror";
            }
            var random = new System.Random(5619);

            Material gold = Mat("MirrorGold", new Color(0.72f, 0.58f, 0.24f), 0f);
            Material goldDark = Mat("MirrorGoldDark", new Color(0.42f, 0.32f, 0.14f), 0f);
            Material gem = Mat("MirrorGem", new Color(0.5f, 0.06f, 0.08f), 0f);
            Material glass = Mat("MirrorGlass", new Color(0.62f, 0.7f, 0.68f), 0f);
            Material glassLight = Mat("MirrorGlassLight", new Color(0.8f, 0.86f, 0.82f), 0f);
            Material hole = Mat("MirrorHole", new Color(0.06f, 0.05f, 0.05f), 0f);
            Material crack = Mat("MirrorCrack", new Color(0.04f, 0.04f, 0.05f), 0f);

            // 没有 Blender 模型时才用基本体拼镜框和玻璃；地上的碎片两种情况都要
            if (modelled == null)
            {
            // 镜框摔在地上，一边被什么东西垫着翘起来一点
            Transform mirror = Group("BrokenMirror", room, new Vector3(0.55f, 0.06f, 1.75f));
            mirror.localRotation = Quaternion.Euler(-6f, 28f, 4f);

            Transform ring = Group("Frame", mirror, Vector3.zero);
            ring.localScale = new Vector3(1f, 1f, 1.35f);
            // 外框粗一圈、内圈一道暗色线脚，像老旧的雕花框
            Lathe("Ring", ring, Vector3.zero, gold,
                0.3f, 0f, 0.4f, 0f, 0.41f, 0.03f, 0.39f, 0.06f, 0.33f, 0.06f, 0.31f, 0.035f, 0.3f, 0f);
            Lathe("InnerLip", ring, Vector3.zero, goldDark,
                0.285f, 0f, 0.305f, 0f, 0.305f, 0.04f, 0.29f, 0.04f, 0.285f, 0f);

            // 顶上的冠饰和暗红宝石，四周一圈雕花凸起
            Prim(PrimitiveType.Sphere, "Crest", mirror, new Vector3(0f, 0.05f, 0.56f), new Vector3(0.22f, 0.06f, 0.12f), gold);
            Prim(PrimitiveType.Sphere, "TopGem", mirror, new Vector3(0f, 0.085f, 0.56f), new Vector3(0.07f, 0.04f, 0.09f), gem);
            for (int i = 0; i < 14; i++)
            {
                float a = i * Mathf.PI * 2f / 14f;
                Prim(PrimitiveType.Sphere, "Carving" + i, mirror,
                    new Vector3(Mathf.Cos(a) * 0.36f, 0.055f, Mathf.Sin(a) * 0.36f * 1.35f),
                    new Vector3(0.06f, 0.035f, 0.06f), i % 2 == 0 ? gold : goldDark);
            }

            // 四个斜角上的卷草雕花，和一截断掉的挂链拖在冠饰后面
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + i * 90f) * Mathf.Deg2Rad;
                Transform curl = Group("Scroll" + i, mirror, new Vector3(Mathf.Cos(a) * 0.4f, 0.04f, Mathf.Sin(a) * 0.4f * 1.35f));
                curl.localRotation = Quaternion.Euler(90f, -i * 90f - 45f, 0f);
                Torus("Curl", curl, Vector3.zero, gold, 0.035f, 0.011f);
                Torus("CurlSmall", curl, new Vector3(0.045f, 0f, 0f), goldDark, 0.018f, 0.008f);
            }
            for (int i = 0; i < 4; i++)
            {
                Transform link = Group("ChainLink" + i, mirror, new Vector3(0.03f * i, 0.015f, 0.64f + 0.045f * i));
                link.localRotation = Quaternion.Euler(i % 2 == 0 ? 0f : 90f, 20f * i, 0f);
                link.localScale = new Vector3(1f, 1f, 1.5f);
                Torus("Link", link, Vector3.zero, goldDark, 0.016f, 0.005f);
            }

            // 玻璃面：整块还在，只是裂了
            Transform face = Group("Glass", ring, new Vector3(0f, 0.012f, 0f));
            Prim(PrimitiveType.Cylinder, "Pane", face, Vector3.zero, new Vector3(0.6f, 0.004f, 0.6f), glass);
            // 两道斜着的反光，卡通里表示"这是镜子"
            Prim(PrimitiveType.Cube, "Glint1", face, new Vector3(-0.12f, 0.005f, 0.12f), new Vector3(0.035f, 0.002f, 0.3f), glassLight, new Vector3(0f, 35f, 0f));
            Prim(PrimitiveType.Cube, "Glint2", face, new Vector3(-0.05f, 0.005f, 0.17f), new Vector3(0.015f, 0.002f, 0.18f), glassLight, new Vector3(0f, 35f, 0f));

            // 蛛网裂纹：从撞击点放射出去的黑线，再加一圈断续的环
            Vector2 impact = new Vector2(0.13f, -0.12f);
            random = new System.Random(5619);
            for (int i = 0; i < 9; i++)
            {
                float a = i * Mathf.PI * 2f / 9f + (float)random.NextDouble() * 0.4f;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float len = 0.18f + (float)random.NextDouble() * 0.2f;
                CrackLine("Ray" + i, face, impact, impact + dir * len, crack);
            }
            for (int i = 0; i < 9; i++)
            {
                float a0 = i * Mathf.PI * 2f / 9f + 0.2f, a1 = a0 + 0.55f;
                float r = 0.08f + (float)random.NextDouble() * 0.03f;
                CrackLine("RingCrack" + i, face,
                    impact + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r,
                    impact + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r, crack);
            }

            // 掉了的几块：黑洞
            Vector2[][] holes =
            {
                new[] { impact + new Vector2(-0.03f, 0.02f), impact + new Vector2(0.05f, 0.03f), impact + new Vector2(0.01f, -0.05f) },
                new[] { new Vector2(0.18f, 0.12f), new Vector2(0.28f, 0.02f), new Vector2(0.27f, 0.18f) },
                new[] { new Vector2(-0.05f, -0.3f), new Vector2(0.08f, -0.26f), new Vector2(0.02f, -0.38f) },
            };
            for (int i = 0; i < holes.Length; i++)
            {
                Shard("Hole" + i, face, holes[i], 0.003f, hole).transform.localPosition = new Vector3(0f, 0.004f, 0f);
            }

            }

            // 碎片撒了一地，离镜子越远越小
            for (int i = 0; i < 14; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float dist = 0.5f + (float)random.NextDouble() * 0.9f;
                float size = Mathf.Lerp(0.13f, 0.05f, (dist - 0.5f) / 0.9f);
                Vector2 c = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
                Vector2[] tri = RandomTriangle(random, size);
                GameObject shard = Shard("Shard" + i, room, tri, 0.008f, i % 3 == 0 ? glassLight : glass);
                // 地毯顶面在 1.6 厘米左右，碎片要放在它上面
                shard.transform.localPosition = new Vector3(0.55f + c.x, 0.02f, 1.75f + c.y);
                shard.transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            }

            // 最大的一块碎片，单独甩得远一点（用户要求镜子里不要出现人脸或眼睛）
            GameObject big = Shard("BigShard", room,
                new[] { new Vector2(-0.12f, -0.06f), new Vector2(0.14f, -0.02f), new Vector2(0.0f, 0.15f) }, 0.01f, glass);
            big.transform.localPosition = new Vector3(-0.35f, 0.02f, 1.35f);
            big.transform.localRotation = Quaternion.Euler(0f, 200f, 0f);
        }

        // 玻璃上的一道裂纹：贴在玻璃面上的细黑条
        static void CrackLine(string name, Transform parent, Vector2 from, Vector2 to, Material mat)
        {
            Vector2 mid = (from + to) * 0.5f;
            Vector2 d = to - from;
            float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            Prim(PrimitiveType.Cube, name, parent, new Vector3(mid.x, 0.005f, mid.y),
                new Vector3(0.006f, 0.002f, d.magnitude), mat, new Vector3(0f, yaw, 0f));
        }

        // 圆环：用旋转体把一个小圆绕 Y 轴转一圈
        static GameObject Torus(string name, Transform parent, Vector3 position, Material mat, float radius, float tube)
        {
            var profile = new float[26];
            for (int i = 0; i <= 12; i++)
            {
                float t = i / 12f * Mathf.PI * 2f;
                profile[i * 2] = radius + Mathf.Sin(t) * tube;
                profile[i * 2 + 1] = -Mathf.Cos(t) * tube;
            }
            return Lathe(name, parent, position, mat, profile);
        }

        // ---------- 家具和小道具 ----------
        static void BuildFurniture(Transform room)
        {
            Material wood = Mat("RoomFurnitureWood", new Color(0.33f, 0.22f, 0.14f), 0f);
            Material woodLight = Mat("RoomFurnitureWoodLight", new Color(0.45f, 0.32f, 0.2f), 0f);
            Material frameGold = Mat("RoomFrameGold", new Color(0.6f, 0.48f, 0.22f), 0f);
            Material canvas = Mat("RoomCanvas", new Color(0.18f, 0.2f, 0.16f), 0f);
            Material paint = Mat("RoomPaintFace", new Color(0.62f, 0.58f, 0.46f), 0f);
            Material eyeWhite = Mat("RoomPaintEye", new Color(0.92f, 0.9f, 0.8f), 0f);
            Material dark = Mat("RoomDark", new Color(0.05f, 0.05f, 0.05f), 0f);
            Material web = Mat("RoomWeb", new Color(0.82f, 0.82f, 0.78f), 0f);

            // 左墙的书架：书有高有矮、有的歪着，中间空了一格
            Transform shelf = Group("Bookshelf", room, new Vector3(-HalfWidth + 0.2f, 0f, 2.7f));
            Box("SideA", shelf, new Vector3(0f, 1.0f, -0.7f), new Vector3(0.38f, 2.0f, 0.05f), wood);
            Box("SideB", shelf, new Vector3(0f, 1.0f, 0.7f), new Vector3(0.38f, 2.0f, 0.05f), wood);
            Box("Back", shelf, new Vector3(-0.17f, 1.0f, 0f), new Vector3(0.03f, 2.0f, 1.4f), wood);
            Box("Top", shelf, new Vector3(0f, 2.02f, 0f), new Vector3(0.42f, 0.05f, 1.5f), woodLight);
            Color[] bookColors =
            {
                new(0.45f, 0.12f, 0.1f), new(0.15f, 0.25f, 0.2f), new(0.5f, 0.42f, 0.2f),
                new(0.2f, 0.18f, 0.3f), new(0.35f, 0.2f, 0.12f), new(0.55f, 0.5f, 0.4f),
            };
            var random = new System.Random(24);
            for (int row = 0; row < 4; row++)
            {
                float y = 0.1f + row * 0.48f;
                Box("Shelf" + row, shelf, new Vector3(0f, y, 0f), new Vector3(0.36f, 0.035f, 1.36f), woodLight);
                float z = -0.64f;
                while (z < 0.62f)
                {
                    if (row == 2 && z > -0.1f && z < 0.25f) { z = 0.25f; continue; }
                    float w = 0.04f + (float)random.NextDouble() * 0.04f;
                    float h = 0.26f + (float)random.NextDouble() * 0.14f;
                    float lean = random.NextDouble() < 0.15 ? 12f : 0f;
                    int bi = random.Next(bookColors.Length);
                    Material m = Mat("RoomBook" + bi, bookColors[bi], 0f);
                    Box("Book", shelf, new Vector3(0.02f, y + 0.02f + h * 0.5f, z + w * 0.5f), new Vector3(0.26f, h, w), m)
                        .transform.localRotation = Quaternion.Euler(lean, 0f, 0f);
                    z += w + 0.004f;
                }
            }

            // 右墙两幅画像：一幅歪着，画里的人眼睛是白的，像在盯着人看
            for (int i = 0; i < 2; i++)
            {
                Transform portrait = Group("Portrait" + i, room, new Vector3(HalfWidth - 0.03f, 1.85f, 2.1f + i * 1.3f));
                // 画面朝向房间（-X），再绕自己的朝向轴歪一点
                portrait.localRotation = Quaternion.Euler(0f, 90f, i == 0 ? 7f : -3f);
                Box("Frame", portrait, Vector3.zero, new Vector3(0.62f, 0.8f, 0.05f), frameGold);
                Box("Canvas", portrait, new Vector3(0f, 0f, -0.028f), new Vector3(0.5f, 0.68f, 0.01f), canvas);
                Prim(PrimitiveType.Sphere, "Head", portrait, new Vector3(0f, 0.08f, -0.034f), new Vector3(0.22f, 0.28f, 0.005f), paint);
                Prim(PrimitiveType.Sphere, "Body", portrait, new Vector3(0f, -0.28f, -0.034f), new Vector3(0.42f, 0.3f, 0.005f), Mat("RoomPaintCoat", new Color(0.3f, 0.12f, 0.12f), 0f));
                Prim(PrimitiveType.Sphere, "EyeL", portrait, new Vector3(-0.045f, 0.11f, -0.037f), new Vector3(0.045f, 0.03f, 0.004f), eyeWhite);
                Prim(PrimitiveType.Sphere, "EyeR", portrait, new Vector3(0.045f, 0.11f, -0.037f), new Vector3(0.045f, 0.03f, 0.004f), eyeWhite);
                Prim(PrimitiveType.Sphere, "PupilL", portrait, new Vector3(-0.055f, 0.105f, -0.039f), Vector3.one * 0.012f, dark);
                Prim(PrimitiveType.Sphere, "PupilR", portrait, new Vector3(0.035f, 0.105f, -0.039f), Vector3.one * 0.012f, dark);
            }

            // 窗边的小圆桌：烛台、一个盘子、一个被咬了一口的红苹果
            Transform table = Group("SideTable", room, new Vector3(-2.8f, 0f, 5.2f));
            Prim(PrimitiveType.Cylinder, "Top", table, new Vector3(0f, 0.72f, 0f), new Vector3(0.6f, 0.02f, 0.6f), woodLight);
            Prim(PrimitiveType.Cylinder, "Leg", table, new Vector3(0f, 0.36f, 0f), new Vector3(0.07f, 0.36f, 0.07f), wood);
            Prim(PrimitiveType.Cylinder, "Foot", table, new Vector3(0f, 0.02f, 0f), new Vector3(0.36f, 0.02f, 0.36f), wood);
            Prim(PrimitiveType.Cylinder, "Plate", table, new Vector3(0.08f, 0.745f, 0.05f), new Vector3(0.22f, 0.006f, 0.22f), Mat("RoomPlate", new Color(0.85f, 0.83f, 0.76f), 0f));
            Transform apple = Group("Apple", table, new Vector3(0.08f, 0.79f, 0.05f));
            Prim(PrimitiveType.Sphere, "Fruit", apple, Vector3.zero, new Vector3(0.085f, 0.08f, 0.085f), Mat("RoomApple", new Color(0.62f, 0.08f, 0.08f), 0f));
            Prim(PrimitiveType.Sphere, "Bite", apple, new Vector3(0.035f, 0.01f, 0.02f), new Vector3(0.04f, 0.045f, 0.03f), Mat("RoomAppleFlesh", new Color(0.93f, 0.9f, 0.7f), 0f));
            Prim(PrimitiveType.Cylinder, "Stem", apple, new Vector3(0f, 0.045f, 0f), new Vector3(0.006f, 0.015f, 0.006f), wood, new Vector3(0f, 0f, 15f));
            Prim(PrimitiveType.Cylinder, "Candle", table, new Vector3(-0.12f, 0.8f, -0.05f), new Vector3(0.045f, 0.08f, 0.045f), Mat("RoomWax", new Color(0.97f, 0.94f, 0.85f), 0f));
            Prim(PrimitiveType.Sphere, "Flame", table, new Vector3(-0.12f, 0.92f, -0.05f), new Vector3(0.03f, 0.06f, 0.03f), Mat("RoomFlame", new Color(1f, 0.75f, 0.2f), 0f));

            // 后墙两个角上的蜘蛛网：几根放射线加两圈横线
            foreach (float side in new[] { -1f, 1f })
            {
                Transform corner = Group(side < 0 ? "WebL" : "WebR", room, new Vector3(side * (HalfWidth - 0.01f), Height - 0.01f, Back - 0.01f));
                for (int i = 0; i < 5; i++)
                {
                    float a = (i / 4f) * 90f;
                    Vector3 dir = Quaternion.Euler(0f, 0f, side < 0 ? -a : a) * Vector3.down;
                    Vector3 diag = (dir + new Vector3(0f, 0f, -0.6f)).normalized;
                    Box("Thread" + i, corner, diag * 0.3f, new Vector3(0.004f, 0.6f, 0.004f), web, false)
                        .transform.localRotation = Quaternion.FromToRotation(Vector3.up, diag);
                }
                foreach (float r in new[] { 0.22f, 0.42f })
                {
                    for (int i = 0; i < 4; i++)
                    {
                        float a0 = (i / 4f) * 90f, a1 = ((i + 1) / 4f) * 90f;
                        Vector3 p0 = (Quaternion.Euler(0f, 0f, side < 0 ? -a0 : a0) * Vector3.down + new Vector3(0f, 0f, -0.6f)).normalized * r;
                        Vector3 p1 = (Quaternion.Euler(0f, 0f, side < 0 ? -a1 : a1) * Vector3.down + new Vector3(0f, 0f, -0.6f)).normalized * r;
                        Box("Ring", corner, (p0 + p1) * 0.5f, new Vector3(0.004f, (p1 - p0).magnitude, 0.004f), web, false)
                            .transform.localRotation = Quaternion.FromToRotation(Vector3.up, (p1 - p0).normalized);
                    }
                }
            }
        }

        static Vector2[] RandomTriangle(System.Random random, float size)
        {
            var pts = new Vector2[3];
            float start = (float)random.NextDouble() * Mathf.PI * 2f;
            for (int i = 0; i < 3; i++)
            {
                float a = start + i * Mathf.PI * 2f / 3f + ((float)random.NextDouble() - 0.5f) * 0.9f;
                float r = size * (0.5f + (float)random.NextDouble() * 0.7f);
                pts[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            return pts;
        }

        // 一片三角形碎玻璃：带厚度的三棱柱，顶面朝上
        static GameObject Shard(string name, Transform parent, Vector2[] tri, float thickness, Material mat)
        {
            // 保证顶面朝上：三点按俯视顺时针排列（Unity 正面是顺时针）
            Vector2 e1 = tri[1] - tri[0], e2 = tri[2] - tri[0];
            if (e1.x * e2.y - e1.y * e2.x > 0f)
            {
                (tri[1], tri[2]) = (tri[2], tri[1]);
            }

            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            Vector3 Top(int i) => new Vector3(tri[i].x, thickness, tri[i].y);
            Vector3 Bottom(int i) => new Vector3(tri[i].x, 0f, tri[i].y);

            verts.AddRange(new[] { Top(0), Top(1), Top(2) });
            tris.AddRange(new[] { 0, 1, 2 });
            verts.AddRange(new[] { Bottom(0), Bottom(2), Bottom(1) });
            tris.AddRange(new[] { 3, 4, 5 });
            for (int i = 0; i < 3; i++)
            {
                int j = (i + 1) % 3;
                int b = verts.Count;
                verts.AddRange(new[] { Bottom(i), Top(i), Top(j), Bottom(j) });
                tris.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }

            var mesh = new Mesh { name = parent.name + "_" + name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            SaveMesh(mesh);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material mat, bool castShadows = true)
        {
            GameObject go = Prim(PrimitiveType.Cube, name, parent, position, size, mat);
            // 墙和天花板不挡平行光，屋里才不会一片黑
            if (!castShadows)
            {
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            return go;
        }

        // 光线偏暗、偏黄绿，蜡烛会闪；再加暗角、胶片颗粒、降饱和，做出老旧卡通恐怖片的感觉
        static void SetupLighting()
        {
            Light sun = Object.FindFirstObjectByType<Light>();
            if (sun != null && sun.type == LightType.Directional)
            {
                sun.color = new Color(0.85f, 0.88f, 0.75f);
                sun.intensity = 0.65f;
                sun.transform.rotation = Quaternion.Euler(55f, 160f, 0f);
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.34f, 0.34f, 0.28f);

            foreach (Light candle in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (candle.type == LightType.Point)
                {
                    candle.gameObject.AddComponent<CandleFlicker>();
                }
            }

            Camera camera = Object.FindFirstObjectByType<Camera>();
            if (camera != null)
            {
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            }

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "UncannyLook";
            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.38f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.06f, 0.05f, 0.03f));
            FilmGrain grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium3);
            grain.intensity.Override(0.35f);
            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(-22f);
            color.contrast.Override(8f);
            color.colorFilter.Override(new Color(1f, 0.97f, 0.86f));
            UnityEditor.AssetDatabase.CreateAsset(profile, "Assets/WhoseStory/Generated/UncannyLook.asset");
            foreach (VolumeComponent component in profile.components)
            {
                UnityEditor.AssetDatabase.AddObjectToAsset(component, profile);
            }

            var volume = new GameObject("PostProcess").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }
    }
}
