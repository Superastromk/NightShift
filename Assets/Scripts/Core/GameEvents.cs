using System;

namespace NightShift.Core
{

public static class GameEvents
    {
        
        public static event Action<GameState> StateChanged;

        
        public static event Action<string> ObjectiveChanged;

        
        public static event Action TimelineRewound;

        
        public static event Action<string> ScareFired;

        
        public static event Action<string, float> StingerRequested;

        
        public static event Action<bool> RewindLock;

        
        public static event Action<UnityEngine.Vector3, string> DeathRegistered;

        
        public static event Action<float, string> ComposureDrained;

        internal static void RaiseStateChanged(GameState s) => StateChanged?.Invoke(s);
        internal static void RaiseObjectiveChanged(string t) => ObjectiveChanged?.Invoke(t);
        internal static void RaiseTimelineRewound() => TimelineRewound?.Invoke();
        internal static void RaiseRewindLock(bool l) => RewindLock?.Invoke(l);
        internal static void RaiseDeathRegistered(UnityEngine.Vector3 p, string c) => DeathRegistered?.Invoke(p, c);
        internal static void RaiseScareFired(string id) => ScareFired?.Invoke(id);
        internal static void RaiseStingerRequested(string id, float v) => StingerRequested?.Invoke(id, v);
        
        public static event Action<UnityEngine.Vector3, float, string> PlayerDamaged;

        internal static void RaiseComposureDrained(float a, string r) => ComposureDrained?.Invoke(a, r);
        public static void RaisePlayerDamaged(UnityEngine.Vector3 source, float amount, string reason) => PlayerDamaged?.Invoke(source, amount, reason);
    }

    
    public enum GameState
    {
        Title,
        Playing,
        Paused,
        Dead,
        Rewinding,
        Ending
    }
}
