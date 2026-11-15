using System.Collections;
using TMPro;
using UnityEngine;
using NightShift.Core;
using NightShift.Monster;
using NightShift.Player;

namespace NightShift.UI
{

public class UIManager : MonoBehaviour
    {
        static UIManager _instance;

        public static UIManager Instance
        {
            get
            {
                if (_instance == null) _instance = FindFirstObjectByType<UIManager>();
                return _instance;
            }
        }

        [Header("Panels")]
        public GameObject TitlePanel;
        public GameObject PausePanel;
        public GameObject HudPanel;
        public GameObject EndingPanel;
        public GameObject SettingsPanel;
        public GameObject HowToPanel;

        [Header("HUD text")]
        public TMP_Text ObjectiveText;
        public TMP_Text ObjectiveLabel;
        public TMP_Text TimelineText;
        public TMP_Text PromptText;
        public TMP_Text ClockText;
        public GameObject PromptGroup;
        public UnityEngine.UI.Image HoldRing;
        public TMP_Text SubtitleText;
        public GameObject SubtitleGroup;
        public TMP_Text HintText;

        [Header("Overlays")]
        public UnityEngine.UI.Image ComposureVignette;
        public CanvasGroup HintGroup;
        public UnityEngine.UI.Image FlashImage;
        public TMP_Text ToastTitle;

        [Header("Flash")]
        public float FlashInSeconds = 0.06f;
        public TMP_Text ToastBody;
        public float FlashHoldSeconds = 0.08f;
        public CanvasGroup ToastGroup;
        public TMP_Text FpsText;

        [Header("HUD meters")]
        public UnityEngine.UI.Image StaminaFill;
        public CanvasGroup StaminaGroup;
        public UnityEngine.UI.Image BatteryFill;
        public CanvasGroup BatteryGroup;
        public UnityEngine.UI.Image[] NoiseBars = new UnityEngine.UI.Image[0];
        public UnityEngine.UI.Image AwarenessIcon;
        public UnityEngine.UI.Image Crosshair;
        public float FlashOutSeconds = 0.4f;
        [Range(0f, 1f)] public float FlashPeakAlpha = 0.9f;
        public UnityEngine.UI.Image FadeImage;
        public UnityEngine.UI.Image ScanlineImage;
        public TMP_Text EndingTimelineText;

        [Header("Ending")]
        public TMP_Text EndingTitleText;

        void OnDestroy()
        { if (_instance == this) _instance = null; }

public void ShowTitle() => Exclusive(TitlePanel);
        public void ShowHUD() => Exclusive(HudPanel);

        [Header("Palette")]
        public Color Accent=new Color(0.95f, 0.72f, 0.3f);
        public Color Danger=new Color(0.9f, 0.2f, 0.16f);
        public Color Calm=new Color(0.82f, 0.88f, 0.92f);
        float _fpsTimer; 
        Coroutine _flash, _subtitle, _toast, _hint, _objectivePunch;
int _fpsFrames;
        float _composure=1f;

        void Awake()
        {
            if (_instance != null && _instance != this) { Debug.LogWarning("TEL WARN duplicate UIManager"); return; }
            _instance = this;
            if (PromptGroup != null) PromptGroup.SetActive(false);
            if (SubtitleGroup != null) SubtitleGroup.SetActive(false);
            if (ToastGroup != null) ToastGroup.alpha = 0f;
            if (HintGroup != null) HintGroup.alpha = 0f;
            SetFlashAlpha(0f);
            SetFade(0f);
        }
        public void ShowPause() => Exclusive(PausePanel);

        public void ShowEnding(int timelineCount)
        {
            if (EndingTimelineText != null) EndingTimelineText.text = $"TIMELINE {timelineCount:00}";
            Exclusive(EndingPanel);
        }

        void Exclusive(GameObject show)
        {
            Set(TitlePanel, show == TitlePanel);
            Set(PausePanel, show == PausePanel);
            Set(HudPanel, show == HudPanel);
            Set(EndingPanel, show == EndingPanel);
            Set(SettingsPanel, show == SettingsPanel);
            Set(HowToPanel, show == HowToPanel);
            SelectFirstButton(show);
        }
        public void ShowSettings() => Exclusive(SettingsPanel);
        public void ShowHowTo() => Exclusive(HowToPanel);

