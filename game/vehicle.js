import * as THREE from 'three';
import { clamp, clamp01, lerp, lerpAngle, moveTowards, signedAngleY, damp } from './utils';

const UP = new THREE.Vector3(0, 1, 0);

/**
 * Selectable cars. Stats feed straight into the physics — the menu order must
 * match everywhere (it does automatically: everything imports this array).
 */
export const CAR_MODELS = [
  {
    // Homage to the legendary silver-and-blue race coupe. If you want the real
    // name in a private build, change `name` here — but keep original names in
    // anything you publish or share: "BMW" and "M3 GTR" are trademarks.
    id: 'werks', name: 'Werks GTR', blurb: 'The legend. Race-bred coupe',
    stats: 'Speed ★★★★ · Grip ★★★★ · Nitro ★★★★',
    color: 0xd3d7dd, stripe: 0x1b52d8, wing: true,
    topSpeedKmh: 228, accel: 16, steerRate: 2.5, nitroAccel: 14,
  },
  {
    id: 'falcon', name: 'Falcon GT', blurb: 'Balanced all-rounder',
    stats: 'Speed ★★★☆ · Grip ★★★☆ · Nitro ★★★☆',
    color: 0x2f6fe0, topSpeedKmh: 215, accel: 15, steerRate: 2.4, nitroAccel: 13,
  },
  {
    id: 'bolt', name: 'Bolt RS', blurb: 'Agile corner-carver',
    stats: 'Speed ★★☆☆ · Grip ★★★★ · Nitro ★★★☆',
    color: 0xf08a1d, topSpeedKmh: 195, accel: 17, steerRate: 2.8, nitroAccel: 12,
  },
  {
    id: 'titan', name: 'Titan V8', blurb: 'Brute-force muscle',
    stats: 'Speed ★★★★ · Grip ★★☆☆ · Nitro ★★★★',
    color: 0xc23038, topSpeedKmh: 235, accel: 13, steerRate: 2.1, nitroAccel: 15,
  },
];

/**
 * Arcade car physics — the same three-layer design as the Unity build, on a
 * lightweight velocity-vector model (no physics engine):
 *   GRIP ASSIST — rotates the velocity vector toward the car's heading each
 *                 frame; the car carves where it points.
 *   DRIFT STATE — handbrake (or big slip angle) drops the assist and boosts yaw
 *                 rate, so the nose rotates while momentum carries — a slide
 *                 you steer with the stick. Drifting charges nitro.
 *   STABILITY   — speed-sensitive steering; walls are solved analytically from
 *                 the track spline (clamp lateral offset), so cars scrape and
 *                 slow instead of bouncing.
 * Cosmetic body roll/pitch fakes weight transfer.
 */
export class Vehicle {
  constructor(scene, spec, track) {
    this.spec = spec;
    this.track = track;

    // ── state ──
    this.pos = new THREE.Vector3();
    this.vel = new THREE.Vector3();     // planar velocity, m/s
    this.heading = 0;                    // radians; forward = (sin h, 0, cos h)
    this.steer = 0;                      // smoothed steering -1..1
    this.controlEnabled = false;
    this.trackHint = 0;                  // last known spline sample index

    // ── telemetry (read by camera/audio/FX/HUD/AI/race) ──
    this.speedKmh = 0;
    this.normalizedSpeed = 0;
    this.slipAngle = 0;                  // degrees
    this.drifting = false;
    this.nitro = 1;
    this.nitroActive = false;
    this.gear = 1;
    this.rpm = 0.15;                     // sawtooth 0..1 for audio
    this.throttle = 0;
    this.wallHit = 0;                    // impact strength this frame (camera shake)
    this.trackT = 0;                     // 0..1 position along the lap spline

    this.buildMesh(scene);
  }

