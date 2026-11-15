using UnityEngine;
using NightShift.Core;
using NightShift.Monster;
using NightShift.Player;

namespace NightShift.World
{

public class Door : MonoBehaviour, IInteractable
    {
        public Transform Hinge;
        [Tooltip("World-space centre of the doorway (for swing direction + monster checks).")]
        public Vector3 DoorwayCenter;
        public float OpenAngle = 100f;
        public float SwingSpeed = 240f;
        public bool StartsOpen;
        public float MonsterForceSeconds = 1.6f;
        public float MonsterDetectRadius = 1.5f;

        public bool IsOpen { get; private set; }
        public string Prompt => IsOpen ? "[E]  Close door" : "[E]  Open door";

        float _angle, _target;
        int _lastInteractFrame = -10;
        float _forceTimer = -1f;
        MonsterAI _monster;
        bool _slam;

        void Awake()
        {
            if (Hinge == null) Hinge = transform;
            if (StartsOpen)
            { IsOpen = true; _angle = _target = OpenAngle; Apply(); }
        }

        void OnEnable()
        { GameEvents.TimelineRewound += SyncFromHinge; }
        void OnDisable()
        { GameEvents.TimelineRewound -= SyncFromHinge; }

        public void Interact(PlayerInteractor by)
        {
            
            bool edge = Time.frameCount - _lastInteractFrame > 1;
            _lastInteractFrame = Time.frameCount;
            if (!edge) return;
            var fpc = FirstPersonController.Instance;
            bool fast = fpc != null && fpc.IsSprinting;
            Toggle(by != null ? by.transform.position : transform.position, fast);
        }

        public void Toggle(Vector3 actorPos, bool slam)
        {
            if (IsOpen) Close(slam); else Open(actorPos, slam);
        }

        public void Open(Vector3 actorPos, bool slam)
        {
            if (IsOpen) return;
            IsOpen = true;
            
            Vector3 n = Hinge.parent != null ? Hinge.parent.forward : transform.forward;
            float side = Vector3.Dot(actorPos - DoorwayCenter, n);
            _target = side >= 0f ? OpenAngle : -OpenAngle; 
            _slam = slam;
            GameEvents.RaiseStingerRequested(slam ? "door_slam" : "door_open",slam ? 0.9f : 0.55f);
            NoiseSystem.Emit(DoorwayCenter,slam ? 11f : 4f, "door");
        }

        public void Close(bool slam)
        {
            if (!IsOpen) return;
            IsOpen = false;
            _target = 0f;
            _slam = slam;
            GameEvents.RaiseStingerRequested(slam ? "door_slam" : "door_close", slam ? 0.9f : 0.5f);
            NoiseSystem.Emit(DoorwayCenter, slam ? 11f : 3.5f, "door");
        }

        void Update()
        {
            if (!Mathf.Approximately(_angle,_target))
            {
                _angle = Mathf.MoveTowards(_angle,_target,SwingSpeed * (_slam ? 1.8f : 1f) * Time.deltaTime);
                Apply();
            }

            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;
            if (IsOpen) { _forceTimer = -1f; return; }

            
            if (_monster == null) _monster = MonsterAI.Instance;
            if (_monster == null || !_monster.isActiveAndEnabled || _monster.IsRewindLocked) return;
            Vector3 d = _monster.transform.position - DoorwayCenter; d.y = 0f;
            if (d.sqrMagnitude < MonsterDetectRadius * MonsterDetectRadius && _monster.WantsToMove)
            {
                if (_forceTimer < 0f)
                {
                    _forceTimer = _monster.CurrentState == MonsterState.Chase ? MonsterForceSeconds : MonsterForceSeconds * 0.6f;
                    GameEvents.RaiseStingerRequested("door_rattle", 0.8f);
                }
                _monster.Stagger(0.15f);
                _forceTimer -= Time.deltaTime;
                if (_forceTimer <= 0f)
                {
                    _forceTimer = -1f;
                    Open(_monster.transform.position, true);
                    Debug.Log("TEL DOOR forced by monster");
                }
            }
            else _forceTimer = -1f;
        }

        void Apply()
        { Hinge.localRotation = Quaternion.Euler(0f, _angle, 0f); }

        
        void SyncFromHinge()
        {
            float y = Hinge.localEulerAngles.y;
            if (y > 180f) y -= 360f;
            _angle = y;
            IsOpen = Mathf.Abs(y) > 8f;
            _target = IsOpen ? Mathf.Sign(y) * OpenAngle : 0f;
            _forceTimer = -1f;
        }
    }
}
