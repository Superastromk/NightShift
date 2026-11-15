using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using NightShift.Audio;
using NightShift.Monster;
using NightShift.UI;

namespace NightShift.Scares
{

public class StalkerEmployee : JumpscareTrigger
    {
        [Header("Stalker config")]
        public float MoveSpeed = 1.7f;
        public float TriggerRange = 1.35f;
        public float TelemetryInterval = 3f;
        public LayerMask SightMask;
        public FigureAnimator Figure;

        float _telemetryTimer;
        bool _leaving;
        NavMeshPath _path;
        int _corner;
        float _repath;
        Renderer[] _renderers;

        protected override void Activate()
        { }

        void Start()
        {
            _path = new NavMeshPath();
            _renderers = GetComponentsInChildren<Renderer>();
            if (SightMask.value == 0) SightMask = LayerMask.GetMask("Default", "NoNav");
            if (Figure == null) Figure = GetComponentInChildren<FigureAnimator>();
            if (Figure != null) Figure.ForcePose = FigureAnimator.Pose.Idle;
        }

        void Update()
        {
            if (!IsPlaying || Player == null) return;
            bool observed = IsObserved();

            if (_leaving)
            {
                if (!observed) gameObject.SetActive(false); 
                return;
            }
            if (!CanFire() || HasFired) return;

            float dist = DistToPlayer();
            _telemetryTimer -= Time.deltaTime;
            if (_telemetryTimer <= 0f)
            {
                _telemetryTimer = Mathf.Max(0.2f,TelemetryInterval);
                Debug.Log("TEL STALKER observed=" + (observed ? 1 : 0) + " dist=" + dist.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            }

            if (!observed && dist > TriggerRange) Creep();

            if (dist <= TriggerRange)
            {
                StingerId="sting_scare_soft";
                StingerVolume = 0.45f;
                FireScare();
                AudioDirector.Instance?.PlayVoice("voice_got_a_minute");
                UIManager.Instance?.ShowSubtitle("\"Got a minute? Just one.\"", 2.6f);
                if (Figure != null) Figure.ForcePose = FigureAnimator.Pose.Loom;
                Vector3 look = Player.position - transform.position; look.y = 0f;
                if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(look);
                StartCoroutine(LeaveRoutine());
            }
        }

        IEnumerator LeaveRoutine()
        {
            yield return new WaitForSeconds(2.2f);
            _leaving = true;
        }

        void Creep()
        {
            _repath -= Time.deltaTime;
            if (_repath <= 0f || _path.status == NavMeshPathStatus.PathInvalid)
            {
                _repath = 0.45f;
                Vector3 from = transform.position, to = Player.position;
                if (NavMesh.SamplePosition(from, out var a, 1.5f, NavMesh.AllAreas)) from = a.position;
                if (NavMesh.SamplePosition(to, out var b, 1.5f, NavMesh.AllAreas)) to = b.position;
                NavMesh.CalculatePath(from, to, NavMesh.AllAreas, _path);
                _corner = 1;
            }
            if (_path.corners == null || _path.corners.Length < 2) return;
            _corner = Mathf.Clamp(_corner, 1, _path.corners.Length - 1);
            Vector3 target = _path.corners[_corner];
            Vector3 dir = target - transform.position; dir.y = 0f;
            if (dir.magnitude < 0.15f && _corner < _path.corners.Length - 1)
            { _corner++; return; }
            if (dir.sqrMagnitude < 0.0004f) return;
            Vector3 step = dir.normalized * Mathf.Min(dir.magnitude, MoveSpeed * Time.deltaTime);
            transform.position += step;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir.normalized), 540f * Time.deltaTime);
        }

        public override string QaTel()
        {
            float dist = DistToPlayer();
            return "stalker dist=" + (float.IsInfinity(dist) ? "-1" : dist.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)) + " fired=" + (HasFired ? 1 : 0);
        }

        bool IsObserved()
        {
            Camera cam = PlayerCam;
            if (cam == null || !cam.enabled) return false;
            if (_renderers == null || _renderers.Length == 0) return false;
            Bounds b = _renderers[0].bounds;
            for (int i = 1; i < _renderers.Length; i++) if (_renderers[i] != null) b.Encapsulate(_renderers[i].bounds);
            if (!GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(cam), b)) return false;
            Vector3 eye = cam.transform.position;
            
            Vector3 head = transform.position + Vector3.up * 1.65f, chest = transform.position + Vector3.up * 1.1f;
            return !Physics.Linecast(eye, head, SightMask, QueryTriggerInteraction.Ignore)
                || !Physics.Linecast(eye, chest, SightMask, QueryTriggerInteraction.Ignore);
        }
    }
}
