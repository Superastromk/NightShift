# THE NIGHT SHIFT — Design Document (Prototype v1)

Tagline: **"You can always go back. It can't."**
Theme: **BOUNCE BACK** — death reverses time; the monster gradually learns to remember.

## 1. Target & look (written from Reference/Target frames — see that folder)

- Camera: first-person, eye height 1.62 m, head-bob subtle, FOV 68 (72 during sprint, 60 during rewind).
- Place: one floor of a corporate office at night — open cubicle farm, long corridor loop, 4 meeting rooms, break room, storage, elevator lobby.
- Palette: desaturated cool greys/blue-greens, warm amber accents (exit signs, desk lamps), near-black shadows.
- Light: fluorescent overheads (many off/flickering), emergency red at corridor ends, monitor glow from cubicles.
- Density: near field (0–3 m) — desk surfaces with papers/mugs/monitors; mid (3–10 m) — cubicle partitions, filing cabinets, plants; far (10–30 m) — corridor depth with door frames, flickering light pools, silhouettes.
- HUD: bottom-left objective text (typewriter font), bottom-right timeline counter "TIMELINE 03", center interaction prompt, thin composure vignette, center flash overlay for rewind.

## 2. Pillars

1. **Rewind is the verb** — every death is a cinematic 4–6 s reversal, never a loading screen.
2. **It remembers** — the monster's behaviour visibly evolves across timelines (the psychological hook).
3. **Horror ↔ comedy contrast** — genuine dread punctured by absurdist timing, never constant jokes.

## 3. Core loop (prototype slice)

1. Spawn at desk. Objective: complete mundane tasks (print report, water plant, deliver folder, get coffee).
2. Environmental wrongness escalates per objective (lights flicker, printer starts, phone rings).
3. Monster introduces itself (disguised as employee), first scripted kill → **demonstrate rewind**.
4. Tasks continue; monster escalates per memory tier; optional deaths teach avoidance.
5. Prototype ends at Act 2 midpoint: elevator lobby event + "TIMELINE N" stinger + title card.

## 4. Rewind mechanic (BOUNCE BACK)

- `TimelineRecorder`: ring buffer, 15 Hz, 5 s history (75 samples) of player pos/rot; `RewindableObject` components record their own transforms.
- Death flow: freeze input → death sting + red pulse → screen distortion (URP volume: chromatic aberration + lens distortion pulse) → recorded samples replay **in reverse** (ghost trail via duplicated camera path points) → white flash → player restored at safe pre-death sample, environment restored → resume.
- Config: rewind duration, recovery point offset, replay speed. Never rewinds full physics — only registered objects.
- During rewind: input locked, no death triggers, monster memory persists (never reset).

## 5. Monster memory progression

| Timeline deaths | Tier | Behaviour |
|---|---|---|
| 0 | 0 | Patrols, ignores player |
| 1 | 1 | Repeats previous behaviour exactly ("first death: same patrol") |
| 2 | 2 | Looks toward last death position; cuts off escape route |
| 3 | 3 | Waits at next objective ahead of the player; rare voice line: *"We need to stop meeting like this."* |
| 4+ | 4 | Hunts actively, fake-outs, appears where it "shouldn't" |

Memory is persistent across rewinds. Config: tier thresholds, voice-line cooldown.

## 6. Jumpscare roster (original, from the brief's style)

