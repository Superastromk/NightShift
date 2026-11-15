using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using NightShift.Core;

namespace NightShift.Monster
{

public class MonsterMemory : MonoBehaviour
    {
        [Header("Tier thresholds (deaths required per tier)")]
        [Tooltip("Deaths required to reach each tier, index = tier. Defaults: 0,1,2,3,4 deaths -> tiers 0..4.")]
        public int[] TierThresholds={ 0, 1, 2, 3, 4 };

        [Header("Player tracking")]
        [Tooltip("Seconds between LastKnownPlayerPos updates.")]
        public float PlayerTrackInterval = 2f;
        [Tooltip("Player must be within this range to update LastKnownPlayerPos.")]
        public float TrackRange = 60f;

        readonly List<Vector3> _deathPositions = new List<Vector3>();
        Transform _player;
        float _trackTimer;
        int _lastLoggedTier = -1;
        EventInfo _deathEvent;
        Delegate _deathHandler;

        
        public int DeathCount => _deathPositions.Count;

        
        public int MemoryTier => ComputeTier(_deathPositions.Count);

        
        public Vector3 LastDeathPosition =>
            _deathPositions.Count > 0 ? _deathPositions[_deathPositions.Count - 1] : Vector3.zero;

        
        public Vector3[] DeathPositions => _deathPositions.ToArray();

        
        public Vector3 LastKnownPlayerPos { get; set; }

        
        public bool ShouldAnticipateRoute => MemoryTier >= 2;

        
        public bool ShouldCampObjective => MemoryTier >= 3;

        void Awake()
        {
            TrySubscribeDeathEvent();
        }

        void OnDestroy()
        {
            if (_deathEvent != null && _deathHandler != null)
            {
                try { _deathEvent.RemoveEventHandler(null, _deathHandler); }
                catch {  }
            }
        }

        void Update()
        {
            CachePlayer();
            _trackTimer -= Time.deltaTime;
            if (_trackTimer <= 0f)
            {
                _trackTimer = Mathf.Max(0.1f, PlayerTrackInterval);
                TrackPlayer();
            }
        }

public void NotifyDeath(Vector3 deathPos)
        {
            _deathPositions.Add(deathPos);
            int tier = MemoryTier;
            if (tier != _lastLoggedTier)
            {
                _lastLoggedTier = tier;
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "TEL MEMORY tier={0} deaths={1}", tier, _deathPositions.Count));
            }
        }

public void ResetMemory()
        {
            _deathPositions.Clear();
            _lastLoggedTier=-1;
            if (_player != null) LastKnownPlayerPos=_player.position;
        }

        
        public string QaTel()
        {            Vector3 d=LastDeathPosition;
            return string.Format(CultureInfo.InvariantCulture,
                "mem={0} deaths={1} lastdeath={2:0.#},{3:0.#}",MemoryTier,DeathCount,d.x,d.z);
        }

        int ComputeTier(int deaths)
        {
            if (TierThresholds == null || TierThresholds.Length == 0) return 0;
            int tier = 0;
            int max = Mathf.Min(TierThresholds.Length - 1,4);
            for (int i = 0; i <= max; i++)
            {
                if (deaths >= TierThresholds[i]) tier = i;
            }
            return tier;
        }

        void CachePlayer()
        {
            if (_player != null) return;
            GameObject go = null;
            try { go = GameObject.FindGameObjectWithTag("Player"); } catch {  }
            if (go == null) go = GameObject.Find("Player");
            if (go != null) _player = go.transform;
        }

        void TrackPlayer()
        {
            if (_player == null) return;
            if ((_player.position - transform.position).sqrMagnitude > TrackRange * TrackRange) return;
            LastKnownPlayerPos = _player.position;
        }

        
        void OnDeathRegistered(Vector3 deathPos, string cause) => NotifyDeath(deathPos);

        void TrySubscribeDeathEvent()
        {
            try
            {
                _deathEvent = typeof(GameEvents).GetEvent("DeathRegistered",
                    BindingFlags.Public | BindingFlags.Static);
                if (_deathEvent == null) return;
                _deathHandler=Delegate.CreateDelegate(_deathEvent.EventHandlerType,
                    this, nameof(OnDeathRegistered), false);
                if (_deathHandler != null) _deathEvent.AddEventHandler(null, _deathHandler);
            }
            catch (Exception e)
            {
                Debug.LogWarning("TEL MEMORY optional GameEvents.DeathRegistered not usable: " + e.Message);
            }
        }
    }
}
