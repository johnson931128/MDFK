# M3 Greybox World Plan

Status: implementation-ready planning document. This document does not authorize changes outside the M3 scope.

Baseline assessed: `main` at `14cca1a` (`feat: add basic melee combat prototype`), Unity `6000.5.10f1`.

Planning assumptions:

- World units are Unity units.
- The greybox Grid uses `1 unit = 1 tile`.
- The initial target view is 16:9. With the current orthographic size of `5`, the visible area is approximately `17.78 x 10` units.
- Existing Player movement, combat, animation, collider, Rigidbody2D, and camera-follow feel are treated as protected baseline behavior.
- M3 builds a traversable test world. It does not add player damage, doors, abilities, checkpoints, respawn, a map UI, or production art.

## 1. Current Project Assessment

The repository currently contains one enabled gameplay scene, `Assets/Scenes/Milestone1.unity`. It is a small, flat prototype with a Player, a ground strip, three BoxCollider2D platforms, one EnemyDummy, a smooth orthographic follow camera, and the MV Human Woman visual/Animator integration.

Current active scene hierarchy:

```text
Milestone1
├─ Global Light 2D
├─ Background
├─ Ground
├─ Platform_Left
├─ Platform_Middle
├─ Platform_Right
├─ Player
│  ├─ GroundCheck
│  ├─ VisualRoot
│  └─ AttackPoint
├─ Main Camera
└─ EnemyDummy
```

Current Player baseline:

| Area | Current value / behavior |
|---|---|
| Movement | Rigidbody2D velocity movement; no Transform movement |
| Horizontal speed | `7` |
| Ground acceleration / deceleration | `60 / 70` |
| Air acceleration / deceleration | `40 / 30` |
| Jump velocity | `12` |
| Coyote time / jump buffer | `0.12s / 0.12s` |
| Jump cut | `0.5` |
| Rigidbody2D gravity scale | `2.7` |
| Low-jump / falling multiplier | `1.8 / 1.55` |
| Maximum falling speed | `18` |
| Rigidbody2D | Dynamic, mass `1`, Interpolate, Continuous collision, rotation frozen |
| Player collider | BoxCollider2D, `0.78 x 0.92`, offset `(0, 0)` |
| Ground check | Child at local `(0, -0.5)`, radius `0.16`, Ground layer mask |
| Facing | `VisualRoot/SpriteRenderer.flipX`; `IsFacingLeft` also drives AttackPoint |
| Attack | `0.8 x 0.65` OverlapBox at local X `+/-0.72`, damage `1`, cooldown `0.35s` |
| EnemyDummy | `3 HP`, 1 x 1 BoxCollider2D, currently on Default layer |

Current camera baseline:

- `CameraFollow2D` runs in `LateUpdate` and SmoothDamps toward `Player.position + (0, 1, -10)`.
- Smooth time is `0.15s`.
- The camera is orthographic with size `5`.
- The current 2560 x 1440 Game View reports a visible world area of about `17.78 x 10` units.
- There are no camera bounds, room zones, look-ahead, dead zones, or Cinemachine packages/configuration.

Current project facilities relevant to M3:

- Unity 2D Tilemap and Tilemap Extras packages are already installed.
- The project already has a `Ground` layer.
- Physics2D gravity is `(0, -9.81)` and the default contact offset is `0.01`.
- The current collision matrix allows all listed layers to collide.
- There is no dedicated Enemy layer.
- There is no health/damage receiver on the Player, no respawn system, and no room/door system. Greybox geometry must therefore avoid lethal pits and irreversible soft locks.

## 2. Scene Architecture

### Decision: Single Scene

M3 should use one new scene:

```text
Assets/Scenes/M3_Greybox.unity
```

`Milestone1.unity` remains unchanged as the known-good regression scene. The M3 scene should be copied once from the stable Milestone1 scene, then extended with the greybox world.

Reasons for a single M3 scene:

1. The five rooms form one compact, continuous loop. Seamless traversal and a visible shortcut are easier to evaluate without scene loading.
2. The project has no scene-transition, persistence, spawn-routing, save, or additive-loading architecture yet. Multi Scene would force systems outside M3's scope.
3. Player, camera, Animator, combat, and Input System references already work in a single scene.
4. The planned world is only about `52 x 36` units. A single Tilemap world is trivial in memory and collider cost.
5. A separate M3 scene protects the Milestone1 baseline while allowing room geometry and camera bounds to evolve.

Single Scene is an M3 decision, not a permanent rule for the full game. Future areas can become separate scenes or additive scene groups after doors, persistence, and cross-scene spawn routing exist.

Planned top-level hierarchy:

