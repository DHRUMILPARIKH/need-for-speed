import * as THREE from 'three';

/**
 * Sprite-pool particles: tire smoke while drifting, blue flame puffs on nitro.
 * Textures are generated on a canvas — no image assets.
 */
export class ParticleFX {
  constructor(scene) {
    this.scene = scene;
    this.pool = [];
    this.active = [];

    const makeTexture = (inner, outer) => {
      const cv = document.createElement('canvas');
      cv.width = cv.height = 64;
      const ctx = cv.getContext('2d');
      const grad = ctx.createRadialGradient(32, 32, 2, 32, 32, 30);
      grad.addColorStop(0, inner);
      grad.addColorStop(1, outer);
      ctx.fillStyle = grad;
      ctx.fillRect(0, 0, 64, 64);
      return new THREE.CanvasTexture(cv);
    };
    this.smokeMat = new THREE.SpriteMaterial({
      map: makeTexture('rgba(230,230,230,0.55)', 'rgba(230,230,230,0)'),
      depthWrite: false, transparent: true,
    });
    this.nitroMat = new THREE.SpriteMaterial({
      map: makeTexture('rgba(120,190,255,0.9)', 'rgba(60,90,255,0)'),
      depthWrite: false, transparent: true, blending: THREE.AdditiveBlending,
    });

    // Each sprite gets its own material clone so per-particle opacity fades
    // don't bleed across the pool; spawn() just swaps the texture/blending.
    for (let i = 0; i < 140; i++) {
      const s = new THREE.Sprite(this.smokeMat.clone());
      s.visible = false;
      scene.add(s);
      this.pool.push(s);
    }
  }

  spawn(pos, vel, life, startScale, growth, material) {
    const sprite = this.pool.pop();
    if (!sprite) return;
    sprite.material.map = material.map;
    sprite.material.blending = material.blending;
    sprite.material.opacity = 1;
    sprite.position.copy(pos);
    sprite.scale.setScalar(startScale);
    sprite.visible = true;
    this.active.push({ sprite, vel: vel.clone(), life, maxLife: life, growth });
  }

  smoke(pos) {
    this.spawn(pos,
      new THREE.Vector3((Math.random() - 0.5) * 1.5, 1 + Math.random(), (Math.random() - 0.5) * 1.5),
      0.9 + Math.random() * 0.5, 0.7, 2.4, this.smokeMat);
  }

  nitro(pos, backward) {
    this.spawn(pos, backward.clone().multiplyScalar(9).add(new THREE.Vector3(0, 0.5, 0)),
      0.22, 0.5, 1.2, this.nitroMat);
  }

  /** Called per frame by the engine with every vehicle. */
  emitFrom(vehicle, dt) {
    if (vehicle.drifting && Math.random() < dt * 45) {
      this.smoke(vehicle.rearPosition(1));
      this.smoke(vehicle.rearPosition(-1));
    }
    if (vehicle.nitroActive && Math.random() < dt * 80) {
      const back = vehicle.forward().multiplyScalar(-1);
      this.nitro(vehicle.rearPosition(0.3), back);
      this.nitro(vehicle.rearPosition(-0.3), back);
    }
  }

  update(dt) {
    for (let i = this.active.length - 1; i >= 0; i--) {
      const p = this.active[i];
      p.life -= dt;
      if (p.life <= 0) {
        p.sprite.visible = false;
        this.pool.push(p.sprite);
        this.active.splice(i, 1);
        continue;
      }
      p.sprite.position.addScaledVector(p.vel, dt);
      p.sprite.scale.addScalar(p.growth * dt);
      p.sprite.material.opacity = p.life / p.maxLife;
    }
  }
}
