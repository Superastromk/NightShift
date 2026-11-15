using System.Collections.Generic;
using UnityEngine;
using NightShift.Core;

namespace NightShift.Audio
{

public class AudioDirector : MonoBehaviour
    {
        static AudioDirector _instance;

        public static AudioDirector Instance
        {
            get
            {
                if (_instance == null) _instance = FindFirstObjectByType<AudioDirector>();
                return _instance;
            }
        }

        [Header("Clip table (parallel lists)")]
        public List<string> ClipIds = new List<string>();
        public List<AudioClip> ClipClips = new List<AudioClip>();

        [Header("Config")]
        public int StingerPoolSize = 6;
        public int WorldPoolSize = 6;
        public float DuckCutoffHz = 650f;
        public float NormalCutoffHz = 22000f;
        public float DefaultVoiceVolume = 1f;
        public float AmbienceVolume = 0.55f;
        public float HeartbeatVolume = 0.8f;
        public float DroneVolume = 0.55f;

        public Dictionary<string, AudioClip> Clips { get; private set; } = new Dictionary<string, AudioClip>();
        public string[] FootstepIds = { "footstep", "footstep_b", "footstep_c" };

        
        public float AmbienceScale { get; set; } = 1f;

        AudioSource[] _pool, _world;
        int _poolNext, _worldNext;
        AudioSource _ambience, _voice, _heartbeat, _drone, _steps;
        AudioLowPassFilter _lowpass;
        float _cutoffTarget = 22000f;
        float _tension, _chase;
        readonly HashSet<string> _warned = new HashSet<string>();

