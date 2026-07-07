using UnityEngine;

namespace ApexRush.Vehicle
{
    /// <summary>
    /// Arcade car controller built on WheelColliders.
    ///
    /// Design philosophy: the WheelColliders provide the plausible base (suspension,
    /// weight transfer, contact), then three arcade "cheats" layered on top make it
    /// forgiving and fun:
    ///   1. GRIP ASSIST  — gently rotates the velocity vector toward the car's facing,
    ///                     so the car goes where it points instead of ice-skating.
    ///   2. DRIFT STATE  — handbrake swaps rear tires to a slippery friction profile
    ///                     and adds yaw assist, so drifts start on demand and hold
    ///                     without sim-level throttle discipline.
    ///   3. STABILITY    — speed-sensitive steering, anti-roll bars, low center of
    ///                     mass, and downforce make spinouts/rollovers nearly impossible.
    ///
    /// Other systems (camera, audio, FX, HUD, AI) read the public properties at the
    /// bottom — this class has no dependencies on them.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VehicleController : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────── wheels ──

        [System.Serializable]
        public class Wheel
        {
            public WheelCollider collider;
            [Tooltip("Visual mesh that follows the physics wheel.")]
            public Transform mesh;
            public bool steer;      // front wheels
            public bool powered;    // RWD: rear only. AWD: all four.
            public bool handbrake;  // rear wheels
        }

        [Header("Wheels (FL, FR, RL, RR)")]
        [SerializeField] private Wheel[] wheels = new Wheel[4];

        // ─────────────────────────────────────────────────────────────── engine ──

        [Header("Engine")]
        [Tooltip("Peak wheel torque in N·m. First thing to raise if the car feels slow.")]
        [SerializeField] private float maxMotorTorque = 2200f;

