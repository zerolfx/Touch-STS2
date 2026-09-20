using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using TouchSts2.Localization;

namespace TouchSts2;

internal static class SettingsUi
{
    public static void AddTo(NInputSettingsPanel panel)
    {
        if (panel.Content.HasNode("TouchSts2Setting")) return;
        AddToggle(panel, "TouchSts2Feedback", "TouchFeedback", TouchSettings.TouchFeedback, TouchSettings.SetTouchFeedback);
        AddToggle(panel, "TouchSts2LongPress", "HoldToInspect", TouchSettings.LongPressInspect, TouchSettings.SetLongPressInspect);
        AddToggle(panel, "TouchSts2Setting", "TouchscreenMode", TouchSettings.Enabled, TouchSettings.SetEnabled);
        panel.Call("UpdateNavigation");
    }

    private static void AddToggle(NInputSettingsPanel panel, string name, string key, bool value, Action<bool> changed)
    {
        var row = new HBoxContainer { Name = name, CustomMinimumSize = new Vector2(0, 80) };
        var label = new MegaLabel
        {
            Text = TouchText.Get(LocManager.Instance.Language, key),
            MaxFontSize = 28, MinFontSize = 18,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontOverride("font", ResourceLoader.Load<Font>("res://themes/kreon_regular_glyph_space_one.tres"));
        label.AddThemeColorOverride("font_color", new Color(1, .964706f, .886275f));
        label.AddThemeFontSizeOverride("font_size", 28);
        row.AddChild(label);
        var toggle = new NTickbox
        {
            Name = "TouchscreenToggle", CustomMinimumSize = new Vector2(100, 80),
            FocusMode = Control.FocusModeEnum.All
        };
        // tickbox.tscn contains only the visuals, not the NTickbox script.
        var visuals = ResourceLoader.Load<PackedScene>("res://scenes/ui/tickbox.tscn").Instantiate<Control>();
        toggle.AddChild(visuals);
        visuals.Owner = toggle;
        visuals.UniqueNameInOwner = true;
        visuals.Position = new Vector2(18, 8);
        row.AddChild(toggle);
        panel.Content.AddChild(row);
        panel.Content.MoveChild(row, 0);
        toggle.IsTicked = value;
        toggle.Toggled += tickbox => changed(tickbox.IsTicked);
        LocManager.LocaleChangeCallback refresh = () =>
        {
            label.RefreshFont(); // Use the game's CJK/Thai/Cyrillic font substitutions.
            label.SetTextAutoSize(TouchText.Get(LocManager.Instance.Language, key));
        };
        LocManager.Instance.SubscribeToLocaleChange(refresh);
        row.TreeExiting += () => LocManager.Instance.UnsubscribeToLocaleChange(refresh);
        // Uses the game's own NTickbox, so its directional-navigation scan includes us.
        panel.Call("UpdateNavigation");
    }
}
