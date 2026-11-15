using System;
using UnityEngine;

namespace NightShift.Core
{

public static class NoiseSystem
    {
        
        public static event Action<Vector3, float, string> Heard;

        
        public static float Meter => Mathf.Clamp01(_level - (Time.time - _time) * 1.6f);

        static float _level, _time=-99f;

        public static void Emit(Vector3 pos, float radius, string source)
        {
            if (radius <= 0.01f) return;
            float lvl=Mathf.Clamp01(radius / 12f);
            if (lvl >= Meter) { _level=lvl; _time=Time.time; }
            Heard?.Invoke(pos, radius, source);
        }

        public static void ResetMeter() { _level=0f; _time=-99f; }
    }

    
    public static class ShiftClock
    {
        public const float StartMinutes=167f;  
        public const float EndMinutes=360f;    
        public static float Minutes { get; private set; }=StartMinutes;
        
        public static float SpinRate=1f;

        public static void Reset() { Minutes=StartMinutes; SpinRate=1f; }

        
        public static void Tick(float progress01, float dt)
        {
            float target=Mathf.Lerp(StartMinutes, EndMinutes - 8f, Mathf.Clamp01(progress01));
            float ceiling=target + 14f;
            Minutes=Mathf.Max(Minutes, target);
            Minutes=Mathf.Min(ceiling, Minutes + dt / 6f);
        }

        public static void ForceEnd() { Minutes=EndMinutes; }

        public static string Text
        {
            get
            {
                int m=Mathf.FloorToInt(Minutes);
                int h=(m / 60) % 24, mm=m % 60;
                int h12=h % 12 == 0 ? 12 : h % 12;
                return $"{h12:00}:{mm:00} {(h < 12 ? "AM" : "PM")}";
            }
        }
    }
}
