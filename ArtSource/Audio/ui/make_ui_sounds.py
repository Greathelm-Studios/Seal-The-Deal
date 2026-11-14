"""Seal the Deal: the UI sound set, synthesised from nothing (Python standard library only).

    python3 ArtSource/Audio/ui/make_ui_sounds.py            # writes Assets/_Project/Audio/UI/*.wav and prints the table

Every sound is a few sine / triangle partials with a fast exponential decay and, sometimes, a little
band-limited noise for air. Nothing is sampled, downloaded or copied. Output: 16-bit, 48 kHz, mono.

Voice: a clean 2001 corporate future. Glassy ticks, soft tones, sparse. Pitches live in one key (C major
pentatonic: C D E G A) so any run of sounds is consonant: hover steps climb the scale, confirm resolves
upward, back and error resolve downward, nothing is harsh or long.

Levels: every clip is scaled to a peak target by class (UI 0.45, sting 0.55, combat 0.70, in linear
full scale), so no UI sound is louder than another by ear or by number; RMS is printed for review.
Tuning: edit the EVENTS table (notes, durations, timbre) and re-run; the output is deterministic
(noise uses a fixed seed).
"""
from __future__ import annotations

import math
import os
import random
import struct
import sys
import wave

SR = 48000
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "_Project", "Audio", "UI"))

# C major pentatonic, octave 4 and up, Hz
def hz(note: str) -> float:
    names = {"C": 0, "D": 2, "E": 4, "G": 7, "A": 9, "B": 11}
    return 440.0 * 2 ** ((names[note[0]] + 12 * (int(note[1]) - 4) - 9) / 12)


def sine(f, t):
    return math.sin(2 * math.pi * f * t)


def tri(f, t):
    p = (f * t) % 1.0
    return 4 * abs(p - 0.5) - 1


class Rng:
    def __init__(self, seed):
        self.r = random.Random(seed)

    def noise(self):
        return self.r.uniform(-1, 1)


def note(buf, start, f, dur, amp=1.0, decay=None, kind="sine", glass=0.0, attack=0.003, glide=None):
    """Add one decaying tone at `start` seconds. glass: level of an inharmonic partial (x2.76) for a glassy tick.
    glide: (f_from, f_to) pitch slide over the note."""
    n = int(dur * SR)
    s0 = int(start * SR)
    decay = decay or dur / 4.5
    ph = 0.0
    for i in range(n):
        t = i / SR
        ff = f if glide is None else glide[0] + (glide[1] - glide[0]) * (i / n)
        ph += ff / SR
        osc = (math.sin(2 * math.pi * ph) if kind == "sine" else (4 * abs((ph % 1.0) - 0.5) - 1))
        v = osc + glass * math.sin(2 * math.pi * ph * 2.76) * math.exp(-t / (decay * 0.5)) + 0.18 * math.sin(2 * math.pi * ph * 2)
        env = math.exp(-t / decay) * min(1.0, t / attack)
        tail = min(1.0, (dur - t) / 0.01)   # 10 ms fade so a clip never ends on a click
        if s0 + i < len(buf):
            buf[s0 + i] += amp * v * env * tail


def air(buf, start, dur, amp=0.3, lo=2000.0, hi=6000.0, sweep=None, decay=None, seed=1, attack=0.004):
    """Band-limited noise burst (difference of two one-pole filters). sweep=(lo0,hi0,lo1,hi1) glides the band."""
    rng = Rng(seed)
    n = int(dur * SR)
    s0 = int(start * SR)
    decay = decay or dur / 3
    a = b = 0.0
    for i in range(n):
        t = i / SR
        if sweep:
            k = i / n
            lo_, hi_ = sweep[0] + (sweep[2] - sweep[0]) * k, sweep[1] + (sweep[3] - sweep[1]) * k
        else:
            lo_, hi_ = lo, hi
        ca, cb = 1 - math.exp(-2 * math.pi * hi_ / SR), 1 - math.exp(-2 * math.pi * lo_ / SR)
        x = rng.noise()
        a += ca * (x - a)
        b += cb * (x - b)
        v = a - b
        env = math.exp(-t / decay) * min(1.0, t / attack) * min(1.0, (dur - t) / 0.01)
        if s0 + i < len(buf):
            buf[s0 + i] += amp * v * env


def swell(buf, start, dur, amp, lo0, hi0, lo1, hi1, seed=3):
    """Noise whoosh: band sweeps up while the level rises then falls (raised-cosine)."""
    rng = Rng(seed)
    n = int(dur * SR)
    s0 = int(start * SR)
    a = b = 0.0
    for i in range(n):
        k = i / n
        lo_, hi_ = lo0 + (lo1 - lo0) * k, hi0 + (hi1 - hi0) * k
        ca, cb = 1 - math.exp(-2 * math.pi * hi_ / SR), 1 - math.exp(-2 * math.pi * lo_ / SR)
        x = rng.noise()
        a += ca * (x - a)
        b += cb * (x - b)
        env = math.sin(math.pi * k) ** 2
        if s0 + i < len(buf):
            buf[s0 + i] += amp * (a - b) * env


# ----------------------------------------------------------------------------------------------- events
# name: (class, builder). Builders write into a buffer of the given length (seconds).
def E(length, cls, rel, fn):
    return dict(length=length, cls=cls, rel=rel, fn=fn)


