using System.Collections;
using UnityEngine;
using NightShift.Audio;
using NightShift.Core;
using NightShift.Objectives;
using NightShift.UI;

namespace NightShift.Scares
{

public class PhoneCallScare : JumpscareTrigger
    {
        [Header("Phone config")]
        [Tooltip("The 'answer the phone' objective id; the call plays once it is completed.")]
        public string AnswerObjectiveId = "phone";
        public float RingEvery = 3.2f;
        public float PauseSeconds = 2.6f;
        public string RingClipId = "phone_ring";
        public string CallerIdText="INCOMING CALL  —  FROM: YOU";

        bool _running;
        float _ringTimer;

        protected override void Activate() { }

        void Update()
        {
            if (_running || !IsPlaying || !CanFire()) return;
            var om = ObjectiveManager.Instance;
            if (om == null) return;

            if (om.IsDone(AnswerObjectiveId))
            {
                _running = true;
                StartCoroutine(RunSequence());
                return;
            }
            
            if (om.Current != null && om.Current.Id == AnswerObjectiveId)
            {
                _ringTimer -= Time.deltaTime;
                if (_ringTimer <= 0f)
                {
                    _ringTimer = RingEvery;
                    AudioDirector.Instance?.PlayAt(RingClipId,transform.position,1f);
                }
            }
        }

        IEnumerator RunSequence()
        {
            yield return new WaitForSeconds(0.9f);
            AudioDirector.Instance?.PlayVoice("voice_im_behind_you");
            UIManager.Instance?.ShowSubtitle("\"I'm standing right behind you.\"", 2.4f);
            yield return new WaitForSeconds(PauseSeconds);

            
            AudioDirector.Instance?.PlayAt(RingClipId, transform.position, 1f);
            yield return new WaitForSeconds(1.5f);
            StingerId = "sting_scare_soft";
            StingerVolume=0.5f;
            FireScare();
            UIManager.Instance?.ShowSubtitle(CallerIdText, 4f, true);
        }
    }
}
