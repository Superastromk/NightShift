using UnityEngine;

namespace NightShift.Rewind
{

public class TimelineRecorder : MonoBehaviour
    {
        [Header("Config")]
        public float SampleRate = 15f;
        public float HistorySeconds = 8f;

        
        public struct Sample
        {
            public Vector3 pos;
            public Quaternion rot;
        }

        Sample[] _buf;
        int _head;   
        int _count;  
        float _timer;

        void Awake()
        {
            int cap = Mathf.Max(2, Mathf.CeilToInt(HistorySeconds * SampleRate) + 2);
            _buf = new Sample[cap];
            _head = 0;
            _count = 0;
            _timer = 0f;
        }

        void Update()
        {

var rc = RewindController.Instance;
            if (rc != null && rc.IsRewinding) return;
            var gm = NightShift.Core.GameManager.Instance;
            if (gm != null && gm.State != NightShift.Core.GameState.Playing) return;

            float step = 1f / Mathf.Max(0.01f, SampleRate);
            _timer += Time.deltaTime;
            
            int guard = 4;
            while (_timer >= step && guard-- > 0)
            {
                _timer -= step;
                PushSample();
            }
        }

        
        public void PushSample()
        {
            if (_buf == null) Awake();
            _buf[_head] = new Sample { pos = transform.position, rot = transform.rotation };
            _head = (_head + 1) % _buf.Length;
            if (_count < _buf.Length) _count++;
        }

        
        int SlotFor(int chronologicalIndex)
        {
            int raw = _head - _count + chronologicalIndex;
            raw %= _buf.Length;
            if (raw < 0) raw += _buf.Length;
            return raw;
        }

public Sample GetSampleAgo(float secondsAgo)
        {
            if (_buf == null || _count == 0)
                return new Sample { pos = transform.position, rot = transform.rotation };

            float maxAgo = (_count - 1) / Mathf.Max(0.01f, SampleRate);
            float t = Mathf.Clamp(secondsAgo, 0f, maxAgo);
            float fIdx = t * SampleRate;
            int iNew = _count - 1;
            int back = Mathf.Min(Mathf.FloorToInt(fIdx), _count - 1);
            float frac = fIdx - Mathf.Floor(fIdx);

            int i0 = iNew - back;
            int i1 = Mathf.Max(i0 - 1, 0); 

            Sample a = _buf[SlotFor(i0)];
            Sample b = _buf[SlotFor(i1)];
            return new Sample
            {
                pos = Vector3.Lerp(a.pos, b.pos, frac),
                rot = Quaternion.Slerp(a.rot, b.rot, frac)
            };
        }

        
        public System.Collections.Generic.IReadOnlyList<Sample> GetHistory()
        {
            var list = new System.Collections.Generic.List<Sample>(_count);
            for (int i = 0; i < _count; i++) list.Add(_buf[SlotFor(i)]);
            return list;
        }

        
        public void Clear()
        {
            _count = 0;
            _head = 0;
            _timer = 0f;
        }

        
        public int Count => _count;

        
        public float Span => (_count - 1) / Mathf.Max(0.01f, SampleRate);

        
        public string QaTel() => $"hist={_count} t={Span:F1}s";
    }
}
