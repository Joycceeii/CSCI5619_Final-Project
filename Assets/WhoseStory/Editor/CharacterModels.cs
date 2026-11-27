using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WhoseStory;

namespace WhoseStory.EditorTools
{
    // 用基本体和旋转体网格拼出三个风格化角色，免下载，之后换真模型时整个 Model 子物体删掉即可。
    // 坐标以脚底为原点、+Z 朝向玩家（角色根物体已经转了 180 度）。
    // 头部的五官、头发、帽子都挂在 Head 下面，CharacterPuppet 转头时会一起动。
    public static class CharacterModels
    {
        const string GeneratedFolder = "Assets/WhoseStory/Generated";
        const string MeshFolder = GeneratedFolder + "/Meshes";
        const string MaterialFolder = GeneratedFolder + "/Materials";

        static readonly Dictionary<string, Material> materials = new();

        // Unity 自带的球和圆柱面数太低，近看能看到棱角，这里换成更圆的版本，每次构建生成一次
        static Mesh smoothSphere;
        static Mesh smoothCylinder;

        // 纸张/水彩底纹，乘在所有 Toon 材质上，让平涂色块有一点手绘的颗粒感
        const string PaperTexturePath = "Assets/WhoseStory/Textures/paper_soft.png";

        public struct Result
        {
            public Transform head;
            public Transform mouth;
            public Transform nose; // 只有匹诺曹有
            public Transform[] eyes; // 眨眼时沿 Y 压扁
        }

        public static void PrepareFolders()
        {
            materials.Clear();
            smoothSphere = null;
            smoothCylinder = null;
            if (AssetDatabase.IsValidFolder(GeneratedFolder))
            {
                AssetDatabase.DeleteAsset(GeneratedFolder);
            }
            AssetDatabase.CreateFolder("Assets/WhoseStory", "Generated");
            AssetDatabase.CreateFolder(GeneratedFolder, "Meshes");
            AssetDatabase.CreateFolder(GeneratedFolder, "Materials");
        }

