"""
Eureka Mine's lap, laid out offline so its shape, grades and clearances can be checked before any C# is written.

The lap is a chain of anchor poses (x, z, heading, height). Consecutive anchors are joined by the shortest curve-straight-curve
path of the link's radius (a Dubins path), or by a helix. Branches (alternate routes) start and end on the main lap and are made
the same way. Heights run linearly between anchors, then are smoothed so grade changes are gentle.

Units are the game's (the Stadium lap is about 1457). +X east, +Z north, +Y up. Point 0 is the start line, heading +X, at y 0.

Run: python art/route.py [outdir]   writes route.json and the drawings plan.png, elevation.png, oblique.png
"""
import json
import math
import os
import sys

STEP = 8.0          # spacing of the sampled points
COVER = 32.0        # least vertical gap between two roads that cross in plan (a tunnel's height plus rock)
SMOOTH = 72.0       # length of the moving average applied to heights (vertical curves)


# ---- geometry --------------------------------------------------------------------------------------------------------------

def wrap(a):
    while a > math.pi:
        a -= 2 * math.pi
    while a <= -math.pi:
        a += 2 * math.pi
    return a


def mod2pi(a):
    """An angle in [0, 2 pi), with a hair under 2 pi taken as 0 (rounding would otherwise turn a straight join into a full loop)."""
    a = a % (2 * math.pi)
    return 0.0 if a > 2 * math.pi - 1e-6 else a


def dubins_csc(p0, h0, p1, h1, r):
    """Shortest of LSL, RSR, LSR, RSL from pose (p0, h0) to (p1, h1) with turn radius r: [(kind, value)], kind 'L'/'R' with an angle or 'S' with a length."""
    best = None
    for a, b in (("L", "L"), ("R", "R"), ("L", "R"), ("R", "L")):
        sa = 1 if a == "L" else -1
        sb = 1 if b == "L" else -1
        # turning circle centres (left of heading is +90 degrees: heading measured from +X toward +Z)
        c0 = (p0[0] - sa * r * math.sin(h0), p0[1] + sa * r * math.cos(h0))
        c1 = (p1[0] - sb * r * math.sin(h1), p1[1] + sb * r * math.cos(h1))
        dx, dz = c1[0] - c0[0], c1[1] - c0[1]
        d = math.hypot(dx, dz)
        if a == b:
            if d < 1e-9:
                # both poses on one circle: a single arc (a hairpin)
                t = mod2pi(sa * (h1 - h0))
                if best is None or r * t < best[0]:
                    best = (r * t, [(a, t), ("S", 0.0), (b, 0.0)])
                continue
            th = math.atan2(dz, dx)
            straight = d
        else:
            if d < 2 * r:
                continue
            # the centres differ by the straight plus twice the radius across it, toward circle 1's side
            straight = math.sqrt(d * d - 4 * r * r)
            th = math.atan2(dz, dx) + sa * math.atan2(2 * r, straight)
        t0 = mod2pi(sa * (th - h0))
        t1 = mod2pi(sb * (h1 - th))
        total = r * t0 + straight + r * t1
        if best is None or total < best[0]:
            best = (total, [(a, t0), ("S", straight), (b, t1)])
    if best is None:
        raise ValueError("no path between poses")
    return best[1]


def walk(p, h, pieces, r, step=1.0):
    """Points along pieces from pose (p, h): yields (x, z, heading, curvature sign). 1-unit steps."""
    out = []
    x, z = p
    for kind, v in pieces:
        if kind == "S":
            n = max(1, int(round(v / step)))
            for _ in range(n):
                x += math.cos(h) * v / n
                z += math.sin(h) * v / n
                out.append((x, z, h, 0))
        else:
            s = 1 if kind == "L" else -1
            length = r * v
            n = max(1, int(round(length / step))) if length > 0 else 0
            for _ in range(n):
                dh = s * v / n
                # chord of an arc of angle dh
                cx = x - s * r * math.sin(h)
                cz = z + s * r * math.cos(h)
                h += dh
                x = cx + s * r * math.sin(h)
                z = cz - s * r * math.cos(h)
                out.append((x, z, h, s))
    return out


