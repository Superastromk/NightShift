using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightShift.Monster
{

[DefaultExecutionOrder(100)]
    public sealed class HumanRigBinder : MonoBehaviour
    {
        public FigureAnimator Source;
        public GameObject Visual;
        public float JawLimit=28f;

        sealed class Link
        {
            public Transform Proxy, Target;
            public Quaternion ProxyBindWorld, TargetNeutralWorld;
            public Vector3 TargetBindLocalPosition, TargetBindLocalScale;
            public bool IsPelvis, IsForearm;
        }

        readonly List<Link> _links=new List<Link>();
        bool _ready;
        Vector3 _proxyHipsRest, _targetHipsRest;
        Transform _targetHips;
        float _elbowRestL=1f, _elbowRestR=1f;
        int _boneCount;
        Transform _targetJaw, _headLink;
        Quaternion _jawNeutral, _headNeutral;
        Vector3 _jawHingeWorld;
        Quaternion _proxyHeadBind;

        public void Configure(FigureAnimator source, GameObject visual)
        {
            Source=source;
            Visual=visual;
            _ready=false;
        }

        void Start() => Initialize();

        void Initialize()
        {
            if (_ready) return;
            if (Source == null) Source=GetComponent<FigureAnimator>();
            if (Source == null || Visual == null || Source.Hips == null) return;
            foreach (var animator in Visual.GetComponentsInChildren<Animator>(true)) animator.enabled=false;
            var bones=Visual.GetComponentsInChildren<Transform>(true);
            _boneCount=bones.Length;
            _links.Clear();
            _targetHips=Find(bones, "pelvis");
            if (_targetHips == null) { Debug.LogError("HUMANRIG missing pelvis on " + Visual.name); return; }
            _proxyHipsRest = Source.Hips.localPosition;
            _targetHipsRest = _targetHips.localPosition;
            _elbowRestL = Source.ElbowL != null ? Source.ElbowL.localScale.y : 1f;
            _elbowRestR = Source.ElbowR != null ? Source.ElbowR.localScale.y : 1f;

            Add(bones, Source.Hips, _targetHips, Source.Spine, Find(bones, "spine_01"), true, true);
            Add(bones, Source.Spine, Find(bones, "spine_01"), Source.Head, Find(bones, "neck_01"),true);
            Add(bones,Source.Head,Find(bones, "head"), null, null, false);
            Add(bones, Source.ShoulderL, Find(bones, "upperarm_l"), Source.ElbowL, Find(bones, "lowerarm_l"), true);
            Add(bones, Source.ElbowL, Find(bones, "lowerarm_l"),null,Find(bones, "hand_l"), true, false, true);
            Add(bones, Source.ShoulderR, Find(bones, "upperarm_r"), Source.ElbowR, Find(bones, "lowerarm_r"), true);
            Add(bones, Source.ElbowR, Find(bones, "lowerarm_r"),null,Find(bones, "hand_r"), true, false, true);
            Add(bones, Source.HipL, Find(bones, "thigh_l"), Source.KneeL, Find(bones, "calf_l"), true);
            Add(bones, Source.KneeL, Find(bones, "calf_l"),null,Find(bones, "foot_l"), true);
            Add(bones, Source.KneeL, Find(bones, "foot_l"), null, null, false);
            Add(bones, Source.HipR, Find(bones, "thigh_r"), Source.KneeR, Find(bones, "calf_r"),true);
            Add(bones,Source.KneeR,Find(bones, "calf_r"), null, Find(bones, "foot_r"), true);
            Add(bones, Source.KneeR, Find(bones, "foot_r"), null, null, false);

            _targetJaw=Find(bones, "jaw");
            if (Source.Jaw != null && _targetJaw != null)
            {
                _headLink = _links[2].Target;
                _proxyHeadBind = Source.Head.rotation;
                _headNeutral = _headLink.rotation;
                _jawNeutral = _targetJaw.rotation;
                
                
                _jawHingeWorld = _headNeutral * Quaternion.Inverse(Source.Head.rotation) *
                    Source.Head.TransformDirection(Vector3.right);
            }
            else _targetJaw = null;

            _ready = _links.Count == 13;
            if (!_ready) { Debug.LogError("HUMANRIG incomplete on " + Visual.name + " links=" + _links.Count); return; }
            foreach (var r in Visual.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
            }
            Debug.Log("HUMANRIG bound " + Visual.name + " links=" + _links.Count + " transforms=" + _boneCount + " jaw=" + (_targetJaw != null ? 1 : 0));
        }

        static Transform Find(Transform[] bones, string name)
        {
            foreach (var bone in bones)
                if (string.Equals(bone.name, name, StringComparison.OrdinalIgnoreCase)) return bone;
            return null;
        }

        void Add(Transform[] bones, Transform proxy, Transform target, Transform proxyChild, Transform targetChild,
            bool align, bool pelvis = false, bool forearm = false)
        {
            if (proxy == null || target == null) return;
            Quaternion neutral = target.rotation;
            if (align && proxyChild != null && targetChild != null)
            {
                Vector3 p = Source.transform.InverseTransformDirection(proxyChild.position - proxy.position);
                Vector3 t = Visual.transform.InverseTransformDirection(targetChild.position - target.position);
                if (p.sqrMagnitude > 0.000001f && t.sqrMagnitude > 0.000001f)
                {
                    Quaternion correction = Quaternion.FromToRotation(t.normalized, p.normalized);
                    neutral = Source.transform.rotation * correction * Quaternion.Inverse(Visual.transform.rotation) * target.rotation;
                }
            }
            _links.Add(new Link
            {
                Proxy = proxy, Target = target, ProxyBindWorld = proxy.rotation, TargetNeutralWorld = neutral,
                TargetBindLocalPosition = target.localPosition, TargetBindLocalScale = target.localScale,
                IsPelvis = pelvis, IsForearm = forearm
            });
        }

        void LateUpdate()
        {
            if (!_ready)
            { Initialize(); if (!_ready) return; }
            float stretchL = Source.ElbowL != null ? Source.ElbowL.localScale.y / Mathf.Max(0.001f, _elbowRestL) : 1f;
            float stretchR = Source.ElbowR != null ? Source.ElbowR.localScale.y / Mathf.Max(0.001f, _elbowRestR) : 1f;
            foreach (var link in _links)
            {
                link.Target.rotation = link.Proxy.rotation * Quaternion.Inverse(link.ProxyBindWorld) * link.TargetNeutralWorld;
                link.Target.localScale = link.TargetBindLocalScale;
                if (link.IsForearm)
                {
                    float stretch = link.Target.name.EndsWith("_l", StringComparison.Ordinal) ? stretchL : stretchR;
                    link.Target.localScale=new Vector3(link.TargetBindLocalScale.x,
                        link.TargetBindLocalScale.y * stretch, link.TargetBindLocalScale.z);
                }
                if (link.IsPelvis)
                {
                    Vector3 motion=Source.transform.TransformVector(Source.Hips.localPosition - _proxyHipsRest);
                    link.Target.localPosition=_targetHipsRest + link.Target.parent.InverseTransformVector(motion);
                }
                else link.Target.localPosition=link.TargetBindLocalPosition;
            }
            if (_targetJaw != null)
            {
                Quaternion headMotion=Source.Head.rotation * Quaternion.Inverse(_proxyHeadBind);
                _targetJaw.rotation=headMotion * Quaternion.AngleAxis(JawAngle(), _jawHingeWorld) * _jawNeutral;
            }
        }

        float JawAngle() => _targetJaw != null?Mathf.Clamp(Mathf.DeltaAngle(0f,Source.Jaw.localEulerAngles.x),0f,JawLimit):0f;

        public string QaTel() => "humanrig=" + (_ready ? 1 : 0) + " links=" + _links.Count + " bones=" + _boneCount +
            " jaw=" + (_targetJaw != null ? 1 : 0) + " jawangle=" + JawAngle().ToString("F1") + " jawlimit=" + JawLimit.ToString("F1");
    }
}
