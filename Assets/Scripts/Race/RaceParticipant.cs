using System.Collections.Generic;
using UnityEngine;

namespace ApexRush.Race
{
    /// <summary>
    /// Per-car race bookkeeping: which checkpoint is next, lap count, lap times,
    /// and a continuous progress value used for live position sorting.
    /// Lives on every car (player and AI). RaceManager adds and configures it at
    /// spawn time — you don't place this in the Editor.
    /// </summary>
    public class RaceParticipant : MonoBehaviour
    {
        public string DisplayName { get; set; } = "Racer";
        public bool IsPlayer { get; set; }

        public int CurrentLap { get; private set; } = 1;      // 1-based
        public int NextCheckpoint { get; private set; }
        public int Position { get; internal set; } = 1;       // set by RaceManager each frame
        public bool Finished { get; private set; }
        public float FinishTime { get; private set; }

        public float CurrentLapTime => Finished ? 0f : Time.time - lapStartTime;
        public float TotalTime => Finished ? FinishTime : Time.time - raceStartTime;
        public float BestLapTime { get; private set; } = float.PositiveInfinity;
        public IReadOnlyList<float> LapTimes => lapTimes;

        /// <summary>
        /// Monotonic race progress: whole checkpoints passed plus the fraction of the
        /// way to the next gate. Comparing this across cars gives live positions.
        /// </summary>
        public float Progress { get; private set; }

        private readonly List<float> lapTimes = new List<float>();
        private RaceManager race;
        private float lapStartTime;
        private float raceStartTime;
        private int totalCheckpointsPassed;

        internal void Init(RaceManager manager)
        {
            race = manager;
        }

        internal void OnRaceStarted()
        {
            raceStartTime = lapStartTime = Time.time;
        }

        public void OnCheckpointPassed(Checkpoint cp)
        {
            if (Finished || race == null || race.State != RaceManager.RaceState.Racing) return;
            if (cp.Index != NextCheckpoint) return; // wrong gate (backwards / cut) — ignored

            totalCheckpointsPassed++;
            NextCheckpoint = (NextCheckpoint + 1) % race.CheckpointCount;

            // Crossing gate 0 again = lap complete.
            if (NextCheckpoint == 1 && totalCheckpointsPassed > 1)
            {
                float lapTime = Time.time - lapStartTime;
                lapTimes.Add(lapTime);
                BestLapTime = Mathf.Min(BestLapTime, lapTime);
                lapStartTime = Time.time;

                if (CurrentLap >= race.TotalLaps)
                {
                    Finished = true;
                    FinishTime = Time.time - raceStartTime;
                }
                else
                {
                    CurrentLap++;
                }
                race.NotifyLapCompleted(this, lapTime);
            }
        }

        private void Update()
        {
            if (race == null || race.CheckpointCount == 0) return;

            // Fraction of the segment to the next checkpoint, from distance remaining.
            Vector3 next = race.GetCheckpointPosition(NextCheckpoint);
            Vector3 prev = race.GetCheckpointPosition(
                (NextCheckpoint - 1 + race.CheckpointCount) % race.CheckpointCount);
            float segment = Vector3.Distance(prev, next);
            float fraction = segment > 1f
                ? 1f - Mathf.Clamp01(Vector3.Distance(transform.position, next) / segment)
                : 0f;

            Progress = totalCheckpointsPassed + fraction;
        }
    }
}
