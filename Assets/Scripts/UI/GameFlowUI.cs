using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using NightShift.Core;
using NightShift.Objectives;
using NightShift.World;

namespace NightShift.UI
{

public class GameFlowUI : MonoBehaviour
    {
        public static GameFlowUI Instance { get; private set; }

        [Header("Cameras")]
        public Camera MenuCamera;
        public Camera PlayerCamera;

        [Header("Ending fallback (no ShiftDirector in scene)")]
        public float EndingDelay = 2.5f;

        enum Sub { None, Settings, HowTo }
        Sub _sub = Sub.None;
        bool _starting;
        float _endingTimer = -1f;
        bool _endingFired;

        void Awake()
        {
            Instance = this;
            GameSettings.Load();
            int pl = LayerMask.NameToLayer("Player"), mo = LayerMask.NameToLayer("Monster");
            if (pl >= 0 && mo >= 0) Physics.IgnoreLayerCollision(pl, mo, true);
        }

        void OnDestroy() { if (Instance == this) Instance=null; }

        void OnEnable()
        {
            GameEvents.StateChanged += OnStateChanged;
            GameEvents.TimelineRewound += OnTimelineRewound;
            GameSettings.Changed += OnSettingsChanged;
        }

        void OnDisable()
        {
            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.TimelineRewound -= OnTimelineRewound;
            GameSettings.Changed -= OnSettingsChanged;
        }

        void Start()
        {
            Time.timeScale=1f;
            ShiftClock.Reset();
            NoiseSystem.ResetMeter();
            GameSettings.Apply();
            var gm=GameManager.Instance;
            if (gm != null) OnStateChanged(gm.State);
            var ui=UIManager.Instance;
            if (ui != null) { ui.SetFade(1f); ui.FadeTo(0f, 1.6f); ui.SetTimeline(0); }
        }

        void OnSettingsChanged() { OfficeLight.ApplyQuality(); }

        void OnTimelineRewound()
        {
            var gm=GameManager.Instance;
            var ui=UIManager.Instance;
            if (gm != null && ui != null) ui.SetTimeline(gm.TimelineIndex);
        }

        void OnStateChanged(GameState s)
        {
            var ui=UIManager.Instance;
            bool menuCam=s == GameState.Title;
            if (MenuCamera != null) MenuCamera.enabled=menuCam;
            if (PlayerCamera != null) PlayerCamera.enabled=!menuCam;
            Time.timeScale=s == GameState.Paused ? 0f : 1f;
            AudioListener.pause=false;
            if (ui == null) return;
            _sub=Sub.None;
            switch (s)
            {
                case GameState.Title: ui.ShowTitle(); break;
                case GameState.Playing: ui.ShowHUD(); break;
                case GameState.Paused: ui.ShowPause(); break;
                case GameState.Ending: ui.ShowEnding(GameManager.Instance != null ? GameManager.Instance.TimelineIndex : 0); break;
            }
            if (s == GameState.Title || s == GameState.Paused || s == GameState.Ending)
            {
                Cursor.lockState=CursorLockMode.None;
                Cursor.visible=true;
            }
        }

        void Update()
        {
            var gm=GameManager.Instance;
            if (gm == null) return;
            var kb=Keyboard.current;

            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (_sub != Sub.None) Back();
                else if (gm.State == GameState.Playing) gm.SetState(GameState.Paused);
                else if (gm.State == GameState.Paused) gm.SetState(GameState.Playing);
            }

            if (gm.State == GameState.Title && _sub == Sub.None && !_starting && kb != null &&
                (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                StartShift();

            if (gm.State == GameState.Playing)
            {
                var om=ObjectiveManager.Instance;
                if (om != null && om.Sequence != null && om.Sequence.Length > 0)
                    ShiftClock.Tick(om.EscalationLevel / (float)om.Sequence.Length, Time.deltaTime);

                
                if (!_endingFired && Progression.ShiftDirector.Instance == null &&
                    om != null && om.Sequence != null && om.Sequence.Length > 0 && om.Current == null)
                {
                    if (_endingTimer < 0f) _endingTimer=EndingDelay;
                    _endingTimer -= Time.deltaTime;
                    if (_endingTimer <= 0f) { _endingFired=true; Debug.Log("TEL FLOW ending"); gm.SetState(GameState.Ending); }
                }
            }
        }

public void StartShift()
        {
            var gm = GameManager.Instance;
            if (gm == null || _starting) return;
            if (gm.State != GameState.Title) { gm.SetState(GameState.Playing); return; }
            StartCoroutine(StartRoutine());
        }

        IEnumerator StartRoutine()
        {
            _starting = true;
            var gm = GameManager.Instance;
            var ui = UIManager.Instance;
            GameEvents.RaiseStingerRequested("ui_click", 0.7f);
            if (ui != null) yield return ui.FadeTo(1f, 0.45f);
            gm.ResetRun();
            gm.SetState(GameState.Playing);
            Debug.Log("TEL FLOW shift started");
            if (ui != null)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                ui.Toast("02:47 AM", "FLOOR 13  ·  CHRONOCORP ACCOUNTS",3.2f);
                yield return ui.FadeTo(0f,1.4f);
                ui.Hint("<b>WASD</b> move   <b>SHIFT</b> sprint   <b>CTRL</b> crouch   <b>E</b> interact   <b>F</b> flashlight", 10f);
            }
            _starting = false;
        }

        public void ResumeShift()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.State == GameState.Paused) gm.SetState(GameState.Playing);
        }

        public void OpenSettings()
        {
            _sub = Sub.Settings;
            GameEvents.RaiseStingerRequested("ui_click", 0.6f);
            UIManager.Instance?.ShowSettings();
        }

        public void OpenHowTo()
        {
            _sub = Sub.HowTo;
            GameEvents.RaiseStingerRequested("ui_click", 0.6f);
            UIManager.Instance?.ShowHowTo();
        }

        
        public void Back()
        {
            _sub=Sub.None;
            GameEvents.RaiseStingerRequested("ui_click",0.6f);
            GameSettings.Save();
            var gm = GameManager.Instance;
            var ui = UIManager.Instance;
            if (gm == null || ui == null) return;
            if (gm.State == GameState.Paused) ui.ShowPause(); else ui.ShowTitle();
        }

        public void ResetSettings()
        {
            GameSettings.ResetDefaults();
            GameSettings.Apply();
            GameSettings.Save();
            GameEvents.RaiseStingerRequested("ui_click", 0.6f);
        }

        public void QuitToTitle()
        {
            
            Debug.Log("TEL FLOW reload to title");
            Time.timeScale = 1f;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
        }

        public void QuitGame()
        {
            Debug.Log("TEL FLOW quit");
            GameSettings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
    }
}