        static void Set(GameObject panel, bool on)
        {
            if (panel != null && panel.activeSelf != on) panel.SetActive(on);
        }

        public void SetEndingText(string title, string body)
        {
            if (EndingTitleText != null && !string.IsNullOrEmpty(title)) EndingTitleText.text = title;
            if (EndingTimelineText != null) EndingTimelineText.text = body ?? "";
            Exclusive(EndingPanel);
        }

        
        static void SelectFirstButton(GameObject panel)
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (events == null) return;
            if (panel == null || panel.name == "HUD") { events.SetSelectedGameObject(null); return; }
            var first=panel.GetComponentInChildren<UnityEngine.UI.Button>();
            events.SetSelectedGameObject(first != null ? first.gameObject : null);
        }

public void SetObjective(string text)
        {
            if (ObjectiveText == null) return;
            bool changed=ObjectiveText.text != (text ?? "");
            ObjectiveText.text = text ?? "";
            if (changed && isActiveAndEnabled && HudPanel != null && HudPanel.activeInHierarchy)
            {
                if (_objectivePunch != null) StopCoroutine(_objectivePunch);
                _objectivePunch = StartCoroutine(ObjectivePunch());
            }
        }

        public void SetTimeline(int n)
        {
            if (TimelineText != null) TimelineText.text = $"TIMELINE {n:00}";
        }

        public void ShowPrompt(string text)
        {
            if (PromptText == null) return;
            if (string.IsNullOrEmpty(text))
            { HidePrompt(); return; }
            PromptText.text = text;
            if (PromptGroup != null && !PromptGroup.activeSelf) PromptGroup.SetActive(true);
        }

        IEnumerator ObjectivePunch()
        {
            var rt = ObjectiveText.rectTransform;
            float t = 0f;
            while (t < 0.6f)
            {
                t += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Clamp01(t / 0.6f);
                ObjectiveText.color = Color.Lerp(Calm, Accent, k);
                rt.localScale = Vector3.one * (1f + 0.12f * k * k);
                yield return null;
            }
            ObjectiveText.color = Calm;
            rt.localScale = Vector3.one;
            _objectivePunch = null;
        }

        public void HidePrompt()
        {
            if (PromptGroup != null && PromptGroup.activeSelf) PromptGroup.SetActive(false);
        }

public void SetCinematic(bool on)
        {
            if (HudPanel == null) return;
            foreach (Transform child in HudPanel.transform)
            {
                if (SubtitleGroup != null && child.gameObject == SubtitleGroup) continue;
                if (ToastGroup != null && child.gameObject == ToastGroup.gameObject) continue;
                if (PromptGroup != null && child.gameObject == PromptGroup) continue;   
                if (HoldRing != null && child.gameObject == HoldRing.gameObject) continue;
                child.gameObject.SetActive(!on);
            }
        }

        
        public bool SubtitleActive => _subtitle != null;

public void ShowSubtitle(string text, float seconds, bool force = false)
        {
            if (SubtitleText == null || SubtitleGroup == null) return;
            if (!force && !GameSettings.Subtitles) return;
            if (_subtitle != null) StopCoroutine(_subtitle);
            SubtitleText.text = text ?? "";
            SubtitleGroup.SetActive(true);
            _subtitle=StartCoroutine(SubtitleRoutine(seconds));
        }

        IEnumerator SubtitleRoutine(float seconds)
        {
            float t=0f;
            while (t < seconds)
            {
                
                var gm=GameManager.Instance;
                if (gm == null || gm.State != GameState.Paused) t += Time.unscaledDeltaTime;
                yield return null;
            }
            SubtitleGroup.SetActive(false);
            _subtitle=null;
        }

        
        public void Toast(string title, string body, float seconds=3f)
        {
            if (ToastGroup == null) return;
            if (ToastTitle != null) ToastTitle.text=title ?? "";
            if (ToastBody != null) ToastBody.text = body ?? "";
            if (_toast != null) StopCoroutine(_toast);
            _toast = StartCoroutine(FadeGroup(ToastGroup, seconds, 0.25f, 0.8f, () => _toast = null));
        }

        
        public void Hint(string text, float seconds = 6f)
        {
            if (HintGroup == null || HintText == null) return;
            HintText.text = text ?? "";
            if (_hint != null) StopCoroutine(_hint);
            _hint = StartCoroutine(FadeGroup(HintGroup, seconds, 0.3f, 0.8f, () => _hint = null));
        }

