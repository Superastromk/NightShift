using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using NightShift.Audio;
using NightShift.Core;
using NightShift.Monster;
using NightShift.Objectives;
using NightShift.Player;
using NightShift.Rewind;
using NightShift.Scares;
using NightShift.UI;
using NightShift.World;

namespace NightShift.Progression
{

public class ShiftDirector : MonoBehaviour
    {
        public static ShiftDirector Instance { get; private set; }

        [Header("Act 2 — power cut")]
        [Tooltip("Objective id whose completion kills the power (start of Act 2).")]
        public string PowerCutAfterId="coffee";
        [Tooltip("Seconds after that objective before the power dies (lets its own scare play).")]
        public float PowerCutDelay = 9f;
        [Tooltip("Objective id that restores power.")]
        public string RestorePowerId = "breaker";
        public string Act2IntercomClip="intercom_act2";
        public string Act2Subtitle = "[INTERCOM] Temporal Recovery is an employee-only service. Please ensure any further deaths are strictly necessary.";

        [Header("Act 3 — final chase")]
        public string Act3IntercomClip = "intercom_act3";
        public string Act3Subtitle="[INTERCOM] That concludes your shift. Take the nearest elevator and keep your eyes forward.";
        public string Act3Objective = "GET TO THE ELEVATOR";
        public float Act3HuntDelay = 3.5f;
        [Tooltip("Chase speed when the hunt (re)starts. Player sprint is ~4.7 m/s but stamina-limited.")]
        public float Act3StartSpeed = 3.2f;
        public float Act3SpeedRamp = 0.11f;
        public float Act3MaxSpeed = 5.0f;
        [Tooltip("After this many Act 3 deaths the cap drops (mercy) so every player can finish.")]
        public int MercyAfterDeaths=1;
        public float MercyCapPerDeath=0.3f;
        public float MercyMinCap=3.6f;
        public float SpawnMinDist=12f;
        public float SpawnMaxDist=22f;
        public float RespawnHoldSeconds=1.6f;
        public Color EmergencyColor=new Color(1f, 0.16f, 0.1f);
        public float EmergencyIntensityScale=0.75f;
        [Tooltip("Enabled when Act 3 starts (fire shutter sealing the corridor).")]
        public GameObject Act3Barrier;
        public string[] Act3DeathLines =
        {
            "\"Back you go. I'll be waiting.\"",
            "\"I don't get to go back.\"",
            "\"Another meeting. You should have booked.\"",
        };

        [Header("Exit / ending")]
        public Transform ExitPoint;
        public Vector3 ExitPointFallback = new Vector3(18.3f,0f,11.7f);
        public float ExitRadius = 1.8f;
        [Tooltip("Elevator door panels that slide open for the ending.")]
        public Transform[] DoorsLeft = new Transform[0];
        public Transform[] DoorsRight = new Transform[0];
        public float DoorOpenDistance = 0.52f;
        public float DoorOpenSeconds = 1.3f;
        public string EndingTitle = "SHIFT COMPLETE";

        public int Act { get; private set; } = 1;
        public bool EndingStarted { get; private set; }
        public bool Hunting { get; private set; }
        public bool PowerOut { get; private set; }

        int _act3Deaths;
        float _huntTime;
        bool _powerCutQueued, _powerRestored;
        Transform _player;
        FirstPersonController _fpc;
        MonsterAI _monster;
        MonsterMemory _memory;
        Coroutine _respawn;

        void Awake()
        {
            if (Instance != null && Instance != this)
            { Debug.LogWarning("TEL WARN duplicate ShiftDirector"); return; }
            Instance=this;
        }

        void OnDestroy() { if (Instance == this) Instance=null; }
        void OnEnable() { GameEvents.TimelineRewound += OnTimelineRewound; }
        void OnDisable() { GameEvents.TimelineRewound -= OnTimelineRewound; }

        void Start()
        {
            var pgo=GameObject.FindGameObjectWithTag("Player");
            if (pgo != null) { _player = pgo.transform; _fpc = pgo.GetComponent<FirstPersonController>(); }
            _monster = FindFirstObjectByType<MonsterAI>();
            if (_monster != null) _memory = _monster.GetComponent<MonsterMemory>();
            if (Act3Barrier != null) Act3Barrier.SetActive(false);
        }

        Vector3 ExitPos => ExitPoint != null ? ExitPoint.position : ExitPointFallback;

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing || EndingStarted) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            QaHooks();
#endif

