using System.Collections.Generic;
using UnityEngine;

namespace EurekaMine
{
	/// <summary>One road of the mine: its points (8 units apart, in race order), width and where it meets the main lap.</summary>
	internal sealed class Road
	{
		internal readonly string Name;
		internal readonly Vector3[] P;
		internal readonly float Width;
		internal readonly bool Closed;
		internal readonly Surface Ground;
		/// <summary>The main lap's point where this road leaves it and where it comes back (-1: not a branch).</summary>
		internal readonly int Fork, Join;
		/// <summary>Distance along the road to each point.</summary>
		internal readonly float[] S;
		/// <summary>Half the road's width and the height of its roof at each point: wider and higher in the chambers.</summary>
		internal readonly float[] HalfW, Ceil;
		/// <summary>The floor's lean at each point (from <c>art/layout.py</c>): the tangent of its angle, positive with the right side up.</summary>
		internal readonly float[] Bank;

		internal Road(string name, float[] x, float[] y, float[] z, float[] bank, float width, bool closed, Surface ground, int fork, int join)
		{
			Bank = bank;
			Name = name;
			Width = width;
			Closed = closed;
			Ground = ground;
			Fork = fork;
			Join = join;
			P = new Vector3[x.Length];
			S = new float[x.Length];
			HalfW = new float[x.Length];
			Ceil = new float[x.Length];
			for (int i = 0; i < x.Length; i++)
			{
				P[i] = new Vector3(x[i], y[i], z[i]);
				HalfW[i] = width * 0.5f;
				Ceil[i] = Mine.Height;
				if (i > 0)
				{
					S[i] = S[i - 1] + Vector3.Distance(P[i], P[i - 1]);
				}
			}
		}

		internal int Count
		{
			get { return P.Length; }
		}

		internal float Half
		{
			get { return Width * 0.5f; }
		}

		internal float Length
		{
			get { return Closed ? S[S.Length - 1] + Vector3.Distance(P[P.Length - 1], P[0]) : S[S.Length - 1]; }
		}

		internal int Wrap(int i)
		{
			return Closed ? ((i % P.Length) + P.Length) % P.Length : Mathf.Clamp(i, 0, P.Length - 1);
		}

		internal float HalfAt(int i)
		{
			return HalfW[Wrap(i)];
		}

		internal float CeilAt(int i)
		{
			return Ceil[Wrap(i)];
		}

		/// <summary>
		/// Opens the road into a chamber from point a to b (wrapping on a closed road): half width and roof eased up over ease points at
		/// each end. The layout's clearance check was made for the plain width, so chambers go only where nothing passes close.
		/// </summary>
		internal void Widen(int a, int b, float half, float ceil, int ease)
		{
			int n = b - a;
			for (int k = -ease; k <= n + ease; k++)
			{
				int i = Wrap(a + k);
				float f = k < 0 ? 1f + (float)k / ease : k > n ? 1f - (float)(k - n) / ease : 1f;
				f = f * f * (3f - 2f * f);
				HalfW[i] = Mathf.Max(HalfW[i], Mathf.Lerp(Half, half, f));
				Ceil[i] = Mathf.Max(Ceil[i], Mathf.Lerp(Mine.Height, ceil, f));
			}
		}

		internal float BankAt(int i)
		{
			return Bank[Wrap(i)];
		}

		/// <summary>
		/// A point built against point i (on the floor, a wall, the roof) lifted by the floor's lean there: the whole bore is sheared
		/// with the banked floor, its ribs kept upright, so everything at a given height over the floor stays at that height.
		/// </summary>
		internal Vector3 Lift(int i, Vector3 p)
		{
			Vector3 a = At(i);
			return p + Vector3.up * (Vector3.Dot(p - a, Right(i)) * BankAt(i));
		}

		/// <summary>The point lateral units right of point i (left if negative) at height over the banked floor.</summary>
		internal Vector3 Edge(int i, float lateral, float height)
		{
			return At(i) + Right(i) * lateral + Vector3.up * (height + lateral * BankAt(i));
		}

		/// <summary>The floor's upward normal at point i.</summary>
		internal Vector3 FloorUp(int i)
		{
			return (Vector3.up - Right(i) * BankAt(i)).normalized;
		}

