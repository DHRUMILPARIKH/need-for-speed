using UnityEngine;
using UnityEngine.UI;
using ApexRush.Core;
using ApexRush.Race;
using ApexRush.Vehicle;

namespace ApexRush.UI
{
    /// <summary>
    /// In-race HUD: speedometer, lap timer, lap/position counters, nitro gauge,
    /// countdown, and the results panel. Purely event/poll driven off RaceManager
    /// and the player's VehicleController — it never touches game logic.
    ///
    /// Uses legacy UnityEngine.UI (Text/Image) so it compiles with zero packages;
    /// swap Text for TMP_Text if you prefer TextMeshPro.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Driving readouts")]
        [SerializeField] private Text speedText;          // "184"
        [SerializeField] private Text gearText;           // "3" / "R"
        [SerializeField] private Image nitroFill;         // Image type = Filled, Horizontal

        [Header("Race readouts")]
        [SerializeField] private Text lapText;            // "LAP 2/3"
        [SerializeField] private Text positionText;       // "3/4"  (hidden in Time Trial)
        [SerializeField] private Text currentLapTimeText; // "0:42.317"
        [SerializeField] private Text bestLapTimeText;    // "BEST 0:39.850"

        [Header("Countdown")]
        [SerializeField] private Text countdownText;      // big center text: 3..1, GO!

        [Header("Results")]
        [SerializeField] private GameObject resultsPanel;
        [SerializeField] private Text resultsTitleText;   // "FINISHED 1st" / "TIME TRIAL COMPLETE"
        [SerializeField] private Text resultsDetailText;  // lap times breakdown

        private RaceManager race;
        private VehicleController playerCar;
        private RaceParticipant player;

        private void Start()
        {
            race = RaceManager.Instance;
            race.OnCountdownTick += HandleCountdown;
            race.OnRaceOver += HandleRaceOver;

            player = race.Player;
            playerCar = player.GetComponent<VehicleController>();

            resultsPanel.SetActive(false);
            countdownText.gameObject.SetActive(false);
            if (positionText != null)
                positionText.transform.parent.gameObject.SetActive(
                    race.Mode == GameSettings.RaceMode.Circuit);
        }

        private void OnDestroy()
        {
            if (race == null) return;
            race.OnCountdownTick -= HandleCountdown;
            race.OnRaceOver -= HandleRaceOver;
        }

        private void Update()
        {
            if (playerCar == null) return;

            // Speed + gear + nitro every frame — these need to feel live.
            speedText.text = Mathf.RoundToInt(playerCar.SpeedKmh).ToString();
            if (gearText != null)
                gearText.text = playerCar.CurrentGear == 0 ? "R" : playerCar.CurrentGear.ToString();
            nitroFill.fillAmount = playerCar.NitroAmount;

            if (race.State == RaceManager.RaceState.Racing && !player.Finished)
            {
                lapText.text = $"LAP {player.CurrentLap}/{race.TotalLaps}";
                if (positionText != null)
                    positionText.text = $"{Ordinal(player.Position)} / {race.Participants.Count}";
                currentLapTimeText.text = FormatTime(player.CurrentLapTime);
                bestLapTimeText.text = float.IsInfinity(player.BestLapTime)
                    ? "BEST --:--.---"
                    : "BEST " + FormatTime(player.BestLapTime);
            }
        }

        // ─────────────────────────────────────────────────────────── events ──

        private void HandleCountdown(int value)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = value > 0 ? value.ToString() : "GO!";
            if (value == 0) Invoke(nameof(HideCountdown), 1f);
        }

        private void HideCountdown() => countdownText.gameObject.SetActive(false);

        private void HandleRaceOver(RaceParticipant p)
        {
            resultsPanel.SetActive(true);

            resultsTitleText.text = race.Mode == GameSettings.RaceMode.TimeTrial
                ? "TIME TRIAL COMPLETE"
                : p.Position == 1 ? "YOU WIN!" : $"FINISHED {Ordinal(p.Position)}";

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < p.LapTimes.Count; i++)
            {
                sb.Append($"Lap {i + 1}   {FormatTime(p.LapTimes[i])}");
                if (Mathf.Approximately(p.LapTimes[i], p.BestLapTime)) sb.Append("  ◄ best");
                sb.AppendLine();
            }
            sb.AppendLine($"\nTotal   {FormatTime(p.FinishTime)}");
            resultsDetailText.text = sb.ToString();
        }

        // Wire these to the results panel buttons in the Inspector:
        public void OnRestartPressed() => race.RestartRace();
        public void OnMenuPressed() => race.QuitToMenu();

        // ─────────────────────────────────────────────────────────── helpers ──

        public static string FormatTime(float seconds)
        {
            int m = (int)(seconds / 60f);
            float s = seconds - m * 60;
            return $"{m}:{s:00.000}";
        }

        private static string Ordinal(int n)
        {
            switch (n)
            {
                case 1: return "1st";
                case 2: return "2nd";
                case 3: return "3rd";
                default: return n + "th";
            }
        }
    }
}
