# Desktop WebGL exploration

The enabled `Osteon` scene also has mouse and keyboard controls
for a desktop WebGL player. `AnatomyDesktopInput` starts only in that scene when
`Application.platform` is `WebGLPlayer`; Windows Editor Play Mode and Quest keep
their existing input paths. The browser build uses the same G0–G3 anatomy,
information panel, Spread sliders, Settings, quiz station, and Field Guide.
Browser checks cover room loading and selected quiz interactions; the complete
acceptance pass below remains open.

## Build and open

1. Install **Unity 6000.3.2f1** with **Web Build Support**. Keep Blender 4.5.x
   and Git LFS available for the source asset import; see [Setup](SETUP.md).
2. Open `Assets/Scenes/Osteon.unity`.
3. In **File → Build Profiles**, select `Web - Desktop - Development`. It
   inherits the global scene list, whose only enabled scene is `Osteon`.
4. Use **Tools → Osteon → Build Optimized WebGL** for account/browser testing.
   This builds `Builds/Web Desktop Optimized/` with size optimization and LTO,
   no development/debug symbols, a 128 MiB initial heap, and uncompressed files
   for the local server. The recipe restores the previous Editor settings and
   temporarily excludes the private quiz demo configuration from the player.
   `Builds/` is ignored by Git. **Build and Run** remains available for debugging.

The `Meta Quest` profile remains the Android build path. The WebGL profile is
for desktop browser exploration; headset WebXR behavior is not established.

## Hosted desktop build

On 2026-11-10, learner profile input was reproduced in the live WebGL player:
typing changed program search to BS Nursing while the entered characters were
invisible. `OsteonSettings.ApplyThemeTo` was recoloring the input text nearly
white on a white field. The theme now keeps `TMP_InputField` text dark; an
unneeded keyboard fallback was removed. Unity rebuilt the optimized player
with `Succeeded`, zero errors, 419 warnings, and `Options: None`.
CloudFront invalidation `I3GR2C6RLRRUV0WAH9YOUST3A6` completed after upload.
In a fresh browser tab, `Mia` was visibly entered in First name and `nursing`
was visibly entered in program search; the selected program changed to BS
Nursing. No learner account was submitted, so creation and code issuance remain
unverified in this browser pass.

On 2026-11-10, a second optimized WebGL build corrected the learner form's
generated text font, UI layer, and click target. Unity reported `Succeeded`,
zero errors, 419 warnings, and `Options: None`. It was uploaded to the existing
site; CloudFront invalidation `I70YFOJAAC20AC7TSP1WD4B40W` completed. The
public entry page and rebuilt WASM returned HTTP 200. The prior hosted files
are in `_rollback/2026-11-10-pre-input-focus-fix/`. Learner field typing still
requires a manual browser check.

On 2026-11-10, the optimized WebGL player was rebuilt after adding a
keyboard-text fallback for focused learner profile fields. Unity reported
`Succeeded`, zero errors, 419 warnings, and `Options: None` in
`Logs/WebDesktop-build.txt`. The rebuilt player was uploaded to the same S3
origin; CloudFront invalidation `ICP6MTVV9G1M8E1L8YV1N3NX5V` completed.
The public entry page, `.wasm`, and `.data` returned HTTP 200, with the WASM
served as `application/wasm`. The previous hosted objects are preserved under
`_rollback/2026-11-10-pre-input-fix/`. Browser typing in the learner form still
needs a manual acceptance check; these deployment checks do not prove it.

On 2026-11-10, `Builds/Osteon-Web-1/` was uploaded to the existing private S3
origin and CloudFront distribution. Invalidation `IXSH9GWR0EI9WEHSNCLHYDH06`
completed. The public entry page returned HTTP 200 and referenced
`Osteon-Web-1.loader.js`; the new `.wasm` and `.data` URLs returned HTTP 200,
with `application/wasm` for the WebAssembly file. The prior hosted objects were
copied to S3 prefix `_rollback/2026-11-10-pre-osteon-web-1/` before upload.
These HTTP checks do not establish browser gameplay or account acceptance for
the new build.

The earlier 2026-11-09 deployment record follows.

The optimized WebGL build is hosted at
<https://d1fz5khe940op0.cloudfront.net/> through CloudFront and a private S3
origin. The 2026-11-09 build succeeded with zero errors, 394 warnings, and
`Options: None`. CloudFront serves the entry page and WebAssembly with HTTP 200;
the WASM response has `Content-Type: application/wasm`. Direct public S3 access
returns 403. Both the accounts and quiz APIs returned HTTP 204 preflight responses
allowing this exact HTTPS origin, GET/POST, and the required request headers.
The room rendered in the Codex in-app browser. On 2026-11-09, the owner
confirmed in Firefox that the room loaded, one G0 → G3 → Back route worked,
and sign-in followed by one quiz answer worked. The remaining browser controls,
panel interactions, narration, and performance checks are still pending.

To update the hosted build, run **Tools → Osteon → Build Optimized WebGL**, verify
the fresh `Logs/WebDesktop-build.txt` result, and upload the contents of
`Builds/Web Desktop Optimized/` to the site bucket. The current objects carry a
five-minute cache lifetime; invalidate CloudFront if a replacement must be
visible immediately.

## Fixed local address for account testing

