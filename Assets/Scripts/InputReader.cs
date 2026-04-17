using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kart
{
    [CreateAssetMenu(fileName = "InputReader", menuName = "Kart/Input Reader")]
    public class InputReader : ScriptableObject, PlayerInputActions.IPlayerActions
    {
        public Vector2 Move => inputActions.Player.Move.ReadValue<Vector2>();
        public bool IsBraking => inputActions.Player.Brake.ReadValue<float>() > 0;
        
        private PlayerInputActions inputActions;

        private void OnEnable()
        {
            if (inputActions == null)
            {
                inputActions = new PlayerInputActions();
                inputActions.Player.SetCallbacks(this);
            }
            inputActions.Enable();
        }
        
        private void OnDisable()
        {
            if (inputActions == null) return;

            inputActions.Disable();
        }

        private void OnDestroy()
        {
            if (inputActions == null) return;
            
            inputActions.Dispose();
            inputActions = null;
        }

        public void Enable()
        {
            inputActions?.Enable();
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            // noop
        }

        public void OnLook(InputAction.CallbackContext context)
        {
            // noop
        }

        public void OnFire(InputAction.CallbackContext context)
        {
            // noop
        }

        public void OnBrake(InputAction.CallbackContext context)
        {
            // noop
        }
    }
}
