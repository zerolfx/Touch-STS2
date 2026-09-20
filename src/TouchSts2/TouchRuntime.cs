using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using TouchSts2.Interaction;

namespace TouchSts2;

internal static class TouchRuntime
{
    private static NMouseCardPlay? _play;
    private static TouchGesture? _gesture;
    private static NHandCardHolder? _switchTo;
    private static Vector2 _switchPress;
    private static Vector2? _switchRelease;
    private static ulong _lastEvent;
    private static bool _lastConsumed, _controllerSuspended, _pointerDown, _parkPending, _initialized;
    private static Vector2 _pointer;
    private static float _inspectX;
    private static Rect2 _originalCardRect;
    private static (NMouseCardPlay Play, Vector2 Position)? _release;
    private static double _commitTime;
    private static ulong _lastHolder;
    private static double _regrabAt;
    private static bool _faulted;
    private static bool _injected;
    private static bool _cursorHidden;
    private static int? _primaryTouch;
    private static int _diagnosticEvents;
    private static double _diagnosticWindow;
    public static bool Active => GameAdapter.Healthy && !_faulted && TouchSettings.Enabled && !_controllerSuspended;
    public static bool HasCard => _play != null && GodotObject.IsInstanceValid(_play) && !_play.IsQueuedForDeletion();
    public static bool CommitRequested { get; private set; }
    public static bool Submitting { get; private set; }
    public static bool SwitchingCard { get; private set; }
    private static bool CanInteract => Active && NGame.IsGameFocusedWindow() &&
        NCombatRoom.Instance != null && ActiveScreenContext.Instance.IsCurrent(NCombatRoom.Instance) &&
        !CombatManager.Instance.IsOverOrEnding && !CombatManager.Instance.PlayerActionsDisabled;
    private static double Now => Time.GetTicksMsec() / 1000d;
    private static Vector2 ViewSize => NGame.Instance!.GetViewport().GetVisibleRect().Size;
    private static float Scale => TouchGesture.Scale(ViewSize.X, ViewSize.Y);
    public static bool PointerInDropZone => TouchGesture.InDropZone(_pointer.Y, ViewSize.Y, Scale);
    public static Vector2 CardPosition
    {
        get
        {
            var (x, y) = _gesture!.GetCardPosition(ViewSize.X, ViewSize.Y, _inspectX);
            return new Vector2(x, y);
        }
    }

    public static bool Owns(NMouseCardPlay play) => ReferenceEquals(_play, play);
    public static bool Owns(NHandCardHolder holder) => HasCard && ReferenceEquals(_play!.Holder, holder);
    public static bool KeepCardTips(NCardHolder holder) => Active && HasCard && ReferenceEquals(_play!.Holder, holder) &&
        _gesture is { HasReleased: true, IsDown: false, IsAiming: false } && !CommitRequested;
    public static bool CanGrab(NHandCardHolder holder) =>
        CanInteract && _pointerDown && (holder.GetInstanceId() != _lastHolder || Now >= _regrabAt);

    public static void Attach(NMouseCardPlay play)
    {
        _play = play;
        CommitRequested = false;
        _gesture = new TouchGesture();
        _gesture.Press(_pointer.X, _pointer.Y);
        _originalCardRect = play.Holder.Hitbox.GetGlobalRect();
        _inspectX = play.Holder.GetGlobalTransformWithCanvas().Origin.X;
        play.Finished += success => Finished(play, success);
        Log($"selected {play.Holder.CardModel?.Id}; viewport={ViewSize}; pointer={_pointer}; hitbox={_originalCardRect}");
    }

    public static bool Observe(InputEvent input)
    {
        if (_injected || Submitting || _faulted || !GameAdapter.Healthy) return false;
        // The same event may visit several patched _Input methods in either tree order.
        if (input.GetInstanceId() == _lastEvent) return _lastConsumed;
        _lastEvent = input.GetInstanceId();
        _lastConsumed = false;
        try { _lastConsumed = ObserveCore(input); }
        catch (Exception error) { Fail(error); }
        return _lastConsumed;
    }

