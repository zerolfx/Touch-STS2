namespace TouchSts2.Interaction;

/// <summary>STS1 PC GameCursor / InputHelper timing, in viewport coordinates.</summary>
public sealed class TouchCursorFeedback
{
    public const int Size = 32;
    public float Alpha { get; private set; }
    public bool IsDown { get; private set; }
    public float X { get; private set; }
    public float Y { get; private set; }
    // STS1 rotates counterclockwise; Godot's Y axis points down.
    public float RotationDegrees => IsDown ? -6 : 0;

    public void Move(float x, float y) { X = x; Y = y; }

    public void Button(bool down, float x, float y)
    {
        Move(x, y);
        if (down && !IsDown) Alpha = .7f;
        IsDown = down;
    }

    public void Tick(float delta)
    {
        // MathHelper.slowColorLerpSnap: deliberately not a fixed-duration tween.
        Alpha += (0 - Alpha) * (delta * 3f);
        if (MathF.Abs(Alpha) < .01f) Alpha = 0;
    }

    public void Hide() => Alpha = 0;
    public void Reset() { Hide(); IsDown = false; }
}
