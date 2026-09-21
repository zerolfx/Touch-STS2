using TouchSts2.Interaction;
using TouchSts2.Localization;

int checks = 0;
void Equal<T>(T expected, T actual, string name)
{
    checks++;
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"{name}: expected {expected}, got {actual}");
}

void Near(float expected, float actual, string name)
{
    checks++;
    if (MathF.Abs(expected - actual) > .001f)
        throw new Exception($"{name}: expected {expected}, got {actual}");
}

foreach (var (width, height) in new[] { (1920f, 1080f), (1920f, 1200f), (1280f, 800f), (2560f, 1080f) })
{
    float scale = TouchGesture.Scale(width, height);
    float handY = height - 80 * scale;
    float dropY = height - 450 * scale;
    float cancelY = height - 20 * scale;
    var gesture = new TouchGesture();
    gesture.Press(400 * scale, handY);
    Equal(ReleaseIntent.Inspect, gesture.Release(400 * scale, handY, height, scale, true, false), "first tap selects attack");
    Equal(false, gesture.IsDown, "release clears down");
    gesture.Press(900 * scale, dropY);
    Equal(ReleaseIntent.Commit, gesture.Release(900 * scale, dropY, height, scale, true, true), "tap enemy commits");
    Equal(ReleaseIntent.Inspect, gesture.Release(900 * scale, dropY, height, scale, true, true), "duplicate release cannot commit");

    gesture = new TouchGesture();
    gesture.Press(400 * scale, handY);
    gesture.Move(900 * scale, dropY, height, scale);
    Equal(ReleaseIntent.Commit, gesture.Release(900 * scale, dropY, height, scale, true, true), "drag enemy commits on first release");

    gesture = new TouchGesture();
    gesture.Press(400 * scale, handY);
    Equal(ReleaseIntent.Commit, gesture.Release(600 * scale, dropY, height, scale, false, false), "defend drag commits without target");

    gesture = new TouchGesture();
    gesture.Press(400 * scale, handY);
    Equal(ReleaseIntent.Cancel, gesture.Release(600 * scale, dropY, height, scale, true, false), "drag attack into empty space cancels");

    gesture = new TouchGesture();
    gesture.Press(400 * scale, handY);
    gesture.Release(400 * scale, handY, height, scale, false, false);
    gesture.Press(400 * scale, dropY);
    Equal(ReleaseIntent.Commit, gesture.Release(400 * scale, dropY, height, scale, false, false), "defend second tap commits");

    gesture = new TouchGesture();
    gesture.Press(400 * scale, handY);
    gesture.Release(400 * scale, handY, height, scale, false, false);
    gesture.Press(400 * scale, handY);
    Equal(ReleaseIntent.Cancel, gesture.Release(400 * scale, handY, height, scale, false, false), "second tap in hand cancels");

    gesture = new TouchGesture();
    gesture.Press(400 * scale, handY);
    gesture.Move(400 * scale, dropY, height, scale);
    Equal(ReleaseIntent.Cancel, gesture.Release(400 * scale, cancelY, height, scale, false, false), "drag back to bottom cancels");

    gesture = new TouchGesture();
    gesture.Press(400 * scale, cancelY);
    Equal(ReleaseIntent.Inspect, gesture.Release(400 * scale, cancelY, height, scale, true, false), "picking up near bottom does not auto-cancel");

    gesture = new TouchGesture();
    gesture.Press(400 * scale, handY);
    Equal(ReleaseIntent.Inspect, gesture.Release(408 * scale, handY - 4 * scale, height, scale, false, false), "finger jitter is not a drag");

    gesture = new TouchGesture();
    gesture.Press(400 * scale, dropY);
    Equal(ReleaseIntent.Inspect, gesture.Release(400 * scale, dropY, height, scale, false, false), "raised card first tap cannot auto-play");

    Equal(false, TouchGesture.InDropZone(height * 0.1f, height, scale), "top panel is outside non-target drop zone");
    Equal(false, TouchGesture.InDropZone(height - 350 * scale, height, scale), "drop threshold is strict");
    Equal(true, TouchGesture.InDropZone(height - 351 * scale, height, scale), "above drop threshold is playable");
    Equal(false, TouchGesture.InDropZone(handY, height, scale), "hand is not play zone");

    gesture = new TouchGesture();
    gesture.Press(400 * scale, handY);
    gesture.Move(900 * scale, dropY, height, scale);
    gesture.Press(900 * scale, dropY);
    Equal(true, gesture.HasDragged, "duplicate down preserves drag");
    Equal(ReleaseIntent.Commit, gesture.Release(900 * scale, dropY, height, scale, true, true), "duplicate down cannot suppress drag commit");

    gesture = new TouchGesture();
    gesture.Press(400 * scale, handY);
    gesture.Press(400 * scale, handY);
    Equal(ReleaseIntent.Inspect, gesture.Release(400 * scale, handY, height, scale, false, false), "duplicate down is not second tap");
    gesture.Move(900 * scale, dropY, height, scale);
    Equal(false, gesture.HasDragged, "hover after release is not dragging");
    Equal(ReleaseIntent.Inspect, gesture.Release(900 * scale, dropY, height, scale, false, false), "stray release after hover cannot play");

    gesture = new TouchGesture();
    gesture.Press(400 * scale, handY);
    gesture.Move(900 * scale, dropY, height, scale);
    gesture.Move(400 * scale, handY, height, scale);
    Equal(ReleaseIntent.Cancel, gesture.Release(400 * scale, handY, height, scale, true, false), "returning to drag origin cancels rather than inspecting");

    // Regression: target cards must stop following the finger after entering aim mode.
    gesture = new TouchGesture();
    gesture.Press(400 * scale, handY);
    gesture.UpdateAim(height, scale, true, false);
    Equal(false, gesture.IsAiming, "first tap starts as inspection");
    Equal((400 * scale, height - 210 * scale), gesture.GetCardPosition(width, height, 400 * scale), "inspection position is below aiming position");
    gesture.Move(420 * scale, height - 160 * scale, height, scale);
    gesture.UpdateAim(height, scale, true, false);
    Equal(false, gesture.IsAiming, "target card follows finger before reaching drop zone");
    var beforeAim = gesture.GetCardPosition(width, height, 400 * scale);
    Near(420 * scale, beforeAim.X, "pre-aim horizontal movement is one-to-one");
    Near(height - 290 * scale, beforeAim.Y, "pre-aim keeps pickup offset without a second lift");
    gesture.Move(900 * scale, dropY, height, scale);
    gesture.UpdateAim(height, scale, true, false);
    Equal(true, gesture.IsAiming, "target card enters aim at drop zone");
    Equal((width / 2, height - 260 * scale), gesture.GetCardPosition(width, height, 400 * scale), "aim uses STS1 PC touch anchor");
    gesture.Move(width - 40 * scale, 100 * scale, height, scale);
    gesture.UpdateAim(height, scale, true, false);
    Equal((width / 2, height - 260 * scale), gesture.GetCardPosition(width, height, 400 * scale), "aimed card never follows finger to screen top");
    gesture.Move(100 * scale, handY, height, scale);
    gesture.UpdateAim(height, scale, true, false);
    Equal((width / 2, height - 260 * scale), gesture.GetCardPosition(width, height, 400 * scale), "aim is latched when moving back toward hand");
    Equal(ReleaseIntent.Cancel, gesture.Release(100 * scale, cancelY, height, scale, true, false), "anchoring preserves bottom cancellation");

    gesture = new TouchGesture();
    Equal(false, gesture.IsAiming, "new card clears previous aim latch");
    gesture.Press(400 * scale, handY);
    gesture.Move(900 * scale, dropY, height, scale);
    gesture.UpdateAim(height, scale, false, true);
    Equal(false, gesture.IsAiming, "non-target card never anchors even over a creature");
    var nonTarget = gesture.GetCardPosition(width, height, 400 * scale);
    Near(900 * scale, nonTarget.X, "non-target horizontal position");
    Near(height - 210 * scale + dropY - handY, nonTarget.Y, "non-target drag preserves pickup offset");
    Equal(ReleaseIntent.Commit, gesture.Release(900 * scale, dropY, height, scale, false, false), "non-target drag still submits");

    gesture = new TouchGesture();
    gesture.Press(400 * scale, handY);
    gesture.Release(400 * scale, handY, height, scale, true, false);
    gesture.Press(900 * scale, dropY);
    gesture.UpdateAim(height, scale, true, true);
    Equal(true, gesture.IsAiming, "second tap on target enters aim without requiring drag");
    Equal(ReleaseIntent.Commit, gesture.Release(900 * scale, dropY, height, scale, true, true), "anchoring preserves tap target submission");

    gesture = new TouchGesture();
    gesture.Press(400 * scale, handY);
    gesture.Move(900 * scale, handY, height, scale);
    gesture.UpdateAim(height, scale, true, true);
    Equal(true, gesture.IsAiming, "low creature hitbox can enter aim outside the geometric drop zone");

    gesture = new TouchGesture();
    gesture.Press(400 * scale, dropY);
    gesture.UpdateAim(height, scale, true, true);
    Equal(false, gesture.IsAiming, "raised first stationary tap remains inspection");
    gesture.Release(400 * scale, dropY, height, scale, true, true);
    gesture.Move(900 * scale, dropY, height, scale);
    gesture.UpdateAim(height, scale, true, true);
    Equal(false, gesture.IsAiming, "released hover cannot start aiming");

    // Co-op sidebar rows can sit above the ordinary card play zone. Legality is
    // supplied by the native node/card filters; geometry must not reject allies.
    float sidebarX = 90 * scale, sidebarY = 120 * scale;
    Equal(false, TouchGesture.InDropZone(sidebarY, height, scale), "sidebar can be outside normal drop zone");
    gesture = new TouchGesture();
    gesture.Press(400 * scale, handY);
    gesture.Move(sidebarX, sidebarY, height, scale);
    gesture.UpdateAim(height, scale, true, true);
    Equal(true, gesture.IsAiming, "legal teammate row enters targeting above play zone");
    Equal(ReleaseIntent.Commit, gesture.Release(sidebarX, sidebarY, height, scale, true, true), "drag onto legal teammate row commits");
    Equal(ReleaseIntent.Inspect, gesture.Release(sidebarX, sidebarY, height, scale, true, true), "duplicate ally release cannot commit twice");

    gesture = new TouchGesture();
    gesture.Press(400 * scale, handY);
    gesture.Release(400 * scale, handY, height, scale, true, false);
    gesture.Press(sidebarX, sidebarY);
    gesture.UpdateAim(height, scale, true, true);
    Equal(ReleaseIntent.Commit, gesture.Release(sidebarX, sidebarY, height, scale, true, true), "tap card then teammate row commits");

    foreach (string rejected in new[] { "enemy-only card on ally", "self excluded", "dead teammate", "target removed before release" })
    {
        gesture = new TouchGesture();
        gesture.Press(400 * scale, handY);
        gesture.Move(sidebarX, sidebarY, height, scale);
        gesture.UpdateAim(height, scale, true, true);
        Equal(ReleaseIntent.Cancel, gesture.Release(sidebarX, sidebarY, height, scale, true, false), rejected);
    }

    // Move equal distances on both sides of drag slop, then past the former top
    // clamp. Picking up off-center must never snap X onto the finger either.
    foreach (float pickupOffset in new[] { -60f, 0f, 80f })
    {
        gesture = new TouchGesture();
        float startX = 400 * scale + pickupOffset * scale;
        gesture.Press(startX, handY);
        var previous = gesture.GetCardPosition(width, height, 400 * scale);
        float previousX = startX, previousY = handY;
        foreach (var (dx, dy) in new[] { (3f, -4f), (6f, -8f), (9f, -12f), (90f, -120f), (-50f, -900f), (0f, 0f) })
        {
            float x = startX + dx * scale, y = handY + dy * scale;
            gesture.Move(x, y, height, scale);
            var next = gesture.GetCardPosition(width, height, 400 * scale);
            Near(x - previousX, next.X - previous.X, "card X displacement equals finger displacement");
            Near(y - previousY, next.Y - previous.Y, "card Y displacement equals finger displacement");
            previous = next; previousX = x; previousY = y;
        }
        gesture.Release(startX, handY, height, scale, false, false);
        gesture.Press(startX + 20 * scale, handY - 40 * scale);
        var regrab = gesture.GetCardPosition(width, height, 400 * scale);
        Near(400 * scale, regrab.X, "new press resets horizontal grab offset");
        Near(height - 210 * scale, regrab.Y, "new press resets vertical grab offset");
    }
}

