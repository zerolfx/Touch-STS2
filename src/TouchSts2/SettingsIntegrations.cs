using Godot;
using MegaCrit.Sts2.Core.Localization;
using TouchSts2.Localization;

namespace TouchSts2;

internal static class SettingsIntegrations
{
    // Called on the first game frame, after all mod initializers have run.
    internal static void Initialize()
    {
        var registry = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("STS2RitsuLib.Settings.ModSettingsRegistry")).FirstOrDefault(t => t != null);
        if (registry == null) return;
        try
        {
            RitsuSettingsAdapter.Register(registry, TouchSettings.Options,
                key => TouchText.Get(LocManager.Instance.Language, key));
            GD.Print("[TouchSts2] Settings registered with RitsuLib Mod Settings");
        }
        catch (Exception error)
        {
            GD.PushWarning($"[TouchSts2] RitsuLib settings integration failed: {error.GetBaseException().Message}");
        }
    }
}
