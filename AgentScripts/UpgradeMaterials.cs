using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NightShift.EditorTools
{
    public static class UpgradeMaterials
    {
        
        static readonly (string tex,string mat,float u,float v)[] Map =
        {
            ("dirty_carpet_diff_1k.jpg", "carpetFloor", 8f, 8f),
            ("ceiling_interior_diff_1k.jpg", "ceilingMat",8f,8f),
            ("plastered_wall_diff_1k.jpg", "wallPaint", 4f, 1.5f),
            ("cotton_jersey_diff_1k.jpg", "cubicleFabric",2f,1f),
        };

        public static string Run()
        {
            var sb = new System.Text.StringBuilder();
            string srcDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Reference/Textures"));
            if (!Directory.Exists(srcDir)) srcDir = Path.Combine(Directory.GetCurrentDirectory(), "../Reference/Textures");
            if (!Directory.Exists(srcDir)) return "ERROR textures not found at " + srcDir;

            if (!AssetDatabase.IsValidFolder("Assets/Textures"))
                AssetDatabase.CreateFolder("Assets", "Textures");

            foreach (var (texFile, matName, u, v) in Map)
            {
                string src = Path.Combine(srcDir, texFile);
                if (!File.Exists(src))
                { sb.AppendLine("MISSING " + texFile); continue; }
                string dst="Assets/Textures/" + texFile;
                File.Copy(src,dst,true);
                AssetDatabase.ImportAsset(dst,ImportAssetOptions.ForceUpdate);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
                var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/" + matName + ".mat");
                if (tex == null || mat == null)
                { sb.AppendLine($"SKIP {matName}: tex={tex != null} mat={mat != null}"); continue; }
                mat.SetTexture("_BaseMap",tex);
                mat.SetTextureScale("_BaseMap", new Vector2(u, v));
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.08f);
                EditorUtility.SetDirty(mat);
                sb.AppendLine($"OK {matName} <- {texFile} tile {u}x{v}");
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            return sb.ToString();
        }
    }
}
