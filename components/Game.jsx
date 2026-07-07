'use client';
import { useState, useEffect } from 'react';
import RaceView from './RaceView';
import { CAR_MODELS } from '../game/vehicle';
import { TRACKS } from '../game/track';

const DEFAULT_SETTINGS = { volume: 0.8, shake: true, difficulty: 1 };

/** Paint chip for the car card — solid color, or twin racing stripes if the car has a livery. */
function swatchStyle(model) {
  const body = `#${model.color.toString(16).padStart(6, '0')}`;
  if (!model.stripe) return body;
  const stripe = `#${model.stripe.toString(16).padStart(6, '0')}`;
  return `linear-gradient(90deg, ${body} 0 36%, ${stripe} 36% 45%, ${body} 45% 55%, ${stripe} 55% 64%, ${body} 64% 100%)`;
}

/**
 * Top-level screen state machine: main menu → car select → track select →
 * race. Settings persist to localStorage. The Three.js game only exists while
 * screen === 'race' (RaceView mounts/unmounts it).
 */
export default function Game() {
  const [screen, setScreen] = useState('main');
  const [carIndex, setCarIndex] = useState(0);
  const [trackIndex, setTrackIndex] = useState(0);
  const [mode, setMode] = useState('circuit');
  const [laps, setLaps] = useState(3);
  const [aiCount, setAiCount] = useState(3);
  const [settings, setSettings] = useState(DEFAULT_SETTINGS);

  useEffect(() => {
    try {
      const saved = localStorage.getItem('apexrush-settings');
      if (saved) setSettings({ ...DEFAULT_SETTINGS, ...JSON.parse(saved) });
    } catch { /* fresh browser */ }
  }, []);

  const saveSettings = (next) => {
    setSettings(next);
    localStorage.setItem('apexrush-settings', JSON.stringify(next));
  };

  if (screen === 'race') {
    return (
      <RaceView
        config={{ carIndex, trackIndex, mode, laps, aiCount, ...settings }}
        onQuit={() => setScreen('main')}
      />
    );
  }

  return (
    <div className="menu-root">
      <div className="menu-panel">
        {screen === 'main' && (
          <>
            <h1 className="title">APEX RUSH</h1>
            <p className="subtitle">arcade street racing</p>
            <button className="btn primary" onClick={() => setScreen('car')}>PLAY</button>
            <button className="btn" onClick={() => setScreen('settings')}>SETTINGS</button>
            <p className="hint">WASD / arrows drive · Space handbrake-drift · Shift nitro · Gamepad supported</p>
          </>
        )}

        {screen === 'car' && (
          <>
            <h2>SELECT CAR</h2>
            <div className="carousel">
              <button className="btn arrow" onClick={() =>
                setCarIndex((carIndex - 1 + CAR_MODELS.length) % CAR_MODELS.length)}>◄</button>
              <div className="card">
                <div className="swatch" style={{ background: swatchStyle(CAR_MODELS[carIndex]) }} />
                <h3>{CAR_MODELS[carIndex].name}</h3>
                <p>{CAR_MODELS[carIndex].blurb}</p>
                <p className="stats">{CAR_MODELS[carIndex].stats}</p>
              </div>
              <button className="btn arrow" onClick={() =>
                setCarIndex((carIndex + 1) % CAR_MODELS.length)}>►</button>
            </div>
            <button className="btn primary" onClick={() => setScreen('track')}>NEXT</button>
            <button className="btn" onClick={() => setScreen('main')}>BACK</button>
          </>
        )}

        {screen === 'track' && (
          <>
            <h2>RACE SETUP</h2>
            <div className="carousel">
              <button className="btn arrow" onClick={() =>
                setTrackIndex((trackIndex - 1 + TRACKS.length) % TRACKS.length)}>◄</button>
              <div className="card">
                <h3>{TRACKS[trackIndex].name}</h3>
                <p>{TRACKS[trackIndex].blurb}</p>
              </div>
              <button className="btn arrow" onClick={() =>
                setTrackIndex((trackIndex + 1) % TRACKS.length)}>►</button>
            </div>

            <div className="row">
              <label>Mode</label>
              <button className={`btn small ${mode === 'circuit' ? 'active' : ''}`}
                onClick={() => setMode('circuit')}>Circuit</button>
              <button className={`btn small ${mode === 'timetrial' ? 'active' : ''}`}
                onClick={() => setMode('timetrial')}>Time Trial</button>
            </div>
            <div className="row">
              <label>Laps: {laps}</label>
              <input type="range" min="1" max="9" value={laps}
                onChange={(e) => setLaps(+e.target.value)} />
            </div>
            {mode === 'circuit' && (
              <div className="row">
                <label>Opponents: {aiCount}</label>
                <input type="range" min="1" max="7" value={aiCount}
                  onChange={(e) => setAiCount(+e.target.value)} />
              </div>
            )}

            <button className="btn primary" onClick={() => setScreen('race')}>RACE</button>
            <button className="btn" onClick={() => setScreen('car')}>BACK</button>
          </>
        )}

        {screen === 'settings' && (
          <>
            <h2>SETTINGS</h2>
            <div className="row">
              <label>Volume: {Math.round(settings.volume * 100)}%</label>
              <input type="range" min="0" max="1" step="0.05" value={settings.volume}
                onChange={(e) => saveSettings({ ...settings, volume: +e.target.value })} />
            </div>
            <div className="row">
              <label>Camera shake</label>
              <button className={`btn small ${settings.shake ? 'active' : ''}`}
                onClick={() => saveSettings({ ...settings, shake: !settings.shake })}>
                {settings.shake ? 'ON' : 'OFF'}
              </button>
            </div>
            <div className="row">
              <label>AI difficulty</label>
              {['Easy', 'Normal', 'Hard'].map((label, i) => (
                <button key={label}
                  className={`btn small ${settings.difficulty === i ? 'active' : ''}`}
                  onClick={() => saveSettings({ ...settings, difficulty: i })}>{label}</button>
              ))}
            </div>
            <button className="btn" onClick={() => setScreen('main')}>BACK</button>
          </>
        )}
      </div>
    </div>
  );
}
