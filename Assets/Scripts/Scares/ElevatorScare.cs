using System.Collections;
using UnityEngine;
using NightShift.Audio;
using NightShift.Core;
using NightShift.Monster;
using NightShift.Player;
using NightShift.UI;

namespace NightShift.Scares
{

public class ElevatorScare : JumpscareTrigger
    {
        [Header("Elevator refs")]
        public Transform DoorLeft;
        public Transform DoorRight;
        public FigureAnimator Intern;
        public Light CabLight;

        [Header("Elevator config")]
        public float TriggerRadius = 5f;
        public float DingPauseSeconds = 1.4f;
        public float DoorSeconds = 1.1f;
        public float NormalBeatSeconds = 1.1f;
        public string CalmLine = "\"Sorry about that. First day.\"";
        public float ScreamSeconds = 1.7f;
        public float DoorSlide = 0.52f;
        public float LungeDistance = 0.75f;

        bool _running;
        Vector3 _l0, _r0, _internHome;

        protected override void Activate() { }

        void Start()
        {
            if (DoorLeft != null) _l0 = DoorLeft.position;
            if (DoorRight != null) _r0 = DoorRight.position;
            if (Intern != null) { _internHome = Intern.transform.position; Intern.ForcePose = FigureAnimator.Pose.Idle; }
        }

        void Update()
        {
            
            if (NightShift.Progression.ShiftDirector.Instance != null && NightShift.Progression.ShiftDirector.Instance.Act >= 3)
            {
                StopAllCoroutines();
                enabled = false;
                return;
            }
            if (_running || !IsPlaying || !CanFire()) return;
            if (DistToPlayer() > TriggerRadius) return;
            _running = true;
            StartCoroutine(RunSequence());
        }

        IEnumerator RunSequence()
        {
            GameEvents.RaiseStingerRequested("elevator_ding", 0.9f);
            yield return new WaitForSeconds(DingPauseSeconds);
            yield return Slide(0f, 1f);

            
            yield return new WaitForSeconds(NormalBeatSeconds);

            
            StingerId = "sting_scare_hard";
            StingerVolume=1f;
            FireScare();
            GameEvents.RaiseStingerRequested("intern_scream",1f);
            var fpc = FirstPersonController.Instance;
            if (fpc != null) fpc.Shake = 0.9f;
            if (Intern != null)
            {
                Intern.ForcePose = FigureAnimator.Pose.Scream;
                Vector3 dir = Player != null ? Player.position - _internHome : Intern.transform.forward;
                dir.y = 0f;
                dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Intern.transform.forward;
                Intern.transform.rotation = Quaternion.LookRotation(dir);
                float t = 0f;
                while (t < 0.12f) { t += Time.deltaTime; Intern.transform.position = _internHome + dir * LungeDistance * Mathf.Clamp01(t / 0.12f); yield return null; }
            }
            yield return new WaitForSeconds(ScreamSeconds);

            
            if (Intern != null)
            {
                Intern.ForcePose = FigureAnimator.Pose.Idle;
                float t = 0f; Vector3 from = Intern.transform.position;
                while (t < 0.5f) { t += Time.deltaTime; Intern.transform.position = Vector3.Lerp(from,_internHome,t / 0.5f); yield return null; }
            }
            yield return new WaitForSeconds(0.7f);
            AudioDirector.Instance?.PlayVoice("voice_first_day");
            UIManager.Instance?.ShowSubtitle(CalmLine, 3f);
            yield return new WaitForSeconds(3.2f);
            while (DoorwayOccupied()) yield return null;
            yield return Slide(1f, 0f);
            if (Intern != null) Intern.gameObject.SetActive(false);
            if (CabLight != null) CabLight.enabled = false;
        }

        bool DoorwayOccupied()
        {
            if (Player == null || DoorLeft == null || DoorRight == null) return false;
            Vector3 doorway = (_l0 + _r0) * 0.5f;
            return Player.position.x > doorway.x - 0.65f && Player.position.x < doorway.x + 2f &&
                Mathf.Abs(Player.position.z - doorway.z) < 1.05f;
        }

        IEnumerator Slide(float from, float to)
        {
            GameEvents.RaiseStingerRequested("door_open", 0.5f);
            Vector3 leftStart = DoorLeft != null ? DoorLeft.position : _l0;
            Vector3 rightStart = DoorRight != null ? DoorRight.position : _r0;
            Vector3 leftEnd = _l0 + Vector3.back * (to * DoorSlide);
            Vector3 rightEnd = _r0 + Vector3.forward * (to * DoorSlide);
            float elapsed = 0f;
            while (elapsed < DoorSeconds)
            {
                if (to < from && DoorwayOccupied())
                {
                    
                    yield return Slide(0f, 1f);
                    while (DoorwayOccupied()) yield return null;
                    leftStart = DoorLeft != null ? DoorLeft.position : _l0;
                    rightStart = DoorRight != null ? DoorRight.position : _r0;
                    elapsed = 0f;
                }
                elapsed += Time.deltaTime;
                float amount = Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(0.01f, DoorSeconds));
                if (DoorLeft != null) DoorLeft.position = Vector3.Lerp(leftStart, leftEnd, amount);
                if (DoorRight != null) DoorRight.position = Vector3.Lerp(rightStart, rightEnd, amount);
                yield return null;
            }
        }
    }
}
