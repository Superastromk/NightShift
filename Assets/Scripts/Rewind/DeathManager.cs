using UnityEngine;
using UnityEngine.InputSystem;
using NightShift.Core;
using NightShift.Monster;

namespace NightShift.Rewind
{

public class DeathManager : MonoBehaviour
    {
        static DeathManager _instance;

        
        public static DeathManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<DeathManager>();
                    if (_instance == null) Debug.LogWarning("TEL WARN DeathManager.Instance: none in scene");
                    else Debug.LogWarning("TEL WARN DeathManager.Instance used FindAnyObjectByType fallback");
                }
                return _instance;
            }
        }

        
        public bool DeathsEnabled { get; set; } = true;

        int _killFrame = -1;
        Transform _player;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("TEL WARN duplicate DeathManager; keeping first");
                return;
            }
            _instance=this;
        }

        void Start()
        {
            var go=GameObject.FindGameObjectWithTag("Player");
            if (go != null) _player = go.transform;
        }

public void Kill(string cause) => KillFrom(cause,_player != null ? _player.position + _player.forward : Vector3.zero);

        
        public void KillFrom(string cause,Vector3 source)
        {
            if (!DeathsEnabled) return;
            if (_killFrame == Time.frameCount) return; 
            _killFrame = Time.frameCount;
            DeathsEnabled = false; 
            GameEvents.RaisePlayerDamaged(source,100f,cause);

            Vector3 pos = _player != null ? _player.position : Vector3.zero;
            GameManager.Instance?.RegisterDeath(pos,cause);

            var rc = RewindController.Instance;
            if (rc != null) rc.BeginRewind(pos,cause);
            else Debug.LogWarning("TEL WARN DeathManager.Kill: no RewindController in scene");
        }

        
        public string QaTel() => $"enabled={(DeathsEnabled ? 1 : 0)}";

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            var gm = GameManager.Instance;
            bool playing = gm == null || gm.State == GameState.Playing;

            
            if (playing && DeathsEnabled && kb.f9Key.wasPressedThisFrame)
                Kill("qa");

            
            if (playing && kb.f8Key.wasPressedThisFrame)
            {
                var monster=MonsterAI.Instance;
                if (monster != null && _player != null)
                {
                    Vector3 pos=_player.position + _player.forward * 3f;
                    if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var hit, 4f, UnityEngine.AI.NavMesh.AllAreas))
                        pos=hit.position;
                    monster.Teleport(pos);
                    monster.ForceAggro();
                    Debug.Log("TEL QA F8 monster aggro at " + pos.x.ToString("0.0") + "," + pos.z.ToString("0.0"));
                }
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            
            if (playing && kb.f10Key.wasPressedThisFrame && _player != null)
            {
                var meter=FindAnyObjectByType<ComposureMeter>();
                Vector3 source=_player.position + Quaternion.Euler(0f, -90f + _qaDamageSide * 90f, 0f) * _player.forward * 3f;
                _qaDamageSide=(_qaDamageSide + 1) % 4;
                if (meter != null) meter.Drain(12f, "qa_direction",source);
                Debug.Log("TEL QA F10 directional damage");
            }
#endif
        }
        int _qaDamageSide;
    }
}
