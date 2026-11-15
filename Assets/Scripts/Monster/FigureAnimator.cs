using UnityEngine;
using NightShift.Core;

namespace NightShift.Monster
{

public class FigureAnimator : MonoBehaviour
    {
        public enum Pose { Auto, Idle, Scream, Loom, Reach, Still }

        [Header("Joints (limbs hang along -Y from each joint)")]
        public Transform Hips;
        public Transform Spine, Head, Jaw;
        public Transform ShoulderL, ShoulderR, ElbowL, ElbowR;
        public Transform HipL, HipR, KneeL, KneeR;

        [Header("Drive")]
        [Tooltip("Optional brain for state + speed. Without one, speed comes from the transform's motion.")]
        public MonsterAI Brain;
        [Tooltip("Metres covered by one full stride (two steps) at walking pace.")]
        public float StrideLength = 1.35f;
        public float LegSwing = 21f;
        public float ArmSwing = 22f;
        [Tooltip("Above this speed (m/s) the gait blends into a run.")]
        public float RunSpeed = 2.6f;
        public float HeadTrack = 1f;
        public float MaxHeadYaw = 55f;
        public Pose ForcePose = Pose.Auto;
        public bool Revealed;
        [Tooltip("Stop-motion frame rate while revealed (0 = smooth).")]
        public float RevealedFps=0f;
        public float RevealedArmStretch=1.05f;

        float _phase, _speed, _reveal, _time, _quantClock;
        float _twitch, _twitchUntil;
        float _yawRate, _lastYaw;
        float _lookYaw, _lookPitch;
        Vector3 _prev;
        Transform _look;
        Vector3 _hipsRest, _elbowScaleL=Vector3.one, _elbowScaleR=Vector3.one;
        float _thigh=0.44f, _shin=0.51f;

        
        float _armPitchL, _armPitchR, _armSpread=6f, _elbowBend=8f, _lean, _headPitch, _headRoll, _jaw;

        void Awake()
        {
            _prev=transform.position;
            _lastYaw=transform.eulerAngles.y;
            if (Hips != null) _hipsRest=Hips.localPosition;
            if (ElbowL != null) _elbowScaleL=ElbowL.localScale;
            if (ElbowR != null) _elbowScaleR=ElbowR.localScale;
            if (KneeL != null) _thigh=Mathf.Abs(KneeL.localPosition.y);
            if (Hips != null && KneeL != null && HipL != null)
                _shin=_hipsRest.y - Mathf.Abs(HipL.localPosition.y) - _thigh;   
            if (Brain == null) Brain=GetComponentInParent<MonsterAI>();
            _time=Random.value * 10f;
        }

