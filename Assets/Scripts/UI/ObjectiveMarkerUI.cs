using TMPro;
using UnityEngine;
using NightShift.Core;
using NightShift.Objectives;

namespace NightShift.UI
{

public class ObjectiveMarkerUI : MonoBehaviour
    {
        public RectTransform Marker;
        public UnityEngine.UI.Image Icon;
        public TMP_Text DistanceText;
        public float EdgePadding = 70f;
        public float HideWithin = 2.2f;
        public float HeightOffset = 0.35f;

        Camera _cam;
        RectTransform _canvasRect;
        CanvasGroup _group;

        void Awake()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) _canvasRect = canvas.transform as RectTransform;
            if (Marker != null)
            {
                _group = Marker.GetComponent<CanvasGroup>();
                if (_group == null) _group = Marker.gameObject.AddComponent<CanvasGroup>();
                _group.alpha = 0f;
            }
        }

        void LateUpdate()
        {
            if (Marker == null || _canvasRect == null) return;
            if (_cam == null || !_cam.enabled) _cam = Camera.main;
            var om = ObjectiveManager.Instance;
            var gm = GameManager.Instance;
            Vector3 world = Vector3.zero;
            bool show = _cam != null && om != null && gm != null && gm.State == GameState.Playing && om.TryGetMarker(out world);
            float targetAlpha = 0f;
            if (show)
            {
                world += Vector3.up * HeightOffset;
                float dist = Vector3.Distance(_cam.transform.position, world);
                Vector3 vp = _cam.WorldToViewportPoint(world);
                bool behind = vp.z < 0f;
                Vector2 dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
                if (behind) dir = -dir;
                Vector2 size = _canvasRect.rect.size;
                Vector2 half = size * 0.5f - new Vector2(EdgePadding, EdgePadding);
                Vector2 pos = new Vector2(dir.x * size.x, dir.y * size.y);
                bool off = behind || Mathf.Abs(pos.x) > half.x || Mathf.Abs(pos.y) > half.y;
                if (off)
                {
                    if (pos.sqrMagnitude < 1f) pos = Vector2.down;
                    float k = Mathf.Min(half.x / Mathf.Max(1f, Mathf.Abs(pos.x)), half.y / Mathf.Max(1f, Mathf.Abs(pos.y)));
                    pos *= k;
                }
                Marker.anchoredPosition = pos;
                if (DistanceText != null) DistanceText.text = Mathf.RoundToInt(dist) + " m";
                targetAlpha=dist < HideWithin ? 0f : (off ? 0.95f : 0.7f);
                if (Icon != null) Icon.rectTransform.localRotation=Quaternion.Euler(0f, 0f, 45f + (off ? Mathf.Sin(Time.unscaledTime * 5f) * 8f : 0f));
            }
            if (_group != null) _group.alpha=Mathf.MoveTowards(_group.alpha, targetAlpha, Time.unscaledDeltaTime * 3f);
        }
    }
}
