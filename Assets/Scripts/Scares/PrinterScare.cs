using UnityEngine;
using NightShift.Core;
using NightShift.Objectives;

namespace NightShift.Scares
{

public class PrinterScare : JumpscareTrigger
    {
        [Header("Printer config")]
        public float StartRadius=6f;
        public float ApproachRadius=2.3f;
        public float BehindDistance=1.15f;
        public float DoneAfterSeconds=4f;
        public float ShakeAmplitude=0.012f;
        public string PrintClipId="printer";
        [Tooltip("The follow only happens after this objective is done (empty = any time).")]
        public string AfterObjectiveId = "print_report";
        [Tooltip("Child object shown when the face page prints.")]
        public GameObject FacePage;
        [Tooltip("Half extents of the copier body, for the 'is there room behind the player' test.")]
        public Vector3 BodyHalfExtents = new Vector3(0.36f,0.5f,0.34f);
        public LayerMask WorldMask;

        enum Phase { Idle,Printing,WaitingForLookAway,BehindPlayer,Returning,Done }
        Phase _phase = Phase.Idle;
        Vector3 _basePos;
        Quaternion _baseRot;
        float _timer;

        protected override void Activate() { }

        void Start()
        {
            _basePos = transform.position;
            _baseRot = transform.rotation;
            if (FacePage != null) FacePage.SetActive(false);
            if (WorldMask.value == 0) WorldMask = LayerMask.GetMask("Default", "NoNav");
        }

        void Update()
        {
            if (!IsPlaying) return;
            switch (_phase)
            {
                case Phase.Idle:
                    if (CanFire() && DistToPlayer() <= StartRadius)
                    {
                        _phase = Phase.Printing;
                        Audio.AudioDirector.Instance?.PlayAt(PrintClipId, transform.position, 0.9f);
                        _timer = 0f;
                    }
                    break;

                case Phase.Printing:
                    transform.position = _basePos + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * ShakeAmplitude;
                    _timer += Time.deltaTime;
                    if (_timer > 4.5f)
                    { _timer = 0f; Audio.AudioDirector.Instance?.PlayAt(PrintClipId, transform.position, 0.9f); }
                    if (DistToPlayer() <= ApproachRadius)
                    {
                        transform.position = _basePos; 
                        _phase = Phase.WaitingForLookAway;
                    }
                    break;

                case Phase.WaitingForLookAway:
                {
                    if (!CanFire()) break;
                    var om = ObjectiveManager.Instance;
                    if (!string.IsNullOrEmpty(AfterObjectiveId) && om != null && !om.IsDone(AfterObjectiveId)) break;
                    if (DistToPlayer() > 1.6f && PlayerLookingAway() && TryTeleportBehindPlayer())
                    {
                        if (FacePage != null) FacePage.SetActive(true);
                        StingerId = "sting_scare_hard";
                        StingerVolume=0.9f;
                        FireScare();
                        Audio.AudioDirector.Instance?.PlayAt(PrintClipId, transform.position, 1f);
                        _phase=Phase.BehindPlayer;
                        _timer=DoneAfterSeconds;
                    }
                    break;
                }

                case Phase.BehindPlayer:
                    _timer -= Time.deltaTime;
                    if (_timer <= 0f) _phase=Phase.Returning;
                    break;

                case Phase.Returning:
                    
                    if (PlayerLookingAway() || DistToPlayer() > 9f)
                    {
                        transform.SetPositionAndRotation(_basePos, _baseRot);
                        _phase=Phase.Done;
                    }
                    break;
            }
        }

        bool PlayerLookingAway()
        {
            Transform p=Player;
            Camera cam=PlayerCam;
            if (p == null || cam == null) return true;
            Vector3 dirToMe=transform.position - cam.transform.position; dirToMe.y=0f;
            Vector3 fwd=cam.transform.forward; fwd.y=0f;
            if (dirToMe.sqrMagnitude < 0.01f || fwd.sqrMagnitude < 0.01f) return false;
            return Vector3.Dot(fwd.normalized, dirToMe.normalized) < -0.15f;
        }

        bool TryTeleportBehindPlayer()
        {
            Transform p=Player;
            Camera cam=PlayerCam;
            if (p == null) return false;
            Vector3 back=cam != null ? -cam.transform.forward : -p.forward;
            back.y=0f;
            if (back.sqrMagnitude < 0.001f) back=-p.forward;
            back.Normalize();
            foreach (float d in new[] { BehindDistance, BehindDistance + 0.35f, BehindDistance - 0.25f })
            {
                Vector3 pos=p.position + back * d;
                pos.y=_basePos.y;
                Quaternion rot=Quaternion.LookRotation(-back);
                Vector3 center=pos + Vector3.up * (BodyHalfExtents.y + 0.06f);
                if (Physics.CheckBox(center, BodyHalfExtents, rot, WorldMask, QueryTriggerInteraction.Ignore)) continue;
                transform.SetPositionAndRotation(pos, rot);
                return true;
            }
            return false; 
        }

        public override string QaTel() => base.QaTel() + " phase=" + _phase;
    }
}
