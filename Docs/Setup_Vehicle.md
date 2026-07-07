# Vehicle Setup Guide (System 1)

How to build a drivable car in the Editor from the two scripts in
`Assets/Scripts/Vehicle/`. Takes ~10 minutes with primitive meshes.

## 1. Project prep

- Unity 2021.3 LTS or newer, 3D template (Built-in RP or URP both fine).
- Copy the `Assets/` folder from this repo into your project.
- **Physics timestep** (Edit > Project Settings > Time): set *Fixed Timestep* to
  `0.01` (100 Hz). WheelColliders are noticeably more stable at 100 Hz than the
  default 50 Hz — this alone fixes a lot of jitter.

## 2. Car GameObject hierarchy

Build exactly this (names matter only for your sanity):

```
Car                          <- Rigidbody + VehicleController + VehicleInput + BoxCollider
├── Body                     <- your car mesh, or a stretched Cube (visual only, NO collider)
├── WheelColliders           <- empty child at (0,0,0)
│   ├── FL  (WheelCollider)
│   ├── FR  (WheelCollider)
│   ├── RL  (WheelCollider)
│   └── RR  (WheelCollider)
└── WheelMeshes              <- empty child at (0,0,0)
    ├── FL_Mesh              <- wheel model or a flattened Cylinder (rotate 90° on Z)
    ├── FR_Mesh
    ├── RL_Mesh
    └── RR_Mesh
```

Placeholder sizing that works with the default tuning: Body cube scaled to
`(1.8, 0.6, 4.2)`, wheel colliders at roughly `(±0.8, 0.1, ±1.3)` local position,
i.e. slightly *below* the body so the suspension has room.

**⚠ Editor-only requirements code can't fix:**
- The Car root's **blue Z axis must point forward** (nose of the car). Everything
  — torque, drift, slip angle — assumes +Z = forward.
- WheelColliders must be on a **child** of the Rigidbody object, never on the
  Rigidbody object itself, and never nested under another collider.
- The Body mesh must **not** have its own collider overlapping the wheels; give
  the *root* one BoxCollider that covers the chassis but stops above the wheel
  radius (raise its center Y).

## 3. Component settings

### Rigidbody (on Car root)
| Field | Value | Why |
|---|---|---|
| Mass | **1400** | All force numbers in VehicleController are tuned for ~1400 kg |
| Drag | 0.05 | Tiny bit of air resistance |
| Angular Drag | 1.5 | Damps twitchy rotation |
| Interpolate | **Interpolate** | Smooth visuals at 100 Hz physics |
| Collision Detection | Continuous | No tunneling through barriers |

### Each WheelCollider
| Field | Value |
|---|---|
| Mass | 25 |
| Radius | 0.35 (match your wheel mesh) |
| Wheel Damping Rate | 1 |
| Suspension Distance | 0.25 |
| Force App Point Distance | 0 |
| **Spring** | 45000 |
| **Damper** | 4000 |
| Target Position | 0.5 |

Friction curves (both Forward and Sideways unless noted):

| Field | Forward | Sideways |
|---|---|---|
| Extremum Slip | 0.4 | 0.25 |
| Extremum Value | 1.2 | 1.4 |
| Asymptote Slip | 0.8 | 0.6 |
| Asymptote Value | 0.7 | 0.9 |
| **Stiffness** | 1.5 | 1.8 |

Sideways stiffness is deliberately high — the drift system scales it *down* at
runtime on the rear wheels, and grip assist covers the front. High base grip +
scripted grip release = the arcade formula.

### VehicleController (on Car root)
Drag the four entries into the **Wheels** array **in order FL, FR, RL, RR**
(anti-roll bars pair them as axles: 0+1 = front, 2+3 = rear). Per entry:

| Wheel | collider | mesh | steer | powered | handbrake |
|---|---|---|---|---|---|
| FL | FL | FL_Mesh | ✔ | ✖ | ✖ |
| FR | FR | FR_Mesh | ✔ | ✖ | ✖ |
| RL | RL | RL_Mesh | ✖ | ✔ | ✔ |
| RR | RR | RR_Mesh | ✖ | ✔ | ✔ |

(That's RWD. For AWD: tick `powered` on all four and drop `maxMotorTorque` ~20%.)

Leave every other inspector value at its default — they're tuned as a set.

### VehicleInput (on Car root)
Defaults work immediately: WASD/arrows or left stick to drive, **Space** / B
handbrake, **Left Shift** / A nitro.

## 4. Test scene

1. New scene → Plane scaled to `(100, 1, 100)` as ground.
2. Drop the Car ~0.5 m above the plane.
3. Temporary camera: parent Main Camera to the Car at local `(0, 3, -7)`,
   rotated ~15° down. (The real ChaseCamera is System 2 — a hard-parented
   camera will feel stiff; that's expected for now.)
4. Press Play. You should be able to: accelerate to ~220 km/h, brake, reverse,
   Space+steer into a smoky-feeling slide that holds after releasing Space,
   and Shift-boost (watch it drain then regen).
5. Save the Car as a prefab in `Assets/Prefabs/`.

## 5. Tuning cheat sheet — "it feels X" → "change Y"

| Complaint | Fix |
|---|---|
| Too floaty / boat-like | Raise WheelCollider Spring (→60000) and Damper (→5000); more negative `centerOfMassOffset.y` |
| Understeers / won't turn in | Raise `gripAssist` (→0.97); raise sideways Stiffness on **front** wheels only |
| Spins out too easily | Lower right end of `steerBySpeed`; raise `angular drag` (→2); lower `driftYawAssist` |
| Drift won't initiate | Lower `driftRearStiffness` (→0.4); lower `driftEntrySlipAngle` (→8); lower `driftMinSpeedKmh` |
| Drift ends too fast / snaps straight | Lower `driftExitSlipAngle` (→3); lower `driftGripLerpSpeed` (→3) |
| Drift is uncontrollable | Raise `driftRearStiffness` (→0.7); raise `gripAssist` |
| Sluggish acceleration | Raise `maxMotorTorque`; flatten the middle of `torqueCurve` |
| Twitchy at high speed | Lower right end of `steerBySpeed` (→0.25); lower `steerSpeed` (→140) |
| Flips in corners | More negative `centerOfMassOffset.y`; lower `antiRollForce` if inside wheels lift |
| Nitro feels weak | Raise `nitroForce` (→12000) — System 2's FOV kick will add a lot of *perceived* punch |

Tune in this order: **torqueCurve → steerBySpeed → gripAssist → drift block**.
Each layer sits on the previous one; drift tuning is meaningless while base
grip is still wrong.

## 6. What's next

Say "next system" and we build the chase camera + game feel (speed FOV, shake,
tire smoke, engine audio) — that's where this controller starts feeling like NFS.