  buildMesh(scene) {
    const g = new THREE.Group();
    const paint = new THREE.MeshStandardMaterial({
      color: this.spec.color, metalness: 0.4, roughness: 0.35,
    });
    const dark = new THREE.MeshStandardMaterial({ color: 0x16181c, roughness: 0.6 });

    // Body pieces are parented to a "chassis" group so roll/pitch tilt is easy.
    this.chassis = new THREE.Group();
    const body = new THREE.Mesh(new THREE.BoxGeometry(1.9, 0.55, 4.2), paint);
    body.position.y = 0.55;
    const cabin = new THREE.Mesh(new THREE.BoxGeometry(1.6, 0.45, 1.9), dark);
    cabin.position.set(0, 1.0, -0.25);
    const nose = new THREE.Mesh(new THREE.BoxGeometry(1.7, 0.3, 0.9), paint);
    nose.position.set(0, 0.42, 2.2);
    this.chassis.add(body, cabin, nose);

    // Optional livery: twin racing stripes over hood, roof, and tail.
    if (this.spec.stripe) {
      const stripeMat = new THREE.MeshStandardMaterial({
        color: this.spec.stripe, metalness: 0.3, roughness: 0.4,
      });
      for (const side of [-1, 1]) {
        const hoodStripe = new THREE.Mesh(new THREE.BoxGeometry(0.26, 0.03, 4.24), stripeMat);
        hoodStripe.position.set(side * 0.3, 0.84, 0);
        const roofStripe = new THREE.Mesh(new THREE.BoxGeometry(0.24, 0.03, 1.92), stripeMat);
        roofStripe.position.set(side * 0.27, 1.24, -0.25);
        this.chassis.add(hoodStripe, roofStripe);
      }
    }

    // Optional GT wing on twin struts.
    if (this.spec.wing) {
      const strutGeo = new THREE.BoxGeometry(0.08, 0.38, 0.1);
      for (const side of [-1, 1]) {
        const strut = new THREE.Mesh(strutGeo, dark);
        strut.position.set(side * 0.62, 1.0, -1.9);
        this.chassis.add(strut);
      }
      const plank = new THREE.Mesh(new THREE.BoxGeometry(1.95, 0.06, 0.5), paint);
      plank.position.set(0, 1.2, -1.95);
      plank.rotation.x = -0.12; // slight angle of attack
      const endplateGeo = new THREE.BoxGeometry(0.05, 0.22, 0.5);
      for (const side of [-1, 1]) {
        const plate = new THREE.Mesh(endplateGeo, dark);
        plate.position.set(side * 0.97, 1.24, -1.95);
        this.chassis.add(plate);
      }
      this.chassis.add(plank);
    }

    // Rear glow strip lights up with nitro.
    this.tailGlow = new THREE.Mesh(
      new THREE.BoxGeometry(1.4, 0.15, 0.06),
      new THREE.MeshBasicMaterial({ color: 0x330000 }));
    this.tailGlow.position.set(0, 0.6, -2.12);
    this.chassis.add(this.tailGlow);

    // Wheels: cylinders lying on their side. Fronts get a steer pivot.
    const wheelGeo = new THREE.CylinderGeometry(0.36, 0.36, 0.3, 14);
    wheelGeo.rotateZ(Math.PI / 2);
    this.wheels = [];
    this.frontPivots = [];
    for (const [x, z, front] of [[-0.95, 1.45, 1], [0.95, 1.45, 1], [-0.95, -1.45, 0], [0.95, -1.45, 0]]) {
      const wheel = new THREE.Mesh(wheelGeo, dark);
      const pivot = new THREE.Group();
      pivot.position.set(x, 0.36, z);
      pivot.add(wheel);
      g.add(pivot);
      this.wheels.push(wheel);
      if (front) this.frontPivots.push(pivot);
    }

    g.add(this.chassis);
    this.mesh = g;
    scene.add(g);
  }

  place(position, heading, trackIndex) {
    this.pos.copy(position);
    this.heading = heading;
    this.vel.set(0, 0, 0);
    this.trackHint = trackIndex;
    this.syncMesh();
  }

  forward() {
    return new THREE.Vector3(Math.sin(this.heading), 0, Math.cos(this.heading));
  }

