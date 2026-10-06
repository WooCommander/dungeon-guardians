"""Cuts the guardian's footsteps out of map-images/dragon-studio-colossal-footsteps-467495.mp3.

The recording is a few colossal stomps followed by seconds of rumble, nearly all of it below 200 Hz. The game needs
single steps that fit the guardian's pace (about 0.43 s per cell), so this takes three separate stomps as variants,
shortens them with a fade-out, adds a little soft saturation so the thud is heard on phone speakers too (they cannot
play the deep bass), and normalises them. Output: Assets/Resources/Audio/guardian_step_1..3.wav, mono, 16 bit.

Blender decodes the MP3 (no ffmpeg needed), so run it with Blender's Python from the repository root:
"C:/Program Files/Blender Foundation/Blender 4.4/blender.exe" -b --factory-startup --python tools/cut_footsteps.py
"""
import os
import wave

import aud
import numpy as np

SOURCE = "map-images/dragon-studio-colossal-footsteps-467495.mp3"
OUTPUT = "Assets/Resources/Audio/guardian_step_{}.wav"

# Start and end of each stomp in seconds, between the dips of the loudness envelope.
STEPS = [(0.02, 0.48), (0.86, 1.14), (1.16, 1.44)]
ATTACK = 0.004
FADE_SHARE = 0.45
DRIVE = 2.2
PEAK = 0.89  # about -1 dB


def main():
    sound = aud.Sound(os.path.abspath(SOURCE))
    rate = int(sound.specs[0])
    samples = sound.data().mean(axis=1)

    for index, (start, end) in enumerate(STEPS, 1):
        step = samples[int(start * rate):int(end * rate)].astype(np.float64)
        length = len(step)

        envelope = np.ones(length)
        attack = int(ATTACK * rate)
        envelope[:attack] = np.linspace(0.0, 1.0, attack)
        fade = int(length * FADE_SHARE)
        envelope[-fade:] = np.linspace(1.0, 0.0, fade) ** 2
        step *= envelope

        # Soft clipping adds overtones of the thud, which small speakers can play.
        step /= np.abs(step).max()
        step = np.tanh(step * DRIVE) / np.tanh(DRIVE)
        step *= PEAK / np.abs(step).max()

        with wave.open(OUTPUT.format(index), "wb") as out:
            out.setnchannels(1)
            out.setsampwidth(2)
            out.setframerate(rate)
            out.writeframes((step * 32767).astype("<i2").tobytes())
        print("written", OUTPUT.format(index), f"{length / rate:.2f} s")


main()
