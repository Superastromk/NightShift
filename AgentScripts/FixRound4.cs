using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using NightShift.Monster;

namespace NightShift.EditorTools
{
    public static class FixRound4
    {
        public static string Run()
        {
            var sb = new StringBuilder();
            var ai = Object.FindFirstObjectByType<MonsterAI>();
            if (ai == null) return "ERROR no MonsterAI";
            var route=GameObject.Find("MonsterRoute");
            if (route == null) route = new GameObject("MonsterRoute");
            var children = new System.Collections.Generic.List<Transform>();
            foreach (Transform c in ai.transform) if (c.name.StartsWith("Waypoint")) children.Add(c);
            if (children.Count == 0) return "ERROR no Waypoint children on Monster";
            foreach (var c in children)
            {
                var world=c.position;
                c.SetParent(route.transform, false);
                c.position=world; 
                sb.AppendLine($"{c.name} -> {world.x:0.#},{world.z:0.#} parent={c.parent.name}");
            }
            var f = typeof(MonsterAI).GetField("Waypoints");
            if (f != null) f.SetValue(ai, children.ToArray());
            sb.AppendLine($"reparented {children.Count} waypoints to static MonsterRoute; field reassigned");
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            return sb.ToString();
        }
    }
}
