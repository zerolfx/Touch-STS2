using System.Reflection;
using System.Reflection.Emit;
using TouchSts2.Localization;

namespace TouchSts2;

internal sealed record TouchOption(string Key, bool Default, Func<bool> Get, Action<bool> Set);

// Only public plugin APIs are used. Neither plugin is a runtime dependency.
internal static class OptionalConfigAdapters
{
    internal static Action RegisterModConfig(Type api, TouchOption[] options)
    {
        var entryType = api.Assembly.GetType("ModConfig.ConfigEntry", true)!;
        var entries = Array.CreateInstance(entryType, options.Length);
        for (int i = 0; i < options.Length; i++)
        {
            var option = options[i];
            var entry = Activator.CreateInstance(entryType)!;
            void Set(string name, object value) => entryType.GetProperty(name)!.SetValue(entry, value);
            Set("Key", option.Key);
            Set("Label", TouchText.Get("eng", option.Key));
            var labels = TouchText.Languages.ToDictionary(lang => lang, lang => TouchText.Get(lang, option.Key));
            labels["en"] = labels["eng"];
            Set("Labels", labels);
            Set("Type", Enum.Parse(entryType.GetProperty("Type")!.PropertyType, "Toggle"));
            Set("DefaultValue", option.Default);
            Set("OnChanged", (Action<object>)(value => option.Set((bool)value)));
            entries.SetValue(entry, i);
        }
        var get = api.GetMethod("GetValue")!.MakeGenericMethod(typeof(bool));
        var set = api.GetMethod("SetValue", [typeof(string), typeof(string), typeof(object)])!;
        api.GetMethod("Register", [typeof(string), typeof(string), entries.GetType()])!
            .Invoke(null, ["TouchSts2", "Touch-STS2", entries]);
        void Sync()
        {
            foreach (var option in options)
                if ((bool)get.Invoke(null, ["TouchSts2", option.Key])! != option.Get())
                    set.Invoke(null, ["TouchSts2", option.Key, option.Get()]);
        }
        // Keep existing preferences authoritative, including on the first plugin installation.
        Sync();
        return Sync;
    }

    internal static object CreateBaseLibConfig(Type configType, Delegate buildUi, Action reset)
        => Activator.CreateInstance(BuildBaseLibAdapter(configType, buildUi, reset))!;

    internal static Type BuildBaseLibAdapter(Type configType, Delegate buildUi, Action reset)
    {
        // BaseLib requires a subclass. Emit only the public/protected virtual adapter,
        // keeping the packaged DLL loadable when BaseLib is absent.
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("TouchSts2.BaseLibAdapter"), AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("Config").DefineType("TouchSts2.Config", TypeAttributes.Public, configType);
        var ctor = type.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes).GetILGenerator();
        ctor.Emit(OpCodes.Ldarg_0);
        ctor.Emit(OpCodes.Ldstr, "TouchSts2.BaseLib.cfg"); // Never overwrite our canonical preferences.
        ctor.Emit(OpCodes.Call, configType.GetConstructor([typeof(string)])!);
        ctor.Emit(OpCodes.Ret);

        var visible = configType.GetMethod("VisibleInModList")!;
        var method = type.DefineMethod(visible.Name, MethodAttributes.Public | MethodAttributes.Virtual, typeof(bool), Type.EmptyTypes);
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Ret);
        type.DefineMethodOverride(method, visible);

        void Forward(string name, Delegate callback)
        {
            var original = configType.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
            var field = type.DefineField(name + "Callback", callback.GetType(), FieldAttributes.Public | FieldAttributes.Static);
            var parameters = original.GetParameters().Select(p => p.ParameterType).ToArray();
            var attrs = MethodAttributes.Virtual | (original.IsPublic ? MethodAttributes.Public : MethodAttributes.Family);
            var forward = type.DefineMethod(name, attrs, typeof(void), parameters);
            var body = forward.GetILGenerator();
            body.Emit(OpCodes.Ldsfld, field);
            for (int i = 0; i < parameters.Length; i++) body.Emit(OpCodes.Ldarg, i + 1);
            body.Emit(OpCodes.Callvirt, callback.GetType().GetMethod("Invoke")!);
            body.Emit(OpCodes.Ret);
            type.DefineMethodOverride(forward, original);
        }
        Forward("SetupConfigUI", buildUi);
        Forward("RestoreDefaultsNoConfirm", reset);
        var adapter = type.CreateType()!;
        adapter.GetField("SetupConfigUICallback")!.SetValue(null, buildUi);
        adapter.GetField("RestoreDefaultsNoConfirmCallback")!.SetValue(null, reset);
        return adapter;
    }
}