def b_hover(b):   # G5 triangle + glassy partial; runtime steps the pitch up the pentatonic
    note(b, 0, hz("G5"), 0.075, 0.9, 0.022, "tri", glass=0.25)


def b_focus(b):   # softer, lower: E5 sine
    note(b, 0, hz("E5"), 0.07, 0.8, 0.02, "sine", glass=0.1)


def b_select(b):  # C5 -> G5, a rising fifth
    note(b, 0, hz("C5"), 0.06, 0.8, 0.02, "tri", glass=0.12)
    note(b, 0.055, hz("G5"), 0.11, 1.0, 0.03, "sine", glass=0.2)


def b_confirm(b):  # C5 G5 C6 rising arpeggio, resolves upward
    for k, (n, t) in enumerate((("C5", 0), ("G5", 0.06), ("C6", 0.12))):
        note(b, t, hz(n), 0.20 - t * 0.6, 0.9, 0.05, "sine", glass=0.2)
    air(b, 0.0, 0.05, 0.10, 3000, 7000, decay=0.012, seed=2)


def b_back(b):    # G5 -> C5, falls
    note(b, 0, hz("G5"), 0.07, 0.8, 0.025, "tri", glass=0.1)
    note(b, 0.06, hz("C5"), 0.13, 0.9, 0.04, "sine", glass=0.1)


def b_error(b):   # two soft low tones, down a third-ish; dull, not buzzy
    note(b, 0, hz("E4"), 0.11, 0.9, 0.05, "tri")
    note(b, 0.10, hz("C4"), 0.17, 0.95, 0.07, "tri")


def b_tab(b):     # glass tick + a breath of air
    note(b, 0, hz("E5"), 0.06, 0.8, 0.018, "tri", glass=0.35)
    air(b, 0, 0.09, 0.22, 2500, 7000, sweep=(2500, 6000, 3500, 8000), decay=0.03, seed=4)


def b_toggle_on(b):
    note(b, 0, hz("C5"), 0.05, 0.8, 0.018, "tri", glass=0.15)
    note(b, 0.045, hz("G5"), 0.08, 1.0, 0.025, "sine", glass=0.25)


def b_toggle_off(b):
    note(b, 0, hz("G5"), 0.05, 0.7, 0.018, "tri", glass=0.1)
    note(b, 0.045, hz("C5"), 0.08, 0.8, 0.025, "sine", glass=0.1)


def b_slider(b):  # a tiny tick; pitch follows the value at run time
    note(b, 0, hz("C6"), 0.028, 0.9, 0.008, "tri", glass=0.3)


def b_dd_open(b):  # air swishes up, a tone lands on top
    air(b, 0, 0.12, 0.25, 1800, 5000, sweep=(1500, 4000, 2500, 7000), decay=0.05, seed=5)
    note(b, 0.05, hz("A5"), 0.09, 0.8, 0.03, "sine", glass=0.2)


def b_dd_close(b):
    air(b, 0, 0.11, 0.22, 1800, 5000, sweep=(2500, 7000, 1500, 4000), decay=0.045, seed=6)
    note(b, 0.0, hz("E5"), 0.09, 0.7, 0.03, "sine", glass=0.1)


def b_kb_listen(b):  # two soft pulses: waiting for a key
    note(b, 0, hz("E5"), 0.07, 0.8, 0.025, "sine")
    note(b, 0.11, hz("E5"), 0.09, 0.9, 0.03, "sine")


def b_kb_set(b):   # a clear, bright ding
    note(b, 0, hz("C6"), 0.28, 1.0, 0.07, "sine", glass=0.3)
    note(b, 0, hz("G5"), 0.18, 0.4, 0.04, "sine")


def b_panel_open(b):  # soft rising two-note sweep
    note(b, 0, hz("C5"), 0.26, 0.7, 0.07, "sine", glide=(hz("C5") * 0.94, hz("C5")))
    note(b, 0.04, hz("G5"), 0.22, 0.7, 0.07, "sine", glide=(hz("G5") * 0.94, hz("G5")))
    air(b, 0, 0.18, 0.12, 1500, 4500, sweep=(1200, 3000, 2200, 6000), decay=0.07, seed=7)


def b_panel_close(b):
    note(b, 0, hz("G5"), 0.2, 0.7, 0.06, "sine", glide=(hz("G5"), hz("G5") * 0.95))
    note(b, 0.04, hz("C5"), 0.2, 0.7, 0.06, "sine", glide=(hz("C5"), hz("C5") * 0.95))
    air(b, 0, 0.15, 0.10, 1500, 4500, sweep=(2200, 6000, 1200, 3000), decay=0.06, seed=8)


def b_transition(b):  # whoosh: noise swell, band sweeping up, a faint tone under it
    swell(b, 0, 0.45, 0.9, 400, 1500, 1500, 7000, seed=9)
    note(b, 0.08, hz("C5"), 0.3, 0.12, 0.2, "sine", glide=(hz("C5") * 0.8, hz("C5") * 1.25))


