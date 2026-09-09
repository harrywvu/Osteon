# PLAN-001 — Stationary viewing and controller grabbing

**Date:** 2026-09-09  
**Status:** Planned  
**Priority:** First, before expansion to more bone groups

## Intent and confirmed choices

Make the experience usable from one standing or seated position. The user found
the current output appropriate but rough, with inconsistent placement and no
suitable viewpoint-turning control. Add direct interaction with the inspected
bone while keeping anatomical context intact.

The user selected smooth viewpoint turning, controller grabbing rather than
bare-hand interaction, and leaving the bone where released. This document does
not describe behavior already implemented.

## Viewing station

- Disable artificial walking, teleportation, jumping, and grab-based locomotion
  throughout the anatomy experience; preserve natural headset tracking.
- Use the left thumbstick for smooth viewpoint turning. Start at 45 degrees per
  second with a 0.15 dead zone. Rotate around the headset's horizontal position
  so turning does not translate the user sideways.
- Use one stable display center for G0 through G3. Center and uniformly fit the
  earlier views so transitions never require walking to another model.
- Start the station approximately 75 cm in front of the user and 25 cm below
  eye height. Keep it fixed in world space after initial placement. Treat these
  measurements as editable defaults requiring headset confirmation.
- Keep the existing 30 cm G3 inspection volume and 45 cm reference-column
  height as starting defaults. The reference and information panel remain
  stationary. Check for overlaps with text, other models, and room furniture.

## G3 grabbing and control ownership

- Either controller can directly grab the enlarged inspection copy with grip,
  then translate and rotate it. Allow one controller to hold it at a time.
- Add fitted grab colliders and controlled rigidbody movement to that copy.
  Disable gravity and throwing momentum. Source anatomy and the reference
  remain non-grabbable; this is not an assembly exercise.
- Releasing grip leaves the bone at its current position and orientation.
  Add **Reset position** to restore the inspection center without changing
  orientation, and **Reset orientation** to restore authored orientation
  without changing position.
- Preserve changes only for the current inspection. Opening another bone or
  reopening the same bone starts at the standard display pose.
- Require a fresh grip press after entering G3, using the existing navigation
  gate, so selecting a bone cannot immediately become a grab.
- Keep right-stick turning/tilting and independent A/B resets when not holding
  the bone. Suspend those controls during a grab to avoid competing rotations.
- On release, use the placed orientation as the new base with zero thumbstick
  offsets. A/B reset their respective thumbstick offsets; Reset orientation
  clears both those offsets and the manually placed rotation.
- Keep left-stick viewpoint turning and UI clicks available. Disconnecting the
  holding controller ends the grab and leaves the bone stationary; reconnecting
  requires neutral input and a fresh press.

## Implementation approach

Extend the reusable inspection display to expose its movable copy and reset
operations. Retain shared meshes and avoid adding behavior to source assets or
the reference copy. Navigation remains the owner of inspection lifetime and
fresh-press gating. Update input reservation so viewpoint turning has a distinct
owner and cannot compete with bone controls or template locomotion.

Coordinate free controller rotation and thumbstick offsets explicitly; do not
let two scripts write the displayed transform simultaneously. Keep imported
template scripts and historical scenes unchanged.

## Acceptance

1. Validate with C1 and Sacrum before repeating the 26-bone flow.
2. Confirm fixed positioning, readable information, reachable grabbing, and
   viewpoint turning without artificial translation.
3. Exercise grab, movement, rotation, release, both reset buttons, and right-stick
   control afterward. Confirm A/B preserve the other thumbstick axis.
4. Repeat Back and re-entry, held-grip transitions, competing controller grabs,
   and controller disconnection/reconnection.
5. Add focused automated checks for the changed pose and input ownership logic.
   The user performs headset acceptance. Do not repeat full Android builds
   unless requested or needed for a build-specific investigation.

Bare-hand grabbing, two-handed manipulation, scaling, throwing, and assembly
remain outside this plan.
