using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using TouchSts2;
using TouchSts2.Localization;

internal static class SettingsAdapterChecks
{
    internal static void VerifyInstalledPlugins(string workshopDirectory)
    {
        var settingsPath = Directory.EnumerateFiles(workshopDirectory, "STS2-RitsuLib.Settings.dll", SearchOption.AllDirectories).Single();
        var shared = Path.GetDirectoryName(settingsPath)!;
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var path = Path.Combine(shared, name.Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        var assembly = Assembly.LoadFrom(settingsPath);
        var registry = assembly.GetType("STS2RitsuLib.Settings.ModSettingsRegistry", true)!;
        int checks = 0, writes = 0;
        void Check(bool condition, string message)
        {
            checks++;
            if (!condition) throw new Exception(message);
        }
        static object Get(object instance, string name) => instance.GetType().GetProperty(name)!.GetValue(instance)!;
        static object? Call(object instance, string name, params object[] args) => instance.GetType().GetMethod(name)!.Invoke(instance, args);
        var keys = new[] { "TouchscreenMode", "HoldToInspect", "TouchFeedback" };
        var current = new[] { true, false, false };
        var defaults = new[] { false, true, true };
        string language = "eng";
        var options = keys.Select((key, i) => new TouchOption(key, defaults[i], () => current[i], value =>
        {
            current[i] = value;
            writes++;
        })).ToArray();
        RitsuSettingsAdapter.Register(registry, options, key => TouchText.Get(language, key));
        Check(writes == 0, "Registration must preserve saved settings");
        var pages = ((IEnumerable)registry.GetMethod("GetPages", Type.EmptyTypes)!.Invoke(null, null)!).Cast<object>();
        var page = pages.Single(p => (string)Get(p, "ModId") == "TouchSts2");
        Check((string)Get(page, "Id") == "touchscreen", "Stable settings page identity");
        var section = ((IEnumerable)Get(page, "Sections")).Cast<object>().Single();
        var entries = ((IEnumerable)Get(section, "Entries")).Cast<object>().ToArray();
        Check(entries.Length == 3, "Register all three toggles");
        for (int i = 0; i < options.Length; i++)
        {
            Check((string)Get(entries[i], "Id") == keys[i], "Keep option identities");
            var binding = Get(entries[i], "Binding");
            Check((bool)Call(binding, "Read")! == current[i], "Read saved canonical preference");
            Check((bool)Call(binding, "CreateDefaultValue")! == defaults[i], "Defaults must not depend on saved values");
            foreach (var locale in TouchText.Languages)
            {
                language = locale;
                Check((string)Call(Get(entries[i], "Label"), "Resolve")! == TouchText.Get(locale, keys[i]),
                    "Labels must resolve in the current language");
            }
            current[i] = !current[i];
            Check((bool)Call(binding, "Read")! == current[i], "Native setting changes must be read live");
            bool next = !current[i];
            Call(binding, "Write", next);
            Check(current[i] == next, "Plugin changes must update canonical preference");
            int beforeSave = writes;
            Call(binding, "Save");
            Check(writes == beforeSave, "Plugin save must not write preferences a second time");
            Call(binding, "Write", Call(binding, "CreateDefaultValue")!);
            Check(current[i] == defaults[i], "Reset must restore the real default");
        }
        RitsuSettingsAdapter.Register(registry, options, key => TouchText.Get(language, key));
        var registered = ((IEnumerable)registry.GetMethod("GetPages", Type.EmptyTypes)!.Invoke(null, null)!).Cast<object>();
        Check(registered.Count(p => (string)Get(p, "ModId") == "TouchSts2") == 1, "Re-registration must not duplicate the page");
        Console.WriteLine($"PASS: {checks} installed RitsuLib settings checks using its real registry, localized labels, and value/reset bindings (no game UI).");
    }
}
