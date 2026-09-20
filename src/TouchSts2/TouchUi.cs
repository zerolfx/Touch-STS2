using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Credits;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Timeline;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Rewards;
using TouchSts2.Interaction;

namespace TouchSts2;

/// <summary>Arbitrates a pointer sequence before GUI dispatch, including presses on child hitboxes.</summary>
internal static class TouchUi
{
    private static readonly Dictionary<Type, string> ScrollFields = new()
    {
        [typeof(NScrollableContainer)] = "_targetDragPosY",
        [typeof(NCardGrid)] = "_targetDrag",
        [typeof(NDropdownContainer)] = "_targetDragPos",
        [typeof(NRewardsScreen)] = "_targetDragPos",
        [typeof(NCreditsScreen)] = "_targetPosition",
        [typeof(NSlotsContainer)] = "_targetPosition"
    };
    private static readonly Dictionary<Type, FieldInfo> Fields = new();
    private static TouchPress? _press;
    private static Control? _origin, _scroll;
    private static NCardHolder? _card;
    private static object? _cardModel;
    private static NRewardButton? _reward;
    private static Reward? _rewardModel;
    private static IScreenContext? _context;
    private static Vector2 _start, _last;
    private static bool _replaying;
    internal static bool IsReplaying => _replaying;
    private static bool _consumeRelease;
    private static PropertyInfo? _gridCanScroll;
    private static readonly List<(NMainMenuTextButton Button, Vector2 Minimum)> MenuButtons = new();
    private static double Now => Time.GetTicksMsec() / 1000d;

    internal static void ValidateContract()
    {
        _gridCanScroll = AccessTools.Property(typeof(NCardGrid), "CanScroll") ?? throw new MissingMemberException("NCardGrid.CanScroll");
        foreach (var (type, name) in ScrollFields)
        {
            var field = AccessTools.Field(type, name) ?? throw new MissingFieldException(type.FullName, name);
            if (field.FieldType != typeof(float) && field.FieldType != typeof(Vector2)) throw new InvalidOperationException($"Unexpected scroll field: {field}");
            Fields[type] = field;
        }
    }