            var om = ObjectiveManager.Instance;
            if (om == null) return;

            if (!_powerCutQueued && om.IsDone(PowerCutAfterId) && !om.IsDone(RestorePowerId)) { _powerCutQueued = true; StartCoroutine(PowerCutRoutine()); }
            if (_powerCutQueued && !_powerRestored && om.IsDone(RestorePowerId)) StartCoroutine(RestorePowerRoutine());
            if (Act < 3 && om.Sequence != null && om.Sequence.Length > 0 && om.Current == null) EnterAct3();

            if (Act == 3)
            {
                if (Hunting) DriveHunt();
                if (_player != null && (HorizontalDistance(_player.position,ExitPos) <= ExitRadius || IsInsideExitCab(_player.position)))
                    StartCoroutine(EndingRoutine());
            }
        }

        bool IsInsideExitCab(Vector3 position)
        {
            if (DoorsLeft == null || DoorsRight == null) return false;
            for (int i = 0; i < Mathf.Min(DoorsLeft.Length,DoorsRight.Length); i++)
            {
                if (DoorsLeft[i] == null || DoorsRight[i] == null) continue;
                var doorway = (DoorsLeft[i].position + DoorsRight[i].position) * 0.5f;
                if (position.x >= doorway.x - 0.6f && position.x <= doorway.x + 2f &&
                    Mathf.Abs(position.z - doorway.z) < 0.9f && Mathf.Abs(position.y) < 1f) return true;
            }
            return false;
        }

IEnumerator PowerCutRoutine()
        {
            yield return new WaitForSeconds(PowerCutDelay);
            while (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) yield return null;
            if (Act >= 3 || _powerRestored || (ObjectiveManager.Instance != null && ObjectiveManager.Instance.IsDone(RestorePowerId))) yield break;
            Act = 2;
            PowerOut = true;
            Debug.Log("TEL ACT 2");
            Debug.Log("TEL POWER off");
            OfficeLight.StutterAll(0.9f);
            GameEvents.RaiseStingerRequested("power_down", 0.9f);
            yield return new WaitForSeconds(0.9f);
            if (_powerRestored || Act >= 3) yield break;
            OfficeLight.PowerAll(false);
            
            _monster?.LoseTrack(12f);
            if (AudioDirector.Instance != null) AudioDirector.Instance.AmbienceScale=0.15f;
            var ui=UIManager.Instance;
            ui?.Toast("ACT II", "POWER FAILURE · FOLLOW THE TASK MARKER", 3.5f);
            yield return new WaitForSeconds(1.8f);
            if (_powerRestored || Act >= 3 || EndingStarted) yield break;
            AudioDirector.Instance?.PlayVoice("sys_power");
            ui?.ShowSubtitle("[SYSTEM] A power fault has been logged. Emergency lighting is offline. Please continue your shift.", 6.5f);
            var torch=Flashlight.Instance;
            if (torch != null && !torch.IsOn) ui?.Hint("<b>F</b>  flashlight      <b>CTRL</b>  crouch to stay quiet and low",9f);
        }

        IEnumerator RestorePowerRoutine()
        {
            _powerRestored = true;
            if (Act < 2) { Act = 2; UIManager.Instance?.Toast("ACT II", "RECOVERY SYSTEM RESTORED", 3f); }
            Debug.Log("TEL POWER on");
            GameEvents.RaiseStingerRequested("breaker",1f);
            yield return new WaitForSeconds(0.5f);
            if (Act < 3)
            {
                OfficeLight.PowerAll(true);
                OfficeLight.StutterAll(1.6f);
            }
            PowerOut = false;
            if (AudioDirector.Instance != null) AudioDirector.Instance.AmbienceScale = 1f;
            yield return new WaitForSeconds(2.2f);
            if (Act < 3)
            {
                AudioDirector.Instance?.PlayVoice(Act2IntercomClip);
                UIManager.Instance?.ShowSubtitle(Act2Subtitle,ClipSeconds(Act2IntercomClip,7f));
            }
        }

