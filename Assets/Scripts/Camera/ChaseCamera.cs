using UnityEngine;
using ApexRush.Vehicle;
using ApexRush.Core;

namespace ApexRush.CameraSystem
{
    /// <summary>
    /// Third-person chase camera. The game-feel workhorse:
    ///   - Damped follow with rotation lag, so the car visibly rotates ahead of the
    ///     camera in corners and drifts (this "separation" is 80% of the drift feel).
    ///   - Speed-based FOV: widens with speed, kicks hard on nitro. FOV change is the
    ///     cheapest, strongest speed cue in racing games.
    ///   - Perlin-noise shake scaled by speed and nitro (never random jitter — Perlin
    ///     reads as vibration, Random.insideUnitSphere reads as a bug).
    /// Put this on the Main Camera (NOT parented to the car).
    /// </summary>
    public class ChaseCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private VehicleController target;

        [Header("Framing")]
        [SerializeField] private float distance = 6.5f;
        [SerializeField] private float height = 2.4f;
        [Tooltip("Where the camera looks, above the car's pivot.")]
        [SerializeField] private float lookHeight = 1.2f;

        [Header("Response")]
        [Tooltip("Position catch-up speed. Lower = camera trails further at speed.")]
        [SerializeField] private float followDamping = 6f;
        [Tooltip("Rotation catch-up. LOWER THIS for more dramatic drift angles on screen.")]
        [SerializeField] private float rotationDamping = 4f;

        [Header("FOV")]
        [SerializeField] private float baseFov = 58f;
        [Tooltip("Extra FOV at top speed.")]
        [SerializeField] private float speedFovBoost = 18f;
        [Tooltip("Extra FOV while nitro is firing — the 'lunge'.")]
        [SerializeField] private float nitroFovKick = 14f;
        [SerializeField] private float fovDamping = 5f;

        [Header("Shake")]
        [SerializeField] private float speedShakeAmount = 0.06f;
        [SerializeField] private float nitroShakeAmount = 0.18f;
        [SerializeField] private float shakeFrequency = 18f;

        private Camera cam;
        private float currentYaw;
        private float impactShake; // decaying one-shot shake, fed by AddImpactShake()

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        /// <summary>RaceManager calls this after spawning the player's car.</summary>
        public void SetTarget(VehicleController newTarget)
        {
            target = newTarget;
            if (target != null)
            {
                // Snap behind the car instantly so there's no swoop on spawn.
                currentYaw = target.transform.eulerAngles.y;
                UpdatePosition(1000f);
            }
        }

        /// <summary>One-shot shake for collisions, landings, etc. 0.1 = tap, 1 = big hit.</summary>
        public void AddImpactShake(float amount) => impactShake = Mathf.Max(impactShake, amount);

        private void LateUpdate()
        {
            if (target == null) return;
            UpdatePosition(Time.deltaTime);
            UpdateFov();
            ApplyShake();
        }

        private void UpdatePosition(float dt)
        {
            // Chase the car's yaw only — ignoring pitch/roll keeps the horizon stable
            // over bumps and jumps.
            float targetYaw = target.transform.eulerAngles.y;
            currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, rotationDamping * dt);

            Quaternion yawRot = Quaternion.Euler(0f, currentYaw, 0f);
            Vector3 desiredPos = target.transform.position
                                 + yawRot * new Vector3(0f, height, -distance);

            transform.position = Vector3.Lerp(transform.position, desiredPos,
                1f - Mathf.Exp(-followDamping * dt)); // frame-rate independent damping

            transform.LookAt(target.transform.position + Vector3.up * lookHeight);
        }

        private void UpdateFov()
        {
            float targetFov = baseFov
                              + speedFovBoost * target.NormalizedSpeed
                              + (target.NitroActive ? nitroFovKick : 0f);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, fovDamping * Time.deltaTime);
        }

        private void ApplyShake()
        {
            impactShake = Mathf.MoveTowards(impactShake, 0f, Time.deltaTime * 2f);
            if (!GameSettings.CameraShake) return;

            float amount = speedShakeAmount * target.NormalizedSpeed * target.NormalizedSpeed
                           + (target.NitroActive ? nitroShakeAmount : 0f)
                           + impactShake * 0.5f;
            if (amount < 0.001f) return;

            float t = Time.time * shakeFrequency;
            Vector3 offset = new Vector3(
                (Mathf.PerlinNoise(t, 0f) - 0.5f),
                (Mathf.PerlinNoise(0f, t) - 0.5f),
                0f) * (2f * amount);

            transform.position += transform.rotation * offset;
        }
    }
}
