namespace TouchSts2.Interaction;

/// <summary>Shared tap/drag/hold arbitration. Distance is maximum excursion, not final displacement.</summary>
internal sealed class TouchPress(float x, float y, double time, float scale)
{
    public bool Dragged { get; private set; }
    public bool Held { get; private set; }
    public void Move(float px, float py)
    {
        if (MathF.Pow(px - x, 2) + MathF.Pow(py - y, 2) >= MathF.Pow(14 * scale, 2)) Dragged = true;
    }
    public bool TryHold(double now)
    {
        if (Dragged || Held || now - time < .55) return false;
        Held = true;
        return true;
    }
    public bool IsTap => !Dragged && !Held;
}