def b_notify(b):   # two-note bell, up a fifth
    note(b, 0, hz("G5"), 0.30, 0.9, 0.09, "sine", glass=0.35)
    note(b, 0.12, hz("D6") if False else hz("G5") * 1.5, 0.34, 0.9, 0.11, "sine", glass=0.35)


def b_ready(b):
    note(b, 0, hz("E5"), 0.08, 0.8, 0.03, "sine", glass=0.15)
    note(b, 0.075, hz("B5"), 0.13, 0.95, 0.045, "sine", glass=0.25)


def b_unready(b):
    note(b, 0, hz("B5"), 0.08, 0.7, 0.03, "sine", glass=0.1)
    note(b, 0.075, hz("E5"), 0.12, 0.8, 0.04, "sine")


def b_count_tick(b):  # a clear, short tick, one pitch
    note(b, 0, hz("E5"), 0.05, 0.9, 0.012, "tri", glass=0.4)
    air(b, 0, 0.012, 0.15, 3000, 8000, decay=0.004, seed=10)


def b_count_go(b):  # rising four-note flourish
    for n, t in (("C5", 0), ("E5", 0.07), ("G5", 0.14), ("C6", 0.21)):
        note(b, t, hz(n), 0.42 - t, 0.9, 0.08, "sine", glass=0.25)


def b_won(b):  # rising arpeggio with a shimmer on top
    for n, t in (("C5", 0), ("E5", 0.12), ("G5", 0.24), ("C6", 0.36), ("E6", 0.50)):
        note(b, t, hz(n), 0.55, 0.7, 0.14, "sine", glass=0.25)
    note(b, 0.50, hz("G6") if False else hz("E6") * 1.5, 0.55, 0.28, 0.2, "sine")
    air(b, 0.48, 0.5, 0.06, 4000, 9000, decay=0.2, seed=11)


def b_lost(b):  # gentle fall, soft, unhurried
    for n, t in (("G4", 0), ("E4", 0.17), ("C4", 0.34)):
        note(b, t, hz(n), 0.65 - t * 0.5, 0.8, 0.16, "tri")


def b_floor(b):    # a gentle two-tone lift chime
    note(b, 0, hz("C6"), 0.45, 0.8, 0.12, "sine", glass=0.3)
    note(b, 0.14, hz("G6") if False else hz("C6") * 1.5, 0.5, 0.7, 0.15, "sine", glass=0.3)
    note(b, 0, hz("C4"), 0.18, 0.5, 0.05, "sine")


def b_case_take(b):  # clack + a bright rise
    air(b, 0, 0.03, 0.35, 1500, 6000, decay=0.008, seed=12)
    note(b, 0.02, hz("G5"), 0.07, 0.8, 0.025, "tri", glass=0.2)
    note(b, 0.075, hz("C6"), 0.13, 1.0, 0.045, "sine", glass=0.25)


def b_case_drop(b):  # thud + a falling tone
    air(b, 0, 0.05, 0.45, 300, 1800, decay=0.014, seed=13)
    note(b, 0, hz("C4"), 0.14, 0.9, 0.05, "sine", glide=(hz("C4") * 1.3, hz("C4")))


def b_handsfull(b):  # soft double bonk, low
    note(b, 0, hz("D4") if False else 293.66, 0.07, 0.9, 0.03, "tri")
    note(b, 0.085, 293.66, 0.09, 0.9, 0.035, "tri")


def b_hit(b):  # bright tick in the 3 kHz region: cuts through gunfire, very short
    note(b, 0, 3136.0, 0.04, 1.0, 0.009, "sine", glass=0.35, attack=0.0008)


def b_hit_head(b):  # brighter than a body hit: a higher tick plus a hair of air
    note(b, 0, 4186.0, 0.05, 1.0, 0.008, "sine", glass=0.4, attack=0.0008)
    note(b, 0.004, 3136.0, 0.04, 0.5, 0.008, "sine", attack=0.0008)


def b_kill(b):  # two rising ticks, a ring, then a tiny air snap (~200 ms; plays over the hit tick)
    note(b, 0, 2637.0, 0.06, 0.9, 0.014, "sine", glass=0.3, attack=0.0008)
    note(b, 0.06, 3951.0, 0.14, 1.0, 0.04, "sine", glass=0.3, attack=0.0008)
    note(b, 0.06, 1975.5, 0.12, 0.35, 0.03, "sine", attack=0.0008)
    air(b, 0.06, 0.03, 0.2, 4000, 9000, decay=0.007, seed=14)


