# Game Feel Setup (System 2): Camera, Audio, FX

## 1. Chase camera

1. Select **Main Camera** (must be a scene root object — **not** parented to the car).
2. Add `ChaseCamera` (Scripts/Camera).
3. Leave the Target field **empty** in race scenes — RaceManager assigns the spawned
   player car via `SetTarget()`. Only drag a car in manually for physics test scenes.
4. Add an **AudioListener** if the camera doesn't have one (default cameras do).

Feel knobs, in order of impact:
- `rotationDamping` **4 → 2.5** for more dramatic drift angles (the car rotates,
  the camera lags, you see the side of the car).
- `speedFovBoost` / `nitroFovKick` — the sensation of speed. Raise before touching
  any physics value if the game "feels slow".
- `followDamping` — lower = camera trails on acceleration (good), too low = seasick.

Camera shake honors the Settings menu toggle automatically. Call
`chaseCamera.AddImpactShake(0.5f)` from any collision handler you add later.

## 2. Engine / skid / nitro audio

On the **Car prefab**:
1. Create three empty children: `Audio_Engine`, `Audio_Skid`, `Audio_Nitro`,
   each with an **AudioSource**: *Loop ON, Play On Awake OFF, Spatial Blend = 1 (3D)*.
2. Assign your clips (**you supply**: an engine loop ~2s seamless, a tire screech
   loop, a whoosh/flame loop — freesound.org has CC0 options).
3. Add `EngineAudio` (Scripts/Audio) to the car root, drag the three sources in.

Tuning: if the engine sounds frantic at idle, drop `minPitch` to 0.4. If shifts
aren't audible, widen the pitch range — the sawtooth RPM from the virtual gearbox
does the shifting, the pitch range is the amplification.

## 3. Tire smoke + nitro particles

On the **Car prefab**:
1. Two Particle Systems `Smoke_RL`, `Smoke_RR` positioned at each rear wheel's
   ground contact. Settings for convincing tire smoke:
   - Start Lifetime 1.2, Start Speed 0.5, Start Size 1→2.5 (random), Start Color
     white @ ~35% alpha
   - Emission: Rate over Time **40**
   - Shape: Hemisphere, radius 0.2
   - Color over Lifetime: fade alpha to 0
   - Size over Lifetime: grow ×2
   - Renderer material: Default-Particle works fine to start
2. One or two Particle Systems on the exhaust(s) for nitro: Start Lifetime 0.15,
   Start Speed 8 (pointing backward), Start Size 0.3, orange→blue Color over
   Lifetime, Emission Rate 120.
3. **On every one of these systems: untick the Emission module's checkbox** —
   the `VehicleFX` script enables it at runtime; if you leave it on they smoke forever.
4. Optional skid marks: TrailRenderer on each rear wheel position (width 0.3,
   time 4, dark semi-transparent material).
5. Add `VehicleFX` (Scripts/FX) to the car root; fill the arrays.

Everything here is data-driven: FX styling changes never require code edits.
