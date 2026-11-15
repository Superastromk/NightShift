using UnityEngine;
using NightShift.Core;

namespace NightShift.Scares
{

public abstract class JumpscareTrigger : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("Stable scare id, e.g. \"printer\". Logged as TEL SCARE <id>.")]
        public string ScareId = "scare";

        [Header("Firing")]
        [Tooltip("If true, the scare fires only once per scene load.")]
        public bool FireOnce = true;
        [Tooltip("Master enable switch for this scare.")]
        public bool Enabled = true;
        [Tooltip("Composure drained from the player when the scare lands.")]
        public float ComposureDamage=15f;
        [Tooltip("Stinger clip id requested when the scare lands.")]
        public string StingerId = "sting_scare_soft";
        [Tooltip("Volume for the requested stinger.")]
        public float StingerVolume = 1f;
        [Tooltip("If set, the scare stays dormant until this objective id has been completed.")]
        public string ArmAfterObjectiveId="";

        
        protected bool HasFired => _fired;
        bool _fired;

        
        protected abstract void Activate();

protected virtual bool CanFire() => Enabled && (!_fired || !FireOnce) && IsArmed;

        
        protected bool IsArmed
        {
            get
            {
                if (string.IsNullOrEmpty(ArmAfterObjectiveId)) return true;
                var om = Objectives.ObjectiveManager.Instance;
                return om == null || om.IsDone(ArmAfterObjectiveId);
            }
        }

protected void FireScare()
        {
            _fired = true;
            GameEvents.RaiseScareFired(ScareId);
            Debug.Log("TEL SCARE " + ScareId);
            ComposureMeter meter = FindAnyObjectByType<ComposureMeter>();
            if (meter != null) meter.Drain(ComposureDamage, ScareId, transform.position);
            else Debug.LogWarning("TEL SCARE WARN no ComposureMeter found in scene; skipping drain for " + ScareId);
            GameEvents.RaiseStingerRequested(StingerId, StingerVolume);
        }

public void Trigger()
        {
            if (!CanFire()) return;
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;
            Activate();
        }

        
        protected static bool IsPlaying =>
            GameManager.Instance == null || GameManager.Instance.State == GameState.Playing;

        
        protected Transform Player
        {
            get
            {
                if (_player != null) return _player;
                GameObject go = null;
                try { go = GameObject.FindGameObjectWithTag("Player"); } catch {  }
                if (go == null) go=GameObject.Find("Player");
                if (go != null) _player = go.transform;
                return _player;
            }
        }
        Transform _player;

        
        protected Camera PlayerCam
        {
            get
            {
                if (_cam == null || !_cam.enabled) _cam = Camera.main;
                return _cam;
            }
        }
        Camera _cam;

        
        protected float DistToPlayer()
        {
            Transform p = Player;
            if (p == null) return float.PositiveInfinity;
            Vector3 a = transform.position; a.y = 0f;
            Vector3 b = p.position; b.y = 0f;
            return Vector3.Distance(a,b);
        }

        
        public virtual string QaTel() =>
            "scare=" + ScareId + " fired=" + (_fired ? 1 : 0) + " enabled=" + (Enabled ? 1 : 0);
    }
}