EVENTS = {
    # name: length s, class, relation, builder
    "HoverMove": E(0.10, "ui", "G5; run steps up C D E G A", b_hover),
    "Focus": E(0.10, "ui", "E5, softer than hover", b_focus),
    "Select": E(0.18, "ui", "C5 to G5, rises a fifth", b_select),
    "Confirm": E(0.26, "ui", "C5 G5 C6, rises (resolves up)", b_confirm),
    "Back": E(0.20, "ui", "G5 to C5, falls", b_back),
    "Error": E(0.30, "ui", "E4 to C4, falls (dull, soft)", b_error),
    "TabChange": E(0.12, "ui", "E5 glass tick + air", b_tab),
    "ToggleOn": E(0.14, "ui", "C5 to G5, quick rise", b_toggle_on),
    "ToggleOff": E(0.14, "ui", "G5 to C5, quick fall", b_toggle_off),
    "SliderTick": E(0.05, "ui", "C6 tick; runtime pitch follows value", b_slider),
    "DropdownOpen": E(0.16, "ui", "air swish up + A5", b_dd_open),
    "DropdownClose": E(0.14, "ui", "air swish down + E5", b_dd_close),
    "KeyBindListen": E(0.20, "ui", "E5 double pulse (waiting)", b_kb_listen),
    "KeyBindSet": E(0.30, "ui", "C6 ding over G5", b_kb_set),
    "PanelOpen": E(0.30, "ui", "C5 + G5 rising sweep", b_panel_open),
    "PanelClose": E(0.26, "ui", "G5 + C5 falling sweep", b_panel_close),
    "Transition": E(0.48, "ui", "noise whoosh up; C5 under", b_transition),
    "Notification": E(0.46, "ui", "G5 then D6, rises a fifth", b_notify),
    "LobbyReady": E(0.22, "ui", "E5 to B5, rises", b_ready),
    "LobbyUnready": E(0.20, "ui", "B5 to E5, falls", b_unready),
    "CountdownTick": E(0.07, "ui", "E5 tick, one pitch", b_count_tick),
    "CountdownGo": E(0.50, "sting", "C5 E5 G5 C6 flourish, up", b_count_go),
    "MatchWon": E(1.10, "sting", "C5 E5 G5 C6 E6 arpeggio, up", b_won),
    "MatchLost": E(1.00, "sting", "G4 E4 C4, slow fall", b_lost),
    "FloorOpened": E(0.65, "sting", "C6 then G6 lift chime over C4", b_floor),
    "BriefcaseTaken": E(0.24, "ui", "clack + G5 to C6 rise", b_case_take),
    "BriefcaseDropped": E(0.20, "ui", "thud + C4 fall", b_case_drop),
    "HandsFull": E(0.20, "ui", "D4 double bonk, low", b_handsfull),
    "HitMarker": E(0.055, "combat", "G7 tick (3.1 kHz), very short (full auto: 10 per second)", b_hit),
    "HitMarkerHead": E(0.055, "combat", "C8 tick (4.2 kHz) over G7: brighter, precision hit", b_hit_head),
    "KillConfirm": E(0.20, "combat", "E7 to B7 ticks + B6 ring (2.6 to 3.9 kHz)", b_kill),
}
PEAK = {"ui": 0.45, "sting": 0.55, "combat": 0.70}

# ------------------------------------------------------------------------------------------ item sounds
# World sounds for the pickups lane (3D, SFX bus): Assets/_Project/Audio/Items/Item_<Slot>.wav. Full strength:
# the game scales impact volume with speed.
def i_pickup(b):   # cloth-and-plastic grab: short low-mid noise + a soft knock
    air(b, 0, 0.09, 0.6, 500, 3000, decay=0.02, seed=21)
    note(b, 0.01, 180.0, 0.10, 0.6, 0.03, "sine", glide=(240.0, 170.0))


def i_drop(b):     # let go: a duller knock, then a small second bounce
    air(b, 0, 0.07, 0.6, 300, 2000, decay=0.018, seed=22)
    note(b, 0, 130.0, 0.12, 0.8, 0.035, "sine", glide=(190.0, 120.0))
    air(b, 0.11, 0.05, 0.25, 300, 1800, decay=0.012, seed=23)


def i_throw(b):    # whoosh: noise swell, band falling as the item leaves
    swell(b, 0, 0.2, 1.0, 900, 5000, 300, 1800, seed=24)


def i_impact_light(b):  # thin clack
    air(b, 0, 0.05, 0.9, 900, 6000, decay=0.010, seed=25, attack=0.0005)
    note(b, 0, 420.0, 0.09, 0.5, 0.02, "tri", glide=(560.0, 400.0), attack=0.0005)


def i_impact_heavy(b):  # thud with weight: low sine drop + broad noise
    air(b, 0, 0.12, 0.8, 120, 2500, decay=0.03, seed=26, attack=0.0005)
    note(b, 0, 70.0, 0.32, 1.0, 0.09, "sine", glide=(140.0, 55.0), attack=0.0005)
    air(b, 0.06, 0.2, 0.2, 200, 1200, decay=0.07, seed=27)


def i_arm(b):      # two-part mechanical click
    air(b, 0, 0.02, 0.8, 2000, 9000, decay=0.004, seed=28, attack=0.0003)
    air(b, 0.045, 0.03, 1.0, 1200, 7000, decay=0.006, seed=29, attack=0.0003)
    note(b, 0.045, 1320.0, 0.05, 0.3, 0.012, "tri", attack=0.0005)


def i_beep(b):     # 50 ms, 2 kHz, square-ish (two odd partials)
    note(b, 0, 2093.0, 0.05, 1.0, 0.06, "tri", attack=0.001)


def i_explosion(b):  # crack, low boom falling in pitch, long filtered rumble tail
    air(b, 0, 0.06, 1.0, 800, 9000, decay=0.012, seed=30, attack=0.0003)
    note(b, 0, 50.0, 0.9, 1.2, 0.22, "sine", glide=(120.0, 32.0), attack=0.001)
    air(b, 0, 1.05, 0.9, 60, 900, sweep=(80, 2500, 40, 300), decay=0.28, seed=31, attack=0.002)
    air(b, 0.05, 0.6, 0.35, 1500, 6000, sweep=(1500, 7000, 600, 2000), decay=0.12, seed=32)