        void Awake()
        {
            if (_instance != null && _instance != this) { Debug.LogWarning("TEL WARN duplicate AudioDirector"); return; }
            _instance = this;
            GameSettings.Load();

            Clips.Clear();
            int n = Mathf.Min(ClipIds.Count, ClipClips.Count);
            for (int i = 0; i < n; i++)
                if (!string.IsNullOrEmpty(ClipIds[i]) && ClipClips[i] != null) Clips[ClipIds[i]] = ClipClips[i];

            _pool = new AudioSource[Mathf.Max(1, StingerPoolSize)];
            for (int i = 0; i < _pool.Length; i++) _pool[i] = Make2D("Sfx" + i, false);

            _world=new AudioSource[Mathf.Max(1, WorldPoolSize)];
            for (int i=0; i < _world.Length; i++)
            {
                var go=new GameObject("World" + i);
                go.transform.SetParent(transform,false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 1.5f;
                src.maxDistance = 22f;
                src.dopplerLevel = 0f;
                _world[i] = src;
            }

            _ambience = Make2D("Ambience", true);
            _heartbeat = Make2D("Heartbeat", true);
            _drone = Make2D("Drone", true);
            _voice=Make2D("Voice",false);
            _steps = Make2D("Steps", false);
        }

        void OnDestroy() { if (_instance == this) _instance = null; }

        AudioSource Make2D(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 0f;
            return src;
        }

        void OnEnable()
        {
            GameEvents.StingerRequested += OnStingerRequested;
            GameEvents.StateChanged += OnStateChanged;
        }

        void OnDisable()
        {
            GameEvents.StingerRequested -= OnStingerRequested;
            GameEvents.StateChanged -= OnStateChanged;
        }

        void Start() { PlayAmbience(); }

        void OnStingerRequested(string clipId, float volume) => PlayStinger(clipId, volume);

        void OnStateChanged(GameState s)
        {
            if (s == GameState.Title || s == GameState.Playing) PlayAmbience();
            bool paused = s == GameState.Paused;
            if (_voice != null) { if (paused) _voice.Pause(); else _voice.UnPause(); }
            if (s == GameState.Title || s == GameState.Ending) { _tension = 0f; _chase = 0f; }
        }

        
        public void PlayStinger(string clipId, float volume = 1f)
        {
            var clip = Find(clipId);
            if (clip == null) return;
            Debug.Log($"TEL AUDIO {clipId}");
            var src = _pool[_poolNext];
            for (int i = 0; i < _pool.Length; i++)
            {
                var s = _pool[(_poolNext + i) % _pool.Length];
                if (!s.isPlaying)
                { src = s; break; }
            }
            _poolNext = (_poolNext + 1) % _pool.Length;
            src.pitch = 1f;
            src.PlayOneShot(clip, volume * GameSettings.Sfx);
        }

        AudioClip Find(string id)
        {
            if (!string.IsNullOrEmpty(id) && Clips.TryGetValue(id, out var clip) && clip != null) return clip;
            if (_warned.Add(id ?? "null")) Debug.LogWarning($"TEL WARN AudioDirector: unknown clip id '{id}'");
            return null;
        }

        
        public void PlayVoice(string lineId)
        {
            var clip = Find(lineId);
            if (clip == null) return;
            Debug.Log($"TEL AUDIO {lineId}");
            _voice.PlayOneShot(clip, DefaultVoiceVolume * GameSettings.Voice);
        }

        
        public void PlayAt(string clipId, Vector3 pos, float volume = 1f)
        {
            var clip = Find(clipId);
            if (clip == null) return;
            var src = _world[_worldNext];
            _worldNext = (_worldNext + 1) % _world.Length;
            src.transform.position = pos;
            src.pitch = Random.Range(0.92f, 1.08f);
            src.PlayOneShot(clip, Mathf.Clamp01(volume) * GameSettings.Sfx);
        }

        
        public void PlayFootstep(float volume)
        {
            AudioClip clip = null;
            for (int tries = 0; tries < 3 && clip == null; tries++)
            {
                string id = FootstepIds[Random.Range(0, FootstepIds.Length)];
                Clips.TryGetValue(id, out clip);
            }
            if (clip == null) return;
            _steps.pitch = Random.Range(0.88f, 1.1f);
            _steps.PlayOneShot(clip, volume * GameSettings.Sfx);
        }

        
        public void PlayAmbience()
        {
            if (_ambience == null || _ambience.isPlaying) return;
            var clip = Find("ambience_office");
            if (clip == null) return;
            _ambience.clip = clip;
            _ambience.volume = 0f;
            _ambience.Play();
        }

        
        public void StopVoice()
        { if (_voice != null) _voice.Stop(); }

        public void DuckForRewind()
        { _cutoffTarget = DuckCutoffHz; }

        
        public void SetTension(float danger01, bool hunting)
        {
            _tension = Mathf.Clamp01(danger01);
            _chase = hunting ? Mathf.Clamp01(0.35f + danger01) : 0f;
        }
        public void RestoreAfterRewind()
        { _cutoffTarget = NormalCutoffHz; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var gm = GameManager.Instance;
            bool live = gm == null || gm.State == GameState.Playing || gm.State == GameState.Dead || gm.State == GameState.Rewinding;

            if (_ambience != null)
                _ambience.volume = Mathf.MoveTowards(_ambience.volume, AmbienceVolume * AmbienceScale * GameSettings.Ambience, dt * 0.6f);

            Loop(_heartbeat, "heartbeat", live ? _tension * HeartbeatVolume * GameSettings.Sfx : 0f, dt, 0.9f + _tension * 0.45f);
            Loop(_drone, "drone_chase",live ? _chase * DroneVolume * GameSettings.Ambience : 0f,dt,1f);

            
            if (_lowpass == null)
            {
                var listener = FindFirstObjectByType<AudioListener>();
                if (listener != null)
                {
                    _lowpass = listener.GetComponent<AudioLowPassFilter>();
                    if (_lowpass == null) _lowpass = listener.gameObject.AddComponent<AudioLowPassFilter>();
                    _lowpass.cutoffFrequency = NormalCutoffHz;
                }
            }
            else if (!Mathf.Approximately(_lowpass.cutoffFrequency,_cutoffTarget))
            {
                _lowpass.cutoffFrequency = Mathf.MoveTowards(_lowpass.cutoffFrequency,_cutoffTarget,dt * 60000f);
                _lowpass.enabled = _lowpass.cutoffFrequency < NormalCutoffHz - 50f;
            }
        }

        void Loop(AudioSource src,string clipId,float targetVol,float dt,float pitch)
        {
            if (src == null) return;
            if (src.clip == null)
            {
                if (!Clips.TryGetValue(clipId,out var clip) || clip == null) return;
                src.clip = clip;
            }
            src.volume = Mathf.MoveTowards(src.volume,targetVol,dt * 0.9f);
            src.pitch = pitch;
            if (src.volume > 0.005f && !src.isPlaying) src.Play();
            else if (src.volume <= 0.005f && src.isPlaying) src.Stop();
        }

        
        public string QaTel() => $"clips={Clips.Count} amb={(_ambience != null && _ambience.isPlaying ? 1 : 0)} tension={_tension:0.00}";
    }
}
