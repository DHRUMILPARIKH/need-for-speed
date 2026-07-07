import { clamp, clamp01, lerp } from './utils';

/**
 * Waypoint(spline)-following driver. Produces the same input object a human
 * does, so AI cars run identical Vehicle physics — rubber-banding shapes the
 * race, it can never drive better than the physics allow.
 */
export class AIDriver {
  constructor(vehicle, track, difficulty, race) {
    this.vehicle = vehicle;
    this.track = track;
    this.race = race;

    // difficulty: 0 easy, 1 normal, 2 hard
    const tiers = [
      { throttleCap: 0.72, cornerCommit: 0.85, wobble: 0.08 },
      { throttleCap: 0.88, cornerCommit: 1.0, wobble: 0.03 },
      { throttleCap: 1.0, cornerCommit: 1.12, wobble: 0 },
    ];
    Object.assign(this, tiers[clamp(difficulty, 0, 2)]);

    this.rubberBandStrength = 0.15; // ±15% throttle
    this.stuckTimer = 0;
    this.reverseTimer = 0;
    this.phase = Math.random() * 10; // desync wobble between bots
  }

  compute(dt) {
    const v = this.vehicle;
    const track = this.track;
    const n = track.sampleCount;
    const metersPerSample = track.length / n;
    const speed = v.vel.length();

    // ── steering: chase a point ahead on the spline ──
    const lookMeters = 11 + speed * 0.5;
    const lookIdx = (v.trackHint + Math.round(lookMeters / metersPerSample)) % n;
    const target = track.centers[lookIdx];
    const fwd = v.forward();
    const dx = target.x - v.pos.x, dz = target.z - v.pos.z;
    const angle = Math.atan2(dz * fwd.x - dx * fwd.z, dx * fwd.x + dz * fwd.z) * -1;
    let steer = clamp(angle / 0.55, -1, 1)
              + Math.sin(performance.now() / 700 + this.phase) * this.wobble;

    // ── speed: brake for the bend ahead ──
    const bendSpan = Math.round((25 + speed * 0.8) / metersPerSample);
    const bend = track.bendAhead(v.trackHint, bendSpan);
    const desiredKmh = lerp(v.spec.topSpeedKmh, 52, clamp01(bend / 1.5)) * this.cornerCommit;

    // ── rubber-band: compare lap-progress to the player ──
    const deficit = this.race ? this.race.playerProgress - this.race.progressOf(v) : 0;
    const band = 1 + clamp(deficit / 0.6, -1, 1) * this.rubberBandStrength;

    let throttle = 0, brake = 0;
    if (v.speedKmh < desiredKmh) throttle = this.throttleCap * band;
    else if (v.speedKmh > desiredKmh * 1.12) brake = 0.8;

    const nitro = deficit > 0.12 && Math.abs(angle) < 0.12 && v.nitro > 0.4;

    // ── stuck recovery: reverse out of walls ──
    if (this.reverseTimer > 0) {
      this.reverseTimer -= dt;
      return { steer: -steer, throttle: 0, brake: 1, handbrake: false, nitro: false };
    }
    if (v.controlEnabled && speed < 1.5 && throttle > 0.1) {
      this.stuckTimer += dt;
      if (this.stuckTimer > 2) { this.reverseTimer = 1.3; this.stuckTimer = 0; }
    } else this.stuckTimer = 0;

    return { steer, throttle, brake, handbrake: false, nitro };
  }
}