def i_vent_end(b):   # last gasp: hiss whose band and level fall away
    air(b, 0, 0.4, 1.0, 1500, 8000, sweep=(2500, 9000, 600, 2500), decay=0.12, seed=34)


def i_wet_floor(b):  # plastic clack, then a small splash
    air(b, 0, 0.03, 0.9, 1200, 5000, decay=0.006, seed=35, attack=0.0003)
    note(b, 0, 600.0, 0.06, 0.5, 0.015, "tri", attack=0.0005)
    air(b, 0.05, 0.25, 0.55, 1500, 8000, sweep=(800, 4000, 2500, 9000), decay=0.07, seed=36, attack=0.01)


def i_slip(b):       # squeak: a tone sliding up fast with a little wobble, then a scuff
    note(b, 0, 1200.0, 0.2, 0.9, 0.09, "tri", glide=(700.0, 1900.0), attack=0.01)
    air(b, 0.16, 0.14, 0.4, 600, 3500, decay=0.04, seed=37)


def vent_loop(seconds=2.0):
    """Seamless gas hiss: band-limited noise with slow level wobble; the last 0.25 s is crossfaded into the first
    0.25 s so the loop point has no click or level step."""
    n, x = int(seconds * SR), int(0.25 * SR)
    raw = [0.0] * (n + x)
    rng = Rng(33)
    a = b = 0.0
    ca, cb = 1 - math.exp(-2 * math.pi * 7000 / SR), 1 - math.exp(-2 * math.pi * 1800 / SR)
    for i in range(n + x):
        v = rng.noise()
        a += ca * (v - a)
        b += cb * (v - b)
        raw[i] = (a - b) * (0.85 + 0.15 * math.sin(2 * math.pi * 3 * i / n))   # 3 whole wobbles per loop
    out = raw[:n]
    for i in range(x):
        k = i / x
        out[i] = raw[i] * math.sqrt(k) + raw[n + i] * math.sqrt(1 - k)   # equal-power: tail leads back into the head
    return out


ITEMS = {  # slot: (length s, peak, description, builder)
    "PickUp": (0.15, 0.6, "grab: soft noise + knock", i_pickup),
    "Drop": (0.20, 0.6, "dull knock + small bounce", i_drop),
    "Throw": (0.20, 0.6, "whoosh, band falling", i_throw),
    "ImpactLight": (0.12, 0.8, "thin clack, full strength", i_impact_light),
    "ImpactHeavy": (0.35, 0.8, "low thud 140 to 55 Hz, full strength", i_impact_heavy),
    "Arm": (0.10, 0.6, "two-part click", i_arm),
    "Beep": (0.05, 0.6, "2.1 kHz, 50 ms", i_beep),
    "Explosion": (1.10, 0.9, "crack + boom 120 to 32 Hz + rumble tail", i_explosion),
    "VentEnd": (0.40, 0.6, "hiss falling away", i_vent_end),
    "WetFloorDown": (0.32, 0.6, "plastic clack + splash", i_wet_floor),
    "Slip": (0.30, 0.6, "squeak 700 to 1900 Hz + scuff", i_slip),
}

# ---------------------------------------------------------------------------------------- weapon sounds
# World sounds for the shooting lane (3D at the muzzle or hit point, SFX bus; Hurt / Death / Respawn are 2D):
# Assets/_Project/Audio/Weapons/Weapon_<Name>.wav. Events and timing: docs/systems/WEAPONS.md.
def w_pistol(b):    # sharp crack, a low punch, short room tail
    air(b, 0, 0.03, 1.0, 1500, 12000, decay=0.006, seed=41, attack=0.0002)
    note(b, 0, 90.0, 0.12, 0.9, 0.03, "sine", glide=(220.0, 70.0), attack=0.0005)
    air(b, 0.004, 0.24, 0.5, 300, 5000, sweep=(400, 7000, 200, 1500), decay=0.055, seed=42, attack=0.001)


def w_ar15(b):      # tighter and brighter, almost no tail: 10 a second must not smear
    air(b, 0, 0.025, 1.0, 2000, 14000, decay=0.005, seed=43, attack=0.0002)
    note(b, 0, 110.0, 0.07, 0.8, 0.018, "sine", glide=(260.0, 90.0), attack=0.0005)
    air(b, 0.003, 0.09, 0.4, 500, 6000, sweep=(600, 8000, 300, 2500), decay=0.022, seed=44, attack=0.001)


def w_dry(b):       # dull click
    air(b, 0, 0.02, 0.8, 600, 3500, decay=0.005, seed=45, attack=0.0003)
    note(b, 0, 320.0, 0.05, 0.4, 0.012, "tri", attack=0.0005)


def w_mag_out(b):   # slide of plastic on metal, then a release click
    air(b, 0, 0.08, 0.5, 1500, 6000, sweep=(2500, 7000, 1200, 4000), decay=0.04, seed=46)
    air(b, 0.07, 0.02, 0.9, 900, 5000, decay=0.005, seed=47, attack=0.0003)


