"""Original wind/water sword swing, using the first water-blade timbre.

Synthesizes noise and resonances only; no source recordings. Does not overwrite.
"""
from pathlib import Path
import sys
import wave

import numpy as np

from generate_water_slash import render


def render_wind_water():
    rate, water = render(seed=151026, time_scale=1.05, pitch=.92,
                         droplets=23, water_gain=1.05, pan_direction=1.0)
    duration = 1.50
    count = round(duration * rate)
    t = np.arange(count) / rate
    rng = np.random.default_rng(151027)
    mix = np.zeros((count, 2), dtype=np.float64)

    # Keep the first version's clean water breakup, following a short wind lead-in.
    offset = round(.055 * rate)
    mix[offset:offset + len(water)] += water.astype(np.float64) / 32768 * .62

    def noise(low, high):
        hz = np.fft.rfftfreq(count, 1 / rate)
        hp = (hz / low) ** 3
        shape = hp / (1 + hp) / (1 + (hz / high) ** 5)
        signal = np.fft.irfft(np.fft.rfft(rng.normal(size=count)) * shape, n=count)
        return signal / max(np.std(signal), 1e-9)

    def add(signal, pan):
        pan = np.clip(pan, -.85, .85)
        mix[:, 0] += signal * np.sqrt((1 - pan) / 2)
        mix[:, 1] += signal * np.sqrt((1 + pan) / 2)

    def decay(start, attack, tail):
        age = np.maximum(t - start, 0)
        return (t >= start) * (1 - np.exp(-age / attack)) * np.exp(-age / tail)

    # Wind tightens toward the blade pass, then falls into a lower, hollow gust.
    rise = np.exp(-((t - .155) / .082) ** 2) * (1 - np.exp(-t / .022))
    bright = np.clip(t / .20, 0, 1)
    airflow = noise(160, 1600) * (1 - bright) + noise(750, 5800) * bright
    swirl = .8 + .13 * np.sin(2 * np.pi * (14 * t + 34 * t * t))
    add(.17 * airflow * rise * swirl, np.tanh((t - .17) * 7) * .65)
    add(.11 * noise(90, 720) * decay(.17, .009, .105), -.08)
    add(.055 * noise(380, 2400) * decay(.24, .025, .20), .35)

    # A compact rushing-water body connects the gust and splash, without thunder.
    ripple = .68 + .20 * np.sin(2 * np.pi * (31 * t + 24 * t * t))
    ripple += .12 * np.sin(2 * np.pi * 89 * t)
    add(.09 * noise(240, 3800) * decay(.19, .003, .105) * ripple, .10)

    # Small wet eddies on the tail; these are resonances, not a musical note.
    for start, frequency, gain, pan in ((.29, 580, .022, -.35),
                                       (.39, 820, .016, .42),
                                       (.51, 680, .010, -.18)):
        age = np.maximum(t - start, 0)
        phase = 2 * np.pi * frequency * (age + 1.9 * age * age)
        add(gain * np.sin(phase) * decay(start, .001, .019), pan)

    dry = mix.copy()
    for delay, gain in ((.043, .045), (.079, .022)):
        samples = round(delay * rate)
        mix[samples:] += dry[:-samples, ::-1] * gain
    mix -= np.mean(mix, axis=0)
    mix = np.tanh(mix * 1.15)
    fade = np.clip(t / .006, 0, 1) * np.clip((duration - t) / .18, 0, 1)
    mix *= fade[:, None]
    mix *= 10 ** (-1.5 / 20) / max(np.max(np.abs(mix)), 1e-9)
    return rate, np.round(mix * 32767).astype('<i2')


if __name__ == '__main__':
    destination = Path(sys.argv[1])
    if destination.exists():
        raise SystemExit(f'Refusing to overwrite: {destination}')
    rate, samples = render_wind_water()
    destination.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(destination), 'wb') as output:
        output.setnchannels(2)
        output.setsampwidth(2)
        output.setframerate(rate)
        output.writeframes(samples.tobytes())
    print(f'Created: {destination.resolve()} | {len(samples) / rate:.2f}s | 48kHz stereo PCM16')
