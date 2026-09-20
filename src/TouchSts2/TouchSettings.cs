using Godot;

namespace TouchSts2;

internal static class TouchSettings
{
    private const string Path = "user://mod_configs/TouchSts2.cfg";
    public static bool Enabled { get; private set; }
    public static bool Diagnostics { get; private set; }
    public static bool LongPressInspect { get; private set; } = true;
    public static bool TouchFeedback { get; private set; } = true;
    public static event Action? Changed;
    internal static readonly TouchOption[] Options =
    [
        new("TouchscreenMode", false, () => Enabled, SetEnabled),
        new("HoldToInspect", true, () => LongPressInspect, SetLongPressInspect),
        new("TouchFeedback", true, () => TouchFeedback, SetTouchFeedback)
    ];

    public static void Load()
    {
        using var cfg = new ConfigFile();
        var result = cfg.Load(Path);
        if (result != Error.Ok && result != Error.FileNotFound)
            GD.PushWarning($"[TouchSts2] Configuration load failed: {result}");
        Enabled = cfg.GetValue("touch", "enabled", false).AsBool();
        Diagnostics = cfg.GetValue("touch", "diagnostics", false).AsBool();
        LongPressInspect = cfg.GetValue("touch", "long_press_inspect", true).AsBool();
        TouchFeedback = cfg.GetValue("touch", "touch_feedback", true).AsBool();
    }

    public static void SetEnabled(bool enabled)
    {
        if (Enabled == enabled) return;
        TouchRuntime.Cancel("setting changed");
        TouchUi.Reset();
        TouchConfirmation.Clear();
        Enabled = enabled;
        Save();
        TouchRuntime.RefreshCursor();
        GD.Print($"[TouchSts2] Touchscreen Mode = {enabled}");
    }

    public static void SetLongPressInspect(bool enabled)
    {
        if (LongPressInspect == enabled) return;
        LongPressInspect = enabled;
        Save();
    }

    public static void SetTouchFeedback(bool enabled)
    {
        if (TouchFeedback == enabled) return;
        TouchFeedback = enabled;
        TouchCursor.Reset();
        Save();
    }

    private static void Save()
    {
        using var cfg = new ConfigFile();
        cfg.SetValue("touch", "enabled", Enabled);
        cfg.SetValue("touch", "diagnostics", Diagnostics);
        cfg.SetValue("touch", "long_press_inspect", LongPressInspect);
        cfg.SetValue("touch", "touch_feedback", TouchFeedback);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("user://mod_configs"));
        var result = cfg.Save(Path);
        if (result != Error.Ok) GD.PushError($"[TouchSts2] Configuration save failed: {result}");
        Changed?.Invoke();
    }
}
