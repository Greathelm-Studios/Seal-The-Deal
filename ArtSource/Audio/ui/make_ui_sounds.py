"""Seal the Deal: the UI sound set, synthesised from nothing (Python standard library only).

    python3 ArtSource/Audio/ui/make_ui_sounds.py            # writes Assets/_Project/Audio/UI/*.wav and prints the table

Every sound is a few sine / triangle partials with a fast exponential decay and, sometimes, a little
band-limited noise for air. Nothing is sampled, downloaded or copied. Output: 16-bit, 48 kHz, mono.

Voice: a clean 2001 corporate future. Glassy ticks, soft tones, sparse. Pitches live in one key (C major
pentatonic: C D E G A) so any run of sounds is consonant: hover steps climb the scale, confirm resolves
upward, back and error resolve downward, nothing is harsh or long.

Levels: every clip is levelled to a perceived target (LOUD, LOUD_EVENT, ITEM_LOUD, WEAPON_LOUD: the A-weighted level
of its loudest 46 ms), under a peak ceiling (PEAK and the tables' second column). To make a sound louder or quieter,
change its target, not the ceiling. --analyse prints every clip's level and the UI pairs that sound alike.
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


def b_tab(b):     # glass tick + a breath of air (A5: its own pitch, apart from focus on E5)
    note(b, 0, hz("A5"), 0.06, 0.8, 0.018, "tri", glass=0.35)
    air(b, 0, 0.09, 0.22, 2500, 7000, sweep=(2500, 6000, 3500, 8000), decay=0.03, seed=4)


def b_toggle_on(b):
    note(b, 0, hz("C5"), 0.05, 0.8, 0.018, "tri", glass=0.15)
    note(b, 0.045, hz("G5"), 0.08, 1.0, 0.025, "sine", glass=0.25)


def b_toggle_off(b):   # A5 to E5: a lighter fall than Back's G5 to C5, so the two never read as the same answer
    note(b, 0, hz("A5"), 0.05, 0.7, 0.018, "tri", glass=0.1)
    note(b, 0.045, hz("E5"), 0.08, 0.8, 0.025, "tri", glass=0.25)


def b_slider(b):  # a tiny tick; pitch follows the value at run time
    note(b, 0, hz("C6"), 0.028, 0.9, 0.008, "tri", glass=0.3)


def b_dd_open(b):  # air swishes up, a tone lands on top
    air(b, 0, 0.12, 0.25, 1800, 5000, sweep=(1500, 4000, 2500, 7000), decay=0.05, seed=5)
    note(b, 0.05, hz("A5"), 0.09, 0.8, 0.03, "sine", glass=0.2)


def b_dd_close(b):
    air(b, 0, 0.11, 0.22, 1800, 5000, sweep=(2500, 7000, 1500, 4000), decay=0.045, seed=6)
    note(b, 0.0, hz("A5"), 0.05, 0.6, 0.02, "sine", glass=0.1)
    note(b, 0.045, hz("D5"), 0.09, 0.7, 0.03, "sine", glass=0.1)


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


def b_ready(b):    # three quick steps up, D5 G5 D6: longer and higher than a dropdown opening
    note(b, 0, hz("D5"), 0.07, 0.8, 0.03, "sine", glass=0.15)
    note(b, 0.06, hz("G5"), 0.08, 0.9, 0.03, "sine", glass=0.2)
    note(b, 0.12, hz("D6"), 0.10, 0.95, 0.04, "sine", glass=0.3)


def b_unready(b):  # the same steps back down, D6 G5 D5
    note(b, 0, hz("D6"), 0.06, 0.7, 0.025, "sine", glass=0.15)
    note(b, 0.055, hz("G5"), 0.07, 0.75, 0.03, "sine", glass=0.1)
    note(b, 0.11, hz("D5"), 0.09, 0.8, 0.035, "sine")


def b_count_tick(b):  # a clear, short tick an octave above the menu's ticks (C6), with a woody knock under it
    note(b, 0, hz("C6"), 0.05, 0.9, 0.012, "tri", glass=0.4)
    note(b, 0, hz("C4"), 0.03, 0.5, 0.008, "sine")
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


def b_title(b):    # the title appears: a soft chord blooms upward, C E G then D6, with a breath of air
    for n, t, a in (("C5", 0.00, 0.6), ("E5", 0.08, 0.6), ("G5", 0.16, 0.65), ("D6", 0.30, 0.55)):
        note(b, t, hz(n), 1.05 - t, a, 0.32, "sine", glass=0.18, attack=0.03)
    note(b, 0.0, hz("C4"), 0.9, 0.35, 0.3, "sine", attack=0.05)
    swell(b, 0.0, 0.6, 0.25, 1500, 4000, 3000, 9000, seed=71)


def b_tr_cloud(b):   # cloud wipe: a soft swish across, a faint high tone riding it
    swell(b, 0.0, 0.28, 0.9, 900, 3500, 2200, 8000, seed=72)
    note(b, 0.06, hz("A5"), 0.2, 0.12, 0.08, "sine", glide=(hz("G5"), hz("A5")))


def b_tr_pane(b):    # pane slide: three glass panes shut in quick taps, then slide away
    for k, n in enumerate(("E6", "D6", "C6")):
        note(b, k * 0.035, hz(n), 0.06, 0.6, 0.014, "tri", glass=0.45, attack=0.0008)
    swell(b, 0.1, 0.16, 0.5, 2000, 6000, 1200, 4000, seed=73)


def b_tr_zoom(b):    # frame zoom = the game starting: a rising whoosh that lands on a bright C chord
    swell(b, 0.0, 0.6, 1.0, 300, 1500, 2500, 9000, seed=74)
    note(b, 0.0, hz("C4"), 0.55, 0.25, 0.4, "sine", glide=(hz("C4") * 0.75, hz("C4")), attack=0.2)
    for n, t in (("C5", 0.52), ("G5", 0.55), ("C6", 0.58), ("E6", 0.62)):
        note(b, t, hz(n), 0.85 - t, 0.75, 0.12, "sine", glass=0.3, attack=0.004)


def b_tr_fade(b):    # reduced-motion fade: a small puff of air, nothing more
    swell(b, 0.0, 0.12, 0.6, 1500, 5000, 2500, 7000, seed=75)


EVENTS = {
    # name: length s, class, relation, builder
    "HoverMove": E(0.10, "ui", "G5; run steps up C D E G A", b_hover),
    "Focus": E(0.10, "ui", "E5, softer than hover", b_focus),
    "Select": E(0.18, "ui", "C5 to G5, rises a fifth", b_select),
    "Confirm": E(0.26, "ui", "C5 G5 C6, rises (resolves up)", b_confirm),
    "Back": E(0.20, "ui", "G5 to C5, falls", b_back),
    "Error": E(0.30, "ui", "E4 to C4, falls (dull, soft)", b_error),
    "TabChange": E(0.12, "ui", "A5 glass tick + air", b_tab),
    "ToggleOn": E(0.14, "ui", "C5 to G5, quick rise", b_toggle_on),
    "ToggleOff": E(0.14, "ui", "A5 to E5, light fall", b_toggle_off),
    "SliderTick": E(0.05, "ui", "C6 tick; runtime pitch follows value", b_slider),
    "DropdownOpen": E(0.16, "ui", "air swish up + A5", b_dd_open),
    "DropdownClose": E(0.14, "ui", "air swish down + A5 to D5", b_dd_close),
    "KeyBindListen": E(0.20, "ui", "E5 double pulse (waiting)", b_kb_listen),
    "KeyBindSet": E(0.30, "ui", "C6 ding over G5", b_kb_set),
    "PanelOpen": E(0.30, "ui", "C5 + G5 rising sweep", b_panel_open),
    "PanelClose": E(0.26, "ui", "G5 + C5 falling sweep", b_panel_close),
    "Transition": E(0.48, "ui", "noise whoosh up; C5 under", b_transition),
    "Notification": E(0.46, "ui", "G5 then D6, rises a fifth", b_notify),
    "LobbyReady": E(0.24, "ui", "D5 G5 D6, three steps up", b_ready),
    "LobbyUnready": E(0.22, "ui", "D6 G5 D5, three steps down", b_unready),
    "CountdownTick": E(0.07, "ui", "C6 tick over a C4 knock", b_count_tick),
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
    "TitleAppear": E(1.10, "sting", "C5 E5 G5 bloom to D6 over C4, airy", b_title),
    "TransitionCloudWipe": E(0.30, "ui", "soft swish across + A5", b_tr_cloud),
    "TransitionPaneSlide": E(0.28, "ui", "three glass taps E6 D6 C6 + slide", b_tr_pane),
    "TransitionFrameZoom": E(0.86, "sting", "rising whoosh landing on C5 G5 C6 E6: the game starts", b_tr_zoom),
    "TransitionFade": E(0.13, "ui", "small puff of air (reduced motion)", b_tr_fade),
}
PEAK = {"ui": 0.70, "sting": 0.80, "combat": 0.90}      # peak ceiling per class (linear full scale)
LOUD = {"ui": -14.0, "sting": -11.0, "combat": -11.0}    # target perceived level per class, dB (A-weighted, loudest 46 ms)
LOUD_EVENT = {"KillConfirm": -9.0,                        # the kill sits above the hit ticks it plays over
              "SliderTick": -18.0, "CountdownTick": -15.0, "HitMarkerHead": -11.0}  # repeated ticks sit a little under the menu

# ------------------------------------------------------------------------------------------ item sounds
# World sounds for the pickups lane (3D, SFX bus): Assets/_Project/Audio/Items/Item_<Slot>.wav. Full strength:
# the game scales impact volume with speed.
def i_pickup(b):   # cloth-and-plastic grab: short low-mid noise + a soft knock
    air(b, 0, 0.09, 0.6, 500, 3000, decay=0.02, seed=21)
    note(b, 0.01, 180.0, 0.10, 0.6, 0.03, "sine", glide=(240.0, 170.0))


def i_drop(b):     # let go: a duller knock, then a small second bounce
    air(b, 0, 0.08, 1.0, 300, 2500, decay=0.022, seed=22)
    note(b, 0, 130.0, 0.12, 0.8, 0.035, "sine", glide=(190.0, 120.0))
    air(b, 0.11, 0.05, 0.25, 300, 1800, decay=0.012, seed=23)


def i_throw(b):    # whoosh: noise swell, band falling as the item leaves
    swell(b, 0, 0.2, 1.0, 900, 5000, 300, 1800, seed=24)


def i_impact_light(b):  # thin clack
    air(b, 0, 0.05, 0.9, 900, 6000, decay=0.010, seed=25, attack=0.0005)
    note(b, 0, 420.0, 0.09, 0.5, 0.02, "tri", glide=(560.0, 400.0), attack=0.0005)


def i_impact_heavy(b):  # thud with weight: low sine drop + broad noise
    air(b, 0, 0.14, 1.2, 200, 3000, decay=0.035, seed=26, attack=0.0005)
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
    air(b, 0.0, 0.7, 0.9, 500, 5000, sweep=(800, 7000, 300, 1500), decay=0.16, seed=32, attack=0.0005)   # the roar


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
    "PickUp": (0.15, 0.90, "grab: soft noise + knock", i_pickup),
    "Drop": (0.20, 0.90, "dull knock + small bounce", i_drop),
    "Throw": (0.20, 0.90, "whoosh, band falling", i_throw),
    "ImpactLight": (0.12, 0.90, "thin clack, full strength", i_impact_light),
    "ImpactHeavy": (0.35, 0.90, "low thud 140 to 55 Hz, full strength", i_impact_heavy),
    "Arm": (0.10, 0.90, "two-part click", i_arm),
    "Beep": (0.05, 0.90, "2.1 kHz, 50 ms", i_beep),
    "Explosion": (1.10, 0.97, "crack + boom 120 to 32 Hz + rumble tail", i_explosion),
    "VentEnd": (0.40, 0.90, "hiss falling away", i_vent_end),
    "WetFloorDown": (0.32, 0.90, "plastic clack + splash", i_wet_floor),
    "Slip": (0.30, 0.90, "squeak 700 to 1900 Hz + scuff", i_slip),
}

# ---------------------------------------------------------------------------------------- weapon sounds
# World sounds for the shooting lane (3D at the muzzle or hit point, SFX bus; Hurt / Death / Respawn are 2D):
# Assets/_Project/Audio/Weapons/Weapon_<Name>.wav. Events and timing: docs/systems/WEAPONS.md.
def w_pistol(b):    # sharp crack, a low punch, short room tail
    air(b, 0, 0.03, 1.0, 1500, 12000, decay=0.006, seed=41, attack=0.0002)
    air(b, 0, 0.12, 1.0, 600, 5000, decay=0.03, seed=141, attack=0.0003)      # the body of the shot: mid-band, 30 ms
    note(b, 0, 90.0, 0.12, 0.9, 0.03, "sine", glide=(220.0, 70.0), attack=0.0005)
    air(b, 0.004, 0.24, 0.5, 300, 5000, sweep=(400, 7000, 200, 1500), decay=0.055, seed=42, attack=0.001)


def w_ar15(b):      # tighter and brighter, almost no tail: 10 a second must not smear
    air(b, 0, 0.025, 1.0, 2000, 14000, decay=0.005, seed=43, attack=0.0002)
    air(b, 0, 0.08, 1.0, 800, 6000, decay=0.018, seed=143, attack=0.0003)     # body: shorter than the pistol's
    note(b, 0, 110.0, 0.07, 0.8, 0.018, "sine", glide=(260.0, 90.0), attack=0.0005)
    air(b, 0.003, 0.09, 0.4, 500, 6000, sweep=(600, 8000, 300, 2500), decay=0.022, seed=44, attack=0.001)


def w_dry(b):       # dull click
    air(b, 0, 0.04, 0.8, 600, 3500, decay=0.012, seed=45, attack=0.0003)
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
    air(b, 0.0, 0.11, 0.7, 1500, 7000, decay=0.03, seed=60, attack=0.0005)


def w_impact_body(b):   # dull thud, no brightness
    air(b, 0, 0.08, 1.0, 250, 2200, decay=0.022, seed=61, attack=0.0005)
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
    air(b, 0, 0.1, 0.6, 400, 2500, decay=0.03, seed=65)


def w_death(b):     # a falling tone that drops away under a soft noise close
    note(b, 0, hz("E4"), 0.7, 0.8, 0.25, "tri", glide=(hz("E4"), hz("C4") / 2))
    note(b, 0, 60.0, 0.4, 0.8, 0.12, "sine", glide=(110.0, 50.0), attack=0.002)
    air(b, 0, 0.7, 0.2, 300, 2500, sweep=(600, 4000, 150, 600), decay=0.3, seed=66)


def w_respawn(b):   # the lift chime's cousin: rising fifth with air, back on the floor
    air(b, 0, 0.2, 0.2, 1500, 6000, sweep=(800, 3000, 2500, 8000), decay=0.08, seed=67)
    note(b, 0.05, hz("C5"), 0.3, 0.8, 0.09, "sine", glass=0.2)
    note(b, 0.17, hz("G5"), 0.33, 0.9, 0.11, "sine", glass=0.25)


WEAPONS = {  # name: (length s, peak, description, builder)
    "PistolShot": (0.26, 0.97, "crack + punch 220 to 70 Hz + 0.2 s room tail", w_pistol),
    "AR15Shot": (0.10, 0.97, "tighter crack, 100 ms so 10 a second stays clean", w_ar15),
    "DryFire": (0.06, 0.90, "dull click", w_dry),
    "ReloadMagOut": (0.11, 0.90, "slide + release click", w_mag_out),
    "ReloadMagIn": (0.14, 0.90, "slide + solid seat", w_mag_in),
    "ReloadSlide": (0.13, 0.90, "pistol slide: back click, forward clack", w_slide),
    "ReloadCharge": (0.21, 0.90, "rifle charging handle: pull, heavy return", w_charge),
    "ReloadFinished": (0.06, 0.90, "small settled click", w_reload_done),
    "Swap": (0.22, 0.90, "swish + catch", w_swap),
    "ImpactWorld": (0.13, 0.90, "hard tick + debris", w_impact_world),
    "ImpactBody": (0.12, 0.90, "dull thud 170 to 85 Hz", w_impact_body),
    "GlassShatter": (0.70, 0.95, "crack + 26 falling glass partials + bright wash", w_glass),
    "Hurt": (0.22, 0.90, "low pulse 120 to 75 Hz, 2D", w_hurt),
    "Death": (0.75, 0.90, "tone falling E4 to C3 over a low drop, 2D", w_death),
    "Respawn": (0.52, 0.90, "C5 to G5 rise with air, 2D", w_respawn),
}
# Shots are the loudest thing in the set; handling sounds sit well under them.
# A shot is a few milliseconds long, so it tops out at its peak ceiling (about -15 and -20 dB on this measure) before it
# reaches the target; everything else is set under that so the order stays shot > glass > impacts > handling.
WEAPON_LOUD = {"PistolShot": -7.0, "AR15Shot": -8.0, "GlassShatter": -17.0, "ImpactWorld": -20.0, "ImpactBody": -21.0,
               "DryFire": -25.0, "ReloadMagOut": -24.0, "ReloadMagIn": -23.0, "ReloadSlide": -23.0, "ReloadCharge": -23.0,
               "ReloadFinished": -27.0, "Swap": -25.0, "Hurt": -19.0, "Death": -17.0, "Respawn": -17.0}
WEAPON_OUT = os.path.normpath(os.path.join(OUT, "..", "Weapons"))


def render_table(table):
    out = {}
    for name, (length, peak, _, fn) in table.items():
        buf = [0.0] * int(length * SR)
        fn(buf)
        dc_and_fade(buf, guard=0.0005)   # world transients keep their attack
        out[name] = level(buf, WEAPON_LOUD.get(name, -14.0), peak)
    return out

ITEM_LOUD = {"Explosion": -7.0, "ImpactHeavy": -11.0, "ImpactLight": -13.0, "Beep": -13.0, "Slip": -13.0, "Arm": -16.0}
ITEM_OUT = os.path.normpath(os.path.join(OUT, "..", "Items"))


def render_items():
    os.makedirs(ITEM_OUT, exist_ok=True)
    out = {}
    for name, (length, peak, _, fn) in ITEMS.items():
        buf = [0.0] * int(length * SR)
        fn(buf)
        dc_and_fade(buf)
        out[name] = level(buf, ITEM_LOUD.get(name, -14.0), peak)
    loop = vent_loop()
    out["VentLoop"] = level(loop, -17.0, 0.9)   # a bed under everything; no fades: it must loop
    return out




def dc_and_fade(buf, guard=0.004):
    m = sum(buf) / max(1, len(buf))
    for i in range(len(buf)):
        buf[i] -= m
    n = max(1, min(len(buf), int(guard * SR)))   # 4 ms fade-in guard, last 10 ms already faded by builders
    for i in range(n):
        buf[i] *= i / n


# ------------------------------------------------------------------------------------------- analysis
# Numbers that stand in for ears until someone listens: how loud each clip is to a listener (A-weighted, loudest
# 46 ms), where its energy sits (spectral centroid), and which pairs are close enough to be confused.
N_FFT = 2048


def _fft(re, im):
    n = len(re)
    j = 0
    for i in range(1, n):
        bit = n >> 1
        while j & bit:
            j ^= bit
            bit >>= 1
        j |= bit
        if i < j:
            re[i], re[j] = re[j], re[i]
            im[i], im[j] = im[j], im[i]
    size = 2
    while size <= n:
        ang = -2 * math.pi / size
        wr, wi = math.cos(ang), math.sin(ang)
        for start in range(0, n, size):
            cr, ci = 1.0, 0.0
            for k in range(size // 2):
                a, b = start + k, start + k + size // 2
                tr, ti = re[b] * cr - im[b] * ci, re[b] * ci + im[b] * cr
                re[b], im[b] = re[a] - tr, im[a] - ti
                re[a], im[a] = re[a] + tr, im[a] + ti
                cr, ci = cr * wr - ci * wi, cr * wi + ci * wr
        size *= 2


def _a_weight(f):
    if f < 10:
        return 0.0
    f2 = f * f
    ra = (12194.0 ** 2 * f2 * f2) / ((f2 + 20.6 ** 2) * math.sqrt((f2 + 107.7 ** 2) * (f2 + 737.9 ** 2)) * (f2 + 12194.0 ** 2))
    return (ra * 1.2589) ** 2   # power gain, 0 dB at 1 kHz


_AW = [_a_weight(k * SR / N_FFT) for k in range(N_FFT // 2)]
_HANN = [0.5 - 0.5 * math.cos(2 * math.pi * i / N_FFT) for i in range(N_FFT)]
BANDS = 24   # log bands 100 Hz .. 16 kHz, for comparing timbre


def analyse(samples):
    """dict(loud: A-weighted level of the loudest frame in dB, centroid: Hz, bands: normalised band energies,
    env: 8-step level envelope) for one clip."""
    pad = list(samples) + [0.0] * N_FFT
    best, total, cen_num, cen_den = 0.0, [0.0] * BANDS, 0.0, 0.0
    frames = []
    lo, hi = math.log(100.0), math.log(16000.0)
    for start in range(0, max(1, len(samples)), N_FFT // 2):
        re = [pad[start + i] * _HANN[i] for i in range(N_FFT)]
        im = [0.0] * N_FFT
        _fft(re, im)
        aw = 0.0
        for k in range(1, N_FFT // 2):
            p = re[k] * re[k] + im[k] * im[k]
            f = k * SR / N_FFT
            aw += p * _AW[k]
            cen_num += p * f
            cen_den += p
            if 100.0 <= f < 16000.0:
                total[int((math.log(f) - lo) / (hi - lo) * BANDS)] += p
        frames.append(aw)
        best = max(best, aw)
    norm = math.sqrt(sum(x * x for x in total)) or 1.0
    env = [0.0] * 8
    for i, v in enumerate(frames):
        env[min(7, i * 8 // len(frames))] += v
    en = math.sqrt(sum(x * x for x in env)) or 1.0
    scale = sum(h * h for h in _HANN) * N_FFT / 4      # so a full-scale sine reads about 0 dB
    return dict(loud=10 * math.log10(best / scale + 1e-12), centroid=cen_num / (cen_den or 1.0),
                bands=[x / norm for x in total], env=[x / en for x in env])


def level(buf, target_db, peak_cap):
    """Scale a clip to a perceived level (analyse()['loud'] == target_db), but never above peak_cap: a clip too short
    or too peaky to reach the target is left at the ceiling, and the table shows by how much it falls short."""
    pk = max(abs(x) for x in buf) or 1.0
    unit = [x / pk for x in buf]
    g = 10 ** ((target_db - analyse(unit)["loud"]) / 20)
    g = min(g, peak_cap)
    return [x * g for x in unit]


def render(name, spec):
    buf = [0.0] * int(spec["length"] * SR)
    spec["fn"](buf)
    dc_and_fade(buf)
    return level(buf, LOUD_EVENT.get(name, LOUD[spec["cls"]]), PEAK[spec["cls"]])


def write_wav(path, samples):
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(round(x * 32767))) for x in samples))


# --------------------------------------------------------------------------------------------- ambient bed
# One quiet bed for the title and the lobby (Music bus, ships at level 0: the owner raises it). Seamless: every tone is
# snapped to a whole number of cycles over the loop, every slow movement's period divides the loop, the bell tails
# wrap round the end, and only the noise layer is crossfaded. Rendered only with --bed or when the file is missing
# (it takes about a minute in pure Python).
BED_SECONDS = 64
BED_OUT = os.path.normpath(os.path.join(OUT, "..", "Ambience", "Ambience_MenuBed.wav"))


def render_bed():
    n = BED_SECONDS * SR
    out = [0.0] * n
    L = float(BED_SECONDS)

    def snap(f):       # a whole number of cycles in the loop: no step at the seam
        return round(f * L) / L

    chords = [("C4", "G4", "E5", "D5"), ("A3", "E4", "C5", "G5"), ("G3", "D4", "A4", "E5"), ("C4", "E4", "G4", "A4")]
    section = L / len(chords)
    for ci, chord in enumerate(chords):
        centre = (ci + 0.5) * section
        for vi, name in enumerate(chord):
            f = snap(hz(name))
            f2 = snap(f * 2.0)
            amp = 0.16 if vi == 0 else 0.10
            for i in range(n):
                t = i / SR
                d = (t - centre + L / 2) % L - L / 2                       # cyclic distance from the chord's centre
                if abs(d) > section:
                    continue
                w = 0.5 + 0.5 * math.cos(math.pi * d / section)           # raised cosine, overlaps its neighbours
                trem = 1.0 + 0.06 * math.sin(2 * math.pi * t / 8.0 + vi)   # 8 s divides the loop
                out[i] += amp * w * trem * (math.sin(2 * math.pi * f * t) + 0.25 * math.sin(2 * math.pi * f2 * t))
    # sparse glass bells, pentatonic, high and soft; tails wrap round the loop end
    rng = random.Random(81)
    bells = ["C6", "D6", "E6", "G6", "A6"]
    t = 1.5
    while t < L:
        f = hz(bells[rng.randrange(len(bells))])
        m = int(2.5 * SR)
        s0 = int(t * SR)
        for k in range(m):
            tt = k / SR
            env = math.exp(-tt / 0.6) * min(1.0, tt / 0.004)
            v = math.sin(2 * math.pi * f * tt) + 0.3 * math.sin(2 * math.pi * f * 2.76 * tt) * math.exp(-tt / 0.15)
            out[(s0 + k) % n] += 0.05 * env * v
        t += rng.uniform(3.0, 6.5)
    # air: band-limited noise, slow swell (period 16 s), crossfaded seam
    x = int(2.0 * SR)
    rng2 = Rng(82)
    a = b = 0.0
    ca, cb = 1 - math.exp(-2 * math.pi * 6000 / SR), 1 - math.exp(-2 * math.pi * 1200 / SR)
    raw = [0.0] * (n + x)
    for i in range(n + x):
        v = rng2.noise()
        a += ca * (v - a)
        b += cb * (v - b)
        raw[i] = (a - b) * (0.5 + 0.5 * math.sin(2 * math.pi * (i % n) / SR / 16.0)) * 0.06
    for i in range(n):
        if i < x:
            k = i / x
            out[i] += raw[i] * math.sqrt(k) + raw[n + i] * math.sqrt(1 - k)
        else:
            out[i] += raw[i]
    m = sum(out) / n
    out = [v - m for v in out]
    pk = max(abs(v) for v in out)
    return [v * (0.5 / pk) for v in out]


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
        ok = pk <= 0.91 and end < 0.01 and len(s) / SR <= 1.2
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
    if "--bed" in sys.argv or not os.path.exists(BED_OUT):
        os.makedirs(os.path.dirname(BED_OUT), exist_ok=True)
        bed = render_bed()
        write_wav(BED_OUT, bed)
        seam = abs(bed[0] - bed[-1])
        typical = max(abs(bed[i + 1] - bed[i]) for i in list(range(0, 2000)) + list(range(len(bed) - 2001, len(bed) - 1)))
        rms = math.sqrt(sum(v * v for v in bed) / len(bed))
        print(f"ambient bed: {BED_SECONDS} s, peak -6.0 dBFS, rms {20 * math.log10(rms):.1f} dBFS, seam step {seam:.4f} vs largest step near it {typical:.4f}"
              + ("" if seam <= typical else "  << CHECK"))
        bad |= seam > typical
    print("audition page:", os.path.relpath(write_audition()))
    if "--analyse" in sys.argv:
        analysis_report()
    return 1 if bad else 0


def analysis_report():
    """Prints loudness per clip, the spread per class, and pairs of UI clips similar enough to be confused."""
    sets = [("UI", {n: (render(n, sp), sp["cls"], sp["length"]) for n, sp in EVENTS.items()})]
    items = render_items()
    sets.append(("Items", {n: (smp, "item", len(smp) / SR) for n, smp in items.items()}))
    sets.append(("Weapons", {n: (smp, "weapon", len(smp) / SR) for n, smp in render_table(WEAPONS).items()}))
    lines, warn = [], 0
    for title, clips in sets:
        feats = {n: analyse(smp) for n, (smp, _, _) in clips.items()}
        lines.append(f"\n{title}: perceived level (A-weighted, loudest 46 ms) and brightness")
        lines.append(f"{'clip':18s} {'class':7s} {'loud dB':>8s} {'centroid Hz':>12s}   (a note in brackets = short of its target)")
        by_cls = {}
        for n, (_, cls, _) in clips.items():
            by_cls.setdefault(cls, []).append(feats[n]["loud"])
        for n, (_, cls, _) in clips.items():
            want = (LOUD_EVENT.get(n, LOUD.get(cls)) if title == "UI" else
                    ITEM_LOUD.get(n, -17.0 if n == "VentLoop" else -14.0) if title == "Items" else WEAPON_LOUD.get(n, -14.0))
            off = feats[n]["loud"] - want
            flag = ""
            if abs(off) > 1.5:
                flag = f"  ({off:+.1f} dB from its target {want:.0f}: at its peak ceiling)"
                warn += 1
            lines.append(f"{n:18s} {cls:7s} {feats[n]['loud']:8.1f} {feats[n]['centroid']:12.0f}{flag}")
        for cls, vals in by_cls.items():
            lines.append(f"  {cls}: spread {max(vals) - min(vals):.1f} dB ({min(vals):.1f} to {max(vals):.1f})")
        if title != "UI":
            continue
        lines.append("\nUI pairs close enough to be confused (same length within 30 %, timbre and shape both above 0.93):")
        names, found = list(clips), 0
        for i, a in enumerate(names):
            for b in names[i + 1:]:
                la, lb = clips[a][2], clips[b][2]
                if min(la, lb) / max(la, lb) < 0.7:
                    continue
                tim = sum(x * y for x, y in zip(feats[a]["bands"], feats[b]["bands"]))
                shp = sum(x * y for x, y in zip(feats[a]["env"], feats[b]["env"]))
                if tim > 0.93 and shp > 0.93:
                    lines.append(f"  {a} / {b}: timbre {tim:.2f}, shape {shp:.2f}")
                    found += 1
        if not found:
            lines.append("  none")
    print("\n".join(lines))
    return warn


# Where each sound plays in the game, for the audition page: (heading, [(clip prefix, name, what the player did)]).
PLACES = [
    ("Title and menus", [("UI", "TitleAppear", "the title appears (once, not when a dialog closes)"),
                         ("UI", "HoverMove", "mouse over a button (a run climbs C D E G A)"),
                         ("UI", "Focus", "arrow keys or pad move focus"),
                         ("UI", "Select", "button pressed down"), ("UI", "Confirm", "button released: it does its thing"),
                         ("UI", "Back", "Back, Esc, Stay in a dialog"), ("UI", "Error", "a greyed-out control pressed"),
                         ("UI", "PanelOpen", "a dialog opens (Quit to desktop?)"), ("UI", "PanelClose", "a dialog closes"),
                         ("UI", "Transition", "a screen opens over another (Settings)"),
                         ("UI", "Notification", "Settings > Audio test button; match warnings")]),
    ("Settings", [("UI", "TabChange", "Q / E or a tab clicked"), ("UI", "ToggleOn", "a toggle turned on"),
                  ("UI", "ToggleOff", "a toggle turned off"), ("UI", "SliderTick", "a slider or stepper step (pitch follows the value)"),
                  ("UI", "KeyBindListen", "Controls: a key cell waits for a key"), ("UI", "KeyBindSet", "Controls: the new key is saved")]),
    ("Screen transitions", [("UI", "TransitionCloudWipe", "cloud wipe: an ordinary screen change"),
                            ("UI", "TransitionPaneSlide", "pane slide: back and forward"),
                            ("UI", "TransitionFrameZoom", "frame zoom: the game starts"),
                            ("UI", "TransitionFade", "the plain fade used when Reduce motion is on")]),
    ("Lobby reception", [("UI", "LobbyReady", "you are ready"), ("UI", "LobbyUnready", "you are not ready")]),
    ("Match HUD", [("UI", "CountdownTick", "3, 2, 1"), ("UI", "CountdownGo", "go"), ("UI", "FloorOpened", "a floor opens"),
                   ("UI", "BriefcaseTaken", "the briefcase is taken"), ("UI", "BriefcaseDropped", "the briefcase is dropped"),
                   ("UI", "HandsFull", "can't pick up: hands full"), ("UI", "MatchWon", "your firm wins"), ("UI", "MatchLost", "your firm loses")]),
    ("Combat feedback", [("UI", "HitMarker", "your shot hit"), ("UI", "HitMarkerHead", "your shot hit the head"),
                         ("UI", "KillConfirm", "your shot downed them")]),
    ("Not played anywhere yet", [("UI", "DropdownOpen", "the kit has no dropdown control"),
                                 ("UI", "DropdownClose", "the kit has no dropdown control")]),
]

# "Walk through the menu": a player's first minute, timed like play: (seconds, prefix, name, semitones, note).
WALK = [(0.0, "UI", "TitleAppear", 0, "The title appears; the ambient bed fades in under it"),
        (1.7, "UI", "HoverMove", 0, "Mouse over Host game"), (1.95, "UI", "HoverMove", 2, "over the next button"),
        (2.2, "UI", "HoverMove", 4, "over Settings"),
        (2.9, "UI", "Focus", 0, "Arrow down"), (3.3, "UI", "Focus", 0, "Arrow down"),
        (3.9, "UI", "Select", 0, "Press Settings"), (4.05, "UI", "Confirm", 0, "Release"),
        (4.05, "UI", "Transition", 0, "Settings opens"),
        (5.1, "UI", "TabChange", 0, "E: next tab")] + \
       [(5.9 + k * 0.09, "UI", "SliderTick", -5 + 17 * (0.5 + 0.08 * k), "Drag a volume slider up" if k == 0 else "")
        for k in range(6)] + \
       [(7.0, "UI", "ToggleOff", 0, "Turn a toggle off"), (7.6, "UI", "ToggleOn", 0, "and on again"),
        (8.4, "UI", "TabChange", 0, "Q: Controls"), (9.1, "UI", "Select", 0, "Press the Jump key cell"),
        (9.25, "UI", "KeyBindListen", 0, "It waits for a key"), (10.4, "UI", "Error", 0, "G is already used: the clash"),
        (10.4, "UI", "PanelOpen", 0, "The clash dialog opens"), (11.5, "UI", "KeyBindSet", 0, "Assign: the key is saved"),
        (12.5, "UI", "Back", 0, "Esc: back to the title"),
        (13.4, "UI", "Focus", 0, "Arrow up to Host game"), (13.9, "UI", "Select", 0, "Press Host game"),
        (14.05, "UI", "Confirm", 0, "Release"),
        (14.1, "UI", "TransitionFrameZoom", 0, "The game starts: the frame zooms in and the bed fades out"),
        (16.0, "UI", "CountdownTick", 0, "3"), (17.0, "UI", "CountdownTick", 0, "2"), (18.0, "UI", "CountdownTick", 0, "1"),
        (19.0, "UI", "CountdownGo", 0, "Go")]


def write_audition():
    """ArtSource/UI/review/10_sound_audition.html: every clip grouped by where it plays, a "walk through the menu" that
    plays them in the order and timing a player meets them, the ambient bed, and the item and weapon sounds.
    Clips are <audio> elements on relative paths, so the page also works opened straight from disk."""
    page = os.path.normpath(os.path.join(HERE, "..", "..", "UI", "review", "10_sound_audition.html"))
    os.makedirs(os.path.dirname(page), exist_ok=True)
    folder = {"UI": OUT, "Item": ITEM_OUT, "Weapon": WEAPON_OUT}

    def src(prefix, name):
        return os.path.relpath(os.path.join(folder[prefix], f"{prefix}_{name}.wav"), os.path.dirname(page)).replace(os.sep, "/")

    def esc(t):
        return t.replace("&", "&amp;").replace("<", "&lt;").replace('"', "&quot;")

    def section(title, rows, extra=""):
        out = f"<h2>{esc(title)}</h2>{extra}<table>"
        for prefix, name, what in rows:
            out += (f'<tr><td><button class="p" data-src="{src(prefix, name)}">Play</button></td>'
                    f"<td><b>{name}</b></td><td>{esc(what)}</td></tr>\n")
        return out + "</table>"

    bed = os.path.relpath(BED_OUT, os.path.dirname(page)).replace(os.sep, "/")
    body = ""
    for title, rows in PLACES:
        body += section(title, rows)
    body += ('<h2>Ambient bed (title and lobby)</h2><p>64 s, seamless, on the Music bus. <b>It ships switched off</b> '
             '(level 0 in UiSoundSet); this is how it would sound raised to a quiet level.</p>'
             f'<p><button id="bed" data-src="{bed}">Play the bed (loops)</button></p>')
    item_desc = dict({k: v[2] for k, v in ITEMS.items()}, VentLoop="gas hiss, seamless loop")
    body += section("Items in the world", [("Item", n, d) for n, d in item_desc.items()])
    body += section("Weapons in the world", [("Weapon", n, f"{WEAPONS[n][0] * 1000:.0f} ms · {WEAPONS[n][2]}") for n in WEAPONS],
                    '<p><button id="burst">Play AR-15 at 10 a second</button></p>')
    walk = ",".join(f'[{t:.2f},"{src(p, n)}",{semi:.2f},"{esc(note)}"]' for t, p, n, semi, note in WALK)
    burst = src("Weapon", "AR15Shot")
    html = f"""<!doctype html><html lang="en"><head><meta charset="utf-8"><title>Seal the Deal: sound audition</title>
