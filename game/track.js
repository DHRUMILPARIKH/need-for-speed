import * as THREE from 'three';

const UP = new THREE.Vector3(0, 1, 0);

/**
 * Track definitions: control points (x, z) of a closed loop. Catmull-Rom smooths
 * them into the racing spline, and ALL geometry — road ribbon, edge lines, walls,
 * start line, scenery — is generated from that spline. No art assets needed.
 */
export const TRACKS = [
  {
    id: 'harbor',
    name: 'Harbor Loop',
    blurb: 'Fast and flowing. Good first track.',
    points: [
      [0, 0], [70, -12], [130, 15], [170, 70], [155, 130], [105, 160],
      [70, 215], [90, 275], [40, 320], [-45, 325], [-105, 275],
      [-95, 205], [-150, 165], [-185, 100], [-160, 35], [-95, 5],
    ],
  },
  {
    id: 'canyon',
    name: 'Canyon Circuit',
    blurb: 'Tight hairpins. Drift practice.',
    points: [
      [0, 0], [85, 0], [145, 40], [155, 110], [100, 140], [40, 120],
      [-10, 155], [0, 225], [65, 255], [135, 245], [175, 305],
      [120, 365], [25, 350], [-65, 375], [-135, 330], [-145, 255],
      [-90, 220], [-125, 160], [-175, 105], [-150, 35], [-80, -12],
    ],
  },
];

export class Track {
  constructor(scene, def) {
    this.halfWidth = 7;          // road half-width in meters
    this.sampleCount = 512;

    const pts = def.points.map(([x, z]) => new THREE.Vector3(x, 0, z));
    this.curve = new THREE.CatmullRomCurve3(pts, true, 'catmullrom', 0.5);
    this.length = this.curve.getLength();

    // Pre-sample the spline once; every physics/AI query works off these arrays.
    this.centers = [];
    this.tangents = [];
    this.lefts = [];
    for (let i = 0; i < this.sampleCount; i++) {
      const t = i / this.sampleCount;
      const c = this.curve.getPointAt(t);
      const tan = this.curve.getTangentAt(t).setY(0).normalize();
      this.centers.push(c);
      this.tangents.push(tan);
      this.lefts.push(new THREE.Vector3().crossVectors(UP, tan).normalize());
    }

    this.group = new THREE.Group();
    this.buildGround();
    this.buildRoad();
    this.buildEdgeLines();
    this.buildWalls();
    this.buildStartLine();
    this.buildScenery();
    scene.add(this.group);
  }

  // ── geometry builders ────────────────────────────────────────────────────

  ribbon(offsetA, offsetB, y, material) {
    // Generic closed strip between two lateral offsets from the centerline.
    const n = this.sampleCount;
    const positions = new Float32Array(n * 2 * 3);
    const indices = [];
    for (let i = 0; i < n; i++) {
      const c = this.centers[i], l = this.lefts[i];
      positions.set([c.x + l.x * offsetA, y, c.z + l.z * offsetA], i * 6);
      positions.set([c.x + l.x * offsetB, y, c.z + l.z * offsetB], i * 6 + 3);
      const j = (i + 1) % n;
      indices.push(i * 2, i * 2 + 1, j * 2, j * 2, i * 2 + 1, j * 2 + 1);
    }
    const geo = new THREE.BufferGeometry();
    geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    geo.setIndex(indices);
    geo.computeVertexNormals();
    const mesh = new THREE.Mesh(geo, material);
    mesh.receiveShadow = true;
    this.group.add(mesh);
    return mesh;
  }

  buildGround() {
    const geo = new THREE.PlaneGeometry(1400, 1400);
    const mat = new THREE.MeshStandardMaterial({ color: 0x27331f });
    const ground = new THREE.Mesh(geo, mat);
    ground.rotation.x = -Math.PI / 2;
    ground.position.y = -0.06;
    ground.receiveShadow = true;
    this.group.add(ground);
  }

  buildRoad() {
    this.ribbon(this.halfWidth, -this.halfWidth, 0,
      new THREE.MeshStandardMaterial({ color: 0x2c2c31, roughness: 0.95 }));
  }

  buildEdgeLines() {
    const mat = new THREE.MeshBasicMaterial({ color: 0xdddddd });
    this.ribbon(this.halfWidth - 0.25, this.halfWidth - 0.65, 0.015, mat);
    this.ribbon(-this.halfWidth + 0.65, -this.halfWidth + 0.25, 0.015, mat);
  }

  buildWalls() {
    // Vertical ribbon walls just outside the road, alternating red/white segments.
    const n = this.sampleCount;
    for (const side of [1, -1]) {
      const off = side * (this.halfWidth + 0.9);
      const positions = new Float32Array(n * 2 * 3);
      const colors = new Float32Array(n * 2 * 3);
      const indices = [];
      const red = new THREE.Color(0xb33030), white = new THREE.Color(0xd8d8d8);
      for (let i = 0; i < n; i++) {
        const c = this.centers[i], l = this.lefts[i];
        positions.set([c.x + l.x * off, 0, c.z + l.z * off], i * 6);
        positions.set([c.x + l.x * off, 1.1, c.z + l.z * off], i * 6 + 3);
        const col = Math.floor(i / 8) % 2 === 0 ? red : white;
        colors.set([col.r, col.g, col.b, col.r, col.g, col.b], i * 6);
        const j = (i + 1) % n;
        indices.push(i * 2, j * 2, i * 2 + 1, i * 2 + 1, j * 2, j * 2 + 1);
      }
      const geo = new THREE.BufferGeometry();
      geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
      geo.setAttribute('color', new THREE.BufferAttribute(colors, 3));
      geo.setIndex(indices);
      geo.computeVertexNormals();
      const mat = new THREE.MeshStandardMaterial({ vertexColors: true, side: THREE.DoubleSide });
      this.group.add(new THREE.Mesh(geo, mat));
    }
  }

