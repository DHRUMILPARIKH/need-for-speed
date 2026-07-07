# HUD & Menus Setup (Systems 6–7)

All UI uses built-in uGUI (Canvas + Text/Image/Button) — zero package installs.
If you prefer TextMeshPro, swap `Text` → `TMP_Text` in the scripts; nothing else changes.

## 1. Race HUD (in each track scene)

Create **UI > Canvas** (Screen Space – Overlay, Canvas Scaler: Scale With Screen
Size, 1920×1080) and build:

```
Canvas
├── Speedo (bottom-right)
│   ├── SpeedText        <- big bold Text "0"
│   ├── UnitText         <- small "km/h"
│   └── GearText         <- "1"
├── NitroBar (above speedo)
│   ├── Background       <- dark Image
│   └── NitroFill        <- Image, Image Type = FILLED, Horizontal  ⚠ must be Filled
├── RaceInfo (top-left)
│   ├── LapText          <- "LAP 1/3"
│   ├── PositionRow      <- container (hidden automatically in Time Trial)
│   │   └── PositionText <- "1st / 4"
│   ├── CurrentLapTimeText
│   └── BestLapTimeText
├── CountdownText (center, huge font, disabled by default)
├── Minimap (top-right)
│   └── MinimapImage     <- RawImage (see §2)
└── ResultsPanel (full-screen, disabled by default)
    ├── ResultsTitleText
    ├── ResultsDetailText
    ├── RestartButton    <- OnClick → HUDController.OnRestartPressed
    └── MenuButton       <- OnClick → HUDController.OnMenuPressed
```

Add `HUDController` to the Canvas and drag every reference in. Note
`PositionText` must sit inside a container row — the script hides the row's
parent in Time Trial mode.

## 2. Minimap (⚠ Editor-only steps)

1. **Layers**: Project Settings > Tags and Layers → add a layer named `Minimap`.
2. **RenderTexture**: Assets > Create > Render Texture, 256×256, name `RT_Minimap`.
3. **Minimap camera**: new Camera named `MinimapCamera` in the track scene:
   Projection **Orthographic**, Target Texture = `RT_Minimap`, Culling Mask =
   **Minimap + Default** (drop Default later if you make dedicated map art),
   Clear Flags = Solid Color (dark grey). **Delete its AudioListener.**
4. Add `MinimapController` (Scripts/UI) to it.
5. **Blip prefab**: a Quad, layer `Minimap`, Unlit/Color material, **remove its
   MeshCollider**. Save as prefab, assign to MinimapController's `blipPrefab`.
   The script instantiates one over each car and tints player vs. AI.
6. On the HUD's `MinimapImage` RawImage: Texture = `RT_Minimap`. Round it off
   with a mask (add an Image with a circle sprite + Mask component) if you like.

## 3. MainMenu scene

New scene `MainMenu` (Build Settings index 0):

```
MainMenu (scene)
├── MenuController        <- MainMenuController script
├── Canvas
│   ├── MainPanel         <- buttons: Play → ShowCarSelect, Settings → ShowSettings, Quit → QuitGame
│   ├── CarSelectPanel    <- CarSelectUI; ◄ ► buttons → PreviousCar/NextCar,
│   │                        Next → ShowTrackSelect, Back → ShowMain
│   ├── TrackSelectPanel  <- TrackSelectUI; ◄ ► track arrows, Circuit toggle,
│   │                        Laps slider (Whole Numbers, 1–9), Opponents slider (1–7),
│   │                        RACE button → MainMenuController.StartRace, Back → ShowCarSelect
│   └── SettingsPanel     <- SettingsUI; volume Slider (0–1), quality Dropdown,
│                            shake Toggle, difficulty Dropdown, Back → ShowMain
├── Turntable             <- empty at world origin-ish, in front of a menu camera
│   ├── Car0_Display      <- visual-only copies of your car models
│   └── Car1_Display ...
├── Menu Camera + Light   <- frames the turntable behind the UI
```

Wire `MainMenuController`'s four panel references, `CarSelectUI`'s entries
(name/blurb/display model — **same order as RaceManager.carPrefabs**), and
`TrackSelectUI`'s track list (sceneName must match Build Settings exactly).

Sliders: tick **Whole Numbers**. The scripts populate dropdown options and
persist everything to PlayerPrefs automatically.

## 4. Full game-flow test

1. Play from MainMenu: pick car → pick track, Circuit, 2 laps, 3 opponents → RACE.
2. Countdown holds all cars → GO releases them.
3. HUD: speed climbs, nitro drains/refills, lap counter ticks at the start line,
   position updates as you pass AI, minimap follows.
4. Finish both laps → results panel with lap breakdown → Restart and Menu buttons.
5. Repeat in Time Trial: no AI, no position readout, best-lap flagged in results.