    private static bool ObserveCore(InputEvent input)
    {
        Probe(input);
        if (input is InputEventScreenTouch touch)
        {
            if (touch.Pressed) _primaryTouch ??= touch.Index;
            else if (_primaryTouch == touch.Index)
            {
                _primaryTouch = null;
                if (touch.Canceled)
                {
                    TouchCursor.Reset();
                    TouchUi.Reset();
                    TouchConfirmation.Clear();
                    _pointerDown = false;
                    _switchTo = null;
                    _switchRelease = null;
                    Cancel("touch canceled by OS");
                }
            }
            return false; // Godot's primary-touch mouse emulation is the only command path.
        }
        // Godot may deliver a complete second tap before _Process runs. Resolve the
        // preceding release before a new press/motion mutates the gesture's origin.
        if (_release is { } pending && (input is InputEventMouseMotion || input is InputEventMouseButton { Pressed: true }))
        {
            _release = null;
            if (Owns(pending.Play) && HasCard) Release(pending.Position);
        }
        if (input is InputEventJoypadButton { Pressed: true } ||
            input is InputEventJoypadMotion axis && Math.Abs(axis.AxisValue) > 0.5f)
        {
            _controllerSuspended = true;
            TouchUi.Reset();
            TouchConfirmation.Clear();
            Cancel("controller input");
            return false;
        }
        if (input is InputEventMouseMotion motion)
        {
            if (Active && !TouchUi.IsReplaying) TouchCursor.Move(motion.Position);
            if (!HasCard && TouchUi.Observe(input)) return true;
            _pointer = motion.Position;
            _gesture?.Move(_pointer.X, _pointer.Y, ViewSize.Y, Scale);
            UpdateAim(_pointer);
            return false; // Movement never changes the touch preference.
        }
        if (input is not InputEventMouseButton mouse) return TouchUi.Observe(input);
        if (mouse.Pressed && mouse.ButtonIndex is MouseButton.Left or MouseButton.Right)
        {
            _controllerSuspended = false;
            if (Active && NControllerManager.Instance is { InputType: not InputType.MouseAndKeyboard } controller)
                controller.ForceMouseMode();
            RefreshCursor();
        }
        if (!Active) return false;
        _pointer = mouse.Position;
        if (mouse.ButtonIndex == MouseButton.Right)
        {
            if (!HasCard) return false;
            if (mouse.Pressed) Cancel("right click");
            return true;
        }
        if (mouse.ButtonIndex != MouseButton.Left) return false;
        bool repeatedDown = mouse.Pressed && _pointerDown;
        _pointerDown = mouse.Pressed;
        if (!TouchUi.IsReplaying) TouchCursor.Button(mouse.Pressed, mouse.Position);
        if (!HasCard && _switchTo == null && TouchUi.Observe(input)) return true;
        if (repeatedDown && (HasCard || _switchTo != null)) return true;
        if (_switchTo != null)
        {
            if (!mouse.Pressed) _switchRelease = mouse.Position;
            return true;
        }
        if (!HasCard) return false;
        if (CommitRequested) return true;
        if (mouse.Pressed)
        {
            var next = NPlayerHand.Instance?.ActiveHolders.LastOrDefault(h =>
                !ReferenceEquals(h, _play!.Holder) && h.Hitbox.IsEnabled && Contains(h.Hitbox, _pointer));
            if (next != null)
            {
                Cancel("switch card");
                _switchTo = next;
                _switchPress = mouse.Position;
                _switchRelease = null;
                _parkPending = false;
                return true;
            }
            _gesture!.Press(_pointer.X, _pointer.Y);
            UpdateAim(_pointer);
        }
        else
        {
            // Wait until GUI hover has caught up with this event. Keep its exact position;
            // do not read a stale hardware mouse position to decide whether to play.
            _release = (_play!, _pointer);
        }
        return true; // Selecting/canceling a card must never click through to an underlying button.
    }

