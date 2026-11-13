"""
Eureka Mine's layout: the anchors of the lap, its alternate routes and the run in from the surface, the checks, and the drawings.

Run: python art/layout.py [outdir]   (default art/out)
"""
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from route import Road, pose_at, COVER, STEP  # noqa: E402

SURFACE = 80.0      # the pithead yard's height above the start line

# ---- the lap ---------------------------------------------------------------------------------------------------------------
# Levels: the main haulage (y 0), the lower gangway (about -42), the upper gangway (+40).

START = dict(name="Start / pit bottom", x=0, z=0, hd=0, y=0)
MAIN = [
	dict(name="Haulage", x=420, z=0, hd=0, y=0, r=120),
	dict(name="Plane head", x=600, z=-190, hd=-90, y=0, r=150),
	dict(name="Plane foot", x=600, z=-560, hd=-90, y=-40, r=150),
	dict(x=420, z=-730, hd=180, y=-42, r=150),
	# the lower gangway: it edges north, then dives south-west under the upper seam's road and comes back up
	dict(name="Fork: upper seam", x=280, z=-730, hd=180, y=-42, r=120),
	dict(x=130, z=-650, hd=180, y=-52, r=80),
	dict(name="Dip", x=0, z=-730, hd=-90, y=-66, r=80),
	dict(x=-140, z=-810, hd=180, y=-54, r=80),
	dict(name="Join: upper seam", x=-280, z=-730, hd=180, y=-42, r=80),
	dict(name="Spiral foot", x=-390, z=-600, hd=90, y=-42, r=110),
	dict(name="Spiral head", helix=(1.25, 115, -1), y=40),
	dict(x=180, z=-420, hd=0, y=40, r=140),
	dict(x=330, z=-220, hd=90, y=40, r=140),
	dict(name="Overcast", x=330, z=0, hd=90, y=40, r=140),
	dict(x=140, z=420, hd=180, y=38, r=130),
	dict(name="Fork: chute", x=-150, z=420, hd=180, y=34, r=120),
	dict(name="Switchbacks", x=-150, z=310, hd=0, y=26, r=55),
	dict(x=100, z=310, hd=0, y=18, r=120),
	dict(x=100, z=200, hd=180, y=10, r=55),
	dict(x=-230, z=200, hd=180, y=4, r=120),
	dict(name="Finish", x=0, z=0, hd=0, y=0, r=100),
]

WIDTH = dict(main=40, seam=30, chute=26, runin=40)


def build():
	main = Road("main", START, MAIN, WIDTH["main"], closed=True)
	m = main.marks
	roads = {"main": main}

	# the upper seam: straight through the old rooms over the dip, a little higher in the middle
	a = pose_at(main, m["Fork: upper seam"])
	b = pose_at(main, m["Join: upper seam"])
	seam = Road("seam", a, [
		dict(x=0, z=-730, hd=180, y=-24, r=200),
		dict(x=b["x"], z=b["z"], hd=b["hd"], y=b["y"], r=200),
	], WIDTH["seam"])
	seam.fork, seam.join = m["Fork: upper seam"], m["Join: upper seam"]
	roads["seam"] = seam

	# the coal chute: skips both switchbacks and drops out above the last leg
	a = pose_at(main, m["Fork: chute"])
	chute_join = m["Finish"] - 230 - 157
	b = pose_at(main, chute_join)
	chute = Road("chute", a, [
		dict(x=-330, z=400, hd=180, y=30, r=100),
		dict(x=b["x"], z=b["z"], hd=b["hd"], y=b["y"], r=100),
	], WIDTH["chute"])
	chute.fork, chute.join = m["Fork: chute"], chute_join
	roads["chute"] = chute

	# the run in: from the grid in the pithead yard, through the drift mouth and down the slope to the pit bottom
	join = main.length - 140
	b = pose_at(main, join)
	runin = Road("runin", dict(name="Grid", x=-1500, z=-80, hd=0, y=SURFACE), [
		dict(name="Drift mouth", x=-1200, z=-80, hd=0, y=SURFACE, r=150),
		dict(x=-440, z=-80, hd=0, y=5, r=150),
		dict(x=b["x"], z=b["z"], hd=b["hd"], y=b["y"], r=150),
	], WIDTH["runin"])
	runin.fork, runin.join = None, join
	roads["runin"] = runin
	return roads


# ---- checks ----------------------------------------------------------------------------------------------------------------

def grades(road):
	P = road.P
	n = len(P)
	worst, at = 0.0, 0
	rng = range(n) if road.closed else range(n - 1)
	for i in rng:
		j = (i + 1) % n
		d = math.hypot(P[j][0] - P[i][0], P[j][2] - P[i][2])
		g = abs(P[j][1] - P[i][1]) / max(1e-6, d)
		if g > worst:
			worst, at = g, P[i][3]
	return worst, at


