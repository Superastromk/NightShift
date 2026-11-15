using System.Linq;
using System.Text;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using NightShift.UI;

namespace NightShift.EditorTools
{
    public static class RebindButtons
    {
        public static string Run()
        {
            var sb = new StringBuilder();
            var root = GameObject.Find("UIRoot");
            if (root == null) return "REBIND ERROR: no UIRoot in scene";
            var flow = Object.FindFirstObjectByType<GameFlowUI>();
            if (flow == null) return "REBIND ERROR: no GameFlowUI in scene";
            flow = Object.FindFirstObjectByType<GameFlowUI>(); 
            foreach (var b in root.GetComponentsInChildren<Button>(true))
            {
                string label = (b.GetComponentInChildren<TMPro.TMP_Text>()?.text ?? b.name).ToLowerInvariant();
                b.onClick.RemoveAllListeners();
                System.Action bind = null;
                string target = null;
                if (label.Contains("start"))
{ bind = () => UnityEventTools.AddPersistentListener(b.onClick, flow.StartShift); target = "StartShift"; }
                else if (label.Contains("resume")) { bind = () => UnityEventTools.AddPersistentListener(b.onClick,flow.ResumeShift); target = "ResumeShift"; }
                else if (label.Contains("quit to title") || label.Contains("quit_to_title")) { bind=() => UnityEventTools.AddPersistentListener(b.onClick, flow.QuitToTitle); target="QuitToTitle"; }
                else if (label.Contains("quit")) { bind = () => UnityEventTools.AddPersistentListener(b.onClick, flow.QuitGame); target = "QuitGame"; }
                if (bind != null)
                { bind(); sb.AppendLine($"bound '{label}' -> {target}"); }
                else sb.AppendLine($"UNMATCHED button '{label}' ({b.name})");
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            return sb.ToString();
        }
    }
}