		/// <summary>
		/// The floor at a fractional point t, lateral units across: a Catmull-Rom spline through the points (height and lean included)
		/// with its right taken from the spline's own direction, so the floor turns and climbs smoothly between points rather than in
		/// 8-unit flat panels (on the spiral each panel was twisted, its two triangles creased 2 degrees: a washboard, owner after 0.4.10).
		/// </summary>
		internal Vector3 Floor(float t, float lateral)
		{
			int i = Mathf.FloorToInt(t);
			float f = t - i;
			Vector3 p0 = At(i - 1), p1 = At(i), p2 = At(i + 1), p3 = At(i + 2);
			if (!Closed)
			{
				// past an open road's ends, carry on straight so the spline does not curl
				if (i - 1 < 0)
				{
					p0 = p1 * 2f - p2;
				}
				if (i + 2 > Count - 1)
				{
					p3 = p2 * 2f - p1;
				}
			}
			float f2 = f * f, f3 = f2 * f;
			Vector3 p = 0.5f * (2f * p1 + (p2 - p0) * f + (2f * p0 - 5f * p1 + 4f * p2 - p3) * f2 + (3f * p1 - p0 - 3f * p2 + p3) * f3);
			Vector3 d = 0.5f * ((p2 - p0) + 2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * f + 3f * (3f * p1 - p0 - 3f * p2 + p3) * f2);
			d.y = 0f;
			Vector3 right = d.sqrMagnitude > 1e-8f ? Vector3.Cross(Vector3.up, d.normalized) : Right(i);
			float b0 = BankAt(i - 1), b1 = BankAt(i), b2 = BankAt(i + 1), b3 = BankAt(i + 2);
			float bank = 0.5f * (2f * b1 + (b2 - b0) * f + (2f * b0 - 5f * b1 + 4f * b2 - b3) * f2 + (3f * b1 - b0 - 3f * b2 + b3) * f3);
			return p + right * lateral + Vector3.up * (lateral * bank);
		}

		internal Vector3 At(int i)
		{
			return Closed ? P[((i % P.Length) + P.Length) % P.Length] : P[Mathf.Clamp(i, 0, P.Length - 1)];
		}

		/// <summary>The level direction of travel at a point.</summary>
		internal Vector3 Forward(int i)
		{
			Vector3 d = At(i + 1) - At(i - 1);
			d.y = 0f;
			return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.right;
		}

		internal Vector3 Right(int i)
		{
			return Vector3.Cross(Vector3.up, Forward(i)).normalized;
		}

		/// <summary>The point nearest to p (brute force: a few hundred points), and the squared distance to it.</summary>
		internal int Nearest(Vector3 p, out float sqr)
		{
			int best = 0;
			sqr = float.MaxValue;
			for (int i = 0; i < P.Length; i++)
			{
				float d = (P[i] - p).sqrMagnitude;
				if (d < sqr)
				{
					sqr = d;
					best = i;
				}
			}
			return best;
		}

		/// <summary>The nearest point by level distance only, and that distance.</summary>
		internal int NearestFlat(Vector3 p, out float dist)
		{
			int best = 0;
			float sqr = float.MaxValue;
			for (int i = 0; i < P.Length; i++)
			{
				float dx = P[i].x - p.x, dz = P[i].z - p.z;
				float d = dx * dx + dz * dz;
				if (d < sqr)
				{
					sqr = d;
					best = i;
				}
			}
			dist = Mathf.Sqrt(sqr);
			return best;
		}
	}

	/// <summary>The four roads, made from <see cref="RouteData"/>.</summary>
	internal static class Route
	{
		internal static Road Main, Seam, Chute, Runin;
		internal static readonly List<Road> All = new List<Road>();
		internal static readonly Dictionary<string, int> Marks = new Dictionary<string, int>();

