namespace TouchSts2.Interaction;

public enum ReleaseIntent { Inspect, Commit, Cancel }

/// <summary>Pure gesture policy. Coordinates have a top-left origin, in viewport units.</summary>
public sealed class TouchGesture
{
    public const float InspectHeight = 210;
    public const float AimHeight = 260;
    public const float DropHeight = 350;
    public const float CancelHeight = 50;
    public const double RegrabDelay = 0.25;
    public bool IsDown { get; private set; }
    public bool HasReleased { get; private set; }
    public bool HasDragged { get; private set; }
    public bool HasLeftBottom { get; private set; }
    public bool IsAiming { get; private set; }
    public float X { get; private set; }
    public float Y { get; private set; }
    private float _startX, _startY;

    public static float Scale(float width, float height) => Math.Min(width / 1920f, height / 1080f);
    public static bool InDropZone(float y, float height, float scale) =>
        y < height - DropHeight * scale && y > height * 0.19f;

    public void Press(float x, float y)
    {
        // Touch-to-mouse promotion can repeat a down without an intervening up.
        // Preserve the original drag and never turn that repeat into a second tap.
        if (IsDown) return;
        IsDown = true;
        HasDragged = false;
        _startX = X = x;
        _startY = Y = y;
    }

    public void Move(float x, float y, float height, float scale)
    {
        X = x;
        Y = y;
        if (!IsDown) return;
        HasDragged |= MathF.Pow(x - _startX, 2) + MathF.Pow(y - _startY, 2) > MathF.Pow(12 * scale, 2);
        HasLeftBottom |= y <= height - CancelHeight * scale;
    }

    public void UpdateAim(float height, float scale, bool singleTarget, bool overTarget)
    {
        // STS1 PC: clickAndDragCards enters inSingleTargetMode at the drop zone
        // (or over a target). updateSingleTargetInput then stops moving the card.
        // Keep first stationary taps as inspection, including a raised hand.
        if (singleTarget && IsDown && (HasDragged || HasReleased) && (InDropZone(Y, height, scale) || overTarget))
            IsAiming = true;
    }

    public (float X, float Y) GetCardPosition(float width, float height, float inspectX)
    {
        float scale = Scale(width, height);
        if (IsAiming) return (width / 2, height - AimHeight * scale);
        // Lift once on pickup, then preserve the grab offset. Drag slop controls
        // release intent only; changing pose at that threshold amplifies motion.
        if (IsDown) return (inspectX + (X - _startX), height - InspectHeight * scale + (Y - _startY));
        return (inspectX, height - InspectHeight * scale);
    }

    public ReleaseIntent Release(float x, float y, float height, float scale, bool singleTarget, bool validTarget)
    {
        if (!IsDown) return ReleaseIntent.Inspect; // Duplicate OS release cannot commit.
        Move(x, y, height, scale);
        IsDown = false;
        bool first = !HasReleased;
        HasReleased = true;
        if (HasLeftBottom && y > height - CancelHeight * scale && HasDragged)
            return ReleaseIntent.Cancel;
        // A stationary first tap is always inspection, including a temporarily raised hand.
        if (first && !HasDragged) return ReleaseIntent.Inspect;
        if (singleTarget ? validTarget : InDropZone(y, height, scale))
            return ReleaseIntent.Commit;
        return ReleaseIntent.Cancel;
    }
}
