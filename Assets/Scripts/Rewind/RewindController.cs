using System.Collections;
using UnityEngine;
using NightShift.Core;
using NightShift.Monster;
using NightShift.Player;

namespace NightShift.Rewind
{

public class RewindController : MonoBehaviour
    {
        static RewindController _instance;

        public static RewindController Instance
        {
            get
            {
                if (_instance == null) _instance = FindFirstObjectByType<RewindController>();
                return _instance;
            }
        }

        [Header("Config")]
        [Tooltip("How many seconds before the death the player resumes.")]
        public float RecoverySecondsBeforeDeath=5f;
        [Tooltip("Recorded seconds rewound per real second (2 = double speed).")]
        public float ReplaySpeed = 2.2f;
        [Tooltip("Seconds the death beat holds before time reverses.")]
        public float DeathBeatSeconds = 0.85f;
        [Tooltip("Seconds after resume during which new deaths are ignored (chain-kill guard).")]
        public float PostRewindGraceSeconds = 1.25f;
        [Tooltip("World geometry the restore point is validated against.")]
        public LayerMask WorldMask;
        [Tooltip("FOV pulled in during the rewind (negative = tunnel).")]
        public float RewindFovOffset = -12f;
        [HideInInspector] public Material GhostMaterial;

        
        [HideInInspector] public float RewindDuration = 4f;
        [HideInInspector] public bool GhostTrailEnabled = true;

        
        public bool IsRewinding { get; private set; }

        
        public event System.Action RewindFinished;
        
        public float Progress01 { get; private set; }
        FirstPersonController _player;
        TimelineRecorder _playerRecorder;
        CharacterController _playerCC;
        MonsterAI _monster;
        TimelineRecorder _monsterRecorder;

        void Awake()
        {
            if (_instance != null && _instance != this) { Debug.LogWarning("TEL WARN duplicate RewindController"); return; }
            _instance = this;
            if (WorldMask.value == 0) WorldMask = LayerMask.GetMask("Default", "NoNav");
        }

        void OnDestroy() { if (_instance == this) _instance=null; }

        void Start()
        {
            var playerGO=GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
            {
                _player = playerGO.GetComponent<FirstPersonController>();
                _playerRecorder = playerGO.GetComponent<TimelineRecorder>();
                _playerCC = playerGO.GetComponent<CharacterController>();
            }
            else Debug.LogWarning("TEL WARN RewindController: no Player tagged object in scene");
            _monster = FindFirstObjectByType<MonsterAI>();
            if (_monster != null) _monsterRecorder = _monster.GetComponent<TimelineRecorder>();
        }

        
        public Coroutine BeginRewind(Vector3 deathPos, string cause)
        {
            if (IsRewinding) return null;
            return StartCoroutine(RewindRoutine(deathPos, cause));
        }

