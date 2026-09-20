using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Rewards;

namespace TouchSts2;

/// <summary>All version-sensitive patch points and private-member access live here.</summary>
internal static class GameAdapter
{
    private static readonly Harmony Patches = new("touchsts2.input");
    private static NHandCardHolder? _startingHolder;
    public static bool Healthy { get; private set; }

    private static (MethodInfo Method, string? Prefix, string? Postfix)[] ResolvePatches()
    {
        // Resolve the entire manifest before changing any game method.
        var specs = new (Type Type, string Method, Type[] Args, string? Prefix, string? Postfix)[]
        {
            (typeof(NControllerManager), "_Input", [typeof(InputEvent)], nameof(ObserveInput), null),
            (typeof(NGame), "_Input", [typeof(InputEvent)], nameof(ObserveInput), null),
            (typeof(NControllerManager), "_Process", [typeof(double)], null, nameof(Process)),
            (typeof(NPlayerHand), "StartCardPlay", [typeof(NHandCardHolder), typeof(bool)], nameof(StartCard), nameof(EndStartCard)),
            (typeof(NMouseCardPlay), "Create", [typeof(NHandCardHolder), typeof(StringName), typeof(bool)], null, nameof(CardCreated)),
            (typeof(NMouseCardPlay), "_Input", [typeof(InputEvent)], nameof(CardInput), null),
            (typeof(NTargetManager), "_Input", [typeof(InputEvent)], nameof(TargetInput), null),
            (typeof(NMouseCardPlay), "IsCardInPlayZone", [], nameof(PlayZone), null),
            (typeof(NMouseCardPlay), "IsCardInCancelZone", [], nameof(CancelZone), null),
            (typeof(NHandCardHolder), "SetTargetPosition", [typeof(Vector2)], nameof(CardPosition), null),
            (typeof(NInputSettingsPanel), "_Ready", [], null, nameof(SettingsReady)),
            (typeof(NCardRewardSelectionScreen), "SelectCard", [typeof(NCardHolder)], nameof(RewardSelected), null),
            (typeof(NChooseACardSelectionScreen), "SelectHolder", [typeof(NCardHolder)], nameof(ChoiceCardSelected), null),
            (typeof(NChooseARelicSelection), "SelectHolder", [typeof(NRelicBasicHolder)], nameof(ChoiceRelicSelected), null),
            (typeof(NMerchantSlot), "OnSelected", [], nameof(ShopSelected), null),
            (typeof(NRestSiteButton), "SelectOption", [typeof(RestSiteOption)], nameof(RestSelected), null),
            (typeof(NCardHolder), "ClearHoverTips", [], nameof(ClearCardTips), null),
            (typeof(NMainMenuTextButton), "ConnectSignals", [], null, nameof(MenuReady))
        };
        var resolved = specs.Select(s => (s.Prefix, s.Postfix, Method: AccessTools.DeclaredMethod(s.Type, s.Method, s.Args)
            ?? throw new MissingMethodException(s.Type.FullName, s.Method))).ToArray();
        if (AccessTools.Field(typeof(NMouseCardPlay), "_isLeftMouseDown")?.FieldType != typeof(bool))
            throw new MissingFieldException(typeof(NMouseCardPlay).FullName, "_isLeftMouseDown");
        foreach (var (name, type) in new[] { ("_targetPosition", typeof(Vector2)), ("_positionCancelToken", typeof(CancellationTokenSource)) })
            if (AccessTools.Field(typeof(NHandCardHolder), name)?.FieldType != type)
                throw new MissingFieldException(typeof(NHandCardHolder).FullName, name);
        foreach (var type in new[] { typeof(NChooseACardSelectionScreen), typeof(NChooseARelicSelection) })
            if (AccessTools.Field(type, "_screenComplete")?.FieldType != typeof(bool))
                throw new MissingFieldException(type.FullName, "_screenComplete");
        if (AccessTools.Field(typeof(NChooseACardSelectionScreen), "_openedTicks")?.FieldType != typeof(ulong))
            throw new MissingFieldException(typeof(NChooseACardSelectionScreen).FullName, "_openedTicks");
        if (AccessTools.Property(typeof(NTargetManager), "HoveredNode")?.PropertyType != typeof(Node))
            throw new MissingMemberException("NTargetManager.HoveredNode");
        if (AccessTools.DeclaredMethod(typeof(NRewardButton), "OnFocus", []) == null)
            throw new MissingMethodException("NRewardButton.OnFocus");
        foreach (var item in resolved)
        {
            if (item.Method.Name is "IsCardInPlayZone" or "IsCardInCancelZone" && item.Method.ReturnType != typeof(bool))
                throw new InvalidOperationException($"Unexpected return type: {item.Method}");
        }
        TouchUi.ValidateContract();
        return resolved.Select(s => (s.Method, s.Prefix, s.Postfix)).ToArray();
    }

