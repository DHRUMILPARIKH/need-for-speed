using UnityEngine;
using UnityEngine.SceneManagement;
using ApexRush.Core;

namespace ApexRush.Menu
{
    /// <summary>
    /// Panel switcher for the MainMenu scene. One Canvas, four panels
    /// (Main, CarSelect, TrackSelect, Settings); this shows exactly one at a time.
    /// The flow is Main → Car Select → Track Select → race scene loads.
    /// Wire the buttons to the public methods in the Inspector.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject carSelectPanel;
        [SerializeField] private GameObject trackSelectPanel;
        [SerializeField] private GameObject settingsPanel;

        private void Start()
        {
            GameSettings.ApplyOnBoot();
            ShowMain();
        }

        public void ShowMain() => Show(mainPanel);
        public void ShowCarSelect() => Show(carSelectPanel);
        public void ShowTrackSelect() => Show(trackSelectPanel);
        public void ShowSettings() => Show(settingsPanel);

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Track Select's final button calls this after mode/track are set.</summary>
        public void StartRace() => SceneManager.LoadScene(GameSettings.SelectedTrackScene);

        private void Show(GameObject panel)
        {
            mainPanel.SetActive(panel == mainPanel);
            carSelectPanel.SetActive(panel == carSelectPanel);
            trackSelectPanel.SetActive(panel == trackSelectPanel);
            settingsPanel.SetActive(panel == settingsPanel);
        }
    }
}
