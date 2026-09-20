using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using TouchSts2.Interaction;

namespace TouchSts2;

/// <summary>One pending local action, confirmed through the game's native checkmark button.</summary>
internal static class TouchConfirmation
{
    private static Control? _selection, _layer;
    private static NConfirmButton? _button;
    private static IScreenContext? _context;
    private static readonly PendingConfirmation Choice = new();
    public static bool Submitting { get; private set; }
    public static bool Pending => Choice.Pending;

    public static bool Stage(Control selection, Action submit, Func<bool> valid, Action? cleanup = null)
    {
        if (!TouchRuntime.Active || Submitting) return false;
        Clear();
        if (!Choice.Stage(submit, valid, cleanup)) return true;
        _selection = selection;
        _context = ActiveScreenContext.Instance.GetCurrentScreen();
        // STS1 keeps Skip/Bowl in their original positions and reveals a separate
        // bottom-right confirm button. Blank-space taps clear the selection.
        // Reuse STS2 visuals, sound and animation; the checkmark needs no new text.
        var layer = new Control { Name = "TouchConfirmation", TopLevel = true,
            ZIndex = 100, MouseFilter = Control.MouseFilterEnum.Ignore };
        var button = ResourceLoader.Load<PackedScene>("res://scenes/ui/confirm_button.tscn").Instantiate<NConfirmButton>();
        button.OffsetTop = -190;
        button.OffsetBottom = -80;
        // Do not compete with the screen's existing Skip/Proceed hotkeys.
        button.OverrideHotkeys([]);
        button.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ => Confirm()));
        layer.AddChild(button);
        _layer = layer;
        _button = button;
        ((Node?)_context ?? NGame.Instance!).AddChild(layer);
        button.Enable();
        GD.Print("[TouchSts2] confirmation staged");
        return true;
    }

    public static void Tick()
    {
        if (!Pending) return;
        if (!TouchRuntime.Active || !NGame.IsGameFocusedWindow() || !Usable(_selection) ||
            !ReferenceEquals(_context, ActiveScreenContext.Instance.GetCurrentScreen()) || !Choice.IsValid)
            Clear();
    }

    public static void OutsidePress(Vector2 point)
    {
        if (Pending && !Contains(_button, point) && !Contains(_selection, point)) Clear();
    }

    private static void Confirm()
    {
        Tick();
        var action = Choice.Take();
        Clear(); // Clear before invoking: synchronous continuations and double taps cannot resubmit.
        if (action == null) return;
        Submitting = true;
        try { action(); GD.Print("[TouchSts2] confirmation submitted"); }
        catch (Exception error) { GD.PushError($"[TouchSts2] Confirmation failed: {error}"); }
        finally { Submitting = false; }
    }

    public static void Clear()
    {
        _context = null;
        _selection = null;
        if (GodotObject.IsInstanceValid(_button)) _button!.Disable();
        _button = null;
        if (GodotObject.IsInstanceValid(_layer)) { _layer!.Hide(); _layer.QueueFree(); }
        _layer = null;
        Choice.Clear();
    }

    internal static bool IsWithin(Control screen) => Pending && Usable(_selection) && screen.IsAncestorOf(_selection!);

    internal static bool Usable(Control? node) => GodotObject.IsInstanceValid(node) && !node!.IsQueuedForDeletion() && node.IsVisibleInTree();
    internal static bool Contains(Control? node, Vector2 point) => Usable(node) &&
        new Rect2(Vector2.Zero, node!.Size).HasPoint(node.GetGlobalTransformWithCanvas().AffineInverse() * point);
}
