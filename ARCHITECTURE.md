# THE NIGHT SHIFT — Architecture Contract (binding for all modules)

Unity 6000.6.9f1, URP 17, Input System 1.20. **No legacy `UnityEngine.Input`** anywhere. All runtime code compiles with
zero errors standalone (no cross-module dependencies beyond the APIs below). Every gameplay class is configurable via
public fields (designers tune in Inspector). All gameplay logging uses `Debug.Log("TEL ...")` — the QA harness copies
these into tel.log.

Namespaces: `NightShift.Core`, `NightShift.Player`, `NightShift.Rewind`, `NightShift.Monster`, `NightShift.Scares`,
`NightShift.Objectives`, `NightShift.UI`, `NightShift.Audio`.

## Events bus (NightShift.Core)

```csharp
public static class GameEvents {
    public static event Action<GameState> StateChanged;          // raised by GameManager
    public static event Action<string>    ObjectiveChanged;      // (objectiveText)
    public static event Action            TimelineRewound;       // after each rewind completes
    public static event Action<string>    ScareFired;            // (scareId)
    public static event Action<string,float> StingerRequested;   // (clipId, volume)
}
```

## NightShift.Core

```csharp
public enum GameState { Title, Playing, Paused, Dead, Rewinding, Ending }

public class GameManager : MonoBehaviour {            // scene singleton: "GameSystems"
    public static GameManager Instance { get; }
    public GameState State { get; }
    public int TimelineIndex { get; }                 // # of completed rewinds (0 = first life)
    public int DeathCount { get; }
    public void SetState(GameState s);
    public void RegisterDeath(Vector3 deathPos, string cause); // -> DeathCount++, TimelineIndex unchanged until rewind done
    public string QaTel();                            // "state=Playing deaths=2 timeline=2"
}
```

```csharp
public class ComposureMeter : MonoBehaviour {         // 100 -> 0; drains near monster / on scares; regen when safe
    public float Value { get; }
    public void Drain(float amount, string reason);   // logs "TEL COMP drain=<amt> reason=<r> value=<v>"
    public void Regen(float amount);
    public string QaTel();
}
```

## NightShift.Player

- Player root GameObject named **"Player"**, tag **Player**, has CharacterController + `FirstPersonController` + `PlayerInteractor` + `TimelineRecorder`. Camera child named "PlayerCamera".
- Movement: WASD + LeftShift sprint, walk 2.2 m/s, sprint 4.2 m/s, gravity, eye height 1.62, mouse look (Mouse.current.delta), sensitivity configurable, pitch clamp ±89, cursor locked in Playing, subtle head-bob.
- PlayerInteractor: raycast from camera 2.5 m; finds `IInteractable`; shows prompt via UIManager; E key invokes `Interact()`.

```csharp
public interface IInteractable { string Prompt { get; } void Interact(PlayerInteractor by); }

public class FirstPersonController : MonoBehaviour {
    public bool InputLocked { get; set; }             // RewindController sets this during rewind
    public string QaTel();                            // "v=<m/s> sprint=1 locked=0"
}
```

## NightShift.Rewind

```csharp
public class TimelineRecorder : MonoBehaviour {       // on Player AND optionally on monster/props
    public float SampleRate = 15f, HistorySeconds = 5f;
    public struct Sample { public Vector3 pos; public Quaternion rot; }
    public void PushSample();                          // internal timer
    public Sample GetSampleAgo(float secondsAgo);      // interpolated, never IndexOutOfRange
    public IReadOnlyList<Sample> GetHistory();         // oldest -> newest
    public void Clear();
    public string QaTel();                             // "hist=<n> t=<span>s"
}

public class RewindableObject : MonoBehaviour {        // put on doors/props that must restore
    public bool RecordPosition = true, RecordRotation = true;
    // own small history; RewindController replays it backwards in sync with the player
}

public class RewindController : MonoBehaviour {        // singleton on GameSystems
    public static RewindController Instance { get; }
    [Header("Config")] public float RewindDuration = 4f;       // seconds of reversal shown
    public float ReplaySpeed = 1.25f;                          // >1 = faster-than-realtime replay
    public float RecoverySecondsBeforeDeath = 1.2f;            // restore point offset
    public bool IsRewinding { get; }
    public Coroutine BeginRewind(Vector3 deathPos, string cause); // full cinematic sequence
    public event Action RewindFinished;                        // GameManager resumes Playing here
    public string QaTel();
}
```

