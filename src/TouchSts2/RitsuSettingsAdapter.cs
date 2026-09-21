using System.Reflection;

namespace TouchSts2;

internal sealed record TouchOption(string Key, bool Default, Func<bool> Get, Action<bool> Set);

// Bind to the public settings API only when RitsuLib is loaded.
internal static class RitsuSettingsAdapter
{
    internal static void Register(Type registry, TouchOption[] options, Func<string, string> localize)
    {
        Type Api(string name) => registry.Assembly.GetType("STS2RitsuLib.Settings." + name, true)!;
        var text = Api("ModSettingsText");
        var builder = Api("ModSettingsPageBuilder");
        var section = Api("ModSettingsSectionBuilder");
        var bindings = Api("ModSettingsBindings");
        var literal = text.GetMethod("Literal", [typeof(string)])!;
        var dynamicText = text.GetMethod("Dynamic", [typeof(Func<string>)])!;
        var callback = bindings.GetMethod("Callback")!.MakeGenericMethod(typeof(bool));
        var withDefault = bindings.GetMethod("WithDefault")!.MakeGenericMethod(typeof(bool));
        var page = Activator.CreateInstance(builder, ["TouchSts2", "touchscreen"])!;
        var title = literal.Invoke(null, ["Touch-STS2"]);
        builder.GetMethod("WithTitle")!.Invoke(page, [title]);
        builder.GetMethod("WithModDisplayName")!.Invoke(page, [title]);
        // Our canonical preferences already save on write. Do not create a second config store.
        Action<object> configure = target =>
        {
            foreach (var option in options)
            {
                var binding = callback.Invoke(null,
                    ["TouchSts2", option.Key, option.Get, option.Set, (Action)(() => { }), Type.Missing]);
                binding = withDefault.Invoke(null, [binding, (Func<bool>)(() => option.Default), null]);
                var label = dynamicText.Invoke(null, [(Func<string>)(() => localize(option.Key))]);
                section.GetMethod("AddToggle")!.Invoke(target, [option.Key, label, binding, null, null]);
            }
        };
        var addSection = builder.GetMethod("AddSection")!;
        var configureDelegate = configure.Method.CreateDelegate(addSection.GetParameters()[1].ParameterType, configure.Target);
        addSection.Invoke(page, ["controls", configureDelegate]);
        registry.GetMethod("Register", [Api("ModSettingsPage")])!
            .Invoke(null, [builder.GetMethod("Build")!.Invoke(page, null)]);
    }
}