    public static void Tick(double delta)
    {
        if (_faulted || !GameAdapter.Healthy) return;
        try { TickCore(); TouchCursor.Tick((float)delta); }
        catch (Exception error) { Fail(error); }
    }

    private static void TickCore()
    {
        if (NGame.Instance == null || NControllerManager.Instance == null) return;
        TouchUi.Tick();
        if (!_initialized)
        {
            _initialized = true;
            GD.Print($"[TouchSts2] game={NGame.GetGameVersion()}; engine={Engine.GetVersionInfo()}; display={DisplayServer.GetName()}; dpi={DisplayServer.ScreenGetDpi()}; emulateMouseFromTouch={Input.EmulateMouseFromTouch}; viewport={ViewSize}");
            if (!NGame.GetGameVersion().Contains("0.111.0"))
                GD.PushWarning("[TouchSts2] Game differs from the v0.111.0 API baseline; runtime validation required.");
        }
        if (!NGame.IsGameFocusedWindow())
        {
            Cancel("window focus lost");
            _pointerDown = false;
            _switchTo = null;
            _switchRelease = null;
            _primaryTouch = null;
            return;
        }
        if (NControllerManager.Instance.InputType != InputType.MouseAndKeyboard)
        {
            _controllerSuspended = true;
            if (HasCard) Cancel("directional navigation");
        }
        if (!CanInteract)
        {
            Cancel("combat context changed");
            _switchTo = null;
            _switchRelease = null;
        }

        if (_switchTo != null && !HasCard)
        {
            var next = _switchTo;
            _switchTo = null;
            var hand = NPlayerHand.Instance;
            // Wait for the canceled native controller to leave the tree before replacing it.
            if (hand != null && hand.InCardPlay) { _switchTo = next; return; }
            if (Active && hand != null && GodotObject.IsInstanceValid(next) && next.Hitbox.IsEnabled)
            {
                int index = hand.ActiveHolders.ToList().IndexOf(next);
                if (index >= 0 && index < 10)
                {
                    SwitchingCard = true;
                    bool wasDown = _pointerDown;
                    _pointerDown = true;
                    _pointer = _switchPress;
                    try
                    {
                        using var action = new InputEventAction { Action = $"mega_select_card_{index + 1}", Pressed = true };
                        hand._UnhandledInput(action);
                    }
                    finally { SwitchingCard = false; _pointerDown = wasDown; }
                    if (_switchRelease is { } end && HasCard)
                    {
                        _pointer = end;
                        _release = (_play!, end);
                    }
                }
            }
            _switchRelease = null;
        }
        if (_release is { } release)
        {
            _release = null;
            if (Owns(release.Play) && HasCard) Release(release.Position);
        }
        if (HasCard)
        {
            if (!CommitRequested)
            {
                UpdateAim(_pointer);
                _play!.Holder.SetTargetPosition(CardPosition);
            }
            else if (Now - _commitTime > 3)
            {
                GD.PushError("[TouchSts2] Native card controller did not finish within 3 seconds; canceling. Please report the game log.");
                Cancel("commit timeout");
            }
        }
        else if (_play != null)
        {
            // Scene teardown can free a native controller without emitting Finished.
            _play = null;
            _gesture = null;
            CommitRequested = false;
            _release = null;
        }
        if (_parkPending && !HasCard && !_pointerDown && _switchTo == null)
        {
            _parkPending = false;
            ParkPointer();
        }
        RefreshCursor();
    }

