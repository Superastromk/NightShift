using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using NightShift.Monster;
using NightShift.Scares;
using NightShift.UI;

namespace NightShift.EditorTools
{
    public static class CriticRound2
    {
        public static string Run()
        {
            var sb = new StringBuilder();

            
            var dm = Object.FindFirstObjectByType<Rewind.DeathManager>();
            if (dm == null) return "ERROR no DeathManager/GameSystems";
            var director = dm.gameObject.GetComponent<FirstKillDirector>();
            if (director == null) director = dm.gameObject.AddComponent<FirstKillDirector>();
            sb.AppendLine("FirstKillDirector on " + dm.gameObject.name);

var ai = Object.FindFirstObjectByType<MonsterAI>();
            if (ai != null)
            {
                ai.FovAngle = 120f;
                ai.ChaseSpeed = 4.0f;
                sb.AppendLine($"MonsterAI FovAngle={ai.FovAngle} ChaseSpeed={ai.ChaseSpeed}");
            }

            
            var ui=Object.FindFirstObjectByType<UIManager>();
            if (ui != null)
            {
                ui.FlashHoldSeconds=0.12f;
                ui.FlashOutSeconds=0.35f;
                sb.AppendLine($"UIManager flash hold={ui.FlashHoldSeconds} out={ui.FlashOutSeconds}");
            }

            
            var rc = Object.FindFirstObjectByType<Rewind.RewindController>();
            if (rc != null)
            {
                const string matPath = "Assets/Materials/GhostTrail.mat";
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                var mat=UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null && shader != null)
                {
                    mat=new Material(shader) { color=new Color(0.55f, 0.85f, 1f, 0.55f) };
                    System.IO.Directory.CreateDirectory("Assets/Materials");
                    UnityEditor.AssetDatabase.CreateAsset(mat,matPath);
                }
                if (mat != null)
                {
                    rc.GhostMaterial = mat;
                    sb.AppendLine("GhostMaterial = " + matPath);
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            return sb.ToString();
        }
    }
}
