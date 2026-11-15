#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace NightShift.EditorBuild
{
    public static class HumanRoleImports
    {
        public static void ImportAll()
        {
            ImportBoss();
            ImportColleague();
            ImportIntern();
        }

        public static void ImportBoss()=>HumanModelLibrary.ImportHuman("Boss",new HumanImportOverrides
            {EyeDiffuse=Texture("Boss","low_poly_eyes.png")});

        public static void ImportColleague()=>ImportVariant("Colleague", "middleage_darkskinned_male_diffuse.png", "male_casualsuit01", "afro", "brown_eye_opaque.png");

        public static void ImportIntern()=>ImportVariant("Intern", "young_lightskinned_male_diffuse3.png", "male_casualsuit03", "short03", "bluegreen_eye_opaque.png");

        static void ImportVariant(string role, string skin, string garment, string hair, string eyes)
        {
            var overrides=new HumanImportOverrides
            {
                BodyDiffuse=Texture(role, skin),
                GarmentDiffuse=Texture(role, garment + "_diffuse.png"),
                GarmentNormal = Texture(role,garment + "_normal.png"),
                ShoesDiffuse = Texture(role, "shoes01_diffuse.png"),
                ShoesNormal = Texture(role, "shoes01_normal.png"),
                HairDiffuse=Texture(role, hair + "_diffuse.png"),
                EyebrowsDiffuse = Texture(role, "eyebrow001.png"),
                EyelashesDiffuse = Texture(role, "eyelashes01.png"),
                TeethDiffuse = Texture(role, "teeth_opaque.png"),
                EyeDiffuse=Texture(role, eyes)
            };
            HumanModelLibrary.ImportHuman(role, overrides);
        }

        static Texture2D Texture(string role, string file)
        {
            string path="Assets/Art/Humans/" + role + "/" + role + ".fbm/" + file;
            if (!System.IO.File.Exists(path) && file.EndsWith("_diffuse.png", StringComparison.OrdinalIgnoreCase))
                path=path.Substring(0, path.Length - 4) + "_opaque.png";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) throw new InvalidOperationException("HUMANIMPORT missing verified texture " + path);
            return texture;
        }
    }
}
#endif