        IEnumerator RewindRoutine(Vector3 deathPos, string cause)
        {
            IsRewinding = true;
            Progress01 = 0f;
            var rewindables = FindObjectsByType<RewindableObject>(FindObjectsSortMode.None);
            try
            {
                
                GameManager.Instance?.SetState(GameState.Dead);
                if (_player != null) _player.InputLocked = true;
                if (DeathManager.Instance != null) DeathManager.Instance.DeathsEnabled = false;
                GameEvents.RaiseRewindLock(true);
                GameEvents.RaiseStingerRequested("sting_death", 1f);
                if (_player != null) _player.Shake = 1f;

                float t = 0f;
                bool faceMonster = _monster != null && _player != null &&
                                   Vector3.Distance(_monster.transform.position, _player.transform.position) < 7f;
                float startYaw = _player != null ? _player.transform.eulerAngles.y : 0f;
                float startPitch = _player != null ? _player.Pitch : 0f;
                while (t < DeathBeatSeconds)
                {
                    t += Time.deltaTime;
                    if (faceMonster)
                    {
                        Vector3 head = _monster.transform.position + Vector3.up * 1.75f;
                        Vector3 to = head - _player.EyePosition;
                        float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                        float pitch = -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
                        float k = Mathf.Clamp01(t / 0.16f);
                        _player.SetLook(Mathf.LerpAngle(startYaw, yaw, k), Mathf.Lerp(startPitch, pitch, k));
                    }
                    yield return null;
                }

                
                GameManager.Instance?.SetState(GameState.Rewinding);
                ShiftClock.SpinRate = -1f;
                Audio.AudioDirector.Instance?.DuckForRewind();
                GameEvents.RaiseStingerRequested("rewind_riser", 1f);
                UI.UIManager.Instance?.FlashRewind(0.35f);
                if (_player != null) _player.FovOffset=RewindFovOffset;
                foreach (var r in rewindables) if (r != null) r.BeginRewind();

                float span=RecoverySecondsBeforeDeath;
                if (_playerRecorder != null) span=Mathf.Min(span, Mathf.Max(_playerRecorder.Span, 0.05f));
                float pitchAtDeath=_player != null ? _player.Pitch : 0f;

                float ago=0f;
                while (ago < span)
                {
                    ago=Mathf.Min(span, ago + Time.deltaTime * ReplaySpeed);
                    Progress01=ago / span;
                    ApplyAll(ago, rewindables, Mathf.Lerp(pitchAtDeath, 0f, Progress01));
                    yield return null;
                }

                
                float safeAgo=span;
                if (_playerRecorder != null && _playerCC != null)
                {
                    int guard=12;
                    while (guard-- > 0 && !IsFree(_playerRecorder.GetSampleAgo(safeAgo).pos))
                        safeAgo=Mathf.Max(0f, safeAgo - 0.25f);
                    if (!Mathf.Approximately(safeAgo, span))
                    {
                        Debug.Log($"TEL REWIND restore adjusted {span:0.00}->{safeAgo:0.00} (blocked)");
                        ApplyAll(safeAgo,rewindables,0f);
                    }
                }

                GameEvents.RaiseStingerRequested("rewind_impact", 0.85f);
                UI.UIManager.Instance?.FlashRewind(0.5f);
                if (_player != null) { _player.FovOffset = 0f; _player.Shake = 0.35f; }

                
                _playerRecorder?.Clear();
                _monsterRecorder?.Clear();
                foreach (var r in rewindables) if (r != null) r.EndRewind();
                var zones = FindObjectsByType<KillZone>(FindObjectsSortMode.None);
                foreach (var z in zones) if (z != null) z.ReArm();

                
                ShiftClock.SpinRate = 1f;
                Audio.AudioDirector.Instance?.RestoreAfterRewind();
                if (_player != null) _player.InputLocked = false;
                GameManager.Instance?.SetState(GameState.Playing);
                GameManager.Instance?.IncrementTimeline();
                GameEvents.RaiseTimelineRewound();
                Debug.Log($"TEL REWIND deaths={GameManager.Instance?.DeathCount} timeline={GameManager.Instance?.TimelineIndex} cause={cause} back={safeAgo:0.0}s");

                
                if (PostRewindGraceSeconds > 0f) yield return new WaitForSeconds(PostRewindGraceSeconds);
            }
            finally
            {
                ShiftClock.SpinRate = 1f;
                GameEvents.RaiseRewindLock(false);
                if (DeathManager.Instance != null) DeathManager.Instance.DeathsEnabled = true;
                Audio.AudioDirector.Instance?.RestoreAfterRewind();
                if (_player != null)
                { _player.InputLocked = false; _player.FovOffset = 0f; }
                foreach (var r in rewindables) if (r != null) r.EndRewind();
                IsRewinding = false;
                Progress01 = 0f;
                RewindFinished?.Invoke();
            }
        }

        void ApplyAll(float ago, RewindableObject[] rewindables, float pitch)
        {
            if (_playerRecorder != null && _player != null)
            {
                var s = _playerRecorder.GetSampleAgo(ago);
                _player.Warp(s.pos, s.rot);
                _player.SetLook(s.rot.eulerAngles.y, pitch);
            }
            if (_monsterRecorder != null && _monster != null)
            {
                var m = _monsterRecorder.GetSampleAgo(ago);
                _monster.RewindPose(m.pos, m.rot);
            }
            for (int i = 0; i < rewindables.Length; i++)
                if (rewindables[i] != null) rewindables[i].ApplySnapshotAgo(ago);
        }

        bool IsFree(Vector3 feet)
        {
            float r = _playerCC.radius * 0.9f;
            Vector3 a = feet + Vector3.up * (r + 0.08f);
            Vector3 b = feet + Vector3.up * (Mathf.Max(_playerCC.height, 1f) - r);
            return !Physics.CheckCapsule(a, b, r, WorldMask, QueryTriggerInteraction.Ignore);
        }

        
        public string QaTel() => $"rewinding={(IsRewinding ? 1 : 0)} back={RecoverySecondsBeforeDeath}";
    }
}
