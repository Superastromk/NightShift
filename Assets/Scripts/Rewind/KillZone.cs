using UnityEngine;

namespace NightShift.Rewind
{

[RequireComponent(typeof(Collider))]
    public class KillZone : MonoBehaviour
    {
        [Header("Config")]
        public string Cause = "unknown";

        [Tooltip("Disable this collider forever after the first kill.")]
        public bool OneShot = false;

        Collider _col;

        void Awake()
        {
            _col = GetComponent<Collider>();
            _col.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (other == null || !other.CompareTag("Player")) return;
            var dm=DeathManager.Instance;
            if (dm == null || !dm.DeathsEnabled) return;

            dm.KillFrom(Cause, transform.position);

            if (OneShot && _col != null)
            {
                _col.enabled=false;
                Debug.Log("TEL KILLZONE off");
            }
        }

        
        public void ReArm()
        {
            if (_col != null && !_col.enabled)
            {
                _col.enabled = true;
                Debug.Log("TEL KILLZONE on");
            }
        }
    }
}
