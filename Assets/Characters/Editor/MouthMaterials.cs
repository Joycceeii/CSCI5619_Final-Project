// Matte, dark materials for Wukong's mouth cavity / tongue (the FBX's own imported ones come out glossy black).
using System.Text;
using UnityEditor;
using UnityEngine;

public static class MouthMaterials
{
    public static void Ensure(StringBuilder log)
    {
        foreach (var (folder, key) in new[] { ("Wukong", "wukong"), ("Bajie", "bajie"), ("Shaseng", "shaseng") }) Ensure(log, folder, key);
    }
    static void Ensure(StringBuilder log, string folder, string key)
    {
        string fbx = $"Assets/Characters/{folder}/{key}_rigged.fbx";
        var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
        if (mi == null) return;
        Material Make(string name, Color c, float smooth)
        {
            string p = $"Assets/Characters/{folder}/Materials/{name}.mat"; System.IO.Directory.CreateDirectory($"Assets/Characters/{folder}/Materials");
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, p); }
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0);
            EditorUtility.SetDirty(m); return m;
        }
        var mouth = Make($"{key}_mouth_Lit", new Color(0.16f, 0.025f, 0.03f), 0.12f);
        var tongue = Make($"{key}_tongue_Lit", new Color(0.55f, 0.16f, 0.18f), 0.3f);
        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), $"{key}_mouth_mat"), mouth);
        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), $"{key}_tongue_mat"), tongue);
        mi.SaveAndReimport();
        log?.AppendLine($"[{folder}] mouth/tongue materials remapped (matte)");
    }
}