```text
M3_Greybox
├─ Global Light 2D                 [copied baseline]
├─ World
│  ├─ Grid
│  │  ├─ Background
│  │  ├─ Ground
│  │  └─ OneWayPlatform
│  ├─ Rooms
│  │  ├─ R01_SpawnMovement
│  │  ├─ R02_Combat
│  │  ├─ R03_Platforming
│  │  ├─ R04_Vertical
│  │  └─ R05_ReturnShortcut
│  ├─ SpawnPoints
│  │  └─ PlayerSpawn
│  ├─ Enemies
│  └─ ManualOverrides
├─ Player                           [copied baseline; protected]
│  ├─ GroundCheck
│  ├─ VisualRoot
│  └─ AttackPoint
└─ Main Camera                      [copied baseline + bounds integration]
```

Each Room object is an organizational and camera-design object, not a separate Unity Scene. It contains one or more camera-zone definitions and editor gizmos, but the collidable geometry remains in the shared Tilemaps.

## 3. Tilemap Architecture

### Grid conventions

- Grid cell size: `(1, 1, 0)`.
- Cell layout: Rectangle.
- Swizzle: XYZ.
- All Grid and Tilemap transforms remain at position `(0, 0, 0)`, rotation `(0, 0, 0)`, scale `(1, 1, 1)`.
- Room geometry is snapped to whole-cell coordinates.
- Tile sprites use a consistent Pixels Per Unit that produces exactly one world unit per tile. A `16 x 16` placeholder at `16 PPU` or `32 x 32` placeholder at `32 PPU` is acceptable.

### Planned hierarchy and components

```text
World
└─ Grid                              Grid
   ├─ Background                     Tilemap, TilemapRenderer
   ├─ Ground                         Tilemap, TilemapRenderer
   │                                 TilemapCollider2D
   │                                 CompositeCollider2D
   │                                 Rigidbody2D (Static)
   └─ OneWayPlatform                 Tilemap, TilemapRenderer
                                     TilemapCollider2D
                                     CompositeCollider2D
                                     Rigidbody2D (Static)
                                     PlatformEffector2D
```

### Component decisions

| Tilemap | TilemapCollider2D | CompositeCollider2D | Rigidbody2D | PlatformEffector2D | Layer |
|---|---:|---:|---:|---:|---|
| Ground | Required | Required | Required, Static | Not used | Ground |
| OneWayPlatform | Required | Required | Required, Static | Required | Ground |
| Background | Not used | Not used | Not used | Not used | Default |

Ground configuration:

- `TilemapCollider2D` supplies collision from occupied cells.
- `CompositeCollider2D` merges neighboring cells to remove internal edges and reduce snagging.
- Use polygon geometry for filled terrain.
- The TilemapCollider2D must participate in the CompositeCollider2D through Unity 6's composite operation setting.
- The Rigidbody2D is Static and exists only because the CompositeCollider2D requires it.
- Do not add a PlatformEffector2D to Ground.

OneWayPlatform configuration:

- Use a separate Tilemap so one-way behavior never affects solid walls or floors.
- Merge adjacent platform tiles with a CompositeCollider2D to avoid seams.
- Prefer outline geometry for the one-way surface.
- Enable `PlatformEffector2D.useOneWay` and `useOneWayGrouping`.
- Use a surface arc around `160-180 degrees`; verify the effector's local up direction is world up.
- The final collider consumed by the effector must have `Used By Effector` enabled.
- Keep OneWayPlatform on the existing Ground layer so the current GroundCheck mask still recognizes landed platforms.

Background configuration:

- Background is visual zoning only.
- It has no Collider2D, Rigidbody2D, Effector, or gameplay layer dependency.
- Fill at least one camera viewport beyond narrow playable shafts so camera centering never reveals the default clear color at a room edge.

M3 should not create RuleTiles, animated tiles, destructible tiles, hazards, slopes, or decorative collision. Three simple greybox tiles are sufficient: solid ground, one-way platform, and background.

## 4. Player Level Metrics

### Measured movement model

With a held full jump, the upward gravity before the apex is approximately:

```text
9.81 * 2.7 = 26.49 units/s²
```

The theoretical apex and rise are therefore:

```text
time to apex = 12 / 26.49 ≈ 0.45s
jump rise    = 12² / (2 * 26.49) ≈ 2.72 units
```

Falling uses the `1.55` multiplier:

```text
fall gravity = 9.81 * 2.7 * 1.55 ≈ 41.06 units/s²
```

Ignoring collision and frame discretization, returning to the same height takes about `0.82s` total. At full horizontal speed the mathematical travel is about `5.7` units. From a standing start, air acceleration reaches speed `7` in about `0.175s`, yielding roughly `5.1` units before landing at equal height.

These are theoretical maxima, not construction targets. Collision width, input timing, jump cut, camera visibility, coyote timing, and landing margin require conservative greybox values.

### Required M3 construction metrics

All gaps are measured edge-to-edge between landable surfaces. Vertical spacing is measured top-surface to top-surface.

