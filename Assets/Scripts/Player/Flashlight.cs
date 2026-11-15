using UnityEngine;
using UnityEngine.InputSystem;
using NightShift.Core;

namespace NightShift.Player
{

public class Flashlight : MonoBehaviour
    {
        public static Flashlight Instance { get; private set; }

        public Light Beam;
        public float BatterySeconds = 75f;
        public float RechargeSeconds = 40f;
        public float FlickerRange = 9f;
        public float FollowLag = 14f;

        public bool IsOn { get; private set; }
        public float Battery01 { get; private set; } = 1f;
        
        public bool EverUsed { get; private set; }

        float _baseIntensity;
        Transform _monster;
        Quaternion _smoothed;

        void Awake()
        {
            Instance = this;
            if (Beam == null) Beam = GetComponentInChildren<Light>(true);
            if (Beam != null) { _baseIntensity = Beam.intensity; Beam.enabled = false; }
            _smoothed = transform.rotation;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Set(bool on)
        {
            if (on && Battery01 <= 0.02f) on = false;
            if (IsOn == on) return;
            IsOn = on;
            if (on) EverUsed = true;
            GameEvents.RaiseStingerRequested("click", 0.5f);
            Debug.Log("TEL FLASHLIGHT " + (on ? "on" : "off"));
        }

        void LateUpdate()
        {
            var gm = GameManager.Instance;
            bool playing = gm != null && gm.State == GameState.Playing;
            var fpc = FirstPersonController.Instance;
            if (playing && (fpc == null || !fpc.InputLocked) && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
                Set(!IsOn);

            float dt = Time.deltaTime;
            if (IsOn)
            {
                Battery01 = Mathf.Max(0f,Battery01 - dt / BatterySeconds);
                if (Battery01 <= 0f) { Set(false); UI.UIManager.Instance?.ShowSubtitle("The flashlight battery is dead. Leave it switched off to recharge.", 3f, true); }
            }
            else Battery01 = Mathf.Min(1f, Battery01 + dt / RechargeSeconds);

            if (Beam == null) return;
            
            var parent = transform.parent;
            if (parent != null)
            {
                _smoothed = Quaternion.Slerp(_smoothed, parent.rotation, 1f - Mathf.Exp(-FollowLag * Time.unscaledDeltaTime));
                transform.rotation = _smoothed;
            }

            float flicker = 1f;
            if (IsOn)
            {
                if (_monster == null) { var m = FindFirstObjectByType<Monster.MonsterAI>(); if (m != null) _monster = m.transform; }
                if (_monster != null)
                {
                    float d = Vector3.Distance(_monster.position, transform.position);
                    if (d < FlickerRange)
                    {
                        float k = 1f - d / FlickerRange;
                        float n = Mathf.PerlinNoise(Time.time * 18f, 3.3f);
                        flicker = n < k * 0.75f ? 0.08f : 1f;
                    }
                }
                if (Battery01 < 0.15f) flicker *= 0.55f + 0.45f * Mathf.PerlinNoise(Time.time * 9f, 0.7f);
            }
            Beam.enabled = IsOn;
            Beam.intensity = _baseIntensity * flicker;
        }

        public string QaTel() => $"torch={(IsOn ? 1 : 0)} batt={Battery01:F2}";
    }
}
