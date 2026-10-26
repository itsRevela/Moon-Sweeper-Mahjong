# Table layout (`MoonMahjong.Table.Layout`)

`src/MoonMahjong.Table/Layout` is a Unity-free port of how OpenRiichi lays out and animates every tile on its 3D
table: `source/Game/Rendering/{GameRenderView,GameScene,RenderTable,RenderWall,RenderHand,RenderPond,RenderCalls,
RenderPlayer,RenderTile,RenderStick,RenderAction}.vala` plus the engine pieces they rely on (`WorldObject`,
`Transform`, `Quat`, `Vec3`, `Animation`, `WorldObjectAnimation`, `Curve`, `Path`). Same offsets, spacings, orderings
and timings; nothing is drawn. The plugin asks it where each of the 136 tiles (and each riichi stick) is at the
current time and moves its GameObjects there. Tests: `tests/MoonMahjong.Table.Tests`.

## Using it

```csharp
var table = new TableLayout(observerSeat, dealer, gameStartInfo, roundStartInfo, TableTimings.Default, Time.time,
    handAreaRise);                          // optional: frame height above the felt, in tile widths (default 0)
// For each event from the round (same order as OpenRiichi's ClientRoundState signals):
table.TileAssignment(tile, now);            // game_tile_assignment
table.TileDraw(seat, now);                  // game_tile_draw
table.DeadTileDraw(seat, now);              // game_dead_tile_draw (after a closed/late kan, when calls finish)
table.TileDiscard(seat, tileId, now);       // game_tile_discard
table.Riichi(seat, open, now);              // game_riichi
table.LateKan / ClosedKan / OpenKan / Pon / Chii(..., now);
table.GameFinished(result, now);            // game_finished (or Ron / Tsumo / Draw directly)
table.SetActive(on, now); table.SetTileSelectGroups(idLists, now);
// Every frame:
TilePose pose = table.GetTilePose(id, Time.time);      // 0..135
StickPose stick = table.GetRiichiStickPose(seat, Time.time);
```

- The constructor queues OpenRiichi's round start: 0.5 s pause, wall split (0.5 s), the deal (16 draws of 0.15 s:
  4, 4, 4 then 1 tile per seat, dealer first), then the first dora flip.
- `OpenKan` queues the replacement draw itself (as `GameRenderView.open_kan` does). `DeadTileDraw` also flips the
  next dora and moves the live wall's last tile to the dead wall.
- `FlipDora` acts immediately and exists only for parity: OpenRiichi's client never emits `game_flip_dora`.
  `FlipUraDora` queues the ura-dora flip; `Ron` and `Tsumo` already queue it for a winner in riichi.
- Tile types matter for sorting hands, `ClosedKan` (finds the four tiles by type) and `LateKan` (finds the pon by
  type), so reveal tiles with `TileAssignment` before those events, as the server does.
