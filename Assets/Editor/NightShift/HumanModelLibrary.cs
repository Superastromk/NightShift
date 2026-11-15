#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using NightShift.Monster;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UObject=UnityEngine.Object;

namespace NightShift.EditorBuild
{
    public enum HumanMaterialKind
    {
        Body,
        Garment,
        Shoes,
        Hair,
        Eyebrows,
        Eyelashes,
        Teeth,
        Eyes
    }

public sealed class HumanImportOverrides
    {
        public Texture2D BodyDiffuse, BodyNormal;
        public Texture2D GarmentDiffuse, GarmentNormal;
        public Texture2D ShoesDiffuse, ShoesNormal;
        public Texture2D HairDiffuse, HairNormal;
        public Texture2D EyebrowsDiffuse, EyebrowsNormal;
        public Texture2D EyelashesDiffuse, EyelashesNormal;
        public Texture2D TeethDiffuse, TeethNormal;
        public Texture2D EyeDiffuse, EyeNormal;

        public Color EyeScleraColor=new Color(0.90f, 0.94f, 0.98f, 1f);
        public Color EyeIrisColor=new Color(0.16f, 0.28f, 0.36f, 1f);

        readonly Dictionary<string, HumanMaterialKind> _materialKinds =
            new Dictionary<string, HumanMaterialKind>(StringComparer.OrdinalIgnoreCase);

        
        public HumanImportOverrides BindMaterial(string importedMaterialName, HumanMaterialKind kind)
        {
            if (string.IsNullOrWhiteSpace(importedMaterialName))
                throw new ArgumentException("An imported material name is required.",nameof(importedMaterialName));
            if (!Enum.IsDefined(typeof(HumanMaterialKind),kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            _materialKinds[importedMaterialName] = kind;
            return this;
        }

        internal bool TryGetMaterialKind(string importedMaterialName,out HumanMaterialKind kind) =>
            _materialKinds.TryGetValue(importedMaterialName,out kind);
    }

public static class HumanModelLibrary
    {
        const string HumanRoot = "Assets/Art/Humans";
        const float TargetHeight = 1.8f;
        const float HeightTolerance = 0.005f;
        const float AlphaCutoff = 0.5f;

        static readonly string[] RequiredBones =
        {
            "pelvis", "spine_01", "neck_01", "head",
            "upperarm_l", "lowerarm_l", "hand_l",
            "upperarm_r", "lowerarm_r", "hand_r",
            "thigh_l", "calf_l", "foot_l", "ball_l",
            "thigh_r", "calf_r", "foot_r", "ball_r"
        };

        sealed class RigInfo
        {
            public int BoneCount;
            public int SkinnedRendererCount;
            public string AvatarName;
        }

        sealed class TextureEntry
        {
            public string Path;
            public string Stem;
            public Texture2D Texture;
            public bool IsNormal;
        }

        sealed class MaterialBinding
        {
            public Material Source;
            public Material Generated;
            public HumanMaterialKind Kind;
        }

        
        public static void ImportHuman(string role) => ImportHuman(role, null);

public static void ImportHuman(string role, HumanImportOverrides overrides) =>
            ImportCore(role, role, overrides);

        
        public static HumanRigBinder Attach(FigureAnimator figure, string role, int layer) =>
            AttachCore(figure, role, role, layer);

public static HumanRigBinder Probe(FigureAnimator figure)
        {
            if (figure == null) throw Error("Probe requires a FigureAnimator.");
            return Probe(figure,figure.gameObject.layer);
        }

        
        public static HumanRigBinder Probe(FigureAnimator figure,int layer)
        {
            Debug.LogWarning("HUMANIMPORT Probe uses the temporary female_casualsuit02 garment; do not ship it as a final role costume.");
            ImportCore("Probe", "probe", null);
            return AttachCore(figure, "Probe", "probe", layer);
        }

        static void ImportCore(string role, string fileStem, HumanImportOverrides overrides)
        {
            ValidateRole(role);
            string roleFolder = HumanRoot + "/" + role;
            string textureFolder = roleFolder + "/" + fileStem + ".fbm";
            string fbxPath = roleFolder + "/" + fileStem + ".fbx";

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (!AssetDatabase.IsValidFolder(roleFolder))
                throw Error("Missing role folder '" + roleFolder + "'. Copy the FBX and its sibling .fbm folder into the current project first.");
            if (!AssetDatabase.IsValidFolder(textureFolder))
                throw Error("Missing texture folder '" + textureFolder + "'. External textures must be copied beside " + fileStem + ".fbx; no fallback textures are used.");

            var importer=AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
                throw Error("No ModelImporter found at '" + fbxPath + "'. Expected Assets/Art/Humans/" + role + "/" + fileStem + ".fbx.");

            ConfigureTextures(textureFolder);
            ConfigureModelImporter(importer);
            ClearMaterialRemaps(importer);
            importer.SaveAndReimport();

            GameObject model=LoadModel(fbxPath);
            Avatar avatar=LoadSingleAvatar(fbxPath);
            ValidateRig(model, avatar, fbxPath);

            NormalizeImportedHeight(importer, model, avatar, fbxPath,
                out float initialHeight, out float finalHeight, out float finalImportScale);

            model=LoadModel(fbxPath);
            avatar=LoadSingleAvatar(fbxPath);
            RigInfo rig=ValidateRig(model, avatar, fbxPath);
            int materialCount=RebuildMaterials(importer, model, role, roleFolder, textureFolder, overrides);

            model=LoadModel(fbxPath);
            avatar=LoadSingleAvatar(fbxPath);
            rig=ValidateRig(model, avatar, fbxPath);
            ValidatePreparedMaterials(model, roleFolder, fbxPath);
            if (importer.optimizeGameObjects)
                throw Error("OptimizeGameObjects became enabled for '" + fbxPath + "'. It must remain false so HumanRigBinder can address every bone.");
            if (Mathf.Abs(importer.globalScale - finalImportScale) > 0.00001f)
                throw Error("Import scale changed unexpectedly for '" + fbxPath + "'.");

            AssetDatabase.SaveAssets();
            Debug.Log("HUMANIMPORT prepared '" + fbxPath + "' height=" + initialHeight.ToString("F4") +
                "m -> " + finalHeight.ToString("F4") + "m, importScale=" + finalImportScale.ToString("F6") +
                ", avatar=" + rig.AvatarName + ", bones=" + rig.BoneCount +
                ", skinnedRenderers=" + rig.SkinnedRendererCount + ", materials=" + materialCount +
                ". Editor validation only; in-game appearance still requires capture.");
        }

        static void ConfigureModelImporter(ModelImporter importer)
        {
            importer.animationType=ModelImporterAnimationType.Human;
            importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.autoGenerateAvatarMappingIfUnspecified=true;
            importer.optimizeGameObjects=false;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            importer.materialName=ModelImporterMaterialName.BasedOnMaterialName;
            importer.materialSearch=ModelImporterMaterialSearch.Local;
            importer.materialLocation=ModelImporterMaterialLocation.InPrefab;
            if (!IsFinite(importer.globalScale) || importer.globalScale <= 0f)
                importer.globalScale=1f;
        }

        static void ClearMaterialRemaps(ModelImporter importer)
        {
            var remove=new List<AssetImporter.SourceAssetIdentifier>();
            foreach (var pair in importer.GetExternalObjectMap())
                if (pair.Value is Material) remove.Add(pair.Key);
            foreach (var identifier in remove) importer.RemoveRemap(identifier);
        }

        static void ConfigureTextures(string textureFolder)
        {
            var paths=FindTexturePaths(textureFolder);
            if (paths.Count == 0)
                throw Error("No Texture2D assets found under '" + textureFolder + "'.");

            foreach (string path in paths)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    throw Error("Texture importer unavailable for '" + path + "'.");

                string stem=Path.GetFileNameWithoutExtension(path);
                bool normal=IsNormalTexture(stem);
                bool cutout=!normal && IsCutoutTexture(stem);
                importer.textureType=normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture=!normal;
                importer.alphaSource=TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency=cutout;
                importer.mipMapsPreserveCoverage=cutout;
                importer.alphaTestReferenceValue=AlphaCutoff;
                importer.SaveAndReimport();
            }
        }

        static void NormalizeImportedHeight(ModelImporter importer, GameObject model, Avatar avatar, string fbxPath,
            out float initialHeight, out float finalHeight, out float finalImportScale)
        {
            initialHeight=MeasureAssetHeight(model);
            finalHeight=initialHeight;
            finalImportScale=importer.globalScale;

            for (int attempt=0; attempt < 3 && Mathf.Abs(finalHeight - TargetHeight) > HeightTolerance; attempt++)
            {
                if (!IsFinite(finalHeight) || finalHeight <= 0.001f)
                    throw Error("Cannot normalize '" + fbxPath + "': measured visible height is " + finalHeight + "m.");

                float nextScale = importer.globalScale * TargetHeight / finalHeight;
                if (!IsFinite(nextScale) || nextScale <= 0f)
                    throw Error("Cannot normalize '" + fbxPath + "': calculated import scale " + nextScale + " is not positive and finite.");
                importer.globalScale = nextScale;
                importer.SaveAndReimport();

                model = LoadModel(fbxPath);
                avatar = LoadSingleAvatar(fbxPath);
                ValidateRig(model, avatar, fbxPath);
                finalHeight = MeasureAssetHeight(model);
                finalImportScale = importer.globalScale;
            }

            if (Mathf.Abs(finalHeight - TargetHeight) > HeightTolerance)
                throw Error("Height normalization failed for '" + fbxPath + "': measured " + finalHeight.ToString("F4") +
                    "m after 3 imports; target is " + TargetHeight.ToString("F3") + "m ± " + HeightTolerance.ToString("F3") + "m.");
        }

        static int RebuildMaterials(ModelImporter importer, GameObject model, string role,
            string roleFolder, string textureFolder, HumanImportOverrides overrides)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
                throw Error("Universal Render Pipeline/Lit is unavailable. Enable the project's URP renderer before importing humans; no package or shader fallback was added.");

            var entries=new List<TextureEntry>();
            foreach (string path in FindTexturePaths(textureFolder))
            {
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) throw Error("Failed to load Texture2D at '" + path + "'.");
                string stem = Path.GetFileNameWithoutExtension(path);
                entries.Add(new TextureEntry { Path = path, Stem = stem, Texture = texture, IsNormal = IsNormalTexture(stem) });
            }

