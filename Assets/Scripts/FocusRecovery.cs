using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class FocusRecovery : MonoBehaviour
{
    [SerializeField] private bool relockCursorOnFocus = true;

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) return;

        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        InputSystem.Update();

        var playerInput = FindFirstObjectByType<PlayerInput>();
        if (playerInput != null && !string.IsNullOrEmpty(playerInput.currentActionMap?.name))
        {
            playerInput.ActivateInput();
            playerInput.currentActionMap.Enable();
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        if (relockCursorOnFocus)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}