using UnityEngine;

namespace NightShift.Core
{

public class ComposureMeter : MonoBehaviour
    {
        [Header("Config")]
        [Tooltip("Passive regen per second once safe.")]
        public float RegenRate = 4f;

        [Tooltip("Seconds without a drain before regen kicks in.")]
        public float RegenDelaySeconds = 5f;

        
        public float Value { get; private set; } = 100f;

        float _lastDrainTime = -999f;
        bool _zeroLogged;

        void OnEnable()
        {
            GameEvents.ComposureDrained += OnComposureDrained;
        }

        void OnDisable()
        {
            GameEvents.ComposureDrained -= OnComposureDrained;
        }

        void OnComposureDrained(float amount, string reason) => Drain(amount, reason);

        void Update()
        {
            if (Value <= 0f && !_zeroLogged)
            {
                _zeroLogged = true;
                Debug.Log("TEL COMP zero");
            }
            if (Value > 10f) _zeroLogged=false;

            if (Time.time - _lastDrainTime >= RegenDelaySeconds && Value < 100f)
                Regen(RegenRate * Time.deltaTime);
        }

public void Drain(float amount, string reason)
        {
            if (amount <= 0f) return;
            _lastDrainTime=Time.time;
            Value=Mathf.Clamp(Value - amount, 0f, 100f);
            Debug.Log($"TEL COMP drain={amount:F1} reason={reason} value={Value:F1}");
            UI.UIManager.Instance?.SetComposure(Value / 100f);
        }

        
        public void Drain(float amount,string reason,Vector3 source)
        {
            Drain(amount,reason);
            if (amount > 0f) GameEvents.RaisePlayerDamaged(source,amount,reason);
        }

        
        public void Regen(float amount)
        {
            Value = Mathf.Clamp(Value + amount,0f,100f);
            UI.UIManager.Instance?.SetComposure(Value / 100f);
        }

        
        public string QaTel() => $"comp={Value:F0}";
    }
}