| Metric | M3 construction value | Usage rule |
|---|---:|---|
| Comfortable horizontal gap | `3.0` units | Default traversal gap; acceptable range `2.5-3.5` |
| Maximum practical horizontal gap | `4.5` units | Use at most once, with at least `3` units of run-up and `3` units of landing platform |
| Comfortable vertical platform spacing | `1.6` units | Default upward step; acceptable range `1.5-1.75` |
| Maximum vertical platform spacing | `2.2` units | Precision challenge only; never chain repeatedly |
| Minimum normal platform width | `2.0` units | Use for ordinary platforms and one-way ledges |
| Minimum precision platform width | `1.5` units | Use sparingly and never after a max horizontal gap |
| Minimum corridor height | `3.0` units | Avoid head collisions and preserve readable character framing |
| Standard doorway/opening | `3.0` wide x `4.0` high | Allows clean movement and camera-zone overlap |
| Combat arena clear width | `18` units | Space between solid side walls, excluding transition corridors |
| Combat room target size | `20-22` wide x `12` high | Supports two stationary EnemyDummies and lateral repositioning |
| Typical horizontal room | `20` wide x `10-14` high | Matches the current 17.78 x 10 camera viewport with bounds margin |
| Vertical room playable shaft | `12` wide x `28` high | Camera envelope must still be at least `18` units wide |

Additional placement rules:

- Landing platforms after gaps greater than `3.5` must be at least `3.0` units wide.
- Never combine a `4.5` horizontal gap with more than `1.0` unit of upward rise.
- Consecutive upward platforms should use `1.6` vertical spacing and alternate horizontally by `2.5-3.0` units.
- Solid ceilings should remain at least `2.0` units above the Player collider top during normal traversal.
- The existing `0.78`-wide collider must never be scaled to fit level geometry.
- No planned fall is lethal because the project has no Player HP or respawn. Every drop must end on a solid catch floor or a recoverable ledge route.

## 5. World Layout

### Spatial layout

The first M3 vertical slice uses five logical rooms in one connected loop:

```text
                                  ┌──────────────────────────────┐
                                  │ R05 RETURN / SHORTCUT        │
                                  │ westward return corridor     │
                                  │ drop shaft at west end       │
                                  └──────────────◄───────────────┘
                                                 ▲
                                                 │ upper exit
                          ┌────────────────┐   ┌──┴───────────────┐
                          │ R03 PLATFORMING├──► R04 VERTICAL      │
                          │ zig-zag route  │   │ alternating climb│
                          └───────▲────────┘   │ and catch floor  │
                                  │            └──────────────────┘
                                  │ north exit
┌──────────────────┐ east ┌───────┴──────────┐
│ R01 SPAWN / MOVE ├─────►│ R02 COMBAT       │
│ tutorial runway  │      │ 2 EnemyDummies   │
│ shortcut landing ◄──────┤ post-combat rise │
└──────────────────┘ drop └──────────────────┘
          ▲
          └──────── R05 one-way return drop closes the loop
```

Critical path:

```text
R01 -> R02 -> R03 -> R04 -> R05 -> R01
```

This path first moves right, then climbs, turns left, and finally drops back near the spawn. It is not a straight left-to-right corridor. The return drop provides a clear spatial payoff: the player sees familiar R01 geometry and understands that the world has looped back on itself.

### World coordinate envelope

| Room | Approximate playable bounds | Camera envelope / note |
|---|---|---|
| R01 Spawn / Movement | `x -10..10`, `y -5..5` | `20 x 10` |
| R02 Combat | `x 10..30`, `y -6..6` | `20 x 12` |
| R03 Platforming | `x 10..30`, `y 6..20` | `20 x 14` |
| R04 Vertical | playable `x 30..42`, `y 2..30` | center on X with an `18 x 28` padded camera envelope |
| R05 Return / Shortcut | corridor `x 8..30`, `y 20..30`; shaft `x 8..12`, `y 5..20` | separate corridor and shaft camera zones |

Total greybox envelope is approximately `52 x 36` units, from `x -10..42` and `y -6..30`.

## 6. Room Specifications

### R01 - Spawn / Movement Room

- Purpose: Reconfirm the stable movement baseline before introducing enemies or precision traversal.
- Approximate dimensions: `20 x 10`, bounds `x -10..10`, `y -5..5`.
- Entrance: Player begins here at approximately `(-7, -3.5)`, placed just above a floor surface at `y -4`.
- Exit: East opening into R02, `3` units wide and `4` units high.
- Platform layout:
  - Main floor with at least `8` units of uninterrupted run-up.
  - One low `2.0`-wide ledge at `+1.6` units.
  - One `3.0` horizontal gap with a `3.0`-wide landing platform.
  - A visible upper-right shaft wall/landing hints at the future shortcut return.
- Enemy placement: None.
- Camera consideration: Camera zone is exactly `20 x 10`; Y is effectively locked while X has limited follow range. The shortcut landing must remain visible near the east side.
- Level design intention: Teach scale, movement speed, jump height, and the visual language of solid versus one-way tiles without text or hazards.

