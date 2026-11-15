using TMPro;
using UnityEngine;
using NightShift.Core;

namespace NightShift.UI
{

public class SettingWidget : MonoBehaviour
    {
        public enum Kind
        {
            Master, Ambience, Sfx, Voice,
            Sensitivity, Fov, RenderScale, Brightness,
            InvertY, HoldCrouch, HeadBob, Subtitles, ScreenShake, ShowFps, Fullscreen, VSync, FilmGrain,
            Quality, FpsCap
        }

        public Kind Setting;
        public UnityEngine.UI.Slider Slider;
        public UnityEngine.UI.Toggle Toggle;
        public UnityEngine.UI.Button Prev, Next;
        public TMP_Text ValueLabel;

        bool _refreshing;

        void Awake()
        {
            GameSettings.Load();
            if (Slider != null) Slider.onValueChanged.AddListener(OnSlider);
            if (Toggle != null) Toggle.onValueChanged.AddListener(OnToggle);
            if (Prev != null) Prev.onClick.AddListener(() => Step(-1));
            if (Next != null) Next.onClick.AddListener(() => Step(1));
        }

        void OnEnable()
        {
            GameSettings.Changed += Refresh;
            Refresh();
        }

        void OnDisable() { GameSettings.Changed -= Refresh; }

        public void Refresh()
        {
            _refreshing = true;
            if (Slider != null) Slider.SetValueWithoutNotify(GetFloat());
            if (Toggle != null) Toggle.SetIsOnWithoutNotify(GetBool());
            if (ValueLabel != null) ValueLabel.text = Label();
            _refreshing = false;
        }

        void OnSlider(float v) { if (_refreshing) return; SetFloat(v); Commit(); }
        void OnToggle(bool v) { if (_refreshing) return; SetBool(v); Commit(); }

        void Step(int dir)
        {
            if (Setting == Kind.Quality)
            {
                GameSettings.Quality = (GameSettings.Quality + dir + GameSettings.QualityNames.Length) % GameSettings.QualityNames.Length;
                
                GameSettings.RenderScale = GameSettings.Quality == 0 ? 0.75f : 1f;
            }
            else if (Setting == Kind.FpsCap)
                GameSettings.FpsCap = (GameSettings.FpsCap + dir + GameSettings.FpsCaps.Length) % GameSettings.FpsCaps.Length;
            GameEvents.RaiseStingerRequested("ui_click", 0.5f);
            Commit();
        }

        void Commit()
        {
            GameSettings.Apply();
            GameSettings.Save();
        }

        float GetFloat()
        {
            switch (Setting)
            {
                case Kind.Master: return GameSettings.Master;
                case Kind.Ambience: return GameSettings.Ambience;
                case Kind.Sfx: return GameSettings.Sfx;
                case Kind.Voice: return GameSettings.Voice;
                case Kind.Sensitivity: return GameSettings.Sensitivity;
                case Kind.Fov: return GameSettings.Fov;
                case Kind.RenderScale: return GameSettings.RenderScale;
                case Kind.Brightness: return GameSettings.Brightness;
            }
            return 0f;
        }

        void SetFloat(float v)
        {
            switch (Setting)
            {
                case Kind.Master: GameSettings.Master = v; break;
                case Kind.Ambience: GameSettings.Ambience = v; break;
                case Kind.Sfx: GameSettings.Sfx = v; break;
                case Kind.Voice: GameSettings.Voice = v; break;
                case Kind.Sensitivity: GameSettings.Sensitivity = v; break;
                case Kind.Fov: GameSettings.Fov = v; break;
                case Kind.RenderScale: GameSettings.RenderScale = v; break;
                case Kind.Brightness: GameSettings.Brightness = v; break;
            }
        }

        bool GetBool()
        {
            switch (Setting)
            {
                case Kind.InvertY: return GameSettings.InvertY;
                case Kind.HoldCrouch: return GameSettings.HoldToCrouch;
                case Kind.HeadBob: return GameSettings.HeadBob;
                case Kind.Subtitles: return GameSettings.Subtitles;
                case Kind.ScreenShake: return GameSettings.ScreenShake;
                case Kind.ShowFps: return GameSettings.ShowFps;
                case Kind.Fullscreen: return GameSettings.Fullscreen;
                case Kind.VSync: return GameSettings.VSync;
                case Kind.FilmGrain: return GameSettings.FilmGrain;
            }
            return false;
        }

        void SetBool(bool v)
        {
            switch (Setting)
            {
                case Kind.InvertY: GameSettings.InvertY = v; break;
                case Kind.HoldCrouch: GameSettings.HoldToCrouch = v; break;
                case Kind.HeadBob: GameSettings.HeadBob = v; break;
                case Kind.Subtitles: GameSettings.Subtitles = v; break;
                case Kind.ScreenShake: GameSettings.ScreenShake = v; break;
                case Kind.ShowFps: GameSettings.ShowFps = v; break;
                case Kind.Fullscreen: GameSettings.Fullscreen = v; break;
                case Kind.VSync: GameSettings.VSync = v; break;
                case Kind.FilmGrain: GameSettings.FilmGrain = v; break;
            }
        }

        string Label()
        {
            switch (Setting)
            {
                case Kind.Master:
                case Kind.Ambience:
                case Kind.Sfx:
                case Kind.Voice:
                case Kind.RenderScale: return Mathf.RoundToInt(GetFloat() * 100f) + "%";
                case Kind.Sensitivity: return GetFloat().ToString("0.00") + "x";
                case Kind.Fov: return Mathf.RoundToInt(GetFloat()) + "°";
                case Kind.Brightness: { int b = Mathf.RoundToInt(GetFloat() * 100f); return (b > 0 ? "+" : "") + b; }
                case Kind.Quality: return GameSettings.QualityNames[Mathf.Clamp(GameSettings.Quality,0,2)].ToUpperInvariant();
                case Kind.FpsCap: return GameSettings.VSync ? "VSYNC" : GameSettings.FpsCapNames[GameSettings.FpsCap].ToUpperInvariant();
            }
            return GetBool() ? "ON" : "OFF";
        }
    }
}