Rewind sequence (BeginRewind): GameManager.State=Dead → death sting + red flash → State=Rewinding →
UIManager.FlashRewind() + distortion → Timers: disable player input, all KillZones off, MonsterAI.SetRewindLock(true) →
reverse-replay player history (ghost trail: faint camera-path echoes) → white flash → restore player at
GetSampleAgo(RecoverySecondsBeforeDeath), restore all RewindableObjects → Monster keeps memory (never reset) →
State=Playing, TimelineIndex++ → `GameEvents.TimelineRewound` (logs "TEL REWIND deaths=<n> timeline=<n> cause=<c>").

```csharp
public class DeathManager : MonoBehaviour {           // singleton on GameSystems
    public static DeathManager Instance { get; }
    public bool DeathsEnabled { get; set; }           // RewindController toggles during rewind
    public void Kill(string cause);                   // idempotent within a frame; -> RegisterDeath + BeginRewind
    public string QaTel();
}

public class KillZone : MonoBehaviour {               // trigger collider; Kill(cause) on Player enter
    public string Cause = "unknown";
    public bool OneShot = false;
}
```

## NightShift.Monster

Monster root **"Monster"**, tag untagged, NavMeshAgent + `MonsterAI` + `MonsterMemory` + `TimelineRecorder` (rewind
shows IT snapping back too — its memory stays). One child "MonsterVisual" (SkinnedMesh or mesh) with Animator optional.

```csharp
public class MonsterMemory : MonoBehaviour {
    public int DeathCount { get; }
    public int MemoryTier { get; }                    // 0..4 (see DESIGN.md thresholds, configurable)
    public Vector3 LastDeathPosition { get; }
    public Vector3[] DeathPositions { get; }          // history
    public Vector3 LastKnownPlayerPos { get; set; }
    public void NotifyDeath(Vector3 deathPos);         // called by GameManager.RegisterDeath
    public bool ShouldAnticipateRoute { get; }         // tier >= 2
    public bool ShouldCampObjective { get; }           // tier >= 3
    public string QaTel();                             // "mem=<tier> deaths=<n> lastdeath=<x,z>"
}

public enum MonsterState { Dormant, Patrol, Stalk, Chase, Disguised, Scream, Lunge }

public class MonsterAI : MonoBehaviour {
    public MonsterState CurrentState { get; }
    public void SetRewindLock(bool locked);           // holds position during rewind, no kills
    public void OnScreamComplete();                    // returns to Chase
    public void Teleport(Vector3 pos);                 // scripted scares / tier-4 fake-outs
    public string QaTel();                             // "state=Chase tier=2 speed=3.2 target=12,4"
}
```

Behaviour: Tier 0 patrol waypoints, ignores player. Tier 1 repeats last patrol exactly (same timing windows).
Tier 2 glances at LastDeathPosition when within 10 m; cuts route to LastKnownPlayerPos. Tier 3 camps the next
objective ahead of player (ObjectiveManager.NextObjectivePos), rare voice line via AudioDirector.PlayVoice
("We need to stop meeting like this." at most once per tier, flag persisted). Tier 4 active hunt + Teleport fake-outs.
Kill: when within 1.1 m and player looking (or Lunge scripted) → DeathManager.Kill("monster").

## NightShift.Scares

```csharp
public abstract class JumpscareTrigger : MonoBehaviour {
    public string ScareId;                            // stable id: "printer", "elevator", ...
    public bool FireOnce = true, Enabled = true;
    public float ComposureDamage = 15f;
    protected abstract void Activate();               // game-specific
    protected void FireScare() {                      // call from Activate when the scare actually lands
        GameEvents.ScareFired?.Invoke(ScareId);
        Debug.Log("TEL SCARE " + ScareId);
        // composure drain + stinger via GameEvents.StingerRequested
    }
}
```

