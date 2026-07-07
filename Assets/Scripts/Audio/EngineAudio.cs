using UnityEngine;
using ApexRush.Vehicle;

namespace ApexRush.Audio
{
    /// <summary>
    /// Drives three looping AudioSources from VehicleController telemetry:
    ///   engine — pitch follows NormalizedRpm (the virtual gearbox's sawtooth makes
    ///            it rise-and-drop through "shifts" automatically),
    ///   skid   — fades in with slip angle / drifting,
    ///   nitro  — plays while boosting.
    /// Attach to the car; assign sources that live on child objects.
    /// You supply the clips (any engine loop, tire screech loop, air/flame loop).
    /// </summary>
    public class EngineAudio : MonoBehaviour
    {
        [Header("Sources (looping, Play On Awake off)")]
        [SerializeField] private AudioSource engineSource;
        [SerializeField] private AudioSource skidSource;
        [SerializeField] private AudioSource nitroSource;

        [Header("Engine pitch")]
        [Tooltip("Pitch at idle. Match to your clip: if the loop was recorded at high " +
                 "revs, use a lower min (0.4) so idle doesn't sound frantic.")]
        [SerializeField] private float minPitch = 0.6f;
        [SerializeField] private float maxPitch = 1.9f;
        [SerializeField] private float pitchDamping = 6f;

        [Header("Skid")]
        [Tooltip("Slip angle (deg) at which the screech reaches full volume.")]
        [SerializeField] private float fullSkidSlipAngle = 25f;
        [SerializeField] private float skidFadeSpeed = 8f;

        private VehicleController vehicle;

        private void Awake()
        {
            vehicle = GetComponent<VehicleController>();
            if (engineSource != null) { engineSource.loop = true; engineSource.Play(); }
            if (skidSource != null) { skidSource.loop = true; skidSource.volume = 0f; skidSource.Play(); }
            if (nitroSource != null) nitroSource.loop = true;
        }

        private void Update()
        {
            if (engineSource != null)
            {
                float targetPitch = Mathf.Lerp(minPitch, maxPitch, vehicle.NormalizedRpm);
                engineSource.pitch = Mathf.Lerp(engineSource.pitch, targetPitch,
                    pitchDamping * Time.deltaTime);
            }

            if (skidSource != null)
            {
                bool skidding = vehicle.IsDrifting && vehicle.IsGrounded;
                float targetVol = skidding
                    ? Mathf.Clamp01(Mathf.Abs(vehicle.SlipAngle) / fullSkidSlipAngle)
                    : 0f;
                skidSource.volume = Mathf.MoveTowards(skidSource.volume, targetVol,
                    skidFadeSpeed * Time.deltaTime);
            }

            if (nitroSource != null)
            {
                if (vehicle.NitroActive && !nitroSource.isPlaying) nitroSource.Play();
                else if (!vehicle.NitroActive && nitroSource.isPlaying) nitroSource.Stop();
            }
        }
    }
}
