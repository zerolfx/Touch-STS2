using Godot;
using TouchSts2;
using TouchSts2.Interaction;

public partial class Checks : Node
{
    private readonly TouchInputOwnership _ownership = new();
    private int _checks, _mouseDown, _mouseUp, _touchDown, _blocked;
    private bool _enabled = true, _pending;
    private Vector2 _motion;

    public override void _Input(InputEvent input)
    {
        if (_ownership.Observe(input, _enabled, _pending)) _blocked++;
        if (input is InputEventScreenTouch { Pressed: true }) _touchDown++;
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
        {
            if (mouse.Pressed) _mouseDown++; else _mouseUp++;
        }
        if (input is InputEventMouseMotion motion) _motion = motion.Position;
    }

    private void Check(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Send(InputEvent input)
    {
        using (input) Input.ParseInputEvent(input);
        Input.FlushBufferedEvents();
    }

    private static void Mouse(bool pressed) => Send(new InputEventMouseButton
    {
        ButtonIndex = MouseButton.Left, Pressed = pressed,
        Position = new Vector2(100, 100), GlobalPosition = new Vector2(100, 100)
    });

    private static void Joy(bool pressed) => Send(new InputEventJoypadButton { ButtonIndex = JoyButton.A, Pressed = pressed });

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Input.UseAccumulatedInput = false;
            GD.Print($"Input checks: engine={Engine.GetVersionInfo()["string"]}; OS={OS.GetName()}; display={DisplayServer.GetName()}");
            CheckInput();
            await CheckHover();
            if (DisplayServer.GetName() != "headless") await CheckNativeWindow();
            GD.Print($"PASS: {_checks} engine checks (shared production helpers, simulated input; no game scenes or physical touchscreen).");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void CheckInput()
    {
        Input.EmulateTouchFromMouse = false;
        Input.EmulateMouseFromTouch = false;
        Mouse(true);
        Check(_ownership.PointerDown, "Mouse-emulated touch starts a pointer gesture");
        Joy(true);
        Joy(false);
        Check(_blocked == 2 && !_ownership.ControllerSuspended, "Controller down and up cannot interrupt a held gesture");
        Send(new InputEventJoypadMotion { Axis = JoyAxis.LeftX, AxisValue = 1 });
        Check(_blocked == 3 && !_ownership.ControllerSuspended, "Stick input cannot interrupt a held gesture");
        Mouse(false);
        Check(!_ownership.PointerDown, "Release ends the held portion");
        _pending = true;
        Joy(true);
        Joy(false);
        Check(_blocked == 5 && !_ownership.ControllerSuspended, "Deferred release remains protected until processed");
        _pending = false;
        Check(!_ownership.ControllerSuspended, "Discarded controller input is not replayed on completion");
        Send(new InputEventJoypadMotion { Axis = JoyAxis.LeftX, AxisValue = .1f });
        Check(!_ownership.ControllerSuspended, "Small stick drift does not take ownership");
        Joy(true);
        Check(_ownership.ControllerSuspended, "A fresh controller press after completion takes ownership");
        Joy(false);
        Mouse(true);
        Check(!_ownership.ControllerSuspended, "A fresh touch resumes pointer interaction");
        _enabled = false;
        Joy(true);
        Check(_blocked == 5 && !_ownership.PointerDown, "Disabling touch mode releases ownership and preserves controller input");
        Joy(false);
        Mouse(false);
        _enabled = true;

        int downs = _mouseDown, ups = _mouseUp, touches = _touchDown;
        Send(new InputEventScreenTouch { Index = 0, Pressed = true, Position = new Vector2(100, 100) });
        Send(new InputEventScreenDrag { Index = 0, Position = new Vector2(400, 300), Relative = new Vector2(300, 200) });
        Send(new InputEventScreenTouch { Index = 0, Pressed = false, Position = new Vector2(400, 300) });
        Check(_touchDown == touches + 1 && _mouseDown == downs && _mouseUp == ups,
            "Raw touch without emulation supplies no mouse command (documented coverage limit)");

        Input.EmulateMouseFromTouch = true;
        Send(new InputEventScreenTouch { Index = 0, Pressed = true, Position = new Vector2(100, 100) });
        Check(_ownership.PointerDown && _mouseDown == downs + 1, "Godot touch promotion starts exactly one pointer gesture");
        Joy(true);
        Check(!_ownership.ControllerSuspended, "Promoted touch also protects against controller takeover");
        Joy(false);
        Send(new InputEventScreenDrag { Index = 0, Position = new Vector2(400, 300), Relative = new Vector2(300, 200) });
        Check(_motion == new Vector2(400, 300), "Promoted drag preserves finger position");
        Send(new InputEventScreenTouch { Index = 1, Pressed = true, Position = new Vector2(700, 500) });
        Send(new InputEventScreenTouch { Index = 1, Pressed = false, Position = new Vector2(700, 500) });
        Check(_mouseDown == downs + 1 && _mouseUp == ups && _ownership.PointerDown, "A second finger does not duplicate or release the primary mouse gesture");
        Send(new InputEventScreenTouch { Index = 0, Pressed = false, Position = new Vector2(400, 300) });
        Check(_mouseDown == downs + 1 && _mouseUp == ups + 1 && !_ownership.PointerDown, "Promoted touch ends exactly once");
        Send(new InputEventScreenTouch { Index = 0, Pressed = true, Position = new Vector2(100, 100) });
        Send(new InputEventScreenTouch { Index = 0, Pressed = false, Canceled = true, Position = new Vector2(100, 100) });
        Check(!_ownership.PointerDown && _mouseUp == ups + 2, "Canceled promoted touch releases pointer ownership");
        Joy(true);
        Check(_ownership.ControllerSuspended, "Controller works after touch cancellation");
        Joy(false);
        Mouse(true);
        _ownership.PointerDown = false; // Runtime focus-loss cleanup.
        Joy(true);
        Check(_ownership.ControllerSuspended, "Focus-loss cleanup cannot leave the controller locked out");
        Joy(false);
        Mouse(false);
        Input.EmulateMouseFromTouch = false;

        // Run actual card policy together with the production ownership guard.
        foreach (bool targeted in new[] { false, true })
        {
            var gesture = new TouchGesture();
            Mouse(true);
            gesture.Press(400, 720);
            gesture.Move(800, 350, 800, 2f / 3);
            Joy(true);
            Check(!_ownership.ControllerSuspended, "Controller input cannot cancel a drag before submission");
            Joy(false);
            Mouse(false);
            _pending = true;
            Check(gesture.Release(800, 350, 800, 2f / 3, targeted, targeted) == ReleaseIntent.Commit,
                "The protected targeted/untargeted drag commits once");
            Check(gesture.Release(800, 350, 800, 2f / 3, targeted, targeted) != ReleaseIntent.Commit,
                "A duplicate release cannot submit twice");
            _pending = false;
        }
    }

    private async Task CheckHover()
    {
        var viewport = new SubViewport { Size = new Vector2I(1280, 800) };
        AddChild(viewport);
        viewport.NotifyMouseEntered();
        var card = new Control { Size = new Vector2(300, 300) };
        viewport.AddChild(card);
        bool tip = false;
        int clicks = 0;
        card.MouseEntered += () => tip = true;
        card.MouseExited += () => tip = false;
        card.GuiInput += input => { if (input is InputEventMouseButton) clicks++; };
        void Hover()
        {
            using var input = new InputEventMouseMotion { Position = new Vector2(100, 100) };
            viewport.PushInput(input, true);
        }
        Hover();
        Check(tip && viewport.GuiGetHoveredControl() == card, "Establish native hover before cleanup");
        // Deliberately never warp: this represents a compositor ignoring the request.
        TouchHover.Clear(viewport);
        Check(!tip && viewport.GuiGetHoveredControl() == null, "Cleanup clears native tips and internal hover without an OS warp");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(!tip && clicks == 0, "Stationary frames do not restore the old hover or click");
        TouchHover.Clear(viewport);
        Hover();
        Check(tip && viewport.GuiGetHoveredControl() == card, "The next real pointer event restores hover");
        using (var down = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = new Vector2(100, 100) })
            viewport.PushInput(down, true);
        using (var up = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Position = new Vector2(100, 100) })
            viewport.PushInput(up, true);
        Check(clicks == 2, "A subsequent click remains usable and is delivered exactly once");
        TouchHover.Clear(viewport);
        card.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        TouchHover.Clear(viewport);
        Check(viewport.GuiGetHoveredControl() == null, "Cleanup tolerates freed controls");
        viewport.QueueFree();
    }

    private async Task CheckNativeWindow()
    {
        var window = GetWindow();
        window.GuiEmbedSubwindows = false;
        var control = new Control { Size = new Vector2(300, 300) };
        AddChild(control);
        bool hovered = false;
        control.MouseEntered += () => hovered = true;
        control.MouseExited += () => hovered = false;
        window.GrabFocus();
        window.WarpMouse(new Vector2(100, 100)); // Setup only, never used by cleanup.
        await ToSignal(GetTree().CreateTimer(.3), SceneTreeTimer.SignalName.Timeout);
        using (var motion = new InputEventMouseMotion { Position = new Vector2(100, 100) })
            window.PushInput(motion, true);
        Check(hovered && window.GuiGetHoveredControl() == control, "Native window must establish real display-server hover");
        var before = DisplayServer.MouseGetPosition();
        using (var neutral = new InputEventMouseMotion { Position = new Vector2(1000, 700) })
            window.PushInput(neutral, true);
        Check(hovered, "Reproduce the old fallback: synthetic motion cannot clear native hover when OS warp is ignored");
        TouchHover.Clear(window);
        Check(!hovered && window.GuiGetHoveredControl() == null, "Native-window cleanup clears hover without moving the OS pointer");
        Check(DisplayServer.MouseGetPosition() == before, "Cleanup works with an unchanged OS pointer");
        await ToSignal(GetTree().CreateTimer(.1), SceneTreeTimer.SignalName.Timeout);
        Check(!hovered, "Native-window hover stays clear between input events");
        using (var motion = new InputEventMouseMotion { Position = new Vector2(100, 100) })
            window.PushInput(motion, true);
        Check(hovered, "Native-window hover resumes on the next pointer event");
        control.QueueFree();
    }
}
