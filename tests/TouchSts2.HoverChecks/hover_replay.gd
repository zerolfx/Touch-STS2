extends SceneTree

# Engine-level regression: a deferred click replaces the UI after physical input
# has ended. A covered card's hover tooltip must leave without another user action.
# This isolates Godot dispatch; it does not load the mod or the game's card scenes.
var viewport: SubViewport
var card: Control
var overlay: Control
var tip_visible := false
var preview_tip_visible := false
var clicks := 0
var overlay_clicks := 0
var checks := 0
var failures := 0

func _initialize():
    call_deferred("run")

func check(condition: bool, message: String):
    checks += 1
    if not condition:
        failures += 1
        push_error(message)

func button(pressed: bool):
    var event := InputEventMouseButton.new()
    event.button_index = MOUSE_BUTTON_LEFT
    event.pressed = pressed
    event.position = Vector2(100, 100)
    event.global_position = event.position
    viewport.push_input(event, true)

func motion():
    var event := InputEventMouseMotion.new()
    event.position = Vector2(100, 100)
    event.global_position = event.position
    viewport.push_input(event, true)

func run():
    print("Hover checks engine: ", Engine.get_version_info().string)
    viewport = SubViewport.new()
    viewport.size = Vector2i(800, 600)
    root.add_child(viewport)
    viewport.notify_mouse_entered()
    card = Control.new()
    card.size = Vector2(200, 200)
    viewport.add_child(card)
    overlay = Control.new()
    overlay.size = Vector2(800, 600)
    overlay.hide()
    viewport.add_child(overlay)
    card.mouse_entered.connect(func(): tip_visible = true)
    card.mouse_exited.connect(func(): tip_visible = false)
    overlay.mouse_entered.connect(func(): preview_tip_visible = true)
    overlay.mouse_exited.connect(func(): preview_tip_visible = false)
    card.gui_input.connect(func(event):
        if event is InputEventMouseButton and not event.pressed:
            clicks += 1
            overlay.show()
    )
    overlay.gui_input.connect(func(event):
        if event is InputEventMouseButton:
            overlay_clicks += 1
    )
    motion()
    check(tip_visible, "Initial hover must show the card tooltip")
    # Let all physical input finish before replaying the captured tap.
    call_deferred("replay")

func replay():
    button(true)
    button(false)
    check(overlay.visible and clicks == 1, "A tap must open the preview exactly once")
    check(tip_visible, "The unrefreshed path must reproduce the covered tooltip")
    motion()
    check(not tip_visible, "Hover refresh must clear the covered tooltip")
    check(preview_tip_visible, "The new preview must receive native hover")
    check(clicks == 1 and overlay_clicks == 0, "Hover refresh must not click through")
    motion()
    check(not tip_visible and preview_tip_visible and overlay_clicks == 0,
        "Repeated refresh must preserve hover without another selection")
    overlay.hide()
    motion()
    check(tip_visible and not preview_tip_visible, "Returning must restore the original hover")
    print("Hover checks: ", checks, " checks, ", failures, " failures")
    quit(0 if failures == 0 else 1)