1. **THE PRINTER** — prints by itself; stops as you approach; behind you when you turn; prints a face. (Comedy-horror.)
2. **THE ELEVATOR** — opens on a normal employee who screams, then: "Sorry. First day." Later elevators are not safe.
3. **THE WATERCOOLER MEETING** — employee at end of hall gets closer every time you look away; whispers "Have you got a minute?"
4. **THE PHONE** — rings; "I'm behind you."; nothing; rings again; caller ID: YOURSELF. (Genuine dread, no sting.)
5. **THE COFFEE MACHINE** — break room; dispensing sound speeds up; the cup overflows black; lights die; monster reveal. (Prototype's mid-boss scare.)
6. **THE FLICKER PASS** (Act 3, not in prototype slice) — lights die along the corridor; each relight it's one row closer.

Mix: 3 genuine (phone, watercooler, flicker) : 3 absurdist (printer, elevator, coffee).

## 7. Scene & systems architecture

See `ARCHITECTURE.md` (the binding contract between modules). Summary:

- `NightShift.Core` — GameManager (state machine + timeline index), ComposureMeter.
- `NightShift.Player` — FirstPersonController (CharacterController + Input System), PlayerInteractor.
- `NightShift.Rewind` — TimelineRecorder, RewindController, RewindableObject, DeathManager, KillZone.
- `NightShift.Monster` — MonsterAI (NavMesh: Patrol/Stalk/Chase/Disguise/Scream), MonsterMemory.
- `NightShift.Scares` — JumpscareTrigger base + PrinterScare, ElevatorScare, StalkerEmployee, PhoneCallScare, CoffeeMachineScare.
- `NightShift.Objectives` — Objective, ObjectiveManager (sequence + escalation).
- `NightShift.UI` — UIManager (TMP HUD, title/pause/ending, rewind flash overlay).
- `NightShift.Audio` — AudioDirector (ambience, stingers, reversed clips; mixer buses: Master/Ambience/SFX/Monster/UI).
- Editor: `AgentScripts/BuildNightShift.cs` (scene gen + NavMesh + lighting), `AgentScripts/GenerateAudio.cs` (synth clips incl. pre-reversed rewind riser), `AgentScripts/BuildUIRoot.cs`.

## 8. Prototype acceptance checks

1. Player moves/looks with real Input System input; QA playtest completes task 1.
2. First scripted kill → rewind restores player pre-death; TEL logs `REWIND ...`; no input lock afterward.
3. Monster memory tier increments; MonsterAI QaTel shows state + tier.
4. ≥3 jumpscares fire (TEL logs `SCARE <name>`); at least 1 comedic, 1 genuine.
5. HUD legible (audit hud clean), no NOFONT/OVERLAP/OFFSCREEN.
6. Frames: title, office exploration, monster encounter, rewind moment, ending card.
7. `QaBuild.Report`: no SHADER-RISK, no pink materials, no compile errors.

## 9. 30-hour schedule (full game; prototype covers M1–M5 partial)

| Hrs | Milestone |
|---|---|
| 0–3 | Design + architecture (this doc) |
| 3–7 | FP controller + interaction + HUD |
| 7–12 | Timeline recorder + rewind + death flow |
| 12–17 | Office floor + objectives + lighting |
| 17–22 | Monster AI + memory + 2 scares |
| 22–26 | Remaining scares + progression + ending |
| 26–29 | Audio, polish, performance |
| 29–30 | Critic review + fixes + final build |

## 10. 2026-11-15 character and threat-HUD refinement

### Actor detail by camera distance

| Distance | Required detail | Source search and decision |
|---|---|---|
| Far silhouette (10–30 m) | Adult 1.75–1.9 m proportions, office clothing, readable head/shoulder/limb silhouette; the boss remains slightly too tall and long-armed. | Existing project: `Figures.cs` procedural lofts and `FigureAnimator` joint poses. Target frames: Outlast #13–20 for grounded horror silhouettes and The Stanley Parable #21–28 for the office setting. |
| Mid distance (3–10 m) | Continuous clothed body, natural shoulders/hips, articulated hands and feet, distinct colleague/intern/boss faces and hair. | Existing project has no FBX/GLB character set. Unity/open-library route selected: MakeHuman MPFB generated locally with its CC0 system asset pack (`mpfb_human.py`), using the game-engine rig so existing joint-driven poses can be rebound. |
| Near interaction / scare (0–3 m) | Facial topology with actual eye sockets, nose, lips, ears and jaw; textured skin, separate eyes and hair; cloth surface detail rather than flat primitives. | MakeHuman CC0 skins/eyes/hair/clothes, then URP material conversion and rendered close-up review. Each completed modelling pass receives an independent critic review before the next pass. |

### Crosshair threat language

- Keep the existing six-pixel interaction dot and hold ring clear.
- Incoming composure damage or a lethal hit produces a short red directional wedge pointing toward the world-space source.
- While the monster is actively hunting, a separate amber/red chevron around the crosshair points toward the boss, including when it is behind the player.
- Indicators fade quickly, never replace the objective marker, and do not add a permanent minimap or large HUD panel.
