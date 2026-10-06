# PLAN-003 — Skull explosion for the first build

**Date:** 2026-10-06  
**Status:** Implemented in the enabled scene; Play Mode verified; physical Quest verification pending

## Goal

In the G2 skull view, let the learner spread the skull's 29 selectable bones outward, rotate the result, point at bones that were previously hidden, and inspect any bone through the existing G3 flow. Keep this within the current stationary viewing station. The 22 skull bones, six middle-ear ossicles, and hyoid remain anatomically distinct; the two teeth meshes are context, not selections.

## Proposed interaction

1. Enter Skull through Axial as today. The skull starts assembled each time it is entered from G1.
2. Show a compact **Spread skull** slider on the movable anatomy panel only while Skull G2 is active. Its range is 0% (assembled) to 100% (fully spread). Animate changes over a short, comfortable interval, with no abrupt jump.
3. In Skull G2, use right-stick sideways to turn the skull around the visual center. The existing selection ray highlights and opens any of the 29 bones, including those revealed in the middle. The slider remains operable with the same XR UI ray as the other panel controls.
4. Entering G3 preserves the spread setting. The enlarged selected bone uses its own centered display, while the smaller reference skull shows the assembled anatomical relationship and highlights the selected bone. Back returns to Skull G2 at the previous spread and rotation. Back to G1 ends this skull visit; reopening starts assembled.
5. At nonzero spread, hide the two nonselectable teeth context meshes if they obstruct the bone layout; restore them when assembled. This is a visual treatment only, not a new anatomy entry.

## Motion and layout

- Capture each selected bone's authored local transform once. Move the entire bone object, so its renderer, low-detail authored ray collider, and XRI interactable stay aligned. Keep rotations and scales unchanged.
- Use one normalized spread value for every bone. Derive an outward direction from each bone's visual center relative to a fixed skull center; use explicit authored directions for central bones and crowded middle-ear ossicles where the radial direction is ambiguous. Tune maximum distances in skull-local space to make targets separable while keeping the full arrangement within comfortable reach and view. Never derive new offsets from already moved positions.
- Use a fixed rotation pivot calculated from the assembled skull. Explosion must not shift the yaw center as the geometry moves. An inward slider move follows the same paths in reverse and reaches the exact authored pose at 0%.
- Keep ray collision meshes authored and prebaked for Android. Do not create replacement mesh colliders at runtime.

## Implementation outline

1. Add a skull-specific component that stores the 29 `BoneSelection` transforms, their starting local poses, a fixed center, and per-bone directions/maximum offsets. Expose a clamped `SetSpread(float)` and an immediate reset for view exit or teardown.
2. Add the conditional slider to the anatomy panel and route it through `AnatomyNavigationController` only when the active view is Skull G2. Configure the skull's existing `SkeletonYawRotator` pattern to use the assembled visual center.
3. Keep spread as view state across Skull G2 → G3 → Back. Make the G3 reference copy use assembled source poses, while the selected inspection copy remains centered and unspread. Clear spread when leaving Skull G2 for G1 and before starting another visit.
4. Extend the existing scene-authoring menu to bind the explosion component and slider without rewriting `skull.fbx`, `skull_ray_colliders.fbx`, or the 29 current `BoneSelection` entries. Preserve authored scene colliders and the enabled scene's navigation references.
5. Update `Docs/G3_INSPECTION.md`, `Docs/SETUP.md`, and `Docs/KNOWN_ISSUES.md` with controls, checks, and actual verification results after implementation.

## Acceptance checks

- All 29 bones reach distinct, inspectable positions at full spread; the ethmoid, sphenoid, vomer, six ossicles, and hyoid get specific visual and ray-target checks. No bone or collider is left behind or detached from its highlight target.
- Slider motion is smooth and reversible. At 0%, each bone exactly matches its authored local pose; repeated spread/assemble cycles do not drift.
- Right-stick yaw stays centered at 0%, midway, and 100%. A controller ray can select newly exposed interior bones on Quest.
- G3 inspection and Back restore the same Skull G2 spread, orientation, selection readiness, and information. Back to G1 and re-entry show an assembled skull. Other groups and their G0–G3 history remain unchanged.
- The current Play Mode anatomy validation passes with new skull-specific state/geometry assertions. Then build and try the Android APK on a physical Quest, checking reachability, UI ray dragging, readability, frame rate, and comfort before calling the feature complete.

## Implemented choices

- The slider is the first-build control. There is no separate spread toggle.
- The G3 reference skull stays assembled and highlights the selected bone.
- On 2026-10-06, the saved-scene Play Mode suite passed 10,571 assertions across 207 entries, including skull spread, slider sizing, selection, Back, and exact reassembly. Physical Quest targeting and comfort remain unverified.