            var diffuseCache = new Dictionary<HumanMaterialKind, Texture2D>();
            var normalCache = new Dictionary<HumanMaterialKind, Texture2D>();
            var bindings = new List<MaterialBinding>();
            var bySourceId = new Dictionary<Material, MaterialBinding>();
            var kindBySourceName = new Dictionary<string, HumanMaterialKind>(StringComparer.OrdinalIgnoreCase);
            var sourceByAssetPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    Material source = materials[slot];
                    if (source == null)
                        throw Error("Null source material on '" + renderer.name + "' slot " + slot + " in '" + importer.assetPath + "'.");

                    HumanMaterialKind kind = ClassifyMaterial(renderer, source, overrides);
                    if (kindBySourceName.TryGetValue(source.name, out HumanMaterialKind previousKind) && previousKind != kind)
                        throw Error("Imported material '" + source.name + "' resolves to both " + previousKind + " and " + kind +
                            ". Give the FBX materials unique names or correct the explicit BindMaterial overrides.");
                    kindBySourceName[source.name] = kind;

                    if (bySourceId.ContainsKey(source)) continue;

                    if (!diffuseCache.TryGetValue(kind, out Texture2D diffuse))
                    {
                        diffuse = ResolveTexture(kind, false, kind != HumanMaterialKind.Eyes,
                            entries, textureFolder, overrides);
                        diffuseCache.Add(kind, diffuse);
                    }
                    if (!normalCache.TryGetValue(kind, out Texture2D normal))
                    {
                        normal = ResolveTexture(kind, true, false, entries, textureFolder, overrides);
                        normalCache.Add(kind, normal);
                    }

