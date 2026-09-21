using Godot;

namespace TouchSts2;

internal static class TouchHover
{
    public static void Clear(Viewport viewport)
    {
        // Use the engine's exit path to clear both internal hover and native tips.
        // A synthetic motion alone can still use the OS cursor on native windows.
        if (viewport.GuiGetHoveredControl() == null) return;
        viewport.NotifyMouseExited();
        viewport.NotifyMouseEntered(); // The next real pointer event can hover again.
    }
}
