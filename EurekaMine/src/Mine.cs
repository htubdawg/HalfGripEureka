using System.Collections.Generic;
using UnityEngine;

namespace EurekaMine
{
	/// <summary>
	/// The underground: every road swept into a tunnel (floor, walls with a chamfer, roof), timber sets, the haulage's rails, lamps, rubble
	/// in the old workings and the timber portal at the drift mouth. Where two roads share a floor (forks and joins) the walls and sets
	/// between them are left out, so the rock between two roads begins where they part.
	/// </summary>
	internal static class Mine
	{
		/// <summary>Floor to roof: low and wide, as a mine entry is, not tall like a road tunnel (0.2.0 had 24).</summary>
		internal const float Height = 18f;
		/// <summary>Floor to the top of the ribs (walls), where they break back to the roof.</summary>
		internal const float WallTop = 12f;
		/// <summary>How far in from the wall the roof starts (the chamfer's width).</summary>
		internal const float Chamfer = 4f;

		/// <summary>Where the lamps hang: the light pool moves real lights to the ones nearest the camera.</summary>
		internal static readonly List<Vector3> Lamps = new List<Vector3>();

		internal static void Build(Dictionary<string, Batch> b)
		{
			Lamps.Clear();
			Niches.Clear();
			Plan();
			foreach (Road r in Route.All)
			{
				Tunnel(r, b);
			}
			Rails(Route.Main, b);
			foreach (Road r in Route.All)
			{
				Rubble(r, b);
			}
			Portal(b);
			StartLine(b);
			Props.Build(b);
		}

		/// <summary>The start and finish at the pit bottom: a chequered line across the floor and a board under the roof.</summary>
		private static void StartLine(Dictionary<string, Batch> b)
		{
			Road r = Route.Main;
			Vector3 a = r.At(0), f = r.Forward(0), right = r.Right(0);
			Batch white = Get(b, "line white", Tex.Plain, new Color(0.85f, 0.85f, 0.82f));
			white.Shadows = false;
			Batch black = Get(b, "line black", Tex.Plain, new Color(0.08f, 0.08f, 0.08f));
			black.Shadows = false;
			const int squares = 10;
			float w = r.Width / squares;
			for (int row = 0; row < 2; row++)
			{
				for (int k = 0; k < squares; k++)
				{
					Vector3 c0 = a - right * r.Half + right * (k * w) + f * (row * w - w) + Vector3.up * 0.08f;
					Vector3 c1 = c0 + right * w, c2 = c1 + f * w, c3 = c0 + f * w;
					((k + row) % 2 == 0 ? white : black).Quad(c3, c2, c1, c0, 4f);
				}
			}
			Batch board = Get(b, "name board", Tex.Boards, new Color(0.9f, 0.9f, 0.9f));
			Quaternion turn = Quaternion.LookRotation(f, Vector3.up);
			board.Box(a + Vector3.up * (WallTop + 2.5f) - f * 0.5f, new Vector3(26f, 4.5f, 0.5f), turn, 6f);
			SceneBuilder.Labels.Add(new Label
			{
				Pos = a + Vector3.up * (WallTop + 2.5f) - f * 0.8f,
				Facing = -f,
				Text = "PIT BOTTOM",
				Color = new Color(0.95f, 0.85f, 0.55f),
				Size = new Vector2(24f, 4f)
			});
		}

		private static Batch Get(Dictionary<string, Batch> b, string name, Texture2D tex, Color tint)
		{
			if (!b.TryGetValue(name, out Batch batch))
			{
				batch = new Batch(name, tex, tint);
				b[name] = batch;
			}
			return batch;
		}

		/// <summary>The first point of a road that is underground: the run in starts in the pithead yard.</summary>
		private static int FirstInside(Road r)
		{
			return r == Route.Runin ? RouteData.RuninMouth : 0;
		}

		/// <summary>A side passage cut into a wall: the opening between points I and I + Span on one side, how deep and how high it is, and what is in it.</summary>
		internal sealed class Niche
		{
			public Road Road;
			public int I, Span;
			public float Side;   // -1 left, +1 right
			public float Depth, High;
			public int Kind;
			// filled in when built: the opening's middle on the floor, the way out of the tunnel into it, along the road, its width
			public Vector3 Mouth, Out, Along;
			public float Width;
		}

		internal static readonly List<Niche> Niches = new List<Niche>();

		/// <summary>Points (8 units each) from one bulb to the next: at least BulbGap, plus up to BulbSpread - 1 more at random (half the gap in a chamber).</summary>
		internal static int BulbGap = 3, BulbSpread = 4;

		// ---- rough rock ---------------------------------------------------------------------------------------------------------

		/// <summary>Smooth value noise in 0..1 from a world position (cells of the given size), the same wherever it is asked, so neighbouring segments meet.</summary>
		internal static float Noise(Vector3 p, float cell, int seed)
		{
			float x = p.x / cell, y = p.y / cell, z = p.z / cell;
			int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y), z0 = Mathf.FloorToInt(z);
			float fx = x - x0, fy = y - y0, fz = z - z0;
			fx = fx * fx * (3f - 2f * fx);
			fy = fy * fy * (3f - 2f * fy);
			fz = fz * fz * (3f - 2f * fz);
			float v = 0f;
			for (int k = 0; k < 8; k++)
			{
				int dx = k & 1, dy = (k >> 1) & 1, dz = (k >> 2) & 1;
				float w = (dx == 1 ? fx : 1f - fx) * (dy == 1 ? fy : 1f - fy) * (dz == 1 ? fz : 1f - fz);
				v += w * Hash(x0 + dx, y0 + dy, z0 + dz, seed);
			}
			return v;
		}

