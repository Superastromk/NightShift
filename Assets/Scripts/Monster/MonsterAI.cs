using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using NightShift.Core;
using NightShift.Objectives;
using NightShift.Player;
using NightShift.Rewind;

namespace NightShift.Monster
{

[RequireComponent(typeof(NavMeshAgent))]
    public class MonsterAI : MonoBehaviour
    {
        [Header("Waypoints (patrol route)")]
        [Tooltip("Patrol waypoints in order. Tier 1 replays the tier-0 route through these.")]
        public Transform[] Waypoints = new Transform[0];

        [Header("Movement speeds (m/s)")]
        [Tooltip("Patrol / fallback movement speed.")]
        public float PatrolSpeed = 1.4f;
        public float StalkSpeed = 1.8f;
        public float ChaseSpeed = 3.2f;
        public float LungeSpeed = 5.5f;
        [Tooltip("Rotation speed in degrees/second.")]
        public float TurnSpeed=360f;
        [Tooltip("Seconds to dwell at each patrol waypoint (tier 0); recorded and replayed at tier 1.")]
        public float WaypointDwellSeconds = 1.5f;

        [Header("Senses")]
        public float SightRange = 14f;
        [Range(1f, 360f)] public float FovAngle = 200f;
        [Tooltip("Require a clear line-of-sight raycast before the player counts as seen.")]
        public bool RequireLineOfSight = true;
        [Tooltip("Seconds without seeing the player before giving up a chase.")]
        public float LoseSightAfter=3f;
        [Tooltip("Player closer than this (and seen) triggers a chase.")]
        public float ChaseTriggerRange = 8f;
        [Tooltip("Player closer than this reveals the disguised monster.")]
        public float DisguiseRevealRange = 3f;
        [Tooltip("Hold this distance while stalking.")]
        public float StalkHoldRange = 10f;

        [Header("Kill")]
        [Tooltip("Kill range in meters (default 1.1).")]
        public float KillRange = 1.1f;
        [Tooltip("In Chase, only kill when the player is roughly facing the monster (always kills at arm's length and during a Lunge).")]
        public bool RequirePlayerLooking = true;
        [Tooltip("Dot(camera.forward, dir to monster) above this counts as 'player looking'.")]
        public float PlayerLookDot = 0.3f;

        [Header("Tier behaviour")]
        [Tooltip("Tier 2: pause and look at the last death position when within this range.")]
        public float DeathGlanceRange = 10f;
        [Tooltip("Tier 2: seconds spent looking at the death position.")]
        public float DeathGlanceSeconds = 2f;
        [Tooltip("Tier 3: camp NextObjectivePos only while the player is at least this far from it.")]
        public float CampPlayerMinDist = 8f;
        [Tooltip("Tier 4: minimum seconds between teleport fake-outs.")]
        public float TeleportFakeoutMinInterval=12f;
        [Tooltip("Tier 4: fake-out teleport happens at most this far from the player.")]
        public float TeleportFakeoutMaxDist = 18f;
        [Tooltip("Tier 4: fake-out teleport at least this far from the player.")]
        public float TeleportFakeoutMinDist = 5f;

        [Header("Lunge")]
        public float LungeTriggerRange = 2.5f;
        public float LungeDuration = 0.8f;
        public float LungeCooldown = 4f;

        [Header("Scream")]
        [Tooltip("Seconds the scripted Scream state holds before auto-returning to Chase.")]
        public float ScreamSeconds = 2.5f;
        [Tooltip("Scripted scares set this flag to push the monster into Scream (consumed in Update; Scream state faces the player).")]
        public bool ScreamRequested;

        [Header("Senses (stealth)")]
        [Tooltip("Height of the monster's eyes above its feet.")]
        public float EyeHeight=1.78f;
        [Tooltip("Geometry that blocks sight (static world + doors). 0 = auto (Default + NoNav).")]
        public LayerMask LosMask;
        [Tooltip("Sight range multiplier while the player's flashlight is on.")]
        public float TorchSightBonus = 1.45f;
        [Tooltip("Sight range multiplier while the player is crouched in the dark.")]
        public float CrouchSightScale = 0.6f;
        [Tooltip("Inside this range it senses the player even from behind.")]
        public float CloseSenseRange=1.7f;
        [Tooltip("Seconds spent looking around at an investigated spot.")]
        public float InvestigateSeconds = 3.5f;
        [Tooltip("Noise below this radius is ignored entirely.")]
        public float MinHeardRadius = 2f;

        [Header("Chase")]
        [Tooltip("Extra chase speed gained per second of continuous hunting.")]
        public float ChaseRamp=0.08f;
        [Tooltip("Cap on the ramp bonus.")]
        public float ChaseRampMax = 0.7f;
        [Tooltip("Extra chase speed per memory tier: it gets better at catching you.")]
        public float ChaseSpeedPerTier = 0.12f;
        [Tooltip("How far ahead (seconds) it aims along the player's movement.")]
        public float LeadSeconds = 0.6f;
        [Tooltip("Before it remembers anything it only gives chase this close.")]
        public float FirstSightChaseRange=3.5f;
        [Tooltip("Seconds after a rewind during which it won't start a chase unless you walk into it.")]
        public float PostRewindCalm = 4f;

