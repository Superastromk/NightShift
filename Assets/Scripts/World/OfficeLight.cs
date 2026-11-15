using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NightShift.Core;

namespace NightShift.World
{

public class OfficeLight : MonoBehaviour
    {
        public static readonly List<OfficeLight> All=new List<OfficeLight>();
        
        public static float CullDistance=30f;
        
        public static bool AccentsEnabled=true;
        
        public static bool FillsEnabled=true;

        public Light Lamp;
        [Tooltip("Optional dim fill light that lifts the ceiling around the fixture.")]
        public Light Fill;
        public Renderer Panel;
        public Material OnMat,OffMat,EmergencyMat;
        [Tooltip("Named group: floor, corridor, break, lobby, storage, meeting, accent.")]
        public string Group = "floor";
        [Tooltip("Stays lit during a power cut (exit signs, monitors on UPS).")]
        public bool OnBackupPower;
        [Tooltip("Small decorative light; skipped on Low quality.")]
        public bool Accent;
        [Tooltip("Randomly stutters like a dying tube.")]
        public bool Flickers;
        public float FlickerEvery = 6f;

        public bool Powered { get; private set; } = true;
        public bool Lit => Powered && !_blink;

        float _baseIntensity;
        Color _baseColor;
        bool _emergency;
        bool _blink;
        float _nextFlicker;
        float _cullTimer;
        bool _inRange = true;
        Coroutine _stutter;
        static Transform _cam;

        void Awake()
        {
            if (Lamp != null) { _baseIntensity = Lamp.intensity; _baseColor = Lamp.color; }
            _nextFlicker = Time.time + Random.Range(1f, FlickerEvery);
            _cullTimer = Random.value * 0.3f;
        }

        void OnEnable() { All.Add(this); Refresh(); }
        void OnDisable() { All.Remove(this); }

        void Update()
        {
            if (Flickers && Powered && Time.time >= _nextFlicker && _stutter == null)
            {
                _nextFlicker = Time.time + Random.Range(FlickerEvery * 0.4f, FlickerEvery * 1.6f);
                _stutter = StartCoroutine(StutterRoutine(Random.Range(0.25f, 0.9f)));
            }

            _cullTimer -= Time.deltaTime;
            if (_cullTimer <= 0f)
            {
                _cullTimer = 0.3f;
                if (_cam == null && Camera.main != null) _cam = Camera.main.transform;
                bool inRange = _cam == null || (_cam.position - transform.position).sqrMagnitude < CullDistance * CullDistance;
                if (inRange != _inRange) { _inRange = inRange; Refresh(); }
            }
        }

        public void SetPower(bool on)
        {
            if (OnBackupPower) on = true;
            if (Powered == on) return;
            Powered = on;
            Refresh();
        }

        public void SetEmergency(bool on, Color color, float intensityScale)
        {
            _emergency = on;
            if (Lamp != null)
            {
                Lamp.color = on ? color : _baseColor;
                Lamp.intensity = on ? _baseIntensity * intensityScale : _baseIntensity;
            }
            Refresh();
        }

        public void Stutter(float seconds)
        {
            if (!isActiveAndEnabled) return;
            if (_stutter != null) StopCoroutine(_stutter);
            _stutter = StartCoroutine(StutterRoutine(seconds));
        }

        IEnumerator StutterRoutine(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                _blink = !_blink || Random.value < 0.3f;
                Refresh();
                yield return new WaitForSeconds(Random.Range(0.03f, 0.14f));
            }
            _blink = false;
            Refresh();
            _stutter = null;
        }

        void Refresh()
        {
            bool lit = Lit;
            if (Lamp != null) Lamp.enabled = lit && _inRange && (!Accent || AccentsEnabled);
            if (Fill != null) Fill.enabled = lit && _inRange && FillsEnabled && !_emergency;
            if (Panel != null)
            {
                var m = !lit ? OffMat : (_emergency && EmergencyMat != null ? EmergencyMat : OnMat);
                if (m != null && Panel.sharedMaterial != m) Panel.sharedMaterial = m;
            }
        }

        public string QaTel() => name + " powered=" + Powered + " lit=" + Lit + " enabled=" + (Lamp != null && Lamp.enabled) +
            " intensity=" + (Lamp != null ? Lamp.intensity : 0f) + " color=" + (Lamp != null ? Lamp.color.ToString() : "none") +
            " pos=" + transform.position + " forward=" + transform.forward + " group=" + Group + " backup=" + OnBackupPower +
            " accent=" + Accent + " emergency=" + _emergency;

public static void PowerAll(bool on, string group = null)
        {
            for (int i = 0; i < All.Count; i++)
                if (group == null || All[i].Group == group) All[i].SetPower(on);
        }

        public static void EmergencyAll(bool on, Color color, float scale)
        {
            for (int i = 0; i < All.Count; i++)
                if (!All[i].Accent) All[i].SetEmergency(on, color, scale);
        }

        public static void StutterAll(float seconds, string group = null)
        {
            for (int i = 0; i < All.Count; i++)
                if ((group == null || All[i].Group == group) && !All[i].Accent) All[i].Stutter(seconds * Random.Range(0.7f, 1.2f));
        }

        
        public static void ApplyQuality()
        {
            int q = GameSettings.Quality;
            
            
            CullDistance = q == 0 ? 22f : 1000f;
            AccentsEnabled = q > 0;
            FillsEnabled = q > 0;
            for (int i = 0; i < All.Count; i++) { All[i]._cullTimer = 0f; All[i].Refresh(); }
        }
    }
}
