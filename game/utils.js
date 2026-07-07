// Small math helpers shared by all game systems.

export const clamp = (v, min, max) => Math.min(max, Math.max(min, v));
export const clamp01 = (v) => clamp(v, 0, 1);
export const lerp = (a, b, t) => a + (b - a) * t;

export function moveTowards(current, target, maxDelta) {
  if (Math.abs(target - current) <= maxDelta) return target;
  return current + Math.sign(target - current) * maxDelta;
}

/** Shortest-path angle interpolation, radians. */
export function lerpAngle(a, b, t) {
  let d = (b - a) % (Math.PI * 2);
  if (d > Math.PI) d -= Math.PI * 2;
  if (d < -Math.PI) d += Math.PI * 2;
  return a + d * t;
}

/** Signed angle (radians) from planar vector a to b around +Y. Vectors are {x,z}. */
export function signedAngleY(ax, az, bx, bz) {
  const dot = ax * bx + az * bz;
  const cross = az * bx - ax * bz; // y-component of a×b for XZ-plane vectors
  return Math.atan2(cross, dot);
}

/** Frame-rate independent exponential damping factor. */
export const damp = (rate, dt) => 1 - Math.exp(-rate * dt);

export function formatTime(seconds) {
  if (!isFinite(seconds)) return '--:--.---';
  const m = Math.floor(seconds / 60);
  const s = seconds - m * 60;
  return `${m}:${s.toFixed(3).padStart(6, '0')}`;
}

export const ordinal = (n) => (n === 1 ? '1st' : n === 2 ? '2nd' : n === 3 ? '3rd' : `${n}th`);