        [Header("Initial state")]
        public MonsterState InitialState = MonsterState.Patrol;

public MonsterState CurrentState { get; private set; }

        
        public static MonsterAI Instance { get; private set; }
        
        public bool IsRewindLocked => _rewindLock;
        
        public bool IsTravelling => _hasMoveTarget && !_rewindLock && _staggerTimer <= 0f;
        
        public bool WantsToMove => (_hasMoveTarget || _staggerTimer > 0f) && !_rewindLock;
        
        public float PlanarSpeed { get; private set; }
        
        public bool SeesPlayer { get; private set; }

        float _staggerTimer;
        Vector3 _investigatePos;
        float _investigateLook;
        float _investigateTimeout;
        float _predictTimer;
        Vector3 _prevPos;

        
        public void Stagger(float seconds) { _staggerTimer = Mathf.Max(_staggerTimer, seconds); }

        
        public void Investigate(Vector3 pos)
        {
            if (CurrentState == MonsterState.Chase || CurrentState == MonsterState.Lunge || CurrentState == MonsterState.Scream) return;
            if (NavMesh.SamplePosition(pos, out var hit, 3f, NavMesh.AllAreas)) pos = hit.position;
            _investigatePos = pos;
            EnterState(MonsterState.Investigate);
        }

        
        public void RewindPose(Vector3 pos, Quaternion rot)
        {
            if (_agent != null && _agent.isOnNavMesh) _agent.Warp(pos);
            else transform.position = pos;
            transform.rotation = rot;
        }

        NavMeshAgent _agent;
        MonsterMemory _memory;
        Transform _player;
        Camera _playerCam;

        bool _agentNavigable;
        bool _fallbackWarned;
        bool _rewindLock;

        
        int _wpIndex;
        float _dwellTimer;
        bool _dwelling;
        float _stallTimer;

        
        class PatrolLeg { public int WaypointIndex; public float DwellSeconds; }
        readonly List<PatrolLeg> _recordedPatrol = new List<PatrolLeg>();
        int _replayIndex;
        float _replayDwell;

        
        float _lastSeenTimer;
        Vector3 _moveTarget;
        bool _hasMoveTarget;

        
        bool _glancing;
        float _glanceTimer;
        Vector3 _glancePos;
        int _glancedDeaths; 

        
        bool _meetingVoicePlayed;

        
        float _fakeoutTimer;

        
        float _lungeTimer;
        float _lungeCooldownTimer;

        
        float _screamTimer;

        EventInfo _rewindLockEvent;
        Delegate _rewindLockHandler;

        void Awake()
        {
            Instance = this;
            _agent = GetComponent<NavMeshAgent>();
            _memory = GetComponent<MonsterMemory>();
            if (LosMask.value == 0) LosMask = LayerMask.GetMask("Default", "NoNav");
            _prevPos=transform.position;
            TrySubscribeRewindLock();
        }

        void OnEnable()
        {
            GameEvents.TimelineRewound += OnTimelineRewound;
            NoiseSystem.Heard += OnNoise;
            EnterState(InitialState);
        }

        void OnDisable()
        {
            GameEvents.TimelineRewound -= OnTimelineRewound;
            NoiseSystem.Heard -= OnNoise;
            if (Instance == this) Instance=null;
            if (_rewindLockEvent != null && _rewindLockHandler != null)
            {
                try { _rewindLockEvent.RemoveEventHandler(null, _rewindLockHandler); }
                catch {  }
            }
        }

        void Update()
        {
            CachePlayer();
            CachePlayerCamera();
            UpdateNavigable();

            Vector3 moved=transform.position - _prevPos; moved.y=0f;
            _prevPos=transform.position;
            if (Time.deltaTime > 0.0001f)
                PlanarSpeed=Mathf.Lerp(PlanarSpeed, moved.magnitude / Time.deltaTime, 0.25f);

var gm=GameManager.Instance;
            if (gm != null && gm.State != GameState.Playing) { HoldPosition(); return; }

            if (ScreamRequested && CurrentState != MonsterState.Scream && !_rewindLock)
            {
                ScreamRequested=false;
                EnterState(MonsterState.Scream);
            }

            if (_rewindLock)
            {
                HoldPosition();
                return; 
            }

            _lungeCooldownTimer -= Time.deltaTime;
            _disengageTimer -= Time.deltaTime;
            SeesPlayer=CanSeePlayer();
            UpdateDanger();

            if (_staggerTimer > 0f)
            {
                _staggerTimer -= Time.deltaTime;
                if (_agentNavigable && _agent != null && _agent.isOnNavMesh) { _agent.isStopped=true; _agent.velocity=Vector3.zero; }
                return;
            }

            switch (CurrentState)
            {
                case MonsterState.Dormant: UpdateDormant(); break;
                case MonsterState.Patrol: UpdatePatrol(); break;
                case MonsterState.Stalk: UpdateStalk(); break;
                case MonsterState.Chase: UpdateChase(); break;
                case MonsterState.Disguised: UpdateDisguised(); break;
                case MonsterState.Scream: UpdateScream(); break;
                case MonsterState.Lunge: UpdateLunge(); break;
                case MonsterState.Investigate: UpdateInvestigate(); break;
            }

            ApplyMovement();
            TryKill();
        }

MonsterState _previousState;
        float _chaseTime, _stuckTimer, _repathTimer;
        int _searchPointsLeft;
        Vector3 _playerVelocity, _lastPlayerPos, _lastDestination=new Vector3(9999f, 0f, 0f);