<style>body{{font:16px/1.5 -apple-system,sans-serif;color:#111B38;background:#F3F4F4;max-width:920px;margin:32px auto;padding:0 16px}}
h1{{font-weight:300;letter-spacing:.06em}}h2{{margin-top:32px;border-bottom:1px solid #C9CED3}}table{{border-collapse:collapse;width:100%}}
td{{padding:4px 12px 4px 0;vertical-align:middle}}button{{font:inherit;padding:4px 14px;border:1px solid #111B38;background:#fff;color:#111B38;border-radius:4px;cursor:pointer}}
button:hover{{background:#244FB9;color:#F3F4F4}}p{{max-width:70ch}}#now{{min-height:1.5em;font-weight:600;color:#244FB9}}</style></head><body>
<h1>SEAL THE DEAL · sound audition</h1>
<p><b>Nobody has listened to these yet</b>: they were synthesised and measured only. Start with the walk: it plays the menu
the way a player meets it in their first minute, with the ambient bed under the title. Then play any group on its own.
Note any that are harsh, too long, too alike, or wrong for what the player just did.</p>
<p><button id="walk">Walk through the menu (20 s)</button> <button id="stop">Stop</button></p><p id="now"></p>
{body}
<script>
const WALK=[{walk}];
const BED="{bed}";
let timers=[],bedAudio=null;
function clip(path,semi){{const a=new Audio(path);if(semi){{a.preservesPitch=false;a.mozPreservesPitch=false;a.webkitPreservesPitch=false;a.playbackRate=Math.pow(2,semi/12);}}a.play();return a;}}
function stopAll(){{timers.forEach(clearTimeout);timers=[];if(bedAudio){{bedAudio.pause();bedAudio=null;}}document.getElementById('now').textContent='';document.getElementById('bed').textContent='Play the bed (loops)';}}
function fade(a,to,ms){{const from=a.volume,t0=performance.now();(function step(){{const k=Math.min(1,(performance.now()-t0)/ms);a.volume=from+(to-from)*k;if(k<1)requestAnimationFrame(step);}})();}}
document.querySelectorAll('button.p').forEach(b=>b.onclick=()=>clip(b.dataset.src,0));
document.getElementById('bed').onclick=function(){{if(bedAudio){{stopAll();return;}}bedAudio=new Audio(this.dataset.src);bedAudio.loop=true;bedAudio.volume=0.35;bedAudio.play();this.textContent='Stop the bed';}};
document.getElementById('burst').onclick=()=>{{for(let i=0;i<10;i++)setTimeout(()=>clip('{burst}',0),i*100);}};
document.getElementById('stop').onclick=stopAll;
document.getElementById('walk').onclick=()=>{{stopAll();bedAudio=new Audio(BED);bedAudio.loop=true;bedAudio.volume=0;bedAudio.play();fade(bedAudio,0.3,1500);
 WALK.forEach(([t,path,semi,note])=>timers.push(setTimeout(()=>{{clip(path,semi);if(note)document.getElementById('now').textContent=t.toFixed(1)+' s · '+note;
  if(note.indexOf('bed fades out')>=0&&bedAudio)fade(bedAudio,0,1500);}},t*1000)));
 timers.push(setTimeout(()=>{{document.getElementById('now').textContent='End of the walk.';if(bedAudio){{bedAudio.pause();bedAudio=null;}}}},21000));}};
</script></body></html>
"""
    with open(page, "w", encoding="utf-8") as fh:
        fh.write(html)
    return page


if __name__ == "__main__":
    sys.exit(main())
