/**
 * Race state: countdown, continuous lap-progress tracking, lap times, live
 * positions, and finish detection. Progress is measured directly from each
 * car's position along the track spline (trackT), accumulated across laps —
 * no checkpoint gates needed, and reversing/corner-cutting can't skip ahead
 * because progress is continuous and monotonic per meter driven.
 *
 * Modes: 'circuit' (player + AI, position decides win) and 'timetrial' (solo,
 * chasing best lap).
 */
export class Race {
  constructor({ mode, totalLaps, entries, callbacks }) {
    this.mode = mode;
    this.totalLaps = totalLaps;
    this.cb = callbacks; // { onCountdown(n), onGo(), onLap(entry, time), onFinish(entry) }
    this.state = 'countdown';
    this.countdown = 3.999; // shows 3,2,1 then GO
    this.time = 0;

    // entries: [{ vehicle, name, isPlayer }]
    this.entries = entries.map((e) => ({
      ...e,
      progress: 0,          // laps as float, e.g. 1.43 = lap 2, 43% around
      lastT: e.vehicle.trackT,
      lap: 1,
      lapStart: 0,
      lapTimes: [],
      best: Infinity,
      finished: false,
      finishTime: 0,
      position: 1,
    }));
    this.player = this.entries.find((e) => e.isPlayer);
  }

  get playerProgress() { return this.player.progress; }

  progressOf(vehicle) {
    const e = this.entries.find((x) => x.vehicle === vehicle);
    return e ? e.progress : 0;
  }

  update(dt) {
    this.time += dt;

    if (this.state === 'countdown') {
      const prev = Math.ceil(this.countdown);
      this.countdown -= dt;
      const now = Math.ceil(this.countdown);
      if (now !== prev && now > 0) this.cb.onCountdown(now);
      if (this.countdown <= 0) {
        this.state = 'racing';
        this.raceStart = this.time;
        for (const e of this.entries) {
          e.vehicle.controlEnabled = true;
          e.lapStart = this.time;
          e.lastT = e.vehicle.trackT;
        }
        this.cb.onCountdown(0); // "GO!"
        this.cb.onGo();
      }
      return;
    }

    if (this.state !== 'racing' && this.state !== 'finished') return;

    for (const e of this.entries) {
      if (e.finished) continue;

      // Accumulate spline progress, handling the 1→0 wrap at the start line.
      const t = e.vehicle.trackT;
      let delta = t - e.lastT;
      if (delta < -0.5) delta += 1;
      if (delta > 0.5) delta -= 1;
      e.progress += delta;
      e.lastT = t;

      // Lap completes when accumulated progress crosses the next whole number.
      if (e.progress >= e.lap) {
        const lapTime = this.time - e.lapStart;
        e.lapTimes.push(lapTime);
        e.best = Math.min(e.best, lapTime);
        e.lapStart = this.time;
        this.cb.onLap(e, lapTime);

        if (e.lap >= this.totalLaps) {
          e.finished = true;
          e.finishTime = this.time - this.raceStart;
          this.cb.onFinish(e);
          if (e.isPlayer) this.state = 'finished';
        } else {
          e.lap++;
        }
      }
    }

    // Live positions: finished cars rank by finish time, others by progress.
    const ordered = [...this.entries].sort((a, b) => {
      if (a.finished !== b.finished) return a.finished ? -1 : 1;
      if (a.finished) return a.finishTime - b.finishTime;
      return b.progress - a.progress;
    });
    ordered.forEach((e, i) => { e.position = i + 1; });
  }

  /** Per-frame HUD payload for the player. */
  playerHud() {
    const p = this.player;
    return {
      lap: Math.min(p.lap, this.totalLaps),
      totalLaps: this.totalLaps,
      position: p.position,
      carCount: this.entries.length,
      currentLapTime: this.state === 'racing' && !p.finished ? this.time - p.lapStart : 0,
      bestLap: p.best,
      totalTime: p.finished ? p.finishTime : this.time - (this.raceStart ?? this.time),
    };
  }
}
