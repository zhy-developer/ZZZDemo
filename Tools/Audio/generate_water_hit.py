"""Synthesize a water-blade hit matching the light water-slash family.

Original procedural audio, no source recordings. Immediate contact rather than
the slash's wind-up, with a compact impact and scattered water droplets.
Writes 48 kHz stereo PCM16 WAV and refuses to overwrite existing files.
"""
from pathlib import Path
import sys
import wave

import numpy as np


def render(seed=71526):
    rate, duration = 48000, .90
    count = round(rate * duration)
    t = np.arange(count) / rate
    rng = np.random.default_rng(seed)
    stereo = np.zeros((count, 2), dtype=np.float64)

    def noise(low, high):
        hz = np.fft.rfftfreq(count, 1 / rate)
        hp = (hz / max(low, 1)) ** 3
        shape = hp / (1 + hp) / (1 + (hz / high) ** 5)
        result = np.fft.irfft(np.fft.rfft(rng.normal(size=count)) * shape, n=count)
        return result / max(np.std(result), 1e-9)

    def decay(start, attack, fall):
        age = np.maximum(t - start, 0)
        return (t >= start) * (1 - np.exp(-age / attack)) * np.exp(-age / fall)

    def add(signal, pan=0):
        pan = np.clip(pan, -1, 1)
        stereo[:, 0] += signal * np.sqrt((1 - pan) / 2)
        stereo[:, 1] += signal * np.sqrt((1 + pan) / 2)

    # Contact lands in the opening milliseconds, aligned with a confirmed hit.
    add(.27 * noise(1000, 8800) * decay(.001, .0005, .012))
    add(.22 * noise(180, 1700) * decay(.002, .001, .030))
    age = np.maximum(t - .002, 0)
    phase = 2 * np.pi * (72 * age + 100 * .020 * (1 - np.exp(-age / .020)))
    add(.19 * np.sin(phase) * decay(.002, .0012, .036))

    # Short blade edge, kept quiet to avoid a metal-on-metal ring.
    edge = np.sin(2 * np.pi * (2380 * t - 700 * t * t))
    add(.021 * edge * decay(.001, .0005, .022), -.12)
    add(.11 * noise(1600, 10500) * decay(.002, .001, .040), .08)

    # Broken-water texture follows the contact instead of masking its transient.
    texture = .66 + .20 * np.sin(2 * np.pi * (43 * t + 55 * t * t))
    texture += .14 * np.sin(2 * np.pi * 127 * t)
    add(.34 * noise(300, 7800) * decay(.005, .0015, .070) * texture, -.16)
    add(.13 * noise(100, 1050) * decay(.009, .002, .043), .12)
    add(.09 * noise(1900, 11000) * decay(.023, .004, .092), .32)
    add(.065 * noise(650, 6600) * decay(.048, .002, .045), -.37)

    # Reuse the first slash's small upward water resonances, with a tighter tail.
    droplet_noise = noise(900, 10500)
    for _ in range(25):
        start = .030 + float(rng.beta(1.2, 3.3)) * .47
        age = np.maximum(t - start, 0)
        frequency = float(rng.uniform(1150, 4100))
        envelope = decay(start, .0005, float(rng.uniform(.005, .018)))
        phase = 2 * np.pi * frequency * (age + 3.8 * age * age)
        droplet = (.75 * np.sin(phase) + .32 * droplet_noise) * envelope
        strength = float(rng.uniform(.023, .060)) * np.exp(-(start - .03) * 3)
        add(strength * droplet, float(rng.uniform(-.75, .75)))

    # Small reflections keep the hit compact for overlapping combo strikes.
    dry = stereo.copy()
    for delay, gain in ((.019, .050), (.037, .026), (.061, .012)):
        offset = round(delay * rate)
        stereo[offset:] += dry[:-offset, ::-1] * gain
    stereo = np.tanh(stereo * 1.15)
    stereo *= (np.clip(t / .001, 0, 1) * np.clip((duration - t) / .10, 0, 1))[:, None]
    stereo *= (10 ** (-1.5 / 20)) / max(np.max(np.abs(stereo)), 1e-9)
    return rate, np.round(stereo * 32767).astype('<i2')


if __name__ == '__main__':
    destination = Path(sys.argv[1])
    if destination.exists():
        raise SystemExit(f'Refusing to overwrite: {destination}')
    rate, samples = render()
    destination.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(destination), 'wb') as output:
        output.setnchannels(2)
        output.setsampwidth(2)
        output.setframerate(rate)
        output.writeframes(samples.tobytes())
    print(f'Created: {destination.resolve()} | {len(samples) / rate:.2f}s | 48kHz stereo PCM16')
