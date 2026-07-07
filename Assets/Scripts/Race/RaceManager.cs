using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using ApexRush.Core;
using ApexRush.Vehicle;
using ApexRush.AI;
using ApexRush.CameraSystem;

namespace ApexRush.Race
{
    /// <summary>
    /// Owns one race in a track scene: spawns cars from GameSettings (or inspector
    /// defaults when you press Play directly in the scene), runs the countdown,
    /// tracks live positions, and decides win/lose.
    ///
    /// Modes:
    ///   Circuit    — player + N AI, first across the line after TotalLaps wins.
    ///   Time Trial — player alone, chasing best lap over TotalLaps.
    ///
    /// UI never polls scattered objects — it subscribes to the events below.
    /// </summary>
    public class RaceManager : MonoBehaviour
    {
        public enum RaceState { Waiting, Countdown, Racing, Finished }

        public static RaceManager Instance { get; private set; }

        [Header("Scene references")]
        [Tooltip("Parent whose children are the Checkpoint gates, in driving order. Child 0 = start/finish.")]
        [SerializeField] private Transform checkpointParent;
        [Tooltip("Grid slots. Element 0 = player. Place them just BEHIND the start line, facing +Z along the track.")]
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private WaypointCircuit aiCircuit;
        [SerializeField] private ChaseCamera chaseCamera;

        [Header("Cars")]
        [Tooltip("Selectable car prefabs — same list and order as the Car Select menu.")]
        [SerializeField] private VehicleController[] carPrefabs;

        [Header("Defaults (overridden by menu via GameSettings)")]
        [SerializeField] private GameSettings.RaceMode defaultMode = GameSettings.RaceMode.Circuit;
        [SerializeField] private int defaultLaps = 3;
        [SerializeField] private int defaultAICount = 3;

        [Header("Countdown")]
        [SerializeField] private int countdownSeconds = 3;

        // ── Public state ──
        public RaceState State { get; private set; } = RaceState.Waiting;
        public GameSettings.RaceMode Mode { get; private set; }
        public int TotalLaps { get; private set; }
        public int CheckpointCount => checkpoints.Count;
        public RaceParticipant Player { get; private set; }
        public IReadOnlyList<RaceParticipant> Participants => participants;

        // ── Events (HUD subscribes) ──
        public event Action<int> OnCountdownTick;                       // 3, 2, 1, then 0 = GO
        public event Action OnRaceStarted;
        public event Action<RaceParticipant, float> OnLapCompleted;     // (who, lap time)
        public event Action<RaceParticipant> OnParticipantFinished;
        public event Action<RaceParticipant> OnRaceOver;                // fired when the PLAYER finishes

        private readonly List<Checkpoint> checkpoints = new List<Checkpoint>();
        private readonly List<RaceParticipant> participants = new List<RaceParticipant>();
        private readonly List<VehicleController> vehicles = new List<VehicleController>();

        private void Awake()
        {
            Instance = this;
            GameSettings.ApplyOnBoot();

            Mode = Application.isEditor && GameSettings.SelectedTrackScene != SceneManager.GetActiveScene().name
                ? defaultMode           // pressed Play directly in the track scene
                : GameSettings.Mode;
            TotalLaps = Mode == GameSettings.RaceMode.TimeTrial || GameSettings.Laps > 0
                ? Mathf.Max(1, GameSettings.Laps)
                : defaultLaps;

            CollectCheckpoints();
        }

        private void Start()
        {
            SpawnCars();
            StartCoroutine(CountdownRoutine());
        }

        private void CollectCheckpoints()
        {
            checkpoints.Clear();
            for (int i = 0; i < checkpointParent.childCount; i++)
            {
                var cp = checkpointParent.GetChild(i).GetComponent<Checkpoint>();
                if (cp == null) continue;
                cp.Index = checkpoints.Count;
                checkpoints.Add(cp);
            }
            if (checkpoints.Count < 3)
                Debug.LogWarning("RaceManager: fewer than 3 checkpoints — lap detection will be unreliable.");
        }

        public Vector3 GetCheckpointPosition(int index) => checkpoints[index].transform.position;

