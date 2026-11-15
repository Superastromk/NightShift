using UnityEngine;
using UnityEngine.InputSystem;

namespace NightShift.Player
{
    
    public interface IInteractable
    {
        
        string Prompt { get; }

        
        void Interact(PlayerInteractor by);
    }

    
    public interface IHoldProgress
    {
        
        float Progress01 { get; }
    }

public class PlayerInteractor : MonoBehaviour
    {
        [Header("Refs")]
        public Camera PlayerCamera;

        [Header("Config")]
        public float Range = 2.6f;
        public Key InteractKey = Key.E;
        [Tooltip("Layers the interaction ray can hit (everything except the player by default).")]
        public LayerMask Mask = ~0;

        
        public IInteractable Current { get; private set; }
        
        public float HoldProgress => Current is IHoldProgress h ? h.Progress01 : 0f;

        FirstPersonController _fpc;

        void Awake()
        {
            if (PlayerCamera == null) PlayerCamera = GetComponentInChildren<Camera>(true);
            _fpc = GetComponent<FirstPersonController>();
        }

        void Update()
        {
            Current = null;
            var ui = UI.UIManager.Instance;
            var gm = Core.GameManager.Instance;
            if ((gm != null && gm.State != Core.GameState.Playing) || (_fpc != null && _fpc.InputLocked) || PlayerCamera == null)
            {
                ui?.HidePrompt();
                return;
            }

            Ray ray = new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, Range, Mask, QueryTriggerInteraction.Ignore))
            {
                var it = hit.collider.GetComponentInParent<IInteractable>();
                if (it != null && !string.IsNullOrEmpty(it.Prompt)) Current = it;
            }

            if (Current != null)
            {
                ui?.ShowPrompt(Current.Prompt);
                var kb = Keyboard.current;
                if (kb != null && kb[InteractKey].isPressed) Current.Interact(this);
            }
            else ui?.HidePrompt();
        }
    }
}
