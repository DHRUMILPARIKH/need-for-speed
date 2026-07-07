using UnityEngine;

namespace ApexRush.Vehicle
{
    /// <summary>
    /// Reads player input (keyboard + gamepad) and exposes it as a clean struct
    /// that VehicleController consumes. Uses the legacy Input Manager so it works
    /// in a fresh project with zero setup: "Horizontal"/"Vertical" already map to
    /// WASD, arrow keys AND the gamepad left stick out of the box.
    ///
    /// AI cars do NOT use this component — AIController writes to
    /// VehicleController.SetInput() directly, so the car code never cares
    /// whether a human or a bot is driving.
    /// </summary>
    public class VehicleInput : MonoBehaviour
    {
        [Header("Keyboard")]
        [SerializeField] private KeyCode handbrakeKey = KeyCode.Space;
        [SerializeField] private KeyCode nitroKey = KeyCode.LeftShift;

        [Header("Gamepad")]
        [Tooltip("Xbox layout: JoystickButton1 = B (handbrake).")]
        [SerializeField] private KeyCode handbrakeButton = KeyCode.JoystickButton1;
        [Tooltip("Xbox layout: JoystickButton0 = A (nitro).")]
        [SerializeField] private KeyCode nitroButton = KeyCode.JoystickButton0;

        private VehicleController vehicle;

        private void Awake()
        {
            vehicle = GetComponent<VehicleController>();
        }

        private void Update()
        {
            var input = new VehicleInputState
            {
                // GetAxis (not GetAxisRaw) gives us free smoothing on keyboard
                // while remaining 1:1 on an analog stick.
                Steer = Input.GetAxis("Horizontal"),
                Throttle = Mathf.Clamp01(Input.GetAxis("Vertical")),
                Brake = Mathf.Clamp01(-Input.GetAxis("Vertical")),
                Handbrake = Input.GetKey(handbrakeKey) || Input.GetKey(handbrakeButton),
                Nitro = Input.GetKey(nitroKey) || Input.GetKey(nitroButton),
            };

            vehicle.SetInput(input);
        }
    }

    /// <summary>Frame snapshot of driver intent. Filled by VehicleInput or AIController.</summary>
    public struct VehicleInputState
    {
        public float Steer;      // -1..1
        public float Throttle;   //  0..1
        public float Brake;      //  0..1 (doubles as reverse when stopped)
        public bool Handbrake;
        public bool Nitro;
    }
}