        // ---------- 白雪公主：黑色短发、红色发带、蓝色上衣、黄色长裙 ----------
        public static Result BuildSnowWhite(Transform model)
        {
            // Blender 做的木偶模型在就用它（PinocchioModel.cs 里的 DollModel），否则用下面代码拼的版本
            if (DollModel.TryBuild("CH_SnowWhite", model, null, false, out Result imported))
            {
                return imported;
            }

            Material skin = Mat("SnowSkin", new Color(1f, 0.86f, 0.78f), 0.35f);
            Material hair = Mat("SnowHair", new Color(0.05f, 0.04f, 0.05f), 0.35f);
            Material bodice = Mat("SnowBodice", new Color(0.12f, 0.22f, 0.62f), 0.3f);
            Material sleeve = Mat("SnowSleeve", new Color(0.75f, 0.12f, 0.15f), 0.3f);
            Material skirt = Mat("SnowSkirt", new Color(1f, 0.85f, 0.3f), 0.25f);
            Material ribbon = Mat("SnowRibbon", new Color(0.85f, 0.08f, 0.12f), 0.4f);
            Material lips = Mat("SnowLips", new Color(0.45f, 0.08f, 0.1f), 0.5f);
            Material shoe = Mat("SnowShoe", new Color(0.6f, 0.45f, 0.2f), 0.5f);

            Material petticoat = Mat("SnowPetticoat", new Color(0.97f, 0.95f, 0.9f), 0f);
            Material hem = Mat("SnowHem", new Color(0.88f, 0.68f, 0.2f), 0f);
            Material lace = Mat("SnowLace", new Color(0.85f, 0.1f, 0.12f), 0f);

            // 裙子：下摆有一圈深一点的滚边，底下露出白衬裙
            Lathe("Petticoat", model, Vector3.zero, petticoat, 0f, 0f, 0.31f, 0f, 0.31f, 0.035f, 0.29f, 0.05f, 0f, 0.05f);
            Lathe("Skirt", model, Vector3.zero, skirt, 0f, 0.03f, 0.3f, 0.03f, 0.3f, 0.07f, 0.27f, 0.25f, 0.19f, 0.55f, 0.13f, 0.78f);
            Lathe("Hem", model, Vector3.zero, hem, 0.302f, 0.03f, 0.305f, 0.03f, 0.302f, 0.085f, 0.296f, 0.085f, 0.298f, 0.03f);
            // 上衣：收腰、胸口略鼓，像真的衣服而不是一根圆柱
            Lathe("Bodice", model, Vector3.zero, bodice, 0.13f, 0.78f, 0.112f, 0.86f, 0.118f, 0.93f, 0.142f, 1.0f, 0.14f, 1.06f, 0.1f, 1.12f, 0f, 1.13f);
            // 胸前的红色交叉系带
            for (int i = 0; i < 3; i++)
            {
                float y = 0.85f + i * 0.06f;
                Prim(PrimitiveType.Cube, "LaceA" + i, model, new Vector3(0f, y, 0.122f + i * 0.006f), new Vector3(0.006f, 0.065f, 0.004f), lace, new Vector3(0f, 0f, 40f));
                Prim(PrimitiveType.Cube, "LaceB" + i, model, new Vector3(0f, y, 0.122f + i * 0.006f), new Vector3(0.006f, 0.065f, 0.004f), lace, new Vector3(0f, 0f, -40f));
            }
            Prim(PrimitiveType.Sphere, "ShoeL", model, new Vector3(-0.07f, 0.02f, 0.2f), new Vector3(0.08f, 0.04f, 0.12f), shoe);
            Prim(PrimitiveType.Sphere, "ShoeR", model, new Vector3(0.07f, 0.02f, 0.2f), new Vector3(0.08f, 0.04f, 0.12f), shoe);

            // 双手往前收在身前，像在紧张地攥着手
            Arms(model, 1.07f, 0.16f, 0.38f, sleeve, 0.12f, skin, skin, -10f, 42f);
            Prim(PrimitiveType.Cylinder, "Neck", model, new Vector3(0f, 1.15f, 0f), new Vector3(0.07f, 0.04f, 0.07f), skin);
            // 白色小立领，只在脖子后半圈
            LatheArc("Collar", model, Vector3.zero, petticoat, 220f, 0.05f, 1.11f, 0.08f, 1.19f, 0.072f, 1.195f, 0.042f, 1.115f);

            Transform head = Group("Head", model, new Vector3(0f, 1.17f, 0f));
            Prim(PrimitiveType.Sphere, "Face", head, new Vector3(0f, 0.13f, 0f), new Vector3(0.25f, 0.27f, 0.25f), skin);
            Prim(PrimitiveType.Sphere, "HairCap", head, new Vector3(0f, 0.16f, -0.03f), new Vector3(0.28f, 0.28f, 0.28f), hair);
            Prim(PrimitiveType.Sphere, "HairBobL", head, new Vector3(-0.1f, 0.08f, -0.03f), new Vector3(0.12f, 0.18f, 0.2f), hair);
            Prim(PrimitiveType.Sphere, "HairBobR", head, new Vector3(0.1f, 0.08f, -0.03f), new Vector3(0.12f, 0.18f, 0.2f), hair);
            Prim(PrimitiveType.Sphere, "HairBack", head, new Vector3(0f, 0.08f, -0.09f), new Vector3(0.24f, 0.2f, 0.12f), hair);
            // 齐刘海：一排压扁的小发片，两边往后收，发梢略尖
            Bangs(head, hair, 0.232f, 0.098f, 6, 0.1f);
            // 两鬓垂下来的一缕
            Prim(PrimitiveType.Sphere, "SideLockL", head, new Vector3(-0.112f, 0.1f, 0.04f), new Vector3(0.035f, 0.13f, 0.05f), hair, new Vector3(0f, 0f, -8f));
            Prim(PrimitiveType.Sphere, "SideLockR", head, new Vector3(0.112f, 0.1f, 0.04f), new Vector3(0.035f, 0.13f, 0.05f), hair, new Vector3(0f, 0f, 8f));
            // 发带是一道从左耳翻过头顶到右耳的半圈，略往后倒；蝴蝶结在头顶偏一侧
            LatheArc("Headband", head, new Vector3(0f, 0.16f, -0.03f), ribbon, 190f, 0.141f, -0.012f, 0.145f, -0.012f, 0.145f, 0.012f, 0.141f, 0.012f, 0.141f, -0.012f)
                .transform.localRotation = Quaternion.Euler(72f, 0f, 0f);
            Transform bow = Group("Bow", head, new Vector3(0.06f, 0.285f, -0.04f));
            bow.localRotation = Quaternion.Euler(-20f, 0f, -25f);
            Prim(PrimitiveType.Sphere, "LoopL", bow, new Vector3(-0.035f, 0f, 0f), new Vector3(0.055f, 0.035f, 0.02f), ribbon, new Vector3(0f, 0f, -15f));
            Prim(PrimitiveType.Sphere, "LoopR", bow, new Vector3(0.035f, 0f, 0f), new Vector3(0.055f, 0.035f, 0.02f), ribbon, new Vector3(0f, 0f, 15f));
            Prim(PrimitiveType.Sphere, "Knot", bow, Vector3.zero, new Vector3(0.022f, 0.022f, 0.022f), ribbon);

            // 眼皮压下来两成，一脸看透一切的冷淡
            Material shadow = Mat("SnowEyeShadow", new Color(0.86f, 0.74f, 0.8f), 0f);
            Transform[] eyes = Eyes(head, 0.15f, 0.032f, 0.105f, 0.06f, shadow, 0.2f);
            Lashes(eyes, 0.06f, hair);
            Nose(head, new Vector3(0f, 0.105f, 0.118f), new Vector3(0.026f, 0.03f, 0.03f), skin, 10f);
            Transform mouth = Prim(PrimitiveType.Sphere, "Mouth", head, new Vector3(0f, 0.06f, 0.112f), new Vector3(0.05f, 0.012f, 0.02f), lips).transform;
            Prim(PrimitiveType.Sphere, "LowerLip", head, new Vector3(0f, 0.05f, 0.11f), new Vector3(0.03f, 0.01f, 0.015f), Mat("SnowLipLight", new Color(0.72f, 0.2f, 0.22f), 0f));

            return new Result { head = head, mouth = mouth, eyes = eyes };
        }

