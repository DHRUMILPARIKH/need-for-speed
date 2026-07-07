using UnityEngine;
using ApexRush.Vehicle;
using ApexRush.Race;

namespace ApexRush.AI
{
    /// <summary>
    /// Waypoint-following driver. Feeds VehicleController.SetInput() with the same
    /// struct a human produces, so AI cars obey identical physics — no cheating
    /// torque, which keeps rubber-banding honest and collisions fair.
    ///
    /// Three layers:
    ///   STEERING — aims at a point ahead on the circuit; look-ahead grows with
    ///              speed so fast cars cut smooth lines instead of zigzagging.
    ///   SPEED    — measures the corner angle further ahead and brakes toward a
    ///              target speed; difficulty scales how hard it commits.
    ///   RUBBER-BAND — compares race progress to the player and nudges throttle
    ///              cap ±, plus uses nitro when far behind. Kept subtle: it should
    ///              read as "competitive AI", never as teleporting rivals.
    ///
    /// RaceManager adds and Configure()s this at spawn — nothing to set up in the
    /// Editor beyond the WaypointCircuit itself.
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class AIController : MonoBehaviour
    {
        [Header("Steering")]
        [SerializeField] private float lookAheadBase = 10f;
        [SerializeField] private float lookAheadPerSpeed = 0.45f;   // meters per m/s
        [SerializeField] private float waypointReachRadius = 8f;
        [Tooltip("Steering angle (deg to target) that maps to full lock.")]
        [SerializeField] private float fullSteerAngle = 40f;

        [Header("Cornering")]
        [Tooltip("How far ahead (waypoints) to sample the corner for braking decisions.")]
        [SerializeField] private int cornerLookAheadPoints = 3;
        [SerializeField] private float minCornerSpeedKmh = 55f;
        [SerializeField] private float maxSpeedKmh = 210f;

        [Header("Rubber-banding")]
        [Tooltip("Progress deficit (in checkpoints) at which catch-up reaches full strength.")]
        [SerializeField] private float rubberBandRange = 4f;
        [Tooltip("Max throttle bonus when far behind / penalty when far ahead. 0.15 = ±15%.")]
        [SerializeField, Range(0f, 0.4f)] private float rubberBandStrength = 0.15f;

        private VehicleController vehicle;
        private WaypointCircuit circuit;
        private RaceManager race;
        private RaceParticipant self;
        private int currentWaypoint;

        // Difficulty-derived caps (set in Configure).
        private float throttleCap = 0.9f;
        private float cornerCommit = 1f;   // >1 = carries more speed through corners
        private float steerJitter;         // easy AI wobbles slightly, reads as "human"

        // Stuck recovery.
        private float stuckTimer;
        private float reverseTimer;

        public void Configure(WaypointCircuit waypoints, int difficulty, RaceManager manager, RaceParticipant participant)
        {
            circuit = waypoints;
            race = manager;
            self = participant;
            currentWaypoint = circuit.GetNearestIndex(transform.position);

            switch (difficulty)
            {
                case 0: throttleCap = 0.75f; cornerCommit = 0.85f; steerJitter = 0.06f; break; // easy
                case 1: throttleCap = 0.90f; cornerCommit = 1.00f; steerJitter = 0.02f; break; // normal
                default: throttleCap = 1.00f; cornerCommit = 1.15f; steerJitter = 0f; break; // hard
            }
        }

        private void Awake()
        {
            vehicle = GetComponent<VehicleController>();
        }

        private void Update()
        {
            if (circuit == null || circuit.Count < 2) return;

            AdvanceWaypoint();

            var input = new VehicleInputState();
            float speed = vehicle.SpeedKmh;

            // ── Steering: chase a point ahead on the line ──
            Vector3 target = LookAheadPoint();
            Vector3 toTarget = target - transform.position;
            float angle = Vector3.SignedAngle(transform.forward, toTarget, Vector3.up);
            input.Steer = Mathf.Clamp(angle / fullSteerAngle, -1f, 1f)
                          + Mathf.Sin(Time.time * 1.3f) * steerJitter;

            // ── Speed: brake for the corner ahead ──
            float desired = DesiredSpeedKmh();
            if (speed < desired) input.Throttle = throttleCap * RubberBandFactor();
            else if (speed > desired * 1.12f) input.Brake = 0.8f;

            // ── Rubber-band nitro: only when well behind and pointing straight ──
            if (BehindPlayerBy() > rubberBandRange * 0.5f && Mathf.Abs(angle) < 8f
                && vehicle.NitroAmount > 0.4f)
                input.Nitro = true;

            HandleStuckRecovery(ref input, speed);
            vehicle.SetInput(input);
        }

        private void AdvanceWaypoint()
        {
            Vector3 wp = circuit.GetPosition(currentWaypoint);
            if (Vector3.Distance(transform.position, wp) < waypointReachRadius)
                currentWaypoint = (currentWaypoint + 1) % circuit.Count;
        }

        private Vector3 LookAheadPoint()
        {
            // Walk forward along the circuit by a speed-scaled distance.
            float lookAhead = lookAheadBase + vehicle.Body.velocity.magnitude * lookAheadPerSpeed;
            int idx = currentWaypoint;
            Vector3 pos = circuit.GetPosition(idx);
            float travelled = Vector3.Distance(transform.position, pos);
            while (travelled < lookAhead)
            {
                Vector3 next = circuit.GetPosition(idx + 1);
                travelled += Vector3.Distance(pos, next);
                pos = next;
                idx++;
            }
            return pos;
        }

        private float DesiredSpeedKmh()
        {
            // Total direction change across the next few waypoints ≈ corner severity.
            float bend = 0f;
            for (int i = 0; i < cornerLookAheadPoints; i++)
            {
                Vector3 a = circuit.GetPosition(currentWaypoint + i) - circuit.GetPosition(currentWaypoint + i - 1);
                Vector3 b = circuit.GetPosition(currentWaypoint + i + 1) - circuit.GetPosition(currentWaypoint + i);
                bend += Vector3.Angle(a, b);
            }
            // 0° bend = flat out; 120°+ = hairpin crawl.
            float t = Mathf.Clamp01(bend / 120f);
            return Mathf.Lerp(maxSpeedKmh, minCornerSpeedKmh, t) * cornerCommit;
        }

        /// <summary>Positive = this AI is behind the player, in checkpoint units.</summary>
        private float BehindPlayerBy()
        {
            if (race == null || race.Player == null || self == null) return 0f;
            return race.Player.Progress - self.Progress;
        }

        private float RubberBandFactor()
        {
            float deficit = Mathf.Clamp(BehindPlayerBy() / rubberBandRange, -1f, 1f);
            return 1f + deficit * rubberBandStrength; // behind → faster, ahead → slower
        }

        private void HandleStuckRecovery(ref VehicleInputState input, float speed)
        {
            bool racing = race == null || race.State == RaceManager.RaceState.Racing;

            if (reverseTimer > 0f)
            {
                // Back out, steering away from where we were trying to go.
                reverseTimer -= Time.deltaTime;
                input.Throttle = 0f;
                input.Brake = 1f;
                input.Steer = -input.Steer;
                input.Nitro = false;
                return;
            }

            if (racing && speed < 4f && input.Throttle > 0.1f)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer > 2f) { reverseTimer = 1.4f; stuckTimer = 0f; }
            }
            else stuckTimer = 0f;
        }
    }
}
