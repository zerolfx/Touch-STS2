using TouchSts2;
using TouchSts2.Localization;

internal static class SettingsAdapterChecks
{
    internal static void VerifyInstalledPlugins(string workshopDirectory)
    {
        var paths = Directory.EnumerateFiles(workshopDirectory, "*.dll", SearchOption.AllDirectories).ToArray();
        var baseLib = System.Reflection.Assembly.LoadFrom(paths.Single(path => Path.GetFileName(path) == "BaseLib.dll"));
        var config = baseLib.GetType("BaseLib.Config.ModConfig", true)!;
        var adapter = OptionalConfigAdapters.BuildBaseLibAdapter(config, (Action<object>)(_ => { }), () => { });
        if (!config.IsAssignableFrom(adapter)) throw new Exception("BaseLib adapter contract failed");
        _ = baseLib.GetType("BaseLib.Config.ModConfigRegistry", true)!.GetMethod("Register", [typeof(string), config])
            ?? throw new Exception("BaseLib registration API missing");

        var plugin = System.Reflection.Assembly.LoadFrom(paths.Single(path => Path.GetFileName(path) == "ModConfig.dll"));
        var api = plugin.GetType("ModConfig.ModConfigApi", true)!;
        var entry = plugin.GetType("ModConfig.ConfigEntry", true)!;
        _ = api.GetMethod("Register", [typeof(string), typeof(string), entry.MakeArrayType()]) ?? throw new Exception("ModConfig registration API missing");
        _ = api.GetMethod("SetValue", [typeof(string), typeof(string), typeof(object)]) ?? throw new Exception("ModConfig setter missing");
        _ = api.GetMethod("GetValue")!.MakeGenericMethod(typeof(bool));
        foreach (var name in new[] { "Key", "Label", "Labels", "DefaultValue", "OnChanged" })
            if (entry.GetProperty(name)?.CanWrite != true) throw new Exception($"ModConfig property missing: {name}");
        _ = Enum.Parse(entry.GetProperty("Type")!.PropertyType, "Toggle");
        Console.WriteLine("PASS: installed BaseLib and ModConfig public API contracts; BaseLib adapter type generated without starting the engine.");
    }

    internal static int Run()
    {
        int checks = 0;
        void Check(bool condition, string message)
        {
            checks++;
            if (!condition) throw new Exception(message);
        }
        var keys = new[] { "TouchscreenMode", "HoldToInspect", "TouchFeedback" };
        var current = new[] { true, false, false };
        var defaults = new[] { false, true, true };
        Action? changed = null;
        var options = keys.Select((key, i) => new TouchOption(key, defaults[i], () => current[i], value =>
        {
            if (current[i] == value) return;
            current[i] = value;
            changed?.Invoke();
        })).ToArray();
        changed = OptionalConfigAdapters.RegisterModConfig(typeof(ModConfig.ModConfigApi), options);
        Check(ModConfig.ModConfigApi.Entries.Length == 3, "All three settings register");
        for (int i = 0; i < options.Length; i++)
        {
            var entry = ModConfig.ModConfigApi.Entries[i];
            Check(ModConfig.ModConfigApi.GetValue<bool>("TouchSts2", keys[i]) == current[i], "Existing preferences survive registration");
            Check((bool)entry.DefaultValue == defaults[i], "Defaults are independent of saved preferences");
            foreach (var language in TouchText.Languages)
                Check(entry.Labels[language] == TouchText.Get(language, keys[i]), "Localized plugin label matches game language");
            Check(entry.Labels["en"] == entry.Labels["eng"], "ModConfig English locale alias");
        }
        options[0].Set(false);
        Check(!ModConfig.ModConfigApi.GetValue<bool>("TouchSts2", keys[0]), "Native settings synchronize to plugin");
        ModConfig.ModConfigApi.SetValue("TouchSts2", keys[1], true);
        Check(current[1], "Plugin changes synchronize to native settings without recursion");
        options[0].Set(true);
        foreach (var entry in ModConfig.ModConfigApi.Entries)
            ModConfig.ModConfigApi.SetValue("TouchSts2", entry.Key, entry.DefaultValue);
        Check(current.SequenceEqual(defaults), "Plugin reset restores real defaults");
        int writes = ModConfig.ModConfigApi.Writes;
        changed();
        Check(writes == ModConfig.ModConfigApi.Writes, "Unchanged values do not trigger callbacks or writes");

        object container = new();
        object? received = null;
        var adapter = (ConfigContract)OptionalConfigAdapters.CreateBaseLibConfig(typeof(ConfigContract),
            (Action<object>)(value => received = value), () => { foreach (var option in options) option.Set(option.Default); });
        Check(adapter.VisibleInModList(), "BaseLib lists custom configuration even without reflected properties");
        Check(adapter.Filename != "TouchSts2.cfg", "BaseLib must not overwrite canonical config with its different file format");
        adapter.SetupConfigUI(container);
        Check(ReferenceEquals(container, received), "BaseLib receives shared settings builder");
        options[0].Set(true);
        options[1].Set(false);
        adapter.Reset();
        Check(current.SequenceEqual(defaults), "BaseLib reset updates canonical settings");
        Check(!ModConfig.ModConfigApi.GetValue<bool>("TouchSts2", keys[0]), "BaseLib reset synchronizes other plugin");
        return checks;
    }
}

// Public because the emitted optional adapter lives in a separate assembly.
public abstract class ConfigContract
{
    public ConfigContract(string filename) => Filename = filename;
    public string Filename { get; }
    public virtual bool VisibleInModList() => false;
    public abstract void SetupConfigUI(object container);
    protected virtual void RestoreDefaultsNoConfirm() => throw new Exception("Missing reset override");
    public void Reset() => RestoreDefaultsNoConfirm();
}

namespace ModConfig
{
    public enum ConfigType { Toggle }
    public sealed class ConfigEntry
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public Dictionary<string, string> Labels { get; set; } = new();
        public ConfigType Type { get; set; }
        public object DefaultValue { get; set; } = false;
        public Action<object>? OnChanged { get; set; }
    }
    public static class ModConfigApi
    {
        public static ConfigEntry[] Entries { get; private set; } = [];
        public static int Writes { get; private set; }
        private static readonly Dictionary<string, object> Values = new();
        public static void Register(string id, string name, ConfigEntry[] entries)
        {
            Entries = entries;
            foreach (var entry in entries) Values[entry.Key] = entry.DefaultValue;
        }
        public static T GetValue<T>(string id, string key) => (T)Values[key];
        public static void SetValue(string id, string key, object value)
        {
            Writes++;
            if (Writes > 30) throw new Exception("Configuration sync loop");
            Values[key] = value;
            Entries.Single(entry => entry.Key == key).OnChanged?.Invoke(value);
        }
    }
}