### R02 - Combat Room

- Purpose: Validate existing melee range, facing, animation readability, and lateral movement in a room-sized arena.
- Approximate dimensions: `20 x 12`, bounds `x 10..30`, `y -6..6`; preserve an `18`-unit clear arena width.
- Entrance: West opening from R01 at floor level.
- Exit: North-east staircase/ledge route into R03 after the arena.
- Platform layout:
  - Mostly flat solid floor at approximately `y -5`.
  - One central `4`-unit-wide, `1.6`-unit-high platform that can be crossed or jumped onto.
  - Post-combat rising ledges near `x 24..29`, each `2.5-3.0` wide and `1.6` higher than the last.
  - Do not place a max-distance gap in this room; combat should be the focus.
- Enemy placement:
  - EnemyDummy A near `(17, -4.5)`.
  - EnemyDummy B near `(25, -4.5)`.
  - Keep at least `6` units between their centers so their 1 x 1 colliders do not visually merge into one encounter.
- Camera consideration: Use one `20 x 12` room zone. The camera can move about `2` units vertically but must not reveal outside geometry.
- Level design intention: Give the player room to approach, retreat, jump past an enemy, and confirm that the `0.8`-wide attack volume is readable. No combat doors or kill requirements are added in M3.

### R03 - Platforming Room

- Purpose: Validate repeated jumps using conservative metrics and force at least one leftward correction so traversal is not a single rightward sprint.
- Approximate dimensions: `20 x 14`, bounds `x 10..30`, `y 6..20`.
- Entrance: South-east rising route from R02, arriving on a wide ledge near `x 24..29`, `y 7`.
- Exit: East opening into the middle-lower portion of R04 near `y 14-15`.
- Platform layout:
  - Entry ledge: `4` units wide.
  - Zig-zag sequence with top surfaces separated by `1.6` vertically.
  - Horizontal edge gaps alternate between `2.5` and `3.0`.
  - Minimum platform width is `2.0`; use `3.0-4.0` for direction changes.
  - Include one lower recovery floor so a missed jump does not cause a soft lock or send the player back to R02.
  - Include one `4.0` horizontal gap with a wide landing ledge as the room's peak challenge; do not use `4.5` here unless manual testing proves excess margin.
- Enemy placement: One optional EnemyDummy on a `4`-unit-wide platform after the midpoint. It must not occupy a precision landing platform.
- Camera consideration: A `20 x 14` zone allows modest vertical follow. Transition overlap with R02 and R04 should be at least `3 x 4` units.
- Level design intention: Establish the standard `1.6` vertical rhythm, alternate movement direction, and test air control without requiring wall jump or dash.

### R04 - Vertical Room

- Purpose: Prove sustained upward traversal and camera behavior in a shaft without adding wall movement abilities.
- Approximate dimensions: playable shaft `12 x 28`, bounds `x 30..42`, `y 2..30`.
- Entrance: West opening from R03 around `y 14-15`.
- Exit: West opening at the top around `y 27-29`, leading into R05.
- Platform layout:
  - Solid catch floor near `y 3`; falling to the bottom is recoverable.
  - Alternating one-way platforms every `1.6` vertical units.
  - Horizontal alternation is `2.5-3.0`, with each platform at least `2.0` wide.
  - Add wider rest ledges at approximately `y 11`, `y 19`, and `y 27`.
  - The route must continue from the catch floor back to the entrance height, preventing a soft lock after a fall.
  - No gaps greater than `3.5` and no vertical step greater than `1.75` in the main climb.
- Enemy placement: None in M3. This room isolates traversal and camera behavior.
- Camera consideration:
  - Playable width is narrower than the `17.78`-unit viewport. Center the camera on shaft X and supply background/wall padding to at least `18` units wide.
  - The camera zone is approximately `18 x 28`, centered on the shaft.
  - Camera follows Y within the zone and clamps before showing beyond the top or bottom.
- Level design intention: Demonstrate vertical world structure, provide readable rest beats, and ensure that missing a platform costs time rather than causing death or an unrecoverable state.

### R05 - Return / Shortcut Room

- Purpose: Turn the traversal direction back toward the start and deliver the first loop/shortcut payoff.
- Approximate dimensions: return corridor `22 x 10`, bounds `x 8..30`, `y 20..30`, plus a `4 x 15` west drop shaft down toward R01.
- Entrance: East opening from the top of R04.
- Exit: Open drop shaft near `x 9-10`, landing in the upper-east portion of R01.
- Platform layout:
  - Broad westward platforms descending in `1.6` increments from the R04 exit.
  - One `3.0` horizontal gap to make the direction reversal active rather than a flat walk.
  - A clearly framed drop opening at the west end.
  - The drop has solid catch geometry and no lethal void.
  - The lower shaft geometry must not be climbable from R01 with the current `2.72` theoretical jump rise; keep the first upward return step greater than `2.2` so the shortcut remains a one-way return in M3.
