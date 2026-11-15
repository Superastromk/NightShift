using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using NightShift.Audio;
using NightShift.Core;
using NightShift.Objectives;
using NightShift.UI;

namespace NightShift.EditorTools
{
    public static class WireFinal
    {
        public static string Run()
        {
            var sb = new StringBuilder();

            
            var gs = GameObject.Find("GameSystems");
            if (gs == null) return "ERROR no GameSystems";
            var flow = gs.GetComponent<GameFlowUI>();
            if (flow == null) flow = gs.AddComponent<GameFlowUI>();
            sb.AppendLine("GameFlowUI: " + (flow != null ? "ok" : "FAILED"));

            var ui=gs.GetComponent<UIManager>();

            
            var root=GameObject.Find("UIRoot");
            if (root != null && ui != null)
            {
                foreach (var im in root.GetComponentsInChildren<Image>(true))
                {
                    if (ui.ComposureVignette == null && im.name.IndexOf("vignette", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    { ui.ComposureVignette = im; sb.AppendLine("ComposureVignette <- " + im.name); }
                    if (ui.FlashImage == null && (im.name.IndexOf("rewind", System.StringComparison.OrdinalIgnoreCase) >= 0 || im.name.IndexOf("flash",System.StringComparison.OrdinalIgnoreCase) >= 0))
                    { ui.FlashImage = im; sb.AppendLine("FlashImage <- " + im.name); }
                }
            }

            
            var ad = gs.GetComponent<AudioDirector>();
            if (ad != null && File.Exists("Assets/Audio/clipmap.json"))
            {
                string json = File.ReadAllText("Assets/Audio/clipmap.json");
                var ids=new List<string>(); var clips=new List<AudioClip>();
                
                string body=json.Replace("{", "").Replace("}", "").Replace("\"", "");
                foreach (var pair in body.Split(','))
                {
                    int ix = pair.IndexOf(':');
                    if (ix < 1) continue;
                    string id=pair.Substring(0, ix).Trim();
                    string path=pair.Substring(ix + 1).Trim();
                    var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip != null) { ids.Add(id); clips.Add(clip); }
                }
                ad.ClipIds=ids; ad.ClipClips=clips;
                sb.AppendLine($"AudioDirector clips wired: {ids.Count}");
            }
            else sb.AppendLine("AudioDirector wiring skipped (missing component or clipmap)");

            
            var om = gs.GetComponent<ObjectiveManager>();
            if (om != null)
            {
                var wanted = new[] { "print_report", "deliver_folder", "refill_coffee", "water_plant", "answer_phone" };
                var all = Object.FindObjectsByType<Objective>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                var seq = new List<Objective>();
                foreach (var w in wanted)
                    foreach (var o in all)
                        if (o.Id == w)
                        { seq.Add(o); break; }
                om.Sequence = seq.ToArray();
                sb.AppendLine($"ObjectiveManager.Sequence wired: {seq.Count}/5");
            }

            
            if (root != null)
            {
                int bound=0;
                foreach (var b in root.GetComponentsInChildren<Button>(true))
                {
                    string label=(b.GetComponentInChildren<TMPro.TMP_Text>()?.text ?? b.name).ToLowerInvariant();
                    b.onClick.RemoveAllListeners();
                    if (label.Contains("start")) { UnityEventTools.AddPersistentListener(b.onClick,flow.StartShift); bound++; sb.AppendLine("bound start"); }
                    else if (label.Contains("resume"))
{ UnityEventTools.AddPersistentListener(b.onClick, flow.ResumeShift); bound++; sb.AppendLine("bound resume"); }
                    else if (label.Contains("quit to title")) { UnityEventTools.AddPersistentListener(b.onClick,flow.QuitToTitle); bound++; sb.AppendLine("bound quit-to-title"); }
                    else if (label.Contains("quit"))
{ UnityEventTools.AddPersistentListener(b.onClick, flow.QuitGame); bound++; sb.AppendLine("bound quit"); }
                }
                sb.AppendLine($"buttons bound: {bound}");
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return sb.ToString();
        }
    }
}
