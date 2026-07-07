/**
 * Fully synthesized audio — no sound files needed.
 *   Engine: two detuned sawtooth oscillators through a lowpass; pitch follows
 *           the vehicle's sawtooth RPM, so gear "shifts" are audible.
 *   Skid:   looped white noise through a bandpass, faded in while drifting.
 *   Nitro:  the same noise through a resonant highpass — reads as rushing air.
 * Created lazily on race start (a user click has happened, so autoplay is fine).
 */
export class GameAudio {
  constructor(volume = 1) {
    this.ctx = null;
    this.volume = volume;
  }

  ensure() {
    if (this.ctx) return;
    const ctx = new (window.AudioContext || window.webkitAudioContext)();
    this.ctx = ctx;

    this.master = ctx.createGain();
    this.master.gain.value = this.volume * 0.5;
    this.master.connect(ctx.destination);

    // Engine.
    this.engineGain = ctx.createGain();
    this.engineGain.gain.value = 0;
    const lowpass = ctx.createBiquadFilter();
    lowpass.type = 'lowpass';
    lowpass.frequency.value = 900;
    this.engineGain.connect(lowpass).connect(this.master);
    this.oscA = ctx.createOscillator();
    this.oscB = ctx.createOscillator();
    this.oscA.type = 'sawtooth';
    this.oscB.type = 'square';
    this.oscA.connect(this.engineGain);
    this.oscB.connect(this.engineGain);
    this.oscA.start();
    this.oscB.start();

    // Shared noise buffer for skid + nitro.
    const noiseBuf = ctx.createBuffer(1, ctx.sampleRate, ctx.sampleRate);
    const data = noiseBuf.getChannelData(0);
    for (let i = 0; i < data.length; i++) data[i] = Math.random() * 2 - 1;

    const makeNoise = (filterType, freq, q) => {
      const src = ctx.createBufferSource();
      src.buffer = noiseBuf;
      src.loop = true;
      const filter = ctx.createBiquadFilter();
      filter.type = filterType;
      filter.frequency.value = freq;
      filter.Q.value = q;
      const gain = ctx.createGain();
      gain.gain.value = 0;
      src.connect(filter).connect(gain).connect(this.master);
      src.start();
      return gain;
    };
    this.skidGain = makeNoise('bandpass', 900, 1.2);
    this.nitroGain = makeNoise('highpass', 2200, 2);
  }

  update(vehicle) {
    if (!this.ctx) return;
    const t = this.ctx.currentTime;
    const smooth = 0.05;

    const freq = 48 + vehicle.rpm * 170;
    this.oscA.frequency.setTargetAtTime(freq, t, smooth);
    this.oscB.frequency.setTargetAtTime(freq * 0.503, t, smooth); // sub-octave growl
    const load = 0.10 + 0.14 * vehicle.rpm + (vehicle.throttle > 0 ? 0.06 : 0);
    this.engineGain.gain.setTargetAtTime(load, t, smooth);

    const skid = vehicle.drifting ? Math.min(0.35, Math.abs(vehicle.slipAngle) / 70) : 0;
    this.skidGain.gain.setTargetAtTime(skid, t, 0.08);
    this.nitroGain.gain.setTargetAtTime(vehicle.nitroActive ? 0.22 : 0, t, 0.06);
  }

  setVolume(v) {
    this.volume = v;
    if (this.master) this.master.gain.value = v * 0.5;
  }

  dispose() {
    if (this.ctx) this.ctx.close();
    this.ctx = null;
  }
}