        IEnumerator FadeGroup(CanvasGroup g, float hold, float fadeIn, float fadeOut, System.Action done)
        {
            float t = 0f;
            while (t < fadeIn)
            { t += Time.unscaledDeltaTime; g.alpha = Mathf.Clamp01(t / fadeIn); yield return null; }
            g.alpha = 1f;
            t = 0f;
            while (t < hold)
            { t += Time.unscaledDeltaTime; yield return null; }
            t = 0f;
            while (t < fadeOut)
            { t += Time.unscaledDeltaTime; g.alpha = 1f - Mathf.Clamp01(t / fadeOut); yield return null; }
            g.alpha = 0f;
            done?.Invoke();
        }

public void SetComposure(float v01)
        { _composure = Mathf.Clamp01(v01); }

        public void FlashRewind(float seconds)
        {
            if (FlashImage == null) return;
            if (_flash != null) StopCoroutine(_flash);
            _flash = StartCoroutine(FlashRoutine(seconds));
        }

        IEnumerator FlashRoutine(float seconds)
        {
            Debug.Log($"TEL FLASH start seconds={seconds:0.##} peak={FlashPeakAlpha:0.##}");
            float peak=FlashPeakAlpha;
            float t=0f;
            while (t < FlashInSeconds) { t += Time.unscaledDeltaTime; SetFlashAlpha(Mathf.Lerp(0f, peak, t / Mathf.Max(0.01f, FlashInSeconds))); yield return null; }
            SetFlashAlpha(peak);
            yield return new WaitForSecondsRealtime(FlashHoldSeconds + Mathf.Max(0f, seconds - FlashInSeconds - FlashHoldSeconds - FlashOutSeconds));
            t=0f;
            while (t < FlashOutSeconds) { t += Time.unscaledDeltaTime; SetFlashAlpha(Mathf.Lerp(peak, 0f, t / Mathf.Max(0.01f, FlashOutSeconds))); yield return null; }
            SetFlashAlpha(0f);
            _flash=null;
        }

        void SetFlashAlpha(float a)
        {
            if (FlashImage == null) return;
            var c=FlashImage.color;
            c.a=a;
            FlashImage.color=c;
            if (FlashImage.gameObject.activeSelf != a > 0.001f)
                FlashImage.gameObject.SetActive(a > 0.001f);
        }

        
        public void SetFade(float alpha)
        {
            if (FadeImage == null) return;
            FadeImage.color=new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
            if (FadeImage.gameObject.activeSelf != alpha > 0.001f) FadeImage.gameObject.SetActive(alpha > 0.001f);
        }

        
        public Coroutine FadeTo(float alpha, float seconds) => StartCoroutine(FadeRoutine(alpha, seconds));

