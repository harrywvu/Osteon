# Desktop WebGL exploration

The enabled `CONTROLLERS MIGRATION` scene also has mouse and keyboard controls
for a desktop WebGL player. `AnatomyDesktopInput` starts only in that scene when
`Application.platform` is `WebGLPlayer`; Windows Editor Play Mode and Quest keep
their existing input paths. The browser build uses the same G0–G3 anatomy,
information panel, Spread sliders, Settings, quiz station, and Field Guide.
Browser interaction for those features has not yet received a recorded acceptance
pass.

## Build and open

1. Install **Unity 6000.3.2f1** with **Web Build Support**. Keep Blender 4.5.x
   and Git LFS available for the source asset import; see [Setup](SETUP.md).
2. Open `Assets/_Recovery/CONTROLLERS MIGRATION.unity`.
3. In **File → Build Profiles**, select `Web - Desktop - Development`. It
   inherits the global scene list, whose only enabled scene is the controller
   migration scene.
4. Use **Build and Run** to make and serve a development build in a desktop
   browser. `Builds/` is ignored by Git. A local output exists at
   `Builds/Web Builds/`; its files predate the latest desktop input and profile
   commits, so rebuild before evaluating the current code.

The `Meta Quest` profile remains the Android build path. The WebGL profile is
for desktop browser exploration; headset WebXR behavior is not established.

## Controls

| Action | Mouse and keyboard |
|---|---|
| Select a division, group, or bone; use panel buttons and sliders | Hover and left click while the cursor is free |
| Move a panel | Drag its blue **Move** bar with the left button |
| Look around | Press **F**, or click empty scene space, to capture the mouse; move the mouse to look. Press **F**, **Tab**, or **Esc** to free the cursor for panels and selection |
| Move through the room | **W/A/S/D**; **Space** rises, **C** lowers, and **Shift** increases speed |
| Rotate the current G0–G2 model | Right drag horizontally, or use **Left/Right Arrow** |
| Turn and tilt a G3 bone | Right drag, or use the arrow keys; **R** resets turn and **T** resets tilt |
| Orbit the view / pan / zoom | **Shift + right drag** / middle drag / mouse wheel away from UI |
| Switch the pectoral G2 side | **Y** |
| Go back one anatomy level | **Backspace** or the information panel's **Back** button |

Mouse input for panels is disabled while the cursor is captured. Free the cursor
before clicking a bone, panel control, or Move bar. The Field Guide shows the
desktop prompts when running in WebGL.

## Browser acceptance pass

- [ ] Confirm the build loads the hospital room, full skeleton, panels, and
      Field Guide in the intended desktop browsers.
- [ ] Select one complete G0 → G1 → G2 → G3 route with the mouse. Check hover,
      information, bone turning and tilt, resets, and Back to G0.
- [ ] Exercise mouse look, movement, orbit, pan, zoom, and cursor release. Check
      that panel controls still receive clicks after mouse look.
- [ ] Drag each Move bar, use each multi-bone G2 Spread slider, switch pectoral
      sides, and check Settings persistence after reload.
- [ ] Check quiz requests only against an intentionally configured API with
      browser CORS and suitable authentication. Do not publish a build containing
      the shared demo token; see [Quiz integration](QUIZ_INTEGRATION.md).
- [ ] Confirm **Read / Stop** reports **Audio unavailable** on WebGL; browser
      narration is not implemented. See [Panel narration](TTS.md).

This checklist is an acceptance target, not a completed test record. The local
WebGL output establishes that files were generated from an earlier revision,
but does not establish that the current browser controls, API calls, visual
layout, or performance passed testing.
