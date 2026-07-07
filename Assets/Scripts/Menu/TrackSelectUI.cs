using UnityEngine;
using UnityEngine.UI;
using ApexRush.Core;

namespace ApexRush.Menu
{
    /// <summary>
    /// Track + race-setup screen: pick track, mode (Circuit / Time Trial),
    /// lap count, and opponent count. Writes everything to GameSettings;
    /// MainMenuController.StartRace() then loads the chosen scene.
    /// </summary>
    public class TrackSelectUI : MonoBehaviour
    {
        [System.Serializable]
        public class TrackEntry
        {
            public string displayName = "Harbor Loop";
            public string sceneName = "Track01";   // must be in Build Settings!
            public Sprite thumbnail;
        }

        [SerializeField] private TrackEntry[] tracks;
        [SerializeField] private Text trackNameText;
        [SerializeField] private Image thumbnailImage;

        [Header("Race setup widgets")]
        [SerializeField] private Toggle circuitToggle;     // off = Time Trial
        [SerializeField] private Slider lapsSlider;        // whole numbers 1..9
        [SerializeField] private Text lapsValueText;
        [SerializeField] private Slider aiCountSlider;     // whole numbers 1..7
        [SerializeField] private Text aiCountValueText;
        [SerializeField] private GameObject circuitOnlyGroup; // AI slider row, hidden in TT

        private int trackIndex;

        private void OnEnable()
        {
            // Reflect current settings into the widgets.
            circuitToggle.isOn = GameSettings.Mode == GameSettings.RaceMode.Circuit;
            lapsSlider.value = GameSettings.Laps;
            aiCountSlider.value = GameSettings.AICount;

            circuitToggle.onValueChanged.AddListener(_ => Apply());
            lapsSlider.onValueChanged.AddListener(_ => Apply());
            aiCountSlider.onValueChanged.AddListener(_ => Apply());
            Apply();
            RefreshTrack();
        }

        private void OnDisable()
        {
            circuitToggle.onValueChanged.RemoveAllListeners();
            lapsSlider.onValueChanged.RemoveAllListeners();
            aiCountSlider.onValueChanged.RemoveAllListeners();
        }

        public void NextTrack() { trackIndex = (trackIndex + 1) % tracks.Length; RefreshTrack(); }
        public void PreviousTrack() { trackIndex = (trackIndex - 1 + tracks.Length) % tracks.Length; RefreshTrack(); }

        private void RefreshTrack()
        {
            trackNameText.text = tracks[trackIndex].displayName;
            if (thumbnailImage != null) thumbnailImage.sprite = tracks[trackIndex].thumbnail;
            GameSettings.SelectedTrackScene = tracks[trackIndex].sceneName;
        }

        private void Apply()
        {
            GameSettings.Mode = circuitToggle.isOn
                ? GameSettings.RaceMode.Circuit
                : GameSettings.RaceMode.TimeTrial;
            GameSettings.Laps = Mathf.RoundToInt(lapsSlider.value);
            GameSettings.AICount = Mathf.RoundToInt(aiCountSlider.value);

            lapsValueText.text = GameSettings.Laps.ToString();
            aiCountValueText.text = GameSettings.AICount.ToString();
            if (circuitOnlyGroup != null) circuitOnlyGroup.SetActive(circuitToggle.isOn);
        }
    }
}
