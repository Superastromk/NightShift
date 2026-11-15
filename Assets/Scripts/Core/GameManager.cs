using UnityEngine;

namespace NightShift.Core
{

public class GameManager : MonoBehaviour
    {
        static GameManager _instance;

        
        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<GameManager>();
                    if (_instance == null) Debug.LogWarning("TEL WARN GameManager.Instance: none in scene");
                    else Debug.LogWarning("TEL WARN GameManager.Instance used FindAnyObjectByType fallback");
                }
                return _instance;
            }
        }

        
        public GameState State { get; private set; }=GameState.Title;

        
        public int TimelineIndex { get; private set; }

        
        public int DeathCount { get; private set; }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("TEL WARN duplicate GameManager; keeping first");
                return;
            }
            _instance = this;
        }

public void SetState(GameState s)
        {
            if (State == s) return;
            GameState old = State;
            State = s;
            Debug.Log($"TEL STATE {old}->{s}");
            GameEvents.RaiseStateChanged(s);
        }

public void RegisterDeath(Vector3 deathPos, string cause)
        {
            DeathCount++;
            Debug.Log($"TEL DEATH cause={cause} pos={deathPos.x:F1},{deathPos.z:F1}");
            GameEvents.RaiseDeathRegistered(deathPos, cause);
        }

        
        public void IncrementTimeline()
        {
            TimelineIndex++;
        }

public void ResetRun()
        {
            DeathCount = 0;
            TimelineIndex = 0;
            var memory = FindAnyObjectByType<Monster.MonsterMemory>();
            if (memory != null) memory.ResetMemory();
        }

        
        public string QaTel() => $"state={State} deaths={DeathCount} timeline={TimelineIndex}";
    }
}
