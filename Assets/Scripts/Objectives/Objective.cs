using UnityEngine;
using NightShift.Core;
using NightShift.Player;

namespace NightShift.Objectives
{

public class Objective : MonoBehaviour, IInteractable, IHoldProgress
    {
        [Header("Identity")]
        public string Id;
        public string Title;
        [Tooltip("Prompt text shown when aimed at, e.g. 'Hold [E]  Print report'.")]
        public string PromptText;

        [Header("Config")]
        [Tooltip("Seconds of holding E required to complete.")]
        public float InteractTime=1.2f;
        [Tooltip("How fast unfinished progress drains once you stop holding (fraction of InteractTime per second).")]
        public float DrainRate = 0.6f;
        [Tooltip("Optional line shown in the subtitle band when the task completes.")]
        public string CompleteLine;
        [Tooltip("Optional sound id played on completion.")]
        public string CompleteClip = "task_done";
        [Tooltip("Noise radius emitted while working on it (0 = silent). The monster hears it.")]
        public float WorkNoise = 0f;
        [Tooltip("Where the HUD marker points (defaults to this transform).")]
        public Transform MarkerAnchor;

        public bool IsDone { get; private set; }
        public float Progress01 => InteractTime > 0.01f ? Mathf.Clamp01(_holdTimer / InteractTime) : 0f;
        public Vector3 MarkerPosition => MarkerAnchor != null ? MarkerAnchor.position : transform.position;

        float _holdTimer;
        bool _completing;
        float _lastHoldTime;
        float _noiseTimer;

        bool IsCurrent
        {
            get
            {
                var mgr = ObjectiveManager.Instance;
                return mgr == null || ReferenceEquals(mgr.Current, this);
            }
        }

        public string Prompt => IsDone || !IsCurrent ? null : PromptText;

        public void Interact(PlayerInteractor by)
        {
            if (IsDone || !IsCurrent) return;
            _completing = true;
            _lastHoldTime = Time.time;
            _holdTimer += Time.deltaTime;
            if (WorkNoise > 0f)
            {
                _noiseTimer -= Time.deltaTime;
                if (_noiseTimer <= 0f) { _noiseTimer = 0.6f; NoiseSystem.Emit(transform.position, WorkNoise, "task"); }
            }
            if (_holdTimer >= InteractTime) Complete();
        }

        void Update()
        {
            if (IsDone || _holdTimer <= 0f) return;
            if (Time.time - _lastHoldTime > 0.15f)
            {
                
                
                _holdTimer = Mathf.Max(0f, _holdTimer - Time.deltaTime * InteractTime * DrainRate);
                if (_holdTimer <= 0f) _completing = false;
            }
        }

        void OnEnable()
        { Core.GameEvents.TimelineRewound += ResetHold; }
        void OnDisable()
        { Core.GameEvents.TimelineRewound -= ResetHold; }

        
        void ResetHold()
        {
            if (IsDone) return;
            _holdTimer = 0f;
            _completing = false;
        }

        public void Complete()
        {
            if (IsDone) return;
            IsDone = true;
            _completing = false;
            int index = 0;
            var mgr = ObjectiveManager.Instance;
            if (mgr != null)
                for (int i = 0; i < mgr.Sequence.Length; i++)
                    if (ReferenceEquals(mgr.Sequence[i], this))
                    { index = i; break; }
            Debug.Log($"TEL OBJECTIVE {index} '{Title}' done");
            if (!string.IsNullOrEmpty(CompleteClip)) GameEvents.RaiseStingerRequested(CompleteClip, 0.6f);
            if (!string.IsNullOrEmpty(CompleteLine)) UI.UIManager.Instance?.ShowSubtitle(CompleteLine, 3.5f, true);
            mgr?.OnObjectiveCompleted(this);
        }
    }
}