        // ---------- 王后：高挑、紫色长袍、立领披风、金色王冠 ----------
        public static Result BuildQueen(Transform model)
        {
            // Blender 做的木偶模型在就用它（PinocchioModel.cs 里的 DollModel），否则用下面代码拼的版本
            if (DollModel.TryBuild("CH_Queen", model, null, false, out Result imported))
            {
                return imported;
            }

            Material skin = Mat("QueenSkin", new Color(0.95f, 0.88f, 0.85f), 0.4f);
            Material gown = Mat("QueenGown", new Color(0.28f, 0.08f, 0.38f), 0.45f);
            Material cape = Mat("QueenCape", new Color(0.06f, 0.04f, 0.07f), 0.5f);
            Material lining = Mat("QueenLining", new Color(0.6f, 0.05f, 0.1f), 0.5f);
            Material veil = Mat("QueenVeil", new Color(0.08f, 0.06f, 0.1f), 0.25f);
            Material gold = Mat("QueenGold", new Color(1f, 0.78f, 0.25f), 0.85f, 1f);
            Material gem = Mat("QueenGem", new Color(0.8f, 0.02f, 0.08f), 0.95f);
            Material lips = Mat("QueenLips", new Color(0.55f, 0.02f, 0.1f), 0.6f);

            // 长裙：腰收得很细、胸口挺，下摆拖地
            Lathe("Gown", model, Vector3.zero, gown, 0f, 0f, 0.34f, 0f, 0.33f, 0.06f, 0.22f, 0.5f, 0.12f, 0.92f, 0.118f, 1.0f, 0.15f, 1.12f, 0.15f, 1.18f, 0.1f, 1.29f, 0f, 1.3f);
            // 裙子正面一道金色滚边
            Prim(PrimitiveType.Cube, "FrontTrim", model, new Vector3(0f, 0.46f, 0.205f), new Vector3(0.025f, 0.9f, 0.006f), gold, new Vector3(-12.6f, 0f, 0f));
            Lathe("Belt", model, Vector3.zero, gold, 0.132f, 0.92f, 0.135f, 0.97f, 0.128f, 0.97f, 0.125f, 0.92f);
            Prim(PrimitiveType.Sphere, "BeltGem", model, new Vector3(0f, 0.945f, 0.135f), new Vector3(0.035f, 0.035f, 0.02f), gem);

            // 披风是后半边，开口朝向玩家
            Prim(PrimitiveType.Sphere, "Cape", model, new Vector3(0f, 0.68f, -0.1f), new Vector3(0.62f, 1.36f, 0.36f), cape);
            LatheArc("Collar", model, new Vector3(0f, 0f, -0.02f), lining, 210f, 0.09f, 1.26f, 0.24f, 1.58f, 0.225f, 1.59f, 0.075f, 1.27f)
                .transform.localScale = new Vector3(1f, 1f, 0.75f);

            // 双臂往身前抱，一副居高临下的样子
            Arms(model, 1.24f, 0.17f, 0.46f, gown, 0.1f, gown, skin, -24f, 55f);
            Prim(PrimitiveType.Cylinder, "Neck", model, new Vector3(0f, 1.33f, 0f), new Vector3(0.065f, 0.05f, 0.065f), skin);
            Lathe("Necklace", model, Vector3.zero, gold, 0.045f, 1.3f, 0.05f, 1.32f, 0.045f, 1.33f, 0.04f, 1.31f);
            Prim(PrimitiveType.Sphere, "Pendant", model, new Vector3(0f, 1.27f, 0.07f), new Vector3(0.03f, 0.04f, 0.015f), gem);

            Transform head = Group("Head", model, new Vector3(0f, 1.35f, 0f));
            Prim(PrimitiveType.Sphere, "Face", head, new Vector3(0f, 0.13f, 0f), new Vector3(0.22f, 0.27f, 0.23f), skin);
            Prim(PrimitiveType.Sphere, "Veil", head, new Vector3(0f, 0.14f, -0.035f), new Vector3(0.26f, 0.3f, 0.26f), veil);
            Prim(PrimitiveType.Sphere, "VeilBack", head, new Vector3(0f, 0.02f, -0.07f), new Vector3(0.24f, 0.26f, 0.16f), veil);

            Transform crown = Group("Crown", head, new Vector3(0f, 0.24f, -0.02f));
            Lathe("Band", crown, Vector3.zero, gold, 0.11f, 0f, 0.115f, 0.05f, 0.105f, 0.05f, 0.1f, 0f);
            for (int i = 0; i < 7; i++)
            {
                float angle = i * Mathf.PI * 2f / 7f + Mathf.PI / 2f;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * 0.108f, 0.045f, Mathf.Sin(angle) * 0.108f);
                Lathe("Spike" + i, crown, pos, gold, 0.022f, 0f, 0.012f, 0.05f, 0f, 0.09f);
            }
            Prim(PrimitiveType.Sphere, "CrownGem", crown, new Vector3(0f, 0.03f, 0.115f), new Vector3(0.035f, 0.045f, 0.02f), gem);

