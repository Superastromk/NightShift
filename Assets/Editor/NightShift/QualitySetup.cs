#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NightShift.EditorBuild
{

public static class QualitySetup
    {
        const string Folder = "Assets/Settings/NightShift";
        static readonly string[] TierNames = { "Low", "Medium", "High" };

        public static void EnsureLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            string[] names={ "Player", "Prop", "Monster", "NoNav" };
            for (int i=0; i < names.Length; i++)
                layers.GetArrayElementAtIndex(6 + i).stringValue=names[i];
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Run()
        {
            Directory.CreateDirectory(Folder);
            var template=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            var templateRenderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            if (template == null || templateRenderer == null)
                throw new System.InvalidOperationException("Expected Assets/Settings/PC_RPAsset.asset and PC_Renderer.asset to exist.");

            var pipelines = new UniversalRenderPipelineAsset[TierNames.Length];
            for (int tier = 0; tier < TierNames.Length; tier++)
                pipelines[tier] = MakeTier(tier, template, templateRenderer);

var qualityAsset = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels=qualityAsset.FindProperty("m_QualitySettings");
            levels.arraySize = TierNames.Length;
            for (int tier = 0; tier < TierNames.Length; tier++)
            {
                var level = levels.GetArrayElementAtIndex(tier);
                level.FindPropertyRelative("name").stringValue = TierNames[tier];
                level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipelines[tier];
                level.FindPropertyRelative("vSyncCount").intValue=1;
                level.FindPropertyRelative("antiAliasing").intValue = 0;     
                level.FindPropertyRelative("lodBias").floatValue = tier == 0 ? 0.7f : tier == 1 ? 1f : 1.5f;
                level.FindPropertyRelative("anisotropicTextures").intValue = tier == 0 ? 0 : 1;
                level.FindPropertyRelative("globalTextureMipmapLimit").intValue=tier == 0 ? 1 : 0;
                level.FindPropertyRelative("realtimeReflectionProbes").boolValue = false;
                level.FindPropertyRelative("softParticles").boolValue = false;
                
                
                level.FindPropertyRelative("excludedTargetPlatforms").arraySize = 0;
            }
            qualityAsset.FindProperty("m_CurrentQuality").intValue=1;
            qualityAsset.ApplyModifiedPropertiesWithoutUndo();

            GraphicsSettings.defaultRenderPipeline=pipelines[1];
            QualitySettings.SetQualityLevel(1, true);
            AssetDatabase.SaveAssets();
        }

        static UniversalRenderPipelineAsset MakeTier(int tier, UniversalRenderPipelineAsset template, UniversalRendererData templateRenderer)
        {
            string name=TierNames[tier];

            
            string rendererPath=$"{Folder}/{name}_Renderer.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(templateRenderer),rendererPath);
                renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            }
            renderer.renderingMode = RenderingMode.ForwardPlus;   
            renderer.depthPrimingMode = DepthPrimingMode.Disabled;
            foreach (var feature in renderer.rendererFeatures)
            {
                if (feature == null) continue;
                if (feature.GetType().Name.Contains("ScreenSpaceAmbientOcclusion"))
                {
                    feature.SetActive(tier == 2);
                    var so = new SerializedObject(feature);
                    var settings = so.FindProperty("m_Settings");
                    if (settings != null)
                    {
                        
                        
                        SetFloat(settings, "Intensity", 0.6f);
                        SetFloat(settings, "Radius",0.45f);
                        SetFloat(settings, "DirectLightingStrength", 0.15f);
                        SetBool(settings, "Downsample", false);
                        SetInt(settings, "Samples", 1);   
                        SetInt(settings, "BlurQuality",0); 
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            EditorUtility.SetDirty(renderer);

            
            string pipelinePath = $"{Folder}/{name}.asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(template), pipelinePath);
                pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            }
            var pso = new SerializedObject(pipeline);
            var list = pso.FindProperty("m_RendererDataList");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            pso.FindProperty("m_DefaultRendererIndex").intValue=0;
            pso.FindProperty("m_RequireOpaqueTexture").boolValue = false;  
            pso.FindProperty("m_RequireDepthTexture").boolValue = tier == 2;
            pso.FindProperty("m_AdditionalLightsPerObjectLimit").intValue = 8;
            pso.FindProperty("m_AdditionalLightShadowsSupported").boolValue=true; 
            pso.FindProperty("m_AdditionalLightsShadowmapResolution").intValue = tier == 0 ? 512 : tier == 1 ? 1024 : 2048;
            pso.FindProperty("m_SoftShadowsSupported").boolValue = tier > 0;
            pso.FindProperty("m_ShadowCascadeCount").intValue = 1;
            pso.FindProperty("m_MainLightShadowsSupported").boolValue=false;    
            pso.ApplyModifiedPropertiesWithoutUndo();

            pipeline.renderScale=tier == 0 ? 0.75f : 1f;
            pipeline.upscalingFilter=tier == 0 ? UpscalingFilterSelection.FSR : UpscalingFilterSelection.Auto;
            pipeline.msaaSampleCount=tier == 0 ? 1 : tier == 1 ? 2 : 4;
            pipeline.supportsHDR=true;   
            pipeline.shadowDistance=tier == 0 ? 15f : tier == 1 ? 25f : 40f;
            pipeline.useSRPBatcher=true;
            EditorUtility.SetDirty(pipeline);
            return pipeline;
        }

        static void SetFloat(SerializedProperty parent, string name, float value)
        {
            var p=parent.FindPropertyRelative(name);
            if (p != null) p.floatValue=value;
        }

        static void SetBool(SerializedProperty parent, string name, bool value)
        {
            var p=parent.FindPropertyRelative(name);
            if (p != null) p.boolValue=value;
        }

        static void SetInt(SerializedProperty parent, string name, int value)
        {
            var p=parent.FindPropertyRelative(name);
            if (p == null) return;
            if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex=value;
            else p.intValue=value;
        }
    }
}
#endif
