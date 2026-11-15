using UnityEngine;
using UnityEngine.AI;

namespace NightShift.World
{

[DefaultExecutionOrder(-500)]
    public class NavMeshLoader : MonoBehaviour
    {
        public NavMeshData Data;
        public NavMeshAgent[] EnableAfterLoad = new NavMeshAgent[0];
        NavMeshDataInstance _instance;

        void OnEnable()
        {
            if (Data != null) _instance = NavMesh.AddNavMeshData(Data);
            else Debug.LogWarning("TEL WARN NavMeshLoader: no NavMeshData assigned");
            foreach (var a in EnableAfterLoad) if (a != null) a.enabled = true;
        }

        void OnDisable()
        { _instance.Remove(); }
    }
}
