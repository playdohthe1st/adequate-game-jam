using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    private PlayerInputActions input;

    private void Awake()
    {
        input = new PlayerInputActions();
    }

    private void OnEnable()
    {
        input.Player.Enable();
    }

    private void OnDisable()
    {
        input.Player.Disable();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) input.Player.Disable();
        else input.Player.Enable();
    }

    public Vector2 GetMove() => input.Player.Move.ReadValue<Vector2>();
    public bool GetJumpPressed() => input.Player.Jump.triggered;
    public bool GetSpinPressed() => input.Player.Spin.triggered;
}
