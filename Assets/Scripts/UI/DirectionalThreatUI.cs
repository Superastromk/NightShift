using System.Collections.Generic;
using UnityEngine;
using NightShift.Core;
using NightShift.Monster;
using NightShift.Progression;

namespace NightShift.UI
{
    
    [DefaultExecutionOrder(200)]
    public class DirectionalThreatUI : MonoBehaviour
    {
        public Camera PlayerCamera;
        public Sprite Chevron;
        public float DamageRadius = 42f;
        public float BossRadius = 64f;
        public float DamageSeconds = 1.45f;

        class Hit
        {
            public Vector3 Source;
            public float Amount;
            public float Time;
        }

        readonly List<Hit> _hits = new List<Hit>();
        readonly UnityEngine.UI.Image[] _damage = new UnityEngine.UI.Image[4];
        UnityEngine.UI.Image _boss;
        float _bossAngle;
        bool _bossVisible,_bossDanger;

        void OnEnable()
        {
            GameEvents.PlayerDamaged += OnPlayerDamaged;
            GameEvents.StateChanged += OnGameStateChanged;
        }

        void OnDisable()
        {
            GameEvents.PlayerDamaged -= OnPlayerDamaged;
            GameEvents.StateChanged -= OnGameStateChanged;
            ClearAll();
        }

        void OnGameStateChanged(GameState state)
        {
            ClearAll();
        }

        void Start()
        {
            if (PlayerCamera == null) PlayerCamera = Camera.main;
            BuildVisuals();
        }

        void BuildVisuals()
        {
            if (_boss != null) return;
            for (int i = 0; i < _damage.Length; i++)
                _damage[i] = MakeChevron("DamageDirection" + i, new Vector2(34f, 22f));
            _boss = MakeChevron("BossDirection", new Vector2(30f, 24f));
        }

        UnityEngine.UI.Image MakeChevron(string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Outline));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.sprite = Chevron;
            image.color = Color.clear;
            image.raycastTarget = false;
            var outline = go.GetComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return image;
        }

        void OnPlayerDamaged(Vector3 source, float amount, string reason)
        {
            if (amount <= 0f) return;
            var game = GameManager.Instance;
            if (game != null && game.State != GameState.Playing) return;
            BuildVisuals();
            _hits.Add(new Hit { Source = source, Amount = amount, Time = Time.unscaledTime });
            if (_hits.Count > 4) _hits.RemoveAt(0);
            Debug.Log($"TEL DAMAGE source={source.x:F1},{source.z:F1} amount={amount:F0} reason={reason}");
        }

        void LateUpdate()
        {
            if (_boss == null) return;
            if (PlayerCamera == null || !PlayerCamera.isActiveAndEnabled) PlayerCamera=Camera.main;
            bool playing=GameManager.Instance == null || GameManager.Instance.State == GameState.Playing;
            if (PlayerCamera == null || !playing)
            {
                ClearAll();
                return;
            }

            for (int i=_hits.Count - 1; i >= 0; i--)
                if (Time.unscaledTime - _hits[i].Time > DamageSeconds) _hits.RemoveAt(i);
            for (int i=0; i < _damage.Length; i++)
            {
                var image=_damage[i];
                if (i >= _hits.Count) { image.color=Color.clear; continue; }
                Hit hit=_hits[_hits.Count - 1 - i];
                float age=Time.unscaledTime - hit.Time;
                float alpha=Mathf.Clamp01((DamageSeconds - age) / 0.45f);
                float strength=Mathf.Lerp(0.9f, 1f, Mathf.Clamp01(hit.Amount / 24f));
                Place(image, hit.Source, DamageRadius, new Color(1f, 0.15f, 0.1f, alpha * strength));
            }

            var monster=MonsterAI.Instance;
            var director=ShiftDirector.Instance;
            _bossVisible=monster != null && (director != null && director.Hunting ||
                monster.CurrentState == MonsterState.Chase || monster.CurrentState == MonsterState.Scream ||
                monster.CurrentState == MonsterState.Lunge);
            _bossDanger=director != null && (director.Hunting || director.Act == 3) ||
                monster != null && monster.CurrentState == MonsterState.Lunge;
            if (!_bossVisible) { _boss.color=Color.clear; return; }
            Place(_boss, monster.transform.position + Vector3.up * 1.2f, BossRadius,
                _bossDanger ? new Color(1f, 0.12f, 0.075f, 0.95f) : new Color(1f, 0.64f, 0.19f, 0.95f));
            _boss.rectTransform.localScale=Vector3.one * (1f + 0.07f * Mathf.Sin(Time.unscaledTime * 8f));
        }

        void Place(UnityEngine.UI.Image image, Vector3 source, float radius, Color color)
        {
            Vector3 local=PlayerCamera.transform.InverseTransformDirection(source - PlayerCamera.transform.position);
            float angle=Mathf.Atan2(local.x, local.z);
            var rt=image.rectTransform;
            rt.anchoredPosition=new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius;
            rt.localRotation=Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
            image.color=color;
            if (image == _boss) _bossAngle=angle * Mathf.Rad2Deg;
        }

        void ClearAll()
        {
            _hits.Clear();
            foreach (var image in _damage) if (image != null) image.color=Color.clear;
            if (_boss != null)
            {
                _boss.color=Color.clear;
                _boss.rectTransform.localScale=Vector3.one;
            }
            _bossVisible=false;
            _bossDanger=false;
            _bossAngle=0f;
        }

        public string QaTel() => $"damage={_hits.Count} boss={(_bossVisible ? 1 : 0)} bossangle={_bossAngle:F0} bossdanger={(_bossDanger ? 1 : 0)}";
    }
}