def w_mag_in(b):    # slide, then a solid seat with a low knock
    air(b, 0, 0.06, 0.4, 1200, 5000, sweep=(1200, 4000, 2500, 7000), decay=0.03, seed=48)
    air(b, 0.06, 0.03, 1.0, 500, 4500, decay=0.007, seed=49, attack=0.0003)
    note(b, 0.06, 150.0, 0.07, 0.6, 0.02, "sine", glide=(210.0, 140.0), attack=0.0005)


def w_slide(b):     # pistol slide: back (click), forward (clack)
    air(b, 0, 0.02, 0.8, 1500, 8000, decay=0.005, seed=50, attack=0.0003)
    air(b, 0.02, 0.05, 0.3, 2000, 7000, decay=0.03, seed=51)
    air(b, 0.075, 0.03, 1.0, 900, 7000, decay=0.007, seed=52, attack=0.0003)
    note(b, 0.075, 900.0, 0.05, 0.25, 0.012, "tri", attack=0.0005)


def w_charge(b):    # rifle charging handle: longer pull, heavier return
    air(b, 0, 0.02, 0.7, 1200, 7000, decay=0.005, seed=53, attack=0.0003)
    air(b, 0.02, 0.09, 0.35, 1500, 6000, sweep=(1500, 5000, 2500, 7000), decay=0.05, seed=54)
    air(b, 0.12, 0.04, 1.0, 600, 6000, decay=0.009, seed=55, attack=0.0003)
    note(b, 0.12, 180.0, 0.08, 0.5, 0.02, "sine", glide=(260.0, 160.0), attack=0.0005)


def w_reload_done(b):  # a small settled click
    air(b, 0, 0.02, 0.7, 2000, 8000, decay=0.004, seed=56, attack=0.0003)
    note(b, 0, 1046.5, 0.05, 0.3, 0.014, "tri", attack=0.0005)


def w_swap(b):      # cloth swish out, a catch as the new gun comes up
    swell(b, 0, 0.16, 0.7, 600, 3000, 1200, 6000, seed=57)
    air(b, 0.17, 0.03, 0.9, 800, 5000, decay=0.007, seed=58, attack=0.0003)


def w_impact_world(b):  # hard tick with a little debris
    air(b, 0, 0.03, 1.0, 1200, 9000, decay=0.006, seed=59, attack=0.0002)
    note(b, 0, 500.0, 0.05, 0.4, 0.012, "tri", glide=(800.0, 450.0), attack=0.0005)
    air(b, 0.02, 0.1, 0.25, 2000, 7000, decay=0.03, seed=60)


def w_impact_body(b):   # dull thud, no brightness
    air(b, 0, 0.05, 0.7, 150, 1500, decay=0.014, seed=61, attack=0.0005)
    note(b, 0, 100.0, 0.11, 0.9, 0.03, "sine", glide=(170.0, 85.0), attack=0.0005)


def w_glass(b):     # crack, then a spray of high glassy partials falling as shards, and a bright noise wash
    air(b, 0, 0.03, 1.0, 2500, 14000, decay=0.006, seed=62, attack=0.0002)
    rng = random.Random(63)
    for k in range(26):
        t = 0.01 + rng.random() ** 1.6 * 0.5
        f = rng.uniform(2200, 7500)
        note(b, t, f, 0.16, rng.uniform(0.12, 0.35) * (1 - t), 0.03, "sine", glass=0.5, attack=0.0005)
    air(b, 0.01, 0.6, 0.45, 3000, 12000, sweep=(3000, 13000, 2000, 7000), decay=0.16, seed=64)


def w_hurt(b):      # your own body: a low pulse, soft, 2D
    note(b, 0, 82.0, 0.2, 1.0, 0.06, "sine", glide=(120.0, 75.0), attack=0.002)
    air(b, 0, 0.08, 0.25, 200, 1200, decay=0.025, seed=65)


def w_death(b):     # a falling tone that drops away under a soft noise close
    note(b, 0, hz("E4"), 0.7, 0.8, 0.25, "tri", glide=(hz("E4"), hz("C4") / 2))
    note(b, 0, 60.0, 0.4, 0.8, 0.12, "sine", glide=(110.0, 50.0), attack=0.002)
    air(b, 0, 0.7, 0.2, 300, 2500, sweep=(600, 4000, 150, 600), decay=0.3, seed=66)


def w_respawn(b):   # the lift chime's cousin: rising fifth with air, back on the floor
    air(b, 0, 0.2, 0.2, 1500, 6000, sweep=(800, 3000, 2500, 8000), decay=0.08, seed=67)
    note(b, 0.05, hz("C5"), 0.3, 0.8, 0.09, "sine", glass=0.2)
    note(b, 0.17, hz("G5"), 0.33, 0.9, 0.11, "sine", glass=0.25)


