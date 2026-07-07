# Apex Rush — Arcade Racing Game

An original-IP, Need for Speed–style arcade racer, in two implementations:

- **`Assets/` + `Docs/`** — Unity (C#) version: scripts + Editor setup guides (this README).
- **`web/`** — Next.js + Three.js version: fully playable in the browser with zero
  assets or engine install — `cd web && npm install && npm run dev`. See `web/README.md`.

## Folder structure

```
Assets/
  Scripts/
    Core/           GameSettings (cross-scene state + options)
    Vehicle/        VehicleController, VehicleInput
    Race/           RaceManager, RaceParticipant, Checkpoint
    AI/             AIController, WaypointCircuit
    Camera/         ChaseCamera
    Audio/          EngineAudio
    FX/             VehicleFX (tire smoke, nitro, skid trails)
    UI/             HUDController, MinimapController
    Menu/           MainMenuController, CarSelectUI, TrackSelectUI, SettingsUI
  Prefabs/          Car, minimap blip, UI prefabs (you create from the docs)
  Materials/        Road, barrier, car paint
  Audio/            Engine loop, skid, music  (YOU SUPPLY)
  Models/           Car body + wheels, track  (YOU SUPPLY, or use primitives)
  Scenes/
    MainMenu.unity
    Track01.unity
Docs/
  Setup_Vehicle.md        Car build + handling tuning cheat sheet
  Setup_GameFeel.md       Chase camera, engine audio, particles
  Setup_Track_Race_AI.md  Track blockout, checkpoints, spawns, waypoints, RaceManager
  Setup_HUD_Menus.md      HUD canvas, minimap layer/RT, menu scene, full flow test
```

## What you supply vs. what code handles

| You supply (assets)                          | Code / Unity generates                      |
|----------------------------------------------|---------------------------------------------|
| Car body mesh + 4 wheel meshes (or cubes/cylinders to start) | All physics, handling, drift, nitro |
| Track road mesh + barrier meshes (or ProBuilder/planes)      | Checkpoints, lap logic, AI waypoints |
| Engine loop WAV, tire screech WAV, music     | Pitch/volume modulation, when to play        |
| Particle textures (optional)                 | Particle systems can use default material    |

## Build order — ALL SYSTEMS COMPLETE ✅

1. ✅ Vehicle physics — `Docs/Setup_Vehicle.md`
2. ✅ Camera + game feel — `Docs/Setup_GameFeel.md`
3. ✅ Track, checkpoints, lap/finish logic — `Docs/Setup_Track_Race_AI.md`
4. ✅ Race systems (countdown, timing, positions, Time Trial + Circuit)
5. ✅ AI opponents with difficulty + rubber-banding
6. ✅ HUD (speedo, timer, lap/position, minimap, nitro gauge) — `Docs/Setup_HUD_Menus.md`
7. ✅ Menus (main, car select, track select, settings)

## Recommended Editor build sequence

Work through the docs in order — each ends with a playable test:
1. `Setup_Vehicle.md` — car drives on a plane.
2. `Setup_GameFeel.md` — it feels fast (camera, audio, smoke).
3. `Setup_Track_Race_AI.md` — full race vs. AI in Track01.
4. `Setup_HUD_Menus.md` — HUD + menu-to-race-to-results flow.

## Architecture in one paragraph

`VehicleController` is the only physics owner; humans drive it through
`VehicleInput`, bots through `AIController` — both call `SetInput()`, so AI obeys
identical physics (fair rubber-banding). `RaceManager` spawns everything from
`GameSettings` (written by the menus), runs the countdown, and broadcasts events;
`HUDController`, `ChaseCamera`, `EngineAudio`, and `VehicleFX` are read-only
consumers of telemetry and events — you can delete any of them and the race
still runs.
