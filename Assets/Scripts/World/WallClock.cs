using UnityEngine;
using NightShift.Core;

namespace NightShift.World
{
    
    public class WallClock : MonoBehaviour
    {
        public Transform HourHand, MinuteHand;
        float _visualMinutes = -1f;

        void Update()
        {
            float target = ShiftClock.Minutes;
            if (_visualMinutes < 0f) _visualMinutes = target;
            var gm = GameManager.Instance;
            bool rewinding = gm != null && gm.State == GameState.Rewinding;
            if (rewinding) _visualMinutes -= Time.deltaTime * 55f;         
            else _visualMinutes = Mathf.MoveTowards(_visualMinutes, target, Time.deltaTime * 90f);
            float m = _visualMinutes;
            if (MinuteHand != null) MinuteHand.localRotation = Quaternion.Euler(0f, 0f, (m % 60f) * 6f);
            if (HourHand != null) HourHand.localRotation = Quaternion.Euler(0f, 0f, (m / 60f % 12f) * 30f);
        }
    }
}
