# PLAN-005 — Panel spacing, shared G2 spread, and wall settings

**Date:** 2026-10-07
**Status:** Implemented in code; Play Mode and Quest verification pending

The four features were committed separately on 2026-10-07. The shared G2
Spread uses group-specific distances and axis weights; skull retains its
explicit central-bone and ossicle directions. A Unity batch validation attempt
stopped during licensing startup before Play Mode, so physical ray targeting,
visual tuning, reach, and comfort remain to be checked on Quest.

This plan follows [PLAN-004](2026-10-07_PLAN-004_Appearance-and-Text-Settings.md). Implement its four parts as separate feature commits. The existing `Meta Quest.asset` working-tree change is unrelated and must stay out of those commits.

## 1. Lower the panel Move bars

**Current layout:** the anatomy Back/Settings row ends at local Y `-380`, and its Move bar begins at `-416` (36 UI units of clearance). The quiz action button ends at `-454`, and its Move bar begins at `-491` (37 units). Both bars have a 58-unit visual and grab target. This close spacing can let a ray meant for a button select the grab interactable.

**Change:** move the anatomy bar center from `-445` to about `-510`, and the quiz bar from `-520` to about `-585`, keeping their width and usable height. That makes the nearest button-to-bar gap about 100 UI units. Update the saved anatomy scene, every `PanelMoveHandle.Configure` call in `AnatomySceneSetup`, and the runtime quiz layout together. Give the new wall settings panel the same minimum separation below its last button. Keep the grab collider confined to the bar; adjust its depth only if ray-hit checks show it intercepts panel controls.

**Checks:** at the top edge, center, and bottom edge of every nearby button, controller rays select the UI button without grabbing its panel. A ray centered on each bar still grabs and moves only that station. Check all three panels at their starting positions and after repositioning, including the quiz Hide/Open launcher. Confirm the lower bars remain comfortably reachable on Quest.

## 2. Make Spread available to G2 bone groups

Replace the skull-only control path with a group spread component and one anatomy-panel slider bound to the active G2 view. The control should identify its group (for example, **Spread skull** or **Spread ribcage**). Apply it to the nine G2 models with at least two selectable bones: skull, vertebral column, ribcage, both lower limbs, both upper limbs, and both pectoral sides. The pelvic view has one selectable hip bone, so hide Spread there, as the user confirmed.

Capture each group's authored bone poses and renderer matrices once. Move each existing bone object so its visible mesh, authored Quest ray collider, and XRI interactable stay together. Use a shared normalized 0–100% value and animation code, with a profile of distances and directions tuned per group. Preserve the skull's explicit directions for central bones and ossicles; add authored exceptions where the vertebral midline, ribs, small hand/foot bones, or pectoral pair need them. At 0%, restore exact authored poses with no cumulative drift. Keep bounds and targets usable at full spread.

Generalize `AnatomyNavigationController`'s `skullExplosion`, `skullSpreadSlider`, `SetSkullSpread`, presentation, and exit handling to select the active group's controller. Generalize `BoneInspectionDisplay`'s assembled-reference matrix source so every G3 reference group stays assembled while its G2 group is spread. G2 → G3 → Back retains that group's spread and yaw; leaving the group for G1 resets it. Switching pectoral sides counts as leaving one G2 group: reset the previous side and enter the other side assembled. Preserve the current skull behavior, including context-mesh handling.

Update scene authoring and saved-scene validation for all nine groups without creating runtime mesh colliders. The current Quest build relies on authored and prebaked ray targets. Verify representative inner and small bones at 0%, halfway, and full spread; test exact reassembly, repeated cycles, yaw center, G3 reference poses, Back, and both pectoral sides. Build and test an APK on Quest before calling the interaction verified.

## 3. Move Settings to the quiz wall

Remove the Settings button and overlay from the movable anatomy panel. Restore the anatomy Back button to its full action-row width; keep anatomy content, Skull/Group Spread, and Back on that panel. Create a separate wall station with a **Settings** launcher button at the same world Y as the quiz launcher (`1.4`) and the same wall-facing orientation. A candidate center is `(4.54, 1.4, 1.9)`, about 1 m from the quiz launcher's `(4.54, 1.4, 0.9)`. Check the actual wall geometry and keep at least about 0.25 m between the two fully open panels at their initial positions.

Selecting the Settings launcher opens a dedicated world-space settings panel on that station; Close hides it and restores the launcher. The settings station can be repositioned with its own separated Move bar, with the launcher following the station just as the quiz launcher does. Reuse the existing saved preferences, theme and text scaling. `OsteonSettings` must apply updates to the anatomy, quiz, and new settings canvases whether the quiz station appears before or after Settings. Opening and closing Settings must not change G0–G3 history, Skull/Group Spread, or the positions of the other panels.

Verify ray selection and contrast with both wall stations open, the quiz hidden, and the stations moved. Check initial visibility from the normal viewing area and comfort on Quest; tune only the horizontal wall position if geometry or reach requires it, keeping launchers at the same height.

## 4. Add highlight color to Settings

Add an independent **Highlight color** choice with a small set of named presets and **Original blue**. Persist it with the other local preferences and show a clear selected state. The current `HighlightMst.mat` has a white base color and blue `_EmissionColor`. Create one runtime copy of that material and update its base/emission colors for the chosen preset; do not change the committed material asset.

Route that runtime material through G0/G1 group hover, G2 individual-bone hover, and the selected bone in the G3 reference display. The relevant code paths are `BoneGroupHoverHighlighter`, `BoneSelection`, `ViewTransitionOnSelect`, and `BoneInspectionDisplay`. A color change should update an active highlight immediately and also apply to highlighters initialized later. Clearing hover must still restore the source bone materials. Choose presets that remain visible with each hospital color and panel theme; color remains independent of the panel theme.

Check every navigation level, both controller rays, simultaneous hover exit, G3 reference creation, Back, Reset to defaults, scene reload, and app restart. Confirm the highlight is discernible on bone surfaces in Quest lighting without obscuring bone detail.

## Implementation order and verification

1. Lower and verify Move bars; commit that feature.
2. Relocate Settings to the wall and verify its separate launcher and station; commit that feature.
3. Add and verify the highlight-color preference; commit that feature.
4. Refactor and tune shared G2 Spread across the nine eligible views; commit that feature.

Run the saved-scene anatomy validation after scene changes and extend its checks for panel hit separation, wall station wiring, highlight propagation, and per-group spread state. Then complete physical Quest testing for ray priority, reach, readable color, spread targeting, and comfort. The existing PLAN-004 settings code compiled with MSBuild, but Unity batch validation on 2026-10-07 was blocked during licensing startup; recheck that gate before implementation validation.