            // 眼皮压一半，看人永远是不耐烦的样子
            // 深紫色眼影的厚眼皮，加两道挑得很高的细眉
            Material eyeShadow = Mat("QueenEyeShadow", new Color(0.42f, 0.28f, 0.5f), 0f);
            Material brow = Mat("QueenBrow", new Color(0.05f, 0.03f, 0.05f), 0f);
            Transform[] eyes = Eyes(head, 0.15f, 0.028f, 0.099f, 0.05f, eyeShadow, 0.45f);
            Lashes(eyes, 0.05f, brow);
            foreach (float side in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, side < 0 ? "BrowL" : "BrowR", head, new Vector3(side * 0.036f, 0.196f, 0.094f),
                    new Vector3(0.05f, 0.006f, 0.006f), brow, new Vector3(0f, side * -18f, side * 16f));
            }
            // 尖一点的鼻子、颧骨上的阴影线、一颗美人痣
            Nose(head, new Vector3(0f, 0.1f, 0.108f), new Vector3(0.022f, 0.045f, 0.04f), skin, 18f);
            foreach (float side in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, side < 0 ? "CheekL" : "CheekR", head, new Vector3(side * 0.06f, 0.085f, 0.088f),
                    new Vector3(0.004f, 0.035f, 0.004f), Mat("QueenCheekLine", new Color(0.7f, 0.58f, 0.6f), 0f), new Vector3(0f, side * -30f, side * -25f));
            }
            Prim(PrimitiveType.Sphere, "BeautyMark", head, new Vector3(0.035f, 0.065f, 0.1f), Vector3.one * 0.008f, brow);
            Transform mouth = Prim(PrimitiveType.Sphere, "Mouth", head, new Vector3(0f, 0.05f, 0.1f), new Vector3(0.05f, 0.012f, 0.02f), lips).transform;

            return new Result { head = head, mouth = mouth, eyes = eyes };
        }

        // ---------- 匹诺曹：木头人偶，看得见关节球，尖帽子，鼻子可伸长 ----------
        public static Result BuildPinocchio(Transform model)
        {
            // Blender 做的正式模型在就用它（PinocchioModel.cs），否则用下面代码拼的版本
            if (PinocchioModel.TryBuild(model, out Result imported))
            {
                return imported;
            }

            Material wood = Mat("PinoWood", new Color(0.86f, 0.64f, 0.42f), 0.35f);
            Material joint = Mat("PinoJoint", new Color(0.62f, 0.42f, 0.25f), 0.4f);
            Material shirt = Mat("PinoShirt", new Color(0.8f, 0.2f, 0.15f), 0.25f);
            Material shorts = Mat("PinoShorts", new Color(0.2f, 0.32f, 0.6f), 0.25f);
            Material hat = Mat("PinoHat", new Color(0.25f, 0.5f, 0.3f), 0.25f);
            Material feather = Mat("PinoFeather", new Color(0.95f, 0.85f, 0.3f), 0.3f);
            Material hair = Mat("PinoHair", new Color(0.3f, 0.18f, 0.08f), 0.4f);
            Material shoe = Mat("PinoShoe", new Color(0.3f, 0.18f, 0.1f), 0.6f);
            Material blush = Mat("PinoBlush", new Color(0.92f, 0.4f, 0.35f), 0.3f);
            Material lips = Mat("PinoLips", new Color(0.45f, 0.15f, 0.1f), 0.4f);

            foreach (float side in new[] { -1f, 1f })
            {
                string s = side < 0 ? "L" : "R";
                float x = side * 0.075f;
                Prim(PrimitiveType.Sphere, "Shoe" + s, model, new Vector3(x, 0.035f, 0.04f), new Vector3(0.1f, 0.07f, 0.17f), shoe);
                Prim(PrimitiveType.Cylinder, "Shin" + s, model, new Vector3(x, 0.16f, 0f), new Vector3(0.06f, 0.1f, 0.06f), wood);
                Prim(PrimitiveType.Sphere, "Knee" + s, model, new Vector3(x, 0.27f, 0f), Vector3.one * 0.075f, joint);
                Prim(PrimitiveType.Cylinder, "Thigh" + s, model, new Vector3(x, 0.36f, 0f), new Vector3(0.065f, 0.08f, 0.065f), wood);
            }

            Lathe("Shorts", model, Vector3.zero, shorts, 0.14f, 0.38f, 0.15f, 0.44f, 0.135f, 0.56f, 0.12f, 0.58f);
            Lathe("Shirt", model, Vector3.zero, shirt, 0.125f, 0.56f, 0.135f, 0.68f, 0.125f, 0.8f, 0.07f, 0.85f, 0f, 0.86f);
            Prim(PrimitiveType.Sphere, "Bowtie", model, new Vector3(0f, 0.83f, 0.08f), new Vector3(0.09f, 0.04f, 0.03f), shorts);
            // 背带：两条蓝色带子从短裤拉到肩膀
            foreach (float side in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, side < 0 ? "StrapL" : "StrapR", model, new Vector3(side * 0.07f, 0.7f, 0.128f),
                    new Vector3(0.025f, 0.28f, 0.006f), shorts, new Vector3(-4f, 0f, side * -6f));
            }
            foreach (float y in new[] { 0.62f, 0.7f })
            {
                Prim(PrimitiveType.Sphere, "Button", model, new Vector3(0f, y, 0.13f), Vector3.one * 0.022f, feather);
            }

            foreach (float side in new[] { -1f, 1f })
            {
                string s = side < 0 ? "L" : "R";
                Prim(PrimitiveType.Sphere, "Shoulder" + s, model, new Vector3(side * 0.15f, 0.8f, 0f), Vector3.one * 0.07f, joint);
                Transform arm = Group("Arm" + s, model, new Vector3(side * 0.15f, 0.8f, 0f));
                // 木偶的胳膊有点僵地往外、往前支着
                arm.localRotation = Quaternion.Euler(-22f, 0f, side * 22f);
                Prim(PrimitiveType.Cylinder, "Upper", arm, new Vector3(0f, -0.08f, 0f), new Vector3(0.045f, 0.07f, 0.045f), wood);
                Prim(PrimitiveType.Sphere, "Elbow", arm, new Vector3(0f, -0.16f, 0f), Vector3.one * 0.055f, joint);
                Prim(PrimitiveType.Cylinder, "Lower", arm, new Vector3(0f, -0.23f, 0f), new Vector3(0.04f, 0.065f, 0.04f), wood);
                Prim(PrimitiveType.Sphere, "Hand", arm, new Vector3(0f, -0.31f, 0f), new Vector3(0.06f, 0.07f, 0.05f), Mat("PinoGlove", Color.white, 0.2f));
            }

            Prim(PrimitiveType.Cylinder, "Neck", model, new Vector3(0f, 0.88f, 0f), new Vector3(0.05f, 0.04f, 0.05f), wood);

            Transform head = Group("Head", model, new Vector3(0f, 0.9f, 0f));
            Prim(PrimitiveType.Sphere, "Face", head, new Vector3(0f, 0.12f, 0f), new Vector3(0.24f, 0.24f, 0.23f), wood);
            Prim(PrimitiveType.Sphere, "Hair", head, new Vector3(0f, 0.15f, -0.03f), new Vector3(0.25f, 0.22f, 0.24f), hair);
            Prim(PrimitiveType.Sphere, "EarL", head, new Vector3(-0.118f, 0.11f, 0f), new Vector3(0.03f, 0.06f, 0.045f), wood);
            Prim(PrimitiveType.Sphere, "EarR", head, new Vector3(0.118f, 0.11f, 0f), new Vector3(0.03f, 0.06f, 0.045f), wood);

            Transform hatGroup = Group("Hat", head, new Vector3(0f, 0.2f, -0.01f));
            hatGroup.localRotation = Quaternion.Euler(-8f, 0f, 6f);
            Lathe("Brim", hatGroup, Vector3.zero, hat, 0f, 0f, 0.19f, 0f, 0.19f, 0.015f, 0.12f, 0.02f, 0f, 0.02f);
            Lathe("Crown", hatGroup, Vector3.zero, hat, 0.11f, 0.01f, 0.1f, 0.08f, 0.06f, 0.15f, 0f, 0.19f);
            Lathe("HatBand", hatGroup, Vector3.zero, shirt, 0.112f, 0.015f, 0.107f, 0.045f, 0f, 0.045f);
            Prim(PrimitiveType.Capsule, "Feather", hatGroup, new Vector3(0.09f, 0.1f, -0.04f), new Vector3(0.02f, 0.09f, 0.04f), feather, new Vector3(0f, 0f, -35f));
            // 帽檐下翘出来几撮木刻的头发
            for (int i = 0; i < 4; i++)
            {
                float a = -0.9f + i * 0.6f;
                Lathe("Tuft" + i, head, new Vector3(Mathf.Sin(a) * 0.11f, 0.19f, Mathf.Cos(a) * 0.06f - 0.02f), hair,
                    0.018f, 0f, 0.01f, 0.03f, 0f, 0.05f).transform.localRotation = Quaternion.Euler(-30f, 0f, -Mathf.Sin(a) * 50f);
            }
            // 脸上的木纹：几道浅浅的弧线
            Material grain = Mat("PinoGrain", new Color(0.72f, 0.52f, 0.33f), 0f);
            Prim(PrimitiveType.Cube, "GrainA", head, new Vector3(-0.085f, 0.17f, 0.075f), new Vector3(0.003f, 0.04f, 0.003f), grain, new Vector3(0f, -40f, 20f));
            Prim(PrimitiveType.Cube, "GrainB", head, new Vector3(0.09f, 0.05f, 0.075f), new Vector3(0.003f, 0.035f, 0.003f), grain, new Vector3(0f, 40f, -25f));

            // 瞪得溜圆，没有眼皮
            Transform[] eyes = Eyes(head, 0.14f, 0.034f, 0.096f, 0.065f, wood, 0f);
            Prim(PrimitiveType.Sphere, "BlushL", head, new Vector3(-0.07f, 0.075f, 0.09f), new Vector3(0.045f, 0.035f, 0.02f), blush);
            Prim(PrimitiveType.Sphere, "BlushR", head, new Vector3(0.07f, 0.075f, 0.09f), new Vector3(0.045f, 0.035f, 0.02f), blush);
            Transform mouth = Prim(PrimitiveType.Sphere, "Mouth", head, new Vector3(0f, 0.045f, 0.095f), new Vector3(0.06f, 0.018f, 0.02f), lips).transform;

            // 鼻子的轴心在鼻根，沿本地 Z 拉长时只往前长，不会戳进脸里
            Transform nose = Group("Nose", head, new Vector3(0f, 0.1f, 0.105f));
            GameObject noseMesh = Lathe("NoseMesh", nose, Vector3.zero, wood, 0.022f, 0f, 0.018f, 0.06f, 0.008f, 0.11f, 0f, 0.115f);
            noseMesh.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Sphere, "NoseTip", nose, new Vector3(0f, 0f, 0.11f), Vector3.one * 0.018f, wood);

            return new Result { head = head, mouth = mouth, nose = nose, eyes = eyes };
        }

        // ---------- 共用的零件 ----------

        static void Arms(Transform model, float shoulderY, float shoulderX, float length,
            Material sleeveMat, float puff, Material armMat, Material handMat, float spread, float forward = 0f)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                string s = side < 0 ? "L" : "R";
                Transform arm = Group("Arm" + s, model, new Vector3(side * shoulderX, shoulderY, 0f));
                arm.localRotation = Quaternion.Euler(-forward, 0f, side * spread);
                Prim(PrimitiveType.Sphere, "Puff", arm, new Vector3(0f, -0.03f, 0f), Vector3.one * puff, sleeveMat);
                Prim(PrimitiveType.Capsule, "Arm", arm, new Vector3(0f, -length * 0.5f, 0f), new Vector3(0.065f, length * 0.5f, 0.065f), armMat);
                // 手：手掌加一根大拇指，比一个球像手
                Prim(PrimitiveType.Sphere, "Hand", arm, new Vector3(0f, -length, 0f), new Vector3(0.055f, 0.07f, 0.035f), handMat);
                Prim(PrimitiveType.Capsule, "Thumb", arm, new Vector3(-side * 0.02f, -length + 0.015f, 0.018f),
                    new Vector3(0.018f, 0.022f, 0.018f), handMat, new Vector3(30f, 0f, side * 35f));
            }
        }

        // 美式动画的眼睛：凸出来的白眼球加一个小黑点瞳孔，瞳孔略往鼻梁靠，显得有点呆。
        // lid 是上眼皮盖住的比例（用皮肤色的半球盖在眼球上半部），0 就是瞪眼。
        // 每只眼睛是一个组，CharacterPuppet 眨眼时把这个组沿 Y 压扁。
        static Transform[] Eyes(Transform head, float y, float x, float z, float size, Material lidMat, float lid)
        {
            Material white = Mat("EyeWhite", new Color(0.98f, 0.98f, 0.96f), 0f);
            Material pupil = Mat("Pupil", new Color(0.02f, 0.02f, 0.02f), 0f);
            var eyes = new Transform[2];
            int k = 0;
            foreach (float side in new[] { -1f, 1f })
            {
                string s = side < 0 ? "L" : "R";
                Transform eye = Group("Eye" + s, head, new Vector3(side * x, y, z));
                Prim(PrimitiveType.Sphere, "White", eye, Vector3.zero, new Vector3(size, size * 1.1f, size * 0.8f), white);
                Prim(PrimitiveType.Sphere, "Pupil", eye, new Vector3(-side * size * 0.08f, -size * 0.02f, size * 0.39f),
                    new Vector3(size * 0.18f, size * 0.18f, size * 0.05f), pupil);
                if (lid > 0f)
                {
                    // 眼皮是同一个眼球外面套一层稍大的"球冠"，只盖住上面 lid 那部分，不会在额头上鼓包。
                    // 先放一个按眼球大小缩放的组，再在里面放单位球冠，描边宽度才能按真实大小算。
                    Transform lidPivot = Group("Lid", eye, Vector3.zero);
                    lidPivot.localScale = new Vector3(size * 1.06f, size * 1.16f, size * 0.88f);
                    float h = 0.5f - lid;
                    var profile = new System.Collections.Generic.List<float> { 0f, h };
                    for (int i = 0; i <= 10; i++)
                    {
                        float py = Mathf.Lerp(h, 0.5f, i / 10f);
                        float r = Mathf.Sqrt(Mathf.Max(0f, 0.25f - py * py));
                        profile.Add(i == 10 ? 0f : r);
                        profile.Add(py);
                    }
                    Lathe("Cap", lidPivot, Vector3.zero, lidMat, profile.ToArray());
                }
                eyes[k++] = eye;
            }
            return eyes;
        }

        // 齐刘海：沿额头一排压扁的发片，越往两边越靠后
        static void Bangs(Transform head, Material hair, float y, float z, int count, float halfWidth)
        {
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0f : i / (count - 1f) * 2f - 1f;
                float x = t * halfWidth;
                Prim(PrimitiveType.Sphere, "Bang" + i, head,
                    new Vector3(x, y - Mathf.Abs(t) * 0.02f, z - t * t * 0.045f),
                    new Vector3(0.05f, 0.065f, 0.035f), hair, new Vector3(-15f, t * 35f, -t * 12f));
            }
        }

        // 上眼皮外侧翘出来的三根睫毛，挂在眼睛组下面，眨眼时一起动
        static void Lashes(Transform[] eyes, float size, Material mat)
        {
            foreach (Transform eye in eyes)
            {
                float side = eye.localPosition.x < 0f ? -1f : 1f;
                for (int i = 0; i < 3; i++)
                {
                    float a = (35f + i * 22f) * Mathf.Deg2Rad;
                    Vector3 pos = new Vector3(side * Mathf.Cos(a) * size * 0.55f, Mathf.Sin(a) * size * 0.6f, size * 0.25f);
                    Prim(PrimitiveType.Cube, "Lash" + i, eye, pos, new Vector3(size * 0.06f, size * 0.25f, size * 0.06f), mat,
                        new Vector3(0f, 0f, side * -(55f - i * 22f)));
                }
            }
        }

        // 卡通鼻子：一个往前下方翘的小鼓包
        static void Nose(Transform head, Vector3 position, Vector3 size, Material skin, float tilt)
        {
            Prim(PrimitiveType.Sphere, "Nose", head, position, size, skin, new Vector3(tilt, 0f, 0f));
        }

        // tilt > 0 外侧下垂显得温和，< 0 内侧下压显得凶
        static void Brows(Transform head, float y, float x, float z, Material mat, float tilt)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                string s = side < 0 ? "L" : "R";
                Prim(PrimitiveType.Sphere, "Brow" + s, head, new Vector3(side * x, y, z),
                    new Vector3(0.045f, 0.009f, 0.015f), mat, new Vector3(0f, side * 15f, side * -tilt));
            }
        }

        internal static Transform Group(string name, Transform parent, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        internal static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPosition,
            Vector3 localScale, Material mat, Vector3? euler = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            if (type == PrimitiveType.Sphere)
            {
                go.GetComponent<MeshFilter>().sharedMesh = SmoothSphere();
            }
            else if (type == PrimitiveType.Cylinder)
            {
                go.GetComponent<MeshFilter>().sharedMesh = SmoothCylinder();
            }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            if (euler.HasValue)
            {
                go.transform.localRotation = Quaternion.Euler(euler.Value);
            }
            go.GetComponent<MeshRenderer>().sharedMaterial = Outlined(mat, go.transform);
            return go;
        }

        // 旋转体：profile 依次给出 (半径, 高度)，自下而上绕 Y 轴旋转一圈。
        // 相邻重复的点会形成硬边。法线朝向沿轮廓前进方向的右侧，所以外轮廓要从下往上写。
        internal static GameObject Lathe(string name, Transform parent, Vector3 localPosition, Material mat, params float[] profile)
        {
            return LatheArc(name, parent, localPosition, mat, 360f, profile);
        }

        // 只转 arcDegrees 度，开口朝向玩家（+Z），用于立领这种只在脑后的部件
        internal static GameObject LatheArc(string name, Transform parent, Vector3 localPosition, Material mat,
            float arcDegrees, params float[] profile)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.AddComponent<MeshFilter>().sharedMesh = LatheMesh(parent.name + "_" + name, profile, 48, arcDegrees);
            go.AddComponent<MeshRenderer>().sharedMaterial = Outlined(mat, go.transform);
            return go;
        }

        // 场景里自定义的网格（比如镜子碎片）也存到 Generated/Meshes，场景才能引用到
        internal static Mesh SaveMesh(Mesh mesh)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath(MeshFolder + "/" + mesh.name + ".asset");
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // 和 Unity 自带球一样：直径 1，中心在原点
        static Mesh SmoothSphere()
        {
            if (smoothSphere != null)
            {
                return smoothSphere;
            }

            const int rings = 24;
            var profile = new float[(rings + 1) * 2];
            for (int i = 0; i <= rings; i++)
            {
                float t = Mathf.PI * i / rings;
                profile[i * 2] = i == 0 || i == rings ? 0f : Mathf.Sin(t) * 0.5f;
                profile[i * 2 + 1] = -Mathf.Cos(t) * 0.5f;
            }
            smoothSphere = LatheMesh("SmoothSphere", profile, 40);
            return smoothSphere;
        }

        // 和 Unity 自带圆柱一样：直径 1，高 2；上下两圈各重复一次，让侧面和端面之间是硬边
        static Mesh SmoothCylinder()
        {
            if (smoothCylinder == null)
            {
                smoothCylinder = LatheMesh("SmoothCylinder",
                    new[] { 0f, -1f, 0.5f, -1f, 0.5f, -1f, 0.5f, 1f, 0.5f, 1f, 0f, 1f }, 56);
            }
            return smoothCylinder;
        }

        static Mesh LatheMesh(string name, float[] profile, int segments = 48, float arcDegrees = 360f)
        {
            float arc = arcDegrees * Mathf.Deg2Rad;
            float start = arcDegrees >= 360f ? 0f : 1.5f * Mathf.PI - arc * 0.5f;
            int rings = profile.Length / 2;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int i = 0; i < rings; i++)
            {
                float r = profile[i * 2];
                float y = profile[i * 2 + 1];
                for (int j = 0; j <= segments; j++)
                {
                    float a = start + j * arc / segments;
                    vertices.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
                    uvs.Add(new Vector2((float)j / segments, (float)i / (rings - 1)));
                }
            }

            int stride = segments + 1;
            for (int i = 0; i < rings - 1; i++)
            {
                for (int j = 0; j < segments; j++)
                {
                    int a = i * stride + j;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;
                    triangles.AddRange(new[] { a, c, d, a, d, b });
                }
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            string path = AssetDatabase.GenerateUniqueAssetPath(MeshFolder + "/" + name + ".asset");
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // 二次元着色：装了 Unity Toon Shader 就用赛璐璐两段阴影 + 描边，没装就退回 URP Lit
        static Shader ToonShader => Shader.Find("Toon/Toon");

        // 描边宽度，单位毫米（世界空间）。Toon Shader 的描边在物体空间里算，所以要按物体缩放换算
        const float OutlineMillimeters = 4.5f;

        internal static Material Mat(string name, Color color, float smoothness, float metallic = 0f)
        {
            if (materials.TryGetValue(name, out Material cached))
            {
                return cached;
            }

            Material mat;
            Shader toon = ToonShader;
            if (toon != null)
            {
                mat = new Material(toon) { name = name };
                // 美式动画：阴影只比底色暗一点、边缘硬切，没有高光和边缘光，靠粗黑描边撑轮廓
                Color shade = new Color(color.r * 0.88f, color.g * 0.85f, Mathf.Min(1f, color.b * 0.9f + 0.02f));
                mat.SetColor("_BaseColor", color);
                mat.SetColor("_1st_ShadeColor", shade);
                mat.SetColor("_2nd_ShadeColor", shade);
                mat.SetFloat("_BaseColor_Step", 0.3f);
                mat.SetFloat("_BaseShade_Feather", 0.0001f);
                mat.SetFloat("_ShadeColor_Step", 0f);
                mat.SetFloat("_1st2nd_Shades_Feather", 0.0001f);
                mat.SetFloat("_1st_ShadeColor_Step", 0.3f);
                mat.SetFloat("_1st_ShadeColor_Feather", 0.0001f);
                mat.SetFloat("_2nd_ShadeColor_Step", 0f);
                mat.SetFloat("_2nd_ShadeColor_Feather", 0.0001f);
                mat.SetColor("_HighColor", Color.black);
                mat.SetFloat("_HighColor_Power", 0f);
                mat.SetFloat("_RimLight", 0f);
                mat.SetFloat("_GI_Intensity", 0.4f);
                mat.SetFloat("_Outline_Width", 0f);
                mat.SetColor("_Outline_Color", new Color(0.04f, 0.035f, 0.04f));
                // 底纹只给房间和镜子用；人物脸上铺了会显得脏
                bool textured = name.StartsWith("Room") || name.StartsWith("Mirror");
                Texture2D paper = textured ? AssetDatabase.LoadAssetAtPath<Texture2D>(PaperTexturePath) : null;
                if (paper != null)
                {
                    // 亮面和暗面都乘上同一张底纹；房间的大块墙面地面铺得更密，不然纹理会被拉得很大
                    mat.SetTexture("_MainTex", paper);
                    mat.SetTexture("_1st_ShadeMap", paper);
                    mat.SetTexture("_2nd_ShadeMap", paper);
                    float tiling = name.StartsWith("Room") ? 3f : 1f;
                    mat.SetTextureScale("_MainTex", new Vector2(tiling, tiling));
                }
                mat.SetFloat("_Is_LightColor_Outline", 0f);
            }
            else
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Smoothness", smoothness);
                mat.SetFloat("_Metallic", metallic);
            }

            AssetDatabase.CreateAsset(mat, MaterialFolder + "/" + name + ".mat");
            materials[name] = mat;
            return mat;
        }

        // 同一个颜色的零件大小差很多，描边宽度按缩放分档各建一份材质；眼睛、嘴这类小零件不描边
        internal static Material Outlined(Material baseMat, Transform t)
        {
            if (baseMat.shader != ToonShader)
            {
                return baseMat;
            }

            Vector3 s = t.lossyScale;
            float size = (Mathf.Abs(s.x) + Mathf.Abs(s.y) + Mathf.Abs(s.z)) / 3f;
            if (size < 0.03f)
            {
                return baseMat;
            }

            float width = Mathf.Round(OutlineMillimeters / size * 2f) / 2f;
            string key = baseMat.name + "_Outline" + width.ToString("0.0").Replace('.', '_');
            if (materials.TryGetValue(key, out Material cached))
            {
                return cached;
            }

            var mat = new Material(baseMat) { name = key };
            mat.SetFloat("_Outline_Width", width);
            AssetDatabase.CreateAsset(mat, MaterialFolder + "/" + key + ".mat");
            materials[key] = mat;
            return mat;
        }
    }
}