        IEnumerator FadeRoutine(float target, float seconds)
        {
            float start=FadeImage != null ? FadeImage.color.a : 0f;
            float t=0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; SetFade(Mathf.Lerp(start, target, t / Mathf.Max(0.01f, seconds))); yield return null; }
            SetFade(target);
        }

        void Update()
        {
            var gm=GameManager.Instance;
            bool hud=HudPanel != null && HudPanel.activeInHierarchy;

            
            if (FpsText != null)
            {
                if (FpsText.gameObject.activeSelf != GameSettings.ShowFps) FpsText.gameObject.SetActive(GameSettings.ShowFps);
                if (GameSettings.ShowFps)
                {
                    _fpsFrames++; _fpsTimer += Time.unscaledDeltaTime;
                    if (_fpsTimer >= 0.5f) { FpsText.text=Mathf.RoundToInt(_fpsFrames / _fpsTimer) + " FPS"; _fpsFrames = 0; _fpsTimer = 0f; }
                }
            }

            bool rewinding = gm != null && gm.State == GameState.Rewinding;
            if (ScanlineImage != null)
            {
                if (ScanlineImage.gameObject.activeSelf != rewinding) ScanlineImage.gameObject.SetActive(rewinding);
                if (rewinding) ScanlineImage.rectTransform.anchoredPosition = new Vector2(0f,(Time.unscaledTime * 900f) % 64f);
            }
            if (!hud) return;

            float dt = Time.unscaledDeltaTime;
            var fpc = FirstPersonController.Instance;
            if (fpc != null && StaminaFill != null)
            {
                StaminaFill.fillAmount = fpc.Stamina01;
                StaminaFill.color = fpc.Exhausted ? Danger : Calm;
                if (StaminaGroup != null)
                    StaminaGroup.alpha = Mathf.MoveTowards(StaminaGroup.alpha,fpc.Stamina01 < 0.995f ? 1f : 0f,dt * 2.5f);
            }
            var torch = Flashlight.Instance;
            if (torch != null && BatteryFill != null)
            {
                BatteryFill.fillAmount = torch.Battery01;
                BatteryFill.color = torch.Battery01 < 0.2f ? Danger : Accent;
                if (BatteryGroup != null)
                    BatteryGroup.alpha = Mathf.MoveTowards(BatteryGroup.alpha,torch.IsOn || torch.Battery01 < 0.98f ? 1f : 0f,dt * 2.5f);
            }
            if (NoiseBars != null && NoiseBars.Length > 0)
            {
                float m = NoiseSystem.Meter;
                for (int i = 0; i < NoiseBars.Length; i++)
                {
                    if (NoiseBars[i] == null) continue;
                    bool on = m > (i + 0.35f) / NoiseBars.Length;
                    var c = on ? (i >= NoiseBars.Length - 1 ? Danger : Calm) : new Color(1f,1f,1f,0.12f);
                    NoiseBars[i].color = Color.Lerp(NoiseBars[i].color,c,dt * 14f);
                }
            }
            if (AwarenessIcon != null)
            {
                var monster = MonsterAI.Instance;
                Color target = new Color(1f,1f,1f,0f);
                if (monster != null && gm != null && gm.State == GameState.Playing)
                {
                    switch (monster.CurrentState)
                    {
                        case MonsterState.Chase:
                        case MonsterState.Lunge: target = Danger; break;
                        case MonsterState.Stalk:
                        case MonsterState.Investigate: target = Accent; break;
                    }
                }
                AwarenessIcon.color = Color.Lerp(AwarenessIcon.color,target,dt * 6f);
                float pulse = target.a > 0.5f ? 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 9f) : 1f;
                AwarenessIcon.rectTransform.localScale = Vector3.one * pulse;
            }
            if (HoldRing != null)
            {
                var interactor = fpc != null ? fpc.GetComponent<PlayerInteractor>() : null;
                float p = interactor != null ? interactor.HoldProgress : 0f;
                HoldRing.fillAmount = p;
                if (HoldRing.gameObject.activeSelf != p > 0.001f) HoldRing.gameObject.SetActive(p > 0.001f);
            }
            if (Crosshair != null)
            {
                bool aiming = PromptGroup != null && PromptGroup.activeSelf;
                Crosshair.color = Color.Lerp(Crosshair.color,aiming ? Accent : new Color(1f,1f,1f,0.35f),dt * 12f);
                Crosshair.rectTransform.localScale = Vector3.Lerp(Crosshair.rectTransform.localScale,Vector3.one * (aiming ? 1.7f : 1f),dt * 12f);
            }
            if (ClockText != null) ClockText.text = ShiftClock.Text;
            if (ComposureVignette != null)
            {
                var c = ComposureVignette.color;
                c.a = Mathf.MoveTowards(c.a,(1f - _composure) * 0.85f,dt * 0.8f);
                ComposureVignette.color = c;
            }
        }
    }
}