void EnterAct3()
        {
            Act = 3;
            Debug.Log("TEL ACT 3");
            foreach (var e in FindObjectsByType<JumpscareTrigger>(FindObjectsSortMode.None)) e.Enabled = false;
            var om = ObjectiveManager.Instance;
            if (om != null) { om.SetOverrideText(Act3Objective); om.MarkerOverride = ExitPos + Vector3.up * 1.2f; }
            StartCoroutine(Act3Opening());
        }

        IEnumerator Act3Opening()
        {
            var ui = UIManager.Instance;
            OfficeLight.PowerAll(true);
            PowerOut = false;
            if (AudioDirector.Instance != null) AudioDirector.Instance.AmbienceScale = 0.6f;
            OfficeLight.StutterAll(1.2f);
            GameEvents.RaiseStingerRequested("power_down", 0.8f);
            yield return new WaitForSeconds(1.2f);
            if (EndingStarted) yield break;
            OfficeLight.EmergencyAll(true, EmergencyColor, EmergencyIntensityScale);
            Debug.Log("TEL LIGHTS emergency=1");
            if (Act3Barrier != null) { Act3Barrier.SetActive(true); GameEvents.RaiseStingerRequested("door_slam",1f); }
            ui?.Toast("ACT III", "TASKS DONE · REACH EITHER ELEVATOR", 3.5f);
            GameEvents.RaiseStingerRequested("alarm", 0.5f);
            AudioDirector.Instance?.PlayVoice(Act3IntercomClip);
            ui?.ShowSubtitle(Act3Subtitle, ClipSeconds(Act3IntercomClip, 6f));
            yield return new WaitForSeconds(Act3HuntDelay);
            if (EndingStarted || Hunting || _respawn != null) yield break;
            while (!EndingStarted && GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) yield return null;
            if (EndingStarted) yield break;
            StartHunt("act3_start");
        }

        void StartHunt(string reason)
        {
            if (_monster == null || _player == null) { Debug.LogWarning("TEL WARN ShiftDirector: no monster/player for hunt"); return; }
            Vector3 spawn = PickSpawnBehindPlayer();
            _monster.SetRewindLock(false);
            _monster.Teleport(spawn);
            
            _monster.RequireLineOfSight = false;
            _monster.FovAngle = 360f;
            _monster.SightRange = 200f;
            _monster.TorchSightBonus = 1f;
            _monster.CrouchSightScale = 1f;
            _huntTime = 0f;
            _monster.ChaseSpeed = Act3StartSpeed;
            _monster.ChaseRamp = 0f;
            _monster.ChaseSpeedPerTier = 0f;
            _monster.ForceAggro();
            var anim = _monster.GetComponentInChildren<FigureAnimator>();
            if (anim != null) anim.Revealed = true;
            Hunting = true;
            GameEvents.RaiseStingerRequested("monster_breath", 1f);
            Debug.Log($"TEL ACT3 hunt reason={reason} spawn={spawn.x:0.#},{spawn.z:0.#} dist={HorizontalDistance(spawn, _player.position):0.#} deaths={_act3Deaths} cap={CurrentCap():0.##}");
        }

        float CurrentCap()
        {
            int over=Mathf.Max(0, _act3Deaths - MercyAfterDeaths + 1);
            return Mathf.Max(MercyMinCap, Act3MaxSpeed - over * MercyCapPerDeath);
        }

        void DriveHunt()
        {
            if (_monster == null || _player == null) return;
            _huntTime += Time.deltaTime;
            _monster.ChaseSpeed=Mathf.Min(CurrentCap(), Act3StartSpeed + _huntTime * Act3SpeedRamp);
            if (_memory != null) _memory.LastKnownPlayerPos=_player.position;
            var s=_monster.CurrentState;
            if (s != MonsterState.Chase && s != MonsterState.Lunge && s != MonsterState.Scream) _monster.ForceAggro();
        }

        Vector3 _lastSpawn=new Vector3(999f, 0f, 999f);

        Vector3 PickSpawnBehindPlayer()
        {
            Vector3 spawn=PickSpawnCandidate();
            _lastSpawn=spawn;
            return spawn;
        }

        Vector3 PickSpawnCandidate()
        {
            Vector3 p=_player.position;
            Vector3 exit=ExitPos;
            Vector3 best=Vector3.zero;
            float bestScore=float.NegativeInfinity;
            bool found=false;
            var wps=_monster.Waypoints;
            if (wps != null)
            {
                foreach (var w in wps)
                {
                    if (w == null) continue;
                    float d=HorizontalDistance(w.position, p);
                    if (d < SpawnMinDist || d > SpawnMaxDist) continue;
                    float behind=HorizontalDistance(w.position, exit) - HorizontalDistance(p, exit);
                    float score=behind - Mathf.Abs(d - (SpawnMinDist + 2f)) * 0.3f;
                    if ((w.position - _lastSpawn).sqrMagnitude < 4f) score -= 6f;   
                    if (score > bestScore) { bestScore=score; best=w.position; found=true; }
                }
            }
            if (found && bestScore > -4f) return best;
            Vector3 away=p - exit; away.y=0f;
            if (away.sqrMagnitude < 0.01f) away=-_player.forward;
            Vector3 cand=p + away.normalized * SpawnMinDist;
            if (NavMesh.SamplePosition(cand, out var hit, 6f, NavMesh.AllAreas)) return hit.position;
            return found ? best : cand;
        }

        void OnTimelineRewound()
        {
            if (Act != 3 || EndingStarted) return;
            _act3Deaths++;
            if (_respawn != null) StopCoroutine(_respawn);
            _respawn=StartCoroutine(Act3Respawn());
        }

        IEnumerator Act3Respawn()
        {
            yield return null; 
            if (_monster == null || _player == null) yield break;
            Hunting=false;
            _monster.Teleport(PickSpawnBehindPlayer());
            _monster.SetRewindLock(true);
            int i=_act3Deaths - 1;
            if (Act3DeathLines != null && i >= 0 && i < Act3DeathLines.Length)
                UIManager.Instance?.ShowSubtitle(Act3DeathLines[i], 2.8f, true);
            Debug.Log($"TEL ACT3 respawn deaths={_act3Deaths}");
            yield return new WaitForSeconds(RespawnHoldSeconds);
            if (EndingStarted) yield break;
            while (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) yield return null;
            StartHunt("rewind");
            _respawn = null;
        }

