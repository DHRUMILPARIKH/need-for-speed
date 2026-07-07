import * as THREE from 'three';
import { lerp, lerpAngle, damp } from './utils';

/**
 * Chase camera, same feel recipe as the Unity build: yaw-lagged follow (the car
 * rotates ahead of the camera in drifts), speed-based FOV with a nitro kick,
 * and sine-mix shake for speed/nitro/impacts.
 */
export class ChaseCamera {
  constructor(camera, shakeEnabled = true) {
    this.camera = camera;
    this.shakeEnabled = shakeEnabled;
    this.yaw = 0;
    this.pos = new THREE.Vector3();
    this.impact = 0;
    this.baseFov = 58;
  }

  snap(vehicle) {
    this.yaw = vehicle.heading;
    this.pos.copy(this.idealPos(vehicle));
    this.camera.position.copy(this.pos);
  }

  idealPos(vehicle) {
    return new THREE.Vector3(
      vehicle.pos.x - Math.sin(this.yaw) * 7.2,
      vehicle.pos.y + 3.1,
      vehicle.pos.z - Math.cos(this.yaw) * 7.2,
    );
  }

  addImpact(strength) { this.impact = Math.max(this.impact, strength); }

  update(dt, vehicle) {
    // Rotation lag: lower rate = more drift drama on screen.
    this.yaw = lerpAngle(this.yaw, vehicle.heading, damp(4.2, dt));
    this.pos.lerp(this.idealPos(vehicle), damp(7, dt));
    this.camera.position.copy(this.pos);
    this.camera.lookAt(vehicle.pos.x, vehicle.pos.y + 1.15, vehicle.pos.z);

    // FOV: the cheapest, strongest speed cue there is.
    const targetFov = this.baseFov
      + 17 * vehicle.normalizedSpeed
      + (vehicle.nitroActive ? 13 : 0);
    this.camera.fov = lerp(this.camera.fov, targetFov, damp(5, dt));
    this.camera.updateProjectionMatrix();

    // Shake: layered sines read as vibration, not glitch.
    this.impact = Math.max(0, this.impact - dt * 2.5);
    if (this.shakeEnabled) {
      const amount = 0.05 * vehicle.normalizedSpeed ** 2
        + (vehicle.nitroActive ? 0.14 : 0)
        + this.impact * 0.4;
      if (amount > 0.002) {
        const t = performance.now() / 1000;
        this.camera.position.x += Math.sin(t * 39) * amount * 0.5 + Math.sin(t * 17) * amount * 0.3;
        this.camera.position.y += Math.cos(t * 47) * amount * 0.4;
      }
    }
  }
}