    public static bool Observe(InputEvent input)
    {
        if (_replaying) return false;
        if (!TouchRuntime.Active) { Reset(); return false; }
        if (input is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            bool pending = TouchConfirmation.Pending;
            Reset(); TouchConfirmation.Clear();
            return pending;
        }
        if (input is InputEventMouseMotion motion && _press != null)
        {
            _press.Move(motion.Position.X, motion.Position.Y);
            if (_press.Dragged && !_press.Held && Valid())
            {
                if (_scroll != null) Scroll(motion.Position - _last);
                TouchConfirmation.Clear();
            }
            _last = motion.Position;
            return true;
        }
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse) return false;
        if (mouse.Pressed)
        {
            if (_press != null) return true;
            _consumeRelease = false;
            TouchConfirmation.OutsidePress(mouse.Position);
            var hovered = NGame.Instance!.GetViewport().GuiGetHoveredControl();
            if (hovered == null || !TouchConfirmation.Contains(hovered, mouse.Position)) return false;
            Control? scroll = null;
            NCardHolder? card = null;
            NRewardButton? reward = null;
            for (Node? n = hovered; n != null; n = n.GetParent())
            {
                // Sliders, scrollbars and confirmation buttons keep native press/release ownership.
                if (n is Godot.Range || n is Button || n.Name == "TouchConfirmation") return false;
                if (n is NHandCardHolder) return false;
                if (n is NCardHolder holder) card = holder;
                if (n is NRewardButton rewardButton && rewardButton.Reward?.HoverTips.Any() == true) reward = rewardButton;
                if (n is Control c && Fields.Keys.Any(t => t.IsInstanceOfType(n))) { scroll = c; break; }
            }
            if (scroll is NCardGrid grid && !CanScroll(grid)) scroll = null;
            if (scroll == null && card == null && reward == null) return false;
            _origin = hovered; _scroll = scroll; _card = card; _cardModel = card?.CardModel;
            _reward = reward; _rewardModel = reward?.Reward;
            _context = ActiveScreenContext.Instance.GetCurrentScreen();
            _start = _last = mouse.Position;
            var view = NGame.Instance.GetViewport().GetVisibleRect().Size;
            _press = new TouchPress(_start.X, _start.Y, Now, TouchGesture.Scale(view.X, view.Y));
            _consumeRelease = true;
            return true;
        }
        if (_press == null) { bool consumed = _consumeRelease; _consumeRelease = false; return consumed; }
        _press.Move(mouse.Position.X, mouse.Position.Y);
        // A slow frame can deliver release before Tick observes the hold timeout.
        // Resolve it here as well, so a held preview can never turn into a purchase/claim.
        TryInspect();
        bool tap = _press.IsTap && Valid() && TouchConfirmation.Contains(_origin, mouse.Position);
        var start = _start;
        var end = mouse.Position;
        var context = _context;
        var origin = _origin;
        Reset();
        _consumeRelease = false;
        if (tap)
        {
            // Defer so the captured release cannot also reach the newly opened screen.
            Callable.From(() =>
            {
                if (!TouchRuntime.Active || !TouchConfirmation.Usable(origin) || !ReferenceEquals(context, ActiveScreenContext.Instance.GetCurrentScreen())) return;
                _replaying = true;
                try
                {
                    var viewport = NGame.Instance!.GetViewport();
                    using var down = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = start, GlobalPosition = start };
                    using var up = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = end, GlobalPosition = end };
                    viewport.PushInput(down, true);
                    viewport.PushInput(up, true);
                }
                finally { _replaying = false; }
            }).CallDeferred();
        }
        return true;
    }

    public static void Tick()
    {
        UpdateMenuTargets();
        TouchConfirmation.Tick();
        if (_press == null) return;
        if (!TouchRuntime.Active || !NGame.IsGameFocusedWindow() || !Valid()) { Reset(); return; }
        TryInspect();
    }

    private static void TryInspect()
    {
        if (TouchRuntime.Active && NGame.IsGameFocusedWindow() && TouchSettings.LongPressInspect &&
            Valid() && (_card != null || _reward != null) && _press!.TryHold(Now))
        {
            TouchConfirmation.Clear();
            if (_card is { } card)
            {
                // Prefer the screen's existing alternate action, with a native
                // inspector fallback for holders that have no alternate binding.
                if (card.GetSignalConnectionList(NCardHolder.SignalName.AltPressed).Count > 0)
                    card.EmitSignal(NCardHolder.SignalName.AltPressed, card);
                else if (card.CardModel is { } model)
                    NGame.Instance!.GetInspectCardScreen().Open([model], 0);
            }
            else if (_reward is { } reward)
            {
                // Reward rows (including returned stolen cards) are not card
                // holders. Reuse native hover content and consume this release;
                // replaying the click would hide the preview and claim the reward.
                NHoverTipSet.Remove(reward);
                reward.Call("OnFocus");
            }
            GD.Print("[TouchSts2] long press: native preview (no selection)");
        }
    }

    private static bool Valid() => TouchConfirmation.Usable(_origin) &&
        ReferenceEquals(_context, ActiveScreenContext.Instance.GetCurrentScreen()) &&
        (_card == null || TouchConfirmation.Usable(_card) && ReferenceEquals(_cardModel, _card.CardModel)) &&
        (_reward == null || TouchConfirmation.Usable(_reward) && ReferenceEquals(_rewardModel, _reward.Reward)) &&
        (_scroll == null || TouchConfirmation.Usable(_scroll)) && (_scroll is not NCardGrid grid || CanScroll(grid));

    private static bool CanScroll(NCardGrid grid) => (bool)_gridCanScroll!.GetValue(grid)!;

    private static void Scroll(Vector2 delta)
    {
        if (_scroll == null) return;
        var pair = Fields.First(p => p.Key.IsInstanceOfType(_scroll));
        var localDelta = _scroll.GetGlobalTransformWithCanvas().BasisXformInv(delta);
        var value = pair.Value.GetValue(_scroll);
        var drag = _scroll is NSlotsContainer ? new Vector2(localDelta.X, 0) : new Vector2(0, localDelta.Y);
        pair.Value.SetValue(_scroll, value is Vector2 v ? v + drag : (object)((float)value! + drag.Y));
    }

    public static void Reset()
    {
        _press = null; _origin = null; _scroll = null; _card = null; _cardModel = null;
        _reward = null; _rewardModel = null; _context = null;
    }

    internal static void RegisterMenuButton(NMainMenuTextButton button) => MenuButtons.Add((button, button.CustomMinimumSize));

    private static void UpdateMenuTargets()
    {
        MenuButtons.RemoveAll(p => !GodotObject.IsInstanceValid(p.Button));
        foreach (var (button, minimum) in MenuButtons)
        {
            // The game has a fixed menu region. Double targets only as far as its available height permits,
            // so continuing a run or showing extra menu entries never pushes Quit off-screen.
            var siblings = button.GetParent()?.GetChildren().OfType<Control>().Where(c => c.Visible).ToArray();
            float height = button.GetParent() is Control parent && siblings is { Length: > 0 }
                ? Math.Min(100, parent.Size.Y / siblings.Length) : 100;
            button.CustomMinimumSize = TouchRuntime.Active
                ? new Vector2(Math.Max(minimum.X, 250), Math.Max(minimum.Y, height)) : minimum;
        }
    }

}