def near_topology(ra, sa, rb, sb, main_len):
	"""True when two points are allowed to be close: neighbours along the same road, or a branch near where it meets the main lap."""
	if ra is rb:
		d = abs(sa - sb)
		if ra.closed:
			d = min(d, ra.length - d)
		return d < 3 * ra.width + 40
	if rb.name == "main":
		ra, sa, rb, sb = rb, sb, ra, sa
	if ra.name == "main":
		for end_branch, end_main in ((0.0, rb.fork), (rb.length, rb.join)):
			if end_main is None:
				continue
			dm = abs(sa - end_main)
			dm = min(dm, main_len - dm)
			if abs(sb - end_branch) < 160 and dm < 160:
				return True
	return False


def clearances(roads):
	main_len = roads["main"].length
	items = [(r, p) for r in roads.values() for p in r.P]
	problems = []
	for i in range(len(items)):
		ra, pa = items[i]
		for j in range(i + 1, len(items)):
			rb, pb = items[j]
			reach = (ra.width + rb.width) / 2 + 12
			dx, dz = pa[0] - pb[0], pa[2] - pb[2]
			if dx * dx + dz * dz > reach * reach:
				continue
			if abs(pa[1] - pb[1]) >= COVER:
				continue
			if near_topology(ra, pa[3], rb, pb[3], main_len):
				continue
			problems.append((ra.name, round(pa[3]), rb.name, round(pb[3]), round(abs(pa[1] - pb[1]), 1)))
	return problems