        // ─────────────────────────────────────────────────────────── spawning ──

        private void SpawnCars()
        {
            int aiCount = Mode == GameSettings.RaceMode.TimeTrial
                ? 0
                : Mathf.Min(GameSettings.AICount > 0 ? GameSettings.AICount : defaultAICount,
                            spawnPoints.Length - 1);

            // Player — the car chosen in the menu.
            int carIdx = Mathf.Clamp(GameSettings.SelectedCarIndex, 0, carPrefabs.Length - 1);
            Player = SpawnOne(carPrefabs[carIdx], spawnPoints[0], isPlayer: true, "You");

            // AI — cycle through the roster, skipping nothing fancy.
            for (int i = 0; i < aiCount; i++)
            {
                var prefab = carPrefabs[(carIdx + 1 + i) % carPrefabs.Length];
                SpawnOne(prefab, spawnPoints[i + 1], isPlayer: false, $"Rival {i + 1}");
            }

            if (chaseCamera != null)
                chaseCamera.SetTarget(Player.GetComponent<VehicleController>());
        }

        private RaceParticipant SpawnOne(VehicleController prefab, Transform slot, bool isPlayer, string name)
        {
            VehicleController car = Instantiate(prefab, slot.position, slot.rotation);
            car.SetControlEnabled(false); // frozen until GO
            vehicles.Add(car);

            var participant = car.gameObject.AddComponent<RaceParticipant>();
            participant.Init(this);
            participant.IsPlayer = isPlayer;
            participant.DisplayName = name;
            participants.Add(participant);

            if (isPlayer)
            {
                if (car.GetComponent<VehicleInput>() == null)
                    car.gameObject.AddComponent<VehicleInput>();
            }
            else
            {
                var input = car.GetComponent<VehicleInput>();
                if (input != null) Destroy(input); // bots must not read the keyboard
                var ai = car.gameObject.AddComponent<AIController>();
                ai.Configure(aiCircuit, GameSettings.AIDifficulty, this, participant);
            }
            return participant;
        }

        // ─────────────────────────────────────────────────────────── countdown ──

        private IEnumerator CountdownRoutine()
        {
            State = RaceState.Countdown;
            yield return new WaitForSeconds(0.5f); // let physics settle on the grid

            for (int i = countdownSeconds; i > 0; i--)
            {
                OnCountdownTick?.Invoke(i);
                yield return new WaitForSeconds(1f);
            }

            OnCountdownTick?.Invoke(0); // "GO!"
            State = RaceState.Racing;
            foreach (var v in vehicles) v.SetControlEnabled(true);
            foreach (var p in participants) p.OnRaceStarted();
            OnRaceStarted?.Invoke();
        }

        // ─────────────────────────────────────────────────────────── positions ──

        private void Update()
        {
            if (State != RaceState.Racing) return;

            // Finished cars rank by finish time, still-racing cars by progress.
            var ordered = participants
                .OrderByDescending(p => p.Finished)
                .ThenBy(p => p.Finished ? p.FinishTime : 0f)
                .ThenByDescending(p => p.Progress)
                .ToList();
            for (int i = 0; i < ordered.Count; i++)
                ordered[i].Position = i + 1;
        }

        internal void NotifyLapCompleted(RaceParticipant p, float lapTime)
        {
            OnLapCompleted?.Invoke(p, lapTime);

            if (!p.Finished) return;
            OnParticipantFinished?.Invoke(p);

            if (p.IsPlayer)
            {
                State = RaceState.Finished;
                OnRaceOver?.Invoke(p);
                // Hand the player's car to the AI so it cruises during results.
                var car = p.GetComponent<VehicleController>();
                var input = p.GetComponent<VehicleInput>();
                if (input != null) Destroy(input);
                var ai = p.gameObject.AddComponent<AIController>();
                ai.Configure(aiCircuit, 1, this, p);
            }
        }

        // ─────────────────────────────────────────────────────────── flow ──

        public void RestartRace() =>
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        public void QuitToMenu() =>
            SceneManager.LoadScene("MainMenu");
    }
}
