using UnityEngine;
using NightShift.Core;

namespace NightShift.World
{

[RequireComponent(typeof(Rigidbody))]
    public class PhysicsProp : MonoBehaviour
    {
        public string ImpactClip = "thud";
        public float MinImpactSpeed=1.3f;
        public float NoisePerSpeed=2.6f;
        public float MaxNoise=11f;

        Rigidbody _rb;
        float _lastImpact=-9f;
        float _spawnTime;

        void Awake()
        {
            _rb=GetComponent<Rigidbody>();
            _rb.sleepThreshold=0.08f;
            _rb.maxDepenetrationVelocity=2.5f;
            _rb.interpolation=RigidbodyInterpolation.None;
            _spawnTime=Time.time;
        }

        void OnCollisionEnter(Collision c)
        {
            if (Time.time - _spawnTime < 1.5f) return;          
            if (Time.time - _lastImpact < 0.35f) return;
            var gm=GameManager.Instance;
            if (gm != null && gm.State != GameState.Playing) return;
            float v=c.relativeVelocity.magnitude;
            if (v < MinImpactSpeed) return;
            _lastImpact=Time.time;
            Vector3 p=c.contactCount > 0 ? c.GetContact(0).point : transform.position;
            Audio.AudioDirector.Instance?.PlayAt(ImpactClip, p, Mathf.Clamp01(v / 5f));
            NoiseSystem.Emit(p, Mathf.Min(MaxNoise, v * NoisePerSpeed), "prop");
        }
    }
}