def crossings(roads):
	"""Places where one road passes over another (in plan within their widths), with the vertical gap."""
	out = {}
	main_len = roads["main"].length
	names = list(roads)
	for ia in range(len(names)):
		for ib in range(ia, len(names)):
			ra, rb = roads[names[ia]], roads[names[ib]]
			for pa in ra.P[::2]:
				for pb in rb.P[::2]:
					reach = (ra.width + rb.width) / 2
					if (pa[0] - pb[0]) ** 2 + (pa[2] - pb[2]) ** 2 > reach * reach:
						continue
					if near_topology(ra, pa[3], rb, pb[3], main_len):
						continue
					gap = abs(pa[1] - pb[1])
					key = (ra.name, rb.name, int(pa[3] // 150))
					if key not in out or gap < out[key][4]:
						out[key] = (ra.name, round(pa[3]), rb.name, round(pb[3]), round(gap, 1))
	return sorted(out.values())


# ---- drawings --------------------------------------------------------------------------------------------------------------

def draw(roads, outdir):
	import matplotlib
	matplotlib.use("Agg")
	import matplotlib.pyplot as plt
	from matplotlib.collections import LineCollection
	from matplotlib import cm, colors

	main = roads["main"]
	ys = [p[1] for r in roads.values() for p in r.P]
	norm = colors.Normalize(min(ys), max(ys))
	cmap = matplotlib.colormaps["plasma"]
	order = sorted(roads.values(), key=lambda r: sum(p[1] for p in r.P) / len(r.P))

	# plan: drawn lowest road first, so what is above covers what is below
	fig, ax = plt.subplots(figsize=(13, 11))
	ax.set_facecolor("#1b1b1b")
	# each road in short runs, so a run that passes over another can be drawn after it
	runs = []
	for r in roads.values():
		P = r.P + ([r.P[0]] if r.closed else [])
		for i in range(0, len(P) - 1, 4):
			chunk = P[i:i + 5]
			runs.append((sum(p[1] for p in chunk) / len(chunk), r, chunk))
	runs.sort(key=lambda t: t[0])
	for y, r, chunk in runs:
		xs, zs = [p[0] for p in chunk], [p[2] for p in chunk]
		w = r.width * 0.30
		ax.plot(xs, zs, color="black", lw=w + 3, solid_capstyle="butt")
		ax.plot(xs, zs, color=cmap(norm(y)), lw=w, solid_capstyle="butt")
		if r.name not in ("main", "runin"):
			ax.plot(xs, zs, color="white", lw=1, ls=(0, (3, 3)))
	for name, s in main.marks.items():
		p = pose_at(main, s)
		ax.annotate(name, (p["x"], p["z"]), color="white", fontsize=9, xytext=(8, -16 if name == "Haulage" else 8), textcoords="offset points",
			bbox=dict(boxstyle="round,pad=0.2", fc="#000000aa", ec="none"))
	for r in (roads["seam"], roads["chute"], roads["runin"]):
		p = r.P[len(r.P) // 2]
		label = {"seam": "Upper seam (old rooms)", "chute": "Coal chute", "runin": "Run in from the pithead"}[r.name]
		ax.annotate(label, (p[0], p[2]), color="#9fd3ff", fontsize=9, xytext=(10, -14), textcoords="offset points")
	g = roads["runin"].P[0]
	ax.annotate("Grid (surface)", (g[0], g[2]), color="white", fontsize=9, xytext=(0, 12), textcoords="offset points")
	# direction arrows on the main lap
	for k in range(0, len(main.P), 40):
		a, b = main.P[k], main.P[(k + 2) % len(main.P)]
		ax.annotate("", (b[0], b[2]), (a[0], a[2]), arrowprops=dict(arrowstyle="-|>", color="white", lw=1.2))
	sm = cm.ScalarMappable(norm=norm, cmap=cmap)
	fig.colorbar(sm, ax=ax, shrink=0.7, label="height (game units, start line = 0, pithead = %d)" % SURFACE)
	ax.set_aspect("equal")
	ax.grid(color="#333333", lw=0.5)
	ax.set_title("Eureka Mine: plan (dashed = alternate routes; higher roads drawn over lower ones)")
	ax.set_xlabel("x (east)")
	ax.set_ylabel("z (north)")
	fig.tight_layout()
	fig.savefig(os.path.join(outdir, "plan.png"), dpi=110)
	plt.close(fig)

	# elevation along the lap, branches placed on the lap's distance between their fork and join
	fig, ax = plt.subplots(figsize=(14, 5))
	ax.plot([p[3] for p in main.P], [p[1] for p in main.P], color="#ffb000", lw=2.5, label="main lap")
	for r, c in ((roads["seam"], "#5fd3ff"), (roads["chute"], "#ff5fd0")):
		span = r.join - r.fork
		ax.plot([r.fork + p[3] / r.length * span for p in r.P], [p[1] for p in r.P], color=c, lw=2, ls="--", label=r.name)
	ri = roads["runin"]
	ax.plot([ri.join - ri.length + p[3] for p in ri.P], [p[1] for p in ri.P], color="#aaaaaa", lw=2, ls=":", label="run in (lap 1 only)")
	for name, s in main.marks.items():
		ax.axvline(s, color="#555555", lw=0.6)
		ax.text(s, ax.get_ylim()[1] if False else 92, name, rotation=90, fontsize=8, va="top", ha="right")
	ax.axhline(SURFACE, color="#6b8e23", lw=1, ls="-.")
	ax.text(-400, SURFACE + 2, "surface", color="#6b8e23", fontsize=8)
	ax.set_xlabel("distance along the lap")
	ax.set_ylabel("height")
	ax.set_ylim(-80, 95)
	ax.grid(alpha=0.3)
	ax.legend(loc="lower left")
	ax.set_title("Eureka Mine: elevation")
	fig.tight_layout()
	fig.savefig(os.path.join(outdir, "elevation.png"), dpi=110)
	plt.close(fig)

	# oblique 3D view, height exaggerated
	fig = plt.figure(figsize=(13, 10))
	ax = fig.add_subplot(projection="3d")
	for r in order:
		P = r.P + ([r.P[0]] if r.closed else [])
		pts = [((P[i][0], P[i][2], P[i][1] * 6)) for i in range(len(P))]
		for i in range(len(pts) - 1):
			ax.plot([pts[i][0], pts[i + 1][0]], [pts[i][1], pts[i + 1][1]], [pts[i][2], pts[i + 1][2]],
				color=cmap(norm(P[i][1])), lw=r.width * 0.12, ls="-" if r.name in ("main", "runin") else "--")
	xs = [p[0] for r in roads.values() for p in r.P]
	zs = [p[2] for r in roads.values() for p in r.P]
	ax.view_init(elev=16, azim=-58)
	ax.set_box_aspect((max(xs) - min(xs), max(zs) - min(zs), 6 * (max(ys) - min(ys))))
	ax.set_title("Eureka Mine: oblique view (height x6)")
	ax.set_axis_off()
	fig.tight_layout()
	fig.savefig(os.path.join(outdir, "oblique.png"), dpi=110)
	plt.close(fig)


def main_run():
	outdir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), "out")
	os.makedirs(outdir, exist_ok=True)
	roads = build()
	main = roads["main"]
	print("lap %.0f units, closes within %.2f units and %.2f degrees" % (main.length, main.gap, main.heading_gap))
	for r in roads.values():
		g, at = grades(r)
		ys = [p[1] for p in r.P]
		print("  %-6s %6.0f long, width %d, height %.0f..%.0f, steepest %.1f%% at %.0f" % (r.name, r.length, r.width, min(ys), max(ys), g * 100, at))
	for r in ("seam", "chute"):
		rd = roads[r]
		print("  %s saves %.0f units over the main lap's %.0f" % (r, (rd.join - rd.fork) - rd.length, rd.join - rd.fork))
	print("marks:", ", ".join("%s %.0f" % (k, v) for k, v in main.marks.items()))
	print("crossings (road, at, road, at, vertical gap):")
	for c in crossings(roads):
		print("  ", c)
	bad = clearances(roads)
	print("clearance problems (closer than %d vertically where roads overlap in plan): %d" % (COVER, len(bad)))
	for b in bad[:20]:
		print("  ", b)
	draw(roads, outdir)
	with open(os.path.join(outdir, "route.json"), "w") as f:
		json.dump({k: dict(width=r.width, closed=r.closed, length=r.length, marks=r.marks,
			fork=getattr(r, "fork", None), join=getattr(r, "join", None),
			points=[[round(p[0], 2), round(p[1], 2), round(p[2], 2)] for p in r.P]) for k, r in roads.items()}, f)


if __name__ == "__main__":
	main_run()
