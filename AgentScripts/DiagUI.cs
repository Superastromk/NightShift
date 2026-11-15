using UnityEngine;
using NightShift.UI;
using NightShift.Monster;

namespace NightShift.EditorTools
{
    public static class DiagUI
    {
        public static string Run()
        {
            var sb=new System.Text.StringBuilder();
            var ui=Object.FindFirstObjectByType<UIManager>();
            if (ui == null) return "NO UIManager in scene";
            sb.AppendLine($"UIManager on {ui.gameObject.name} active={ui.gameObject.activeInHierarchy}");
            sb.AppendLine($"FlashImage: {(ui.FlashImage == null ? "NULL" : Describe(ui.FlashImage.gameObject))}");
            sb.AppendLine($"TimelineText: {(ui.TimelineText == null ? "NULL" : ui.TimelineText.text)}");
            sb.AppendLine($"HudPanel: {(ui.HudPanel == null ? "NULL" : ui.HudPanel.name)}");
            var monsters=Object.FindObjectsByType<MonsterAI>(FindObjectsSortMode.None);
            foreach (var m in monsters)
            {
                var agent=m.GetComponent<UnityEngine.AI.NavMeshAgent>();
                sb.AppendLine($"Monster {m.name}: agent={(agent == null ? "NULL" : $"enabled={agent.enabled} isOnNavMesh={agent.isOnNavMesh} pathStatus={(agent == null ? "-" : agent.pathStatus.ToString())} dest={agent.destination} stopped={agent.isStopped} warp...")}");
                if (agent != null && agent.isOnNavMesh)
                    sb.AppendLine($"  remaining={agent.remainingDistance} pending={agent.pathPending}");
            }
            
            var hit = new UnityEngine.AI.NavMeshHit();
            bool found = UnityEngine.AI.NavMesh.SamplePosition(new Vector3(-1.3f,0.1f,4.3f),out hit,2f,UnityEngine.AI.NavMesh.AllAreas);
            sb.AppendLine($"navmesh near (-1.3,4.3): {(found ? $"hit={hit.position}" : "NONE within 2m")}");
            found=UnityEngine.AI.NavMesh.SamplePosition(new Vector3(0f, 0.1f, 6.5f), out hit, 2f, UnityEngine.AI.NavMesh.AllAreas);
            sb.AppendLine($"navmesh near (0,6.5): {(found ? $"hit={hit.position}" : "NONE within 2m")}");
            return sb.ToString();
        }

        static string Describe(GameObject go)
        {
            string chain = go.name;
            for (var p = go.transform.parent; p != null; p = p.parent)
                chain = p.name + "/" + chain;
            CanvasGroup cg;
            string cgAlpha=go.TryGetComponent<CanvasGroup>(out cg) ? cg.alpha.ToString() : "none";
            return $"{chain} activeSelf={go.activeSelf} activeInHierarchy={go.activeInHierarchy} canvasGroupAlpha={cgAlpha}";
        }
    }
}
