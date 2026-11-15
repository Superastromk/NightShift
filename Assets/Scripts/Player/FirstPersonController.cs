using UnityEngine;
using UnityEngine.InputSystem;
using NightShift.Core;

namespace NightShift.Player
{

[RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        public static FirstPersonController Instance { get; private set; }

        [Header("Refs")]
        public Camera PlayerCamera;

        [Header("Movement")]
        public float WalkSpeed=2.5f;
        public float SprintSpeed=4.7f;
        public float CrouchSpeed=1.25f;
        public float Acceleration=16f;
        public float Deceleration=20f;
        public float Gravity=-20f;

        [Header("Body")]
        public float StandHeight = 1.8f;
        public float CrouchHeight = 1.0f;
        public float EyeBelowTop = 0.12f;
        public float CrouchLerpSpeed = 9f;
        public LayerMask HeadroomMask = ~0;

        [Header("Stamina")]
        public float SprintSeconds = 6.5f;
        public float RegenSeconds = 8f;
        public float RegenDelay = 1.1f;
        [Range(0f, 1f)] public float ExhaustedUntil = 0.3f;

        [Header("Look")]
        [Tooltip("Degrees per pixel of mouse delta at sensitivity 1.")]
        public float MouseSensitivity=0.11f;
        public float PitchClamp=86f;

        [Header("Head bob / FOV")]
        public float HeadBobAmplitude = 0.035f;
        public float HeadBobFrequency = 7.2f;
        public float SprintFovBoost = 6f;
        public float FovLerpSpeed = 6f;

        [Header("Footsteps / noise")]
        public float StrideWalk = 0.78f;
        public float StrideSprint = 1.15f;
        public float NoiseWalk = 4.5f;
        public float NoiseSprint = 12f;
        public float NoiseCrouch = 0f;

        [Header("Pushing props")]
        public float PushImpulse = 1.6f;
        public LayerMask PushMask = ~0;

        
        public bool InputLocked { get; set; }
        public bool IsCrouching { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsMoving => CurrentSpeed > 0.4f;
        
        public float Stamina01 { get; private set; } = 1f;
        public bool Exhausted { get; private set; }
        
        public Vector3 EyePosition => PlayerCamera != null ? PlayerCamera.transform.position : transform.position + Vector3.up * 1.6f;
        
        public float FovOffset { get; set; }
        
        public float Shake { get; set; }

        CharacterController _cc;
        Vector3 _planarVel;
        float _verticalVel;
        float _pitch;
        float _bobPhase;
        float _height;
        float _lastSprintTime = -99f;
        float _stride;
        bool _crouchToggle;
        float _lastPushNoise;

        void Awake()
        {
            Instance = this;
            _cc = GetComponent<CharacterController>();
            if (PlayerCamera == null) PlayerCamera = GetComponentInChildren<Camera>(true);
            _height = StandHeight;
            ApplyHeight(_height);
            GameSettings.Load();
            if (PlayerCamera != null) PlayerCamera.fieldOfView = GameSettings.Fov;
        }

        void OnDestroy()
        { if (Instance == this) Instance = null; }
        void OnEnable() => GameEvents.StateChanged += OnStateChanged;
        void OnDisable() => GameEvents.StateChanged -= OnStateChanged;

        void OnStateChanged(GameState s)
        {
            bool gameplay = s == GameState.Playing || s == GameState.Dead || s == GameState.Rewinding;
            Cursor.lockState = gameplay ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !gameplay;
        }

        
        public void SetLook(float yawDeg, float pitchDeg)
        {
            transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
            _pitch = Mathf.Clamp(pitchDeg, -PitchClamp, PitchClamp);
            if (PlayerCamera != null) PlayerCamera.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        public float Pitch => _pitch;

        
        public void Warp(Vector3 pos, Quaternion rot)
        {
            _cc.enabled = false;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, rot.eulerAngles.y, 0f));
            _cc.enabled = true;
            _planarVel = Vector3.zero;
            _verticalVel = 0f;
        }

        void Update()
        {
            bool playing = GameManager.Instance != null && GameManager.Instance.State == GameState.Playing;
            bool canInput = playing && !InputLocked;
            var kb = Keyboard.current;
            float dt = Time.deltaTime;

            
            if (canInput && Mouse.current != null)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                float sens = MouseSensitivity * Mathf.Clamp(GameSettings.Sensitivity, 0.1f, 4f);
                transform.Rotate(0f, delta.x * sens, 0f, Space.Self);
                float dy = delta.y * sens * (GameSettings.InvertY ? -1f : 1f);
                _pitch = Mathf.Clamp(_pitch - dy, -PitchClamp, PitchClamp);
            }

            
            bool wantCrouch = IsCrouching;
            if (canInput && kb != null)
            {
                if (GameSettings.HoldToCrouch) wantCrouch = kb.leftCtrlKey.isPressed || kb.cKey.isPressed;
                else if (kb.leftCtrlKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame)
                { _crouchToggle = !_crouchToggle; wantCrouch = _crouchToggle; }
                else wantCrouch = _crouchToggle;
            }
            if (!wantCrouch && IsCrouching && !HasHeadroom()) wantCrouch = true; 
            if (wantCrouch != IsCrouching)
            { IsCrouching = wantCrouch; _crouchToggle = wantCrouch; }
            float targetH = IsCrouching ? CrouchHeight : StandHeight;
            if (!Mathf.Approximately(_height, targetH))
            {
                _height = Mathf.MoveTowards(_height, targetH, CrouchLerpSpeed * dt * Mathf.Max(0.15f, Mathf.Abs(targetH - _height)) + 0.4f * dt);
                ApplyHeight(_height);
            }

            
            Vector3 wish = Vector3.zero;
            bool wantSprint = false;
            if (canInput && kb != null)
            {
                Vector2 m = Vector2.zero;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) m.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) m.x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) m.y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) m.y += 1f;
                wish = transform.right * m.x + transform.forward * m.y;
                if (wish.sqrMagnitude > 1f) wish.Normalize();
                wantSprint = kb.leftShiftKey.isPressed && m.y > 0f && !IsCrouching;
            }

            if (Exhausted && Stamina01 >= ExhaustedUntil) Exhausted = false;
            IsSprinting = wantSprint && !Exhausted && Stamina01 > 0f && wish.sqrMagnitude > 0.01f;
            if (IsSprinting)
            {
                Stamina01 = Mathf.Max(0f, Stamina01 - dt / Mathf.Max(0.1f, SprintSeconds));
                _lastSprintTime = Time.time;
                if (Stamina01 <= 0f)
                { Exhausted = true; Debug.Log("TEL PLAYER exhausted"); }
            }
            else if (Time.time - _lastSprintTime > RegenDelay)
                Stamina01=Mathf.Min(1f, Stamina01 + dt / Mathf.Max(0.1f, RegenSeconds) * (IsMoving ? 0.7f : 1.25f));

            float speed=IsCrouching ? CrouchSpeed : IsSprinting ? SprintSpeed : WalkSpeed;
            Vector3 targetVel=wish * speed;
            float rate=targetVel.sqrMagnitude > 0.01f ? Acceleration : Deceleration;
            _planarVel=Vector3.MoveTowards(_planarVel, targetVel, rate * dt);

            if (_cc.isGrounded && _verticalVel < 0f) _verticalVel=-2.5f;
            _verticalVel += Gravity * dt;
            if (_cc.enabled) _cc.Move((_planarVel + Vector3.up * _verticalVel) * dt);

            
            float planarSpeed=CurrentSpeed;
            if (playing && _cc.isGrounded && planarSpeed > 0.5f)
            {
                _stride += planarSpeed * dt;
                float strideLen=IsSprinting ? StrideSprint : StrideWalk;
                if (_stride >= strideLen)
                {
                    _stride=0f;
                    float vol=IsCrouching ? 0.12f : IsSprinting ? 0.6f : 0.3f;
                    Audio.AudioDirector.Instance?.PlayFootstep(vol);
                    NoiseSystem.Emit(transform.position, IsCrouching ? NoiseCrouch : IsSprinting ? NoiseSprint : NoiseWalk, "step");
                }
            }

            
            if (PlayerCamera != null)
            {
                float bob = 0f,sway = 0f;
                if (GameSettings.HeadBob && HeadBobAmplitude > 0f && planarSpeed > 0.5f && _cc.isGrounded)
                {
                    _bobPhase += dt * HeadBobFrequency * Mathf.Lerp(0.75f,1.55f,Mathf.InverseLerp(CrouchSpeed,SprintSpeed,planarSpeed));
                    float amp = HeadBobAmplitude * (IsSprinting ? 1.5f : 1f);
                    bob = Mathf.Sin(_bobPhase * 2f) * amp;
                    sway = Mathf.Cos(_bobPhase) * amp * 0.6f;
                }
                Vector3 shake = Vector3.zero;
                float roll = 0f;
                if (Shake > 0.001f)
                {
                    float s = GameSettings.ScreenShake ? Shake : Shake * 0.15f;
                    shake = new Vector3(Mathf.PerlinNoise(Time.time * 31f,0f) - 0.5f,Mathf.PerlinNoise(0f,Time.time * 29f) - 0.5f,0f) * 0.09f * s;
                    roll = (Mathf.PerlinNoise(Time.time * 23f,7f) - 0.5f) * 5f * s;
                    Shake = Mathf.MoveTowards(Shake,0f,Time.unscaledDeltaTime * 1.6f);
                }
                PlayerCamera.transform.localPosition = new Vector3(sway,_height - EyeBelowTop + bob,0f) + shake;
                PlayerCamera.transform.localRotation = Quaternion.Euler(_pitch,0f,roll);

                float targetFov = GameSettings.Fov + (IsSprinting ? SprintFovBoost : 0f) + FovOffset;
                PlayerCamera.fieldOfView = Mathf.Lerp(PlayerCamera.fieldOfView,targetFov,FovLerpSpeed * Time.unscaledDeltaTime);
            }
        }

        void ApplyHeight(float h)
        {
            _cc.height = h;
            _cc.center = new Vector3(0f,h * 0.5f + _cc.skinWidth,0f);
        }

        bool HasHeadroom()
        {
            float r = _cc.radius * 0.9f;
            Vector3 origin = transform.position + Vector3.up * (CrouchHeight - r);
            float dist = StandHeight - CrouchHeight + 0.05f;
            return !Physics.SphereCast(origin,r,Vector3.up,out _,dist,HeadroomMask,QueryTriggerInteraction.Ignore);
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var rb = hit.collider.attachedRigidbody;
            if (rb == null || rb.isKinematic) return;
            if (hit.moveDirection.y < -0.3f) return; 
            Vector3 dir = new Vector3(hit.moveDirection.x,0f,hit.moveDirection.z);
            float speed = CurrentSpeed;
            if (speed < 0.2f) return;
            rb.AddForceAtPosition(dir * PushImpulse * Mathf.Clamp(speed,0.5f,5f) * Mathf.Min(rb.mass,12f) * 0.25f,hit.point,ForceMode.Impulse);
            if (speed > 3.2f && Time.time - _lastPushNoise > 0.6f)
            {
                _lastPushNoise = Time.time;
                NoiseSystem.Emit(hit.point,7f, "bump");
            }
        }

        
        public float CurrentSpeed
        {
            get
            {
                if (_cc == null) return 0f;
                Vector3 v = _cc.velocity; v.y = 0f;
                return v.magnitude;
            }
        }

        
        public string QaTel() =>
            $"v={CurrentSpeed:F1} sprint={(IsSprinting ? 1 : 0)} crouch={(IsCrouching ? 1 : 0)} stam={Stamina01:F2} locked={(InputLocked ? 1 : 0)}";
    }
}
