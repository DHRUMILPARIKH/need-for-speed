using UnityEngine;
using UnityEngine.UI;
using ApexRush.Core;

namespace ApexRush.Menu
{
    /// <summary>
    /// Settings panel: master volume, graphics quality, camera shake toggle,
    /// and AI difficulty. Everything persists via PlayerPrefs through GameSettings.
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        [SerializeField] private Slider volumeSlider;          // 0..1
        [SerializeField] private Dropdown qualityDropdown;     // auto-filled
        [SerializeField] private Toggle cameraShakeToggle;
        [SerializeField] private Dropdown difficultyDropdown;  // Easy / Normal / Hard

        private void OnEnable()
        {
            // Populate quality options from the project's quality levels.
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(
                QualitySettings.names));

            difficultyDropdown.ClearOptions();
            difficultyDropdown.AddOptions(new System.Collections.Generic.List<string>
                { "Easy", "Normal", "Hard" });

            // Reflect saved values.
            volumeSlider.value = GameSettings.MasterVolume;
            qualityDropdown.value = QualitySettings.GetQualityLevel();
            cameraShakeToggle.isOn = GameSettings.CameraShake;
            difficultyDropdown.value = GameSettings.AIDifficulty;

            volumeSlider.onValueChanged.AddListener(v => GameSettings.MasterVolume = v);
            qualityDropdown.onValueChanged.AddListener(q => QualitySettings.SetQualityLevel(q, true));
            cameraShakeToggle.onValueChanged.AddListener(v => GameSettings.CameraShake = v);
            difficultyDropdown.onValueChanged.AddListener(d => GameSettings.AIDifficulty = d);
        }

        private void OnDisable()
        {
            volumeSlider.onValueChanged.RemoveAllListeners();
            qualityDropdown.onValueChanged.RemoveAllListeners();
            cameraShakeToggle.onValueChanged.RemoveAllListeners();
            difficultyDropdown.onValueChanged.RemoveAllListeners();
            PlayerPrefs.Save();
        }
    }
}