  update(dt, rawInput) {
    const input = this.controlEnabled
      ? rawInput
      : { steer: 0, throttle: 0, brake: 0, handbrake: false, nitro: false };
    const spec = this.spec;
    const fwd = this.forward();
    const speed = this.vel.length();
    const signedSpeed = this.vel.dot(fwd);
    const topSpeed = spec.topSpeedKmh / 3.6;

    this.speedKmh = speed * 3.6;
    this.normalizedSpeed = clamp01(speed / topSpeed);
    this.throttle = input.throttle;
    this.wallHit = 0;

    // ── slip angle: velocity direction vs nose direction ──
    this.slipAngle = speed > 2
      ? (signedAngleY(fwd.x, fwd.z, this.vel.x / speed, this.vel.z / speed) * 180) / Math.PI
      : 0;

    // ── drift state machine ──
    const absSlip = Math.abs(this.slipAngle);
    const fastEnough = this.speedKmh > 40;
    if (!this.drifting) {
      const handbrakeEntry = input.handbrake && Math.abs(input.steer) > 0.1;
      this.drifting = fastEnough && (handbrakeEntry || absSlip > 14);
    } else if ((absSlip < 5 && !input.handbrake) || !fastEnough) {
      this.drifting = false;
    }

    // ── steering: smoothed input, speed-sensitive authority ──
    this.steer = moveTowards(this.steer, clamp(input.steer, -1, 1), 4.5 * dt);
    const speedFactor = lerp(1, 0.42, this.normalizedSpeed);
    const authority = clamp(speed / 6, 0, 1) * Math.sign(signedSpeed || 1);
    let yawRate = this.steer * spec.steerRate * speedFactor * authority;
    if (this.drifting) yawRate *= 1.65; // yaw assist: the nose rotates into the slide
    this.heading += yawRate * dt;

    // ── longitudinal forces ──
    let accel = 0;
    if (input.throttle > 0.01) {
      if (signedSpeed < -0.5) accel = -18 * Math.sign(signedSpeed); // braking out of reverse
      else accel = spec.accel * input.throttle * clamp01(1.05 - this.normalizedSpeed);
    } else if (input.brake > 0.01) {
      if (signedSpeed > 0.5) accel = -20 * input.brake;             // brake
      else if (speed < 12) accel = -spec.accel * 0.5 * input.brake; // reverse
    }
    // Handbrake: strong drag but keep 30% throttle so held-drifts stay alive.
    if (input.handbrake) accel = accel * 0.3 - Math.sign(signedSpeed) * 4.5;

    // ── nitro ──
    this.nitroActive = input.nitro && this.nitro > 0.01 && signedSpeed > 1;
    if (this.nitroActive) {
      accel += spec.nitroAccel;
      this.nitro = clamp01(this.nitro - dt / 4);
    } else {
      this.nitro = clamp01(this.nitro + 0.05 * dt);
    }
    if (this.drifting) this.nitro = clamp01(this.nitro + 0.15 * dt); // drift charges boost

    this.vel.addScaledVector(fwd, accel * dt);

    // ── drag + top speed cap (nitro allowed 12% over) ──
    this.vel.multiplyScalar(1 - (0.35 + 0.6 * this.normalizedSpeed) * 0.1 * dt);
    const cap = topSpeed * (this.nitroActive ? 1.12 : 1);
    if (this.vel.length() > cap) this.vel.setLength(cap);

    // ── GRIP ASSIST: rotate velocity toward heading ──
    if (speed > 1) {
      const dir = this.vel.clone().normalize();
      const targetSign = signedSpeed >= 0 ? 1 : -1;
      const angle = signedAngleY(dir.x, dir.z, fwd.x * targetSign, fwd.z * targetSign);
      const gripRate = this.drifting ? 2.0 : 7.5; // rad/s of velocity realignment
      this.vel.applyAxisAngle(UP, angle * Math.min(1, gripRate * dt));
      // Drifting scrubs a little speed — rewards clean lines.
      if (this.drifting) this.vel.multiplyScalar(1 - 0.25 * dt);
    }

    // ── integrate ──
    this.pos.addScaledVector(this.vel, dt);

    // ── wall collision: clamp lateral offset from the spline ──
    const proj = this.track.project(this.pos, this.trackHint);
    this.trackHint = proj.index;
    this.trackT = proj.t;
    const limit = this.track.halfWidth - 1.1; // car half-width margin
    if (Math.abs(proj.lateral) > limit) {
      const l = this.track.lefts[proj.index];
      const pen = Math.abs(proj.lateral) - limit;
      const side = Math.sign(proj.lateral);
      this.pos.addScaledVector(l, -side * pen);
      // Remove outward velocity, keep the along-wall component (scrape, not bounce).
      const outward = (this.vel.x * l.x + this.vel.z * l.z) * side;
      if (outward > 0) {
        this.vel.addScaledVector(l, -side * outward);
        this.wallHit = Math.min(1, outward / 15);
      }
      this.vel.multiplyScalar(1 - 1.5 * dt); // grinding friction

      // Glance-off: steer the nose toward the wall's direction so contact
      // becomes a scrape along it, not a dead stop with the nose pinned in.
      const tan = this.track.tangents[proj.index];
      let wallYaw = Math.atan2(tan.x, tan.z);
      // Pick the tangent direction closest to our current heading.
      const diff = ((wallYaw - this.heading) % (Math.PI * 2) + Math.PI * 3) % (Math.PI * 2) - Math.PI;
      if (Math.abs(diff) > Math.PI / 2) wallYaw += Math.PI;
      this.heading = lerpAngle(this.heading, wallYaw, damp(2.2, dt));
    }

    this.updateGearbox(signedSpeed);
    this.syncMesh(dt, yawRate, accel);
  }

