# PLAN-002 — Reusable bone-group expansion

**Date:** 2026-09-09  
**Status:** Planned  
**Dependency:** Complete and review PLAN-001 first

## Intent

Reuse the polished vertebral-column interaction across the skeleton. Separately
addressable bone geometry is the main asset requirement, but navigation and
group registration must first be generalized. A separate model file for every
group is unnecessary when existing assets contain reusable individual meshes.

## Reusable group interface

- Introduce an `AnatomyGroup` scene/prefab component with a stable group ID,
  division, display name, group information, source view, and selectable bones.
- Change navigation to enter a registered group rather than a specifically
  named vertebral view. Derive breadcrumbs, reference displays, inspection
  copies, and Back history from the active group.
- Reuse bone information, selection, highlighting, and inspection components.
  One bone may reference several renderers when its geometry has multiple parts.
- Give left and right bones distinct identities. Keep discs and other context
  meshes visible without registering them as selectable bones.
- Preserve introductory content: name, location, function, and distinguishing
  features. Reuse one consistent information-panel layout.

## Asset intake and rollout

1. Inventory the existing axial, appendicular, pelvic-girdle, hyoid, and other
   source assets before creating or requesting additional models. Their file
   presence does not establish that the required individual meshes are ready.
2. Record each group's readiness: ready, needs mesh separation, needs cleanup,
   or missing assets. Identify missing small bones and unresolved fused-bone
   boundaries explicitly rather than claiming complete coverage.
3. Verify identities, left/right assignment, authored orientation, scale,
   materials, renderer references, and unique collider ownership.
4. Register ready groups using the common component and inspection system.
   Keep the vertebral column as the regression reference.
5. Expand in this order: thoracic cage; skull/head and neck; shoulder girdle;
   upper limbs including hands; pelvic girdle; lower limbs including feet.

Only ready groups are exposed through navigation. Record gaps in the feature
register; do not create misleading placeholder selections or fabricate missing
geometry. This plan prepares and integrates assets, not a claim that every
required anatomical mesh currently exists.

## Acceptance and defaults

- Every registered bone highlights alone, opens matching geometry and
  information, and appears correctly highlighted in its group's reference.
- Back restores the previous group/division pose and information, including
  repeated entry, same-bone reselection, and transitions between groups.
- The viewing station and controller controls behave consistently across groups.
- Check automated registry integrity, duplicate IDs, missing references, and
  navigation restoration. Perform a user-led headset pass for each added group,
  concentrating on unusually small or large bones.
- Retain one inspected bone at a time and the always-visible reference group.
  Assembly, quizzes, hand tracking, and throwing remain deferred.

Do not repeat full-project builds for each asset registration unless a concrete
build failure or user request requires them.
