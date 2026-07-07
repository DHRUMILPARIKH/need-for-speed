using UnityEngine;

namespace ApexRush.Core
{
    /// <summary>
    /// Cross-scene game state: what the menus selected and what the options are.
    /// Static so it survives scene loads without a DontDestroyOnLoad object.
    /// Persisted to PlayerPrefs where it makes sense (settings, not selections).
    /// </summary>
    public static class GameSettings
    {
        public enum RaceMode { Circuit, TimeTrial }

        // ── Selections made in the menu (defaults let you press Play in a track scene directly) ──
        public static RaceMode Mode = RaceMode.Circuit;
        public static int SelectedCarIndex = 0;
        public static string SelectedTrackScene = "Track01";
        public static int AICount = 3;
        [System.NonSerialized] public static int Laps = 3;

        // ── Options (persisted) ──
        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat("opt_volume", 1f);
            set { PlayerPrefs.SetFloat("opt_volume", value); AudioListener.volume = value; }
        }

        public static bool CameraShake
        {
            get => PlayerPrefs.GetInt("opt_shake", 1) == 1;
            set => PlayerPrefs.SetInt("opt_shake", value ? 1 : 0);
        }

        /// <summary>0 = easy, 1 = normal, 2 = hard. Maps to AIController difficulty.</summary>
        public static int AIDifficulty
        {
            get => PlayerPrefs.GetInt("opt_difficulty", 1);
            set => PlayerPrefs.SetInt("opt_difficulty", Mathf.Clamp(value, 0, 2));
        }

        public static void ApplyOnBoot()
        {
            AudioListener.volume = MasterVolume;
        }
    }
}