IEnumerator EndingRoutine()
        {
            EndingStarted = true;
            Hunting = false;
            if (_respawn != null) { StopCoroutine(_respawn); _respawn = null; }
            Debug.Log("TEL ENDING start");

            if (DeathManager.Instance != null) DeathManager.Instance.DeathsEnabled = false;
            if (_fpc != null) _fpc.InputLocked = true;
            var om = ObjectiveManager.Instance;
            if (om != null) om.MarkerOverride = null;
            var anim = _monster != null ? _monster.GetComponentInChildren<FigureAnimator>() : null;
            if (_monster != null)
            {
                _monster.SetRewindLock(true);
                _monster.Teleport(PickFarPoint());
            }
            AudioDirector.Instance?.SetTension(0f, false);
            var ui = UIManager.Instance;
            ui?.SetCinematic(true);

            
            GameEvents.RaiseStingerRequested("elevator_ding", 1f);
            yield return TurnPlayerToward(ExitPos + new Vector3(2f, 1.4f, 0f), 0.8f);
            StartCoroutine(SlideDoors());
            OfficeLight.EmergencyAll(false, Color.white, 1f);
            if (AudioDirector.Instance != null) AudioDirector.Instance.AmbienceScale=1f;
            ShiftClock.ForceEnd();
            ui?.ShowSubtitle("6:00 AM. Your shift is over.",2.2f,true);
            yield return new WaitForSeconds(2.4f);

            
            yield return new WaitForSeconds(1.2f);

            
            if (_monster != null && _player != null)
            {
                Vector3 back = -_player.forward; back.y = 0f;
                Vector3 pos = _player.position + back.normalized * 1.3f;
                _monster.Teleport(pos);
                Vector3 look = _player.position - _monster.transform.position; look.y = 0f;
                if (look.sqrMagnitude > 0.001f) _monster.transform.rotation = Quaternion.LookRotation(look);
                if (anim != null) { anim.Revealed = true; anim.ForcePose = FigureAnimator.Pose.Loom; }
            }
            GameEvents.RaiseStingerRequested("monster_breath", 1f);
            yield return new WaitForSeconds(0.9f);
            if (_monster != null) yield return TurnPlayerToward(_monster.transform.position + Vector3.up * 1.7f, 1.2f);
            GameEvents.RaiseStingerRequested("sting_scare_soft", 0.7f);
            yield return new WaitForSeconds(0.5f);
            AudioDirector.Instance?.PlayVoice("voice_going_down");
            ui?.ShowSubtitle("\"Going down, are we?\"",1.8f,true);
            yield return new WaitForSeconds(1.8f);

            
            GameEvents.RaiseStingerRequested("sting_scare_hard", 1f);
            if (_fpc != null) _fpc.Shake = 1f;
            ui?.SetFade(1f);
            Debug.Log("TEL ENDING blackout");
            yield return new WaitForSeconds(2f);

            
            GameEvents.RaiseStingerRequested("phone_ring", 0.8f);
            ui?.ShowSubtitle("1 NEW VOICEMAIL  —  FROM: YOU",2.6f,true);
            yield return new WaitForSeconds(2.8f);
            AudioDirector.Instance?.PlayVoice("voice_voicemail");
            ui?.ShowSubtitle("\"It's me. Well, you. Don't get in that elevator.\"", 4f, true);
            yield return new WaitForSeconds(4.3f);
            AudioDirector.Instance?.PlayVoice("voice_took_elevator");
            ui?.ShowSubtitle("\"You got in, didn't you? You never listen.\"",3f,true);
            yield return new WaitForSeconds(3.4f);

            
            var gm = GameManager.Instance;
            int timelines = gm != null ? gm.TimelineIndex : 0;
            int deaths = gm != null ? gm.DeathCount : 0;
            string verdict = deaths == 0 ? "You made it through without dying."
                : deaths == 1 ? "You died once. The night remembers."
                : $"You died {deaths} times. It remembers every one.";
            string body =$"TIMELINE {timelines:00}\n\n" + verdict;
            gm?.SetState(GameState.Ending);
            ui?.SetEndingText(EndingTitle,body);
            ui?.SetFade(0f);
            AudioDirector.Instance?.PlayVoice("voice_same_time");
            Debug.Log($"TEL ENDING card timelines={timelines} deaths={deaths}");
            Debug.Log("TEL FLOW ending");
        }

        Vector3 PickFarPoint()
        {
            Vector3 far=_monster.transform.position; float best=-1f;
            if (_monster.Waypoints != null && _player != null)
                foreach (var w in _monster.Waypoints)
                {
                    if (w == null) continue;
                    float d=HorizontalDistance(w.position,_player.position);
                    if (d > best) { best=d; far=w.position; }
                }
            return far;
        }

        IEnumerator TurnPlayerToward(Vector3 worldPos,float seconds)
        {
            if (_player == null || _fpc == null) yield break;
            Vector3 to=worldPos - _fpc.EyePosition;
            if (to.sqrMagnitude < 0.0001f) yield break;
            float yaw0=_player.eulerAngles.y,pitch0=_fpc.Pitch;
            float yaw1=Mathf.Atan2(to.x,to.z) * Mathf.Rad2Deg;
            float pitch1=-Mathf.Atan2(to.y,new Vector2(to.x,to.z).magnitude) * Mathf.Rad2Deg;
            float t=0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k=Mathf.SmoothStep(0f,1f,t / seconds);
                _fpc.SetLook(Mathf.LerpAngle(yaw0,yaw1,k),Mathf.Lerp(pitch0,pitch1,k));
                yield return null;
            }
            _fpc.SetLook(yaw1,pitch1);
        }

        IEnumerator SlideDoors()
        {
            int nl=DoorsLeft != null ? DoorsLeft.Length : 0,nr=DoorsRight != null ? DoorsRight.Length : 0;
            var l0=new Vector3[nl]; var r0=new Vector3[nr];
            for (int i=0; i < nl; i++) if (DoorsLeft[i] != null) l0[i]=DoorsLeft[i].position;
            for (int i=0; i < nr; i++) if (DoorsRight[i] != null) r0[i]=DoorsRight[i].position;
            float t=0f;
            while (t < DoorOpenSeconds)
            {
                t += Time.deltaTime;
                float k=Mathf.SmoothStep(0f,1f,t / DoorOpenSeconds) * DoorOpenDistance;
                for (int i=0; i < nl; i++) if (DoorsLeft[i] != null) DoorsLeft[i].position=l0[i] + Vector3.back * k;
                for (int i=0; i < nr; i++) if (DoorsRight[i] != null) DoorsRight[i].position=r0[i] + Vector3.forward * k;
                yield return null;
            }
        }

