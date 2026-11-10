# Desktop WebGL exploration

The enabled `Osteon` scene also supports a desktop WebGL player.
`AnatomyDesktopInput` starts only when `Application.platform` is `WebGLPlayer`;
Windows Editor Play Mode and Quest keep their existing input paths. The browser
build uses the same G0–G3 anatomy, information panel, Spread sliders, Settings,
quiz station, and Field Guide. See [Setup](SETUP.md) for shared prerequisites.

## Build and open

1. Open `Assets/Scenes/Osteon.unity`.
2. In **File → Build Profiles**, select `Web - Desktop - Development`. It
   inherits the global scene list, whose only enabled scene is `Osteon`.
3. Use **Tools → Osteon → Build Optimized WebGL** for account/browser testing.
   It writes `Builds/Web Desktop Optimized/` with size optimization and LTO, no
   development symbols, a 128 MiB initial heap, and uncompressed files for the
   local server. It restores previous Editor settings and temporarily excludes
   the private quiz demo configuration from the player.
4. **Build and Run** remains available for debugging.

`Builds/` is ignored by Git. The `Meta Quest` profile remains the Android path;
headset WebXR behavior is not established.

## Hosted build

The optimized build is hosted at <https://d1fz5khe940op0.cloudfront.net/>
through CloudFront with a private S3 origin. Direct public S3 access returns 403.
Accounts and quiz APIs allow this origin for the required methods and headers.

To update it, run **Tools → Osteon → Build Optimized WebGL**, check the fresh
`Logs/WebDesktop-build.txt` result, and upload the contents of
`Builds/Web Desktop Optimized/` to the site bucket. Preserve the previous objects
under a dated `_rollback/` prefix, then invalidate CloudFront; current objects
have a five-minute cache lifetime.

Recent optimized builds succeed with zero errors and roughly 400 warnings, and
the room has loaded in the Codex in-app browser and Firefox. The owner reports a
G0 → G3 → Back route, sign-in, one quiz answer, and cross-device resume. These
are partial checks, not full browser acceptance.

## Local server

Run `./Tools/Start-WebDesktop.ps1` from PowerShell to serve
`Builds/Web Desktop Optimized/` at **http://localhost:8000**. The background
Python server binds only to loopback and serves the build directory, not the
repository. It reports its PID; stop it with
`Stop-Process -Id <reported PID>`. Logs are under
`Logs/web-desktop-8000.*.log`. Restart the command after a machine restart.

The launcher serves uncompressed output and refuses compressed builds. Configure
an appropriate server before switching compression.

For API CORS, allow the exact origin `http://localhost:8000`, methods GET and
POST, and headers `authorization`, `content-type`, and `x-unity-version`. Leave
Allow credentials disabled; the client sends a bearer header, not cookies. API
Gateway handles OPTIONS preflight. Open the site through `localhost`, not
`127.0.0.1` or another port, to match the configured origin.

The older development WASM module (73,230,658 bytes, large debug name section)
triggered a Firefox `InternalError: out of memory` during compilation. The
optimized build is about 29 MB with the debug section removed; prefer it for
acceptance. The failure was not conclusively diagnosed.

## Controls

| Action | Mouse and keyboard |
|---|---|
| Select a division, group, bone, panel button, or slider | Hover and left click with a free cursor |
| Move a panel | Drag its blue **Move** bar with the left button |
| Look around | Press **F**, or click empty scene space, to capture the mouse; press **F**, **Tab**, or **Esc** to free it |
| Move through the room | **W/A/S/D**; **Space** rises, **C** lowers, **Shift** increases speed |
| Rotate the current G0–G2 model | Right drag horizontally, or **Left/Right Arrow** |
| Turn and tilt a G3 bone | Right drag or arrow keys; **R** resets turn, **T** resets tilt |
| Orbit / pan / zoom | **Shift + right drag** / middle drag / mouse wheel away from UI |
| Switch pectoral G2 side | **Y** |
| Go back one level | **Backspace** or the panel's **Back** button |

Mouse input for panels is disabled while the cursor is captured. Free the cursor
before clicking a bone, panel control, or Move bar. The Field Guide shows desktop
prompts in WebGL.

## Browser acceptance pass

- [ ] Load the hospital room, full skeleton, panels, and Field Guide in the
      intended desktop browsers.
- [ ] Complete one G0 → G1 → G2 → G3 route with the mouse: hover, information,
      turn/tilt, resets, and Back to G0.
- [ ] Exercise mouse look, movement, orbit, pan, zoom, and cursor release; panel
      controls still receive clicks after mouse look.
- [ ] Drag each Move bar, use each multi-bone Spread slider, switch pectoral
      sides, and check Settings persistence after reload.
- [ ] Sign in through Accounts, then check quiz start, answer feedback, Next,
      resume, switch, expiry, and the quiz API's CORS success/error responses.
      See [Quiz integration](QUIZ_INTEGRATION.md).
- [ ] Confirm **Read / Stop** plays bundled anatomy, quiz, and Settings
      recordings and stops when content changes. Text without a recording has no
      browser speech fallback. See [Panel narration](TTS.md).

This checklist is an acceptance target, not a completed test record. Build
success and room loading do not establish that controls, API calls, layout, or
performance passed.
