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
			uint h = (uint)(x * 374761393 + y * 668265263 + z * 2147483647 + seed * 1274126177);
			h = (h ^ (h >> 13)) * 1274126177u;
			h ^= h >> 16;
			return (h & 0xFFFFFF) / (float)0x1000000;
		}

		/// <summary>How far the rock stands back from the smooth wall the cars touch (always back, never into the road): two scales of noise.</summary>
		private static float RibDepth(Vector3 p)
		{
			return 0.5f + 1.6f * Noise(p, 14f, 1) + 0.9f * Noise(p, 4.5f, 2);
		}

		private static float RoofRise(Vector3 p)
		{
			return 1.4f * Noise(p, 12f, 3) + 0.6f * Noise(p, 4f, 4);
		}

		/// <summary>
		/// What a stretch of entry is like, changing every 220 units or so: timbered (sets close together, header boards across the roof
		/// between them), bolted (no sets, steel straps across the bolted roof) or both (sets far apart over bolts).
		/// </summary>
		private static int Support(Road r, int i)
		{
			if (r == Route.Seam)
			{
				return 0;
			}
			return (int)(Hash((int)(r.S[r.Wrap(i)] / 220f), r.Count, 0, 9) * 3f);
		}

		// ---- the tunnels -----------------------------------------------------------------------------------------------------

		private static void Tunnel(Road r, Dictionary<string, Batch> b)
		{
			string key = r == Route.Main ? "haulage" : r == Route.Seam ? "seam" : r == Route.Chute ? "chute" : "slope";
			bool old = r == Route.Seam;
			Batch floor = Get(b, key + " floor", r.Ground == Surface.Coal ? Tex.Coal : old ? Tex.Culm : Tex.MineFloor,
				r.Ground == Surface.Coal ? new Color(1f, 1f, 1f) : old ? new Color(0.85f, 0.82f, 0.8f) : new Color(1f, 0.97f, 0.94f));
			floor.Collide = true;
			floor.Ground = r.Ground;
			floor.Shadows = false;
			Batch yard = Get(b, "yard road", Tex.Gravel, new Color(0.8f, 0.78f, 0.74f));
			yard.Collide = true;
			yard.Ground = Surface.Yard;
			yard.Shadows = false;
			// the rock the player sees: rough, drawn only
			Batch ribs = Get(b, key + " ribs", old ? Tex.OldRib : Tex.RibWall, Color.white);
			ribs.Shadows = false;
			Batch roof = Get(b, key + " roof", old ? Tex.Rock : Tex.RoofBolts, old ? new Color(0.7f, 0.68f, 0.65f) : Color.white);
			roof.Shadows = false;
			// the smooth walls and roof the cars touch, never drawn: straight across every side passage, so nothing catches on an opening
			Batch guard = Get(b, "guard walls", null, Color.white);
			guard.Collide = true;
			guard.Wall = true;
			guard.Slippery = true;
			guard.Invisible = true;
			Batch lid = Get(b, "guard roof", null, Color.white);
			lid.Collide = true;
			lid.Invisible = true;
			Batch timber = Get(b, "timber sets", Tex.Timber, Color.white);
			timber.Shadows = false;
			Batch steel = Get(b, "roof straps", Tex.Steel, new Color(0.45f, 0.44f, 0.42f));
			steel.Shadows = false;
			Batch bulb = Get(b, "bulbs", Tex.Plain, new Color(1f, 0.85f, 0.55f));
			bulb.Emission = new Color(2.6f, 1.8f, 0.8f);
			bulb.Shadows = false;
			Batch dead = Get(b, "dead bulbs", Tex.Plain, new Color(0.25f, 0.23f, 0.2f));
			dead.Shadows = false;
			Batch wire = Get(b, "lamp wire", Tex.Belt, Color.white);
			wire.Shadows = false;

			int n = r.Count;
			int last = r.Closed ? n : n - 1;
			int inside = FirstInside(r);
			Dictionary<int, Niche> niches = PlanNiches(r, inside);
			int nextBulb = 2;
			for (int i = 0; i < last; i++)
			{
				Vector3 a = r.At(i), c = r.At(i + 1);
				Vector3 ra = r.Right(i), rc = r.Right(i + 1);
				float ha = r.HalfAt(i), hc = r.HalfAt(i + 1);
				float ka = r.CeilAt(i), kc = r.CeilAt(i + 1);
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

				// the smooth guard: walls at the road's edge and a flat roof
				if (!leftOpen)
				{
					guard.Quad(aL + up * -1.5f, aL + up * ka, cL + up * kc, cL + up * -1.5f, 10f);
				}
				if (!rightOpen)
				{
					guard.Quad(cR + up * -1.5f, cR + up * kc, aR + up * ka, aR + up * -1.5f, 10f);
				}
				lid.Quad(a + ra * ha + up * ka, c + rc * hc + up * kc, c - rc * hc + up * kc, a - ra * ha + up * ka, 10f);

				// the rock: each side's rib in rows up from the floor, broken back to a roof in strips, all pushed back by noise
				float va = r.S[r.Wrap(i)] / 18f, vc = va + Vector3.Distance(a, c) / 18f;
				float ta = ka - (Height - WallTop), tc = kc - (Height - WallTop);
				Vector3[] pa = Section(r, i, niches), pc = Section(r, i + 1, niches);
				int m = pa.Length;
				int mid = m / 2;
				for (int k = 0; k < m - 1; k++)
				{
					bool leftSide = k < Rows;
					bool rightSide = k >= m - 1 - Rows;
					if (leftSide && leftOpen || rightSide && rightOpen)
					{
						continue;
					}
					if (leftSide)
					{
						// rows of the left rib, bottom up: the coal band is in the texture, u running up the rib
						ribs.Quad(pa[k], pa[k + 1], pc[k + 1], pc[k], new Vector2(URow(pa[k], a, ta), va), new Vector2(URow(pa[k + 1], a, ta), va), new Vector2(URow(pc[k + 1], c, tc), vc), new Vector2(URow(pc[k], c, tc), vc));
					}
					else if (rightSide)
					{
						// the right rib's points run top down, so the same order faces into the tunnel
						ribs.Quad(pa[k], pa[k + 1], pc[k + 1], pc[k], new Vector2(URow(pa[k], a, ta), va), new Vector2(URow(pa[k + 1], a, ta), va), new Vector2(URow(pc[k + 1], c, tc), vc), new Vector2(URow(pc[k], c, tc), vc));
					}
					else
					{
						// the roof strips, wound to face down
						roof.Quad(pa[k + 1], pc[k + 1], pc[k], pa[k], new Vector2((k + 1) * 0.4f, va), new Vector2((k + 1) * 0.4f, vc), new Vector2(k * 0.4f, vc), new Vector2(k * 0.4f, va));
					}
				}
				if (niches.TryGetValue(i, out Niche start))
				{
					BuildNiche(start, b);
				}
				if (niches.TryGetValue(-1 - i, out Niche start2))
				{
					BuildNiche(start2, b);
				}
				if (leftOpen || rightOpen)
				{
					continue;
				}

				Quaternion turn = Quaternion.LookRotation(r.Forward(i), Vector3.up);
				int support = Support(r, i);
				int setEvery = old ? 2 : support == 0 ? 2 : support == 2 ? 4 : 0;
				float top = ka - 0.9f;
				if (setEvery > 0 && i % setEvery == 0 && nl == null && nr == null)
				{
					// a set: two posts standing in against the ribs (their faces flush with the guard, so nothing stands in the road)
					// and a cap under the roof; the old workings' lean
					float lean = old ? ((i * 37) % 7 - 3) * 0.7f : ((i * 13) % 5 - 2) * 0.25f;
					timber.Box(a - ra * (ha + 0.85f) + up * (top * 0.5f - 0.5f), new Vector3(1.7f, top + 1f, 1.7f), turn * Quaternion.Euler(0f, 0f, lean), 4f);
					timber.Box(a + ra * (ha + 0.85f) + up * (top * 0.5f - 0.5f), new Vector3(1.7f, top + 1f, 1.7f), turn * Quaternion.Euler(0f, 0f, -lean), 4f);
					timber.Box(a + up * (top + 0.2f), new Vector3(2f * ha + 3f, 1.5f, 1.8f), turn, 4f);
					if (support == 0 || old)
					{
						// header boards laid across the caps to the next set
						for (int k = -2; k <= 2; k++)
						{
							Vector3 p = Vector3.Lerp(a, c, 0.5f) + Vector3.Lerp(ra, rc, 0.5f) * (k * ha * 0.38f) + up * (top + 1.1f);
							timber.Box(p, new Vector3(1.4f, 0.35f, 8f * setEvery - 1f), turn * Quaternion.Euler(0f, ((i + k) % 3 - 1) * 2f, 0f), 4f);
						}
					}
				}
				else if (support == 1 && i % 2 == 1)
				{
					// a steel strap across the bolted roof, a plate at each bolt
					Vector3 p = a + up * (ka + 0.05f);
					steel.Box(p, new Vector3(2f * ha - 1f, 0.2f, 0.9f), turn, 3f);
					for (int k = -2; k <= 2; k++)
					{
						steel.Box(p + ra * (k * ha * 0.42f) + up * -0.15f, new Vector3(1.1f, 0.25f, 1.1f), turn, 1f);
					}
				}
				// bare bulbs on a wire along the roof, at uneven spacing, some dead: never the even row of fittings a road tunnel has
				bool chamber = ha > r.Half + 2f;
				if (!old && i >= nextBulb)
				{
					float h = Hash(i, r.Count, 1, 11);
					nextBulb = i + (chamber ? 2 : 3 + (int)(h * 4f));
					foreach (float side in chamber ? new[] { -0.4f, 0.4f } : new[] { 0.3f })
					{
						Vector3 at = a + ra * (ha * side) + up * (ka - 1.6f);
						bool lit = Hash(i, (int)(side * 10f), 2, 12) > 0.2f;
						(lit ? bulb : dead).Box(at, new Vector3(0.7f, 0.9f, 0.7f), turn, 1f);
						wire.Box(at + up * 0.75f, new Vector3(0.12f, 0.8f, 0.12f), turn, 1f);
						if (lit)
						{
							Lamps.Add(at);
						}
					}
				}
				if (!old)
				{
					// the lamp wire itself, sagging between hangers
					Vector3 w0 = a + ra * (ha * 0.3f) + up * (ka - 0.6f), w1 = c + rc * (hc * 0.3f) + up * (kc - 0.6f);
					Vector3 sag = (w0 + w1) * 0.5f + up * -0.35f;
					wire.Cylinder(w0, sag, 0.08f, 3, 2f, false);
					wire.Cylinder(sag, w1, 0.08f, 3, 2f, false);
				}
			}
		}

		/// <summary>Rows of each rib from the floor to its top (the roof follows in strips).</summary>
		private const int Rows = 4;
		private const int RoofStrips = 5;

		/// <summary>u up a rib: its height over the rib's top there (taller in a chamber, where the seam is shown thicker), never past the texture.</summary>
		private static float URow(Vector3 p, Vector3 floor, float wallTop)
		{
			return Mathf.Clamp01((p.y - floor.y) / wallTop);
		}

		/// <summary>
		/// The cross-section of the rock at point i, from the left rib's foot over the roof to the right rib's foot: each rib in Rows rows,
		/// the roof in RoofStrips strips. Every vertex stands back from the guard by the noise at its place, so ribs and roof are rough;
		/// where a side passage opens the rib starts above it, and at a passage's edges the rib stands on the guard line, so the
		/// passage's box meets it.
		/// </summary>
		private static Vector3[] Section(Road r, int i, Dictionary<int, Niche> niches)
		{
			Vector3 p = r.At(i), right = r.Right(i), up = Vector3.up;
			float h = r.HalfAt(i), ceil = r.CeilAt(i);
			float wallTop = ceil - (Height - WallTop);
			List<Vector3> pts = new List<Vector3>();
			foreach (float side in new[] { -1f, 1f })
			{
				Niche nc = null;
				foreach (Niche x in niches.Values)
				{
					int ii = r.Wrap(i);
					if (x.Side == side && ii >= x.I && ii <= x.I + x.Span)
					{
						nc = x;
					}
				}
				float from = nc != null ? nc.High : -1.5f;
				List<Vector3> rib = new List<Vector3>();
				for (int k = 0; k <= Rows; k++)
				{
					float y = Mathf.Lerp(from, wallTop, (float)k / Rows);
					Vector3 at = p + right * (side * h) + up * y;
					float back = nc != null ? 0f : RibDepth(at) * Mathf.Clamp01((y + 1.5f) / 3f + 0.3f);
					rib.Add(at + right * (side * back));
				}
				if (side > 0f)
				{
					rib.Reverse();
				}
				if (side < 0f)
				{
					pts.AddRange(rib);
					// the roof, left to right: broken back from the ribs' tops, rough
					for (int k = 1; k < RoofStrips; k++)
					{
						float f = (float)k / RoofStrips;
						float lateral = Mathf.Lerp(-(h - Chamfer), h - Chamfer, (f - 0.2f) / 0.6f);
						lateral = Mathf.Clamp(lateral, -(h - Chamfer * 0.2f), h - Chamfer * 0.2f);
						float y = k == 1 || k == RoofStrips - 1 ? Mathf.Lerp(wallTop, ceil, 0.8f) : ceil;
						Vector3 at = p + right * lateral + up * y;
						pts.Add(at + up * RoofRise(at));
					}
				}
				else
				{
					pts.AddRange(rib);
				}
			}
			return pts.ToArray();
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
					new Niche { Road = r, I = Route.Mark("Plane head") + 2, Span = 3, Side = -1f, Depth = 40f, High = 11.5f, Kind = 400 },
					new Niche { Road = r, I = Route.Mark("Plane foot") - 2, Span = 3, Side = -1f, Depth = 40f, High = 11.5f, Kind = 401 }
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
			int every = old ? 3 : 7;
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
				foreach (float side in old ? new[] { -1f, 1f } : new[] { count % 2 == 0 ? -1f : 1f })
				{
					Niche nc = new Niche { Road = r, I = i, Span = span, Side = side, Depth = old ? 34f : big ? 22f : 18f, High = old ? WallTop - 0.5f : big ? 11.5f : 10f };
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

		/// <summary>The passage's box: a floor, a ceiling, the back and two sides, all solid and seen from inside, and then what is in it.</summary>
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
			// the back is level with the mouth's lower corner, so the floor is flat
			float y = Mathf.Min(pa.y, pc.y);
			Vector3 ba = pa + o * nc.Depth, bc = pc + o * nc.Depth;
			ba.y = bc.y = y;
			Vector3 up = Vector3.up * nc.High;
			Batch walls = Get(b, old ? "seam rooms" : "niche walls", old ? Tex.OldRib : Tex.RibWall, old ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.85f, 0.84f, 0.8f));
			walls.Collide = true;
			walls.Wall = true;
			walls.Shadows = false;
			Batch floor = Get(b, "niche floors", old ? Tex.Culm : Tex.MineFloor, new Color(0.8f, 0.78f, 0.76f));
			floor.Collide = true;
			floor.Shadows = false;
			Batch roof = Get(b, "niche roofs", Tex.RoofBolts, new Color(0.75f, 0.74f, 0.72f));
			roof.Shadows = false;
			Vector3 lo = Vector3.down * 1.5f;
			floor.QuadBoth(pa, pc, bc, ba, 12f);
			roof.QuadBoth(pa + up, pc + up, bc + up, ba + up, 16f);
			// one tile up the wall's height, as on the tunnel walls, so the coal band runs on round into the passage at the same height
			walls.QuadBoth(ba + lo, ba + up, bc + up, bc + lo, WallTop);
			walls.QuadBoth(pa + lo, pa + up, ba + up, ba + lo, WallTop);
			walls.QuadBoth(pc + lo, pc + up, bc + up, bc + lo, WallTop);
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
			Batch rock = Get(b, "rubble", Tex.Rock, new Color(0.5f, 0.48f, 0.46f));
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
						int pieces = old ? 3 : 1 + rng.Next(2);
						for (int k = 0; k < pieces; k++)
						{
							float s = 1.5f + (float)rng.NextDouble() * (old ? 3f : 1.8f);
							Vector3 at = edge + r.Right(i) * side * (s * 0.45f) + r.Forward(i) * (float)(rng.NextDouble() * 8.0);
							rock.Box(at + Vector3.up * s * 0.25f, new Vector3(s * 1.4f, s * 0.8f, s), Quaternion.Euler((float)rng.NextDouble() * 30f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 30f), 3f);
						}
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
