using Godot;
using MegaCrit.Sts2.Core.Modding;

namespace TouchSts2;

[ModInitializer(nameof(Init))]
public static class ModEntry
{
    private static bool _initialized;
    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            TouchSettings.Load();
            GameAdapter.Install();
            GD.Print("[TouchSts2] 0.2.5 initialized; tested API baseline v0.111.0. Enable Touchscreen Mode in Input Settings.");
        }
        catch (Exception error)
        {
            GameAdapter.Uninstall();
            GD.PushError($"[TouchSts2] DISABLED: incompatible game API or patch failure. {error}");
        }
    }
}