        void EnterState(MonsterState s)
        {
            _previousState=CurrentState;
            CurrentState=s;
            _hasMoveTarget=false;
            switch (s)
            {
                case MonsterState.Patrol:
                    _dwelling=false;
                    if (Tier() >= 1) { _replayIndex=0; _replayDwell=0f; }
                    break;
                case MonsterState.Chase:
                    _lastSeenTimer=0f;
                    if (_previousState != MonsterState.Chase && _previousState != MonsterState.Lunge)
                    {
                        _chaseTime=0f;
                        
                        if (_previousState != MonsterState.Scream) { _staggerTimer=0.35f; Audio.AudioDirector.Instance?.PlayStinger("monster_breath",0.7f); }
                    }
                    _stuckTimer = 0f;
                    break;
                case MonsterState.Lunge:
                    _lungeTimer = LungeDuration;
                    break;
                case MonsterState.Scream:
                    _screamTimer = ScreamSeconds;
                    HoldPosition();
                    break;
                case MonsterState.Disguised:
                    HoldPosition();
                    break;
                case MonsterState.Investigate:
                    _investigateLook = 0f;
                    _investigateTimeout = 14f;
                    _searchPointsLeft = _previousState == MonsterState.Chase ? 2 : 0;
                    break;
            }
        }

        void UpdateInvestigate()
        {
            CheckEngage();
            if (CurrentState != MonsterState.Investigate) return;
            _investigateTimeout -= Time.deltaTime;
            float d = HorizontalDistance(transform.position,_investigatePos);
            if (d > 1.3f && _investigateTimeout > 0f && _investigateLook <= 0f)
            {
                SetMoveTarget(_investigatePos,StalkSpeed * 1.35f);
                return;
            }
            
            HoldPosition();
            _investigateLook += Time.deltaTime;
            float sweep = Mathf.Cos(_investigateLook * 1.7f) * 95f;
            transform.Rotate(0f,sweep * Time.deltaTime,0f);
            if (_investigateLook < InvestigateSeconds * (_searchPointsLeft > 0 ? 0.5f : 1f)) return;

if (_searchPointsLeft > 0 && TryPickSearchPoint(out var next))
            {
                _searchPointsLeft--;
                _investigatePos = next;
                _investigateLook = 0f;
                _investigateTimeout = 10f;
                Debug.Log("TEL MONSTER searching next spot");
                return;
            }
            EnterState(MonsterState.Patrol);
        }

        bool TryPickSearchPoint(out Vector3 point)
        {
            Vector3 bias = _playerVelocity.sqrMagnitude > 0.2f ? _playerVelocity.normalized : transform.forward;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                Vector3 dir = Quaternion.Euler(0f, UnityEngine.Random.Range(-70f, 70f), 0f) * bias;
                Vector3 candidate = _investigatePos + dir * UnityEngine.Random.Range(3f, 7f);
                if (!NavMesh.SamplePosition(candidate, out var hit, 2f, NavMesh.AllAreas)) continue;
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                float length = 0f;
                for (int c = 1; c < path.corners.Length; c++) length += Vector3.Distance(path.corners[c - 1], path.corners[c]);
                if (length > 15f) continue;
                point = hit.position;
                return true;
            }
            point = _investigatePos;
            return false;
        }

        void OnNoise(Vector3 pos, float radius, string source)
        {
            if (_rewindLock || radius < MinHeardRadius) return;
            if (Tier() < 1 && radius < 9f) return; 
            var gm = GameManager.Instance;
            if (gm != null && gm.State != GameState.Playing) return;
            if (CurrentState != MonsterState.Patrol && CurrentState != MonsterState.Investigate && CurrentState != MonsterState.Stalk) return;
            float d = Vector3.Distance(transform.position, pos);
            if (Physics.Linecast(pos + Vector3.up * 0.8f, transform.position + Vector3.up * 1.2f, LosMask, QueryTriggerInteraction.Ignore))
                radius *= 0.5f;   
            if (d > radius) return;
            
            if (CurrentState == MonsterState.Investigate && HorizontalDistance(_investigatePos, pos) < 2.5f) { _investigateLook = 0f; return; }
            Debug.Log(string.Format(CultureInfo.InvariantCulture, "TEL MONSTER heard {0} r={1:0.#} d={2:0.#}", source, radius, d));
            Investigate(pos);
        }

