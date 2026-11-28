using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Thin wrapper so gameplay scripts never touch a specific input backend.
/// Uses the new Input System when it is enabled, otherwise the legacy Input Manager.
/// Keys: WASD move, Shift sprint, Ctrl crouch, RMB select, LMB move selected, R + RMB rotate selected,
/// X/Y/Z axis tools, S + (+/-) resize, wheel distance, Space jump (when enabled), Esc pause, F1 controls.
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

    /// <summary>Space pressed this frame: jump, when PlayerController has jumping enabled.</summary>
    public static bool JumpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Space);
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

    /// <summary>F1 pressed this frame: show or hide the controls panel.</summary>
    public static bool ControlsPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.F1);
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

    public static bool SelectPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(1);
#endif
    }

    /// <summary>R held: arms rotation of the selected item.</summary>
    public static bool RotateKeyHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.rKey.isPressed;
#else
        return Input.GetKey(KeyCode.R);
#endif
    }

    /// <summary>Right mouse button held: rotates the selected item while rotation is armed.</summary>
    public static bool RotateMouseHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.rightButton.isPressed;
#else
        return Input.GetMouseButton(1);
#endif
    }

    /// <summary>X, Y or Z held: that local axis is active for moving, rotating or resizing the selected item.</summary>
    public static bool AxisKeyHeld(Axis axis)
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k == null) return false;
        return axis == Axis.X ? k.xKey.isPressed : axis == Axis.Y ? k.yKey.isPressed : axis == Axis.Z && k.zKey.isPressed;
#else
        return axis == Axis.X ? Input.GetKey(KeyCode.X) : axis == Axis.Y ? Input.GetKey(KeyCode.Y) : axis == Axis.Z && Input.GetKey(KeyCode.Z);
#endif
    }

    /// <summary>S held: resize mode for the selected item.</summary>
    public static bool ScaleKeyHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.sKey.isPressed;
#else
        return Input.GetKey(KeyCode.S);
#endif
    }

    /// <summary>+ held (the +/= key or Numpad +): grow.</summary>
    public static bool ScaleUpHeld()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        return k != null && (k.equalsKey.isPressed || k.numpadPlusKey.isPressed);
#else
        return Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus);
#endif
    }

    /// <summary>- held (main keyboard or Numpad -): shrink.</summary>
    public static bool ScaleDownHeld()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        return k != null && (k.minusKey.isPressed || k.numpadMinusKey.isPressed);
#else
        return Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus);
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