- Enemy placement: One EnemyDummy on a wide corridor section, never immediately adjacent to the drop.
- Camera consideration:
  - Use one `22 x 10` corridor zone and one tall shaft zone.
  - The shaft zone should center the camera horizontally and follow the fall vertically.
  - Transition to R01 bounds before the Player lands so the camera settles on familiar space rather than snapping after impact.
- Level design intention: Create a memorable reversal, show spatial reuse, and close the loop without doors, keys, abilities, or scene loading.

## 7. Camera Strategy

### Current CameraFollow2D assessment

The existing camera is appropriate for free movement in an unbounded test scene, but it is insufficient for a room-based world because it always follows the Player on both axes. At world edges it will show outside the map, and in narrow shafts it will drift horizontally with the Player instead of presenting the vertical route clearly.

### Decision

- Camera Bounds are required for M3.
- Vertical traversal continues to use the current smooth follow behavior on Y.
- Cinemachine is not justified for this milestone. The current needs are rectangular bounds, sticky room selection, and viewport-aware clamping; two small scripts and one narrow CameraFollow2D integration are less invasive than replacing the camera stack.
- An additional camera-bounds script is required.

Planned camera architecture:

```text
Main Camera
├─ CameraFollow2D                 existing smooth follow
└─ CameraBounds2D                 selects/clamps to current room zone

World/Rooms/Rxx_...
└─ CameraZone(s)                  CameraRoomZone2D data + gizmo, no Collider2D
```

`CameraRoomZone2D` should store a world-space rectangular size and priority. It does not need a physics collider or a new layer. `CameraBounds2D` keeps the current zone until the target exits it, then selects a containing zone. Overlap at doorways prevents jitter.

Clamp calculation must account for the viewport:

```text
halfHeight = camera.orthographicSize
halfWidth  = halfHeight * camera.aspect
cameraX    = clamp(desiredX, bounds.minX + halfWidth, bounds.maxX - halfWidth)
cameraY    = clamp(desiredY, bounds.minY + halfHeight, bounds.maxY - halfHeight)
```

If a zone is narrower or shorter than the viewport, clamp that axis to the zone center rather than producing inverted min/max values. R04 deliberately uses this behavior on X.

`CameraFollow2D.cs` may receive one minimal optional reference/call to the bounds provider so clamping happens before SmoothDamp. Do not replace its offset or `0.15s` smoothing baseline during M3. Camera framing feel remains a manual-test item.

## 8. Implementation Architecture

### New Files

```text
Assets/Editor/GreyboxWorldBuilder.cs
Assets/Scripts/CameraBounds2D.cs
Assets/Scripts/CameraRoomZone2D.cs
```

Unity will also create the corresponding `.meta` files.

### Modified Files

```text
Assets/Scripts/CameraFollow2D.cs
ProjectSettings/EditorBuildSettings.asset   [through Unity API only]
```

The M3 implementation should not modify `Assets/Scenes/Milestone1.unity`.

### New GameObjects

- `World`
- `World/Grid`
- `World/Grid/Background`
- `World/Grid/Ground`
- `World/Grid/OneWayPlatform`
- `World/Rooms/R01_SpawnMovement`
- `World/Rooms/R02_Combat`
- `World/Rooms/R03_Platforming`
- `World/Rooms/R04_Vertical`
- `World/Rooms/R05_ReturnShortcut`
- Camera zone children for each room; R05 has separate corridor and shaft zones.
- `World/SpawnPoints/PlayerSpawn`
- `World/Enemies` and deterministic EnemyDummy children.
- `World/ManualOverrides`, reserved for human-authored additions the builder never touches.

### New Unity Assets

```text
Assets/Scenes/M3_Greybox.unity
Assets/Generated/M3Greybox/GreyboxTiles.png
Assets/Generated/M3Greybox/Ground.asset
Assets/Generated/M3Greybox/OneWayPlatform.asset
Assets/Generated/M3Greybox/Background.asset
```

A Tile Palette is optional editor convenience, not a runtime requirement. Do not add production art.

### Layers / Physics Changes

- Reuse the existing `Ground` layer for Ground and OneWayPlatform Tilemaps.
- Keep Background on Default with no collider.
- Keep EnemyDummy objects on Default for M3 because `PlayerCombat.enemyLayer` currently masks Default and then filters for `EnemyDummy` components.
- Do not add an Enemy layer during M3; doing so would require synchronized Scene mask changes and adds regression risk without improving this greybox slice.
- Do not change the global Physics2D gravity, collision matrix, contact offset, or Player Rigidbody2D settings.
- Camera zones use data components rather than Collider2D triggers, so no camera-zone physics layer is needed.

### Protected baseline files