  buildStartLine() {
    const c = this.centers[0], tan = this.tangents[0], l = this.lefts[0];
    const geo = new THREE.PlaneGeometry(this.halfWidth * 2 - 1, 2.5);
    // Checkerboard via canvas texture.
    const cv = document.createElement('canvas');
    cv.width = 128; cv.height = 16;
    const ctx = cv.getContext('2d');
    for (let x = 0; x < 16; x++) for (let y = 0; y < 2; y++) {
      ctx.fillStyle = (x + y) % 2 ? '#fff' : '#111';
      ctx.fillRect(x * 8, y * 8, 8, 8);
    }
    const tex = new THREE.CanvasTexture(cv);
    const mesh = new THREE.Mesh(geo, new THREE.MeshBasicMaterial({ map: tex }));
    mesh.rotation.x = -Math.PI / 2;
    mesh.position.set(c.x, 0.02, c.z);
    mesh.rotation.z = Math.atan2(l.x, l.z) + Math.PI / 2;
    // Orient the stripe across the road:
    mesh.rotation.order = 'YXZ';
    mesh.rotation.set(-Math.PI / 2, 0, -Math.atan2(tan.x, tan.z));
    this.group.add(mesh);
  }

  buildScenery() {
    // Low-poly "city blocks" scattered outside the track so speed is readable.
    const rng = mulberry(42);
    const box = new THREE.BoxGeometry(1, 1, 1);
    const mats = [0x3a4150, 0x4a4440, 0x39504a, 0x504a5a].map(
      (c) => new THREE.MeshStandardMaterial({ color: c }));
    let placed = 0, guard = 0;
    while (placed < 90 && guard++ < 2000) {
      const x = (rng() - 0.5) * 900, z = (rng() - 0.5) * 900 + 160;
      // Keep clear of the road.
      let minD = Infinity;
      for (let i = 0; i < this.sampleCount; i += 8) {
        const dx = this.centers[i].x - x, dz = this.centers[i].z - z;
        minD = Math.min(minD, dx * dx + dz * dz);
      }
      if (minD < 26 * 26) continue;
      const h = 6 + rng() * 30, w = 8 + rng() * 14;
      const m = new THREE.Mesh(box, mats[placed % mats.length]);
      m.position.set(x, h / 2, z);
      m.scale.set(w, h, 8 + rng() * 14);
      this.group.add(m);
      placed++;
    }
  }

  // ── physics/AI queries ───────────────────────────────────────────────────

  /**
   * Project a world position onto the spline. `hint` is the caller's last known
   * sample index, so the search is a cheap local window instead of a full scan.
   * Returns { index, t, lateral } — lateral is signed meters left(+)/right(-)
   * of centerline; |lateral| > halfWidth means "in the wall".
   */
  project(pos, hint = 0) {
    const n = this.sampleCount;
    let best = hint, bestD = Infinity;
    for (let k = -10; k <= 10; k++) {
      const i = ((hint + k) % n + n) % n;
      const dx = this.centers[i].x - pos.x, dz = this.centers[i].z - pos.z;
      const d = dx * dx + dz * dz;
      if (d < bestD) { bestD = d; best = i; }
    }
    const l = this.lefts[best];
    const lateral = (pos.x - this.centers[best].x) * l.x + (pos.z - this.centers[best].z) * l.z;
    return { index: best, t: best / n, lateral };
  }

  /** Total direction change (radians) over the next `span` samples — corner severity. */
  bendAhead(index, span) {
    const n = this.sampleCount;
    const a = this.tangents[index % n];
    const b = this.tangents[(index + span) % n];
    return Math.acos(Math.max(-1, Math.min(1, a.dot(b))));
  }

  /** Grid slot `i` (0 = pole). Two-wide grid a few meters behind the start line. */
  gridSlot(i) {
    const n = this.sampleCount;
    const metersPerSample = this.length / n;
    const back = Math.round((6 + Math.floor(i / 2) * 7) / metersPerSample);
    const idx = (n - back) % n;
    const side = (i % 2 === 0 ? 1 : -1) * 2.6;
    const c = this.centers[idx], l = this.lefts[idx], tan = this.tangents[idx];
    return {
      position: new THREE.Vector3(c.x + l.x * side, 0, c.z + l.z * side),
      heading: Math.atan2(tan.x, tan.z),
      index: idx,
    };
  }

  /** 2D outline for the HUD minimap: [[x,z], ...] plus bounds. */
  minimapData() {
    const pts = [];
    let minX = Infinity, maxX = -Infinity, minZ = Infinity, maxZ = -Infinity;
    for (let i = 0; i < this.sampleCount; i += 4) {
      const c = this.centers[i];
      pts.push([c.x, c.z]);
      minX = Math.min(minX, c.x); maxX = Math.max(maxX, c.x);
      minZ = Math.min(minZ, c.z); maxZ = Math.max(maxZ, c.z);
    }
    return { pts, minX, maxX, minZ, maxZ };
  }
}

/** Tiny seeded PRNG so scenery is stable between reloads. */
function mulberry(seed) {
  let a = seed;
  return () => {
    a |= 0; a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
