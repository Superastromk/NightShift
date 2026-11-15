using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using NightShift.Core;
using NightShift.Monster;
using NightShift.Objectives;
using NightShift.Player;
using NightShift.Rewind;
using NightShift.World;

namespace NightShift.Scares
{

public class FirstKillDirector : MonoBehaviour
    {
        [Tooltip("Objective id that arms the first kill.")]
        public string AfterObjectiveId = "deliver_report";
        public float RevealDelay=2.4f;
        public float BlackoutSeconds=0.9f;
        public float RevealBeatSeconds=2.0f;
        public float RevealDistance=3.2f;

        bool _fired;

        void Update()
        {
            if (_fired) return;
            var gm=GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;
            var om=ObjectiveManager.Instance;
            if (om == null || !om.IsDone(AfterObjectiveId)) return;
            _fired=true;
            if (gm.DeathCount > 0) return; 
            StartCoroutine(DirectFirstKill());
        }

        IEnumerator DirectFirstKill()
        {
            yield return new WaitForSeconds(RevealDelay);
            var fpc=FirstPersonController.Instance;
            var monster=MonsterAI.Instance;
            if (fpc == null || monster == null) yield break;

            
            OfficeLight.StutterAll(0.35f);
            yield return new WaitForSeconds(0.35f);
            OfficeLight.PowerAll(false);
            GameEvents.RaiseStingerRequested("power_down",0.7f);
            yield return new WaitForSeconds(BlackoutSeconds);

            
            Transform player = fpc.transform;
            Vector3 fwd = player.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            Vector3 pos = player.position + fwd.normalized * RevealDistance;
            if (NavMesh.Raycast(player.position,pos,out var hit,NavMesh.AllAreas))
                pos = Vector3.Lerp(player.position,hit.position,0.85f);
            monster.Teleport(pos);
            Vector3 look = player.position - monster.transform.position; look.y = 0f;
            if (look.sqrMagnitude > 0.01f) monster.transform.rotation = Quaternion.LookRotation(look);
            monster.ScreamRequested = true;
            OfficeLight.PowerAll(true);
            OfficeLight.StutterAll(RevealBeatSeconds);
            GameEvents.RaiseStingerRequested("sting_scare_hard", 1f);
            fpc.Shake = 0.8f;
            Debug.Log("TEL FIRSTKILL reveal at " + pos.x.ToString("0.0") + "," + pos.z.ToString("0.0"));

            yield return new WaitForSeconds(RevealBeatSeconds);
            DeathManager.Instance?.KillFrom("firstkill", monster.transform.position);
        }
    }
}
