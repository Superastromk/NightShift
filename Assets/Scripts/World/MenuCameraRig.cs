using UnityEngine;
using NightShift.Core;

namespace NightShift.World
{
    
    public class MenuCameraRig : MonoBehaviour
    {
        public Vector3 SwayDegrees = new Vector3(1.2f, 3.5f, 0f);
        public float SwaySpeed = 0.13f;
        public float Dolly = 0.5f;
        Quaternion _rot; Vector3 _pos;

        void Awake()
        { _rot = transform.localRotation; _pos = transform.localPosition; }

        void Update()
        {
            float t = Time.unscaledTime * SwaySpeed;
            transform.localRotation = _rot * Quaternion.Euler(Mathf.Sin(t * 1.3f) * SwayDegrees.x, Mathf.Sin(t) * SwayDegrees.y, 0f);
            transform.localPosition = _pos + transform.forward * (Mathf.Sin(t * 0.7f) * Dolly);
        }
    }
}