DO NOT MODIFY:

```text
Assets/Scripts/PlayerController.cs
Assets/Scripts/PlayerCombat.cs
Assets/Editor/MVHumanWomanPlayerBuilder.cs
Assets/Generated/MVHumanWomanPlayer/*
Assets/Scenes/Milestone1.unity
```

Changing a protected file is allowed only if a reproducible blocker proves M3 cannot function otherwise, and only after reporting the blocker and receiving explicit approval. GroundCheck interaction with one-way platforms must first be solved through Tilemap/Effector configuration and level layout; do not preemptively refactor PlayerController.

## 9. GreyboxWorldBuilder Design

Planned file:

```text
Assets/Editor/GreyboxWorldBuilder.cs
```

Suggested CLI command:

```text
build_m3_greybox_world
```

### Responsibilities

1. Preflight the expected baseline scene, required packages, scripts, Ground layer, tile assets, and clean compile state.
2. Create `M3_Greybox.unity` by copying `Milestone1.unity` only when the M3 scene does not exist.
3. Open and operate only on `M3_Greybox.unity`.
4. Ensure the World hierarchy and required components exist.
5. Create missing placeholder textures and Tile assets at deterministic paths.
6. Populate the three Tilemaps from explicit room-cell definitions.
7. Configure Ground and OneWayPlatform collision components.
8. Create room data objects and camera-zone definitions.
9. Place Player only when the M3 scene is first created; later runs must not reset the Player transform.
10. Create missing EnemyDummy objects with deterministic names and positions.
11. Add the M3 scene to Build Settings without removing or disabling Milestone1.
12. Validate before saving, then save only the M3 scene and M3-owned assets.

### Idempotency and ownership rules

- Never call `Milestone1SceneBuilder.Build()`; it creates a new scene and is destructive to the current scene context.
- Never call `MVHumanWomanPlayerBuilder.Build()` from the M3 builder; the copied baseline already contains the correct Player visual and Animator.
- Never use unconditional `DeleteAssetIfPresent`, `DeleteAsset`, `DestroyImmediate`, or scene-wide recreation.
- Use deterministic asset paths and hierarchy paths.
- `EnsureGameObject(path)` creates only missing objects.
- `EnsureComponent<T>()` adds only missing components. Existing serialized values are preserved unless a field is null and the builder owns that reference.
- The default command is `BuildMissingOnly`: a second run on an unchanged result must produce no Scene or asset diff.
- If an expected object exists with incompatible components or values, report a conflict and stop instead of overwriting it.
- Builder-owned Tilemap cells are populated only when the target M3 Tilemap is empty. If it already contains cells, validate bounds/counts and leave it unchanged.
- Human edits belong under `World/ManualOverrides` or on objects not registered as builder-owned. The builder never modifies that subtree.
- Do not rename or reparent existing Player, Main Camera, GroundCheck, VisualRoot, or AttackPoint objects.
- Save only after all required references and validation checks pass. A failed validation leaves the scene unsaved.

### Safe rerun acceptance test

1. Run the builder once and save.
2. Record generated asset GUIDs, root object count, Tilemap cell counts, Player serialized values, and Scene diff.
3. Run the builder a second time.
4. Confirm GUIDs, object counts, Tilemap cell counts, Player values, and file hashes are unchanged.
5. Add one object under `World/ManualOverrides`, rerun, and confirm the object remains untouched.

## 10. Luna Implementation Steps

### Phase 1 - Tilemap foundation

1. Verify `main` is clean and starts at or after `14cca1a`.
2. Add `GreyboxWorldBuilder.cs` with preflight and `BuildMissingOnly` behavior.
3. Copy Milestone1 to `M3_Greybox.unity` only if the target is absent.
4. Create the World/Grid hierarchy and three placeholder Tile assets.
5. Add the M3 scene to Build Settings while preserving Milestone1.
6. Recompile and verify no Console errors before proceeding.

### Phase 2 - Room geometry

1. Encode room bounds and coordinate constants from Sections 5 and 6.
2. Populate Background first so every camera envelope has visual coverage.
3. Populate solid Ground cells for room shells, walls, floors, recovery floors, and shaft boundaries.
4. Populate OneWayPlatform cells using `1.6` vertical and `2.5-3.0` horizontal rhythm.
5. Add clear openings at each planned room connection.
6. Verify no gap or vertical step exceeds the Section 4 limits.

### Phase 3 - Collision

1. Configure Ground TilemapCollider2D, CompositeCollider2D, and Static Rigidbody2D.
2. Configure OneWayPlatform TilemapCollider2D, CompositeCollider2D, Static Rigidbody2D, and PlatformEffector2D.
3. Put both collidable Tilemaps on Ground.
4. Verify composite paths have no internal seams at ordinary tile joins.
5. Verify PlatformEffector normals point upward and side collision does not trap the Player.

### Phase 4 - Player spawn