    // Can be run without a Godot engine: verifies the very same private/public seam manifest.
    internal static string[] ValidateContract() => ResolvePatches().Select(p => $"{p.Method.DeclaringType!.Name}.{p.Method.Name}").ToArray();

    public static void Install()
    {
        foreach (var item in ResolvePatches())
        {
            Patches.Patch(item.Method, Hook(item.Prefix), Hook(item.Postfix));
            GD.Print($"[TouchSts2] patch OK: {item.Method.DeclaringType!.Name}.{item.Method.Name}");
        }
        Healthy = true;
    }

    private static HarmonyMethod? Hook(string? name) => name == null ? null :
        new HarmonyMethod(typeof(GameAdapter).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!);

    public static void Uninstall()
    {
        Healthy = false;
        Patches.UnpatchAll(Patches.Id);
    }

    private static bool ObserveInput(InputEvent inputEvent)
    {
        bool consumed = TouchRuntime.Observe(inputEvent);
        if (consumed) NGame.Instance?.GetViewport().SetInputAsHandled();
        return !consumed;
    }

    private static void Process(double __0) => TouchRuntime.Tick(__0);

    private static bool RewardSelected(NCardRewardSelectionScreen __instance, NCardHolder cardHolder)
    {
        var card = cardHolder.CardModel;
        return !TouchConfirmation.Stage(cardHolder,
            () => __instance.Call("SelectCard", cardHolder),
            () => TouchConfirmation.Usable(__instance) && TouchConfirmation.Usable(cardHolder) && ReferenceEquals(card, cardHolder.CardModel));
    }

    private static bool ShopSelected(NMerchantSlot __instance, ref Task __result, MethodBase __originalMethod)
    {
        var entry = __instance.Entry;
        if (!TouchRuntime.Active || TouchConfirmation.Submitting || entry == null || !entry.IsStocked) return true;
        if (!entry.EnoughGold)
        {
            // STS1 reports insufficient gold on the first tap. Let the native
            // purchase validation produce its usual feedback without a confirm UI.
            TouchConfirmation.Clear();
            return true;
        }
        int cost = entry.Cost;
        TouchConfirmation.Stage(__instance,
            () => ObserveTask((Task)__originalMethod.Invoke(__instance, null)!),
            () => TouchConfirmation.Usable(__instance) && ReferenceEquals(entry, __instance.Entry) && entry.IsStocked && entry.EnoughGold && entry.Cost == cost);
        __result = Task.CompletedTask;
        return false;
    }

    private static bool ChoiceCardSelected(NChooseACardSelectionScreen __instance, NCardHolder cardHolder,
        bool ____screenComplete, ulong ____openedTicks)
    {
        if (!TouchRuntime.Active || TouchConfirmation.Submitting) return true;
        // Preserve the native opening guard: the release that opened the screen
        // cannot select its first card. Discovery choices use STS1's reward flow.
        if (____screenComplete || Time.GetTicksMsec() - ____openedTicks <= 350) return false;
        var card = cardHolder.CardModel;
        return !TouchConfirmation.Stage(cardHolder,
            () => __instance.Call("SelectHolder", cardHolder),
            () => ChoiceAvailable(__instance) && TouchConfirmation.Usable(cardHolder) && ReferenceEquals(card, cardHolder.CardModel));
    }

    private static bool ChoiceRelicSelected(NChooseARelicSelection __instance, NRelicBasicHolder relicHolder,
        bool ____screenComplete)
    {
        if (!TouchRuntime.Active || TouchConfirmation.Submitting) return true;
        if (____screenComplete) return false;
        var relic = relicHolder.Relic.Model;
        return !TouchConfirmation.Stage(relicHolder,
            () => __instance.Call("SelectHolder", relicHolder),
            () => ChoiceAvailable(__instance) && TouchConfirmation.Usable(relicHolder) && ReferenceEquals(relic, relicHolder.Relic.Model));
    }