  /** Cosmetic sawtooth RPM across 5 virtual gears — drives audio pitch + HUD. */
  updateGearbox(signedSpeed) {
    if (signedSpeed < -0.5) {
      this.gear = 0; // reverse
      this.rpm = clamp01(-signedSpeed / 12);
      return;
    }
    const gears = 5;
    const width = 1 / gears;
    this.gear = clamp(Math.floor(this.normalizedSpeed / width) + 1, 1, gears);
    const inGear = (this.normalizedSpeed - (this.gear - 1) * width) / width;
    this.rpm = lerp(0.15, 0.95, inGear) * (this.throttle > 0.01 ? 1 : 0.7);
  }

  syncMesh(dt = 0.016, yawRate = 0, accel = 0) {
    this.mesh.position.copy(this.pos);
    this.mesh.rotation.y = this.heading;

    // Fake weight transfer: roll out of turns, pitch under accel/brake.
    const rollTarget = -yawRate * 0.055 * this.normalizedSpeed * 4;
    const pitchTarget = -accel * 0.004;
    this.chassis.rotation.z = lerp(this.chassis.rotation.z, rollTarget, damp(6, dt));
    this.chassis.rotation.x = lerp(this.chassis.rotation.x, pitchTarget, damp(6, dt));

    // Wheel spin + front wheel steer angle.
    const spin = (this.vel.dot(this.forward()) / 0.36) * dt;
    for (const w of this.wheels) w.rotation.x += spin;
    for (const p of this.frontPivots)
      p.rotation.y = lerp(p.rotation.y, this.steer * 0.45, damp(10, dt));

    this.tailGlow.material.color.setHex(this.nitroActive ? 0x33aaff : 0x330000);
  }

  /** World position of the rear axle center — smoke/nitro emitter anchor. */
  rearPosition(side = 0) {
    const fwd = this.forward();
    const left = new THREE.Vector3(fwd.z, 0, -fwd.x);
    return this.pos.clone().addScaledVector(fwd, -1.8).addScaledVector(left, side * 0.85)
      .add(new THREE.Vector3(0, 0.3, 0));
  }

  dispose(scene) {
    scene.remove(this.mesh);
  }
}
