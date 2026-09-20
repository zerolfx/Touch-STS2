using Godot;
using MegaCrit.Sts2.Core.Nodes;
using TouchSts2.Interaction;

namespace TouchSts2;

internal static class TouchCursor
{
    private static readonly TouchCursorFeedback Feedback = new();
    private static Sprite2D? _sprite;

    public static void Move(Vector2 point) => Feedback.Move(point.X, point.Y);
    public static void Button(bool down, Vector2 point) => Feedback.Button(down, point.X, point.Y);

    public static void Tick(float delta)
    {
        if (!TouchRuntime.UseTouchCursor || !TouchSettings.TouchFeedback || NGame.Instance == null || !NGame.IsGameFocusedWindow())
        {
            Reset();
            return;
        }
        Feedback.Tick(delta);
        if (Feedback.Alpha == 0) { Hide(); return; }
        if (!GodotObject.IsInstanceValid(_sprite))
        {
            using var stream = typeof(TouchCursor).Assembly.GetManifestResourceStream("TouchSts2.sts1.orb.png")
                ?? throw new InvalidOperationException("Missing STS1 cursor texture.");
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            using var image = new Image();
            if (image.LoadPngFromBuffer(bytes.ToArray()) != Error.Ok)
                throw new InvalidOperationException("Invalid STS1 cursor texture.");
            // A single visual above screen UI, with no Control to intercept input.
            var layer = new CanvasLayer { Name = "TouchCursor", Layer = 100 };
            _sprite = new Sprite2D
            {
                Texture = ImageTexture.CreateFromImage(image),
                TextureFilter = CanvasItem.TextureFilterEnum.Linear,
                RegionEnabled = true,
                RegionRect = new Rect2(0, 0, TouchCursorFeedback.Size, TouchCursorFeedback.Size),
                Centered = true
            };
            layer.AddChild(_sprite);
            NGame.Instance.AddChild(layer);
        }
        var size = NGame.Instance.GetViewport().GetVisibleRect().Size;
        _sprite!.Scale = Vector2.One * TouchGesture.Scale(size.X, size.Y);
        _sprite.Position = new Vector2(Feedback.X, Feedback.Y);
        _sprite.RotationDegrees = Feedback.RotationDegrees;
        _sprite.Modulate = new Color(1, 1, 1, Feedback.Alpha);
        _sprite.Visible = true;
    }

    public static void Hide()
    {
        Feedback.Hide();
        if (GodotObject.IsInstanceValid(_sprite)) _sprite!.Visible = false;
    }

    public static void Reset() { Feedback.Reset(); Hide(); }
}
