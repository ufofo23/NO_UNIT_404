#!/usr/bin/env python3
"""
NO UNIT 404 - audio cue generator (GDD 19).

Renders every AudioCue in Core/AudioService.cs to a 44.1 kHz 16-bit WAV under
Assets/_Project/Resources/NO404/Audio/. The file names must match the enum
members exactly: AudioService resolves a cue with
Resources.Load("NO404/Audio/" + cue).

This is synthesis, not field recording. It exists so the game ships with a
coherent sound bed instead of silence, and so the audio pass has a reference to
replace file by file. Dropping a real recording over any file here needs no
code change.

Usage:  python Tools/GenerateAudio.py [--out <dir>] [--seed N]
"""

import argparse
import os

import numpy as np
from scipy.io import wavfile
from scipy.signal import lfilter

SR = 44100
TWO_PI = 2.0 * np.pi


# ---------------------------------------------------------------- primitives

def t(seconds):
    """Sample-time vector."""
    return np.arange(int(round(seconds * SR))) / float(SR)


def silence(seconds):
    """A buffer that is exactly as long as t(seconds). Both must round the same
    way or a mix of the two is off by a sample."""
    return np.zeros(len(t(seconds)))


def white(seconds, rng):
    return rng.standard_normal(int(round(seconds * SR)))


def pink(seconds, rng):
    """Paul Kellett's pink filter, as a 4th order IIR."""
    b = [0.049922035, -0.095993537, 0.050612699, -0.004408786]
    a = [1.0, -2.494956002, 2.017265875, -0.522189400]
    return lfilter(b, a, white(seconds, rng))


def square(seconds, hz, duty=0.5):
    return np.where((t(seconds) * hz) % 1.0 < duty, 1.0, -1.0)


# ------------------------------------------------------------------ filters

def _biquad(x, b0, b1, b2, a0, a1, a2):
    return lfilter([b0 / a0, b1 / a0, b2 / a0], [1.0, a1 / a0, a2 / a0], x)


def lowpass(x, hz, q=0.707):
    w = TWO_PI * hz / SR
    alpha = np.sin(w) / (2.0 * q)
    cos_w = np.cos(w)
    b0 = (1.0 - cos_w) / 2.0
    return _biquad(x, b0, 1.0 - cos_w, b0, 1.0 + alpha, -2.0 * cos_w, 1.0 - alpha)


def highpass(x, hz, q=0.707):
    w = TWO_PI * hz / SR
    alpha = np.sin(w) / (2.0 * q)
    cos_w = np.cos(w)
    b0 = (1.0 + cos_w) / 2.0
    return _biquad(x, b0, -(1.0 + cos_w), b0, 1.0 + alpha, -2.0 * cos_w, 1.0 - alpha)


def bandpass(x, hz, q=4.0):
    w = TWO_PI * hz / SR
    alpha = np.sin(w) / (2.0 * q)
    cos_w = np.cos(w)
    return _biquad(x, alpha, 0.0, -alpha, 1.0 + alpha, -2.0 * cos_w, 1.0 - alpha)


# ----------------------------------------------------------------- envelopes

def perc(seconds, attack=0.002, tau=0.15):
    """Fast attack, exponential tail - the shape of anything struck."""
    env = np.exp(-t(seconds) / tau)
    n_attack = max(1, int(attack * SR))
    env[:n_attack] *= np.linspace(0.0, 1.0, n_attack)
    return env


