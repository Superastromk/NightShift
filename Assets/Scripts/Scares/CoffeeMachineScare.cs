using System.Collections;
using UnityEngine;
using NightShift.Core;
using NightShift.Monster;
using NightShift.Objectives;
using NightShift.Player;
using NightShift.World;

namespace NightShift.Scares
{

public class CoffeeMachineScare : JumpscareTrigger
    {
        [Header("Coffee config")]
        [Tooltip("Fires when this objective completes.")]
        public string TriggerObjectiveId = "coffee";
        [Tooltip("OfficeLight group of the break room.")]
        public string LightGroup="break";
        [Tooltip("Where the monster appears (far side of the room, away from the door).")]
        public Transform RevealPoint;
        public float DispenseSeconds = 3.2f;
        public float BlackoutSeconds = 1.3f;
        public string DispenseClipId = "coffee_pour";

        protected override void Activate()
        { }

        bool _running;

        void Update()
        {
            if (_running || !IsPlaying || !CanFire()) return;
            var om = ObjectiveManager.Instance;
            if (om == null || !om.IsDone(TriggerObjectiveId)) return;
            _running = true;
            StartCoroutine(RunSequence());
        }

        IEnumerator RunSequence()
        {
            yield return new WaitForSeconds(0.8f);
            GameEvents.RaiseStingerRequested(DispenseClipId, 0.8f);
            OfficeLight.StutterAll(DispenseSeconds, LightGroup);
            UI.UIManager.Instance?.ShowSubtitle("[The coffee machine won't stop pouring.]", 2.5f);
            yield return new WaitForSeconds(DispenseSeconds);

            OfficeLight.PowerAll(false, LightGroup);
            GameEvents.RaiseStingerRequested("power_down",0.6f);
            yield return new WaitForSeconds(BlackoutSeconds);

            MonsterAI monster = MonsterAI.Instance;
            if (monster != null && Player != null)
            {
                Vector3 pos = RevealPoint != null ? RevealPoint.position : transform.position;
                monster.Teleport(pos);
                Vector3 look = Player.position - monster.transform.position; look.y = 0f;
                if (look.sqrMagnitude > 0.01f) monster.transform.rotation = Quaternion.LookRotation(look);
                monster.ScreamRequested = true;
            }
            StingerId = "sting_scare_hard";
            StingerVolume = 1f;
            FireScare();
            var fpc = FirstPersonController.Instance;
            if (fpc != null) fpc.Shake = 0.7f;
            OfficeLight.PowerAll(true, LightGroup);
            OfficeLight.StutterAll(1.2f, LightGroup);
            UI.UIManager.Instance?.Hint("<b>RUN.</b>  Doors slow it down.", 5f);
        }
    }
}