Run `./Tools/Start-WebDesktop.ps1` from PowerShell to serve `Builds/Web Desktop Optimized/`
at **http://localhost:8000**. The background Python server binds only to loopback
and serves the build directory, not the repository. The command reports its PID;
stop that process with `Stop-Process -Id <reported PID>` when finished. Logs are
under `Logs/web-desktop-8000.*.log`. Restart the command after a machine restart.

The 2026-11-05 source adds public quiz URL configuration and account bearer
authentication. A new optimized build succeeded on 2026-11-05 with zero errors
and 386 warnings (194,219,012 bytes reported by Unity; 25m34s build time).
Artifact inspection found both public service URLs and no embedded demo-token
configuration or Gemini secret name. A browser interaction attempt was denied
by the localhost origin approval gate, so browser quiz interaction remains
unautomated. The user subsequently confirmed WebGL sign-in using a saved
learner code and visible explanations after wrong quiz answers. The loopback
server was started at `http://localhost:8000` for manual inspection.
Screenshots on 2026-11-06 showed the quiz advancing through answered questions,
with both deterministic fallback and longer generated feedback visible. The
generated feedback was about mastery rather than anatomy. After the owner
deployed a feedback fix, two further screenshots showed explanations of the
correct anatomy answers without mastery or streak commentary; see
[Quiz integration](QUIZ_INTEGRATION.md). This is a partial browser quiz pass,
not full browser acceptance.
The quiz API CORS configuration was saved on 2026-11-05. Live OPTIONS returned
204 for `http://localhost:8000` with GET/POST and the three required headers;
an unauthenticated POST returned 401 with the same allowed origin. A direct
authenticated quiz start also returned the allowed origin. These checks verify
API CORS, not a browser quiz flow.

The optimized build is uncompressed. This launcher does not supply gzip/Brotli
encoding headers, and refuses those compressed outputs. Configure an appropriate
server before switching compression. The older output remains in `Builds/Web Builds/`.

For the account HTTP API, allow the exact browser origin `http://localhost:8000`,
methods GET and POST, and headers authorization, content-type, and x-unity-version in API Gateway
HTTP API CORS. Leave Allow credentials disabled because the client uses an
explicit bearer header, not browser cookies. API Gateway handles the OPTIONS
preflight automatically. Open the site using localhost, not 127.0.0.1 or a different
port, to match the configured origin. Validate the actual preflight response
before claiming browser account acceptance.

On 2026-11-03, the loopback server was started and HTTP checks passed for the
entry page, loader JavaScript, and WebAssembly (including application/wasm MIME).
These checks establish file serving only; gameplay and browser account/CORS
acceptance remain pending.

The user reported a Firefox-style `InternalError: out of memory` while compiling
the old development WebAssembly module on 2026-11-03. Its streaming-failure message
also suggests checking compression headers. Inspection confirmed a valid uncompressed
WASM file (73,230,658 bytes), `Content-Type: application/wasm`, and no Content-Encoding
header. The module declares 32 MiB initial memory and approximately 2 GiB maximum;
its debug name section alone is about 20 MB. No server encoding or memory limits
were changed based on this error.

The same localhost build successfully instantiated and rendered the room in the
Codex in-app browser. This is a browser workaround, not Firefox acceptance or a
full gameplay pass. Prefer a fresh optimized WebGL build for broader acceptance;
browser-specific compilation pressure is suspected, not conclusively diagnosed.

On 2026-11-03, a fresh non-development optimized build succeeded with zero errors
and 400 warnings in 21m57s. WASM decreased from 73,230,658 to 28,958,978 bytes
(60% smaller), and inspection confirmed that the debug name section was removed.
The new loopback server serves `Builds/Web Desktop Optimized/`; HEAD checks passed
for the entry page and application/wasm with no Content-Encoding. The private
quiz demo configuration was excluded during packaging and restored afterward.
The user confirmed that Firefox now loads the room at
`http://localhost:8000/?build=20261103-optimized`. Use this URL or a hard reload
to avoid the old cached entry page referring to missing `Web Builds.*` assets.

The optimized player also rendered the room in the in-app browser. Clicking empty
space to capture the mouse produced a Chromium UnknownError there, so that
browser's interaction pass remains incomplete. Native Firefox automation could
not proceed because desktop app approval timed out. Firefox room loading is a
user-reported check; account UI, full navigation, and Quest acceptance are pending.
The saved API CORS configuration was confirmed and, after propagation, live
OPTIONS preflight returned 204 with the localhost origin, GET/POST, and all three
allowed headers: authorization, content-type, and x-unity-version. This verifies
preflight configuration; the account UI flow still needs a manual browser pass.

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
- [ ] Sign in through Accounts, then check quiz start, answer feedback, Next,
      resume, switch, expiry, and the quiz API's CORS success/error responses;
      see [Quiz integration](QUIZ_INTEGRATION.md).
- [ ] Confirm **Read / Stop** plays bundled anatomy, quiz, and Settings
      recordings, and stops when panel content changes. Text without a
      recording has no browser speech fallback. See [Panel narration](TTS.md).

This checklist is an acceptance target, not a completed test record. The optimized
build and room-loading checks above do not establish that all browser controls,
API calls, visual layout, or performance passed testing.
