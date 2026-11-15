using UnityEngine;
using NightShift.Core;

namespace NightShift.Rewind
{

public class RewindableObject : MonoBehaviour
    {
        [Header("Config")]
        public bool RecordPosition=true;
        public bool RecordRotation=true;
        public float RecordRate=12f;
        public float HistorySeconds=8f;
        [Tooltip("Record in local space (hinged doors).")]
        public bool Local = false;

        public struct Snapshot { public Vector3 pos; public Quaternion rot; }

        Snapshot[] _buf;
        int _head,_count;
        float _timer;
        Rigidbody _rb;
        bool _wasKinematic;
        bool _scrubbing;

        void Awake()
        {
            int cap = Mathf.Max(2,Mathf.CeilToInt(HistorySeconds * RecordRate) + 2);
            _buf = new Snapshot[cap];
            _rb = GetComponent<Rigidbody>();
        }

        void Update()
        {
            if (_scrubbing) return;
            var gm = GameManager.Instance;
            if (gm != null && gm.State != GameState.Playing) return;
            float step = 1f / Mathf.Max(0.01f,RecordRate);
            _timer += Time.deltaTime;
            int guard = 3;
            while (_timer >= step && guard-- > 0) { _timer -= step; RecordSnapshot(); }
        }

        public void RecordSnapshot()
        {
            if (_buf == null) Awake();
            _buf[_head] = Local
                ? new Snapshot { pos = transform.localPosition,rot = transform.localRotation }
                : new Snapshot { pos = transform.position,rot = transform.rotation };
            _head = (_head + 1) % _buf.Length;
            if (_count < _buf.Length) _count++;
        }

        int SlotFor(int chronologicalIndex)
        {
            int raw = (_head - _count + chronologicalIndex) % _buf.Length;
            if (raw < 0) raw += _buf.Length;
            return raw;
        }

        
        public Snapshot GetSnapshotAgo(float secondsAgo)
        {
            if (_buf == null || _count == 0)
                return Local
                    ? new Snapshot { pos = transform.localPosition,rot = transform.localRotation }
                    : new Snapshot { pos = transform.position,rot = transform.rotation };
            float maxAgo = (_count - 1) / Mathf.Max(0.01f,RecordRate);
            float t = Mathf.Clamp(secondsAgo,0f,maxAgo);
            float fIdx = t * RecordRate;
            int iNew = _count - 1;
            int back = Mathf.Min(Mathf.FloorToInt(fIdx),_count - 1);
            float frac = fIdx - Mathf.Floor(fIdx);
            int i0 = iNew - back;
            int i1 = Mathf.Max(i0 - 1,0);
            Snapshot a = _buf[SlotFor(i0)],b = _buf[SlotFor(i1)];
            return new Snapshot { pos = Vector3.Lerp(a.pos,b.pos,frac),rot = Quaternion.Slerp(a.rot,b.rot,frac) };
        }

        
        public void BeginRewind()
        {
            if (_scrubbing) return;
            _scrubbing = true;
            if (_rb != null) { _wasKinematic = _rb.isKinematic; _rb.isKinematic = true; }
        }

        
        public void ApplySnapshotAgo(float secondsAgo)
        {
            Snapshot s = GetSnapshotAgo(secondsAgo);
            if (Local)
            {
                if (RecordPosition) transform.localPosition = s.pos;
                if (RecordRotation) transform.localRotation = s.rot;
            }
            else
            {
                if (RecordPosition) transform.position = s.pos;
                if (RecordRotation) transform.rotation = s.rot;
            }
        }

        
        public void EndRewind()
        {
            if (!_scrubbing) return;
            _scrubbing = false;
            if (_rb != null)
            {
                _rb.isKinematic = _wasKinematic;
                if (!_rb.isKinematic) { _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; _rb.Sleep(); }
            }
            ClearHistory();
        }

        public void ClearHistory() { _count = 0; _head = 0; _timer = 0f; }
    }
}
