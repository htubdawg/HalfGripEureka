"""Writes shots.json for shoot.js: camera positions (game coordinates) along the mine's roads, read from scene.json. Run by run.sh."""
import json
import math
import os
import sys

here = os.path.dirname(os.path.abspath(__file__))
work = sys.argv[1]
sc = json.load(open(os.path.join(work, "scene.json")))
R = {k: [v[i:i + 3] for i in range(0, len(v), 3)] for k, v in sc["roads"].items()}


def inside(road, i, ahead=6, h=8):
    P = R[road]
    a, b = P[i % len(P)], P[(i + ahead) % len(P)]
    return dict(eye=[a[0], a[1] + h, a[2]], at=[b[0], b[1] + h - 1, b[2]])


surf = ["ground", "yard", "highwall", "hill top", "benches", "culm", "hopper", "house", "church", "dome", "cross", "chime", "porch",
        "headframe", "boiler", "breaker", "trestle", "siding", "name board", "portal"]
dark = dict(dark=True, bg=0x050403, fog=700, hide=["ground", "hill top"])
shots = [
    dict(name="01_grid", eye=[-1520, 14, -60], at=[-1200, 14, -80], opts=dict(fog=2500)),
    dict(name="02_pithead_aerial", eye=[-2200, 260, -900], at=[-1350, 0, -60], opts=dict(fog=4000)),
    dict(name="03_mine_xray", eye=[1100, 500, -1700], at=[-150, -90, -150], opts=dict(hide=surf + ["roof", "walls", "timber", "rails", "sleepers", "rubble"], bg=0x202020, fov=55)),
    dict(name="05_drift_mouth", **inside("slope", 30, 8, 9), opts=dict(fog=2500)),
    dict(name="06_slope_down", **inside("slope", 80, 8, 9), opts=dark),
    dict(name="07_merge_from_slope", **inside("slope", len(R["slope"]) - 14, 10, 9), opts=dark),
    dict(name="08_haulage", **inside("haulage", 10, 8, 9), opts=dark),
    dict(name="09_plane", **inside("haulage", 92, 8, 9), opts=dark),
    dict(name="10_seam_fork", **inside("haulage", 178, 8, 9), opts=dark),
    dict(name="11_seam", **inside("upper seam", 30, 6, 8), opts=dark),
    dict(name="12_spiral", **inside("haulage", 340, 6, 9), opts=dark),
    dict(name="13_chute_fork", **inside("haulage", 622, 8, 9), opts=dark),
    dict(name="14_chute", **inside("coal chute", 20, 6, 8), opts=dark),
    dict(name="15_switchback", **inside("haulage", 660, 5, 9), opts=dark),
    dict(name="16_pit_bottom", **inside("haulage", len(R["haulage"]) - 10, 12, 10), opts=dark),
    dict(name="17_pit_bottom_shaft", eye=[R["haulage"][10][0], R["haulage"][10][1] + 12, R["haulage"][10][2] - 10], at=[R["haulage"][4][0], R["haulage"][4][1] + 12, R["haulage"][4][2] + 40], opts=dark),
    dict(name="18_lower_gangway", **inside("haulage", 280, 6, 9), opts=dark),
    dict(name="19_upper_gangway", **inside("haulage", 440, 6, 9), opts=dark),
    dict(name="21_plane_head_spur", **inside("haulage", 86, 4, 9), opts=dark),
    dict(name="22_plane_foot_spur", **inside("haulage", 124, 6, 9), opts=dark),
    dict(name="30_village", eye=[-1700, 40, 140], at=[-2000, 10, 360], opts=dict(fog=2500)),
    dict(name="31_house_close", eye=[-1940, 14, 240], at=[-1984, 14, 300], opts=dict(fog=2500)),
    dict(name="32_church", eye=[-2000, 22, 440], at=[-2050, 40, 545], opts=dict(fog=2500)),
    dict(name="33_house_backs", eye=[-1900, 30, 345], at=[-2000, 10, 340], opts=dict(fog=2500)),
    dict(name="20_slope_lower", **inside("slope", 120, 8, 9), opts=dark),
]