        void UpdateDanger()
        {
            if (_player == null) return;
            float d = HorizontalDistance(transform.position, _player.position);
            bool hunting = CurrentState == MonsterState.Chase || CurrentState == MonsterState.Lunge;
            float danger = hunting ? Mathf.Clamp01(1.15f - d / 16f) : (Tier() >= 1 ? Mathf.Clamp01(1f - d / 7f) * 0.45f : 0f);
            var fx = PostFXDriver.Instance;
            if (fx != null) fx.Danger = Mathf.Lerp(fx.Danger, danger, Time.deltaTime * 3f);
            Audio.AudioDirector.Instance?.SetTension(danger, hunting);
        }

void UpdateDormant()
        {
            HoldPosition();
            
        }

        void UpdatePatrol()
        {
            CheckEngage();
            if (CurrentState != MonsterState.Patrol) return;
            if (TryPatrolStallRecovery()) return;
            
            if (TryDeathGlance()) return;

            if (Waypoints == null || Waypoints.Length == 0)
            {
                HoldPosition();
                return;
            }

            
            if (Tier() >= 3 && TryCampFromPatrol())
            { CheckEngage(); return; }

if (Tier() >= 1 && _recordedPatrol.Count >= 2)
            { UpdatePatrolReplay(); return; }

            Transform wp = Waypoints[_wpIndex % Waypoints.Length];
            if (wp == null)
            { _wpIndex++; return; }

            if (_dwelling)
            {
                _dwellTimer -= Time.deltaTime;
                if (_dwellTimer <= 0f)
                { _dwelling = false; _wpIndex++; }
                return;
            }

            SetMoveTarget(wp.position, PatrolSpeed);
            if (HorizontalDistance(transform.position, wp.position) < 0.6f)
            {
                _dwelling = true;
                _dwellTimer = WaypointDwellSeconds;
                
                if (Tier() == 0)
                    _recordedPatrol.Add(new PatrolLeg { WaypointIndex = _wpIndex % Waypoints.Length, DwellSeconds = WaypointDwellSeconds });
            }

            CheckEngage();
        }

        
        void UpdatePatrolReplay()
        {
            if (TryPatrolStallRecovery()) return;
            PatrolLeg leg = _recordedPatrol[_replayIndex % _recordedPatrol.Count];
            if (Waypoints == null || leg.WaypointIndex >= Waypoints.Length || Waypoints[leg.WaypointIndex] == null)
            {
                _replayIndex++;
                return;
            }
            Transform wp = Waypoints[leg.WaypointIndex];

            if (_replayDwell > 0f)
            {
                _replayDwell -= Time.deltaTime;
                if (_replayDwell <= 0f) _replayIndex++;
                return;
            }

            SetMoveTarget(wp.position, PatrolSpeed);
            if (HorizontalDistance(transform.position, wp.position) < 0.6f)
                _replayDwell = leg.DwellSeconds;

            CheckEngage();
        }

        void UpdateStalk()
        {
            if (_player == null)
            { EnterState(MonsterState.Patrol); return; }

            float dist = HorizontalDistance(transform.position, _player.position);
            if (CanSeePlayer())
            {
                _lastSeenTimer = 0f;
                if (dist <= ChaseTriggerRange || Tier() >= 4)
                { EnterState(MonsterState.Chase); return; }
                
                if (dist > StalkHoldRange) SetMoveTarget(_player.position, StalkSpeed);
                else HoldPosition();
            }
            else
            {
                _lastSeenTimer += Time.deltaTime;
                if (_lastSeenTimer > LoseSightAfter)
                    EnterState(Tier() >= 1 ? MonsterState.Patrol : MonsterState.Patrol);
            }

            if (TryDeathGlance()) return;
        }

