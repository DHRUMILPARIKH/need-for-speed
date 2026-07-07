# Track, Race Systems & AI Setup (Systems 3–5)

## 1. Build the track (Track01 scene)

**You supply** the road/barrier meshes — or block it out free with Unity primitives
or ProBuilder (Window > Package Manager > ProBuilder). A drivable blockout:

```
Track01 (scene)
├── Environment
│   ├── Road          <- your road mesh + MeshCollider, or scaled Cubes/Planes
│   ├── Barriers      <- walls with colliders on BOTH sides of the road everywhere
│   └── Ground        <- large plane under everything (catches off-track cars)
├── Checkpoints       <- empty parent
│   ├── CP_00 (start/finish), CP_01, CP_02 ... in driving order
├── SpawnPoints       <- empty parent
│   ├── Spawn_0 (player), Spawn_1 ... Spawn_7
├── AIWaypoints       <- empty parent with WaypointCircuit
│   ├── WP_00, WP_01, ... around the lap
├── RaceManager       <- empty with RaceManager script
├── Main Camera       <- with ChaseCamera
└── Directional Light
```

**Barriers are load-bearing**: the AI has no off-track recovery beyond
reverse-when-stuck, so the track must be a closed channel. Give barrier colliders
a frictionless Physic Material (dynamic/static friction 0, Friction Combine =
Minimum) so wall scrapes bleed speed instead of gluing the car to the wall —
this is a big "forgiving feel" contributor.

### Checkpoints (⚠ all Editor work)
- Each `CP_xx`: add the `Checkpoint` script (auto-adds a trigger BoxCollider).
- Scale the collider to span the **full road width and ~8 m tall** — a gap a car
  can slip through breaks lap counting for the whole race.
- Order = child order under `Checkpoints`. **Child 0 is the start/finish line.**
- 8–15 gates per lap is plenty. Orientation doesn't matter; position does.

### Spawn points
- Empties **just behind the start/finish line**, facing the driving direction
  (blue Z arrow points down-track). Cars must cross CP_00 shortly after GO —
  lap counting anchors on that first crossing.
- Grid them 2-wide with ~6 m gaps. Spawn_0 = player (front row).

### AI waypoints
- Add `WaypointCircuit` to `AIWaypoints`; children are the racing line.
- One every 15–25 m on straights, every 5–10 m through corners, placed on the
  line a good driver would take (outside–apex–outside). Cyan gizmo loop shows
  the result in the Scene view.
- Waypoints don't need to touch checkpoints — separate systems.

## 2. RaceManager wiring

On the `RaceManager` object, assign: `checkpointParent` → Checkpoints,
`spawnPoints` → the Spawn_x transforms in order, `aiCircuit` → AIWaypoints,
`chaseCamera` → Main Camera, `carPrefabs` → your car prefab(s) — **same order as
the Car Select menu list**.

Defaults (mode/laps/AI count) apply when you press Play directly in the track
scene; coming from the menu, GameSettings overrides them.

What it does at runtime, so you don't wire it by hand: spawns the player + AI,
adds `RaceParticipant` to every car, strips `VehicleInput` from bots and adds
`AIController`, points the camera at the player, freezes everyone, runs 3-2-1-GO,
then sorts live positions every frame and fires events the HUD listens to.

**Win/lose**: Circuit — your position when you complete the final lap (1st = win
screen). Time Trial — no opponents; the results screen shows lap times with the
best flagged. Restart / Quit-to-menu buttons are on the results panel.

## 3. AI difficulty & rubber-banding

Difficulty comes from the Settings menu (Easy/Normal/Hard) and maps to throttle
cap, corner commitment, and steering wobble in `AIController.Configure()`.

Rubber-banding is deliberately gentle: ±15% throttle scaled by how far
ahead/behind the player they are (over a 4-checkpoint window), plus nitro use
only when well behind and on a straight. Raise `rubberBandStrength` toward 0.3
for party-game chaos, or set it to 0 for honest racing. Because AI cars run the
identical `VehicleController` physics as the player, they can genuinely be
out-driven — the band shapes the race, it can't drive for them.

If AI cars corner badly: add more waypoints at the apex, or lower
`lookAheadPerSpeed` (they're cutting too much). If they brake too early, raise
`minCornerSpeedKmh` or set difficulty Hard (`cornerCommit` 1.15).

## 4. Scenes in Build Settings

File > Build Settings > add `MainMenu` (index 0) and `Track01`. Scene names must
match `TrackSelectUI` entries and `GameSettings.SelectedTrackScene` defaults.