    private static bool ChoiceAvailable(Control screen) => TouchConfirmation.Usable(screen) &&
        !screen.Get("_screenComplete").AsBool();

    private static bool RestSelected(NRestSiteButton __instance, RestSiteOption option, ref Task __result, MethodBase __originalMethod)
    {
        if (!TouchConfirmation.Stage(__instance,
            () => ObserveTask((Task)__originalMethod.Invoke(__instance, [option])!),
            () => TouchConfirmation.Usable(__instance) && __instance.IsEnabled && option.IsEnabled && ReferenceEquals(__instance.Option, option))) return true;
        __result = Task.CompletedTask;
        return false;
    }

    private static async void ObserveTask(Task task)
    {
        try { await task; }
        catch (Exception error) { GD.PushError($"[TouchSts2] Native confirmed operation failed: {error}"); }
    }

    private static bool ClearCardTips(NCardHolder __instance) => !TouchRuntime.KeepCardTips(__instance);
    private static void MenuReady(NMainMenuTextButton __instance) => TouchUi.RegisterMenuButton(__instance);

    private static bool StartCard(NHandCardHolder holder, ref bool startedViaShortcut)
    {
        if (!TouchRuntime.Active || startedViaShortcut && !TouchRuntime.SwitchingCard) return true;
        if (!TouchRuntime.CanGrab(holder)) return false;
        // The native shortcut route is only a transport: our gesture policy supports both
        // drag/release and tap/tap. It avoids native auto-play on the initial touch.
        startedViaShortcut = true;
        _startingHolder = holder;
        return true;
    }

    private static void EndStartCard() => _startingHolder = null;

    private static void CardCreated(NHandCardHolder holder, NMouseCardPlay __result)
    {
        if (ReferenceEquals(holder, _startingHolder)) TouchRuntime.Attach(__result);
    }

    private static bool CardInput(NMouseCardPlay __instance, InputEvent inputEvent, ref bool ____isLeftMouseDown)
    {
        bool owned = TouchRuntime.Owns(__instance);
        bool consumed = TouchRuntime.Observe(inputEvent);
        if (owned)
        {
            // Keep this latched through the native async continuation. A synthetic down/up
            // in one frame would be missed by MultiCreatureTargeting's polling loop.
            ____isLeftMouseDown = TouchRuntime.CommitRequested;
            if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left }) consumed = true;
        }
        if (consumed) __instance.GetViewport().SetInputAsHandled();
        return !consumed;
    }

    private static bool TargetInput(NTargetManager __instance, InputEvent inputEvent)
    {
        if (TouchRuntime.Submitting) return true;
        bool owned = TouchRuntime.HasCard;
        bool consumed = TouchRuntime.Observe(inputEvent);
        if (owned && inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left }) consumed = true;
        if (consumed) __instance.GetViewport().SetInputAsHandled();
        return !consumed;
    }

    private static bool PlayZone(NMouseCardPlay __instance, ref bool __result)
    {
        if (!TouchRuntime.Owns(__instance)) return true;
        __result = TouchRuntime.CommitRequested || TouchRuntime.PointerInDropZone;
        return false;
    }

    private static bool CancelZone(NMouseCardPlay __instance, ref bool __result)
    {
        if (!TouchRuntime.Owns(__instance)) return true;
        // Cancellation uses the finger gesture, never the parked hardware cursor.
        __result = false;
        return false;
    }

    private static bool CardPosition(NHandCardHolder __instance, ref Vector2 ____targetPosition,
        ref CancellationTokenSource? ____positionCancelToken)
    {
        if (!TouchRuntime.Owns(__instance) || TouchRuntime.CommitRequested) return true;
        // Native SetTargetPosition eases local Position and is also called by
        // LerpToMouse. Own the position while dragging so both callers are idempotent.
        ____positionCancelToken?.Cancel();
        ____positionCancelToken = null;
        var parent = __instance.GetParent<CanvasItem>().GetGlobalTransformWithCanvas();
        ____targetPosition = parent.AffineInverse() * TouchRuntime.CardPosition;
        __instance.Position = ____targetPosition;
        if (!__instance.Hitbox.IsEnabled) __instance.Hitbox.SetEnabled(true);
        return false;
    }

    private static void SettingsReady(NInputSettingsPanel __instance)
    {
        try { SettingsUi.AddTo(__instance); }
        catch (Exception error) { GD.PushError($"[TouchSts2] Could not add input setting: {error}"); }
    }
}