        void UpdateChase()
        {
            if (_player == null)
            { EnterState(MonsterState.Patrol); return; }
            _chaseTime += Time.deltaTime;
            TrackPlayerVelocity();

            bool seen = SeesPlayer;
            if (seen)
            {
                _lastSeenTimer = 0f;
                _memoryKnownPos = _player.position;
                
                
                _predictTimer = Tier() >= 4 ? 2.5f : Tier() >= 3 ? 1.6f : Tier() >= 2 ? 1.0f : 0.6f;
            }
            else
            {
                _lastSeenTimer += Time.deltaTime;
                if (_predictTimer > 0f)
                {
                    _predictTimer -= Time.deltaTime;
                    _memoryKnownPos = _player.position;
                }
                else if (HorizontalDistance(transform.position, _memoryKnownPos) < 1.2f || _lastSeenTimer > LoseSightAfter + 8f)
                {
                    
                    Debug.Log("TEL MONSTER lost player; searching");
                    _investigatePos=_memoryKnownPos;
                    EnterState(MonsterState.Investigate);
                    return;
                }
            }

            float dist=HorizontalDistance(transform.position, _player.position);
            Vector3 target=seen || _predictTimer > 0f ? LeadTarget(dist) : _memoryKnownPos;
            float speed=ChaseSpeed + Mathf.Min(ChaseRampMax, _chaseTime * ChaseRamp) + Tier() * ChaseSpeedPerTier;
            SetMoveTarget(target, speed);
            UnstickIfNeeded(dist);

            
            if (seen && dist <= LungeTriggerRange && _lungeCooldownTimer <= 0f &&
                !NavMesh.Raycast(transform.position, _player.position, out _, NavMesh.AllAreas))
            {
                _lungeCooldownTimer=LungeCooldown;
                EnterState(MonsterState.Lunge);
            }
        }

        void TrackPlayerVelocity()
        {
            if (Time.deltaTime <= 0f) return;
            Vector3 v=(_player.position - _lastPlayerPos) / Time.deltaTime;
            v.y=0f;
            if (v.sqrMagnitude > 100f) v=Vector3.zero; 
            _playerVelocity=Vector3.Lerp(_playerVelocity, v, 1f - Mathf.Exp(-6f * Time.deltaTime));
            _lastPlayerPos=_player.position;
        }

Vector3 LeadTarget(float dist)
        {
            Vector3 toMonster=transform.position - _player.position;
            if (Vector3.Dot(_playerVelocity, toMonster) > 0f) return _player.position;   
            float t=Mathf.Clamp(dist / Mathf.Max(1f, ChaseSpeed), 0f, LeadSeconds);
            Vector3 lead=Vector3.ClampMagnitude(_playerVelocity * t, dist * 0.5f);
            Vector3 ahead=_player.position + lead;
            if (NavMesh.SamplePosition(_player.position, out var from, 1.5f, NavMesh.AllAreas))
            {
                if (NavMesh.Raycast(from.position, ahead, out var hit, NavMesh.AllAreas)) return hit.position;
                return ahead;
            }
            return _player.position;
        }

        void UnstickIfNeeded(float distToPlayer)
        {
            if (!_agentNavigable || _agent == null || !_agent.isOnNavMesh || distToPlayer < 2f || _staggerTimer > 0f) { _stuckTimer=0f; return; }
            if (_agent.velocity.sqrMagnitude > 0.09f) { _stuckTimer=0f; return; }
            _stuckTimer += Time.deltaTime;
            if (_stuckTimer > 1.2f && _stuckTimer - Time.deltaTime <= 1.2f)
            {
                _agent.ResetPath();
                _lastDestination=new Vector3(9999f, 0f, 0f);   
            }
            if (_stuckTimer > 3f)
            {
                
                _agent.ResetPath();
                _repathTimer=0f;
                _lastDestination=new Vector3(float.PositiveInfinity, 0f, 0f);
                _moveTarget=SeesPlayer && _player != null ? _player.position : _memoryKnownPos;
                _stuckTimer=0f;
                Debug.Log("TEL MONSTER replanning stalled chase");
            }
        }

        void UpdateDisguised()
        {
            HoldPosition();
            if (_player == null) return;
            float dist = HorizontalDistance(transform.position,_player.position);
            if (dist <= DisguiseRevealRange || (CanSeePlayer() && dist <= ChaseTriggerRange && Tier() >= 1))
                EnterState(MonsterState.Chase);
        }

        void UpdateScream()
        {
            HoldPosition();
            FacePlayer();
            _screamTimer -= Time.deltaTime;
            if (_screamTimer <= 0f)
            {
                if (_player != null) _memoryKnownPos = _player.position;
                EnterState(Tier() >= 1 ? MonsterState.Chase : MonsterState.Patrol);
            }
        }

        void UpdateLunge()
        {
            if (_player == null) { EnterState(MonsterState.Chase); return; }
            _lungeTimer -= Time.deltaTime;
            SetMoveTarget(_player.position,LungeSpeed);
            if (_lungeTimer <= 0f) EnterState(MonsterState.Chase);
        }

Vector3 _memoryKnownPos;

        int Tier() => _memory != null ? _memory.MemoryTier : 0;

        float _disengageTimer;

        void CheckEngage()
        {
            if (_player == null || !SeesPlayer) return;
            float dist = HorizontalDistance(transform.position,_player.position);
            if (_disengageTimer > 0f && dist > 2f) return;
            if (Tier() < 1)
            {
                
                
                if (dist <= FirstSightChaseRange) { Debug.Log("TEL MONSTER engage (first sight)"); EnterState(MonsterState.Chase); }
                return;
            }
            if (dist <= ChaseTriggerRange) EnterState(MonsterState.Chase);
            else if (dist <= StalkHoldRange) EnterState(MonsterState.Stalk);
        }