Concrete (each in its own file, all with `public string QaTel()`): `PrinterScare` (prints, stops on approach,
teleports behind player, prints face, comedic), `ElevatorScare` (doors open, employee screams then calm line),
`StalkerEmployee` (static pose at waypoint; moves only while unobserved by camera frustum + raycast; whisper on close),
`PhoneCallScare` (ring, voice line "I'm behind you.", second ring caller id YOURSELF — genuine dread, quiet),
`CoffeeMachineScare` (break room event: dispensing accelerates, blackout, monster reveal + Lunge).

## NightShift.Objectives

```csharp
public class Objective : MonoBehaviour, IInteractable {
    public string Id, Title, PromptText;
    public float InteractTime = 1f;                   // hold-to-complete
    public void Complete();
}

public class ObjectiveManager : MonoBehaviour {       // singleton "GameSystems"
    public static ObjectiveManager Instance { get; }
    public IReadOnlyList<Objective> Sequence { get; } // ordered office tasks
    public Objective Current { get; }
    public Vector3 NextObjectivePos { get; }          // used by MonsterMemory tier 3
    public int EscalationLevel { get; }               // rises per completion: drives scare scheduler
    public string QaTel();                             // "obj=2/5 'Refill coffee' esc=2"
}
```

## NightShift.UI (uGUI + TextMeshPro; never OnGUI)

UIManager singleton builds nothing at runtime — scene has a Canvas "UIRoot" with (built by `AgentScripts/BuildUIRoot.cs`):
- TitlePanel: game title, "Start Shift" button (name contains "Start"), "Quit" button.
- PausePanel: Resume (name contains "Resume"), Quit.
- HUD: ObjectiveText (bottom-left), TimelineText (bottom-right "TIMELINE 03"), PromptText (center),
  ComposureBar (thin bottom-center), MonsterVignette (fullscreen radial, alpha = 1 - composure/100).
- RewindOverlay: white fullscreen Image (flash), FlashRewind() does white->distortion hold->clear.
- EndingPanel: timeline count + "YOUR LAST SHIFT" card.

```csharp
public class UIManager : MonoBehaviour {
    public static UIManager Instance { get; }
    public void ShowPrompt(string text); public void HidePrompt();
    public void SetObjective(string text);
    public void SetTimeline(int n);
    public void SetComposure(float v01);
    public void FlashRewind(float seconds);
    public void ShowTitle/ShowHUD/ShowPause/ShowEnding(...);   // panel exclusivity handled here
}
```

## NightShift.Audio

```csharp
public class AudioDirector : MonoBehaviour {          // singleton "GameSystems", mixer buses Master/Ambience/SFX/Monster/UI
    public static AudioDirector Instance { get; }
    public void PlayStinger(string clipId, float volume = 1f);   // clipIds resolved from a public Dict<string,AudioClip>
    public void PlayVoice(string lineId);                        // monster voice lines ("meeting_like_this", ...)
    public void PlayAmbience();                                  // office hum loop, starts on Title
    public void DuckForRewind();                                 // muffles ambience during rewind (lowpass), restores after
    public string QaTel();
}
```

Clip ids (generated by `AgentScripts/GenerateAudio.cs`): `ambience_office`, `sting_death`, `sting_scare_soft`,
`sting_scare_hard`, `rewind_riser` (pre-reversed build), `rewind_impact`, `footstep`, `monster_breath`,
`phone_ring`, `printer`, `elevator_ding`, `voice_meeting`, `voice_im_behind_you`, `voice_first_day`, `voice_got_a_minute`.

## QA / telemetry contract (mandatory)

1. Player object named "Player", tag `Player`; Monster named "Monster".
2. `QaTel()` on: GameManager, FirstPersonController, MonsterAI, MonsterMemory, RewindController, TimelineRecorder
   (player's), ObjectiveManager, ComposureMeter, AudioDirector.
3. `Debug.Log("TEL <event>")` for: `DEATH cause=<c> pos=<x,z>`, `REWIND ...` (from RewindController), `SCARE <id>`,
   `OBJECTIVE <i> '<title>' done`, `MEMORY tier=<n> deaths=<n>`, `STATE <old>-><new>`, `KILLZONE off/on`.
4. State changes in GameManager.SetState log `TEL STATE ...`.
5. Buttons findable by QA `click @Start` / `click @Resume` (uGUI TMP text or button names contain those words).
