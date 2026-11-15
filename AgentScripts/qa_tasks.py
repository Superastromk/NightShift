"""Checks every office task in order against the open Editor in Play mode.

For each objective: find a spot on the floor in front of it, stand there, aim at it, and
check that the interaction prompt appears for that objective (nothing in the way, in reach).
Then hold E for real (the interactor's own Interact path, frame by frame) until it completes.

    python qa_tasks.py <out_dir>
"""
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import qa_live as q  # noqa: E402

OM = q.OM
FPC = q.FPC

STAND = r"""
var om = NightShift.Objectives.ObjectiveManager.Instance; var c = om.Current; if (c == null) return "none";
var p = NightShift.Player.FirstPersonController.Instance;
var col = c.GetComponentInChildren<UnityEngine.Collider>();
UnityEngine.Vector3 target = col != null ? col.bounds.center : c.MarkerPosition;
string best = null; float bestScore = 1e9f; UnityEngine.Vector3 bestPos = UnityEngine.Vector3.zero;
for (int ring = 0; ring < 3; ring++) for (int i = 0; i < 24; i++) {
  float r = 0.75f + ring * 0.35f; float a = i / 24f * UnityEngine.Mathf.PI * 2f;
  var cand = new UnityEngine.Vector3(target.x + UnityEngine.Mathf.Cos(a) * r, 0f, target.z + UnityEngine.Mathf.Sin(a) * r);
  UnityEngine.AI.NavMeshHit hit; if (!UnityEngine.AI.NavMesh.SamplePosition(cand, out hit, 0.25f, UnityEngine.AI.NavMesh.AllAreas)) continue;
  var eye = hit.position + UnityEngine.Vector3.up * 1.67f;
  UnityEngine.RaycastHit rh; var dir = target - eye;
  if (dir.magnitude > 2.4f) continue;
  if (!UnityEngine.Physics.Raycast(eye, dir.normalized, out rh, 2.6f, ~((1<<6)|(1<<2)), UnityEngine.QueryTriggerInteraction.Ignore)) continue;
  if (rh.collider.GetComponentInParent<NightShift.Objectives.Objective>() != c) continue;
  float score = UnityEngine.Mathf.Abs(r - 1.0f);
  if (score < bestScore) { bestScore = score; bestPos = hit.position; best = "ok"; }
}
if (best == null) return "NO_STANDING_SPOT id=" + c.Id + " target=" + target.ToString("F2");
var to = target - (bestPos + UnityEngine.Vector3.up * 1.67f);
p.Warp(bestPos + UnityEngine.Vector3.up * 0.05f, UnityEngine.Quaternion.identity);
p.SetLook(UnityEngine.Mathf.Atan2(to.x, to.z) * UnityEngine.Mathf.Rad2Deg, -UnityEngine.Mathf.Atan2(to.y, new UnityEngine.Vector2(to.x, to.z).magnitude) * UnityEngine.Mathf.Rad2Deg);
p.GetComponent<NightShift.Rewind.TimelineRecorder>().Clear();
return "stand id=" + c.Id + " at " + bestPos.ToString("F1") + " dist=" + to.magnitude.ToString("F2");
"""

CHECK = r"""
var c = NightShift.Objectives.ObjectiveManager.Instance.Current; if (c == null) return "none";
var pi = NightShift.Player.FirstPersonController.Instance.GetComponent<NightShift.Player.PlayerInteractor>();
var cur = pi.Current; var ui = NightShift.UI.UIManager.Instance;
string prompt = ui.PromptGroup != null && ui.PromptGroup.activeSelf ? ui.PromptText.text : "(hidden)";
return "aim id=" + c.Id + " current=" + (cur == null ? "null" : ((UnityEngine.Component)cur).name) + " match=" + (object.ReferenceEquals(cur, c)) + " prompt='" + prompt + "'";
"""

HOLD = r"""
var c = NightShift.Objectives.ObjectiveManager.Instance.Current; if (c == null) return "none";
var pi = NightShift.Player.FirstPersonController.Instance.GetComponent<NightShift.Player.PlayerInteractor>();
if (!object.ReferenceEquals(pi.Current, c)) return "not aimed";
c.Interact(pi); return "held p=" + c.Progress01.ToString("F2") + " done=" + c.IsDone;
"""

SAFE = r"""
var m = NightShift.Monster.MonsterAI.Instance; if (m != null) { m.SetRewindLock(true); m.Teleport(new UnityEngine.Vector3(-19f, 0f, -13f)); }
var fk = UnityEngine.Object.FindFirstObjectByType<NightShift.Scares.FirstKillDirector>(); if (fk != null) fk.enabled = false;
var dm = NightShift.Rewind.DeathManager.Instance; if (dm != null) dm.DeathsEnabled = false;
return "safe";
"""


def main():
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    print(q.ev(q.START))
    time.sleep(2.5)
    print(q.ev(SAFE))
    for n in range(12):
        stand = q.ev(STAND)
        print(stand)
        if stand == "none":
            break
        if stand.startswith("NO_STANDING_SPOT") or stand.startswith("ERR"):
            q.ev(f"var c={OM}.Current; c.Complete(); return \"forced\";")
            continue
        time.sleep(0.4)
        check = q.ev(CHECK)
        print(check)
        q.shot(out, f"task_{n:02d}")
        # hold E: one Interact per frame, as the interactor would
        status = ""
        for _ in range(60):
            status = q.ev(HOLD)
            if "done=True" in status or status in ("none", "not aimed"):
                break
            time.sleep(0.05)
        print("  ", status)
        if "done=True" not in status:
            q.ev(f"var c={OM}.Current; if(c!=null) c.Complete(); return \"forced\";")
        q.ev(SAFE)
        time.sleep(0.3)
    print(q.ev(q.STATE))


if __name__ == "__main__":
    main()
