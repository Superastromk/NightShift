# THE NIGHT SHIFT

*You get to go back. It doesn't.*

A first-person horror-comedy prototype built in Unity 6000.6.9f1, using URP and the Input System.

You are the new night-shift employee in an ordinary corporate office. Something is roaming the floor with you. When it kills you, time rewinds: you remember what happened, and increasingly, so does it.

## Launch

On Windows, run `Builds/Windows/Play TheNightShift.cmd`, or open `Builds/Windows/TheNightShift.exe`.

Keep `TheNightShift_Data` and `UnityPlayer.dll` beside the executable. The build is not a single-file portable application.

## Controls

| Input | Action |
|---|---|
| WASD / arrows | Move |
| Shift | Sprint |
| Mouse | Look |
| E, held | Interact with the object under the crosshair |
| F | Flashlight |
| Left Ctrl / C | Crouch; hold or toggle in Settings |
| Esc | Pause |
| Enter / Space | Start shift from the title |

## The shift

Complete ten office tasks, including the quarterly report, coffee, the main breaker in Storage, the lift-lobby parcel, and one last plant. Finish them and reach the elevator.

Death rewinds the player a few seconds. The TIMELINE counter records each rewind. The monster's memory escalates across deaths, making later hunts less forgiving. Red chevrons around the crosshair indicate damage directions; the outer boss chevron indicates its direction during a hunt and changes color with danger.

## Verification

The `qa/` directory contains test routes, screenshots, telemetry, and reports. Verified in built-player tests:

- Release launcher, title, movement, pause, and resume.
- Ordinary held E clock-in, flashlight, and Left Ctrl crouch in the Windows release.
- Settings, How to Play, the shift-report ending, and Return to Title.
- Main breaker task 7/10 through a real held E interaction, followed by power restoration and task 8/10.
- Damage-direction and boss-direction HUD indicators, including simultaneous damage, front/rear bearings, lethal damage, rewind clearing, and pause clearing.
- Death, rewind, resume, timeline counter, and escalating monster memory.
- Sixteen naturalized voice lines have nonempty mono PCM WAV assets with successful Unity imports.

The release smoke test is complete. A complete no-checkpoint playthrough and a listening review of the dialogue have not been performed. The environment still contains prototype geometry and materials. Character limitations include the Boss's neutral mouth, minor scalp artifacts, and a small intern temple wedge. Existing nonfatal URP warnings concern unsupported or stripped unused depth-of-field and Panini Projection passes.

## Project

- `Assets/Scenes/OfficeFloor.unity` — office scene.
- `Assets/Scripts/` — runtime gameplay.
- `Assets/Editor/NightShift/` — scene and character builders.
- `Assets/Art/Humans/` — imported character models and materials.
- `SourceAssets/Humans/` — source models, local modelling scripts, proofs, and credits.
- `AgentScripts/` — retained editor-side generation utilities.
- `tools/` — staging, dialogue, and project-code maintenance scripts.

Character provenance and graphical-asset licensing are recorded under `SourceAssets/Humans/`. MakeHuman/MPFB application code is separate from the CC0 graphical assets and generated character output.

Development-only QA hooks are excluded from release code. Test setup that advances objectives or injects HUD damage is not a substitute for ordinary player-input verification.