def chase(road, i, back=26, up=9, lamp=None):
    """A chase camera behind a car at point i of a road, with the car's headlight: 4.4 ahead of the car's middle and 2.3 over the floor
    (Headlights + Spot Light in the scene dump), aimed along the road and pitched down by lamp["pitch"] degrees."""
    P = R[road]
    a, b = P[i % len(P)], P[(i + 1) % len(P)]
    dx, dz = b[0] - a[0], b[2] - a[2]
    n = math.hypot(dx, dz)
    dx, dz = dx / n, dz / n
    view = dict(eye=[a[0] - dx * back, a[1] + up, a[2] - dz * back], at=[a[0] + dx * 40, a[1] + 4, a[2] + dz * 40])
    opts = dict(dark, car=[a[0], a[1], a[2], dx, dz])
    if lamp:
        pitch = math.radians(lamp.get("pitch", 0))
        opts["lamps"] = [dict(p=[a[0] + dx * 4.4, a[1] + 2.3, a[2] + dz * 4.4], d=[dx * math.cos(pitch), -math.sin(pitch), dz * math.cos(pitch)],
                              i=lamp["i"], outer=lamp["outer"], inner=lamp["inner"], range=lamp.get("range", 150))]
        opts["amb"] = math.pi
    return dict(view, opts=opts)


# the game's headlight (intensity 500, range 150; its cone and aim are not in the scene dump: Unity's default 30/21.8 degrees, level) against
# the mine's own (Underground.Headlamp: 8 times as bright, a 110/60.5 degree cone, range 220; Mod.HeadlightBoost, HeadlightAngle)
STOCK = dict(i=500, outer=30, inner=21.8)
MINE = dict(i=4000, outer=110, inner=60.5, range=220)
for tag, road, i in [("haulage", "haulage", 40), ("lower", "haulage", 280), ("upper", "haulage", 445), ("bend", "haulage", 660)]:
    shots.append(dict(name=f"40_head_{tag}_a_none", **chase(road, i)))
    shots[-1]["opts"]["head"] = 0
    shots[-1]["opts"]["amb"] = math.pi
    shots.append(dict(name=f"40_head_{tag}_b_stock", **chase(road, i, lamp=STOCK)))
    shots.append(dict(name=f"40_head_{tag}_c_mine", **chase(road, i, lamp=MINE)))

# the banked, smoothed floor (0.4.11): the spiral, the second switchback and the dip's S-bend behind a car with the mine's headlight, a low
# look along the spiral's floor, and the spiral with its rock hidden, seen from the helix's axis (its floors lean in toward it)
cut = ["rock", "roof", "rubble", "posts", "spill", "wire", "bulbs", "cable", "chevron", "hanger", "passage", "puddles", "steel", "sleepers", "rails", "glow"]
for tag, road, i in [("spiral", "haulage", 350), ("hairpin", "haulage", 693), ("dip", "haulage", 236)]:
    shots.append(dict(name=f"50_bank_{tag}", **chase(road, i, lamp=MINE)))
shots.append(dict(name="50_bank_spiral_low", **inside("haulage", 352, 4, 4), opts=dark))
shots.append(dict(name="51_spiral_cut", eye=[-275, 30, -600], at=[R["haulage"][350][0], R["haulage"][350][1], R["haulage"][350][2]],
                  opts=dict(hide=surf + cut, bg=0x202020, fov=70)))
shots.append(dict(name="51_hairpin_cut", eye=[R["haulage"][693][0] + 60, R["haulage"][693][1] + 60, R["haulage"][693][2] + 90],
                  at=[R["haulage"][693][0], R["haulage"][693][1], R["haulage"][693][2]], opts=dict(hide=surf + cut, bg=0x202020, fov=60)))

# HEADTRY="i,outer,inner,pitch;...": other headlights to compare (41_try_*)
for k, spec in enumerate(filter(None, os.environ.get("HEADTRY", "").split(";"))):
    i, outer, inner, pitch = (float(x) for x in spec.split(","))
    for tag, road, at in [("lower", "haulage", 280), ("upper", "haulage", 445), ("bend", "haulage", 660)]:
        shots.append(dict(name=f"41_try_{tag}_{k}", **chase(road, at, lamp=dict(i=i, outer=outer, inner=inner, pitch=pitch, range=220))))

if os.environ.get("ONLY"):
    shots = [s for s in shots if s["name"].startswith(os.environ["ONLY"])]
json.dump(shots, open(os.path.join(work, "shots.json"), "w"))
