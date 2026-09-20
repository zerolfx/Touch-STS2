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
        AddOptions(panel.Content, true);
        panel.Call("UpdateNavigation");
    }

    internal static void AddOptions(Control container, bool prepend = false)
    {
        var options = prepend ? TouchSettings.Options.Reverse() : TouchSettings.Options;
        foreach (var option in options) AddToggle(container, option, prepend);
    }

    private static void AddToggle(Control container, TouchOption option, bool prepend)
    {
        var row = new HBoxContainer
        {
            Name = option.Key == "TouchscreenMode" ? "TouchSts2Setting" : "TouchSts2" + option.Key,
            CustomMinimumSize = new Vector2(0, 80)
        };
        var label = new MegaLabel
        {
            Text = TouchText.Get(LocManager.Instance.Language, option.Key),
            // Match native settings; initial zero-sized container layout must not shrink the text.
            AutoSizeEnabled = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
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
            Name = "TouchscreenToggle", CustomMinimumSize = new Vector2(320, 80),
            FocusMode = Control.FocusModeEnum.All
        };
        // tickbox.tscn contains only the visuals, not the NTickbox script.
        var visuals = ResourceLoader.Load<PackedScene>("res://scenes/ui/tickbox.tscn").Instantiate<Control>();
        toggle.AddChild(visuals);
        visuals.Owner = toggle;
        visuals.UniqueNameInOwner = true;
        visuals.Position = new Vector2(128, 8);
        row.AddChild(toggle);
        container.AddChild(row);
        if (prepend) container.MoveChild(row, 0);
        toggle.IsTicked = option.Get();
        toggle.Toggled += tickbox => option.Set(tickbox.IsTicked);
        void Sync() => toggle.IsTicked = option.Get();
        TouchSettings.Changed += Sync;
        LocManager.LocaleChangeCallback refresh = () =>
        {
            label.AddThemeFontOverride("font", ResourceLoader.Load<Font>("res://themes/kreon_regular_glyph_space_one.tres"));
            label.RefreshFont(); // Use the game's CJK/Thai/Cyrillic font substitutions.
            label.Text = TouchText.Get(LocManager.Instance.Language, option.Key);
        };
        LocManager.Instance.SubscribeToLocaleChange(refresh);
        row.TreeExiting += () =>
        {
            TouchSettings.Changed -= Sync;
            LocManager.Instance.UnsubscribeToLocaleChange(refresh);
        };
    }
}
