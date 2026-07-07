using UnityEngine;
using ApexRush.Vehicle;

namespace ApexRush.FX
{
    /// <summary>
    /// Toggles the car's particle effects from VehicleController telemetry:
    ///   - Tire smoke at each rear wheel while drifting and grounded.
    ///   - Nitro flames/trail while boosting.
    /// The particle systems themselves are authored in the Editor (settings in
    /// Docs/Setup_GameFeel.md) — this script only turns emission on/off, so
    /// designers can restyle the effects without touching code.
    /// </summary>
    public class VehicleFX : MonoBehaviour
    {
        [Header("Tire smoke (one per rear wheel, at ground level)")]
        [SerializeField] private ParticleSystem[] driftSmoke;

        [Header("Nitro (exhaust flames, speed-line trail, etc.)")]
        [SerializeField] private ParticleSystem[] nitroFx;

        [Header("Optional skid marks (TrailRenderers at rear wheels)")]
        [SerializeField] private TrailRenderer[] skidTrails;

        private VehicleController vehicle;

        private void Awake()
        {
            vehicle = GetComponent<VehicleController>();
        }

        private void Update()
        {
            bool smoking = vehicle.IsDrifting && vehicle.IsGrounded;
            foreach (var ps in driftSmoke) SetEmission(ps, smoking);
            foreach (var tr in skidTrails) if (tr != null) tr.emitting = smoking;

            foreach (var ps in nitroFx) SetEmission(ps, vehicle.NitroActive);
        }

        private static void SetEmission(ParticleSystem ps, bool on)
        {
            if (ps == null) return;
            var emission = ps.emission;
            if (emission.enabled == on) return;
            emission.enabled = on;
            if (on && !ps.isPlaying) ps.Play();
        }
    }
}
