using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using NightShift.Monster;

namespace NightShift.EditorTools
{
    public static class FixWaypoints
    {
        public static string Run()
        {
            var sb = new StringBuilder();
            var ai = Object.FindFirstObjectByType<MonsterAI>();
            if (ai == null) return "ERROR no MonsterAI";
            
            Vector3[] pts =
            {
                new Vector3(-16f, 0f, 6.5f), new Vector3(-8f, 0f, 6.5f),
                new Vector3(0f, 0f, 6.5f),   new Vector3(8f, 0f, 6.5f),
                new Vector3(16f, 0f, 6.5f),  new Vector3(17.5f, 0f, 10.5f),
                new Vector3(8f, 0f, 6.5f),   new Vector3(-8f, 0f, 6.5f),
            };
            var f = typeof(MonsterAI).GetField("Waypoints");
            if (f == null)
            { sb.AppendLine("no Waypoints field; trying properties"); }
            var t=ai.transform;
            var children=new System.Collections.Generic.List<Transform>();
            foreach (Transform c in t) if (c.name.StartsWith("Waypoint")) children.Add(c);
            if (children.Count == 0)
            {
                for (int i = 0; i < pts.Length; i++)
                {
                    var w = new GameObject("Waypoint" + i).transform;
                    w.SetParent(t, false); w.position = pts[i]; children.Add(w);
                }
                sb.AppendLine("created " + pts.Length + " waypoints");
            }
            for (int i=0; i < children.Count && i < pts.Length; i++)
                children[i].position=pts[i];
            if (f != null) f.SetValue(ai, children.ToArray());
            sb.AppendLine("waypoints set on corridor loop: " + Mathf.Min(children.Count,pts.Length));
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            return sb.ToString();
        }
    }
}
