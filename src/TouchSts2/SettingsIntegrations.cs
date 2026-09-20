using Godot;

namespace TouchSts2;

internal static class SettingsIntegrations
{
    // Called on the first game frame, after all mod initializers have run.
    internal static void Initialize()
    {
        void Register(string typeName, Action<Type> register)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(typeName)).FirstOrDefault(t => t != null);
            if (type == null) return;
            try
            {
                register(type);
                GD.Print($"[TouchSts2] Settings registered with {type.Assembly.GetName().Name}");
            }
            catch (Exception error)
            {
                GD.PushWarning($"[TouchSts2] Optional settings integration failed: {typeName}: {error.GetBaseException().Message}");
            }
        }
        Register("ModConfig.ModConfigApi", api =>
        {
            var sync = OptionalConfigAdapters.RegisterModConfig(api, TouchSettings.Options);
            TouchSettings.Changed += () =>
            {
                try { sync(); }
                catch (Exception error) { GD.PushWarning($"[TouchSts2] ModConfig sync failed: {error.GetBaseException().Message}"); }
            };
        });
        Register("BaseLib.Config.ModConfig", configType =>
        {
            var config = OptionalConfigAdapters.CreateBaseLibConfig(configType,
                (Action<Control>)(container => SettingsUi.AddOptions(container)),
                () => { foreach (var option in TouchSettings.Options) option.Set(option.Default); });
            configType.Assembly.GetType("BaseLib.Config.ModConfigRegistry", true)!
                .GetMethod("Register", [typeof(string), configType])!.Invoke(null, ["TouchSts2", config]);
        });
    }
}
