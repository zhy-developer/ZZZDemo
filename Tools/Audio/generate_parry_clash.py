"""Original steel-on-steel parry: compact impact, inharmonic blade ring, short sparks.

Generates 48 kHz stereo PCM16 without source recordings; never plays or overwrites audio.
"""
from pathlib import Path
import sys
import wave

import numpy as np


def render():
    rate, duration = 48000, 1.12
    count = round(rate * duration)
    t = np.arange(count) / rate
    rng = np.random.default_rng(171026)
    mix = np.zeros((count, 2), dtype=np.float64)

    def noise(low, high):
        hz = np.fft.rfftfreq(count, 1 / rate)
        hp = (hz / low) ** 3
        shape = hp / (1 + hp) / (1 + (hz / high) ** 6)
        signal = np.fft.irfft(np.fft.rfft(rng.normal(size=count)) * shape, n=count)
        return signal / max(np.std(signal), 1e-9)

    def env(start, attack, decay):
        age = np.maximum(t - start, 0)
        return (t >= start) * (1 - np.exp(-age / attack)) * np.exp(-age / decay)

    def add(signal, pan=0):
        mix[:, 0] += signal * np.sqrt((1 - pan) / 2)
        mix[:, 1] += signal * np.sqrt((1 + pan) / 2)

    # Immediate dry strike, with a low steel body rather than a gunshot or explosion.
    add(.40 * noise(1800, 11000) * env(.002, .00035, .009))
    add(.24 * noise(170, 1600) * env(.003, .0007, .023))
    add(.10 * np.sin(2 * np.pi * (205 * t - 260 * t * t)) * env(.003, .001, .026))

    # Two differently tuned blades. Non-harmonic partials avoid a musical bell/chime.
    modes = [(780, .12, .105), (1317, .115, .16), (2179, .095, .23),
             (3371, .062, .18), (4873, .032, .11), (7169, .012, .065)]
    for blade, detune, pan in ((0, 1, -.2), (1, 1.087, .2)):
        onset = .003 + blade * .0015
        age = np.maximum(t - onset, 0)
        for frequency, gain, tail in modes:
            phase = 2 * np.pi * frequency * detune * age
            phase += .045 * np.sin(2 * np.pi * 37 * age) * np.exp(-age * 25)
            add(gain * np.sin(phase) * env(onset, .0006, tail), pan)

    # Brief blade slide and a few light contacts; no sustained hiss or giant reverb tail.
    add(.09 * noise(1400, 6200) * env(.014, .002, .039) * (.7 + .3 * np.sin(2 * np.pi * 91 * t)), -.08)
    for start, gain, pan in ((.035, .075, -.3), (.060, .048, .32), (.091, .026, -.12)):
        add(gain * noise(2600, 9500) * env(start, .0005, .006), pan)
    dry = mix.copy()
    for delay, gain in ((.021, .055), (.043, .035), (.079, .018)):
        offset = round(delay * rate)
        mix[offset:] += dry[:-offset, ::-1] * gain

    mix -= mix.mean(axis=0)
    mix = np.tanh(mix * 1.25)
    mix *= (np.clip(t / .001, 0, 1) * np.clip((duration - t) / .16, 0, 1))[:, None]
    mix *= 10 ** (-1.5 / 20) / max(np.max(np.abs(mix)), 1e-9)
    return rate, np.round(mix * 32767).astype('<i2')


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
    print(f'Created {destination}: {len(samples) / rate:.2f}s, 48 kHz stereo PCM16')