        bool TryDeathGlance()
        {
            if (_memory == null || _memory.DeathCount == 0) return false;
            if (_glancing)
            {
                _glanceTimer -= Time.deltaTime;
                RotateTowards(_glancePos);
                if (_glanceTimer <= 0f)
                {
                    _glancing = false;
                    _glancedDeaths = _memory.DeathCount; 
                }
                return true;
            }
            
            
            if (_memory.DeathCount <= _glancedDeaths) return false;
            
            
            float d = HorizontalDistance(transform.position, _memory.LastDeathPosition);
            if (d <= DeathGlanceRange && d > 2.5f)
            {
                _glancing = true;
                _glanceTimer = DeathGlanceSeconds;
                _glancePos = _memory.LastDeathPosition;
                HoldPosition();
                return true;
            }
            return false;
        }

        float _campCooldown;
        float _campHold;

        
        bool TryCampFromPatrol()
        {
            if (_player == null || ObjectiveManager.Instance == null || ObjectiveManager.Instance.Current == null) return false;
            _campCooldown -= Time.deltaTime;
            if (_campCooldown > 0f) return false;
            Vector3 objPos = ObjectiveManager.Instance.NextObjectivePos;
            if (NavMesh.SamplePosition(objPos, out var hit, 3f, NavMesh.AllAreas)) objPos = hit.position;
            if (HorizontalDistance(_player.position, objPos) < CampPlayerMinDist) { _campCooldown = 6f; return false; }
            if (HorizontalDistance(transform.position, objPos) < 1.8f)
            {
                HoldPosition();
                FacePlayer();
                _campHold += Time.deltaTime;
                if (!_meetingVoicePlayed && Audio.AudioDirector.Instance != null)
                {
                    _meetingVoicePlayed = true;
                    Audio.AudioDirector.Instance.PlayVoice("voice_meeting");
                    UI.UIManager.Instance?.ShowSubtitle("\"You keep showing up to my meetings.\"", 3f);
                }
                if (_campHold > 9f) { _campHold=0f; _campCooldown=25f; return false; }
                return true;
            }
            SetMoveTarget(objPos, StalkSpeed);
            return true;
        }

        void TryCampObjective()
        {
            if (_player == null || ObjectiveManager.Instance == null) return;
            Vector3 objPos=ObjectiveManager.Instance.NextObjectivePos;
            if (HorizontalDistance(_player.position, objPos) < CampPlayerMinDist) return;
            if (HorizontalDistance(transform.position, objPos) < 1.5f)
            {
                HoldPosition();
                FacePosition(objPos);
                
                if (!_meetingVoicePlayed && Audio.AudioDirector.Instance != null)
                {
                    _meetingVoicePlayed=true;
                    Audio.AudioDirector.Instance.PlayVoice("voice_meeting");
                }
            }
            else
            {
                SetMoveTarget(objPos,StalkSpeed);
            }
        }

        void TryKill()
        {
            if (CurrentState != MonsterState.Chase && CurrentState != MonsterState.Lunge) return;
            if (_player == null || _rewindLock) return;
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;
            if (DeathManager.Instance == null || !DeathManager.Instance.DeathsEnabled) return;

            float dist = HorizontalDistance(transform.position,_player.position);
            if (dist > KillRange) return;
            
            if (Physics.Linecast(transform.position + Vector3.up * 1.3f,_player.position + Vector3.up * 1.1f,LosMask,QueryTriggerInteraction.Ignore))
                return;
            bool armsLength = dist < 0.85f;
            if (CurrentState == MonsterState.Chase && RequirePlayerLooking && !armsLength && !PlayerLookingAtMonster())
                return;

            DeathManager.Instance.KillFrom("monster", transform.position);
        }

        bool PlayerLookingAtMonster()
        {
            if (_playerCam == null || _player == null) return true;
            Vector3 dir = (transform.position - _playerCam.transform.position).normalized;
            return Vector3.Dot(_playerCam.transform.forward, dir) >= PlayerLookDot;
        }

        bool CanSeePlayer()
        {
            if (_player == null) return false;
            var fpc = FirstPersonController.Instance;
            Vector3 eye = transform.position + Vector3.up * EyeHeight;
            Vector3 head = fpc != null ? fpc.EyePosition : _player.position + Vector3.up * 1.5f;
            float range = SightRange;
            if (Flashlight.Instance != null && Flashlight.Instance.IsOn) range *= TorchSightBonus;
            else if (fpc != null && fpc.IsCrouching) range *= CrouchSightScale;
            Vector3 to = head - eye;
            float dist = to.magnitude;
            if (dist > range) return false;
            Vector3 flat = to; flat.y = 0f;
            if (dist > CloseSenseRange && Vector3.Angle(transform.forward, flat) > FovAngle * 0.5f) return false;
            if (!RequireLineOfSight) return true;
            if (!Physics.Linecast(eye, head, LosMask, QueryTriggerInteraction.Ignore)) return true;
            if (fpc == null || !fpc.IsCrouching)
            {
                Vector3 chest = _player.position + Vector3.up * 1.15f;
                if (!Physics.Linecast(eye, chest, LosMask, QueryTriggerInteraction.Ignore)) return true;
            }
            return false;
        }

float _currentSpeed;

