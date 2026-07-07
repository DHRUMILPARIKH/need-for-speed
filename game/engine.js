import * as THREE from 'three';
import { Track, TRACKS } from './track';
import { Vehicle, CAR_MODELS } from './vehicle';
import { Input } from './input';
import { AIDriver } from './ai';
import { Race } from './race';
import { ChaseCamera } from './camera';
import { GameAudio } from './audio';
import { ParticleFX } from './fx';

/**
 * Composition root: builds the scene from a race config, owns the rAF loop,
 * and reports to React through callbacks. React never touches Three.js;
 * the game never touches the DOM outside its canvas.
 *
 * config: { carIndex, trackIndex, mode ('circuit'|'timetrial'), laps, aiCount,
 *           difficulty, volume, shake }
 * callbacks: { onCountdown(n), onHud(data), onResults(results) }
 */
export function createGame(canvas, config, callbacks) {
  // ── renderer / scene ──
  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
  renderer.setSize(window.innerWidth, window.innerHeight);

  const scene = new THREE.Scene();
  scene.background = new THREE.Color(0x101624);
  scene.fog = new THREE.Fog(0x101624, 150, 620);

  scene.add(new THREE.HemisphereLight(0x8899cc, 0x223311, 0.9));
  const sun = new THREE.DirectionalLight(0xffeecc, 1.4);
  sun.position.set(120, 180, 80);
  scene.add(sun);

  const camera = new THREE.PerspectiveCamera(58, window.innerWidth / window.innerHeight, 0.1, 900);

  // ── world ──
  const track = new Track(scene, TRACKS[config.trackIndex]);
  const fx = new ParticleFX(scene);
  const audio = new GameAudio(config.volume);
  const input = new Input();
  input.attach();

  // ── cars ──
  const aiCount = config.mode === 'timetrial' ? 0 : config.aiCount;
  const vehicles = [];
  const drivers = []; // parallel: null = player

  const spawn = (specIndex, gridIndex, isPlayer, name) => {
    const v = new Vehicle(scene, CAR_MODELS[specIndex % CAR_MODELS.length], track);
    const slot = track.gridSlot(gridIndex);
    v.place(slot.position, slot.heading, slot.index);
    vehicles.push(v);
    return { vehicle: v, isPlayer, name };
  };

  const entries = [spawn(config.carIndex, 0, true, 'You')];
  for (let i = 0; i < aiCount; i++)
    entries.push(spawn(config.carIndex + 1 + i, i + 1, false, `Rival ${i + 1}`));

  const race = new Race({
    mode: config.mode,
    totalLaps: config.laps,
    entries,
    callbacks: {
      onCountdown: callbacks.onCountdown,
      onGo: () => audio.ensure(),
      onLap: () => {},
      onFinish: (e) => {
        if (!e.isPlayer) return;
        callbacks.onResults({
          mode: config.mode,
          position: e.position,
          carCount: race.entries.length,
          lapTimes: e.lapTimes,
          best: e.best,
          total: e.finishTime,
        });
      },
    },
  });

  for (let i = 1; i < entries.length; i++)
    drivers[i] = new AIDriver(entries[i].vehicle, track, config.difficulty, race);

  const player = entries[0].vehicle;
  const chase = new ChaseCamera(camera, config.shake);
  chase.snap(player);
  callbacks.onCountdown(3);

  const minimap = track.minimapData();

  // ── loop ──
  let raf = 0;
  let last = performance.now();
  let disposed = false;

  function frame(now) {
    if (disposed) return;
    raf = requestAnimationFrame(frame);
    const dt = Math.min((now - last) / 1000, 0.05); // clamp tab-switch spikes
    last = now;

    // Drive every car: player from real input, bots from their AI driver.
    for (let i = 0; i < entries.length; i++) {
      const inp = drivers[i] ? drivers[i].compute(dt) : input.read();
      entries[i].vehicle.update(dt, inp);
      fx.emitFrom(entries[i].vehicle, dt);
    }

    race.update(dt);
    if (player.wallHit > 0) chase.addImpact(player.wallHit);
    chase.update(dt, player);
    fx.update(dt);
    audio.update(player);
    renderer.render(scene, camera);

    // HUD payload — React writes it into DOM refs (no re-render per frame).
    callbacks.onHud({
      speedKmh: player.speedKmh,
      gear: player.gear,
      nitro: player.nitro,
      nitroActive: player.nitroActive,
      drifting: player.drifting,
      race: race.playerHud(),
      minimap,
      cars: entries.map((e) => ({
        x: e.vehicle.pos.x, z: e.vehicle.pos.z, isPlayer: e.isPlayer,
      })),
    });
  }
  raf = requestAnimationFrame(frame);

  const onResize = () => {
    camera.aspect = window.innerWidth / window.innerHeight;
    camera.updateProjectionMatrix();
    renderer.setSize(window.innerWidth, window.innerHeight);
  };
  window.addEventListener('resize', onResize);

  return {
    setVolume: (v) => audio.setVolume(v),
    dispose() {
      disposed = true;
      cancelAnimationFrame(raf);
      window.removeEventListener('resize', onResize);
      input.detach();
      audio.dispose();
      renderer.dispose();
      scene.traverse((o) => {
        if (o.geometry) o.geometry.dispose();
        if (o.material) (Array.isArray(o.material) ? o.material : [o.material])
          .forEach((m) => m.dispose());
      });
    },
  };
}
