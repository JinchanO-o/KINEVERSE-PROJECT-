using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Thin wrapper so gameplay scripts never touch a specific input backend.
/// Uses the new Input System when it is enabled, otherwise the legacy Input Manager.
/// Keys: WASD move, Shift sprint, Ctrl crouch, LMB grab, RMB highlight, wheel distance, Esc pause.
/// </summary>
public static class InputBridge
{
    public static Vector2 Move()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k == null) return Vector2.zero;
        float x = (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f);
        float y = (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
        return new Vector2(x, y);
#else
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
    }

    /// <summary>Mouse delta this frame (pixels for the new Input System).</summary>
    public static Vector2 Look()
    {
#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        return m == null ? Vector2.zero : m.delta.ReadValue();
#else
        return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#endif
    }

    public static bool Sprint()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
#else
        return Input.GetKey(KeyCode.LeftShift);
#endif
    }

    public static bool Crouch()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.leftCtrlKey.isPressed;
#else
        return Input.GetKey(KeyCode.LeftControl);
#endif
    }

    public static bool EscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    public static bool GrabPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    public static bool GrabHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }

    public static bool HighlightPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(1);
#endif
    }

    /// <summary>Wheel notches this frame (+ up, - down), clamped to +/-3.</summary>
    public static float Wheel()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null) return 0f;
        float notches = Mouse.current.scroll.ReadValue().y / 120f;
#else
        float notches = Input.mouseScrollDelta.y;
#endif
        return Mathf.Clamp(notches, -3f, 3f);
    }
}