Equal(2f / 3f, TouchGesture.Scale(1280, 800), "Deck scales by width, not height");
foreach (float scale in new[] { 2f / 3f, 1f, 1.5f })
{
    var press = new TouchPress(100, 100, 0, scale);
    press.Move(100 + 5 * scale, 100);
    Equal(true, press.IsTap, "finger jitter remains a tap");
    Equal(false, press.TryHold(.54), "hold does not trigger early");
    Equal(true, press.TryHold(.56), "stationary hold triggers inspection");
    Equal(false, press.TryHold(1), "hold fires once");
    Equal(false, press.IsTap, "hold cannot also select card");
    press = new TouchPress(100, 100, 0, scale);
    press.Move(100, 100 + 20 * scale);
    press.Move(100, 100);
    Equal(false, press.IsTap, "scroll back to origin still cannot click");
    Equal(false, press.TryHold(1), "scroll cannot open inspector");

    press = new TouchPress(100, 100, 0, scale);
    Equal(true, press.TryHold(.55), "reward preview activates at hold threshold");
    Equal(false, press.IsTap, "preview release cannot also claim reward");
    press.Move(100, 100 + 50 * scale);
    press.Move(100, 100);
    Equal(false, press.IsTap, "moving away and back after preview cannot claim");
    Equal(false, press.TryHold(2), "preview is not repeatedly opened while held");
    press = new TouchPress(100, 100, 3, scale);
    Equal(false, press.TryHold(3.2), "fresh short tap does not preview");
    Equal(true, press.IsTap, "separate short tap after preview can claim normally");
}
Equal(1f, TouchGesture.Scale(2560, 1080), "ultrawide scales by height");
Console.WriteLine($"PASS: {checks} gesture checks across four viewport sizes.");