        void SetMoveTarget(Vector3 pos, float speed)
        {
            _moveTarget = pos;
            _currentSpeed = speed;
            _hasMoveTarget = true;
        }

bool TryPatrolStallRecovery()
        {
            if (_dwelling || _replayDwell > 0f || !_hasMoveTarget) { _stallTimer = 0f; return false; }
            if (_agentNavigable && _agent != null && _agent.isOnNavMesh && !_agent.isStopped
                && _agent.velocity.magnitude < 0.15f)
            {
                _stallTimer += Time.deltaTime;
                if (_stallTimer > 5f)
                {
                    _stallTimer = 0f;
                    _hasMoveTarget = false;
                    if (_recordedPatrol.Count > 0 && Tier() >= 1) _replayIndex++;
                    else _wpIndex++;
                    Debug.Log("TEL MONSTER stall-skip (patrol)");
                    return true;
                }
                return false;
            }
            _stallTimer = 0f;
            return false;
        }

        void HoldPosition()
        {
            _hasMoveTarget = false;
            _lastDestination = new Vector3(9999f, 0f, 0f);
            if (_agentNavigable && _agent != null && _agent.isOnNavMesh)
            {
                if (!_agent.isStopped)
                { _agent.isStopped = true; _agent.ResetPath(); }
            }
        }

        void ApplyMovement()
        {
            
            if (Tier() >= 4) UpdateFakeout();

            if (!_hasMoveTarget) return;

            if (_agentNavigable && _agent != null && _agent.isOnNavMesh)
            {
                if (_agent.isStopped) _agent.isStopped = false;
                bool hunting = CurrentState == MonsterState.Chase || CurrentState == MonsterState.Lunge;
                _agent.speed = _currentSpeed;
                _agent.acceleration = hunting ? 26f : 10f;
                _agent.angularSpeed = hunting ? 720f : 280f;
                _agent.autoBraking = !hunting;   
                
                _repathTimer -= Time.deltaTime;
                bool targetJumped = (_moveTarget - _lastDestination).sqrMagnitude > 9f;
                if ((_repathTimer <= 0f && ((_moveTarget - _lastDestination).sqrMagnitude > 0.04f || !_agent.hasPath || _agent.isPathStale)) || targetJumped)
                {
                    var route = new NavMeshPath();
                    bool accepted = TryPlanRoute(_moveTarget, route);
                    if (!accepted && hunting && SeesPlayer && _player != null)
                        accepted = TryPlanRoute(_player.position, route);
                    if (accepted)
                    {
                        _agent.SetPath(route);
                        _lastDestination = _moveTarget;
                    }
                    else
                    {
                        
                        _agent.ResetPath();
                    }
                    _repathTimer = hunting ? 0.25f : 1f;
                }
            }
            else
            {
                
                if (!_fallbackWarned)
                {
                    _fallbackWarned = true;
                    Debug.LogWarning("TEL MONSTER waiting for a valid NavMesh");
                }
                if (NavMesh.SamplePosition(transform.position, out var recovery, 0.5f, NavMesh.AllAreas) &&
                    !Physics.Linecast(transform.position + Vector3.up, recovery.position + Vector3.up, LosMask))
                {
                    if (_agent != null && _agent.enabled) _agent.Warp(recovery.position);
                }
            }
        }

        bool TryPlanRoute(Vector3 target, NavMeshPath route)
        {
            if (!NavMesh.SamplePosition(target, out var floor, 1.5f, NavMesh.AllAreas)) return false;
            return _agent.CalculatePath(floor.position, route) && route.status == NavMeshPathStatus.PathComplete;
        }

        void UpdateFakeout()
        {
            _fakeoutTimer -= Time.deltaTime;
            if (_fakeoutTimer > 0f || _player == null) return;
            _fakeoutTimer=Mathf.Max(1f, TeleportFakeoutMinInterval);
            if (PlayerLookingAtMonster()) return; 
            float angle=UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float radius=UnityEngine.Random.Range(TeleportFakeoutMinDist, Mathf.Max(TeleportFakeoutMinDist + 0.1f, TeleportFakeoutMaxDist));
            Vector3 pos=_player.position + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            pos.y=transform.position.y;
            Teleport(pos);
        }

        void FacePlayer()
        {
            if (_player != null) RotateTowards(_player.position);
        }

        void FacePosition(Vector3 pos) => RotateTowards(pos);

