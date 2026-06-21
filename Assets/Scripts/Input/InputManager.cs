using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    private PlayerInputActions input;

    private void Awake()
    {
        // Create the input system instance
        input = new PlayerInputActions();
    }

    private void OnEnable()
    {
        // Turn input on when this object is active
        input.Player.Enable();
    }

    private void OnDisable()
    {
        // Turn input off when this object is disabled
        input.Player.Disable();
    }

    /// <summary>
    /// Returns the current movement direction from the player.
    /// Value is a Vector2 where X = left/right, Y = up/down (if used).
    /// </summary>
    public Vector2 GetMove()
    {
        return input.Player.Move.ReadValue<Vector2>();
    }

    /// <summary>
    /// Returns true only on the frame the jump button is pressed.
    /// Used for actions that should happen once per press (like jumping).
    /// </summary>
    public bool GetJumpPressed()
    {
        return input.Player.Jump.triggered;
    }

    public bool GetSpinPressed()
    {
        return input.Player.Spin.triggered;
    }
}