int gestureChecks = checks;
Equal(16, TouchText.Languages.Count(), "all current game languages included");
foreach (var language in TouchText.Languages)
    foreach (var key in new[] { "TouchscreenMode", "HoldToInspect", "TouchFeedback" })
    {
        Equal(false, string.IsNullOrWhiteSpace(TouchText.Get(language, key)), $"nonempty {language}/{key}");
        Equal(false, TouchText.Get(language, key).Contains(" / "), $"single language {language}/{key}");
    }
LocaleExpectations.Verify(Equal);
Equal("Touchscreen mode", TouchText.Get("unknown", "TouchscreenMode"), "unknown locale falls back to English");
Equal("Touchscreen mode", TouchText.Get(null, "TouchscreenMode"), "unavailable locale falls back to English");
Console.WriteLine($"PASS: {checks - gestureChecks} localization checks.");

int cursorChecks = checks;
var cursor = new TouchCursorFeedback();
Equal(0f, cursor.Alpha, "cursor starts invisible");
Equal(32, TouchCursorFeedback.Size, "STS1 source region is 32 pixels");
cursor.Button(true, 120, 240);
Equal(.7f, cursor.Alpha, "physical down starts at STS1 alpha");
Equal(-6f, cursor.RotationDegrees, "pressed rotation accounts for inverted Y axis");
cursor.Tick(1f / 60);
Near(.665f, cursor.Alpha, "STS1 first rendered frame at 60 Hz");
cursor.Button(true, 140, 250);
Near(.665f, cursor.Alpha, "duplicate down cannot relight cursor");
cursor.Move(400, 300);
Equal(400f, cursor.X, "drag follows pointer X");
Equal(300f, cursor.Y, "drag follows pointer Y");
cursor.Button(false, 400, 300);
Equal(0f, cursor.RotationDegrees, "release removes rotation");
Near(.665f, cursor.Alpha, "release does not reset fade");
cursor.Tick(1f / 60);
Near(.63175f, cursor.Alpha, "release continues same fade");
cursor.Move(900, 500);
Equal(900f, cursor.X, "residual cursor follows pointer after release");
cursor.Button(true, 900, 500);
Equal(.7f, cursor.Alpha, "next press relights the same cursor");
cursor.Hide();
Equal(0f, cursor.Alpha, "neutral cursor warp hides feedback immediately");
Equal(true, cursor.IsDown, "neutral warp does not invent a release");
cursor.Reset();
Equal(false, cursor.IsDown, "cancel clears pressed state");
foreach (var (fps, frames) in new[] { (30, 41), (60, 83), (144, 202) })
{
    cursor.Reset();
    cursor.Button(true, 0, 0);
    for (int frame = 1; frame < frames; frame++) cursor.Tick(1f / fps);
    Equal(true, cursor.Alpha >= .01f, $"{fps} Hz stays visible before STS1 snap threshold");
    cursor.Tick(1f / fps);
    Equal(0f, cursor.Alpha, $"{fps} Hz snaps to invisible at STS1 threshold");
    Equal(true, cursor.IsDown, "holding does not prevent fade out");
    cursor.Button(true, 20, 20);
    Equal(0f, cursor.Alpha, "repeated held event does not revive faded cursor");
}
Console.WriteLine($"PASS: {checks - cursorChecks} STS1 cursor checks at 30/60/144 Hz.");
ConfirmationChecks.Run(Equal);

