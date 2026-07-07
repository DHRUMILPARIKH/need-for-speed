using UnityEngine;
using ApexRush.Race;

namespace ApexRush.UI
{
    /// <summary>
    /// Drives a top-down orthographic minimap camera that follows the player.
    /// The camera renders only the "Minimap" layer to a RenderTexture shown in a
    /// RawImage on the HUD; cars get a bright unlit quad ("blip") on that layer.
    /// Setup steps in Docs/Setup_HUD.md — the layer and RenderTexture must be
    /// created in the Editor.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class MinimapController : MonoBehaviour
    {
        [SerializeField] private float height = 120f;
        [SerializeField] private float zoom = 90f;
        [Tooltip("Rotate the map with the car (true) or keep north-up (false).")]
        [SerializeField] private bool rotateWithPlayer = true;

        [Header("Blips (spawned onto each car)")]
        [SerializeField] private GameObject blipPrefab;      // unlit quad, Minimap layer
        [SerializeField] private Color playerColor = new Color(0.2f, 0.8f, 1f);
        [SerializeField] private Color aiColor = new Color(1f, 0.3f, 0.2f);
        [SerializeField] private float blipScale = 8f;

        private Transform player;

        private void Start()
        {
            GetComponent<Camera>().orthographicSize = zoom;

            // Attach a blip above every car so it shows through geometry.
            foreach (var p in RaceManager.Instance.Participants)
            {
                if (p.IsPlayer) player = p.transform;
                if (blipPrefab == null) continue;

                GameObject blip = Instantiate(blipPrefab, p.transform);
                blip.transform.localPosition = Vector3.up * 6f;
                blip.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                blip.transform.localScale = Vector3.one * blipScale;

                var rend = blip.GetComponent<Renderer>();
                if (rend != null) rend.material.color = p.IsPlayer ? playerColor : aiColor;
            }
        }

        private void LateUpdate()
        {
            if (player == null) return;
            transform.position = player.position + Vector3.up * height;
            transform.rotation = rotateWithPlayer
                ? Quaternion.Euler(90f, player.eulerAngles.y, 0f)
                : Quaternion.Euler(90f, 0f, 0f);
        }
    }
}
