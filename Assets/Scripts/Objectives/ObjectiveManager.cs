using UnityEngine;
using NightShift.Core;

namespace NightShift.Objectives
{

public class ObjectiveManager : MonoBehaviour
    {
        static ObjectiveManager _instance;

        public static ObjectiveManager Instance
        {
            get
            {
                if (_instance == null) _instance = FindFirstObjectByType<ObjectiveManager>();
                return _instance;
            }
        }

        [Header("Scene wiring")]
        [Tooltip("Ordered office tasks.")]
        public Objective[] Sequence = new Objective[0];

        int _completedCount;
        int _currentIndex;
        string _override;

        public System.Collections.Generic.IReadOnlyList<Objective> SequenceList => Sequence;

        
        public Objective Current =>
            Sequence != null && _currentIndex >= 0 && _currentIndex < Sequence.Length
                ? Sequence[_currentIndex]
                : null;

        
        public Vector3 NextObjectivePos
        {
            get { var c = Current; return c != null ? c.MarkerPosition : Vector3.zero; }
        }

        
        public bool TryGetMarker(out Vector3 pos)
        {
            if (MarkerOverride.HasValue) { pos = MarkerOverride.Value; return true; }
            var c = Current;
            if (c != null) { pos = c.MarkerPosition; return true; }
            pos = Vector3.zero;
            return false;
        }

        
        public Vector3? MarkerOverride { get; set; }

        
        public int EscalationLevel => _completedCount;

        
        public bool IsDone(string id)
        {
            if (Sequence == null) return false;
            for (int i = 0; i < Sequence.Length; i++)
                if (Sequence[i] != null && Sequence[i].Id == id) return Sequence[i].IsDone;
            return false;
        }

        void Awake()
        {
            if (_instance != null && _instance != this) { Debug.LogWarning("TEL WARN duplicate ObjectiveManager"); return; }
            _instance = this;
        }

        void OnDestroy() { if (_instance == this) _instance = null; }

        void Start()
        {
            AnnounceCurrent();
        }

        
        public void SetOverrideText(string text)
        {
            _override = text;
            AnnounceCurrent();
        }

        void AnnounceCurrent()
        {
            var c = Current;
            string text = !string.IsNullOrEmpty(_override) ? _override : (c != null ? $"{_currentIndex + 1}/{Sequence.Length}  {c.Title}" : "SHIFT COMPLETE");
            UI.UIManager.Instance?.SetObjective(text);
            GameEvents.RaiseObjectiveChanged(text);
        }

        
        public void OnObjectiveCompleted(Objective obj)
        {
            if (obj == null || !ReferenceEquals(obj, Current)) return;
            _completedCount=Mathf.Min(_completedCount + 1, Sequence != null ? Sequence.Length : 0);
            if (ReferenceEquals(obj, Current)) _currentIndex++;
            AnnounceCurrent();
        }

        
        public string QaTel()
        {
            int total=Sequence != null ? Sequence.Length : 0;
            var c=Current;
            return $"obj={_completedCount}/{total} '{(c != null ? c.Id : "none")}'";
        }
    }
}