        void RotateTowards(Vector3 pos)
        {
            Vector3 dir=pos - transform.position;
            dir.y=0f;
            if (dir.sqrMagnitude < 0.001f) return;
            Quaternion look=Quaternion.LookRotation(dir.normalized);
            transform.rotation=Quaternion.RotateTowards(transform.rotation, look, TurnSpeed * Time.deltaTime);
        }

        void UpdateNavigable()
        {
            _agentNavigable=_agent != null && _agent.isOnNavMesh;
        }

public void SetRewindLock(bool locked)
        {
            _rewindLock=locked;
            if (locked) HoldPosition();
        }

        void OnRewindLockEvent(bool locked) => SetRewindLock(locked);

        void OnTimelineRewound()
        {
            
            
            _glancing=false;
            _lastSeenTimer=LoseSightAfter + 1f;
            _disengageTimer=PostRewindCalm;
            EnterState(MonsterState.Patrol);
            if (_player != null && HorizontalDistance(transform.position, _player.position) < 9f)
                RetreatOutOfSight();
        }

public void LoseTrack(float seconds)
        {
            _disengageTimer=Mathf.Max(_disengageTimer, seconds);
            if (CurrentState == MonsterState.Chase || CurrentState == MonsterState.Lunge || CurrentState == MonsterState.Stalk)
            {
                _investigatePos=_memoryKnownPos;
                EnterState(MonsterState.Investigate);
                Debug.Log("TEL MONSTER lost track (darkness)");
            }
        }

        
        void RetreatOutOfSight()
        {
            Vector3 best = transform.position;
            float bestScore = float.NegativeInfinity;
            foreach (var w in Waypoints)
            {
                if (w == null) continue;
                float d = HorizontalDistance(w.position,_player.position);
                if (d < 10f) continue;
                bool hidden = Physics.Linecast(w.position + Vector3.up * EyeHeight,_player.position + Vector3.up * 1.5f,LosMask,QueryTriggerInteraction.Ignore);
                float score = (hidden ? 100f : 0f) - Mathf.Abs(d - 14f);
                if (score > bestScore) { bestScore = score; best = w.position; }
            }
            Teleport(best);
            Debug.Log("TEL MONSTER retreat after rewind");
        }

        void TrySubscribeRewindLock()
        {
            try
            {
                _rewindLockEvent = typeof(GameEvents).GetEvent("RewindLock",
                    BindingFlags.Public | BindingFlags.Static);
                if (_rewindLockEvent == null) return;
                _rewindLockHandler = Delegate.CreateDelegate(_rewindLockEvent.EventHandlerType,
                    this, nameof(OnRewindLockEvent), false);
                if (_rewindLockHandler != null) _rewindLockEvent.AddEventHandler(null, _rewindLockHandler);
            }
            catch (Exception e)
            {
                Debug.LogWarning("TEL MONSTER optional GameEvents.RewindLock not usable: " + e.Message);
            }
        }

public void OnScreamComplete()
        {
            EnterState(MonsterState.Chase);
        }

public void Teleport(Vector3 pos)
        {
            Vector3 target=pos;
            if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var hit, 2.5f, UnityEngine.AI.NavMesh.AllAreas))
                target=hit.position;
            if (_agent != null && _agent.isOnNavMesh) _agent.Warp(target);
            else transform.position=target;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "TEL MONSTER teleport {0:0.#},{1:0.#}",target.x,target.z));
        }

public void ForceAggro()
        {
            CachePlayer();
            EnterState(MonsterState.Chase);
            _playerVelocity = Vector3.zero;
            _repathTimer = 0f;
            _lastDestination = new Vector3(float.PositiveInfinity,0f,0f);
            if (_player != null)
            {
                _memoryKnownPos = _lastPlayerPos = _player.position;
                _lastSeenTimer = 0f;
                _predictTimer = LoseSightAfter;
                SetMoveTarget(_player.position,ChaseSpeed);
            }
            Debug.Log("TEL MONSTER aggro state=Chase");
        }

        
        public string QaTel()
        {
            float speed = _agentNavigable && _agent != null ? _agent.speed : PatrolSpeed;
            Vector3 t = _hasMoveTarget ? _moveTarget : transform.position;
            return string.Format(CultureInfo.InvariantCulture,
                "state={0} tier={1} speed={2:0.0} target={3:0.#},{4:0.#}",
                CurrentState, Tier(), speed, t.x, t.z);
        }

void CachePlayer()
        {
            if (_player != null) return;
            GameObject go = null;
            try { go = GameObject.FindGameObjectWithTag("Player"); } catch {  }
            if (go == null) go=GameObject.Find("Player");
            if (go != null) _player = go.transform;
        }

        void CachePlayerCamera()
        {
            if (_playerCam != null) return;
            _playerCam = Camera.main;
        }

        static float HorizontalDistance(Vector3 a,Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a,b);
        }
    }
}
