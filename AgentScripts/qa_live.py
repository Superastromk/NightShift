"""Live-Editor QA driver: runs a scripted walkthrough in Play mode and captures the Game view.

    python qa_live.py <out_dir> <scenario>

Scenarios are small Python lists below. Each step is ("eval", code) | ("wait", s) | ("shot", name).
Uses ucmd.run, so the open Editor must have its Pipeline server running.
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ucmd  # noqa: E402

GM = "NightShift.Core.GameManager.Instance"
FPC = "NightShift.Player.FirstPersonController.Instance"
OM = "NightShift.Objectives.ObjectiveManager.Instance"


def ev(code, timeout=120):
    status, text = ucmd.run(["script", "eval", "--code", code], timeout)
    try:
        d = json.loads(text)
        return d.get("result") if d.get("success") else "ERR " + str(d.get("error")) + " " + str(d.get("errorDetails", ""))[:600]
    except ValueError:
        return "ERR " + text[:400]


def shot(out_dir, name):
    path = os.path.abspath(os.path.join(out_dir, name + ".png"))
    status, text = ucmd.run(["capture", "game_view", "--output-path", path, "--width", "1280", "--height", "720"], 60)
    return path if status == 200 else "ERR " + text[:300]


def warp(x, y, z, yaw, pitch=0):
    return (f"var p={FPC}; p.Warp(new UnityEngine.Vector3({x}f,{y}f,{z}f), UnityEngine.Quaternion.Euler(0,{yaw}f,0)); "
            f"p.SetLook({yaw}f,{pitch}f); p.GetComponent<NightShift.Rewind.TimelineRecorder>().Clear(); return \"ok\";")


START = (f"var f=NightShift.UI.GameFlowUI.Instance; f.StartShift(); return {GM}.QaTel();")
STATE = (f"return {GM}.QaTel()+\" | \"+{OM}.QaTel()+\" | \"+{FPC}.QaTel()+\" | m=\"+(NightShift.Monster.MonsterAI.Instance!=null?NightShift.Monster.MonsterAI.Instance.QaTel():\"-\")"
         f"+\" | d=\"+(NightShift.Progression.ShiftDirector.Instance!=null?NightShift.Progression.ShiftDirector.Instance.QaTel():\"-\");")
FREEZE_MONSTER = "var m=NightShift.Monster.MonsterAI.Instance; if(m!=null) m.SetRewindLock(true); return \"frozen\";"
COMPLETE = f"var c={OM}.Current; if(c==null) return \"none\"; var id=c.Id; c.Complete(); return \"done \"+id;"

def look_at_monster(dist, side=0.0, height=1.1):
    return ("var m=NightShift.Monster.MonsterAI.Instance; var t=m.transform; var p=" + FPC + "; "
            f"p.Warp(t.position+t.forward*{dist}f+t.right*{side}f, UnityEngine.Quaternion.identity); "
            f"var to=(t.position+UnityEngine.Vector3.up*{height}f)-p.EyePosition; "
            "p.SetLook(UnityEngine.Mathf.Atan2(to.x,to.z)*UnityEngine.Mathf.Rad2Deg, -UnityEngine.Mathf.Atan2(to.y,new UnityEngine.Vector2(to.x,to.z).magnitude)*UnityEngine.Mathf.Rad2Deg); return \"ok\";")


KEY_LIGHT = ("var m=NightShift.Monster.MonsterAI.Instance; var t=m.transform; var g=UnityEngine.GameObject.Find(\"QA_Key\"); "
             "if(g==null){ g=new UnityEngine.GameObject(\"QA_Key\"); var l=g.AddComponent<UnityEngine.Light>(); l.type=UnityEngine.LightType.Point; l.range=7; l.intensity=3.5f; } "
             "g.transform.position=t.position+t.forward*1.6f+t.right*0.9f+UnityEngine.Vector3.up*2.3f; return \"lit\";")

SCENARIOS = {
    "ui": [
        ("shot", "u1_title"),
        ("eval", "NightShift.UI.GameFlowUI.Instance.OpenSettings(); return \"settings\";"), ("wait", 0.5), ("shot", "u2_settings"),
        ("eval", "NightShift.UI.GameFlowUI.Instance.Back(); NightShift.UI.GameFlowUI.Instance.OpenHowTo(); return \"howto\";"), ("wait", 0.5), ("shot", "u3_howto"),
        ("eval", "NightShift.UI.GameFlowUI.Instance.Back(); return \"back\";"),
        ("eval", START), ("wait", 1.2), ("eval", FREEZE_MONSTER), ("shot", "u4_start_toast"),
        ("wait", 5.0), ("eval", warp(12.6, 0.05, 12.6, 0, 8)), ("wait", 0.6), ("shot", "u5_prompt"),
        ("eval", "NightShift.UI.UIManager.Instance.ShowSubtitle(\"[INTERCOM] Please do not die more than is strictly necessary.\", 6f); NightShift.UI.UIManager.Instance.Toast(\"TIMELINE 01\", \"It remembers where you died.\", 4f); return \"busy\";"),
        ("wait", 0.6), ("shot", "u6_busy"),
        ("eval", GM + ".SetState(NightShift.Core.GameState.Paused); return \"pause\";"), ("wait", 0.4), ("shot", "u7_pause"),
        ("eval", GM + ".SetState(NightShift.Core.GameState.Playing); return \"resume\";"),
    ],
    "figures": [
        ("eval", START), ("wait", 2.5),
        ("eval", FREEZE_MONSTER), ("eval", KEY_LIGHT),
        ("eval", look_at_monster(2.3)), ("wait", 0.5), ("shot", "f1_front"),
        ("eval", look_at_monster(0.9, 0, 1.62)), ("wait", 0.4), ("shot", "f2_face"),
        ("eval", look_at_monster(0.2, 2.3)), ("wait", 0.4), ("shot", "f3_side"),
        ("eval", "var m=NightShift.Monster.MonsterAI.Instance; m.SetRewindLock(false); return \"walk\";"),
        ("eval", look_at_monster(4.0, 1.2)), ("wait", 0.8), ("shot", "f4_walk_a"), ("wait", 0.3), ("shot", "f5_walk_b"),
        ("eval", "var m=NightShift.Monster.MonsterAI.Instance; m.ForceAggro(); return \"aggro\";"),
        ("eval", look_at_monster(5.0, 2.0)), ("wait", 0.5), ("shot", "f6_chase"),
        ("eval", "var s=UnityEngine.GameObject.Find(\"Stalker\"); var p=" + FPC + "; var t=s.transform; p.Warp(t.position+t.forward*2.2f, UnityEngine.Quaternion.identity); var to=(t.position+UnityEngine.Vector3.up*1.2f)-p.EyePosition; p.SetLook(UnityEngine.Mathf.Atan2(to.x,to.z)*UnityEngine.Mathf.Rad2Deg, -8f); return \"stalker\";"),
        ("wait", 0.4), ("shot", "f7_colleague"),
    ],
    "tour": [
        ("shot", "00_title"),
        ("eval", START), ("wait", 3.0), ("eval", FREEZE_MONSTER), ("shot", "01_spawn"),
        ("eval", warp(13.5, 0.05, 8.6, 20, 5)), ("wait", 0.6), ("shot", "02_lobby_clock"),
        ("eval", warp(16.8, 0.05, 6.5, -90, 3)), ("wait", 0.6), ("shot", "03_corridor"),
        ("eval", warp(-14.0, 0.05, 3.0, 125, 8)), ("wait", 0.6), ("shot", "04_farm"),
        ("eval", warp(0.2, 0.05, -8.6, 180, 22)), ("wait", 0.6), ("shot", "05_mydesk"),
        ("eval", warp(15.6, 0.05, 2.4, 40, 18)), ("wait", 0.6), ("shot", "06_copier"),
        ("eval", warp(-1.6, 0.05, 9.0, 25, 10)), ("wait", 0.6), ("shot", "07_break"),
        ("eval", warp(7.6, 0.05, 10.2, 75, 5)), ("wait", 0.6), ("shot", "08_breaker"),
        ("eval", warp(-7.6, 0.05, 9.0, 40, 12)), ("wait", 0.6), ("shot", "09_meeting2"),
        ("eval", warp(-15.6, 0.05, 9.4, -35, 10)), ("wait", 0.6), ("shot", "10_manager"),
        ("eval", "var m=NightShift.Monster.MonsterAI.Instance; var p=" + FPC + "; var t=m.transform; "
                 "p.Warp(t.position+t.forward*2.2f, UnityEngine.Quaternion.identity); var to=t.position-p.transform.position; "
                 "p.SetLook(UnityEngine.Mathf.Atan2(to.x,to.z)*UnityEngine.Mathf.Rad2Deg, 2f); return \"faced\";"),
        ("wait", 0.6), ("shot", "11_monster"),
        ("eval", STATE),
    ],
}


def main():
    out_dir, scenario = sys.argv[1], sys.argv[2]
    if len(sys.argv) > 3 and sys.argv[3] == "--rebuild":
        rebuild()
    os.makedirs(out_dir, exist_ok=True)
    for step in SCENARIOS[scenario]:
        kind = step[0]
        if kind == "wait":
            time.sleep(step[1])
        elif kind == "eval":
            print("EVAL", ev(step[1]))
        elif kind == "shot":
            print("SHOT", shot(out_dir, step[1]))



def editor_status():
    status, text = ucmd._request("/api/editor_status")
    try:
        return json.loads(text)
    except ValueError:
        return {}


def wait_ready(min_loads=None, limit=240):
    """Wait until the Editor is idle (not compiling / reloading); returns codeLoadCount."""
    t0 = time.time()
    while time.time() - t0 < limit:
        try:
            s = editor_status()
        except Exception:
            time.sleep(2)
            continue
        if s and not s.get("compiling") and not s.get("domainReloadInProgress"):
            if min_loads is None or s.get("codeLoadCount", 0) >= min_loads:
                return s.get("codeLoadCount", 0)
        time.sleep(2)
    raise RuntimeError("editor not ready")


def rebuild():
    print("STOP", ev("UnityEditor.EditorApplication.isPlaying = false; return \"stopping\";"))
    time.sleep(3)
    before = wait_ready()
    print("REFRESH", ev("UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceUpdate); return \"refreshed\";", 300))
    time.sleep(4)
    loads = wait_ready()
    print("code loads", before, "->", loads)
    print("BUILD", ev("NightShift.EditorBuild.OfficeBuilder.Run(); return \"built\";", 600))
    log = ev("var l=System.IO.File.ReadAllLines(UnityEngine.Application.consoleLogPath); var o=new System.Text.StringBuilder(); "
             "int start=0; for(int i=l.Length-1;i>=0;i--){ if(l[i].Contains(\"NSBUILD DONE\")){ start=i; break; } } "
             "for(int i=System.Math.Max(0,start-400);i<=start;i++) if(l[i].StartsWith(\"NSBUILD\")&&(l[i].Contains(\"NAV\")||l[i].Contains(\"FLOAT\")||l[i].Contains(\"OVERLAP\")||l[i].Contains(\"floatcheck\")||l[i].Contains(\"missing\")||l[i].Contains(\"navmesh\"))) o.AppendLine(l[i]); return o.ToString();")
    print(log)
    print("PLAY", ev("UnityEditor.EditorApplication.isPlaying = true; return \"playing\";"))
    time.sleep(8)
    wait_ready()
    warm_up()


def warm_up():
    """Visit every area once so async shader compilation finishes before scored captures."""
    ev(START)
    time.sleep(2)
    for x, z, yaw in [(13.5, 8.6, 20), (16.8, 6.5, -90), (-14, 3, 125), (0.2, -8.6, 180), (15.6, 2.4, 40),
                      (-1.6, 9, 25), (7.6, 10.2, 75), (-7.6, 9, 40), (-15.6, 9.4, -35), (0, -3, 90), (0, -3, -90)]:
        ev(warp(x, 0.05, z, yaw, 8))
        time.sleep(1.0)
    ev("UnityEditor.EditorApplication.isPlaying = false; return \"stop\";")
    time.sleep(3)
    wait_ready()
    ev("UnityEditor.EditorApplication.isPlaying = true; return \"play\";")
    time.sleep(9)
    wait_ready()


if __name__ == "__main__":
    main()