1. Create `World/SpawnPoints/PlayerSpawn` at approximately `(-7, -3.5, 0)`.
2. On first scene creation only, place the existing Player at the spawn.
3. Preserve Rigidbody2D, BoxCollider2D, PlayerController, Animator, PlayerCombat, GroundCheck, VisualRoot, and AttackPoint exactly.
4. Verify the Player starts above, not inside, the Ground composite.

### Phase 5 - Enemy placement

1. Create deterministic EnemyDummy objects under `World/Enemies`.
2. Place two in R02, one optional in R03, none in R04, and one in R05.
3. Reuse the existing EnemyDummy behavior and simple greybox visual.
4. Keep them on Default so existing `PlayerCombat.enemyLayer` continues to work.
5. Do not add AI, patrol, contact damage, drops, room locks, or respawn.

### Phase 6 - Camera bounds

1. Add `CameraRoomZone2D.cs` and `CameraBounds2D.cs`.
2. Add camera zones using Section 5 dimensions and doorway overlaps.
3. Integrate optional viewport-aware clamping into CameraFollow2D while preserving offset and smoothTime.
4. Use separate corridor and shaft zones for R05.
5. Center narrow-axis zones such as R04 rather than allowing invalid clamp ranges.
6. Verify camera zones contain all playable surfaces plus one viewport of background padding where needed.

### Phase 7 - Verification and handoff

1. Recompile and confirm zero compilation and Console errors.
2. Run structural and physics checks from Section 11.
3. Run the builder twice and prove idempotency.
4. Confirm protected files and Milestone1 are unchanged.
5. Enter Play Mode for smoke testing.
6. Perform the complete manual checklist from Section 12.
7. Stop and report any unreachable platform, camera leak, false grounding, or soft lock before publication.

## 11. Automated Verification

Unity CLI / Pipeline can objectively verify:

- Unity Editor is connected, not compiling, and not in Safe Mode.
- `M3_Greybox.unity` opens, is enabled in Build Settings, and Milestone1 remains enabled.
- The expected hierarchy exists exactly once.
- Ground, OneWayPlatform, and Background each have the required component set and no forbidden components.
- Ground and OneWayPlatform use the Ground layer.
- Background has no Collider2D.
- Tilemap cell bounds and occupied cell counts match the builder's room specification.
- Ground and OneWayPlatform Rigidbody2D components are Static.
- CompositeCollider2D generation completes and produces geometry.
- PlatformEffector2D is enabled only on OneWayPlatform.
- Player remains Dynamic with gravity scale `2.7`, Interpolate, Continuous collision, and frozen rotation.
- Player collider remains `0.78 x 0.92`; GroundCheck remains local `(0, -0.5)` with radius `0.16`.
- PlayerController serialized movement values remain exactly those listed in Section 1.
- PlayerCombat remains wired to AttackPoint, Animator, Input Actions, and PlayerController.
- Camera remains orthographic size `5`, offset `(0, 1, -10)`, smoothTime `0.15`.
- Each room camera zone is at least viewport-safe on the intended scrolling axes.
- Player spawn does not overlap solid terrain and GroundCheck overlaps Ground after settling in Play Mode.
- EnemyDummy count, positions, HP, collider size, and Default layer match the room specification.
- Runtime camera positions remain inside the selected zone after scripted read-only position probes in Play Mode.
- Console contains no compilation, Tilemap, CompositeCollider2D, PlatformEffector2D, missing-reference, Animator, or Input System errors.
- Two consecutive builder runs produce identical asset GUIDs, object counts, cell counts, and no second-run Git diff.
- Git diff confirms no changes to PlayerController.cs, PlayerCombat.cs, MVHumanWomanPlayerBuilder.cs, generated Player Animator assets, or Milestone1.unity.

Automation cannot certify subjective jump timing, actual keyboard focus, combat spacing feel, camera comfort, or whether the loop reads clearly. Those remain manual Play Test items.

## 12. Manual Play Test

Test from a fresh Play Mode entry with no Inspector overrides:

- A/D movement, variable jump, coyote time, jump buffer, facing, attack, and animation still feel identical to the confirmed baseline.
- Every required platform is reachable without dash, wall jump, attack movement, or unintended coyote exploits.
- Standard `3.0` gaps feel comfortable and the single longest gap is demanding but fair.
- `1.6` vertical spacing is repeatable without bumping ceilings.
- Minimum-width platforms provide enough landing margin for the `0.78` collider.
- GroundCheck remains stable on solid Tilemap terrain and does not flicker at CompositeCollider2D seams.
- Passing upward through OneWayPlatform tiles does not grant an unintended buffered air jump or mark the Player grounded for a noticeable period.
- Landing and walking across adjacent one-way tiles does not snag, bounce, or fall through.
- R02 provides enough room to approach, retreat, jump over, and attack two EnemyDummies.
- Enemy colliders do not block required room exits or precision landings.
- R03's route requires a direction change and does not collapse into one long rightward jump.
- Falling in R03 or R04 always reaches a recoverable floor/route.
- R04 supports traversal from its catch floor all the way to the top without wall jump.
- The camera follows vertical traversal smoothly, remains centered horizontally in R04, and never shows outside the authored background.
- Camera-zone transitions at room openings do not snap, oscillate, or lag far enough to hide the next landing.
- R05's westward return and drop visibly reconnect to R01 and create a loop/shortcut feeling.
- The R05 drop does not leave the Player outside a camera zone.
- The shortcut cannot be climbed from R01 in a way that accidentally skips the entire slice.
- No room, shaft, missed jump, or enemy placement can cause a soft lock.
- Camera Follow still tracks the existing Player and no duplicate Player/Main Camera exists.

