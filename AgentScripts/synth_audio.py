"""Synthesises the extra sound effects for THE NIGHT SHIFT (numpy only, 22.05 kHz mono 16-bit WAV).
Usage: python synth_audio.py <Assets/Audio dir>"""
import sys, wave
import numpy as np

SR = 22050
rng = np.random.default_rng(1307)
out_dir = sys.argv[1]


def t(sec):
    return np.arange(int(SR * sec)) / SR


def save(name, x, peak=0.85):
    x = np.asarray(x, dtype=np.float64)
    m = np.max(np.abs(x)) or 1.0
    x = x / m * peak
    with wave.open(f"{out_dir}/{name}.wav", "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((x * 32767).astype("<i2").tobytes())
    print(name, f"{len(x) / SR:.2f}s")


def lowpass(x, cutoff):
    a = np.exp(-2 * np.pi * cutoff / SR)
    y = np.empty_like(x); acc = 0.0
    for i, v in enumerate(x):
        acc = (1 - a) * v + a * acc
        y[i] = acc
    return y


def highpass(x, cutoff):
    return x - lowpass(x, cutoff)


def env(n, attack, decay):
    i = np.arange(n) / SR
    return np.minimum(1, i / max(attack, 1e-5)) * np.exp(-i / decay)


def noise(n):
    return rng.standard_normal(n)


def fade_loop(x, sec=0.25):
    n = int(SR * sec)
    head, tail = x[:n].copy(), x[-n:].copy()
    k = np.linspace(0, 1, n)
    body = x[:-n].copy()
    body[:n] = head * k + tail * (1 - k)
    return body


# --- UI / small
tt = t(0.05); save("click", np.sin(2 * np.pi * 2400 * tt) * env(len(tt), 0.001, 0.008) + 0.4 * noise(len(tt)) * env(len(tt), 0.0005, 0.004), 0.6)
tt = t(0.6); save("task_done", (np.sin(2 * np.pi * 660 * tt) * env(len(tt), 0.004, 0.18)
                               + np.concatenate([np.zeros(int(SR * 0.12)), (np.sin(2 * np.pi * 990 * t(0.48)) * env(int(SR * 0.48), 0.004, 0.2))])[:len(tt)]), 0.5)

# --- footsteps on carpet (two variants)
for name, f in (("footstep_b", 78), ("footstep_c", 92)):
    tt = t(0.22)
    x = np.sin(2 * np.pi * f * tt * (1 - tt * 1.5)) * env(len(tt), 0.004, 0.045) + 0.5 * lowpass(noise(len(tt)), 900) * env(len(tt), 0.002, 0.03)
    save(name, x, 0.7)

# --- doors
tt = t(0.9)
creak = np.sin(2 * np.pi * (180 + 90 * np.sin(2 * np.pi * 3.1 * tt)) * tt + 4 * np.sin(2 * np.pi * 37 * tt)) * (0.5 + 0.5 * np.sin(2 * np.pi * 11 * tt)) ** 2
x = highpass(creak, 300) * np.sin(np.pi * tt / 0.9) ** 0.7 * 0.5
x[:int(SR * 0.06)] += np.sin(2 * np.pi * 900 * t(0.06)) * env(int(SR * 0.06), 0.001, 0.012)  # latch
save("door_open", x, 0.6)
tt = t(0.4); save("door_close", np.sin(2 * np.pi * 95 * tt) * env(len(tt), 0.003, 0.06) + 0.6 * lowpass(noise(len(tt)), 1200) * env(len(tt), 0.001, 0.025), 0.7)
tt = t(0.9); save("door_slam", np.sin(2 * np.pi * 62 * tt * (1 - 0.2 * tt)) * env(len(tt), 0.001, 0.16) + lowpass(noise(len(tt)), 2500) * env(len(tt), 0.0005, 0.05) + 0.3 * noise(len(tt)) * env(len(tt), 0.0005, 0.008), 0.95)
tt = t(1.3)
rat = np.zeros(len(tt))
for k in range(9):
    s = int(SR * (0.05 + k * 0.13 + rng.uniform(-0.02, 0.02)))
    n = int(SR * 0.09)
    rat[s:s + n] += (np.sin(2 * np.pi * rng.uniform(140, 220) * t(0.09)) + 0.8 * lowpass(noise(n), 3000)) * env(n, 0.001, 0.02)
save("door_rattle", rat, 0.85)

# --- props
tt = t(0.45); save("thud", np.sin(2 * np.pi * 70 * tt * (1 - 0.6 * tt)) * env(len(tt), 0.002, 0.08) + 0.5 * lowpass(noise(len(tt)), 700) * env(len(tt), 0.001, 0.04), 0.9)
tt = t(0.8)
cl = np.zeros(len(tt))
for k, s0 in enumerate((0.0, 0.11, 0.2, 0.31, 0.47)):
    s = int(SR * s0); n = int(SR * 0.16)
    f = rng.uniform(900, 2600)
    cl[s:s + n] += (np.sin(2 * np.pi * f * t(0.16)) * 0.6 + highpass(noise(n), 1500)) * env(n, 0.0005, 0.035) * (0.9 ** k)
save("clatter", cl, 0.85)

# --- power / breaker / alarm
tt = t(1.6); f = 420 * np.exp(-tt * 2.3) + 28
save("power_down", (np.sin(2 * np.pi * np.cumsum(f) / SR) + 0.3 * np.sin(2 * np.pi * np.cumsum(f * 2.01) / SR)) * np.exp(-tt * 1.1) + 0.6 * np.concatenate([lowpass(noise(int(SR * 0.08)), 1800) * env(int(SR * 0.08), 0.001, 0.02), np.zeros(len(tt) - int(SR * 0.08))]), 0.9)
tt = t(1.4)
x = np.sin(2 * np.pi * 55 * tt) * env(len(tt), 0.001, 0.1) + lowpass(noise(len(tt)), 2800) * env(len(tt), 0.0005, 0.03)
hum = np.sin(2 * np.pi * 100 * tt) * 0.25 + np.sin(2 * np.pi * 200 * tt) * 0.1
x += hum * np.clip((tt - 0.15) * 3, 0, 1) * np.exp(-np.clip(tt - 0.9, 0, 9) * 6)
save("breaker", x, 0.95)
tt = t(3.0)
al = np.sign(np.sin(2 * np.pi * np.where((tt % 1.0) < 0.5, 620, 465) * tt)) * 0.5
save("alarm", lowpass(al, 1800) * np.minimum(1, (3.0 - tt) * 2) * np.minimum(1, tt * 20), 0.5)

# --- coffee machine
tt = t(3.4)
x = highpass(lowpass(noise(len(tt)), 2400), 250) * (0.5 + 0.5 * np.sin(2 * np.pi * (3 + tt * 3) * tt) ** 2)
x += 0.5 * np.sin(2 * np.pi * (110 + 25 * tt) * tt) * (0.6 + 0.4 * np.sin(2 * np.pi * 17 * tt))
save("coffee_pour", x * np.minimum(1, tt * 6) * np.minimum(1, (3.4 - tt) * 5), 0.7)

# --- intern scream (deliberately a bit ridiculous)
tt = t(1.5)
f0 = 520 + 260 * np.minimum(1, tt * 9) + 30 * np.sin(2 * np.pi * 7.5 * tt) - 90 * np.clip(tt - 1.0, 0, 1)
ph = 2 * np.pi * np.cumsum(f0) / SR
x = np.sin(ph + 2.2 * np.sin(ph * 2.0)) + 0.5 * np.sin(ph * 3.01) + 0.25 * highpass(noise(len(tt)), 2000)
x = np.tanh(x * 1.8)
save("intern_scream", x * np.minimum(1, tt * 40) * np.minimum(1, (1.5 - tt) * 6), 0.9)

# --- heartbeat loop (1.0 s, lub-dub)
tt = t(1.0)
hb = np.zeros(len(tt))
for s0, a, f in ((0.02, 1.0, 52), (0.3, 0.7, 62)):
    s = int(SR * s0); n = int(SR * 0.22)
    hb[s:s + n] += a * np.sin(2 * np.pi * f * t(0.22) * (1 - 0.8 * t(0.22))) * env(n, 0.006, 0.05)
save("heartbeat", lowpass(hb, 300), 0.95)

# --- chase drone loop (6 s)
tt = t(6.25)
dr = (np.sin(2 * np.pi * 41 * tt) + 0.7 * np.sin(2 * np.pi * 43.5 * tt) + 0.45 * np.sin(2 * np.pi * 61.7 * tt)
      + 0.25 * np.sin(2 * np.pi * 123.4 * tt + 2 * np.sin(2 * np.pi * 0.5 * tt)))
dr *= 0.65 + 0.35 * np.sin(2 * np.pi * 2.0 * tt) ** 2
dr += 0.18 * highpass(lowpass(noise(len(tt)), 5000), 2500) * (0.5 + 0.5 * np.sin(2 * np.pi * 0.33 * tt))
save("drone_chase", fade_loop(dr, 0.25), 0.8)
