namespace TouchSts2.Interaction;

/// <summary>A local preview has no side effects until its valid action is taken once.</summary>
internal sealed class PendingConfirmation
{
    private (Action Submit, Func<bool> Valid, Action? Cleanup)? _choice;
    public bool Pending => _choice != null;
    public bool IsValid => _choice is { } choice && choice.Valid();

    public bool Stage(Action submit, Func<bool> valid, Action? cleanup = null)
    {
        Clear();
        if (!valid()) return false;
        _choice = (submit, valid, cleanup);
        return true;
    }

    public Action? Take()
    {
        var action = IsValid ? _choice?.Submit : null;
        Clear();
        return action;
    }

    public void Clear()
    {
        var cleanup = _choice?.Cleanup;
        _choice = null;
        cleanup?.Invoke();
    }
}