- Selection: pass each Core `TileSelectionGroup`'s `SelectionTiles` as a list of IDs; `GetTileSelectionGroup(hovered,
  now)` returns the index of the group to highlight/select, or null (inactive table, nothing hovered, not selectable).

## Time

Every call takes the current time in seconds and times must not decrease (an exception otherwise). Events go into
OpenRiichi's action queue: each starts when the previous one's `pre + time + post` has elapsed, and its tile
animations start then. Queries advance the model to their time. Results depend only on the events and their times,
not on how often you query (a test polls at 60 fps and compares against a model that is never polled).

An animation (`animate_towards`) keeps the tile at its old pose until `pre` has passed (strictly), then moves it along
a straight line and the shortest rotation with OpenRiichi's curve (`SmoothApproachCurve` for tiles, `SmoothDepartCurve`
for opening/closing hands), and it is exactly at the target at `pre + time`. `post` only holds back whatever is queued
next on the same object.

## Coordinates

Inside the model everything stays in OpenRiichi's space: right-handed, Y up, table centre at the origin, felt at
y = 0, seat `i` turned `i` quarter turns about +Y (seat 0 sits at +Z, seat 1 at +X), each player looking at the centre
with +X on their right. Units are OpenRiichi world units (a tile is 0.5265 wide).

Every pose the model returns goes through one conversion (`TableCoordinates`) to **table-local plugin space**:

- Unity's left-handed axes, Y up; the table centre at the origin; the felt at Y = 0.
- The observer's seat on -Z, looking towards +Z; +X is the observer's right, so the next seat in turn order
  (shimocha) sits at +X and the opposite seat at +Z.
- One unit = one tile width: multiply positions by the real tile width.

Position: undo the observer seat's rotation, mirror Z (the handedness change; it keeps the observer's right on +X),
divide by the tile width. Rotation: conjugate by the same mirror (quaternion `(w, x, y, z)` becomes `(w, -x, -y, z)`),
then, for tiles, append the change from the plugin's tile mesh to OpenRiichi's tile model.

### Tile orientation

OpenRiichi's tile model (`bin/Data/Models/tile_low.obj`, `tile_high.obj`) lies flat: width along X, thickness along
Y with the face on +Y, length along Z. Its face UVs run u with +X and v with -Z; the OBJ parser flips v and stb loads
images top row first, so the face image's top points to model -Z and its right to +X.

The plugin mesh (`build/assets/tile_mesh.json`) has its face on local +Z, the image upright along +Y and the width along
X; its face UVs run u with -X, which is the unmirrored picture seen from the front in Unity's left-handed space. So:
plugin +Z = OpenRiichi +Y (face), plugin +Y = OpenRiichi -Z (image top), plugin +X = OpenRiichi -X. That map and the Z
mirror are each a reflection; together they are a proper rotation, the half turn about (0, 1, 1)/sqrt(2), quaternion
`(x 0, y sqrt(1/2), z sqrt(1/2), w 0)`, appended to every tile rotation. Tests check the result: a pond tile faces +Y
with its image top away from its owner, a wall tile faces -Y, opponents' hands stand facing their owners.

The observed hand is not upright. OpenRiichi tilts it back to `0.5 - VIEW_ANGLE` = 0.06 pi (10.8 degrees) from flat,
facing its high camera (at 16 up, 10 back, looking down). Its tiles face (0, cos 10.8°, -sin 10.8°): mostly up,
leaning towards the observer, image top pointing away. Opponents' hands stand vertical. A spectator model
(`observerSeat = -1`) tilts no hand.

### Proportions

OpenRiichi's layout spacing comes from `tile_size = obb(tile_low.obj) * 1.5` = (0.5265, 0.3832, 0.6794) world units
(width, thickness, length). In plugin units that is `TableLayout.TileSize` = **(1, 1.2903, 0.7279)** (width, face
height, thickness), about 26 : 33.5 : 18.9, not the 26 : 35 : 20 of a real tile. The model keeps OpenRiichi's
proportions so nothing overlaps; scale the tile mesh to `TileSize` (times the real width) to match exactly. A 26:35:20
mesh drawn at these poses is about 4% too long and 6% too thick: pond rows and stacked wall tiles would clip
slightly.

Other objects (their meshes are modelled like OpenRiichi's, so only the frame changes):

- Riichi stick: `TableLayout.RiichiStickSize` = (4, 0.0847, 0.3337) tile widths (length along local X, printed face
  +Y). Hidden until riichi, then it slides to the centre piece's edge (0, 0.174, 2.319 towards its seat); a returned
  stick slides back to the hand line.
- Centre piece: `TableLayout.CenterPieceSize` = (5.798, 0.242, 5.798) tile widths, pivot at the centre of its base,
  never moves (`CenterPiecePose`).
- Tile and stick poses are the OpenRiichi object origins. Their meshes are not recentred: the tile model's box centre
  is within 0.005 tile widths of its origin and the stick's within 0.03, so a centred mesh is placed correctly to
  within that.

## Layout reference (OpenRiichi world units; w, h, l = tile width, thickness, length)

| Element | Where (in the owning seat's frame) |
|---|---|
| Hand | 8 from the centre; tiles side by side, sorted by type (stable), centred; a tile drawn after the deal lies sideways one row beyond the hand at x = (n/2 - 1) w |
| Pond | starts 3w from the centre, rows of 6 from the player's left, third row unbounded; riichi tile sideways, aligned to the row's far edge |
| Calls | corner at (8, 8) (the player's near right); melds run leftwards, each new meld further towards the centre; called tile sideways on the discarder's side; closed kan shows the middle two face down; late kan stacks the added tile beside the called one |
| Walls | 17 stacks of 2 at 10w from the centre, wall `i` (tile IDs 34i..34i+33) in front of seat (4 - i) mod 4; draws start at the dealer's wall, stack `wall_index`, and run clockwise |
| Dead wall | 14 tiles at the end of the live wall; the split opens a 1.5w gap on each side of it; dora indicator = top of the third stack from the break; ura dora slides out by l and flips, the indicator drops to the lower layer |

## What was dropped, and deviations

Dropped (the engine's or the plugin's job): meshes, materials, textures, colours, model quality, table and felt,
lights, the camera and its 3 s fly-in, centre-piece labels (round wind, names, scores), the dealer's wind indicator,
options, hover/indicated highlighting, picking and cursors, spectator seat switching (`observe_next/prev`,
`set_observed`, `animate_angle`), `load_options`, `game_loaded`, the action mutex. Unused engine parts are not ported:
unbuffered animations, `finish_animations`, scale paths, `LinearizedPath3D`, `ExponentCurve`.

Sounds are not played here: each action that OpenRiichi's GameScene plays a sound for records a `SoundCue` at its
start time instead, and `TakeSounds(time)` hands the due ones to the plugin.

Deviations, each kept as small as possible:

- **Fit to the Moon Sweepers table (`ModLayout`, requested 2026-10-25).** The 3D table has a raised rail between the
  felt and a frame that is higher than the felt, and OpenRiichi's hand areas straddle it. So every player's hand and
  melds move `ModLayout.HandOutwardShift` (0.45) tile widths toward their owner, which centres them on the frame, and
  stand `handAreaRise` above the felt (the plugin measures it from the frame and felt meshes, about 0.31 tile widths).
  A tile drawn after the deal stands upright `ModLayout.DrawnTileGap` (0.25) widths right of the hand, not sideways one
  row beyond it (where the rail cut it). Called melds run in one row along the edge, leftwards from the player's right
  corner, `ModLayout.MeldGap` (0.1) widths apart, as on a real table; OpenRiichi stacked each new meld one meld-depth
  nearer the centre. Walls, ponds and the centre are unchanged. A late kan's second sideways tile still reaches the
  rail: the frame is too narrow for two rows of tiles.

- **Continuous time.** OpenRiichi starts the next action on the first frame after the current one ends, so every
  action takes at least one frame. Here it starts exactly at the end, and zero-length actions take no time. This
  removes frame-rate dependence and changes timing by at most a frame.
- **Exact transforms.** Every scale in the table is uniform, so local transforms compose as (position, quaternion,
  scale) instead of 4x4 matrices. Same results up to float rounding, with one exception. It also avoids
  `Mat4.get_rotation`, which zeroes a quaternion component when the matching off-diagonal difference is exactly 0
  (exact half turns): `x *= sign(x * 0)`. The exception: when a tile starts exactly a half turn from its target (a
  face-down wall tile dealt to the seat opposite that wall, or drawn sideways from the wall on the drawer's left),
  the shortest rotation is ambiguous and rounding noise picks the spin direction. OpenRiichi's noise comes from the
  matrix round trip, ours from quaternion products, so the tile may spin the other way during its 0.15 s move. End
  poses are identical, and the choice here is deterministic.
- **Failing loudly.** OpenRiichi passes a null tile on (and crashes) when a wall runs out or a late kan has no pon; here
  that is an `InvalidOperationException`. Arguments are validated: seats 0-3, tile IDs 0-135, wall index 2-12 (a dice
  sum), finite non-decreasing times, four players.
- **Names.** `AnimationTime` and the render half of `AnimationTimings` are `TableAnimationTime` and `TableTimings`, and
  selection groups are passed as ID lists, so nothing clashes with the Core ports of `AnimationTime`,
  `AnimationTimings` and `TileSelectionGroup`. `RenderTile.tile_type` (a `Tile`) is `RenderTile.Tile`.
- `TileAssignment` stores a copy of the tile rather than the caller's object.

Kept on purpose, although they look like bugs in the Vala (fixes belong in separate, documented changes):

- `GameScene.observer` is never assigned, so at an exhaustive draw the observer's own noten hand is also turned face
  down (the exemption `player != observer` always passes).
- An unrevealed `RenderTile` carries `Tile(0, BLANK)`, and picking matches the hovered tile by that ID. So any
  unrevealed tile (wall, opponents' hands) matches a selection group that contains tile 0.
- Tsumo sets the winning tile at `size + 0.5` while the other 13 tiles are still laid out for 13, so the gap is a full
  tile width rather than the half the formula suggests.
- `RenderPond` creates and positions a `wrap` node it never uses (tiles go into the pond node itself).
- Four-winds and four-kan draws neither reveal nor close any hand; `draw()` tests `TRIPLE_RON` twice.
