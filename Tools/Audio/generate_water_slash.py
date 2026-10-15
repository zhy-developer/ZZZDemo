"""Synthesize an original, short sword-through-water SFX without source recordings.

Uses NumPy and the standard library. Writes a 48 kHz stereo PCM16 WAV.
Run with an output path; existing files are never overwritten.
"""
from pathlib import Path
import sys
import wave

import numpy as np


def render(*, seed=71426, time_scale=1.0, pitch=1.0, droplets=30,
           water_gain=1.0, pan_direction=1.0):
    # Default settings reproduce version 01; optional parameters create sibling cuts.
    rate, duration = 48000, 1.18 * time_scale
    count = round(rate * duration)
    t = np.arange(count) / rate / time_scale
    rng = np.random.default_rng(seed)
    stereo = np.zeros((count, 2), dtype=np.float64)

    def noise(low, high):
        low, high = low * pitch, high * pitch
        spectrum = np.fft.rfft(rng.normal(size=count))
        hz = np.fft.rfftfreq(count, 1 / rate)
        # Smooth spectral edges avoid an artificial whistle at a hard cutoff.
        hp = (hz / max(low, 1)) ** 3
        shape = hp / (1 + hp) / (1 + (hz / high) ** 5)
        result = np.fft.irfft(spectrum * shape, n=count)
        return result / max(np.std(result), 1e-9)

    def add(signal, pan=0.0):
        position = np.clip(pan * pan_direction, -1, 1)
        stereo[:, 0] += signal * np.sqrt((1 - position) / 2)
        stereo[:, 1] += signal * np.sqrt((1 + position) / 2)

    def decay(start, attack, fall):
        age = np.maximum(t - start, 0)
        return (t >= start) * (1 - np.exp(-age / attack)) * np.exp(-age / fall)

    # Blade passage: rising, bright air followed by a short darker sweep.
    swing = np.exp(-((t - .105) / .055) ** 2) * (1 - np.exp(-t / .012))
    brightness = np.clip(t / .155, 0, 1)
    air = (noise(650, 3400) * (1 - brightness) + noise(1900, 10000) * brightness)
    add(.23 * air * swing, np.clip((t - .10) * 9, -.65, .65))
    add(.07 * noise(150, 850) * decay(.10, .003, .055), .05)

    # Water contact and breakup: irregular broadband texture, not a sustained hiss.
    splash = noise(260, 7800)
    texture = .66 + .19 * np.sin(2 * np.pi * (37 * t + 61 * t * t))
    texture += .15 * np.sin(2 * np.pi * 113 * t)
    add(.39 * water_gain * splash * decay(.135, .0015, .080) * texture, -.10)
    add(.12 * water_gain * noise(90, 1100) * decay(.138, .002, .040), .10)
    add(.09 * water_gain * noise(1800, 11500) * decay(.158, .009, .15), .28)

    # Quiet steel overtones give the cut a blade edge without a metal-on-metal clang.
    age = np.maximum(t - .125, 0)
    ring = (np.sin(2 * np.pi * pitch * (2450 * age - 520 * age * age))
            + .35 * np.sin(2 * np.pi * pitch * 5630 * age))
    add(.024 * ring * decay(.125, .0006, .035), -.18)

    # Individual droplets: short upward resonances plus tiny noisy impacts.
    droplet_noise = noise(900, 10500)
    for _ in range(droplets):
        start = .15 + float(rng.beta(1.2, 3.0)) * .72
        age = np.maximum(t - start, 0)
        frequency = float(rng.uniform(1050, 4200)) * pitch
        fall = float(rng.uniform(.005, .021))
        envelope = decay(start, .0005, fall)
        phase = 2 * np.pi * frequency * (age + 3.8 * age * age)
        droplet = (.75 * np.sin(phase) + .32 * droplet_noise) * envelope
        strength = float(rng.uniform(.022, .065)) * np.exp(-(start - .15) * 2)
        add(water_gain * strength * droplet, float(rng.uniform(-.85, .85)))

    # A little cross-channel reflection joins the layers without a large-room tail.
    dry = stereo.copy()
    for delay, gain in ((.027, .075), (.051, .042), (.083, .022)):
        offset = round(delay * rate)
        stereo[offset:] += dry[:-offset, ::-1] * gain

    stereo = np.tanh(stereo * 1.2)
    fade = np.clip(t / .004, 0, 1) * np.clip((1.18 - t) / .12, 0, 1)
    stereo *= fade[:, None]
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