WEAPONS = {  # name: (length s, peak, description, builder)
    "PistolShot": (0.26, 0.90, "crack + punch 220 to 70 Hz + 0.2 s room tail", w_pistol),
    "AR15Shot": (0.10, 0.90, "tighter crack, 100 ms so 10 a second stays clean", w_ar15),
    "DryFire": (0.06, 0.50, "dull click", w_dry),
    "ReloadMagOut": (0.11, 0.60, "slide + release click", w_mag_out),
    "ReloadMagIn": (0.14, 0.65, "slide + solid seat", w_mag_in),
    "ReloadSlide": (0.13, 0.65, "pistol slide: back click, forward clack", w_slide),
    "ReloadCharge": (0.21, 0.65, "rifle charging handle: pull, heavy return", w_charge),
    "ReloadFinished": (0.06, 0.40, "small settled click", w_reload_done),
    "Swap": (0.22, 0.50, "swish + catch", w_swap),
    "ImpactWorld": (0.13, 0.70, "hard tick + debris", w_impact_world),
    "ImpactBody": (0.12, 0.70, "dull thud 170 to 85 Hz", w_impact_body),
    "GlassShatter": (0.70, 0.85, "crack + 26 falling glass partials + bright wash", w_glass),
    "Hurt": (0.22, 0.60, "low pulse 120 to 75 Hz, 2D", w_hurt),
    "Death": (0.75, 0.60, "tone falling E4 to C3 over a low drop, 2D", w_death),
    "Respawn": (0.52, 0.50, "C5 to G5 rise with air, 2D", w_respawn),
}
WEAPON_OUT = os.path.normpath(os.path.join(OUT, "..", "Weapons"))


def render_table(table):
    out = {}
    for name, (length, peak, _, fn) in table.items():
        buf = [0.0] * int(length * SR)
        fn(buf)
        dc_and_fade(buf, guard=0.0005)   # world transients keep their attack
        g = peak / (max(abs(x) for x in buf) or 1.0)
        out[name] = [x * g for x in buf]
    return out

ITEM_OUT = os.path.normpath(os.path.join(OUT, "..", "Items"))


def render_items():
    os.makedirs(ITEM_OUT, exist_ok=True)
    out = {}
    for name, (length, peak, _, fn) in ITEMS.items():
        buf = [0.0] * int(length * SR)
        fn(buf)
        dc_and_fade(buf)
        g = peak / (max(abs(x) for x in buf) or 1.0)
        out[name] = [x * g for x in buf]
    loop = vent_loop()
    g = 0.5 / max(abs(x) for x in loop)
    out["VentLoop"] = [x * g for x in loop]     # no fades: it must loop
    return out




def dc_and_fade(buf, guard=0.004):
    m = sum(buf) / max(1, len(buf))
    for i in range(len(buf)):
        buf[i] -= m
    n = max(1, min(len(buf), int(guard * SR)))   # 4 ms fade-in guard, last 10 ms already faded by builders
    for i in range(n):
        buf[i] *= i / n


def render(name, spec):
    buf = [0.0] * int(spec["length"] * SR)
    spec["fn"](buf)
    dc_and_fade(buf)
    peak = max(abs(x) for x in buf) or 1.0
    g = PEAK[spec["cls"]] / peak
    return [max(-1.0, min(1.0, x * g)) for x in buf]


def write_wav(path, samples):
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(round(x * 32767))) for x in samples))


def main():
    os.makedirs(OUT, exist_ok=True)
    print(f"{'event':18s} {'len ms':>7s} {'peak dBFS':>10s} {'rms dBFS':>9s}  class  relation")
    bad = False
    for name, spec in EVENTS.items():
        s = render(name, spec)
        write_wav(os.path.join(OUT, f"UI_{name}.wav"), s)
        pk = max(abs(x) for x in s)
        rms = math.sqrt(sum(x * x for x in s) / len(s))
        end = abs(s[-1])
        ok = pk <= 0.71 and end < 0.01 and len(s) / SR <= 1.2
        bad |= not ok
        print(f"{name:18s} {len(s) / SR * 1000:7.0f} {20 * math.log10(pk):10.1f} {20 * math.log10(rms):9.1f}  {spec['cls']:6s} {spec['rel']}"
              + ("" if ok else "  << CHECK"))
    if "--table" in sys.argv:   # markdown for docs/systems/UI_SOUND.md
        rows = ["| Event | Length | Peak | RMS | Class | Pitch and relation |", "|---|---|---|---|---|---|"]
        for name, spec in EVENTS.items():
            s = render(name, spec)
            pk = max(abs(x) for x in s)
            rms = math.sqrt(sum(x * x for x in s) / len(s))
            rows.append(f"| `{name}` | {len(s) / SR * 1000:.0f} ms | {20 * math.log10(pk):.1f} dBFS | {20 * math.log10(rms):.1f} dBFS | {spec['cls']} | {spec['rel']} |")
        print("\n".join(rows))
    print(f"\n{len(EVENTS)} files in {os.path.relpath(OUT)}")
    print(f"\n{'item slot':18s} {'len ms':>7s} {'peak dBFS':>10s} {'rms dBFS':>9s}")
    items = render_items()
    for name, smp in items.items():
        write_wav(os.path.join(ITEM_OUT, f"Item_{name}.wav"), smp)
        pk = max(abs(x) for x in smp)
        rms = math.sqrt(sum(x * x for x in smp) / len(smp))
        print(f"{name:18s} {len(smp) / SR * 1000:7.0f} {20 * math.log10(pk):10.1f} {20 * math.log10(rms):9.1f}")
    lp = items["VentLoop"]
    step, typical = abs(lp[0] - lp[-1]), sum(abs(lp[i + 1] - lp[i]) for i in range(len(lp) - 1)) / (len(lp) - 1)
    print(f"VentLoop seam: step {step:.4f} vs typical sample step {typical:.4f}" + ("" if step < typical * 4 else "  << CHECK"))
    bad |= step >= typical * 4
    print(f"{len(items)} files in {os.path.relpath(ITEM_OUT)}")
    print(f"\n{'weapon sound':18s} {'len ms':>7s} {'peak dBFS':>10s} {'rms dBFS':>9s}")
    os.makedirs(WEAPON_OUT, exist_ok=True)
    weapons = render_table(WEAPONS)
    for name, smp in weapons.items():
        write_wav(os.path.join(WEAPON_OUT, f"Weapon_{name}.wav"), smp)
        pk = max(abs(x) for x in smp)
        rms = math.sqrt(sum(x * x for x in smp) / len(smp))
        ok = abs(smp[-1]) < 0.01
        bad |= not ok
        print(f"{name:18s} {len(smp) / SR * 1000:7.0f} {20 * math.log10(pk):10.1f} {20 * math.log10(rms):9.1f}" + ("" if ok else "  << CHECK"))
    print(f"{len(weapons)} files in {os.path.relpath(WEAPON_OUT)}")
    print("audition page:", os.path.relpath(write_audition()))
    return 1 if bad else 0