                    string assetPath = roleFolder + "/" + role + "_" + SanitizeFileName(source.name) + ".mat";
                    if (sourceByAssetPath.TryGetValue(assetPath,out string otherSource) &&
                        !string.Equals(otherSource,source.name,StringComparison.OrdinalIgnoreCase))
                        throw Error("Material filenames collide at '" + assetPath + "' for '" + otherSource + "' and '" + source.name + "'.");
                    sourceByAssetPath[assetPath] = source.name;

                    Material generated = CreateOrUpdateMaterial(assetPath,lit,kind,source.name,
                        diffuse,normal,overrides);
                    if (kind == HumanMaterialKind.Eyes && diffuse == null)
                    {
                        string fallback = IsIrisMaterial(source.name) ? "blue-gray iris" : "light sclera";
                        Debug.LogWarning("HUMANIMPORT Procedural eye material '" + source.name + "' has no eye diffuse texture; using a " + fallback +
                            " URP/Lit fallback. Copy a verified texture into '" + textureFolder + "' and assign HumanImportOverrides.EyeDiffuse to override it.");
                    }

                    var binding = new MaterialBinding { Source = source, Generated = generated, Kind = kind };
                    bindings.Add(binding);
                    bySourceId.Add(source, binding);
                }
            }

            if (bindings.Count == 0)
                throw Error("No skinned material bindings were created for '" + importer.assetPath + "'.");

            AssetDatabase.SaveAssets();
            foreach (var binding in bindings)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(binding.Source),binding.Generated);
            importer.SaveAndReimport();
            return bindings.Count;
        }

        static Material CreateOrUpdateMaterial(string assetPath,Shader lit,HumanMaterialKind kind,
            string sourceName,Texture2D diffuse,Texture2D normal,HumanImportOverrides overrides)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                    throw Error("Cannot create material at '" + assetPath + "' because another asset type already occupies that path.");
                material = new Material(lit)
                { name = Path.GetFileNameWithoutExtension(assetPath) };
                AssetDatabase.CreateAsset(material, assetPath);
            }
            else
            {
                material.shader = lit;
            }

            bool cutout = (kind == HumanMaterialKind.Hair && sourceName.IndexOf("opaque",StringComparison.OrdinalIgnoreCase)<0) ||
                kind == HumanMaterialKind.Eyebrows || kind == HumanMaterialKind.Eyelashes;
            Color tint=Color.white;
            if (kind == HumanMaterialKind.Eyes && diffuse == null)
            {
                if (IsIrisMaterial(sourceName))
                    tint=overrides != null ? overrides.EyeIrisColor : new Color(0.16f, 0.28f, 0.36f, 1f);
                else
                    tint=overrides != null ? overrides.EyeScleraColor : new Color(0.90f, 0.94f, 0.98f, 1f);
            }

            SetTextureIfPresent(material, "_BaseMap",diffuse);
            SetTextureIfPresent(material, "_MainTex",diffuse);
            SetColorIfPresent(material, "_BaseColor", tint);
            SetColorIfPresent(material, "_Color", tint);
            SetTextureIfPresent(material, "_BumpMap",normal);
            SetFloatIfPresent(material, "_BumpScale",1f);
            if (normal != null) material.EnableKeyword("_NORMALMAP");
            else material.DisableKeyword("_NORMALMAP");

            SetTextureIfPresent(material, "_MetallicGlossMap",null);
            SetTextureIfPresent(material, "_SpecGlossMap",null);
            SetTextureIfPresent(material, "_EmissionMap", null);
            SetColorIfPresent(material, "_EmissionColor", Color.black);
            material.DisableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_EMISSION");
            SetFloatIfPresent(material, "_WorkflowMode", 1f);
            SetFloatIfPresent(material, "_Metallic", 0f);
            SetFloatIfPresent(material, "_Smoothness",SmoothnessFor(kind));
            SetFloatIfPresent(material, "_Glossiness",SmoothnessFor(kind));
            SetFloatIfPresent(material, "_SmoothnessTextureChannel", 0f);
            SetFloatIfPresent(material, "_SpecularHighlights", cutout ? 0f : 1f);
            SetFloatIfPresent(material, "_EnvironmentReflections",cutout ? 0f : 1f);
            if(cutout)
            {
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                SetColorIfPresent(material, "_SpecColor",Color.black);
            }
            else
            {
                material.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
                material.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                SetColorIfPresent(material, "_SpecColor",new Color(0.04f,0.04f,0.04f,1f));
            }
            material.DisableKeyword("_RECEIVE_SHADOWS_OFF");

            material.SetOverrideTag("RenderType",cutout ? "TransparentCutout" : "Opaque");
            SetFloatIfPresent(material, "_Surface", 0f);
            SetFloatIfPresent(material, "_Blend",0f);
            SetFloatIfPresent(material, "_AlphaClip",cutout ? 1f : 0f);
            SetFloatIfPresent(material, "_Cutoff", AlphaCutoff);
            SetFloatIfPresent(material, "_SrcBlend", (float)BlendMode.One);
            SetFloatIfPresent(material, "_DstBlend",(float)BlendMode.Zero);
            SetFloatIfPresent(material, "_ZWrite",1f);
            SetFloatIfPresent(material, "_Cull", (float)(kind==HumanMaterialKind.Hair || cutout ? CullMode.Off : CullMode.Back));
            if (cutout) material.EnableKeyword("_ALPHATEST_ON");
            else material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue=cutout ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            material.SetShaderPassEnabled("ShadowCaster",true);
            material.enableInstancing=true;
            EditorUtility.SetDirty(material);
            return material;
        }

        static HumanRigBinder AttachCore(FigureAnimator figure,string role,string fileStem,int layer)
        {
            if (figure == null) throw Error("Attach requires a FigureAnimator.");
            ValidateRole(role);
            if (layer < 0 || layer > 31)
                throw Error("Attach layer must be in Unity's 0..31 range; received " + layer + ".");

            string roleFolder=HumanRoot + "/" + role;
            string fbxPath=roleFolder + "/" + fileStem + ".fbx";
            GameObject prepared = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (prepared == null)
                throw Error("Prepared FBX not found at '" + fbxPath + "'. Call HumanModelLibrary.ImportHuman(\"" + role + "\") first.");
            Avatar avatar = LoadSingleAvatar(fbxPath);
            ValidateRig(prepared,avatar,fbxPath);
            ValidatePreparedMaterials(prepared,roleFolder,fbxPath);

            
            var proxyRenderers = figure.GetComponentsInChildren<MeshRenderer>(true);
            GameObject attachmentRoot = null;
            try
            {
                
                
                attachmentRoot = new GameObject(role + "_Human");
                attachmentRoot.transform.localPosition = Vector3.zero;
                attachmentRoot.transform.localRotation = Quaternion.identity;
                attachmentRoot.transform.localScale = Vector3.one;

                var model = (GameObject)PrefabUtility.InstantiatePrefab(prepared);
                if (model == null) throw Error("Failed to instantiate prepared FBX '" + fbxPath + "'.");
                model.name=role + "_Model";
                model.transform.SetParent(attachmentRoot.transform,false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;

                foreach (var animator in model.GetComponentsInChildren<Animator>(true))
                {
                    animator.applyRootMotion = false;
                    animator.enabled = false;
                }

                Bounds raw = MeasureVisibleBounds(attachmentRoot);
                float rawHeight = raw.size.y;
                if (!IsFinite(rawHeight) || rawHeight <= 0.001f)
                    throw Error("Cannot attach '" + fbxPath + "': visible skinned height is " + rawHeight + "m.");
                float scale=TargetHeight / rawHeight;
                if (!IsFinite(scale) || scale <= 0f)
                    throw Error("Cannot attach '" + fbxPath + "': calculated root scale " + scale + " is invalid.");

                attachmentRoot.transform.localScale=Vector3.one * scale;
                model.transform.localPosition=new Vector3(0f, -raw.min.y, 0f);
                Bounds grounded=MeasureVisibleBounds(attachmentRoot);
                if (Mathf.Abs(grounded.size.y - TargetHeight) > HeightTolerance ||
                    Mathf.Abs(grounded.min.y) > HeightTolerance)
                    throw Error("Grounding failed for '" + fbxPath + "': height=" + grounded.size.y.ToString("F4") +
                        "m, minY=" + grounded.min.y.ToString("F4") + "m; expected 1.8000m and 0.0000m before FigureSpec scale.");

                attachmentRoot.transform.SetParent(figure.transform,false);
                attachmentRoot.transform.localPosition = Vector3.zero;
                attachmentRoot.transform.localRotation = Quaternion.identity;
                attachmentRoot.transform.localScale = Vector3.one * scale;
                
                foreach (var transform in attachmentRoot.GetComponentsInChildren<Transform>(true))
                    transform.gameObject.layer = layer;

                var binder = figure.GetComponent<HumanRigBinder>();
                if (binder == null) binder = figure.gameObject.AddComponent<HumanRigBinder>();
                binder.JawLimit=role=="Boss"?0f:28f;
                binder.Configure(figure,attachmentRoot);

                foreach (var proxy in proxyRenderers)
                    if (proxy != null) proxy.enabled = false;

                Debug.Log("HUMANIMPORT attached '" + fbxPath + "' to '" + figure.name + "' height=" +
                    grounded.size.y.ToString("F4") + "m, groundMinY=" + grounded.min.y.ToString("F4") +
                    "m, localScale=" + scale.ToString("F6") + ", layer=" + layer +
                    ". Runtime binding and appearance require parent integration capture.");
                return binder;
            }
            catch
            {
                if (attachmentRoot != null) UObject.DestroyImmediate(attachmentRoot);
                throw;
            }
        }

        static RigInfo ValidateRig(GameObject model,Avatar avatar,string fbxPath)
        {
            if (avatar == null)
                throw Error("No Avatar sub-asset found in '" + fbxPath + "'. Use Human/CreateFromThisModel with automatic mapping; no procedural fallback is permitted.");
            if (!avatar.isValid || !avatar.isHuman)
                throw Error("Avatar '" + avatar.name + "' in '" + fbxPath + "' is valid=" + avatar.isValid +
                    ", human=" + avatar.isHuman + ". Fix the FBX skeleton or human mapping before attaching it.");

            var renderers=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length == 0)
                throw Error("No SkinnedMeshRenderer found in '" + fbxPath + "'. A static mesh cannot be driven by HumanRigBinder.");

            var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var transform in model.GetComponentsInChildren<Transform>(true)) names.Add(transform.name);
            var missing=new List<string>();
            foreach (string required in RequiredBones)
                if (!names.Contains(required)) missing.Add(required);
            if (missing.Count > 0)
                throw Error("Rig '" + fbxPath + "' is missing " + missing.Count + " required bones: " +
                    string.Join(", ",missing) + ". OptimizeGameObjects must be false and the game_engine bone names must be preserved.");

            var bones = new HashSet<Transform>();
            foreach (var renderer in renderers)
            {
                if (renderer.sharedMesh == null)
                    throw Error("SkinnedMeshRenderer '" + renderer.name + "' in '" + fbxPath + "' has no shared mesh.");
                if (renderer.bones == null || renderer.bones.Length == 0)
                    throw Error("SkinnedMeshRenderer '" + renderer.name + "' in '" + fbxPath + "' has no imported bones.");
                if (renderer.sharedMaterials == null || renderer.sharedMaterials.Length < renderer.sharedMesh.subMeshCount ||
                    renderer.sharedMaterials.Length == 0)
                    throw Error("SkinnedMeshRenderer '" + renderer.name + "' in '" + fbxPath + "' has " +
                        (renderer.sharedMaterials == null ? 0 : renderer.sharedMaterials.Length) + " materials for " +
                        renderer.sharedMesh.subMeshCount + " submeshes.");
                foreach (var bone in renderer.bones)
                    if (bone != null) bones.Add(bone);
            }

            return new RigInfo
            {
                BoneCount=bones.Count,
                SkinnedRendererCount=renderers.Length,
                AvatarName=avatar.name
            };
        }

        static void ValidatePreparedMaterials(GameObject model,string roleFolder,string fbxPath)
        {
            Shader lit=Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
                throw Error("Universal Render Pipeline/Lit is unavailable while validating '" + fbxPath + "'.");

            int slotCount=0;
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var materials=renderer.sharedMaterials;
                for (int slot=0; slot < materials.Length; slot++)
                {
                    slotCount++;
                    Material material=materials[slot];
                    if (material == null)
                        throw Error("Null material remains on '" + renderer.name + "' slot " + slot + " in '" + fbxPath + "'. Run ImportHuman and resolve its errors.");
                    string path=NormalizeAssetPath(AssetDatabase.GetAssetPath(material));
                    if (!IsUnderFolder(path,roleFolder) || !path.EndsWith(".mat",StringComparison.OrdinalIgnoreCase))
                        throw Error("Material '" + material.name + "' on '" + renderer.name + "' slot " + slot +
                            " is not a generated role material under '" + roleFolder + "'. Run ImportHuman to rebuild FBX remaps.");
                    if (material.shader != lit)
                        throw Error("Material '" + material.name + "' uses '" + material.shader.name +
                            "' instead of Universal Render Pipeline/Lit. Run ImportHuman with the project's URP renderer enabled.");
                }
            }
            if (slotCount == 0) throw Error("No skinned material slots found in '" + fbxPath + "'.");
        }

        static HumanMaterialKind ClassifyMaterial(SkinnedMeshRenderer renderer, Material source,
            HumanImportOverrides overrides)
        {
            if (overrides != null && overrides.TryGetMaterialKind(source.name, out HumanMaterialKind explicitKind))
                return explicitKind;

            string meshName=renderer.sharedMesh != null ? renderer.sharedMesh.name : string.Empty;
            string key=(renderer.name + " " + meshName + " " + source.name).ToLowerInvariant();
            if (key.Contains("eyebrow")) return HumanMaterialKind.Eyebrows;
            if (key.Contains("eyelash") || key.Contains("lash")) return HumanMaterialKind.Eyelashes;
            if (key.Contains("teeth") || key.Contains("tooth")) return HumanMaterialKind.Teeth;
            if (key.Contains("shoe")) return HumanMaterialKind.Shoes;
            if (key.Contains("casualsuit") || key.Contains("garment") || key.Contains("cloth") ||
                key.Contains("uniform") || key.Contains("male_") || key.Contains("female_")) return HumanMaterialKind.Garment;
            if (key.Contains("short01") || key.Contains("hair")) return HumanMaterialKind.Hair;
            if (key.Contains("body") || key.Contains("skin")) return HumanMaterialKind.Body;
            if (key.Contains("low-poly") || key.Contains("eye") || key.Contains("iris") ||
                key.Contains("sclera") || key.Contains("cornea")) return HumanMaterialKind.Eyes;

            throw Error("Cannot classify imported material '" + source.name + "' on renderer '" + renderer.name +
                "' (mesh '" + meshName + "'). Use new HumanImportOverrides().BindMaterial(\"" + source.name +
                "\", HumanMaterialKind.<kind>) when calling ImportHuman; no texture was guessed.");
        }

        static Texture2D ResolveTexture(HumanMaterialKind kind, bool normal, bool required,
            List<TextureEntry> entries, string textureFolder, HumanImportOverrides overrides)
        {
            Texture2D explicitTexture=GetTextureOverride(kind, normal, overrides);
            if (explicitTexture != null)
            {
                string overridePath=NormalizeAssetPath(AssetDatabase.GetAssetPath(explicitTexture));
                if (!IsUnderFolder(overridePath, textureFolder))
                    throw Error("Explicit " + kind + (normal ? " normal" : " diffuse") + " override must be under '" +
                        textureFolder + "'; received '" + overridePath + "'. Copy it into the role .fbm folder first.");
                return explicitTexture;
            }

            var matches = new List<TextureEntry>();
            foreach (var entry in entries)
                if (entry.IsNormal == normal && MatchesTextureKind(kind,entry.Stem,normal)) matches.Add(entry);

            if (matches.Count > 1)
            {
                var names = new List<string>();
                foreach (var match in matches) names.Add(match.Path);
                throw Error("Ambiguous " + kind + (normal ? " normal" : " diffuse") + " textures (" + matches.Count +
                    "): " + string.Join(", ", names) + ". Assign the corresponding HumanImportOverrides texture explicitly.");
            }
            if (matches.Count == 1) return matches[0].Texture;
            if (required)
                throw Error("No " + kind + (normal ? " normal" : " diffuse") + " texture matched the verified filename rules in '" +
                    textureFolder + "'. Copy the correct external texture there or assign HumanImportOverrides." + kind +
                    (normal ? "Normal" : "Diffuse") + ". No white fallback texture is used.");
            return null;
        }

        static bool MatchesTextureKind(HumanMaterialKind kind,string stem,bool normal)
        {
            bool diffuse=stem.IndexOf("_diffuse",StringComparison.OrdinalIgnoreCase) >= 0;
            switch (kind)
            {
                case HumanMaterialKind.Body:
                    return stem.StartsWith("young_", StringComparison.OrdinalIgnoreCase) && (normal || diffuse);
                case HumanMaterialKind.Garment:
                    return (stem.StartsWith("male_", StringComparison.OrdinalIgnoreCase) ||
                        stem.StartsWith("female_",StringComparison.OrdinalIgnoreCase)) && (normal || diffuse);
                case HumanMaterialKind.Shoes:
                    return stem.StartsWith("shoes",StringComparison.OrdinalIgnoreCase) && (normal || diffuse);
                case HumanMaterialKind.Hair:
                    return (stem.StartsWith("short", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("afro", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("bob",StringComparison.OrdinalIgnoreCase)) && (normal || diffuse);
                case HumanMaterialKind.Eyebrows:
                    return stem.StartsWith("eyebrow", StringComparison.OrdinalIgnoreCase);
                case HumanMaterialKind.Eyelashes:
                    return stem.StartsWith("eyelash", StringComparison.OrdinalIgnoreCase);
                case HumanMaterialKind.Teeth:
                    return stem.StartsWith("teeth",StringComparison.OrdinalIgnoreCase) ||
                        stem.StartsWith("tooth",StringComparison.OrdinalIgnoreCase);
                case HumanMaterialKind.Eyes:
                    return (stem.StartsWith("eye_", StringComparison.OrdinalIgnoreCase) ||
                        stem.StartsWith("eyes_", StringComparison.OrdinalIgnoreCase) ||
                        stem.StartsWith("iris_",StringComparison.OrdinalIgnoreCase)) && (normal || diffuse);
                default:
                    return false;
            }
        }

        static Texture2D GetTextureOverride(HumanMaterialKind kind,bool normal,HumanImportOverrides overrides)
        {
            if (overrides == null) return null;
            switch (kind)
            {
                case HumanMaterialKind.Body: return normal ? overrides.BodyNormal : overrides.BodyDiffuse;
                case HumanMaterialKind.Garment: return normal ? overrides.GarmentNormal : overrides.GarmentDiffuse;
                case HumanMaterialKind.Shoes: return normal ? overrides.ShoesNormal : overrides.ShoesDiffuse;
                case HumanMaterialKind.Hair: return normal ? overrides.HairNormal : overrides.HairDiffuse;
                case HumanMaterialKind.Eyebrows: return normal ? overrides.EyebrowsNormal : overrides.EyebrowsDiffuse;
                case HumanMaterialKind.Eyelashes: return normal ? overrides.EyelashesNormal : overrides.EyelashesDiffuse;
                case HumanMaterialKind.Teeth: return normal ? overrides.TeethNormal : overrides.TeethDiffuse;
                case HumanMaterialKind.Eyes: return normal ? overrides.EyeNormal : overrides.EyeDiffuse;
                default: return null;
            }
        }

        static float SmoothnessFor(HumanMaterialKind kind)
        {
            switch (kind)
            {
                case HumanMaterialKind.Body: return 0.25f;
                case HumanMaterialKind.Garment: return 0.20f;
                case HumanMaterialKind.Shoes: return 0.30f;
                case HumanMaterialKind.Hair:
                case HumanMaterialKind.Eyebrows:
                case HumanMaterialKind.Eyelashes: return 0.30f;
                case HumanMaterialKind.Teeth: return 0.35f;
                case HumanMaterialKind.Eyes: return 0.80f;
                default: return 0.20f;
            }
        }

        static float MeasureAssetHeight(GameObject model)
        {
            GameObject instance=UObject.Instantiate(model);
            try
            {
                instance.transform.SetParent(null,false);
                instance.transform.localPosition=Vector3.zero;
                instance.transform.localRotation=Quaternion.identity;
                instance.transform.localScale=Vector3.one;
                return MeasureVisibleBounds(instance).size.y;
            }
            finally
            {
                UObject.DestroyImmediate(instance);
            }
        }

        static Bounds MeasureVisibleBounds(GameObject root)
        {
            bool found=false;
            Bounds bounds=new Bounds();
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.sharedMesh == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!found) bounds=renderer.bounds;
                else bounds.Encapsulate(renderer.bounds);
                found=true;
            }
            if (!found || !IsFinite(bounds.size.x) || !IsFinite(bounds.size.y) || !IsFinite(bounds.size.z))
                throw Error("No finite visible SkinnedMeshRenderer bounds could be measured on '" + root.name + "'.");
            return bounds;
        }

        static GameObject LoadModel(string fbxPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null) throw Error("Failed to load imported GameObject at '" + fbxPath + "'.");
            return model;
        }

        static Avatar LoadSingleAvatar(string fbxPath)
        {
            Avatar avatar=null;
            int count=0;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (!(asset is Avatar candidate)) continue;
                avatar=candidate;
                count++;
            }
            if (count != 1)
                throw Error("Expected exactly one Avatar in '" + fbxPath + "' but found " + count +
                    ". Remove duplicate avatar definitions or fix the FBX import before continuing.");
            return avatar;
        }

        static List<string> FindTexturePaths(string textureFolder)
        {
            var unique=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D",new[] { textureFolder }))
            {
                string path=NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guid));
                if (IsUnderFolder(path,textureFolder)) unique.Add(path);
            }
            var paths=new List<string>(unique);
            paths.Sort(StringComparer.OrdinalIgnoreCase);
            return paths;
        }

        static bool IsNormalTexture(string stem) =>
            stem.EndsWith("_normal",StringComparison.OrdinalIgnoreCase);

        static bool IsCutoutTexture(string stem) =>
            (stem.StartsWith("short", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("afro", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("bob", StringComparison.OrdinalIgnoreCase)) ||
            stem.StartsWith("eyebrow",StringComparison.OrdinalIgnoreCase) ||
            stem.StartsWith("eyelash",StringComparison.OrdinalIgnoreCase);

        static bool IsIrisMaterial(string sourceName) =>
            sourceName.IndexOf("iris", StringComparison.OrdinalIgnoreCase) >= 0 ||
            sourceName.IndexOf("pupil", StringComparison.OrdinalIgnoreCase) >= 0;

        static void ValidateRole(string role)
        {
            if (string.IsNullOrWhiteSpace(role) || !string.Equals(role, role.Trim(), StringComparison.Ordinal) ||
                role == "." || role == ".." || role.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                role.IndexOf('/') >= 0 || role.IndexOf('\\') >= 0)
                throw Error("Role must be one safe folder/file name, not a path; received '" + role + "'.");
        }

        static string SanitizeFileName(string value)
        {
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_') chars[i]='_';
            string result=new string(chars).Trim('_');
            if (result.Length == 0) throw Error("Cannot derive a material asset filename from '" + value + "'.");
            return result;
        }

        static bool IsUnderFolder(string assetPath, string folder)
        {
            assetPath=NormalizeAssetPath(assetPath);
            folder=NormalizeAssetPath(folder).TrimEnd('/');
            return assetPath.StartsWith(folder + "/",StringComparison.OrdinalIgnoreCase);
        }

        static string NormalizeAssetPath(string path) =>
            string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static void SetTextureIfPresent(Material material, string property, Texture texture)
        {
            if (material.HasProperty(property)) material.SetTexture(property, texture);
        }

        static void SetColorIfPresent(Material material, string property, Color value)
        {
            if (material.HasProperty(property)) material.SetColor(property, value);
        }

        static void SetFloatIfPresent(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }

        static InvalidOperationException Error(string message) =>
            new InvalidOperationException("HUMANIMPORT " + message);
    }
}
#endif