void QaHooks()
        {
            var kb=Keyboard.current;
            if (kb == null) return;
            if (kb.f7Key.wasPressedThisFrame)
            {
                var cur=ObjectiveManager.Instance != null ? ObjectiveManager.Instance.Current : null;
                if (cur != null) { Debug.Log("TEL QA F7 complete " + cur.Id); cur.Complete(); }
            }
            if (kb.f6Key.wasPressedThisFrame && _fpc != null)
            {
                _fpc.Warp(ExitPos + new Vector3(-ExitRadius * 0.5f,0.1f,0f),Quaternion.Euler(0f,90f,0f));   
                Debug.Log("TEL QA F6 player to elevator approach");
            }
            if (kb.f5Key.wasPressedThisFrame && _fpc != null)
            {
                var cur = ObjectiveManager.Instance != null ? ObjectiveManager.Instance.Current : null;
                if (cur != null && cur.Id == RestorePowerId)
                    foreach (var light in OfficeLight.All)
                        if (light.Group == "storage") Debug.Log("TEL QA LIGHT " + light.QaTel());
                if (cur != null && (TryFindSpotFacing(cur,out var stand,out var target) || cur.Id == RestorePowerId))
                {
                    
                    
                    if (cur.Id == RestorePowerId)
                    {
                        var col=cur.GetComponentInChildren<Collider>();
                        target=col != null ? col.bounds.center : cur.MarkerPosition;
                        stand=new Vector3(target.x,0f,target.z - 1.2f);
                    }
                    _fpc.Warp(stand + Vector3.up * 0.05f,Quaternion.identity);
                    Vector3 to=target - _fpc.EyePosition;
                    _fpc.SetLook(Mathf.Atan2(to.x,to.z) * Mathf.Rad2Deg,-Mathf.Atan2(to.y,new Vector2(to.x,to.z).magnitude) * Mathf.Rad2Deg);
                    Debug.Log("TEL QA F5 player to objective " + cur.Id + " stand=" + stand.ToString("F2") + " target=" + target.ToString("F2"));
                }
            }
        }

        
        static bool TryFindSpotFacing(Objective task,out Vector3 stand,out Vector3 target)
        {
            var col = task.GetComponentInChildren<Collider>();
            target = col != null ? col.bounds.center : task.MarkerPosition;
            stand = Vector3.zero;
            float best = float.MaxValue;
            int mask = ~((1 << 6) | (1 << 2));   
            
            
            for (int pass = 0; pass < 2 && best == float.MaxValue; pass++)
            for (int ring = 0; ring < 3; ring++)
                for (int i = 0; i < 24; i++)
                {
                    float r = 0.75f + ring * 0.35f,a = i / 24f * Mathf.PI * 2f;
                    var candidate = new Vector3(target.x + Mathf.Cos(a) * r,0f,target.z + Mathf.Sin(a) * r);
                    if (!NavMesh.SamplePosition(candidate,out var hit,0.25f,NavMesh.AllAreas)) continue;
                    
                    int blockers = pass == 0 ? mask : mask & ~(1 << 7);
                    if (Physics.CheckCapsule(hit.position + Vector3.up * 0.35f,hit.position + Vector3.up * 1.5f,0.32f,blockers,QueryTriggerInteraction.Ignore)) continue;
                    Vector3 eye = hit.position + Vector3.up * 1.67f,dir = target - eye;
                    if (dir.magnitude > 2.4f) continue;
                    if (!Physics.Raycast(eye,dir.normalized,out var rh,2.6f,mask,QueryTriggerInteraction.Ignore)) continue;
                    if (rh.collider.GetComponentInParent<Objective>() != task) continue;
                    float score = Mathf.Abs(r - 1f);
                    if (task.Id == "breaker")
                    {
                        
                        
                        Vector3 away = (candidate - target).normalized;
                        score += (1f - Vector3.Dot(away, Vector3.back)) * 1.1f;
                    }
                    if (score < best)
                    { best = score; stand = hit.position; }
                }
            return best < float.MaxValue;
        }

        static float ClipSeconds(string clipId, float fallback)
        {
            var ad = AudioDirector.Instance;
            if (ad != null && ad.Clips.TryGetValue(clipId, out var c) && c != null) return Mathf.Max(fallback, c.length + 0.3f);
            return fallback;
        }

        static float HorizontalDistance(Vector3 a, Vector3 b)
        { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

        public string QaTel() =>
            $"act={Act} power={(PowerOut ? 0 : 1)} hunting={(Hunting ? 1 : 0)} a3deaths={_act3Deaths} ending={(EndingStarted ? 1 : 0)} mspeed={(_monster != null ? _monster.ChaseSpeed : 0f):0.0}";
    }
}