def write_audition():
    """ArtSource/UI/review/10_sound_audition.html: every clip with a play button, for judging by ear.
    Opens straight from disk; the clips are referenced by relative path, nothing is embedded."""
    page = os.path.normpath(os.path.join(HERE, "..", "..", "UI", "review", "10_sound_audition.html"))
    os.makedirs(os.path.dirname(page), exist_ok=True)

    def rows(folder, prefix, table, describe):
        out = ""
        for name in table:
            src = os.path.relpath(os.path.join(folder, f"{prefix}_{name}.wav"), os.path.dirname(page)).replace(os.sep, "/")
            out += (f'<tr><td><button onclick="play(this)" data-src="{src}">Play</button></td>'
                    f"<td><b>{name}</b></td><td>{describe(name)}</td></tr>\n")
        return out
    item_desc = dict({k: v[2] for k, v in ITEMS.items()}, VentLoop="gas hiss, seamless loop (plays twice here to hear the seam)")
    body = "<h2>UI (31)</h2><table>" + rows(OUT, "UI", EVENTS, lambda n: f"{EVENTS[n]['length'] * 1000:.0f} ms · {EVENTS[n]['rel']}") + "</table>"
    body += "<h2>Items (12)</h2><table>" + rows(ITEM_OUT, "Item", item_desc, lambda n: item_desc[n]) + "</table>"
    body += "<h2>Weapons (15)</h2><table>" + rows(WEAPON_OUT, "Weapon", WEAPONS, lambda n: f"{WEAPONS[n][0] * 1000:.0f} ms · {WEAPONS[n][2]}") + "</table>"
    html = f"""<!doctype html><html lang="en"><head><meta charset="utf-8"><title>Seal the Deal: sound audition</title>
<style>body{{font:16px/1.5 -apple-system,sans-serif;color:#111B38;background:#F3F4F4;max-width:900px;margin:32px auto;padding:0 16px}}
h1{{font-weight:300;letter-spacing:.06em}}h2{{margin-top:32px;border-bottom:1px solid #C9CED3}}table{{border-collapse:collapse;width:100%}}
td{{padding:4px 12px 4px 0;vertical-align:middle}}button{{font:inherit;padding:4px 14px;border:1px solid #111B38;background:#fff;color:#111B38;border-radius:4px;cursor:pointer}}
button:hover{{background:#244FB9;color:#F3F4F4}}p{{max-width:70ch}}</style></head><body>
<h1>SEAL THE DEAL · sound audition</h1>
<p>Every clip the generator makes. <b>Nobody has listened to these yet</b>: they were synthesised and measured only. Press Play, or use
"Play the hover run" to hear five hovers climb the scale as they do in the menu. Note any that are harsh, too long, too alike or wrong for the event.</p>
<p><button onclick="run()">Play the hover run</button> <button onclick="burst()">Play AR-15 at 10 a second</button></p>
{body}
<script>
function play(b){{const a=new Audio(b.dataset.src);a.play();if(b.dataset.src.includes('VentLoop'))a.onended=()=>new Audio(b.dataset.src).play();}}
function run(){{[0,2,4,7,9].forEach((s,i)=>setTimeout(()=>{{const a=new Audio('{os.path.relpath(os.path.join(OUT, "UI_HoverMove.wav"), os.path.dirname(page)).replace(os.sep, "/")}');a.preservesPitch=false;a.playbackRate=Math.pow(2,s/12);a.play();}},i*140));}}
function burst(){{for(let i=0;i<10;i++)setTimeout(()=>new Audio('{os.path.relpath(os.path.join(WEAPON_OUT, "Weapon_AR15Shot.wav"), os.path.dirname(page)).replace(os.sep, "/")}').play(),i*100);}}
</script></body></html>
"""
    with open(page, "w", encoding="utf-8") as fh:
        fh.write(html)
    return page


if __name__ == "__main__":
    sys.exit(main())