		internal static float Hash(int x, int y, int z, int seed)
		{
			// a proper mix (murmur's finaliser): neighbouring points must not get related values
			uint h = (uint)x * 0x8da6b343u ^ (uint)y * 0xd8163841u ^ (uint)z * 0xcb1ab31fu ^ (uint)seed * 0x165667b1u;
			h ^= h >> 16;
			h *= 0x85ebca6bu;
			h ^= h >> 13;
			h *= 0xc2b2ae35u;
			h ^= h >> 16;
			return (h & 0xFFFFFF) / (float)0x1000000;
		}

		// ---- the bore -------------------------------------------------------------------------------------------------------
		// Every road is one continuous bore of rough rock, after the No. 9 Mine at Lansford: dark wet rock, roughly arched, low, rails
		// on gravel down the middle, bulbs on a wire along the crown. The cross-section is the guard's shape (straight ribs to the
		// rib top, an elliptical arch to the crown), and the rock the player sees is that shape pushed back by noise at two scales,
		// built of shared vertices so it shades smoothly. The cars touch only the guard: the same shape without the noise.

		/// <summary>Heights of the rib's rows. Side passages open from the floor to row 3, so their openings meet the rows exactly.</summary>
		private static readonly float[] RibRows = { -1.5f, 1.5f, 3.5f, 5.5f, 7.5f, 10f, WallTop };
		internal const float PassageHigh = 10f;
		/// <summary>The rib row at PassageHigh: a side passage opens the rows below it.</summary>
		private const int PassageRow = 5;
		private const int ArchSteps = 16;
		/// <summary>Columns of rock per 8-unit step of road: the roughness needs them.</summary>
		private const int Sub = 3;

		/// <summary>The number of points across a section: each rib's rows, and the arch between them.</summary>
		private static int SectionCount
		{
			get { return 2 * RibRows.Length + ArchSteps - 1; }
		}

		/// <summary>How far the rock stands back from the guard at a place: never less than 0.9 (signs and cable hang in that gap), lumps and ledges on top.</summary>
		private static float Back(Vector3 p, float rough)
		{
			float big = Noise(p, 18f, 1), small = Noise(p, 2.4f, 5);
			// ridged at the middle scales: sharp crests and pockets, rock broken rather than rolling
			float ridge = 1f - Mathf.Abs(2f * Noise(p, 6.5f, 2) - 1f);
			float ridge2 = 1f - Mathf.Abs(2f * Noise(p, 3.6f, 3) - 1f);
			return 0.9f + rough * (3.5f * big + 3.6f * ridge * ridge + 1.8f * ridge2 * ridge2 + 1.2f * small);
		}

		/// <summary>The guard's section at point i (no noise): lateral offset and height, and the outward normal there, for each index across.</summary>
		private static void Shape(float h, float ceil, int k, out float lateral, out float height, out Vector2 normal)
		{
			int rows = RibRows.Length;
			if (k < rows || k >= SectionCount - rows)
			{
				bool left = k < rows;
				int row = left ? k : SectionCount - 1 - k;
				lateral = left ? -h : h;
				height = RibRows[row];
				normal = new Vector2(left ? -1f : 1f, 0f);
				return;
			}
			// the arch: an ellipse from the left rib's top over the crown to the right's
			float phi = Mathf.PI * (1f - (float)(k - rows + 1) / ArchSteps);
			float rise = ceil - WallTop;
			lateral = h * Mathf.Cos(phi);
			height = WallTop + rise * Mathf.Sin(phi);
			normal = new Vector2(Mathf.Cos(phi) / h, Mathf.Sin(phi) / Mathf.Max(1f, rise)).normalized;
		}

		/// <summary>The road's frame at a fractional point t (between points): where it is, its right, its half width and roof.</summary>
		private static void Frame(Road r, float t, out Vector3 at, out Vector3 right, out float half, out float ceil)
		{
			int i = Mathf.FloorToInt(t);
			float f = t - i;
			at = Vector3.Lerp(r.At(i), r.At(i + 1), f);
			right = Vector3.Lerp(r.Right(i), r.Right(i + 1), f).normalized;
			half = Mathf.Lerp(r.HalfAt(i), r.HalfAt(i + 1), f);
			ceil = Mathf.Lerp(r.CeilAt(i), r.CeilAt(i + 1), f);
		}

		/// <summary>The rock's point k across the section at the fractional point t of a road.</summary>
		internal static Vector3 Rock(Road r, float t, int k)
		{
			Frame(r, t, out Vector3 p, out Vector3 right, out float h, out float ceil);
			Shape(h, ceil, k, out float lateral, out float height, out Vector2 n);
			Vector3 at = p + right * lateral + Vector3.up * height;
			bool arch = k >= RibRows.Length && k < SectionCount - RibRows.Length;
			float back = Back(at, r == Route.Seam ? 1.35f : 1f);
			if (arch)
			{
				// the roof sags in places as well as rising: down to two units under the guard's arch, never near a car's roof
				back = back * 0.75f - 2.4f;
			}
			int near = Mathf.RoundToInt(t);
			if (Mathf.Abs(t - near) < 0.4f)
			{
				back = Mathf.Max(back, Clearance(r, near, k));
			}
			if (Flat(r, t, k))
			{
				back = 0f;
			}
			return at + (right * n.x + Vector3.up * n.y) * back;
		}

