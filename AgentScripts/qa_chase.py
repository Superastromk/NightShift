"""Chase test for the monster AI, run against the open Editor in Play mode.

    python qa_chase.py <out_dir>

1. Hidden player: monster is put into Chase 10 m away with a cubicle row between them; we time
   how long it takes to reach the player standing still.
2. Fleeing player: the player is moved down the corridor at sprint pace while we sample the
   distance; a good chaser closes the gap and catches up around the corner.
"""
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import qa_live as q  # noqa: E402

M = "NightShift.Monster.MonsterAI.Instance"


def sample(label):
    return q.ev(f"var m={M}; var p={q.FPC}; var d=UnityEngine.Vector3.Distance(m.transform.position,p.transform.position); "
                f"return \"{label} d=\"+d.ToString(\"0.0\")+\" \"+m.QaTel()+\" gm=\"+{q.GM}.QaTel();")


def place_monster(x, z):
    return q.ev(f"var m={M}; m.SetRewindLock(false); m.Teleport(new UnityEngine.Vector3({x}f,0f,{z}f)); m.ForceAggro(); return \"aggro\";")


def main():
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    print(q.ev(q.START))
    time.sleep(2.5)

    # ---- test 1: hidden behind the cubicle row, monster must path around it
    print(q.ev(q.warp(-1.0, 0.05, -1.6, 0, 0)))
    print(place_monster(-1.0, 3.0))
    t0 = time.time()
    caught = None
    for i in range(30):
        time.sleep(0.5)
        line = sample(f"t={time.time() - t0:4.1f}")
        print(line)
        if "state=Dead" in line or "state=Rewinding" in line:
            caught = time.time() - t0
            break
    print("TEST1 caught_after_s=", caught)
    if caught:
        q.shot(out, "t1_caught")
        time.sleep(7)   # let the rewind finish

    # ---- test 2: flee west down the corridor at 4.6 m/s, turn into the farm at x=-14
    print(q.ev(q.warp(10.0, 0.05, 6.5, -90, 0)))
    print(place_monster(17.0, 6.5))
    x, z = 10.0, 6.5
    t0 = time.time()
    last = time.time()
    caught = None
    while time.time() - t0 < 14:
        now = time.time()
        step = 4.6 * (now - last)
        last = now
        if x > -14.0:
            x -= step
        else:
            z -= step
        q.ev(f"var p={q.FPC}; p.Warp(new UnityEngine.Vector3({x:.2f}f,0.05f,{z:.2f}f), UnityEngine.Quaternion.Euler(0,{-90 if x > -14 else 180},0)); return \"\";")
        line = sample(f"t={now - t0:4.1f} px={x:.1f},{z:.1f}")
        print(line)
        if "state=Dead" in line or "state=Rewinding" in line:
            caught = now - t0
            q.shot(out, "t2_caught")
            break
        time.sleep(0.15)
    print("TEST2 caught_after_s=", caught)


if __name__ == "__main__":
    main()
