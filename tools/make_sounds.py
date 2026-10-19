#!/usr/bin/env python3
"""Synthesises AVAAnotherWorld's sounds, ambience and music into ../src/AVAAnotherWorld/Sounds as 16-bit mono WAV.

Nothing is sampled from any game: every effect is built here from sine, triangle, saw and noise voices and a few simple
filters. The aliens speak gibberish through the formant voice in speech.py. The stings and the ending theme are new;
the title theme is Erik Satie's Gymnopedie No. 1 (1888, public domain), played on a synthesised piano. Run it from anywhere:
    python3 tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave

from speech import say


RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "AVAAnotherWorld", "Sounds")
RNG = random.Random(1991)


def write(name, samples, volume=0.7):
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * volume * 32767)) for s in samples))


def voice(kind, phase):
    p = phase % 1.0
    if kind == "square":
        return 1.0 if p < 0.5 else -1.0
    if kind == "tri":
        return 4 * p - 1 if p < 0.5 else 3 - 4 * p
    if kind == "saw":
        return 2 * p - 1
    return math.sin(2 * math.pi * p)


def tone(seconds, f0, f1=None, kind="sine", attack=0.004, release=1.0, vibrato=0.0, vib_rate=6.0, gain=1.0):
    """A note gliding from f0 to f1 (exponentially), with a short attack and a shaped decay."""
    n = int(RATE * seconds)
    f1 = f1 or f0
    out, phase, held = [], 0.0, 0.0
    for i in range(n):
        t = i / RATE
        f = f0 * (f1 / f0) ** (i / max(1, n - 1))
        if vibrato:
            f *= 1 + vibrato * math.sin(2 * math.pi * vib_rate * t)
        phase += f / RATE
        if kind == "noise":
            if int(phase) != int(phase - f / RATE):
                held = RNG.uniform(-1, 1)
            s = held
        else:
            s = voice(kind, phase)
        env = min(1.0, t / attack) * (1 - i / n) ** release
        out.append(s * env * gain)
    return out


def noise(seconds, gain=1.0):
    return [RNG.uniform(-1, 1) * gain for _ in range(int(RATE * seconds))]


def lowpass(samples, cutoff):
    """A one-pole low-pass filter; cutoff may be a number or a function of the time."""
    out, y = [], 0.0
    for i, x in enumerate(samples):
        c = cutoff(i / RATE) if callable(cutoff) else cutoff
        a = 1 - math.exp(-2 * math.pi * c / RATE)
        y += a * (x - y)
        out.append(y)
    return out


def highpass(samples, cutoff):
    low = lowpass(samples, cutoff)
    return [x - l for x, l in zip(samples, low)]


def shape(samples, env):
    """Multiplies by an envelope given as a function of the time."""
    return [s * env(i / RATE) for i, s in enumerate(samples)]


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def cat(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, v in enumerate(p):
            out[i] += v
    return out


def at(seconds, part, total=None):
    """A part placed so far in (padded to the total length)."""
    out = silence(seconds) + part
    if total:
        out = out[: int(total * RATE)] + silence(max(0, total - len(out) / RATE))
    return out


def loop(samples, fade=0.6):
    """Folds the end into the start so the sound loops without a click."""
    n = int(fade * RATE)
    body, tail = samples[:-n], samples[-n:]
    for i in range(n):
        k = i / n
        body[i] = body[i] * k + tail[i] * (1 - k)
    return body


NOTES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(note):
    n = NOTES[note[0]]
    rest = note[1:]
    while rest and rest[0] in "#b":
        n += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 440.0 * 2 ** ((12 * (int(rest) + 1) + n - 69) / 12)


def piano(note, seconds, gain=0.5):
    """A soft piano: a few decaying partials, the upper ones dying faster."""
    f = hz(note)
    n = int(RATE * seconds)
    out = []
    for i in range(n):
        t = i / RATE
        s = (math.sin(2 * math.pi * f * t) * math.exp(-t * 1.1)
             + 0.45 * math.sin(2 * math.pi * 2 * f * t) * math.exp(-t * 2.2)
             + 0.18 * math.sin(2 * math.pi * 3 * f * t) * math.exp(-t * 3.5)
             + 0.08 * math.sin(2 * math.pi * 4.02 * f * t) * math.exp(-t * 5))
        out.append(s * min(1, t / 0.006) * min(1, (n - i) / (0.08 * RATE)) * gain)
    return out


def pad(notes, seconds, gain=0.3, attack=1.0):
    """A slow string pad: detuned saws softened by a low-pass filter."""
    parts = []
    for note in notes:
        f = hz(note)
        parts.append(tone(seconds, f, kind="saw", attack=attack, release=0.3, vibrato=0.003, vib_rate=0.4, gain=gain))
        parts.append(tone(seconds, f * 1.004, kind="saw", attack=attack, release=0.3, gain=gain * 0.8))
    return lowpass(mix(*parts), 1400)


def place(track, start, part):
    i0 = int(start * RATE)
    if len(track) < i0 + len(part):
        track.extend([0.0] * (i0 + len(part) - len(track)))
    for i, v in enumerate(part):
        track[i0 + i] += v


# ───────────────────────────── the title theme ─────────────────────────────


def gymnopedie():
    """Erik Satie, Gymnopedie No. 1 (1888): the opening, lent et douloureux, over its rocking chords."""
    beat = 60 / 70
    track = []
    # Bars of three beats: a low note on the first, the chord on the second (held through the third).
    chords = [("G2", ["B3", "D4", "F#4"]), ("D2", ["A3", "C#4", "F#4"])]
    melody = [
        None, None, None, None,
        [("-", 1), ("F#5", 1), ("A5", 1)], [("G5", 1), ("F#5", 1), ("C#5", 1)], [("B4", 1), ("C#5", 1), ("D5", 1)], [("A4", 3)],
        [("F#4", 3)], [("F#4", 3)], [("F#4", 3)], [("F#4", 3)],
        [("-", 1), ("F#5", 1), ("A5", 1)], [("G5", 1), ("F#5", 1), ("C#5", 1)], [("B4", 1), ("C#5", 1), ("D5", 1)], [("A4", 3)],
        [("C#5", 3)], [("F#5", 3)], [("E5", 3)], [("E5", 3)],
    ]
    held = None
    for bar, notes in enumerate(melody):
        t0 = bar * 3 * beat
        bass, chord = chords[bar % 2]
        place(track, t0, piano(bass, 3 * beat, 0.42))
        for c in chord:
            place(track, t0 + beat, piano(c, 2 * beat, 0.2))
        if not notes:
            continue
        t = t0
        for note, beats in notes:
            if note != "-":
                # A held note tied over the bar line sounds once.
                if not (note == held and beats == 3 and t == t0 and bar > 0 and melody[bar - 1] == [(note, 3)]):
                    length = beats * beat
                    k = bar + 1
                    while k < len(melody) and melody[k] == [(note, 3)] and beats == 3:
                        length += 3 * beat
                        k += 1
                    place(track, t, piano(note, length + 0.4, 0.5))
            held = note
            t += beats * beat
    track += silence(1.5)
    return lowpass(track, 5000)


# ───────────────────────────── stings and the ending ─────────────────────────────


def arrival():
    """Under the water, then up: a low swell and a high glassy figure."""
    low = pad(["D2", "A2", "D3"], 4.5, 0.25, attack=1.5)
    chimes = []
    for i, n in enumerate(["A5", "E6", "D6", "A5", "F#6"]):
        chimes = mix(chimes or [0.0], at(0.6 + i * 0.55, tone(1.6, hz(n), kind="sine", attack=0.01, release=2.5, gain=0.25)))
    return mix(low, chimes)


def captured():
    """Caught: hard low stabs, a falling dissonance."""
    out = []
    for i, (a, b) in enumerate([("C2", "F#2"), ("C2", "F#2"), ("B1", "F2")]):
        stab = mix(tone(0.5 if i < 2 else 2.4, hz(a), kind="saw", attack=0.005, release=1.6, gain=0.5),
                   tone(0.5 if i < 2 else 2.4, hz(b), kind="saw", attack=0.005, release=1.6, gain=0.4))
        out = mix(out or [0.0], at(i * 0.45, lowpass(stab, 1800)))
    high = tone(2.8, hz("C#6"), hz("C5"), kind="tri", attack=0.4, release=1.2, vibrato=0.01, gain=0.18)
    return mix(out, at(0.9, high))


def freed():
    """Out of the cage: a rising, hopeful figure over a held chord."""
    under = pad(["D3", "A3", "F#4"], 4.0, 0.2, attack=0.6)
    line = []
    for i, n in enumerate(["D4", "F#4", "A4", "D5", "E5", "F#5"]):
        line = mix(line or [0.0], at(i * 0.32, piano(n, 1.6, 0.4)))
    return mix(under, line)


def reunion():
    """The friend again: three warm notes and a chord."""
    under = pad(["G3", "B3", "D4"], 4.2, 0.2, attack=0.8)
    line = []
    for i, n in enumerate(["D5", "B4", "G5"]):
        line = mix(line or [0.0], at(i * 0.55, piano(n, 2.2, 0.45)))
    return mix(under, line)


def ending():
    """The flight: an original theme, a slow melody over long chords, ending on the open fifth."""
    beat = 60 / 76
    track = []
    chords = [["D3", "A3", "F#4"], ["B2", "F#3", "D4"], ["G2", "D3", "B3"], ["A2", "E3", "C#4"],
              ["D3", "A3", "F#4"], ["B2", "F#3", "D4"], ["G2", "D3", "B3"], ["A2", "E3", "A3"]]
    for i, ch in enumerate(chords):
        place(track, i * 4 * beat, pad(ch, 4 * beat + 0.8, 0.14, attack=1.2))
    tune = [("F#5", 2), ("E5", 1), ("D5", 1), ("A5", 3), ("F#5", 1), ("G5", 2), ("F#5", 1), ("E5", 1), ("C#5", 4),
            ("D5", 2), ("E5", 1), ("F#5", 1), ("B5", 3), ("A5", 1), ("G5", 1), ("F#5", 1), ("E5", 1), ("D5", 1), ("E5", 4)]
    t = 0.0
    for note, beats in tune:
        place(track, t, piano(note, beats * beat + 0.6, 0.38))
        t += beats * beat
    place(track, 32 * beat, pad(["D3", "A3", "D4", "A4"], 5.0, 0.16, attack=0.4))
    place(track, 32 * beat, piano("D5", 4.5, 0.4))
    return lowpass(track, 4500)


def death():
    """The death sting: a falling, dissonant chord."""
    return mix(tone(1.6, hz("E4"), hz("E3"), kind="saw", attack=0.005, release=1.5, gain=0.3),
               tone(1.6, hz("A#4"), hz("A#3"), kind="saw", attack=0.005, release=1.5, gain=0.25),
               tone(0.5, 120, 40, kind="sine", attack=0.002, release=1.0, gain=0.6))


# ───────────────────────────── ambience ─────────────────────────────


def rain():
    hiss = lowpass(highpass(noise(8.6, 0.6), 400), 5000)
    patter = silence(8.6)
    for _ in range(500):
        place(patter, RNG.uniform(0, 8.3), tone(0.03, RNG.uniform(1500, 4000), kind="noise", attack=0.001, release=2, gain=RNG.uniform(0.1, 0.4)))
    rumble = lowpass(noise(8.6, 0.8), 60)
    return loop(mix(hiss, patter, rumble))


def wind():
    gust = lambda t: 300 + 260 * (0.5 + 0.5 * math.sin(2 * math.pi * t / 4.3) * math.sin(2 * math.pi * t / 2.9 + 1))
    w = lowpass(lowpass(noise(10.6, 1.0), gust), 900)
    w = shape(w, lambda t: 0.6 + 0.4 * math.sin(2 * math.pi * t / 5.3))
    drone = tone(10.6, hz("D2"), kind="sine", attack=2, release=0.01, gain=0.08)
    return loop(mix(w, drone))


def cave():
    air = lowpass(noise(10.6, 0.5), 180)
    drips = silence(10.6)
    for i in range(9):
        f = RNG.uniform(900, 1800)
        place(drips, RNG.uniform(0, 10), mix(tone(0.25, f, f * 1.6, kind="sine", attack=0.001, release=3, gain=0.35),
                                            at(0.08, tone(0.4, f * 0.8, kind="sine", attack=0.01, release=4, gain=0.12))))
    drone = mix(tone(10.6, hz("A1"), kind="sine", attack=2, release=0.01, gain=0.16), tone(10.6, hz("E2"), kind="sine", attack=3, release=0.01, gain=0.08))
    return loop(mix(air, drips, drone))


def machine():
    hum = mix(*[tone(8.6, 55 * k, kind="sine", attack=0.5, release=0.01, gain=0.3 / k) for k in (1, 2, 3, 5)])
    rattle = shape(lowpass(noise(8.6, 0.3), 1200), lambda t: 0.5 + 0.5 * (1 if (t * 6.0) % 1 < 0.3 else 0))
    whine = tone(8.6, 880, kind="sine", attack=1, release=0.01, vibrato=0.01, vib_rate=0.25, gain=0.03)
    return loop(mix(hum, rattle, whine))


def hall():
    drone = mix(tone(10.6, hz("C2"), kind="saw", attack=2, release=0.01, gain=0.08), tone(10.6, hz("G2"), kind="saw", attack=3, release=0.01, gain=0.05))
    air = lowpass(noise(10.6, 0.4), 250)
    return loop(mix(lowpass(drone, 400), air))


# ───────────────────────────── effects ─────────────────────────────


def thunder():
    crack = lowpass(noise(0.3, 1.0), 3000)
    roll = shape(lowpass(noise(3.5, 1.0), lambda t: 400 * math.exp(-t * 0.8) + 60), lambda t: math.exp(-t * 0.7) * (0.6 + 0.4 * math.sin(t * 9) ** 2))
    return mix(shape(crack, lambda t: math.exp(-t * 14)), at(0.08, roll))


def roar():
    growl = tone(1.4, 95, 60, kind="saw", attack=0.05, release=0.8, vibrato=0.06, vib_rate=23, gain=0.6)
    rasp = lowpass(shape(noise(1.4), lambda t: min(1, t * 8) * (1 - t / 1.4)), 1200)
    return mix(lowpass(growl, 900), rasp)


def main():
    os.makedirs(OUT, exist_ok=True)
    for f in os.listdir(OUT):
        if f.endswith(".wav"):
            os.remove(os.path.join(OUT, f))
    # Moving.
    write("Step.wav", lowpass(tone(0.06, 900, 300, kind="noise", attack=0.002, release=2.0), 2000), 0.18)
    write("Jump.wav", mix(lowpass(tone(0.1, 1200, 600, kind="noise", release=1.5, gain=0.4), 3000), tone(0.08, 180, 260, kind="sine", gain=0.4)), 0.2)
    write("Land.wav", mix(tone(0.12, 120, 60, kind="sine", attack=0.001), lowpass(tone(0.1, 800, 200, kind="noise", gain=0.6), 1500)), 0.3)
    write("Climb.wav", cat(lowpass(tone(0.12, 600, 300, kind="noise", gain=0.5), 2000), silence(0.12), lowpass(tone(0.12, 700, 300, kind="noise", gain=0.5), 2000)), 0.2)
    write("Grab.wav", lowpass(tone(0.12, 1400, 500, kind="noise", attack=0.001), 2500), 0.25)
    write("Kick.wav", mix(lowpass(tone(0.12, 1800, 400, kind="noise"), 3000), tone(0.08, 160, 90, kind="sine")), 0.3)
    # The water.
    splash = mix(lowpass(shape(noise(0.7), lambda t: math.exp(-t * 6)), lambda t: 3000 * math.exp(-t * 3) + 300),
                 *[at(RNG.uniform(0.05, 0.4), tone(0.08, f, f * 1.5, kind="sine", attack=0.001, release=3, gain=0.25)) for f in (700, 900, 1200, 1500)])
    write("Splash.wav", splash, 0.45)
    write("Gasp.wav", say("HH AA1 HH", rate=RATE, pitch=140, speed=1.2, size=1.0), 0.4)
    write("Tentacle.wav", mix(lowpass(shape(noise(1.0), lambda t: math.sin(math.pi * t)), 500), tone(1.0, 70, 40, kind="saw", attack=0.1, release=1, gain=0.4)), 0.4)
    # Creatures.
    write("Squish.wav", lowpass(mix(tone(0.2, 400, 120, kind="tri", attack=0.001), shape(noise(0.2, 0.6), lambda t: math.exp(-t * 20))), 1500), 0.32)
    write("Bite.wav", mix(tone(0.15, 900, 300, kind="saw", attack=0.001, release=2, gain=0.4), lowpass(shape(noise(0.25), lambda t: math.exp(-t * 15)), 2000)), 0.4)
    write("Plop.wav", tone(0.12, 300, 900, kind="sine", attack=0.001, release=2), 0.3)
    write("Roar.wav", roar(), 0.55)
    write("Thud.wav", mix(tone(0.5, 80, 35, kind="sine", attack=0.002), lowpass(shape(noise(0.4), lambda t: math.exp(-t * 10)), 400)), 0.5)
    # The pistol.
    write("Shot.wav", mix(tone(0.22, 2400, 300, kind="saw", attack=0.001, release=1.4, gain=0.5), tone(0.22, 1200, 150, kind="sine", attack=0.001, release=1.2, gain=0.5)), 0.32)
    write("Charge.wav", mix(tone(1.3, 180, 900, kind="saw", attack=0.05, release=0.2, vibrato=0.03, vib_rate=14, gain=0.25), tone(1.3, 360, 1800, kind="sine", attack=0.1, release=0.2, gain=0.2)), 0.22)
    write("Shield.wav", mix(tone(0.6, 300, 1400, kind="tri", attack=0.01, release=1.0, vibrato=0.05, vib_rate=30, gain=0.4), lowpass(shape(noise(0.6), lambda t: math.exp(-t * 6)), 4000)), 0.3)
    write("Block.wav", mix(tone(0.25, 1800, 900, kind="square", attack=0.001, release=2, gain=0.25), highpass(shape(noise(0.25), lambda t: math.exp(-t * 18)), 2000)), 0.3)
    write("Super.wav", mix(tone(0.6, 1600, 80, kind="saw", attack=0.002, release=1.0, gain=0.5), lowpass(shape(noise(0.6), lambda t: math.exp(-t * 5)), 2500), tone(0.6, 60, 40, kind="sine", gain=0.6)), 0.5)
    write("Break.wav", mix(highpass(shape(noise(0.5), lambda t: math.exp(-t * 7)), 1500), *[at(i * 0.04, tone(0.2, 2000 + i * 400, kind="sine", attack=0.001, release=3, gain=0.2)) for i in range(5)]), 0.36)
    write("Zap.wav", mix(tone(0.4, 3000, 200, kind="square", attack=0.001, release=1.2, gain=0.3), highpass(shape(noise(0.4), lambda t: math.exp(-t * 9)), 1000)), 0.4)
    # Voices: the aliens speak a tongue of their own; the physicist only cries out.
    write("Shout.wav", say("K AA1 R , V AA1 Z EH K !", rate=RATE, pitch=80, speed=1.1, size=1.35, seed=3), 0.45)
    write("Cry.wav", say("AA1 AA !", rate=RATE, pitch=95, speed=0.9, size=1.35, bend=lambda t: 1.1 - 0.5 * t, seed=4), 0.45)
    write("Oru.wav", say("OW1 R UW , M EY1 N AA , L IY1 Z AA !", rate=RATE, pitch=100, speed=0.9, size=1.3, seed=5), 0.45)
    write("Die.wav", mix(death(), say("AH1 !", rate=RATE, pitch=150, speed=1.0, size=1.0, bend=lambda t: 1.0 - 0.3 * t)), 0.5)
    # The cage, the door, the switch, the barriers, the drips.
    write("Creak.wav", lowpass(tone(0.5, 300, 380, kind="saw", attack=0.05, release=1.0, vibrato=0.08, vib_rate=37, gain=0.4), 1500), 0.22)
    write("Snap.wav", mix(highpass(shape(noise(0.15), lambda t: math.exp(-t * 30)), 2500), tone(0.3, 1500, 400, kind="square", attack=0.001, release=2, gain=0.3)), 0.45)
    write("Crash.wav", mix(lowpass(shape(noise(1.4), lambda t: math.exp(-t * 3)), 1600), *[at(RNG.uniform(0, 0.3), tone(0.6, f, kind="sine", attack=0.001, release=3, gain=0.2)) for f in (420, 610, 830, 1170)], tone(0.6, 70, 35, kind="sine")), 0.55)
    write("Door.wav", mix(tone(1.4, 60, 90, kind="saw", attack=0.1, release=0.5, gain=0.4), lowpass(noise(1.4, 0.4), 500), at(1.2, tone(0.3, 120, 60, kind="sine"))), 0.4)
    write("Work.wav", cat(*[tone(0.07, f, kind="square", attack=0.002, release=1.0, gain=0.3) + silence(0.05) for f in (880, 1320, 990, 1760)]), 0.2)
    write("Switch.wav", mix(tone(0.08, 2000, kind="square", attack=0.001, gain=0.3), at(0.1, tone(0.4, 600, 200, kind="saw", attack=0.002, release=1.2, gain=0.3))), 0.3)
    write("Buzz.wav", mix(tone(0.5, 120, kind="saw", attack=0.002, release=0.8, gain=0.4), highpass(shape(noise(0.5), lambda t: math.exp(-t * 4)), 2000)), 0.38)
    write("Drip.wav", tone(0.2, 1400, 2400, kind="sine", attack=0.001, release=3), 0.16)
    write("Fizz.wav", mix(highpass(shape(noise(0.8), lambda t: math.exp(-t * 3)), 3000), say("AA1 !", rate=RATE, pitch=150, speed=1.2)), 0.45)
    # The night of the storm.
    write("Thunder.wav", thunder(), 0.7)
    write("Surge.wav", mix(tone(2.4, 60, 2400, kind="saw", attack=0.3, release=0.4, vibrato=0.02, vib_rate=11, gain=0.4), lowpass(shape(noise(2.4), lambda t: t / 2.4), 3000)), 0.42)
    write("Typing.wav", cat(*[lowpass(tone(0.025, 3000, 1000, kind="noise", attack=0.001, release=2), 4000) + silence(RNG.uniform(0.05, 0.16)) for _ in range(9)]), 0.25)
    write("Wings.wav", cat(*[lowpass(shape(noise(0.35), lambda t: math.sin(math.pi * t / 0.35)), 500) + silence(0.15) for _ in range(3)]), 0.4)
    # Ambience loops.
    write("Rain.wav", rain(), 0.35)
    write("Wind.wav", wind(), 0.35)
    write("Cave.wav", cave(), 0.35)
    write("Machine.wav", machine(), 0.3)
    write("Hall.wav", hall(), 0.3)
    # Music.
    write("TitleTheme.wav", gymnopedie(), 0.4)
    write("Arrival.wav", arrival(), 0.4)
    write("Captured.wav", captured(), 0.45)
    write("Freed.wav", freed(), 0.42)
    write("Reunion.wav", reunion(), 0.42)
    write("Ending.wav", ending(), 0.42)


if __name__ == "__main__":
    main()
