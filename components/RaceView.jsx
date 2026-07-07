'use client';
import { useEffect, useRef, useState } from 'react';
import { createGame } from '../game/engine';
import { formatTime, ordinal } from '../game/utils';

/**
 * Mounts the Three.js game and renders the HUD over it. Fast-changing readouts
 * (speed, timers, nitro, minimap) are written into DOM refs from the game's
 * per-frame callback — zero React re-renders during racing. React state is
 * only used for structural moments: countdown numbers and the results screen.
 */
export default function RaceView({ config, onQuit }) {
  const canvasRef = useRef(null);
  const speedRef = useRef(null);
  const gearRef = useRef(null);
  const nitroRef = useRef(null);
  const lapRef = useRef(null);
  const posRef = useRef(null);
  const curTimeRef = useRef(null);
  const bestTimeRef = useRef(null);
  const minimapRef = useRef(null);

  const [countdown, setCountdown] = useState(null);
  const [results, setResults] = useState(null);
  const [runId, setRunId] = useState(0); // bump to restart the race

  useEffect(() => {
    setCountdown(null);
    setResults(null);

    const game = createGame(canvasRef.current, config, {
      onCountdown: (n) => {
        setCountdown(n);
        if (n === 0) setTimeout(() => setCountdown(null), 900);
      },
      onResults: (r) => setResults(r),
      onHud: (d) => {
        // Imperative writes — runs at 60fps without touching React.
        if (speedRef.current) speedRef.current.textContent = Math.round(d.speedKmh);
        if (gearRef.current) gearRef.current.textContent = d.gear === 0 ? 'R' : d.gear;
        if (nitroRef.current) {
          nitroRef.current.style.width = `${d.nitro * 100}%`;
          nitroRef.current.style.background = d.nitroActive ? '#66ccff' : '#2f8fdd';
        }
        if (lapRef.current)
          lapRef.current.textContent = `LAP ${d.race.lap}/${d.race.totalLaps}`;
        if (posRef.current)
          posRef.current.textContent = config.mode === 'circuit'
            ? `${ordinal(d.race.position)} / ${d.race.carCount}` : '';
        if (curTimeRef.current)
          curTimeRef.current.textContent = formatTime(d.race.currentLapTime);
        if (bestTimeRef.current)
          bestTimeRef.current.textContent = `BEST ${formatTime(d.race.bestLap)}`;
        drawMinimap(minimapRef.current, d.minimap, d.cars);
      },
    });

    return () => game.dispose();
  }, [runId]); // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <div className="race-root">
      <canvas ref={canvasRef} className="game-canvas" />

      {/* HUD */}
      <div className="hud top-left">
        <div ref={lapRef} className="hud-big" />
        <div ref={posRef} className="hud-big accent" />
        <div ref={curTimeRef} className="hud-mono" />
        <div ref={bestTimeRef} className="hud-mono dim" />
      </div>

      <div className="hud bottom-right">
        <div className="speedo">
          <span ref={speedRef} className="speed-num">0</span>
          <span className="speed-unit">km/h</span>
          <span ref={gearRef} className="gear">1</span>
        </div>
        <div className="nitro-track"><div ref={nitroRef} className="nitro-fill" /></div>
        <div className="nitro-label">NITRO</div>
      </div>

      <canvas ref={minimapRef} className="hud minimap" width="180" height="180" />

      {countdown !== null && (
        <div className="countdown">{countdown === 0 ? 'GO!' : countdown}</div>
      )}

      {results && (
        <div className="results">
          <h1>
            {results.mode === 'timetrial' ? 'TIME TRIAL COMPLETE'
              : results.position === 1 ? 'YOU WIN!'
              : `FINISHED ${ordinal(results.position)}`}
          </h1>
          <table>
            <tbody>
              {results.lapTimes.map((t, i) => (
                <tr key={i}>
                  <td>Lap {i + 1}</td>
                  <td className="hud-mono">{formatTime(t)}</td>
                  <td>{t === results.best ? '◄ best' : ''}</td>
                </tr>
              ))}
              <tr className="total">
                <td>Total</td>
                <td className="hud-mono">{formatTime(results.total)}</td>
                <td />
              </tr>
            </tbody>
          </table>
          <div className="row center">
            <button className="btn primary" onClick={() => setRunId(runId + 1)}>RESTART</button>
            <button className="btn" onClick={onQuit}>MENU</button>
          </div>
        </div>
      )}
    </div>
  );
}

function drawMinimap(canvas, map, cars) {
  if (!canvas || !map) return;
  const ctx = canvas.getContext('2d');
  const W = canvas.width, H = canvas.height, pad = 14;
  const scale = Math.min(
    (W - pad * 2) / (map.maxX - map.minX),
    (H - pad * 2) / (map.maxZ - map.minZ));
  const toX = (x) => pad + (x - map.minX) * scale + (W - pad * 2 - (map.maxX - map.minX) * scale) / 2;
  const toY = (z) => H - (pad + (z - map.minZ) * scale + (H - pad * 2 - (map.maxZ - map.minZ) * scale) / 2);

  ctx.clearRect(0, 0, W, H);
  ctx.fillStyle = 'rgba(10,14,22,0.55)';
  ctx.beginPath();
  ctx.arc(W / 2, H / 2, W / 2 - 1, 0, Math.PI * 2);
  ctx.fill();

  ctx.strokeStyle = 'rgba(255,255,255,0.7)';
  ctx.lineWidth = 3;
  ctx.beginPath();
  map.pts.forEach(([x, z], i) => (i === 0 ? ctx.moveTo(toX(x), toY(z)) : ctx.lineTo(toX(x), toY(z))));
  ctx.closePath();
  ctx.stroke();

  for (const c of cars) {
    ctx.fillStyle = c.isPlayer ? '#4fc3ff' : '#ff5544';
    ctx.beginPath();
    ctx.arc(toX(c.x), toY(c.z), c.isPlayer ? 5 : 4, 0, Math.PI * 2);
    ctx.fill();
  }
}