def fade(x, seconds=0.01):
    """Edge fade so nothing clicks at the boundaries."""
    n = min(int(seconds * SR), len(x) // 2)
    if n <= 0:
        return x
    out = x.copy()
    out[:n] *= np.linspace(0.0, 1.0, n)
    out[-n:] *= np.linspace(1.0, 0.0, n)
    return out


# ------------------------------------------------------------------ modeling

def modal(seconds, partials, rng=None):
    """
    Struck-object synthesis. partials is a list of (hz, gain, tau). A random
    start phase per partial keeps a stack of sines from sounding like an organ.
    """
    out = np.zeros(int(round(seconds * SR)))
    tt = t(seconds)
    for hz, gain, tau in partials:
        phase = rng.uniform(0, TWO_PI) if rng is not None else 0.0
        out += gain * np.sin(TWO_PI * hz * tt + phase) * np.exp(-tt / tau)
    return out


def comb(x, delay_s, g):
    d = max(1, int(delay_s * SR))
    a = np.zeros(d + 1)
    a[0] = 1.0
    a[d] = -g
    return lfilter([1.0], a, x)


def allpass(x, delay_s, g):
    d = max(1, int(delay_s * SR))
    b = np.zeros(d + 1)
    b[0] = -g
    b[d] = 1.0
    a = np.zeros(d + 1)
    a[0] = 1.0
    a[d] = -g
    return lfilter(b, a, x)


def reverb(x, mix=0.3, size=1.0):
    """Schroeder plate. Enough room for a stairwell, not a cathedral."""
    wet = np.zeros(len(x))
    for delay, g in ((0.0297, 0.805), (0.0371, 0.827), (0.0411, 0.783), (0.0437, 0.764)):
        wet += comb(x, delay * size, g)
    wet /= 4.0
    wet = allpass(wet, 0.005 * size, 0.7)
    wet = allpass(wet, 0.0017 * size, 0.7)
    return (1.0 - mix) * x + mix * wet


# -------------------------------------------------------------------- levels

def peak_norm(x, db=-6.0):
    p = np.max(np.abs(x))
    if p < 1e-9:
        return x
    return x / p * (10.0 ** (db / 20.0))


def rms_norm(x, db=-34.0):
    r = np.sqrt(np.mean(x ** 2))
    if r < 1e-9:
        return x
    return x * (10.0 ** (db / 20.0)) / r


def loopify(x, crossfade=1.5):
    """
    Equal-power crossfade of the tail over the head, then trim. What comes back
    is seamless when Unity loops it, which is the whole job of a room tone.
    """
    n = int(crossfade * SR)
    if len(x) <= 2 * n:
        return fade(x)
    head, tail, body = x[:n], x[-n:], x[n:-n]
    ramp = np.linspace(0.0, 1.0, n)
    blended = head * np.sqrt(ramp) + tail * np.sqrt(1.0 - ramp)
    return np.concatenate([blended, body])


def place(buffer, x, at_seconds, gain=1.0):
    """
    Mix x into buffer at a time offset, clipped to the buffer at both ends. A
    negative offset is allowed - jittered placements ask for one - and trims the
    head of x rather than wrapping around.
    """
    start = int(at_seconds * SR)
    if start >= len(buffer):
        return
    if start < 0:
        x = x[-start:]
        start = 0
    if len(x) == 0:
        return
    end = min(len(buffer), start + len(x))
    buffer[start:end] += x[:end - start] * gain


# ============================================================== the cue bank
# One function per AudioCue member; the dict key in build_all is the enum name.

def anomaly_start(rng):
    """A channel has just started lying: a sub that drops under a reversed swell."""
    dur = 2.2
    out = silence(dur)
    tt = t(dur)

    # Sub drop 58 -> 27 Hz. The pitch bend is what makes it read as wrong.
    hz = 58.0 * np.exp(-tt / 0.9) + 27.0
    out += np.sin(TWO_PI * np.cumsum(hz) / SR) * np.exp(-tt / 0.7) * 0.9

    # Reversed noise swell into the transient - built forwards, then flipped.
    swell = pink(1.0, rng) * np.linspace(0.0, 1.0, int(1.0 * SR)) ** 3
    swell = bandpass(swell, 900.0, q=1.2)[::-1]
    place(out, swell, 0.0, 0.5)

    # A high shimmer that detunes as it fades - the tape-wobble tell.
    wobble = 1.0 + 0.004 * np.sin(TWO_PI * 5.5 * tt)
    out += np.sin(TWO_PI * 2340.0 * np.cumsum(wobble) / SR) * np.exp(-tt / 0.35) * 0.10
    out += np.sin(TWO_PI * 3130.0 * np.cumsum(wobble) / SR) * np.exp(-tt / 0.22) * 0.06

    return peak_norm(fade(reverb(out, mix=0.22)), -7.0)


def interphone(rng):
    """The front-door intercom. A flat electronic two-tone, deliberately cheap."""
    dur = 1.5
    out = silence(dur)
    for hz, at in ((784.0, 0.0), (588.0, 0.42)):
        beep = square(0.34, hz, duty=0.45) * 0.5 + square(0.34, hz * 2.0) * 0.12
        beep = lowpass(beep, 3200.0)
        place(out, beep * perc(0.34, attack=0.006, tau=0.30), at)
    return peak_norm(fade(out), -8.0)


def interphone_self_echo(office_tone):
    """
    GDD 2.3 hook #5 - the handset carries the office's own room tone back.
    Built from the office loop so it is literally the same air, combed and
    delayed just enough that the player hears the room they are standing in.
    """
    src = office_tone[:int(2.4 * SR)].copy()
    src = bandpass(src, 1100.0, q=0.8) * 6.0        # telephone band
    combed = comb(src, 0.037, 0.72)
    combed = highpass(lowpass(combed, 3400.0), 320.0)

    env = np.ones(len(combed))
    n_in = int(0.25 * SR)
    n_out = int(0.6 * SR)
    env[:n_in] = np.linspace(0.0, 1.0, n_in)
    env[-n_out:] = np.linspace(1.0, 0.0, n_out)
    return peak_norm(combed * env, -12.0)


def phone_ring(rng):
    """Korean landline: a bell-toned double ring."""
    dur = 3.0
    out = silence(dur)
    for at in (0.0, 1.55):
        tone = np.sin(TWO_PI * 480.0 * t(1.1)) + np.sin(TWO_PI * 620.0 * t(1.1)) * 0.7
        tremolo = 0.55 + 0.45 * np.sin(TWO_PI * 22.0 * t(1.1))
        place(out, fade(tone * tremolo, 0.03) * 0.5, at)
    return peak_norm(reverb(out, mix=0.15), -9.0)


def footsteps(rng):
    """Three steps on a concrete corridor - heel transient plus the floor under it."""
    dur = 1.35
    out = silence(dur)
    for at in (0.0, 0.44, 0.90):
        heel = highpass(white(0.09, rng), 1400.0) * perc(0.09, attack=0.001, tau=0.018)
        body = modal(0.22, [(96.0, 0.5, 0.05), (152.0, 0.25, 0.035)], rng) * perc(0.22, tau=0.05)
        scuff = bandpass(white(0.13, rng), 2600.0, q=1.5) * perc(0.13, tau=0.035) * 0.35
        step = silence(0.22)
        place(step, heel, 0.0, 0.6)
        place(step, body, 0.0)
        place(step, scuff, 0.0)
        place(out, step, at + rng.uniform(-0.02, 0.02), rng.uniform(0.82, 1.0))
    return peak_norm(reverb(out, mix=0.26, size=1.3), -10.0)


def footsteps_distant(rng):
    """
    The same three steps heard through a floor slab (v2.1 spec 0.10.4).

    Built from the near cue rather than as its own sound, because it has to read as the
    same event: low-passed hard, quieter, and given a longer room so it arrives from
    somewhere the caretaker is not.
    """
    near = footsteps(rng)
    muffled = lowpass(near, 420.0)
    return peak_norm(reverb(muffled, mix=0.55, size=2.6), -22.0)


def door_open(rng):
    """Latch, hinge, and the swing settling."""
    dur = 1.6
    out = silence(dur)

    latch = highpass(white(0.05, rng), 2200.0) * perc(0.05, attack=0.0005, tau=0.010)
    place(out, latch, 0.0, 0.7)

    # Hinge: noise dragged through stacked resonators. Stick-slip, not a tone.
    n = int(0.75 * SR)
    creak_src = white(0.75, rng)
    creak = np.zeros(n)
    for hz in (420.0, 510.0, 600.0, 690.0, 780.0):
        creak += bandpass(creak_src, hz, q=14.0)
    creak *= (0.5 + 0.5 * np.sin(TWO_PI * 7.0 * t(0.75))) * np.linspace(1.0, 0.2, n)
    place(out, creak, 0.06, 0.5)

    thump = modal(0.4, [(74.0, 0.6, 0.09), (128.0, 0.2, 0.05)], rng) * perc(0.4, tau=0.08)
    place(out, thump, 1.05, 0.8)
    return peak_norm(reverb(out, mix=0.24), -9.0)


def evidence_taken(rng):
    """Paper lifted off a desk, then a soft confirm."""
    dur = 0.55
    out = silence(dur)
    rustle = highpass(white(0.26, rng), 2800.0) * perc(0.26, attack=0.01, tau=0.09)
    place(out, rustle, 0.0, 0.55)
    click = np.sin(TWO_PI * 1180.0 * t(0.10)) * perc(0.10, attack=0.001, tau=0.035)
    place(out, click, 0.20, 0.35)
    return peak_norm(fade(out), -12.0)


def report_filed(rng):
    """Two notes up. The only unambiguously good news the game gives."""
    dur = 0.5
    out = silence(dur)
    for hz, at in ((784.0, 0.0), (1046.0, 0.11)):
        note = np.sin(TWO_PI * hz * t(0.30)) + 0.2 * np.sin(TWO_PI * hz * 2.0 * t(0.30))
        place(out, note * perc(0.30, attack=0.004, tau=0.10), at, 0.5)
    return peak_norm(fade(out), -11.0)


def report_rejected(rng):
    """Low, flat, short. No drama - it is a form coming back."""
    buzz = lowpass(square(0.26, 146.0) * perc(0.26, attack=0.004, tau=0.09), 900.0)
    out = silence(0.42)
    place(out, buzz, 0.0)
    return peak_norm(fade(out), -11.0)


def fire_alarm(rng):
    """Korean fire bell: a clapper hitting a steel dome, fast and hard."""
    dur = 2.4
    out = silence(dur)
    partials = [(1180.0, 0.55, 0.30), (1795.0, 0.42, 0.24), (2410.0, 0.30, 0.18),
                (3260.0, 0.20, 0.13), (4520.0, 0.12, 0.09), (620.0, 0.25, 0.35)]
    at = 0.0
    while at < dur - 0.15:
        strike = modal(0.36, partials, rng) * perc(0.36, attack=0.0008, tau=0.10)
        place(strike, highpass(white(0.02, rng), 4000.0) * perc(0.02, tau=0.004) * 0.4, 0.0)
        place(out, strike, at, rng.uniform(0.9, 1.0))
        at += 0.115
    return peak_norm(reverb(out, mix=0.3, size=1.4), -3.5)


def office_door_knock(rng):
    """
    Three knuckles on the steel office door (GDD 15.6). One of the two sounds the
    player hears without having asked for anything, so it is dry and close.
    """
    dur = 1.5
    out = silence(dur)
    for at in (0.0, 0.30, 0.585):
        knock = modal(0.5, [(88.0, 0.85, 0.055), (196.0, 0.40, 0.035),
                            (412.0, 0.22, 0.022), (770.0, 0.10, 0.014)], rng)
        knock *= perc(0.5, attack=0.0006, tau=0.055)
        place(knock, highpass(white(0.03, rng), 2600.0) * perc(0.03, tau=0.006) * 0.30, 0.0)
        place(out, knock, at + rng.uniform(-0.008, 0.008), rng.uniform(0.85, 1.0))
    return peak_norm(reverb(out, mix=0.14), -5.0)


def office_door_handle(rng):
    """Someone trying the handle. Metal on metal, with no rhythm to it."""
    dur = 1.9
    out = silence(dur)
    at = 0.05
    while at < 1.6:
        rattle = modal(0.16, [(1420.0, 0.4, 0.020), (2180.0, 0.28, 0.014),
                              (3350.0, 0.16, 0.009), (520.0, 0.3, 0.030)], rng)
        rattle *= perc(0.16, attack=0.0004, tau=0.022)
        place(out, rattle, at, rng.uniform(0.4, 1.0))
        at += rng.uniform(0.07, 0.26)

    # The spring in the latch, complaining.
    squeak = bandpass(white(0.5, rng), 1850.0, q=22.0) * perc(0.5, attack=0.05, tau=0.18)
    place(out, squeak, 0.55, 0.5)
    return peak_norm(reverb(out, mix=0.18), -8.0)


def office_door_forced(rng):
    """Bodyweight against the door. Low, wide, and it does not resolve."""
    dur = 2.6
    out = silence(dur)
    for at, gain in ((0.0, 1.0), (0.62, 0.92), (1.18, 1.0)):
        hit = modal(0.9, [(44.0, 1.0, 0.16), (67.0, 0.6, 0.11),
                          (152.0, 0.35, 0.06), (310.0, 0.18, 0.035)], rng)
        hit *= perc(0.9, attack=0.001, tau=0.13)
        place(hit, bandpass(white(0.35, rng), 780.0, q=2.0) * perc(0.35, tau=0.06) * 0.35, 0.0)
        place(hit, modal(0.6, [(2100.0, 0.10, 0.05), (2960.0, 0.07, 0.03)], rng)
              * perc(0.6, tau=0.07), 0.0)
        place(out, hit, at, gain)
    return peak_norm(reverb(out, mix=0.26, size=1.2), -3.5)


def power_warning(rng):
    """The reserve is going (GDD 15.5). Three beeps falling, and the hum sags."""
    dur = 2.3
    out = silence(dur)
    for hz, at in ((880.0, 0.0), (784.0, 0.45), (622.0, 0.90)):
        beep = square(0.22, hz) * 0.45 + np.sin(TWO_PI * hz * t(0.22)) * 0.35
        place(out, lowpass(beep, 4000.0) * perc(0.22, attack=0.004, tau=0.10), at)

    tt = t(dur)
    sag = np.interp(tt, [0.0, 1.0, 1.9, dur], [1.0, 1.0, 0.55, 0.35])
    hum = np.sin(TWO_PI * 60.0 * np.cumsum(sag) / SR) * 0.35
    hum += np.sin(TWO_PI * 120.0 * np.cumsum(sag) / SR) * 0.12
    out += hum * np.linspace(1.0, 0.4, len(tt))
    return peak_norm(fade(out), -8.0)


def breaker_reset(rng):
    """A real contactor: the lever, the clunk, then the building coming back."""
    dur = 2.2
    out = silence(dur)

    place(out, highpass(white(0.04, rng), 1800.0) * perc(0.04, tau=0.008), 0.0, 0.5)

    clunk = modal(0.5, [(58.0, 0.9, 0.07), (124.0, 0.5, 0.045), (280.0, 0.25, 0.02)], rng)
    place(out, clunk * perc(0.5, attack=0.0006, tau=0.06), 0.10)
    place(out, highpass(white(0.10, rng), 5200.0) * perc(0.10, tau=0.02) * 0.35, 0.10)

    # Mains hum swelling back in - the sound of the lights returning.
    swell = np.linspace(0.0, 1.0, int(1.6 * SR)) ** 2
    hum = (np.sin(TWO_PI * 60.0 * t(1.6)) * 0.5
           + np.sin(TWO_PI * 120.0 * t(1.6)) * 0.22
           + np.sin(TWO_PI * 180.0 * t(1.6)) * 0.08) * swell
    place(out, hum, 0.42, 0.6)
    return peak_norm(fade(out), -7.0)


# ---------------------------------------------------------------- room tones
# Twelve second loops. Quiet by design: room tone is meant to be noticed when
# it changes, not while it plays (GDD 19.2).

LOOP_SECONDS = 12.0
RENDER_SECONDS = LOOP_SECONDS + 3.0


def room_tone_office(rng):
    """Mains hum, a fluorescent tube, the PC fan, and a CRT that should not be on."""
    tt = t(RENDER_SECONDS)
    out = np.zeros(len(tt))

    for hz, gain in ((60.0, 0.30), (120.0, 0.16), (180.0, 0.07), (240.0, 0.03)):
        out += np.sin(TWO_PI * hz * tt + rng.uniform(0, TWO_PI)) * gain

    # Fluorescent ballast: a 120 Hz buzz that flickers slowly and unevenly.
    flicker = 0.75 + 0.25 * np.sin(TWO_PI * 0.23 * tt) * np.sin(TWO_PI * 0.07 * tt)
    out += bandpass(white(RENDER_SECONDS, rng), 120.0, q=18.0) * flicker * 1.6
    out += bandpass(white(RENDER_SECONDS, rng), 360.0, q=24.0) * flicker * 0.6

    out += lowpass(pink(RENDER_SECONDS, rng), 700.0) * 0.55        # PC fan

    # The CRT line whine. Almost inaudible, and the room is wrong without it.
    out += np.sin(TWO_PI * 15734.0 * tt) * 0.012

    return rms_norm(loopify(out), -33.0)


def room_tone_corridor(rng):
    """Colder and further away. Air moving in a concrete box."""
    tt = t(RENDER_SECONDS)
    out = lowpass(pink(RENDER_SECONDS, rng), 420.0) * 1.2
    out += np.sin(TWO_PI * 60.0 * tt) * 0.06              # hum through the wall
    out += lowpass(white(RENDER_SECONDS, rng), 140.0) * 0.5   # distant plant

    # A slow breath in the shaft, so the corridor is never quite steady.
    out *= 0.85 + 0.15 * np.sin(TWO_PI * 0.09 * tt + 1.1)
    return rms_norm(loopify(reverb(out, mix=0.35, size=1.6)), -36.0)


def room_tone_basement(rng):
    """Deep plant rumble, pipe resonance, and water finding its way down."""
    tt = t(RENDER_SECONDS)
    out = lowpass(pink(RENDER_SECONDS, rng), 190.0) * 1.6
    out += np.sin(TWO_PI * 33.0 * tt) * 0.22
    out += np.sin(TWO_PI * 49.0 * tt + 0.7) * 0.12
    out += bandpass(white(RENDER_SECONDS, rng), 210.0, q=9.0) * 0.5   # pipe

    # Drips. Kept clear of the loop seam so the crossfade never cuts one in half.
    at = 1.4
    while at < LOOP_SECONDS - 1.6:
        drop = modal(0.35, [(1750.0, 0.5, 0.030), (2680.0, 0.3, 0.018),
                            (940.0, 0.35, 0.055)], rng)
        drop *= perc(0.35, attack=0.0005, tau=0.035)
        place(out, reverb(drop, mix=0.5, size=1.8), at, rng.uniform(0.10, 0.20))
        at += rng.uniform(2.1, 4.3)

    return rms_norm(loopify(out), -32.0)


def room_tone_outside(rng):
    """Wind across the rooftop, and the city refusing to be completely asleep."""
    tt = t(RENDER_SECONDS)

    gust = 0.55 + 0.45 * np.abs(np.sin(TWO_PI * 0.061 * tt) + 0.5 * np.sin(TWO_PI * 0.017 * tt))
    wind = lowpass(pink(RENDER_SECONDS, rng), 900.0) * gust * 1.3
    wind += bandpass(white(RENDER_SECONDS, rng), 1600.0, q=1.1) * gust * 0.25   # edge whistle

    traffic = lowpass(pink(RENDER_SECONDS, rng), 220.0) * 0.7
    traffic *= 0.8 + 0.2 * np.sin(TWO_PI * 0.04 * tt)

    return rms_norm(loopify(wind + traffic), -31.0)


# ==================================================================== driver

def build_all(seed=404):
    """Renders every cue. Returns {enum member name: mono float samples}."""
    rng = np.random.default_rng(seed)
    clips = {}

    # The office tone is rendered first because the self-echo cue is made of it.
    clips["RoomToneOffice"] = room_tone_office(rng)
    clips["RoomToneCorridor"] = room_tone_corridor(rng)
    clips["RoomToneBasement"] = room_tone_basement(rng)
    clips["RoomToneOutside"] = room_tone_outside(rng)

    clips["AnomalyStart"] = anomaly_start(rng)
    clips["Interphone"] = interphone(rng)
    clips["InterphoneSelfEcho"] = interphone_self_echo(clips["RoomToneOffice"])
    clips["PhoneRing"] = phone_ring(rng)
    clips["Footsteps"] = footsteps(rng)
    clips["FootstepsDistant"] = footsteps_distant(rng)
    clips["DoorOpen"] = door_open(rng)
    clips["EvidenceTaken"] = evidence_taken(rng)
    clips["ReportFiled"] = report_filed(rng)
    clips["ReportRejected"] = report_rejected(rng)
    clips["FireAlarm"] = fire_alarm(rng)
    clips["OfficeDoorKnock"] = office_door_knock(rng)
    clips["OfficeDoorHandle"] = office_door_handle(rng)
    clips["OfficeDoorForced"] = office_door_forced(rng)
    clips["PowerWarning"] = power_warning(rng)
    clips["BreakerReset"] = breaker_reset(rng)

    return clips


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    default_out = os.path.join(here, "..", "Assets", "_Project", "Resources", "NO404", "Audio")

    parser = argparse.ArgumentParser(description="Render NO UNIT 404 audio cues.")
    parser.add_argument("--out", default=default_out)
    parser.add_argument("--seed", type=int, default=404)
    args = parser.parse_args()

    out_dir = os.path.abspath(args.out)
    os.makedirs(out_dir, exist_ok=True)

    clips = build_all(args.seed)
    total = 0
    for name in sorted(clips):
        data = np.clip(clips[name], -1.0, 1.0)
        pcm = (data * 32767.0).astype(np.int16)
        path = os.path.join(out_dir, name + ".wav")
        wavfile.write(path, SR, pcm)

        size = os.path.getsize(path)
        total += size
        peak = 20.0 * np.log10(max(np.max(np.abs(data)), 1e-9))
        rms = 20.0 * np.log10(max(np.sqrt(np.mean(data ** 2)), 1e-9))
        print("  %-20s %6.2fs  peak %6.1f dB  rms %6.1f dB  %8.1f KB"
              % (name, len(data) / float(SR), peak, rms, size / 1024.0))

    print("\n%d clips, %.2f MB -> %s" % (len(clips), total / 1048576.0, out_dir))


if __name__ == "__main__":
    main()
