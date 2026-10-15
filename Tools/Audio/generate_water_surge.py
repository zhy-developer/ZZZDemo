"""Heavy sword-through-surf variant, synthesized without source recordings.

Usage: python generate_water_surge.py output.wav
Keeps the original light slash intact; refuses to overwrite an existing file.
"""
from pathlib import Path
import sys
import wave

import numpy as np


def render():
    rate, duration = 48000, 2.15
    n = round(rate * duration)
    t = np.arange(n) / rate
    rng = np.random.default_rng(71427)
    output = np.zeros((n, 2))
    hz = np.fft.rfftfreq(n, 1 / rate)

    def noise(low, high):
        spectrum = np.fft.rfft(rng.normal(size=n))
        ratio = (hz / low) ** 3
        spectral_shape = ratio / (1 + ratio) / (1 + (hz / high) ** 5)
        signal = np.fft.irfft(spectrum * spectral_shape, n=n)
        return signal / max(np.std(signal), 1e-9)

    def envelope(start, attack, decay):
        age = np.maximum(t - start, 0)
        return (t >= start) * (1 - np.exp(-age / attack)) * np.exp(-age / decay)

    def add(signal, pan=0):
        pan = np.clip(pan, -1, 1)
        output[:, 0] += signal * np.sqrt((1 - pan) / 2)
        output[:, 1] += signal * np.sqrt((1 + pan) / 2)

    # A weightier, lower blade sweep leads into the crest of the wave.
    swing = np.exp(-((t - .115) / .062) ** 2) * np.clip(t / .025, 0, 1)
    add(.22 * noise(210, 2300) * swing, np.clip((t - .11) * 8, -.55, .65))
    add(.055 * noise(1600, 5800) * swing, .2)

    # Dense central body: turbulent water rather than isolated, bright droplets.
    slow_grid = np.linspace(0, duration, 37)
    turbulence = np.interp(t, slow_grid, rng.uniform(.45, 1.1, len(slow_grid)))
    add(.36 * noise(65, 590) * envelope(.155, .006, .29) * turbulence)
    add(.43 * noise(180, 2300) * envelope(.15, .004, .21), -.06)
    add(.16 * noise(1000, 6000) * envelope(.168, .012, .24), .16)

    # Successive overlapping surges make the tail roll and collapse in waves.
    for start, strength, pan in ((.23, .24, -.5), (.35, .31, .38),
                                 (.51, .24, -.25), (.71, .17, .6), (.94, .10, -.4)):
        body = .72 * noise(110, 1250) + .28 * noise(650, 4200)
        swell = envelope(start, .034, .19)
        add(strength * body * swell, pan)

    # Small, uneven sheets of water breaking apart; no metallic ringing layer.
    wet = noise(260, 4200)
    for _ in range(46):
        start = .18 + rng.beta(1.1, 2.5) * 1.35
        width = rng.uniform(.009, .033)
        strength = rng.uniform(.024, .065) * np.exp(-max(0, start - .4))
        add(strength * wet * envelope(start, .0018, width), rng.uniform(-.8, .8))

    # Subdued water-cavity resonances add liquid character beneath the surf.
    for _ in range(22):
        start = rng.uniform(.2, 1.18)
        age = np.maximum(t - start, 0)
        frequency = rng.uniform(230, 720)
        phase = 2 * np.pi * frequency * (age + 2.7 * age * age)
        bubble = np.sin(phase) * envelope(start, .001, rng.uniform(.012, .038))
        add(.025 * bubble * np.exp(-start), rng.uniform(-.65, .65))

    # Wide receding wash; independent channels prevent a thin, centered hiss.
    wash = envelope(.36, .06, .34)
    add(.060 * noise(240, 2700) * wash, -.75)
    add(.060 * noise(240, 2700) * wash, .75)
    dry = output.copy()
    for delay, gain in ((.036, .075), (.069, .055), (.117, .03)):
        offset = round(delay * rate)
        output[offset:] += dry[:-offset, ::-1] * gain

    # Gentle peak compression increases body while leaving headroom for the mix.
    output = np.tanh(output * 1.65)
    fade = np.clip(t / .006, 0, 1) * np.clip((duration - t) / .34, 0, 1)
    output *= fade[:, None]
    output *= 10 ** (-1.5 / 20) / max(np.max(np.abs(output)), 1e-9)
    return rate, np.round(output * 32767).astype('<i2')


if __name__ == '__main__':
    destination = Path(sys.argv[1])
    if destination.exists():
        raise SystemExit(f'Refusing to overwrite: {destination}')
    rate, samples = render()
    destination.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(destination), 'wb') as audio:
        audio.setnchannels(2)
        audio.setsampwidth(2)
        audio.setframerate(rate)
        audio.writeframes(samples.tobytes())
    print(f'Created: {destination.resolve()} | {len(samples) / rate:.2f}s | 48kHz stereo PCM16')