		internal static void Load()
		{
			if (Main != null)
			{
				return;
			}
			Main = new Road("haulage", RouteData.MainX, RouteData.MainY, RouteData.MainZ, RouteData.MainBank, RouteData.MainWidth, true, Surface.Haulage, -1, -1);
			Seam = new Road("upper seam", RouteData.SeamX, RouteData.SeamY, RouteData.SeamZ, RouteData.SeamBank, RouteData.SeamWidth, false, Surface.Rough, RouteData.SeamFork, RouteData.SeamJoin);
			Chute = new Road("coal chute", RouteData.ChuteX, RouteData.ChuteY, RouteData.ChuteZ, RouteData.ChuteBank, RouteData.ChuteWidth, false, Surface.Coal, RouteData.ChuteFork, RouteData.ChuteJoin);
			Runin = new Road("slope", RouteData.RuninX, RouteData.RuninY, RouteData.RuninZ, RouteData.RuninBank, RouteData.RuninWidth, false, Surface.Haulage, -1, RouteData.RuninJoin);
			All.Clear();
			All.Add(Main);
			All.Add(Seam);
			All.Add(Chute);
			All.Add(Runin);
			Marks.Clear();
			for (int i = 0; i < RouteData.MarkNames.Length; i++)
			{
				Marks[RouteData.MarkNames[i]] = RouteData.MarkIndex[i];
			}
			// the pit bottom: a high wide chamber round the start line, where the slope comes in (nothing passes over it)
			Main.Widen(-12, 18, 36f, 36f, 6);
		}

		/// <summary>
		/// Which ground a point on the roads stands on: the haulage's wherever its floor is (a branch still on the lap's floor drives like
		/// the lap), else the branch's, else the slope's. The bots' waypoints carry it as their grip, so they plan their speed for it.
		/// </summary>
		internal static Surface GroundAt(Vector3 p)
		{
			foreach (Road r in new[] { Main, Seam, Chute, Runin })
			{
				int i = r.NearestFlat(p, out float dist);
				if (dist < r.HalfW[i] && Mathf.Abs(p.y - r.P[i].y) < 4f)
				{
					return r.Ground;
				}
			}
			return Surface.Haulage;
		}

		internal static int Mark(string name)
		{
			return Marks.TryGetValue(name, out int i) ? i : 0;
		}

		/// <summary>
		/// Is p inside some other road's drivable space: within its half width less the margin, over its floor, and under its smooth
		/// bore's arch (ribs to <see cref="Mine.WallTop"/>, an ellipse to the roof) less the margin? At forks and joins each road's rough
		/// rock, its roof above all, carried on across the other road's space, hanging below that road's roof: a sheet of rock across the
		/// road seen from one way and culled (see-through) from the other (owner, 0.4.7).
		/// </summary>
		internal static bool InDrive(Road self, Vector3 p, float margin)
		{
			foreach (Road r in All)
			{
				if (r == self)
				{
					continue;
				}
				int n = r.Count, last = r.Closed ? n : n - 1;
				for (int i = 0; i < last; i++)
				{
					Vector3 a = r.P[i], c = r.P[(i + 1) % n];
					if ((a - p).sqrMagnitude > 3600f)
					{
						continue;
					}
					Vector3 ab = c - a, ap = p - a;
					ab.y = 0f;
					ap.y = 0f;
					float len2 = ab.sqrMagnitude;
					float f = len2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(ap, ab) / len2) : 0f;
					Vector3 q = Vector3.Lerp(a, c, f);
					float dx = p.x - q.x, dz = p.z - q.z, dy = p.y - q.y;
					float h = Mathf.Lerp(r.HalfAt(i), r.HalfAt(i + 1), f), ceil = Mathf.Lerp(r.CeilAt(i), r.CeilAt(i + 1), f);
					float lat = Mathf.Sqrt(dx * dx + dz * dz);
					if (dy < -1f || lat > h - margin)
					{
						continue;
					}
					float s = lat / h;
					float roof = Mine.WallTop + (ceil - Mine.WallTop) * Mathf.Sqrt(Mathf.Max(0f, 1f - s * s));
					if (dy < roof - margin)
					{
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>
		/// Is p inside some other road's corridor (within its half width, less a margin, and near its floor height)? Walls, sets and lamps
		/// are left out there: where two roads share a floor (at a fork or a join) there must be no wall across either.
		/// </summary>
		internal static bool InsideOther(Road self, Vector3 p, float margin)
		{
			foreach (Road r in All)
			{
				if (r == self)
				{
					continue;
				}
				// cheap rejection: roads are compact, but check every point (a few thousand tests per call at build time only)
				for (int i = 0; i < r.P.Length; i++)
				{
					Vector3 q = r.P[i];
					float dy = p.y - q.y;
					if (dy < -3f || dy > r.Ceil[i] - 2f)
					{
						continue;
					}
					float dx = q.x - p.x, dz = q.z - p.z;
					float h = r.HalfW[i] - margin;
					if (dx * dx + dz * dz < h * h)
					{
						return true;
					}
				}
			}
			return false;
		}
	}
}
