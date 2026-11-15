using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NightShift.EditorTools
{
    public static class FixSceneRound1
    {
        public static string Run()
        {
            var sb = new StringBuilder();

            
            var player = GameObject.Find("Player");
            if (player != null)
            {
                Vector3? spot = null;
                for (float z = -9.5f; z <= -7.5f && spot == null; z += 1f)
                    for (float x = -16f; x <= 16f; x += 2f)
                    {
                        var p = new Vector3(x, 0.08f, z);
                        var hits = Physics.OverlapBox(p + Vector3.up * 0.9f, new Vector3(0.3f, 0.9f, 0.3f),
                            Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                        bool blocked = false;
                        foreach (var h in hits)
                            if (!h.isTrigger && h.gameObject != player && h.bounds.max.y > 0.4f)
                            { blocked = true; break; }
                        if (!blocked)
                        { spot = p; break; }
                    }
                if (spot != null)
                {
                    player.transform.position = spot.Value;
                    player.transform.rotation = Quaternion.identity;
                    sb.AppendLine($"Player spawn moved to {spot.Value}");
                }
                else sb.AppendLine("Player spawn: NO CLEAR SPOT FOUND");
            }

            
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f,0.36f,0.44f);
            RenderSettings.ambientIntensity = 1.25f;
            RenderSettings.fogDensity = 0.022f;
            RenderSettings.fogColor = new Color(0.10f,0.12f,0.16f);

            int bumped = 0;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Point)
                {
                    l.intensity = Mathf.Max(l.intensity * 6f,6f);
                    l.range = Mathf.Max(l.range,10f);
                    bumped++;
                }
            }
            sb.AppendLine($"point lights bumped: {bumped}");

            
            foreach (var guid in AssetDatabase.FindAssets("t:VolumeProfile"))
            {
                var prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (prof == null) continue;
                if (prof.TryGet<ColorAdjustments>(out var ca))
                { ca.postExposure.Override(0.35f); sb.AppendLine("postExposure -> 0.35 on " + prof.name); }
                if (prof.TryGet<Vignette>(out var vg)) vg.intensity.Override(0.22f);
            }

            
            foreach (var tmp in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None))
            {
                if (tmp.text != null && tmp.text.Contains("go back"))
                {
                    var rt = (RectTransform)tmp.transform;
                    rt.anchoredPosition = new Vector2(rt.anchoredPosition.x,rt.anchoredPosition.y - 50f);
                    sb.AppendLine("subtitle moved down 50px");
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            return sb.ToString();
        }
    }
}
