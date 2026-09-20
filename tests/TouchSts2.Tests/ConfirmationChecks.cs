using TouchSts2.Interaction;

internal static class ConfirmationChecks
{
    public static void Run(Action<bool, bool, string> equal)
    {
        int checks = 0;
        void Check(bool actual, string name) { equal(true, actual, name); checks++; }
        var choice = new PendingConfirmation();
        int spent = 0, cleaned = 0;
        Check(choice.Stage(() => spent++, () => true, () => cleaned++), "stage first preview");
        Check(spent == 0 && choice.Pending, "preview never spends a use or submits a choice");
        choice.Stage(() => spent += 10, () => true, () => cleaned++);
        Check(spent == 0 && cleaned == 1, "replacement cleans only the old preview");
        choice.Take()?.Invoke();
        Check(spent == 10 && cleaned == 2 && !choice.Pending, "only the latest choice submits");
        choice.Take()?.Invoke();
        Check(spent == 10 && cleaned == 2, "duplicate confirmation does nothing");

        foreach (string invalidation in new[] { "tool changed", "uses changed", "cell revealed", "screen closed", "option replaced", "option locked" })
        {
            bool valid = true;
            choice.Stage(() => spent++, () => valid, () => cleaned++);
            valid = false;
            Check(!choice.IsValid, invalidation + " invalidates preview");
            choice.Take()?.Invoke();
            Check(spent == 10 && !choice.Pending, invalidation + " cannot submit");
        }
        Check(!choice.Stage(() => spent++, () => false), "invalid choices are not staged");
        Check(!choice.Pending, "invalid staging leaves no action");
        choice.Stage(() => spent++, () => true, () => cleaned++);
        int beforeClear = cleaned;
        choice.Clear();
        choice.Clear();
        Check(cleaned == beforeClear + 1 && spent == 10, "cancel cleans once without submitting");

        choice.Stage(() => spent++, () => true, () =>
        {
            Check(!choice.Pending, "cleanup cannot see a stale action");
            choice.Take()?.Invoke();
        });
        choice.Take()?.Invoke();
        Check(spent == 11, "cleanup reentrancy cannot submit twice");
        choice.Stage(() => throw new InvalidOperationException("test action"), () => true);
        try { choice.Take()?.Invoke(); }
        catch (InvalidOperationException) { }
        Check(!choice.Pending && choice.Take() == null, "failed action cannot be replayed");
        Console.WriteLine($"PASS: {checks} pending confirmation checks.");
    }
}