		/// <summary>The crown of the bore at point i: where the lamp wire runs.</summary>
		internal static Vector3 Crown(Road r, int i)
		{
			return Rock(r, i, SectionCount / 2);
		}

		/// <summary>Points where the rock must stand further back (an occasional post): (road, point, side) to the least distance.</summary>
		private static readonly Dictionary<long, float> clear = new Dictionary<long, float>();
		/// <summary>A side passage's two edge points: the rib's rows up to PassageHigh stand on the guard line there, so the passage's sides meet them.</summary>
		private static readonly HashSet<long> flat = new HashSet<long>();
		/// <summary>The points of a passage's opening: the rib's row at PassageHigh stands on the guard line over it, so the mouth's top meets it.</summary>
		private static readonly HashSet<long> lintel = new HashSet<long>();

		private static long Key(Road r, int i, float side)
		{
			return ((long)Route.All.IndexOf(r) << 40) | ((long)r.Wrap(i) << 1) | (side > 0f ? 1L : 0L);
		}

		private static float Clearance(Road r, int i, int k)
		{
			if (k >= RibRows.Length && k < SectionCount - RibRows.Length)
			{
				return 0f;
			}
			return clear.TryGetValue(Key(r, i, k < RibRows.Length ? -1f : 1f), out float c) ? c : 0f;
		}

		/// <summary>On a side passage's opening (its points, and between them) the rib's rows up to PassageHigh stand on the guard line.</summary>
		private static bool Flat(Road r, float t, int k)
		{
			int row = k < RibRows.Length ? k : SectionCount - 1 - k;
			if (row > PassageRow || k >= RibRows.Length && k < SectionCount - RibRows.Length)
			{
				return false;
			}
			float side = k < RibRows.Length ? -1f : 1f;
			int i0 = Mathf.FloorToInt(t + 1e-4f);
			bool onPoint = Mathf.Abs(t - Mathf.Round(t)) < 1e-4f;
			if (onPoint && flat.Contains(Key(r, Mathf.RoundToInt(t), side)))
			{
				return true;
			}
			bool over = onPoint ? lintel.Contains(Key(r, Mathf.RoundToInt(t), side)) : lintel.Contains(Key(r, i0, side)) && lintel.Contains(Key(r, i0 + 1, side));
			return row == PassageRow && over;
		}

		/// <summary>An occasional timber post against a rib, every so often where the rib is whole (the rock is cleared back behind it).</summary>
		private static bool PostAt(Road r, int i, float side, Dictionary<int, Niche> niches)
		{
			if (r == Route.Runin && i < RouteData.RuninMouth + 2 || r.HalfAt(i) > r.Half + 1f)
			{
				return false;
			}
			if (Hash(i, side > 0f ? 1 : 0, Route.All.IndexOf(r), 21) > (r == Route.Seam ? 0.3f : 0.09f))
			{
				return false;
			}
			if (NicheAt(niches, r, i, side) != null || NicheAt(niches, r, i - 1, side) != null)
			{
				return false;
			}
			Vector3 edge = r.At(i) + r.Right(i) * side * r.HalfAt(i) + Vector3.up * 2f;
			return !Route.InsideOther(r, edge, 1.5f);
		}

		// ---- the tunnels -----------------------------------------------------------------------------------------------------

		private static readonly Dictionary<Road, Dictionary<int, Niche>> plans = new Dictionary<Road, Dictionary<int, Niche>>();

		/// <summary>Plans every road's side passages and posts first: the rock of each bore must know where they are before it is built.</summary>
		private static void Plan()
		{
			plans.Clear();
			clear.Clear();
			flat.Clear();
			lintel.Clear();
			foreach (Road r in Route.All)
			{
				Dictionary<int, Niche> niches = PlanNiches(r, FirstInside(r));
				plans[r] = niches;
				foreach (Niche nc in niches.Values)
				{
					flat.Add(Key(r, nc.I, nc.Side));
					flat.Add(Key(r, nc.I + nc.Span, nc.Side));
					for (int k = nc.I; k <= nc.I + nc.Span; k++)
					{
						lintel.Add(Key(r, k, nc.Side));
					}
				}
				int last = r.Closed ? r.Count : r.Count - 1;
				for (int i = 0; i < last; i++)
				{
					foreach (float side in new[] { -1f, 1f })
					{
						if (PostAt(r, i, side, niches))
						{
							clear[Key(r, i, side)] = 2.4f;
						}
					}
				}
			}
		}