    private static void Release(Vector2 position)
    {
        // A new input event can flush a pending release before the next Tick.
        if (!CanInteract) { Cancel("combat context changed before release"); return; }
        var play = _play!;
        var card = play.Holder.CardModel;
        if (card == null || NCombatRoom.Instance == null) { Cancel("card removed"); return; }
        bool single = card.TargetType is TargetType.AnyEnemy or TargetType.AnyAlly;
        var manager = NTargetManager.Instance;
        Node? target = single ? FindTarget(position) : null;
        _gesture!.Move(position.X, position.Y, ViewSize.Y, Scale);
        UpdateAim(position);
        var intent = _gesture!.Release(position.X, position.Y, ViewSize.Y, Scale, single, target != null);
        Log($"release {card.Id}: {intent}; position={position}; target={target?.Name}");
        if (intent == ReleaseIntent.Cancel) { Cancel("gesture"); return; }
        if (intent == ReleaseIntent.Inspect) { play.Holder.Call("CreateHoverTips"); return; }
        if (!card.CanPlay(out _, out _) || !card.CanPlayTargeting(TargetCreature(target)))
        {
            Cancel("card no longer playable");
            return;
        }
        if (single)
        {
            SyncTarget(target);
            // Native hooks may reject a target after hit testing (or a teammate
            // may have died). Never submit using a previously hovered creature.
            if (!ReferenceEquals(NativeHoveredTarget, target)) { Cancel("target rejected"); return; }
        }
        CommitRequested = true;
        play.Holder.Call("ClearHoverTips");
        _commitTime = Now;
        if (single)
        {
            Submitting = true;
            try
            {
                using var action = new InputEventAction { Action = MegaInput.select, Pressed = true };
                manager._Input(action);
            }
            finally { Submitting = false; }
        }
        else
        {
            // Feed only this owned native controller. The adapter latches its mouse state
            // until the async game routine validates and queues the card normally.
            _injected = true;
            try
            {
                using var mouse = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = position };
                play._Input(mouse);
            }
            finally { _injected = false; }
        }
    }

    private static Creature? TargetCreature(Node? node) => node switch
    {
        NCreature creature => creature.Entity,
        NMultiplayerPlayerState state => state.Player.Creature,
        _ => null
    };

    private static Node? NativeHoveredTarget => NTargetManager.Instance.Get("HoveredNode").AsGodotObject() as Node;

    private static Node? FindTarget(Vector2 position)
    {
        var card = _play?.Holder.CardModel;
        var manager = NTargetManager.Instance;
        if (card == null || NCombatRoom.Instance == null || manager == null || !manager.IsInSelection) return null;
        // The sidebar is a native targeting surface in co-op. It must take
        // precedence over creatures behind it, including when the row is invalid.
        var row = NRun.Instance?.GlobalUi.MultiplayerPlayerContainer.GetChildren()
            .OfType<NMultiplayerPlayerState>().LastOrDefault(n =>
                TouchConfirmation.Usable(n) && Contains(n.Hitbox, position));
        Node? candidate = row;
        if (row != null && !row.Hitbox.IsEnabled) return null;
        candidate ??= NCombatRoom.Instance.CreatureNodes.LastOrDefault(n => Contains(n.Hitbox, position));
        return candidate != null && manager.AllowedToTargetNode(candidate) && card.CanPlayTargeting(TargetCreature(candidate))
            ? candidate : null;
    }

    private static void SyncTarget(Node? target)
    {
        var previous = NativeHoveredTarget;
        if (ReferenceEquals(previous, target)) return;
        if (GodotObject.IsInstanceValid(previous)) NTargetManager.Instance.OnNodeUnhovered(previous!);
        if (target != null) NTargetManager.Instance.OnNodeHovered(target);
    }

    private static void UpdateAim(Vector2 position)
    {
        if (!HasCard || _gesture == null || CommitRequested) return;
        bool single = _play!.Holder.CardModel?.TargetType is TargetType.AnyEnemy or TargetType.AnyAlly;
        var target = single ? FindTarget(position) : null;
        bool wasAiming = _gesture.IsAiming;
        _gesture.UpdateAim(ViewSize.Y, Scale, single, target != null);
        if (single && _gesture.IsAiming && NTargetManager.Instance.IsInSelection) SyncTarget(target);
        if (_gesture.IsAiming && !wasAiming)
        {
            _play.Holder.SetScaleInstantly(Vector2.One);
            Log($"aim anchored: card={_play.Holder.CardModel?.Id}; position={CardPosition}; pointer={position}; scale=1");
        }
    }

    public static void Cancel(string reason)
    {
        _release = null;
        _switchTo = null;
        _switchRelease = null;
        if (!HasCard) return;
        Log($"cancel: {reason}");
        var play = _play!;
        // Only cancel this card's target selection, never an unrelated potion/modal.
        bool singleTarget = play.Holder.CardModel?.TargetType is TargetType.AnyEnemy or TargetType.AnyAlly;
        // Cancel the native async routine before resolving SelectionFinished: that
        // task can resume synchronously and must not attempt to play a canceled card.
        play.CancelPlayCard();
        if (singleTarget)
            NTargetManager.Instance?.CancelTargeting();
    }

    private static void Finished(NMouseCardPlay play, bool success)
    {
        if (!Owns(play)) return;
        Log($"native Finished success={success}");
        _lastHolder = play.Holder.GetInstanceId();
        _regrabAt = Now + TouchGesture.RegrabDelay;
        _play = null;
        _gesture = null;
        if (GodotObject.IsInstanceValid(play.Holder)) play.Holder.Call("ClearHoverTips");
        _release = null;
        CommitRequested = false;
        _parkPending = Active;
    }

    private static bool Contains(Control control, Vector2 viewportPosition) =>
        control.IsVisibleInTree() && new Rect2(Vector2.Zero, control.Size).HasPoint(
            control.GetGlobalTransformWithCanvas().AffineInverse() * viewportPosition);

    private static void ParkPointer()
    {
        TouchCursor.Hide();
        var neutral = new Vector2(10 * Scale, ViewSize.Y / 2);
        _injected = true;
        try
        {
            // Synthetic motion clears Godot hover even if gamescope refuses the warp.
            NGame.Instance!.GetViewport().WarpMouse(neutral);
            using var motion = new InputEventMouseMotion { Position = neutral, GlobalPosition = neutral, Device = -1 };
            NGame.Instance.GetViewport().PushInput(motion, true);
        }
        finally { _injected = false; }
        Log($"park requested={neutral}; actual={NGame.Instance!.GetViewport().GetMousePosition()}");
    }

    public static void RefreshCursor()
    {
        if (!Active) TouchCursor.Reset();
        if (Active) Input.MouseMode = Input.MouseModeEnum.Hidden;
        else if (_cursorHidden) NGame.Instance?.CursorManager?.SetCursorShown(true);
        _cursorHidden = Active;
    }

    private static void Probe(InputEvent input)
    {
        if (!TouchSettings.Diagnostics) return;
        if (Now - _diagnosticWindow > 1) { _diagnosticWindow = Now; _diagnosticEvents = 0; }
        if (++_diagnosticEvents > 40) return;
        string detail = input switch
        {
            InputEventMouseButton m => $"button={m.ButtonIndex} down={m.Pressed} position={m.Position}",
            InputEventScreenTouch t => $"finger={t.Index} down={t.Pressed} canceled={t.Canceled} position={t.Position}",
            InputEventScreenDrag d => $"finger={d.Index} position={d.Position}",
            _ => ""
        };
        GD.Print($"[TouchSts2:input] {input.GetType().Name} device={input.Device} {detail}");
    }

    private static void Log(string message)
    {
        if (TouchSettings.Diagnostics) GD.Print($"[TouchSts2] {message}");
    }

    private static void Fail(Exception error)
    {
        GD.PushError($"[TouchSts2] Runtime failure; touch overrides disabled: {error}");
        try { Cancel("runtime failure"); }
        catch (Exception cleanupError) { GD.PushError($"[TouchSts2] Cleanup failed: {cleanupError}"); }
        _faulted = true;
        _play = null;
        RefreshCursor();
    }
}