def helix(p, h, turns, r, side):
    """A helix of turns full turns, radius r, turning left (side +1) or right (-1)."""
    return walk(p, h, [("L" if side > 0 else "R", 2 * math.pi * turns)], r)


# ---- a road from anchors ---------------------------------------------------------------------------------------------------

class Road:
    """A polyline of dense points built from anchors. Anchor: dict(x, z, hd (degrees), y, r (radius of the link to it), helix=(turns, r, side))."""

    def __init__(self, name, start, anchors, width, closed=False):
        self.name = name
        self.width = width
        self.closed = closed
        pts = [(start["x"], start["z"], math.radians(start["hd"]), 0)]
        ys = [(0.0, start["y"])]
        dense_len = 0.0
        x, z, h = start["x"], start["z"], math.radians(start["hd"])
        self.marks = {start.get("name", "start"): 0.0}
        for a in anchors:
            if "helix" in a:
                turns, hr, side = a["helix"]
                seg = helix((x, z), h, turns, hr, side)
            else:
                seg = walk((x, z), h, dubins_csc((x, z), h, (a["x"], a["z"]), math.radians(a["hd"]), a["r"]), a["r"])
            for q in seg:
                dense_len += math.hypot(q[0] - pts[-1][0], q[1] - pts[-1][1])
                pts.append(q)
            x, z, h = pts[-1][0], pts[-1][1], pts[-1][2]
            ys.append((dense_len, a["y"]))
            if "name" in a:
                self.marks[a["name"]] = dense_len
        self.length = dense_len
        # resample every STEP
        self.P = []
        acc = [0.0]
        for i in range(1, len(pts)):
            acc.append(acc[-1] + math.hypot(pts[i][0] - pts[i - 1][0], pts[i][1] - pts[i - 1][1]))
        n = int(self.length // STEP) if closed else int(round(self.length / STEP))
        j = 0
        for k in range(n + (0 if closed else 1)):
            s = min(k * STEP if closed else k * self.length / n, self.length)
            while j < len(acc) - 2 and acc[j + 1] < s:
                j += 1
            t = (s - acc[j]) / max(1e-9, acc[j + 1] - acc[j])
            px = pts[j][0] + (pts[j + 1][0] - pts[j][0]) * t
            pz = pts[j][1] + (pts[j + 1][1] - pts[j][1]) * t
            self.P.append([px, 0.0, pz, s])
        # heights: linear between anchors, then smoothed
        raw = [interp(ys, p[3]) for p in self.P]
        half = int(SMOOTH / STEP / 2)
        m = len(raw)
        sm = []
        for i in range(m):
            tot, cnt = 0.0, 0
            for k in range(i - half, i + half + 1):
                if closed:
                    tot += raw[k % m]
                    cnt += 1
                elif 0 <= k < m:
                    tot += raw[k]
                    cnt += 1
            sm.append(tot / cnt)
        if not closed:
            # keep the ends exactly on the anchors' heights (they meet the main lap there)
            for i in range(min(half, m)):
                w = i / half
                sm[i] = raw[i] * (1 - w) + sm[i] * w
                sm[m - 1 - i] = raw[m - 1 - i] * (1 - w) + sm[m - 1 - i] * w
        for p, y in zip(self.P, sm):
            p[1] = y
        if closed:
            end = pts[-1]
            self.gap = math.hypot(end[0] - pts[0][0], end[1] - pts[0][1])
            self.heading_gap = math.degrees(abs(wrap(end[2] - pts[0][2])))


def interp(table, s):
    for i in range(1, len(table)):
        if s <= table[i][0]:
            a, b = table[i - 1], table[i]
            t = (s - a[0]) / max(1e-9, b[0] - a[0])
            return a[1] + (b[1] - a[1]) * t
    return table[-1][1]


def pose_at(road, s):
    """Pose (x, z, heading degrees, y) at arc length s on a road."""
    P = road.P
    i = min(range(len(P)), key=lambda k: abs(P[k][3] - s))
    a = P[max(0, i - 1)]
    b = P[min(len(P) - 1, i + 1)]
    return dict(x=P[i][0], z=P[i][2], hd=math.degrees(math.atan2(b[2] - a[2], b[0] - a[0])), y=P[i][1], s=P[i][3])
