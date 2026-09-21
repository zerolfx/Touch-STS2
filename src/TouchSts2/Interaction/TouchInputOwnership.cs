using Godot;

namespace TouchSts2.Interaction;

// Keep a pointer gesture atomic. Controller events during it are discarded,
// never queued to activate a control after the finger is released.
internal sealed class TouchInputOwnership
{
    public bool PointerDown { get; set; }
    public bool ControllerSuspended { get; set; }

    public bool Observe(InputEvent input, bool enabled, bool pendingRelease)
    {
        if (!enabled)
        {
            PointerDown = ControllerSuspended = false;
            return false;
        }
        if (input is InputEventJoypadButton or InputEventJoypadMotion)
        {
            if (!ControllerSuspended && (PointerDown || pendingRelease)) return true;
            if (input is InputEventJoypadButton { Pressed: true } ||
                input is InputEventJoypadMotion axis && Math.Abs(axis.AxisValue) > 0.5f)
                ControllerSuspended = true;
        }
        if (input is InputEventMouseButton mouse)
        {
            if (mouse.ButtonIndex == MouseButton.Left) PointerDown = mouse.Pressed;
            if (mouse.Pressed && mouse.ButtonIndex is MouseButton.Left or MouseButton.Right)
                ControllerSuspended = false;
        }
        return false;
    }
}
