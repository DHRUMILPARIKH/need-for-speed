/**
 * Keyboard + gamepad input. Keyboard: WASD/arrows, Space = handbrake,
 * Shift = nitro. Gamepad (standard mapping): left stick steer, RT throttle,
 * LT brake, A = nitro, B = handbrake. Gamepads are polled, not evented.
 */
export class Input {
  constructor() {
    this.keys = new Set();
    this.onDown = (e) => {
      this.keys.add(e.code);
      // Stop arrows/space from scrolling the page.
      if (['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Space'].includes(e.code))
        e.preventDefault();
    };
    this.onUp = (e) => this.keys.delete(e.code);
  }

  attach() {
    window.addEventListener('keydown', this.onDown);
    window.addEventListener('keyup', this.onUp);
  }

  detach() {
    window.removeEventListener('keydown', this.onDown);
    window.removeEventListener('keyup', this.onUp);
    this.keys.clear();
  }

  read() {
    const k = this.keys;
    let steer = (k.has('KeyA') || k.has('ArrowLeft') ? -1 : 0)
              + (k.has('KeyD') || k.has('ArrowRight') ? 1 : 0);
    let throttle = k.has('KeyW') || k.has('ArrowUp') ? 1 : 0;
    let brake = k.has('KeyS') || k.has('ArrowDown') ? 1 : 0;
    let handbrake = k.has('Space');
    let nitro = k.has('ShiftLeft') || k.has('ShiftRight');

    const pads = typeof navigator !== 'undefined' && navigator.getGamepads
      ? navigator.getGamepads() : [];
    const gp = pads && pads[0];
    if (gp) {
      const axis = gp.axes[0] || 0;
      if (Math.abs(axis) > 0.12) steer += axis;              // deadzone
      throttle = Math.max(throttle, gp.buttons[7]?.value || 0);
      brake = Math.max(brake, gp.buttons[6]?.value || 0);
      nitro = nitro || !!gp.buttons[0]?.pressed;
      handbrake = handbrake || !!gp.buttons[1]?.pressed;
    }

    return {
      steer: Math.max(-1, Math.min(1, steer)),
      throttle, brake, handbrake, nitro,
    };
  }
}