		private static void Tunnel(Road r, Dictionary<string, Batch> b)
		{
			string key = r == Route.Main ? "haulage" : r == Route.Seam ? "seam" : r == Route.Chute ? "chute" : "slope";
			bool old = r == Route.Seam;
			Batch floor = Get(b, key + " floor", r.Ground == Surface.Coal ? Tex.Coal : old ? Tex.Culm : Tex.MineFloor, Color.white);
			floor.Collide = true;
			floor.Ground = r.Ground;
			floor.Shadows = false;
			Batch yard = Get(b, "yard road", Tex.Gravel, new Color(0.8f, 0.78f, 0.74f));
			yard.Collide = true;
			yard.Ground = Surface.Yard;
			yard.Shadows = false;
			Batch rock = Get(b, key + " rock", old ? Tex.OldRib : Tex.RibWall, Color.white);
			rock.Shadows = false;
			rock.Smooth = 0.35f;
			// the smooth bore the cars touch, never drawn: straight across every side passage, so nothing catches on an opening
			Batch guard = Get(b, "guard", null, Color.white);
			guard.Collide = true;
			guard.Wall = true;
			guard.Slippery = true;
			guard.Invisible = true;
			Batch timber = Get(b, "posts", Tex.Timber, new Color(0.75f, 0.68f, 0.6f));
			timber.Shadows = false;
			Batch bulb = Get(b, "bulbs", Tex.Plain, new Color(1f, 0.85f, 0.55f));
			bulb.Emission = new Color(2.6f, 1.8f, 0.8f);
			bulb.Shadows = false;
			Batch wire = Get(b, "lamp wire", Tex.Belt, Color.white);
			wire.Shadows = false;
			Batch wet = Get(b, "puddles", Tex.Plain, new Color(0.05f, 0.055f, 0.06f));
			wet.Smooth = 0.9f;
			wet.Shadows = false;

			int n = r.Count;
			int last = r.Closed ? n : n - 1;
			int inside = FirstInside(r);
			Dictionary<int, Niche> niches = plans[r];
			int m = SectionCount, rows = RibRows.Length;
			Batch roofRock = Get(b, key + " roof", old ? Tex.OldRib : Tex.RoofRock, Color.white);
			roofRock.Shadows = false;
			roofRock.Smooth = 0.35f;

			int nextBulb = 2;
			for (int i = 0; i < last; i++)
			{
				Vector3 a = r.At(i), c = r.At(i + 1);
				Vector3 ra = r.Right(i), rc = r.Right(i + 1);
				float ha = r.HalfAt(i), hc = r.HalfAt(i + 1);
				Vector3 aL = a - ra * ha, aR = a + ra * ha, cL = c - rc * hc, cR = c + rc * hc;
				bool surface = i < inside;

				// the floor, unless another road's floor is already there (a branch where it still runs on the lap's floor)
				if (r == Route.Main || !(OnOtherFloor(r, aL) && OnOtherFloor(r, aR) && OnOtherFloor(r, cL) && OnOtherFloor(r, cR)))
				{
					Vector3 lift = surface ? Vector3.up * 0.06f : Vector3.zero;
					(surface ? yard : floor).Quad(aL + lift, cL + lift, cR + lift, aR + lift, 12f);
				}
				if (surface)
				{
					continue;
				}

				Vector3 up = Vector3.up;
				bool leftOpen = Route.InsideOther(r, (aL + cL) * 0.5f + up * 2f, 1.5f);
				bool rightOpen = Route.InsideOther(r, (aR + cR) * 0.5f + up * 2f, 1.5f);
				Niche nl = NicheAt(niches, r, i, -1f), nr = NicheAt(niches, r, i, 1f);
				// the floor runs on under the rock to its foot (drawn only), so no gap shows where the rib stands back from the guard
				Batch skirt = Get(b, key + " floor edge", r.Ground == Surface.Coal ? Tex.Coal : old ? Tex.Culm : Tex.MineFloor, Color.white);
				skirt.Shadows = false;
				Vector3 sink = up * -0.04f;
				if (!leftOpen && nl == null)
				{
					skirt.Quad(aL - ra * 12f + sink, cL - rc * 12f + sink, cL + sink, aL + sink, 12f);
				}
				if (!rightOpen && nr == null)
				{
					skirt.Quad(aR + sink, cR + sink, cR + rc * 12f + sink, aR + ra * 12f + sink, 12f);
				}

				// the rock: Sub columns per step, each a section of the rough bore, faces flat-shaded so it breaks like broken rock
				float vA = r.S[r.Wrap(i)] / 24f, step = Vector3.Distance(a, c) / 24f;
				for (int sub = 0; sub < Sub; sub++)
				{
					float t0 = i + (float)sub / Sub, t1 = i + (float)(sub + 1) / Sub;
					float v0 = vA + step * sub / Sub, v1 = vA + step * (sub + 1) / Sub;
					float u0 = 0f;
					for (int k = 0; k < m - 1; k++)
					{
						bool leftRib = k < rows - 1, rightRib = k >= m - rows;
						bool arch = !leftRib && !rightRib;
						int row = leftRib ? k : rightRib ? m - 2 - k : -1;
						Shape(ha, r.CeilAt(i), k, out float lA, out float yA, out Vector2 unusedA);
						Shape(ha, r.CeilAt(i), k + 1, out float lB, out float yB, out Vector2 unusedB);
						float u1 = u0 + Mathf.Sqrt((lB - lA) * (lB - lA) + (yB - yA) * (yB - yA)) / 24f;
						bool skip = leftRib && leftOpen || rightRib && rightOpen;
						// a side passage opens the rib's rows below PassageHigh; the guard runs across it all the same
						bool passage = row >= 0 && row < PassageRow && (leftRib ? nl != null : nr != null);
						if (!skip && !passage)
						{
							Vector3 p00 = Rock(r, t0, k), p01 = Rock(r, t0, k + 1), p11 = Rock(r, t1, k + 1), p10 = Rock(r, t1, k);
							if (arch)
							{
								roofRock.Quad(p00, p01, p11, p10, new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1));
							}
							else
							{
								// on the ribs u is the height over the floor, so the coal band sits at the same height on both
								float ua = (yA + 1.5f) / 24f, ub = (yB + 1.5f) / 24f;
								rock.Quad(p00, p01, p11, p10, new Vector2(ua, v0), new Vector2(ub, v0), new Vector2(ub, v1), new Vector2(ua, v1));
							}
						}
						u0 = u1;
					}
				}
				for (int k = 0; k < m - 1; k++)
				{
					bool leftRib = k < rows - 1, rightRib = k >= m - rows;
					if (leftRib && leftOpen || rightRib && rightOpen)
					{
						continue;
					}
					Shape(ha, r.CeilAt(i), k, out float la0, out float hy0, out Vector2 unusedA);
					Shape(ha, r.CeilAt(i), k + 1, out float la1, out float hy1, out Vector2 unusedB);
					Shape(hc, r.CeilAt(i + 1), k, out float lc0, out float hc0, out Vector2 unusedC);
					Shape(hc, r.CeilAt(i + 1), k + 1, out float lc1, out float hc1, out Vector2 unusedD);
					guard.Quad(a + ra * la0 + up * hy0, a + ra * la1 + up * hy1, c + rc * lc1 + up * hc1, c + rc * lc0 + up * hc0, 10f);
				}
				if (niches.TryGetValue(i, out Niche start))
				{
					BuildNiche(start, b);
				}
				if (niches.TryGetValue(-1 - i, out Niche start2))
				{
					BuildNiche(start2, b);
				}

				// an occasional post against a rib, the rock cleared back behind it
				foreach (float side in new[] { -1f, 1f })
				{
					if (clear.ContainsKey(Key(r, i, side)))
					{
						float lean = ((i * 7) % 5 - 2) * 1.2f;
						Quaternion turn = Quaternion.LookRotation(r.Forward(i), Vector3.up) * Quaternion.Euler(0f, (i % 3 - 1) * 8f, side * lean);
						Vector3 foot = a + ra * side * (ha + 0.95f);
						timber.Cylinder(foot + Vector3.down * 0.5f, foot + turn * Vector3.up * (WallTop + 1.5f), 0.85f, 7, 4f, true);
					}
				}

				// bare bulbs hanging from the wire along the crown, at uneven spacing; a puddle here and there in the ruts
				bool chamber = ha > r.Half + 2f;
				if (!old && i >= nextBulb && !leftOpen && !rightOpen)
				{
					float hsh = Hash(i, r.Count, 1, 11);
					nextBulb = i + (chamber ? Mathf.Max(2, BulbGap / 2) : BulbGap + (int)(hsh * BulbSpread));
					Vector3 crown = Crown(r, i);
					Vector3 at = new Vector3(crown.x, Mathf.Min(crown.y, a.y + r.CeilAt(i)) - 1.4f, crown.z);
					bulb.Box(at, new Vector3(0.6f, 0.8f, 0.6f), Quaternion.LookRotation(r.Forward(i), Vector3.up), 1f);
					wire.Box((at + crown) * 0.5f + Vector3.up * 0.2f, new Vector3(0.1f, Mathf.Max(0.3f, crown.y - at.y), 0.1f), Quaternion.identity, 1f);
					Lamps.Add(at);
				}
				if (!old)
				{
					Vector3 w0 = Crown(r, i) + Vector3.down * 0.3f, w1 = Crown(r, i + 1) + Vector3.down * 0.3f;
					Vector3 sag = (w0 + w1) * 0.5f + Vector3.down * 0.4f;
					wire.Cylinder(w0, sag, 0.07f, 3, 2f, false);
					wire.Cylinder(sag, w1, 0.07f, 3, 2f, false);
				}
				if (Hash(i, 3, Route.All.IndexOf(r), 31) < 0.3f)
				{
					// a puddle: flat, a hair over the floor, glossy black
					float side = Hash(i, 4, 0, 32) < 0.5f ? -1f : 1f;
					float off = side * ha * (0.15f + 0.6f * Hash(i, 5, 0, 33));
					float len = 3f + 5f * Hash(i, 6, 0, 34), wid = 1.5f + 3f * Hash(i, 7, 0, 35);
					// an irregular blob: a fan round its middle, each edge point at its own distance
					Vector3 f = r.Forward(i);
					Vector3 mid = a + ra * off + f * (2f + len * 0.5f) + up * 0.05f;
					int centre = wet.Vertex(mid, Vector2.zero);
					int first = -1, prev = -1;
					for (int k = 0; k < 9; k++)
					{
						float ang = k * Mathf.PI * 2f / 9f;
						float rad = 0.6f + 0.5f * Hash(i, k, 8, 36);
						Vector3 e = mid + f * (Mathf.Cos(ang) * len * 0.5f * rad) + ra * (Mathf.Sin(ang) * wid * rad);
						int id = wet.Vertex(e, Vector2.zero);
						if (prev >= 0)
						{
							wet.Triangle(centre, prev, id);   // the edge runs clockwise seen from above: facing up
						}
						else
						{
							first = id;
						}
						prev = id;
					}
					wet.Triangle(centre, prev, first);
				}
			}
		}

		private static Niche NicheAt(Dictionary<int, Niche> niches, Road r, int i, float side)
		{
			foreach (Niche nc in niches.Values)
			{
				if (nc.Side == side && i >= nc.I && i < nc.I + nc.Span)
				{
					return nc;
				}
			}
			return null;
		}

		/// <summary>
		/// Where the side passages go: every so often on a fairly straight stretch, sides taking turns, never at a junction, a chamber, a
		/// hazard or anywhere the passage would run into another road. In the old workings they are the rooms between the pillars, on both
		/// sides and close together. Keyed by the first point (negative keys for a second niche at the same point).
		/// </summary>
		private static Dictionary<int, Niche> PlanNiches(Road r, int inside)
		{
			Dictionary<int, Niche> plan = new Dictionary<int, Niche>();
			bool old = r == Route.Seam;
			if (r == Route.Main)
			{
				// the two headings off the plane the runaway cars start from and end in (left side, the outside of the plane's foot)
				foreach (Niche spur in new[]
				{
					new Niche { Road = r, I = Route.Mark("Plane head") + 2, Span = 3, Side = -1f, Depth = 40f, High = PassageHigh, Kind = 400 },
					new Niche { Road = r, I = Route.Mark("Plane foot") - 2, Span = 3, Side = -1f, Depth = 40f, High = PassageHigh, Kind = 401 }
				})
				{
					if (NicheFits(spur, true))
					{
						plan[spur.I] = spur;
					}
					else
					{
						Mod.Info("No room for the runaway cars' heading at point " + spur.I);
					}
				}
			}
			int every = old ? 5 : 7;
			int count = 0;
			int last = r.Closed ? r.Count : r.Count - 1;
			for (int i = inside + 4; i < last - 6; i += 1)
			{
				if ((i - inside) % every != 0)
				{
					continue;
				}
				count++;
				bool big = !old && count % 3 == 0;
				int span = old ? 2 : big ? 4 : 2;
				foreach (float side in new[] { count % 2 == 0 ? -1f : 1f })
				{
					Niche nc = new Niche { Road = r, I = i, Span = span, Side = side, Depth = old ? 34f : big ? 22f : 18f, High = PassageHigh };
					nc.Kind = old ? (count * 7 + (side > 0 ? 3 : 0)) % 5 : big ? 100 + (count / 3) % 6 : count % 7;
					if (!NicheFits(nc, false) || Overlaps(plan, nc))
					{
						continue;
					}
					int key = plan.ContainsKey(i) ? -1 - i : i;
					plan[key] = nc;
				}
			}
			return plan;
		}

		private static bool Overlaps(Dictionary<int, Niche> plan, Niche nc)
		{
			foreach (Niche o in plan.Values)
			{
				if (o.Side == nc.Side && nc.I <= o.I + o.Span + 2 && o.I <= nc.I + nc.Span + 2)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Does a side passage fit here? A forced one (the runaway cars' headings) skips the rules about where passages go and keeps only the room check.</summary>
		private static bool NicheFits(Niche nc, bool forced)
		{
			Road r = nc.Road;
			int a = nc.I, b = nc.I + nc.Span;
			if (!forced && Vector3.Angle(r.Forward(a), r.Forward(b)) > 9f)
			{
				return false;
			}
			for (int k = a - 1; k <= b + 1; k++)
			{
				if (r.HalfAt(k) > r.Half + 1f)
				{
					return false;   // a chamber
				}
			}
			if (!forced && r == Route.Main && (InRange(a, Route.Mark("Plane foot") - 2, Route.Seam.Fork + 2) || InRange(a, Route.Mark("Plane head") - 4, Route.Mark("Plane foot") + 2)))
			{
				return false;   // the coal chutes' walls, and the plane where the runaway cars run
			}
			if (!forced && r == Route.Chute && (Mathf.Abs(a - Mathf.RoundToInt((r.Count - 1) * 0.3f)) < 5 || Mathf.Abs(a - Mathf.RoundToInt((r.Count - 1) * 0.62f)) < 5))
			{
				return false;   // the ventilation doors
			}
			Vector3 mid = (r.At(a) + r.At(b)) * 0.5f;
			Vector3 right = r.Right((a + b) / 2);
			float half = r.HalfAt((a + b) / 2);
			Vector3 mouth = mid + right * nc.Side * half;
			Vector3 back = mouth + right * nc.Side * nc.Depth;
			if (Route.InsideOther(r, mouth + Vector3.up * 2f, -4f))
			{
				return false;
			}
			// nothing of any road (this one away from here included) may come near the passage's box, at about its height
			foreach (Road o in Route.All)
			{
				for (int k = 0; k < o.Count; k++)
				{
					if (o == r && Mathf.Abs(r.Wrap(k) - r.Wrap((a + b) / 2)) < 8 + nc.Span || o == r && r.Closed && r.Count - Mathf.Abs(k - (a + b) / 2) < 8 + nc.Span)
					{
						continue;
					}
					Vector3 q = o.P[k];
					if (q.y < mid.y - o.Ceil[k] - 12f || q.y > mid.y + nc.High + 12f)
					{
						continue;
					}
					float reach = o.HalfW[k] + nc.Span * 4f + 10f;
					for (float f = 0.25f; f <= 1f; f += 0.25f)
					{
						Vector3 p = Vector3.Lerp(mouth, back, f);
						float dx = p.x - q.x, dz = p.z - q.z;
						if (dx * dx + dz * dz < reach * reach)
						{
							return false;
						}
					}
				}
			}
			return true;
		}

		private static bool InRange(int i, int lo, int hi)
		{
			return i >= lo && i <= hi;
		}

		/// <summary>
		/// A side passage: a short bore of the same rough rock running off from the rib's opening (floor to PassageHigh) and ending in a
		/// rough face, with a flat floor. Drawn only: the guard runs straight across the opening, so no car can drive in.
		/// </summary>
		private static void BuildNiche(Niche nc, Dictionary<string, Batch> b)
		{
			Road r = nc.Road;
			bool old = r == Route.Seam;
			int a = nc.I, c = nc.I + nc.Span;
			Vector3 pa = r.At(a) + r.Right(a) * nc.Side * r.HalfAt(a), pc = r.At(c) + r.Right(c) * nc.Side * r.HalfAt(c);
			Vector3 o = (r.Right(a) + r.Right(c)).normalized * nc.Side;
			nc.Mouth = (pa + pc) * 0.5f;
			nc.Out = o;
			nc.Along = (pc - pa).normalized;
			nc.Width = Vector3.Distance(pa, pc);
			nc.Mouth.y = Mathf.Min(pa.y, pc.y);
			Batch rock = Get(b, old ? "seam rock" : "passage rock", old ? Tex.OldRib : Tex.RibWall, Color.white);
			rock.Shadows = false;
			rock.Smooth = 0.35f;
			Batch floor = Get(b, "passage floors", old ? Tex.Culm : Tex.MineFloor, new Color(0.8f, 0.78f, 0.76f));
			floor.Shadows = false;
			// the passage's own section: ribs to PassageHigh and a low arch; its right (seen walking in) is across the opening
			Vector3 side = Vector3.Cross(Vector3.up, o).normalized;
			float h = nc.Width * 0.5f, high = nc.High, rise = 3f;
			const int ribRows = 5, archSteps = 6, across = 2 * ribRows + archSteps - 1, steps = 10;
			int[][] ids = new int[steps + 1][];
			Vector3[] centres = new Vector3[steps + 1];
			for (int s = 0; s <= steps; s++)
			{
				float d = nc.Depth * s / steps;
				Vector3 centre = nc.Mouth + o * d;
				centres[s] = centre;
				ids[s] = new int[across];
				for (int k = 0; k < across; k++)
				{
					// the left rib's rows, the arch, the right rib's rows (as the bore's section, smaller)
					float lateral, height;
					Vector2 normal;
					if (k < ribRows)
					{
						lateral = -h;
						height = high * k / (ribRows - 1f);
						normal = new Vector2(-1f, 0f);
					}
					else if (k >= across - ribRows)
					{
						lateral = h;
						height = high * (across - 1 - k) / (ribRows - 1f);
						normal = new Vector2(1f, 0f);
					}
					else
					{
						float phi = Mathf.PI * (1f - (float)(k - ribRows + 1) / archSteps);
						lateral = h * Mathf.Cos(phi);
						height = high + rise * Mathf.Sin(phi);
						normal = new Vector2(Mathf.Cos(phi) / h, Mathf.Sin(phi) / rise).normalized;
					}
					if (s == 0)
					{
						height = Mathf.Min(height, high);   // the mouth is the rib's opening: flat-topped at PassageHigh
					}
					Vector3 at = centre + side * lateral + Vector3.up * height;
					// at the mouth the passage meets the rib's opening exactly; deeper in it is as rough as the bore
					float back = s == 0 ? 0f : Back(at, old ? 1.35f : 1f) * (s == 1 ? 0.6f : 1f);
					ids[s][k] = rock.Vertex(at + (side * normal.x + Vector3.up * normal.y) * back, new Vector2(k / 6f, d / 24f));
				}
			}
			for (int s = 0; s < steps; s++)
			{
				for (int k = 0; k < across - 1; k++)
				{
					// seen from inside the passage (as the bore's ribs are)
					rock.QuadIndex(ids[s][k], ids[s][k + 1], ids[s + 1][k + 1], ids[s + 1][k]);
				}
			}
			// the face at the end: a fan to a point pushed back into the rock
			Vector3 end = centres[steps] + Vector3.up * (high * 0.5f);
			int tip = rock.Vertex(end + o * (2f + 3f * Noise(end, 5f, 7)), new Vector2(1f, nc.Depth / 24f + 0.2f));
			for (int k = 0; k < across - 1; k++)
			{
				rock.Triangle(ids[steps][k], ids[steps][k + 1], tip);
			}
			Vector3 fa = pa, fc = pc, ba = pa + o * (nc.Depth + 2f), bc = pc + o * (nc.Depth + 2f);
			fa.y = fc.y = ba.y = bc.y = nc.Mouth.y + 0.02f;
			floor.QuadBoth(fa, fc, bc, ba, 12f);
			Niches.Add(nc);
		}

		/// <summary>True where another road's floor runs under this point at about the same height. A branch's floor tile is left out only when all four corners are on the lap's floor, or a hole would open beside it.</summary>
		private static bool OnOtherFloor(Road self, Vector3 p)
		{
			foreach (Road r in Route.All)
			{
				if (r == self || r == Route.Seam && self == Route.Chute || r == Route.Chute && self == Route.Seam)
				{
					continue;
				}
				for (int i = 0; i < r.Count; i++)
				{
					Vector3 q = r.P[i];
					if (Mathf.Abs(q.y - p.y) > 1.5f)
					{
						continue;
					}
					float dx = q.x - p.x, dz = q.z - p.z;
					float hh = r.Half + 0.5f;
					if (dx * dx + dz * dz < hh * hh)
					{
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>Two rails down the middle of the haulage, on sleepers. Scenery only (no collider: the cars would hop on them).</summary>
		private static void Rails(Road r, Dictionary<string, Batch> b)
		{
			Batch steel = Get(b, "rails", Tex.Steel, new Color(0.7f, 0.7f, 0.72f));
			steel.Shadows = false;
			Batch sleepers = Get(b, "sleepers", Tex.Timber, new Color(0.55f, 0.5f, 0.45f));
			sleepers.Shadows = false;
			for (int i = 0; i < r.Count; i++)
			{
				Vector3 a = r.At(i), c = r.At(i + 1);
				Vector3 ra = r.Right(i), rc = r.Right(i + 1);
				if (Route.InsideOther(r, a, -6f))
				{
					continue;   // the rails stop short of the junctions
				}
				foreach (float side in new[] { -3.2f, 3.2f })
				{
					Vector3 p0 = a + ra * side + Vector3.up * 0.25f, p1 = c + rc * side + Vector3.up * 0.25f;
					Vector3 w0 = ra * 0.3f, w1 = rc * 0.3f;
					steel.Quad(p0 - w0, p1 - w1, p1 + w1, p0 + w0, 4f);
				}
				if (i % 2 == 0)
				{
					sleepers.Box(a + Vector3.up * 0.08f, new Vector3(9.5f, 0.2f, 1.2f), Quaternion.LookRotation(r.Forward(i), Vector3.up), 4f);
				}
			}
		}

		/// <summary>Fallen rock along the edges of the old workings, flat enough to drive over but rough to look at. Scenery only.</summary>
		/// <summary>
		/// Fallen rock heaped against the ribs (each rock's middle behind the guard, so only its face shows and nothing stands in the
		/// road), and loose coal and slate spilled thin along the floor's edges, flat enough that driving over it is nothing. Thick in
		/// the old workings, here and there everywhere else.
		/// </summary>
		private static void Rubble(Road r, Dictionary<string, Batch> b)
		{
			bool old = r == Route.Seam;
			Batch rock = Get(b, "rubble", old ? Tex.OldRib : Tex.RibWall, new Color(1.2f, 1.15f, 1.1f));
			rock.Shadows = false;
			Batch spill = Get(b, "coal spill", Tex.Coal, new Color(0.9f, 0.9f, 0.9f));
			spill.Shadows = false;
			System.Random rng = new System.Random(19 + r.Count);
			int start = r == Route.Runin ? RouteData.RuninMouth + 3 : 2;
			int last = r.Closed ? r.Count : r.Count - 3;
			for (int i = start; i < last; i++)
			{
				foreach (float side in new[] { -1f, 1f })
				{
					Vector3 edge = r.At(i) + r.Right(i) * side * r.HalfAt(i);
					if (Route.InsideOther(r, edge + Vector3.up * 2f, 0f))
					{
						continue;
					}
					if (old || rng.NextDouble() < 0.25)
					{
						// a heap of broken rock against the rib, its middle behind the guard so only its toe shows
						float s = old ? 3.5f + (float)rng.NextDouble() * 3f : 2f + (float)rng.NextDouble() * 2.5f;
						Vector3 at = edge + r.Right(i) * side * (s * 0.55f) + r.Forward(i) * (float)(rng.NextDouble() * 8.0);
						rock.Heap(at, s, s * 0.55f, 7, 2, s * 0.35f, rng, 3f);
					}
					if (rng.NextDouble() < (old ? 0.6 : 0.35))
					{
						// a thin wedge of spill from the rib out onto the floor
						Vector3 f = r.Forward(i), right = r.Right(i);
						float w = 2f + (float)rng.NextDouble() * 4f, len = 5f + (float)rng.NextDouble() * 7f;
						Vector3 a = edge + f * (float)(rng.NextDouble() * 4.0), c = a + f * len;
						Vector3 aIn = a - right * side * w, cIn = c - right * side * (w * 0.5f);
						Vector3 lift = Vector3.up * 0.12f, hump = Vector3.up * 0.35f;
						if (side < 0f)
						{
							spill.Quad(a + hump, c + hump, cIn + lift, aIn + lift, 4f);
						}
						else
						{
							spill.Quad(aIn + lift, cIn + lift, c + hump, a + hump, 4f);
						}
					}
				}
			}
		}

		/// <summary>The drift mouth: a heavy timber frame round the opening in the hill's face, with the mine's name board over it.</summary>
		private static void Portal(Dictionary<string, Batch> b)
		{
			Batch timber = Get(b, "portal", Tex.Timber, new Color(0.8f, 0.75f, 0.7f));
			Road r = Route.Runin;
			int m = RouteData.RuninMouth;
			Vector3 a = r.At(m);
			Vector3 f = r.Forward(m), right = r.Right(m);
			Quaternion turn = Quaternion.LookRotation(f, Vector3.up);
			float h = r.Half + 1.5f;
			for (int k = 0; k < 3; k++)
			{
				Vector3 at = a + f * (k * 3f - 1f);
				timber.Box(at - right * h + Vector3.up * (Height * 0.5f), new Vector3(2.4f, Height + 4f, 2.4f), turn, 4f);
				timber.Box(at + right * h + Vector3.up * (Height * 0.5f), new Vector3(2.4f, Height + 4f, 2.4f), turn, 4f);
				timber.Box(at + Vector3.up * (Height + 1f), new Vector3(2f * h + 4f, 2.4f, 2.4f), turn, 4f);
			}
			Batch board = Get(b, "name board", Tex.Boards, new Color(0.9f, 0.9f, 0.9f));
			board.Box(a - f * 1.6f + Vector3.up * (Height + 6.5f), new Vector3(34f, 7f, 0.6f), turn, 6f);
			SceneBuilder.Labels.Add(new Label
			{
				Pos = a - f * 2.05f + Vector3.up * (Height + 6.5f),
				Facing = -f,
				Text = "EUREKA MINE",
				Color = new Color(0.95f, 0.85f, 0.55f),
				Size = new Vector2(32f, 6f)
			});
		}
	}
}
