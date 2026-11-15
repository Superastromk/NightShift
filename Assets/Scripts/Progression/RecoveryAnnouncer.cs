using System.Collections;
using UnityEngine;
using NightShift.Audio;
using NightShift.Core;
using NightShift.Monster;
using NightShift.UI;

namespace NightShift.Progression
{

public class RecoveryAnnouncer : MonoBehaviour
    {
        [System.Serializable]
        public class Line { public string ClipId; [TextArea] public string Subtitle; }

        public Line[] Lines =
        {
            new Line { ClipId = "sys_recovery_1", Subtitle="[TEMPORAL RECOVERY] Your death has been reversed. Your workstation is expecting you." },
            new Line { ClipId = "sys_recovery_2", Subtitle = "[TEMPORAL RECOVERY] A second death has been reversed. Please treat this as your final warning." },
            new Line { ClipId = "sys_recovery_3", Subtitle="[TEMPORAL RECOVERY] Third death recorded. Repeated mortality has been noted in your personnel file." },
            new Line { ClipId = "sys_recovery_4", Subtitle = "[TEMPORAL RECOVERY] Each additional death will now be charged to your annual leave." },
            new Line { ClipId = "sys_recovery_5", Subtitle="[TEMPORAL RECOVERY] Death reversed. You are familiar with the procedure." },
        };

        [Tooltip("What the monster 'learned', shown under the TIMELINE card, indexed by memory tier.")]
        public string[] TierNotes =
        {
            "",
            "It felt that.",
            "It remembers where you died.",
            "It knows where you are going.",
            "It remembers everything.",
        };

        public float Delay = 1.1f;

        void OnEnable()
        { GameEvents.TimelineRewound += OnRewound; }
        void OnDisable()
        { GameEvents.TimelineRewound -= OnRewound; }

        void OnRewound()
        {
            StartCoroutine(Announce());
        }

        IEnumerator Announce()
        {
            var gm = GameManager.Instance;
            var ui = UIManager.Instance;
            int timeline = gm != null ? gm.TimelineIndex : 1;
            var mem = FindFirstObjectByType<MonsterMemory>();
            int tier = mem != null ? mem.MemoryTier : 0;
            string note = TierNotes != null && TierNotes.Length > 0 ? TierNotes[Mathf.Clamp(tier, 0, TierNotes.Length - 1)] : "";
            ui?.Toast($"BOUNCE BACK · {timeline:00}", "Tasks saved. " + note, 3.6f);

            var director = ShiftDirector.Instance;
            if (director != null && director.Act >= 3) yield break; 
            yield return new WaitForSeconds(Delay);
            if (Lines == null || Lines.Length == 0) yield break;
            var line = Lines[Mathf.Clamp(timeline - 1, 0, Lines.Length - 1)];
            AudioDirector.Instance?.PlayVoice(line.ClipId);
            float secs = 4f;
            var ad = AudioDirector.Instance;
            if (ad != null && ad.Clips.TryGetValue(line.ClipId, out var clip) && clip != null) secs = clip.length + 0.4f;
            ui?.ShowSubtitle(line.Subtitle, secs);
            if (timeline == 1) { yield return new WaitForSeconds(secs + 0.5f); ui?.Hint("Death rewinds your position. <b>Finished tasks stay done.</b> Try a different route; close doors and stay low.", 8f); }
        }
    }
}