if (args.Length >= 2)
{
    string gameData = Path.GetFullPath(args[0]);
    System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (_, name) =>
    {
        string path = Path.Combine(gameData, name.Name + ".dll");
        return File.Exists(path) ? System.Reflection.Assembly.LoadFrom(path) : null;
    };
    var mod = System.Reflection.Assembly.LoadFrom(Path.GetFullPath(args[1]));
    using var orb = mod.GetManifestResourceStream("TouchSts2.sts1.orb.png")
        ?? throw new Exception("Release DLL is missing the STS1 cursor texture.");
    Equal("AB6A47EE0F8742873BC8F17765BF052C3F21EF3AA1A1698EC1188D412242536B",
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(orb)), "exact STS1 PC cursor asset embedded");
    Console.WriteLine("PASS: embedded cursor texture matches STS1 PC SHA-256.");
    var validate = mod.GetType("TouchSts2.GameAdapter", true)!.GetMethod("ValidateContract",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
    var seams = (string[])validate.Invoke(null, null)!;
    Console.WriteLine($"PASS: {seams.Length} game API patch targets, mouse/position fields, scroll contracts, native target property and reward preview entry resolved.");
    using var strings = mod.GetManifestResourceStream("TouchSts2.Localization.strings.json")
        ?? throw new Exception("Release DLL is missing localization resources.");
    using var translations = System.Text.Json.JsonDocument.Parse(strings);
    Equal(16, translations.RootElement.EnumerateObject().Count(), "release DLL embeds all translations");
    Console.WriteLine("This is a static API check, not proof that in-game input works.");
    if (args.Length == 3) SettingsAdapterChecks.VerifyInstalledPlugins(args[2]);
}
