"""Writes shots.json for shoot.js: camera positions (game coordinates) along the mine's roads, read from scene.json. Run by run.sh."""
import json
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
]
json.dump(shots, open(os.path.join(work, "shots.json"), "w"))
