"""Generate four light water-slash variations, preserving all existing WAV files."""
from pathlib import Path
import sys
import wave

from generate_water_slash import render


VARIANTS = [
    ('Sword_Water_Slash_02_Quick.wav',
     dict(seed=71430, time_scale=.82, pitch=1.08, droplets=23, water_gain=.88)),
    ('Sword_Water_Slash_03_Return.wav',
     dict(seed=71431, time_scale=.95, pitch=.91, droplets=28, water_gain=1.0, pan_direction=-1.0)),
    ('Sword_Water_Slash_04_Splash.wav',
     dict(seed=71432, time_scale=1.06, pitch=1.02, droplets=43, water_gain=1.22)),
    ('Sword_Water_Slash_05_Finish.wav',
     dict(seed=71433, time_scale=1.14, pitch=.91, droplets=34, water_gain=1.13)),
]


if __name__ == '__main__':
    folder = Path(sys.argv[1])
    for name, _ in VARIANTS:
        if (folder / name).exists():
            raise SystemExit(f'Refusing to overwrite: {folder / name}')
    folder.mkdir(parents=True, exist_ok=True)
    for name, parameters in VARIANTS:
        rate, samples = render(**parameters)
        destination = folder / name
        with wave.open(str(destination), 'wb') as output:
            output.setnchannels(2)
            output.setsampwidth(2)
            output.setframerate(rate)
            output.writeframes(samples.tobytes())
        print(f'Created: {destination.resolve()} | {len(samples) / rate:.2f}s | 48kHz stereo PCM16')
