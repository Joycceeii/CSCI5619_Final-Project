using UnityEditor;
using UnityEngine;

namespace WhoseStory.EditorTools
{
    // Art 文件夹里的立绘自动设成 Sprite：立绘的轴心在脚底中间，鼻子贴图的轴心在鼻根（左侧中间）。
    public class MangaArtImporter : AssetPostprocessor
    {
        public const string ArtFolder = "Assets/WhoseStory/Art/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtFolder))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = assetPath.EndsWith("/nose.png")
                ? (int)SpriteAlignment.LeftCenter
                : (int)SpriteAlignment.BottomCenter;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
    }
}