## 13. Risks

### GroundCheck and OneWayPlatform

`PlayerController` uses `Physics2D.OverlapCircle`, which detects collider overlap without understanding player intent. While rising through a one-way collider, GroundCheck may briefly report grounded. This could refresh coyote time or consume a buffered jump unexpectedly. Do not modify PlayerController preemptively; verify first, then adjust one-way collider thickness, GroundCheck separation, or platform layout. Escalate before any protected-script change.

### CompositeCollider2D seams

Incorrect composite participation, extrusion, or geometry type can leave internal edges that snag the `0.78`-wide Player collider. Verify generated paths and walk across long terrain strips at full speed.

### PlatformEffector2D configuration

Wrong surface arc, local orientation, or Used By Effector ownership can make platforms solid from below, non-solid from above, or produce side trapping. OneWayPlatform must remain isolated from solid Ground.

### Camera bounds

The camera viewport is wider than the R04 playable shaft. A naive clamp produces min values greater than max values or exposes clear color. Center undersized axes and build at least `18` units of visual padding.

### Camera-zone transitions

Overlapping zones can oscillate every frame if selection is not sticky. Non-overlapping zones can leave the Player unbounded in doorways or the R05 shaft. Keep the current zone until exit and give transitions explicit overlap/priority.

### Builder overwrite

The existing Milestone1 and MV Human Woman builders contain scene/asset generation behavior that is inappropriate for a mature M3 scene. Calling them from GreyboxWorldBuilder could recreate scenes or generated assets. The M3 builder must copy the baseline once and then own only M3 paths.

### Builder idempotency

Unconditional Tilemap clearing or asset deletion would erase manual tuning. Default to missing-only creation, validate conflicts, and stop instead of reconciling destructively.

### Player spawn

Resetting Player position on every builder run destroys manual test state and may serialize an unintended runtime position. Set spawn only on first scene creation; confirm the Player collider begins slightly above the floor.

### Enemy layer

PlayerCombat currently masks Default. Moving enemies to a new layer without updating the serialized mask makes attacks silently miss. M3 deliberately keeps enemies on Default. A future Enemy layer migration must update both object layers and PlayerCombat masks atomically.

### No Player death / respawn

There is no Player HP, death, checkpoint, or respawn. Pits cannot be used as failure states. All lower bounds need solid recovery floors, and every route must remain traversable after a fall.

### Single-scene future pressure

Single Scene is correct for five greybox rooms but should not become an excuse to place the entire production world in one scene. Future area streaming requires a deliberate persistence and transition milestone.

### Protected baseline regression

Scene copying and camera integration can accidentally lose Player references, alter Rigidbody2D values, duplicate the camera, or regenerate the Animator. Automated checks must compare protected component values and file diffs before M3 is accepted.

## 14. Future Expansion

M3's architecture should support, but not implement, the following:

- Ability Gates: Room connection objects can later carry gate requirements while Tilemap geometry remains unchanged. Gate logic belongs to future door/ability systems, not the M3 builder.
- Multiple Areas: Each future area can use the same World/Grid/Rooms structure in a separate scene or additive scene group. The M3 single scene becomes the reference area format.
- Doors: Standard `3 x 4` room openings and camera-zone overlaps provide deterministic locations for future door prefabs and transition triggers.
- Save Points: `World/SpawnPoints` can later hold typed checkpoints without changing PlayerController.
- Respawn: PlayerSpawn establishes a stable spawn reference that a future respawn manager can consume.
- Map UI: Room objects and their logical IDs (`R01` through `R05`) can become map nodes. M3 does not collect discovery state or render a map.
- Boss Rooms: The same room-bound and camera-zone architecture can define a larger sealed arena later. M3 does not add combat locks, boss state, or encounter scripting.
- Multiple spawn/return routes: The room graph and deterministic connection names can later support doors and cross-scene spawn routing after persistence exists.

None of these future systems should be added during M3 implementation. The milestone succeeds when the five-room greybox loop is traversable, readable, bounded by the camera, regression-safe, and ready for later mechanics.
