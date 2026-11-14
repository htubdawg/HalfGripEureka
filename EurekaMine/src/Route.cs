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

		internal Road(string name, float[] x, float[] y, float[] z, float width, bool closed, Surface ground, int fork, int join)
		{
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
			Main = new Road("haulage", RouteData.MainX, RouteData.MainY, RouteData.MainZ, RouteData.MainWidth, true, Surface.Haulage, -1, -1);
			Seam = new Road("upper seam", RouteData.SeamX, RouteData.SeamY, RouteData.SeamZ, RouteData.SeamWidth, false, Surface.Rough, RouteData.SeamFork, RouteData.SeamJoin);
			Chute = new Road("coal chute", RouteData.ChuteX, RouteData.ChuteY, RouteData.ChuteZ, RouteData.ChuteWidth, false, Surface.Coal, RouteData.ChuteFork, RouteData.ChuteJoin);
			Runin = new Road("slope", RouteData.RuninX, RouteData.RuninY, RouteData.RuninZ, RouteData.RuninWidth, false, Surface.Haulage, -1, RouteData.RuninJoin);
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
