# PLAN-004 — Appearance and text settings

> The original anatomy-panel Settings placement below is historical. PLAN-005
> moved Settings to its own wall station and added highlight color; see
> [PLAN-005](2026-10-07_PLAN-005_Panel-Spacing-Shared-Spread-and-Wall-Settings.md).

**Date:** 2026-10-07
**Status:** Implemented in code; Play Mode and physical Quest verification pending

## Goal

Give the learner an in-world **Settings** button for changing the hospital environment color, panel theme, and text size during the current G0–G3 experience. Changes should be visible immediately, survive scene navigation, and restore on the next app launch.

## Proposed interaction

1. Put a clearly labeled Settings button beside Back at the bottom of the anatomy panel, available at every granularity level. Selecting it opens a settings view within the same movable panel; Close returns to the exact information view and anatomical navigation state. The settings view has its own Close control. The existing anatomy Back button and left X continue to own G0–G3 history.
2. Offer a small set of named **Hospital color** choices, including **Original**. Recolor chosen hospital room surfaces while keeping bone materials, selection highlights, panel UI, ray targets, and room lighting separate.
3. Offer named **Panel theme** choices such as Default dark, Light, and High contrast. Apply one theme to the authored anatomy panel and the runtime-created quiz launcher/panel, including backgrounds, buttons, slider, move handles, and text contrast. Keep hover, pressed, disabled, and selected states distinguishable.
4. Offer **Text size** choices Small, Standard, and Large with a live preview. Apply the choice to anatomy information, settings controls, quiz content, button labels, and move-handle labels. Standard must match the current scene's intended appearance. Provide scrolling or expanded text regions where larger text cannot fit, rather than letting auto-sizing silently shrink it.
5. Include **Reset to defaults**. Save selections locally on the headset and reapply them when the scene and quiz UI are created. Settings are device preferences for now; there is no account sync.

## Implementation outline

1. Inventory the renderers and materials under the hospital room model in the enabled scene. Identify an explicit set of colorable surfaces and their original material colors. Test the chosen tint method on the imported Blender asset without editing the source `.blend` or recoloring anatomical models. Keep materials shared by unrelated objects from changing together.
2. Add one runtime appearance-settings owner with validated preset IDs, defaults, local persistence, and a change event. Apply hospital colors only to the authored room targets. Restore original colors exactly when Original or Reset is chosen.
3. Create the Settings button and page at runtime inside the authored anatomy panel, so the scene setup tool can rebuild that panel without losing settings. Give all controls XR UI ray targets large enough to use on Quest. Opening Settings must not push a navigation history frame or reserve G3 controller inputs.
4. Centralize UI color roles and original font sizes so the anatomy panel, Skull spread slider, and `OsteonQuizDemo` use the same current preferences. The quiz UI is created at runtime, so it must read saved preferences on creation and listen for later changes. Apply text scaling from each element's baseline size; recheck text layout and scroll behavior for long G3 descriptions and quiz questions.
5. Update the setup and feature docs with the actual controls and verification results. Keep Play Mode and Quest verification marked pending until they run successfully.

## Acceptance checks

- A controller ray can open Settings, use every control, close it, and continue G0–G3 selection and Back without losing the current view, Skull spread, or panel placement.
- Hospital color changes affect only the intended room surfaces. Bones, highlights, colliders, lighting behavior, and anatomical readability remain intact. Original and Reset recover the exact starting colors.
- Every theme is readable in both anatomy and quiz UI, including disabled buttons, focus/hover states, and the Skull slider. Reopening the quiz does not revert its theme.
- Every text size stays readable without clipping key controls or silently shrinking a selected Large size. Check the longest bone descriptions and quiz options, plus the settings view itself.
- Preferences persist after scene reload and app restart. A clean install uses the current default colors and Standard text size. Corrupt or unknown saved values fall back safely to defaults.
- Extend saved-scene Play Mode checks for UI wiring, preference application, Reset, quiz creation, and navigation. Then build and test on physical Quest for ray targeting, contrast, text readability, and comfort.

## Confirmed design choice

- **Hospital color scope:** the first version changes room surfaces. The user confirmed this on 2026-10-07.

## Implementation record

- `OsteonSettings` builds the in-world Settings page at runtime and stores three device preferences with `PlayerPrefs`. Hospital presets use material copies assigned only to opaque renderers below `hospital room`; Original recovers their source colors. Room lights, anatomical models, and source `.blend` data are untouched.
- One palette updates anatomy and quiz backgrounds, buttons, hover/pressed/disabled states, slider parts, grab bars, and text. `OsteonQuizDemo` applies the saved palette when its station appears.
- Text sizes are 90%, 100%, and 120% of authored sizes. Anatomy descriptions and quiz reading regions scroll at the chosen size; answer buttons gain room for larger labels. Settings stay open only as a panel view and do not modify anatomical navigation history.
- A Visual Studio MSBuild compile of the generated Unity C# project succeeded on 2026-10-07. Unity batch Play Mode validation did not start because the local Unity Licensing Client repeatedly lost its connection before script import. Physical Quest UI use, color appearance, and text layout remain unverified.