        void LateUpdate()
        {
            float dt=Time.deltaTime;
            if (dt <= 0f) return;

            
            float animDt=dt;
            if (Revealed && RevealedFps > 0f)
            {
                _quantClock += dt;
                if (_quantClock < 1f / RevealedFps) return;
                animDt=_quantClock;
                _quantClock=0f;
            }

            
            Vector3 delta=transform.position - _prev; delta.y=0f;
            _prev=transform.position;
            float measured=delta.magnitude / Mathf.Max(animDt, 0.0001f);
            if (measured > 12f) measured=0f;   
            float speed=Brain != null ? Mathf.Min(Brain.PlanarSpeed, measured + 0.5f) : measured;
            _speed=Mathf.Lerp(_speed, speed, 1f - Mathf.Exp(-8f * animDt));
            float yaw=transform.eulerAngles.y;
            _yawRate=Mathf.Lerp(_yawRate, Mathf.DeltaAngle(_lastYaw, yaw) / animDt, 1f - Mathf.Exp(-8f * animDt));
            _lastYaw=yaw;
            _reveal=Mathf.MoveTowards(_reveal, Revealed ? 1f : 0f, animDt * 1.5f);
            _time += animDt;

            Pose pose=ResolvePose();

            
            float gaitSpeed=Mathf.Max(_speed, Mathf.Abs(_yawRate) * 0.006f);
            float walk=pose == Pose.Still ? 0f : Mathf.Clamp01(gaitSpeed / 1.1f);
            float run=Mathf.Clamp01((_speed - RunSpeed * 0.8f) / (RunSpeed * 0.6f));
            
            
            float reach=Mathf.Sqrt(walk);
            float stride=StrideLength * Mathf.Max(0.35f, reach) * Mathf.Lerp(1f, 1.5f, run);
            _phase += gaitSpeed * animDt / stride * Mathf.PI * 2f;

            
            float tArmL=0f, tArmR=0f, tSpread=6f, tElbow=8f, tLean=0f, tHeadPitch=0f, tHeadRoll=0f, tJaw=0f;
            switch (pose)
            {
                case Pose.Scream: tArmL=tArmR=-150f; tSpread=34f; tElbow=20f; tLean=-10f; tHeadPitch=-20f; tJaw=1f; break;
                case Pose.Loom: tArmL=tArmR=-14f; tSpread=8f; tElbow=22f; tLean=16f; tHeadPitch=12f; tHeadRoll=24f; tJaw=0.55f; break;
                case Pose.Reach: tArmL=-84f; tArmR=-72f; tSpread=7f; tElbow=12f; tLean=14f; tHeadPitch=-6f; tJaw=0.7f; break;
            }
            tLean += walk * 3f + run * 9f;
            tElbow += walk * 10f + run * 55f;
            tHeadRoll += _reveal * 8f;
            tJaw=Mathf.Max(tJaw, _reveal * 0.35f);

            float k=1f - Mathf.Exp(-(pose == Pose.Scream ? 16f : 7f) * animDt);
            _armPitchL=Mathf.Lerp(_armPitchL, tArmL, k);
            _armPitchR=Mathf.Lerp(_armPitchR, tArmR, k);
            _armSpread=Mathf.Lerp(_armSpread, tSpread, k);
            _elbowBend=Mathf.Lerp(_elbowBend, tElbow, k);
            _lean=Mathf.Lerp(_lean, tLean, k);
            _headPitch=Mathf.Lerp(_headPitch, tHeadPitch, k);
            _headRoll=Mathf.Lerp(_headRoll, tHeadRoll, k);
            _jaw=Mathf.Lerp(_jaw, tJaw, 1f - Mathf.Exp(-12f * animDt));

            
            float legAmp=LegSwing * reach * Mathf.Lerp(1f, 1.45f, run);
            float thighL=Mathf.Sin(_phase) * legAmp, thighR=Mathf.Sin(_phase + Mathf.PI) * legAmp;
            float kneeL=KneeFlex(_phase, walk, run), kneeR=KneeFlex(_phase + Mathf.PI, walk, run);

            
            float breathe=Mathf.Sin(_time * 1.6f);
            float idle=1f - walk;
            float shift=(Mathf.PerlinNoise(_time * 0.15f, 1.7f) - 0.5f) * 2f * idle;

            
            float reachL=LegReach(thighL, kneeL), reachR=LegReach(thighR, kneeR);
            float drop=(_thigh + _shin) - Mathf.Max(reachL, reachR);
            float sway=Mathf.Cos(_phase) * walk;   
            float pelvisYaw=Mathf.Sin(_phase) * 7f * walk, spineYaw=-Mathf.Sin(_phase) * 11f * walk;   
            if (Hips != null)
            {
                Hips.localPosition=_hipsRest + new Vector3(sway * 0.025f + shift * 0.02f, -drop + breathe * 0.002f, 0f);
                Hips.localRotation=Quaternion.Euler(0f, pelvisYaw, -sway * 4f - shift * 2.5f);
            }
            if (HipL != null) HipL.localRotation=Quaternion.Euler(-thighL, 0f, shift * 2.5f);
            if (HipR != null) HipR.localRotation=Quaternion.Euler(-thighR, 0f, shift * 2.5f);
            if (KneeL != null) KneeL.localRotation=Quaternion.Euler(kneeL, 0f, 0f);
            if (KneeR != null) KneeR.localRotation=Quaternion.Euler(kneeR, 0f, 0f);

            
            float shake=pose == Pose.Scream ? Mathf.Sin(Time.time * 55f) * 2f : 0f;
            if (Spine != null)
                Spine.localRotation=Quaternion.Euler(_lean + breathe * 1.2f * idle + shake, spineYaw, sway * 3f + shift * 3f);

            
            float armAmp=ArmSwing * walk * (pose == Pose.Idle ? 1f : 0.2f) * Mathf.Lerp(1f, 2.1f, run);
            float swingL=Mathf.Sin(_phase + Mathf.PI) * armAmp, swingR=Mathf.Sin(_phase) * armAmp;
            if (Revealed && Time.time > _twitchUntil && Random.value < animDt * 0.4f) { _twitch=Random.Range(-8f, 8f); _twitchUntil=Time.time + 0.16f; }
            if (Time.time > _twitchUntil) _twitch=0f;
            if (ShoulderL != null) ShoulderL.localRotation=Quaternion.Euler(_armPitchL - swingL + shake, 0f, -_armSpread - breathe * 0.6f + _twitch * 0.4f);
            if (ShoulderR != null) ShoulderR.localRotation=Quaternion.Euler(_armPitchR - swingR - shake, 0f, _armSpread + breathe * 0.6f);
            float stretch=Mathf.Lerp(1f, RevealedArmStretch, _reveal);
            if (ElbowL != null)
            {
                ElbowL.localRotation=Quaternion.Euler(-_elbowBend - Mathf.Max(0f, swingL) * 0.7f, 0f, 0f);
                ElbowL.localScale=new Vector3(_elbowScaleL.x, _elbowScaleL.y * stretch, _elbowScaleL.z);
            }
            if (ElbowR != null)
            {
                ElbowR.localRotation=Quaternion.Euler(-_elbowBend - Mathf.Max(0f, swingR) * 0.7f, 0f, 0f);
                ElbowR.localScale=new Vector3(_elbowScaleR.x, _elbowScaleR.y * stretch, _elbowScaleR.z);
            }

            
            if (Head != null)
            {
                float lookYaw=0f, lookPitch=0f;
                if (HeadTrack > 0.01f && pose != Pose.Still)
                {
                    if (_look == null) { var cam=Camera.main; if (cam != null) _look=cam.transform; }
                    if (_look != null)
                    {
                        Vector3 local=transform.InverseTransformPoint(_look.position) - new Vector3(0f, 1.65f, 0f);
                        if (local.sqrMagnitude > 0.05f && local.z > 0f)
                        {
                            lookYaw=Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -MaxHeadYaw, MaxHeadYaw) * HeadTrack;
                            lookPitch=Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -30f, 30f) * HeadTrack;
                        }
                    }
                }
                _lookYaw=Mathf.MoveTowardsAngle(_lookYaw, lookYaw, 90f * animDt);
                _lookPitch=Mathf.MoveTowardsAngle(_lookPitch, lookPitch, 60f * animDt);
                float wander=(Mathf.PerlinNoise(_time * 0.4f, 5.1f) - 0.5f) * 10f * idle;
                
