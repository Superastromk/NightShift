using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NightShift.Core
{

[RequireComponent(typeof(Volume))]
    public class PostFXDriver : MonoBehaviour
    {
        public static PostFXDriver Instance { get; private set; }

        [Header("Base look")]
        public float BaseExposure=0.3f;
        public float BaseSaturation=-6f;
        public float BaseVignette=0.28f;
        public float BaseChromatic=0f;
        public float BaseGrain=0f;

        [Header("Rewind look")]
        public float RewindChromatic = 1f;
        public float RewindLens = -0.42f;
        public float RewindSaturation = -75f;
        public float RewindVignette = 0.48f;
        public float BlendSpeed = 7f;

        
        public float Danger { get; set; }
        
        public float HitPulse { get; set; }

        Volume _volume;
        ColorAdjustments _color;
        ChromaticAberration _ca;
        LensDistortion _lens;
        Vignette _vignette;
        FilmGrain _grain;
        Bloom _bloom;
        float _rewind;

        void Awake()
        {
            Instance = this;
            _volume = GetComponent<Volume>();
            var p = _volume.profile; 
            p.TryGet(out _color);
            p.TryGet(out _ca);
            p.TryGet(out _lens);
            p.TryGet(out _vignette);
            p.TryGet(out _grain);
            p.TryGet(out _bloom);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            var gm = GameManager.Instance;
            bool rewinding = gm != null && gm.State == GameState.Rewinding;
            bool dead = gm != null && gm.State == GameState.Dead;
            float dt = Time.unscaledDeltaTime;
            _rewind = Mathf.MoveTowards(_rewind,rewinding ? 1f : 0f,dt * (rewinding ? BlendSpeed : 2.5f));
            HitPulse = Mathf.MoveTowards(HitPulse,dead ? 1f : 0f,dt * (dead ? 12f : 1.8f));
            float danger = Mathf.Clamp01(Danger);
            bool low = GameSettings.Quality == 0;

            if (_color != null)
            {
                _color.postExposure.Override(BaseExposure + GameSettings.Brightness * 1.2f);
                _color.saturation.Override(Mathf.Lerp(BaseSaturation - danger * 18f,RewindSaturation,_rewind));
                _color.colorFilter.Override(Color.Lerp(Color.Lerp(Color.white,new Color(0.72f,0.86f,1f),_rewind),
                    new Color(1f,0.35f,0.3f),HitPulse * 0.75f));
            }
            if (_ca != null)
            {
                float ca = Mathf.Lerp(BaseChromatic + danger * 0.08f,RewindChromatic,_rewind);
                _ca.active = !low || ca > 0.2f;
                _ca.intensity.Override(ca);
            }
            if (_lens != null)
            {
                float wobble = _rewind * Mathf.Sin(Time.unscaledTime * 23f) * 0.06f;
                _lens.active = _rewind > 0.01f;
                _lens.intensity.Override(RewindLens * _rewind + wobble);
            }
            if (_vignette != null)
            {
                float pulse = danger * (0.08f + 0.05f * Mathf.Sin(Time.unscaledTime * 6.5f));
                _vignette.intensity.Override(Mathf.Lerp(BaseVignette + pulse,RewindVignette,_rewind) + HitPulse * 0.15f);
                _vignette.color.Override(Color.Lerp(Color.black,new Color(0.35f,0f,0f),Mathf.Max(danger * 0.6f,HitPulse)));
            }
            if (_grain != null)
            {
                _grain.active = GameSettings.FilmGrain && !low;
                _grain.intensity.Override(BaseGrain + _rewind * 0.12f);
            }
            if (_bloom != null) _bloom.active = !low;
        }
    }
}
