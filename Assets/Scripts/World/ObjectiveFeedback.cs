using UnityEngine;
using NightShift.Objectives;

namespace NightShift.World
{

public class ObjectiveFeedback : MonoBehaviour
    {
        public Objective Task;
        [Tooltip("Optional part that moves when the task completes (a lever, a switch).")]
        public Transform Moving;
        public Vector3 DoneLocalEuler;
        public float MoveSeconds = 0.25f;
        [Tooltip("Optional indicator whose material swaps on completion.")]
        public Renderer Indicator;
        public Material DoneMaterial;
        [Tooltip("Optional light that switches on when the task completes.")]
        public Light DoneLight;

        bool _applied;
        float _t;
        Quaternion _from, _to;

        void Start()
        {
            if (Moving != null) { _from=Moving.localRotation; _to=Quaternion.Euler(DoneLocalEuler); }
        }

        void Update()
        {
            if (Task == null) return;
            if (!_applied && Task.IsDone)
            {
                _applied=true;
                if (Indicator != null && DoneMaterial != null) Indicator.sharedMaterial=DoneMaterial;
                if (DoneLight != null) DoneLight.enabled=true;
            }
            if (_applied && Moving != null && _t < 1f)
            {
                _t=Mathf.Min(1f, _t + Time.deltaTime / Mathf.Max(0.01f, MoveSeconds));
                Moving.localRotation=Quaternion.Slerp(_from, _to, _t * _t);
            }
        }
    }
}