        [Tooltip("Torque multiplier across normalized speed (0 = stopped, 1 = top speed). " +
                 "Default: strong launch, tapering to 0 at top speed — this IS your " +
                 "acceleration curve, tune it before anything else.")]
        [SerializeField] private AnimationCurve torqueCurve =
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.5f, 0.9f), new Keyframe(1f, 0f));

        [Tooltip("Soft cap in km/h. Torque falls to zero here (nitro can exceed it).")]
        [SerializeField] private float topSpeedKmh = 220f;

        [SerializeField] private float reverseTopSpeedKmh = 40f;

        [Header("Virtual Gearbox (audio/feel only — does not affect torque)")]
        [SerializeField] private int gearCount = 5;
        [Tooltip("Normalized RPM at which the virtual gearbox upshifts.")]
        [SerializeField, Range(0.5f, 1f)] private float shiftUpRpm = 0.95f;

        // ─────────────────────────────────────────────────────────────── brakes ──

        [Header("Brakes")]
        [SerializeField] private float brakeTorque = 4500f;
        [SerializeField] private float handbrakeTorque = 6000f;

        // ─────────────────────────────────────────────────────────────── steering ──

        [Header("Steering")]
        [SerializeField] private float maxSteerAngle = 32f;

        [Tooltip("Steer angle multiplier vs normalized speed. Tightens steering at speed " +
                 "so full lock at 200 km/h doesn't instantly spin you. If high-speed " +
                 "lane changes feel sluggish, raise the right end (e.g. 0.35 -> 0.5).")]
        [SerializeField] private AnimationCurve steerBySpeed =
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.5f, 0.55f), new Keyframe(1f, 0.35f));

        [Tooltip("How fast the wheels turn toward the target angle (deg/sec). " +
                 "Lower = heavier, more deliberate. Higher = twitchier.")]
        [SerializeField] private float steerSpeed = 180f;

        // ─────────────────────────────────────────────────────────────── drift ──

        [Header("Drift")]
        [Tooltip("Rear sideways-friction stiffness while drifting. 1 = full grip. " +
                 "Lower = longer, loopier slides. THE main drift feel knob.")]
        [SerializeField, Range(0.2f, 1f)] private float driftRearStiffness = 0.55f;

        [Tooltip("Extra yaw torque in the steer direction while drifting. Helps rotate " +
                 "the car into the slide so drifts feel snappy, not mushy.")]
        [SerializeField] private float driftYawAssist = 900f;

        [Tooltip("Slip angle (deg between facing and velocity) above which we count as " +
                 "drifting even without the handbrake held — keeps a slide alive after " +
                 "you release Space.")]
        [SerializeField] private float driftEntrySlipAngle = 12f;

        [Tooltip("Below this slip angle the drift state ends and rear grip returns.")]
        [SerializeField] private float driftExitSlipAngle = 5f;

        [Tooltip("Minimum speed (km/h) for drift mechanics — prevents parking-lot spins.")]
        [SerializeField] private float driftMinSpeedKmh = 40f;

        [Tooltip("How quickly rear grip fades in/out of drift. Higher = more abrupt.")]
        [SerializeField] private float driftGripLerpSpeed = 5f;

        // ─────────────────────────────────────────────────────────────── nitro ──

        [Header("Nitro")]
        [SerializeField] private float nitroForce = 9000f;
        [Tooltip("Seconds of nitro from a full tank.")]
        [SerializeField] private float nitroCapacity = 4f;
        [Tooltip("Passive refill per second (0 = only refill by drifting/pickups).")]
        [SerializeField] private float nitroRegenPerSecond = 0.15f;
        [Tooltip("Nitro earned per second of drifting — NFS-style 'drift to boost' loop.")]
        [SerializeField] private float nitroPerDriftSecond = 0.6f;

        // ─────────────────────────────────────────────────────────────── stability ──

        [Header("Stability (the 'forgiving' part)")]
        [Tooltip("0 = pure physics (slidey). 1 = full arcade rail-grip. This rotates the " +
                 "velocity vector toward the facing direction each frame. 0.9–0.98 feels " +
                 "like classic arcade racers. Reduced automatically while drifting.")]
        [SerializeField, Range(0f, 1f)] private float gripAssist = 0.94f;

        [Tooltip("Downforce (N per m/s of speed). Keeps the car planted at speed.")]
        [SerializeField] private float downforcePerSpeed = 55f;

        [Tooltip("Anti-roll bar stiffness. Fights body roll; too high causes inside-wheel lift.")]
        [SerializeField] private float antiRollForce = 8000f;

        [Tooltip("Center of mass offset from the Rigidbody's computed COM. Negative Y = " +
                 "lower = harder to flip. THE anti-rollover knob.")]
        [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0f, -0.5f, 0f);

        // ─────────────────────────────────────────────────────────────── state ──

        private Rigidbody rb;
        private VehicleInputState input;
        private float currentSteerAngle;
        private float baseRearStiffness = 1f;   // captured from the WheelCollider at startup
        private float rearStiffnessCurrent = 1f;
        private bool controlEnabled = true;      // RaceManager freezes cars during countdown

        // Public read-only state for camera / audio / FX / HUD / AI:
        public float SpeedKmh { get; private set; }
        public float NormalizedSpeed { get; private set; }     // 0..1 of top speed
        public float NormalizedRpm { get; private set; }       // 0..1, for engine audio pitch
        public int CurrentGear { get; private set; } = 1;      // 1-based; 0 = reverse
        public bool IsDrifting { get; private set; }
        public float SlipAngle { get; private set; }           // degrees
        public bool IsGrounded { get; private set; }
        public bool NitroActive { get; private set; }
        public float NitroAmount { get; private set; }         // 0..1 for the HUD gauge
        public Rigidbody Body => rb;
        public Wheel[] Wheels => wheels;

        // ─────────────────────────────────────────────────────────── lifecycle ──

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.centerOfMass += centerOfMassOffset;
            NitroAmount = 1f;

            // Remember the rear tire grip authored on the WheelCollider so drift
            // can scale it down and restore it exactly.
            foreach (var w in wheels)
            {
                if (w.handbrake) { baseRearStiffness = w.collider.sidewaysFriction.stiffness; break; }
            }
            rearStiffnessCurrent = baseRearStiffness;
        }

        /// <summary>Called by VehicleInput (player) or AIController (bots) every frame.</summary>
        public void SetInput(VehicleInputState newInput)
        {
            input = controlEnabled ? newInput : default;
        }

        /// <summary>RaceManager uses this to freeze cars during the countdown.</summary>
        public void SetControlEnabled(bool enabled)
        {
            controlEnabled = enabled;
            if (!enabled) input = default;
        }

        /// <summary>Refill nitro externally (pickups, race events).</summary>
        public void AddNitro(float normalizedAmount) =>
            NitroAmount = Mathf.Clamp01(NitroAmount + normalizedAmount);

        private void FixedUpdate()
        {
            UpdateTelemetry();
            ApplySteering();
            UpdateDriftState();
            ApplyDriveAndBrakes();
            ApplyNitro();
            ApplyGripAssist();
            ApplyDownforce();
            ApplyAntiRoll();
            UpdateVirtualGearbox();
        }

        private void Update()
        {
            // Visual-only: sync wheel meshes to their colliders every rendered frame.
            foreach (var w in wheels)
            {
                if (w.mesh == null) continue;
                w.collider.GetWorldPose(out Vector3 pos, out Quaternion rot);
                w.mesh.SetPositionAndRotation(pos, rot);
            }
        }

        // ─────────────────────────────────────────────────────────── telemetry ──

        private void UpdateTelemetry()
        {
            SpeedKmh = rb.velocity.magnitude * 3.6f;
            NormalizedSpeed = Mathf.Clamp01(SpeedKmh / topSpeedKmh);

            // Slip angle: how far the car's velocity points away from its nose.
            // Only meaningful above walking pace.
            Vector3 flatVel = Vector3.ProjectOnPlane(rb.velocity, transform.up);
            SlipAngle = flatVel.magnitude > 2f
                ? Vector3.SignedAngle(transform.forward, flatVel, transform.up)
                : 0f;

            int groundedCount = 0;
            foreach (var w in wheels)
                if (w.collider.isGrounded) groundedCount++;
            IsGrounded = groundedCount >= 3;
        }

        // ─────────────────────────────────────────────────────────── steering ──

        private void ApplySteering()
        {
            // Speed-sensitive: at top speed the wheels only get a fraction of full lock.
            float speedFactor = steerBySpeed.Evaluate(NormalizedSpeed);
            float targetAngle = input.Steer * maxSteerAngle * speedFactor;

            // Rate-limited approach instead of a snap: gives steering "weight".
            currentSteerAngle = Mathf.MoveTowards(
                currentSteerAngle, targetAngle, steerSpeed * Time.fixedDeltaTime);

            foreach (var w in wheels)
                if (w.steer) w.collider.steerAngle = currentSteerAngle;
        }

        // ─────────────────────────────────────────────────────────────── drift ──

        private void UpdateDriftState()
        {
            float absSlip = Mathf.Abs(SlipAngle);
            bool fastEnough = SpeedKmh > driftMinSpeedKmh;

            if (!IsDrifting)
            {
                // Enter: handbrake while turning, or the car is already sliding.
                bool handbrakeEntry = input.Handbrake && Mathf.Abs(input.Steer) > 0.1f;
                IsDrifting = fastEnough && IsGrounded && (handbrakeEntry || absSlip > driftEntrySlipAngle);
            }
            else
            {
                // Exit: slide has straightened out, or we've slowed to a crawl.
                if (absSlip < driftExitSlipAngle && !input.Handbrake) IsDrifting = false;
                if (!fastEnough) IsDrifting = false;
            }

            // Fade rear sideways grip toward the drift value (or back to normal).
            float targetStiffness = IsDrifting ? baseRearStiffness * driftRearStiffness : baseRearStiffness;
            rearStiffnessCurrent = Mathf.Lerp(
                rearStiffnessCurrent, targetStiffness, driftGripLerpSpeed * Time.fixedDeltaTime);

            foreach (var w in wheels)
            {
                if (!w.handbrake) continue;
                WheelFrictionCurve side = w.collider.sidewaysFriction;
                side.stiffness = rearStiffnessCurrent;
                w.collider.sidewaysFriction = side;
            }

            if (IsDrifting)
            {
                // Yaw assist: torque the car into the slide in the steered direction.
                // This is what makes the drift controllable with just the stick.
                rb.AddTorque(transform.up * (input.Steer * driftYawAssist), ForceMode.Force);

                // NFS-style economy: drifting charges nitro.
                AddNitro(nitroPerDriftSecond / nitroCapacity * Time.fixedDeltaTime);
            }
        }

        // ─────────────────────────────────────────────────────── drive/brakes ──

        private void ApplyDriveAndBrakes()
        {
            float forwardSpeed = Vector3.Dot(rb.velocity, transform.forward); // m/s, signed
            bool movingForward = forwardSpeed > 0.5f;
            bool movingBackward = forwardSpeed < -0.5f;

            float motor = 0f;
            float brake = 0f;

            if (input.Throttle > 0.01f)
            {
                if (movingBackward)
                    brake = brakeTorque * input.Throttle;          // W while reversing = brake
                else
                    motor = maxMotorTorque * input.Throttle * torqueCurve.Evaluate(NormalizedSpeed);
            }
            else if (input.Brake > 0.01f)
            {
                if (movingForward)
                    brake = brakeTorque * input.Brake;             // S while moving = brake...
                else if (SpeedKmh < reverseTopSpeedKmh)
                    motor = -maxMotorTorque * 0.5f * input.Brake;  // ...S while stopped = reverse
            }

            int poweredCount = 0;
            foreach (var w in wheels) if (w.powered) poweredCount++;

            foreach (var w in wheels)
            {
                w.collider.motorTorque = w.powered ? motor / Mathf.Max(1, poweredCount) : 0f;
                w.collider.brakeTorque = brake;

                if (input.Handbrake && w.handbrake)
                {
                    w.collider.brakeTorque = handbrakeTorque;
                    w.collider.motorTorque = 0f;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────── nitro ──

        private void ApplyNitro()
        {
            NitroActive = input.Nitro && NitroAmount > 0.01f && IsGrounded;

            if (NitroActive)
            {
                rb.AddForce(transform.forward * nitroForce, ForceMode.Force);
                NitroAmount = Mathf.Clamp01(NitroAmount - Time.fixedDeltaTime / nitroCapacity);
            }
            else
            {
                NitroAmount = Mathf.Clamp01(NitroAmount + nitroRegenPerSecond * Time.fixedDeltaTime);
            }
        }

        // ─────────────────────────────────────────────────────────── stability ──

        /// <summary>
        /// The core arcade cheat. Each physics step, rotate the horizontal velocity
        /// slightly toward where the car is pointing. The car "carves" instead of
        /// understeering into walls. Weakened while drifting so slides stay slides.
        /// </summary>
        private void ApplyGripAssist()
        {
            if (!IsGrounded || rb.velocity.sqrMagnitude < 4f) return;

            float assist = IsDrifting ? gripAssist * 0.35f : gripAssist;

            Vector3 flatVel = Vector3.ProjectOnPlane(rb.velocity, transform.up);
            Vector3 verticalVel = rb.velocity - flatVel;

            // Preserve speed magnitude and forward/backward sign; steer the direction.
            float sign = Mathf.Sign(Vector3.Dot(flatVel, transform.forward));
            Vector3 targetDir = transform.forward * sign;

            Vector3 newFlat = Vector3.Slerp(
                flatVel.normalized, targetDir, assist * Time.fixedDeltaTime * 5f) * flatVel.magnitude;

            rb.velocity = newFlat + verticalVel;
        }

        private void ApplyDownforce()
        {
            if (IsGrounded)
                rb.AddForce(-transform.up * (downforcePerSpeed * rb.velocity.magnitude));
        }

        /// <summary>
        /// Standard anti-roll bar: transfers suspension load between the two wheels
        /// of an axle, reducing body roll in corners. Runs per axle (0-1 front, 2-3 rear).
        /// </summary>
        private void ApplyAntiRoll()
        {
            for (int axle = 0; axle + 1 < wheels.Length; axle += 2)
            {
                WheelCollider left = wheels[axle].collider;
                WheelCollider right = wheels[axle + 1].collider;

                float travelL = GetSuspensionTravel(left);
                float travelR = GetSuspensionTravel(right);
                float force = (travelL - travelR) * antiRollForce;

                if (left.isGrounded)
                    rb.AddForceAtPosition(left.transform.up * -force, left.transform.position);
                if (right.isGrounded)
                    rb.AddForceAtPosition(right.transform.up * force, right.transform.position);
            }
        }

        private static float GetSuspensionTravel(WheelCollider wheel)
        {
            if (!wheel.GetGroundHit(out WheelHit hit)) return 1f; // fully extended in air
            return (-wheel.transform.InverseTransformPoint(hit.point).y - wheel.radius)
                   / wheel.suspensionDistance;
        }

        // ────────────────────────────────────────────────────── virtual gearbox ──

        /// <summary>
        /// Purely cosmetic gearbox: divides the speed range into gears and produces a
        /// sawtooth NormalizedRpm so engine audio rises and "shifts". Torque delivery
        /// is untouched — arcade cars shouldn't bog between gears.
        /// </summary>
        private void UpdateVirtualGearbox()
        {
            float forwardSpeed = Vector3.Dot(rb.velocity, transform.forward);
            if (forwardSpeed < -0.5f)
            {
                CurrentGear = 0; // reverse
                NormalizedRpm = Mathf.Clamp01(-forwardSpeed * 3.6f / reverseTopSpeedKmh);
                return;
            }

            float gearWidth = 1f / gearCount;
            CurrentGear = Mathf.Clamp(Mathf.FloorToInt(NormalizedSpeed / gearWidth) + 1, 1, gearCount);

            float posInGear = (NormalizedSpeed - (CurrentGear - 1) * gearWidth) / gearWidth;
            // Idle floor of 0.15 so the engine never sounds dead; scale into the shift point.
            NormalizedRpm = Mathf.Lerp(0.15f, shiftUpRpm, posInGear);
            if (input.Throttle < 0.01f) NormalizedRpm *= 0.7f; // off-throttle drop
        }
    }
}