                Head.localRotation=Quaternion.Euler(_headPitch + _lookPitch - _lean * 0.7f, _lookYaw + wander + _twitch - (pelvisYaw + spineYaw), _headRoll - sway * 2f + shake * 1.5f);
            }
            if (Jaw != null) Jaw.localRotation=Quaternion.Euler(_jaw * 28f, 0f, 0f);
        }

        Pose ResolvePose()
        {
            if (ForcePose != Pose.Auto) return ForcePose;
            if (Brain == null) return Pose.Idle;
            var gm=GameManager.Instance;
            if (gm != null && gm.State == GameState.Dead) return Pose.Scream;
            switch (Brain.CurrentState)
            {
                case MonsterState.Scream: return Pose.Scream;
                case MonsterState.Lunge: return Pose.Reach;
                case MonsterState.Chase: return Pose.Idle; 
                default: return Pose.Idle;
            }
        }

        
        static float KneeFlex(float phase, float walk, float run)
        {
            float swing=Mathf.Max(0f, Mathf.Sin(phase + 1.35f));
            return (4f + swing * Mathf.Lerp(48f, 95f, run)) * walk + 2f;
        }

        
        float LegReach(float thighDeg, float kneeDeg)
        {
            float a=thighDeg * Mathf.Deg2Rad, b=(thighDeg - kneeDeg) * Mathf.Deg2Rad;
            return _thigh * Mathf.Cos(a) + _shin * Mathf.Cos(b);
        }
    }
}
