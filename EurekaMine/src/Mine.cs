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
		/// <summary>Floor to roof.</summary>
		internal const float Height = 24f;
		/// <summary>Floor to the top of the straight wall, where the chamfer to the roof starts.</summary>
		internal const float WallTop = 17f;
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
			Rubble(Route.Seam, b);
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
			Batch walls = Get(b, key + " walls", old ? Tex.OldRib : Tex.RibWall, Color.white);
			walls.Collide = true;
			walls.Wall = true;
			walls.Slippery = true;
			walls.Shadows = false;
			Batch roof = Get(b, key + " roof", old ? Tex.Rock : Tex.RoofBolts, old ? new Color(0.7f, 0.68f, 0.65f) : Color.white);
			roof.Collide = true;
			roof.Shadows = false;
			Batch timber = Get(b, "timber sets", Tex.Timber, Color.white);
			timber.Shadows = false;
			Batch lamp = Get(b, "lamps", Tex.Plain, new Color(1f, 0.85f, 0.55f));
			lamp.Emission = new Color(2.4f, 1.7f, 0.8f);
			lamp.Shadows = false;
			Batch cage = Get(b, "lamp cages", Tex.Steel, new Color(0.4f, 0.4f, 0.42f));
			cage.Shadows = false;

			int n = r.Count;
			int last = r.Closed ? n : n - 1;
			int inside = FirstInside(r);
			int setEvery = old ? 2 : 3;
			Dictionary<int, Niche> niches = PlanNiches(r, inside);
			for (int i = 0; i < last; i++)
			{
				Vector3 a = r.At(i), c = r.At(i + 1);
				Vector3 ra = r.Right(i), rc = r.Right(i + 1);
				float ha = r.HalfAt(i), hc = r.HalfAt(i + 1);
				float ka = r.CeilAt(i), kc = r.CeilAt(i + 1);
				float ta = ka - (Height - WallTop), tc = kc - (Height - WallTop);   // the tops of the straight walls
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
				Vector3 aLc = a - ra * (ha - Chamfer) + up * ka, aRc = a + ra * (ha - Chamfer) + up * ka;
				Vector3 cLc = c - rc * (hc - Chamfer) + up * kc, cRc = c + rc * (hc - Chamfer) + up * kc;
				float va = r.S[r.Wrap(i)] / 18f, vc = va + Vector3.Distance(a, c) / 18f;

				bool leftOpen = Route.InsideOther(r, (aL + cL) * 0.5f + up * 2f, 1.5f);
				bool rightOpen = Route.InsideOther(r, (aR + cR) * 0.5f + up * 2f, 1.5f);
				Niche nl = NicheAt(niches, r, i, -1f), nr = NicheAt(niches, r, i, 1f);
				if (!leftOpen)
				{
					// the wall, its u axis running up it (the coal seam is a band in the texture); over a niche only the lintel above it
					float from = nl != null ? nl.High : -1.5f;
					walls.Quad(aL + up * from, aL + up * ta, cL + up * tc, cL + up * from,
						new Vector2(Mathf.Max(0f, from) / ta, va), new Vector2(1f, va), new Vector2(1f, vc), new Vector2(Mathf.Max(0f, from) / tc, vc));
					walls.Quad(aL + up * ta, aLc, cLc, cL + up * tc, 10f);
				}
				if (!rightOpen)
				{
					float from = nr != null ? nr.High : -1.5f;
					walls.Quad(cR + up * from, cR + up * tc, aR + up * ta, aR + up * from,
						new Vector2(Mathf.Max(0f, from) / tc, vc), new Vector2(1f, vc), new Vector2(1f, va), new Vector2(Mathf.Max(0f, from) / ta, va));
					walls.Quad(cR + up * tc, cRc, aRc, aR + up * ta, 10f);
				}
				// the roof (left in even over a junction: there both roofs are at about the same height)
				roof.Quad(aRc, cRc, cLc, aLc, 16f);
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
				if (i % setEvery == 0 && nl == null && nr == null)
				{
					// a set: two posts against the walls and a cap under the roof
					float lean = old ? ((i * 37) % 7 - 3) * 0.6f : 0f;
					timber.Box(a - ra * (ha - 0.9f) + up * (ta * 0.5f), new Vector3(1.6f, ta + 3f, 1.6f), turn * Quaternion.Euler(0f, 0f, lean), 4f);
					timber.Box(a + ra * (ha - 0.9f) + up * (ta * 0.5f), new Vector3(1.6f, ta + 3f, 1.6f), turn * Quaternion.Euler(0f, 0f, -lean), 4f);
					timber.Box(a + up * (ta + 1f), new Vector3(2f * ha - 0.6f, 1.6f, 1.8f), turn, 4f);
				}
				bool chamber = ha > r.Half + 2f;
				if (!old && (i % 5 == 2 || chamber && i % 2 == 0))
				{
					foreach (float side in chamber ? new[] { -0.45f, 0.45f } : new[] { 0.08f })
					{
						Vector3 at = a + up * (ta + 0.2f) + ra * (ha * side);
						lamp.Box(at, new Vector3(1.1f, 1.3f, 1.1f), turn, 1f);
						cage.Box(at + up * 0.9f, new Vector3(1.6f, 0.3f, 1.6f), turn, 1f);
						cage.Box(at + up * 1.6f, new Vector3(0.25f, 1.4f, 0.25f), turn, 1f);
						Lamps.Add(at);
					}
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
					Niche nc = new Niche { Road = r, I = i, Span = span, Side = side, Depth = old ? 34f : big ? 22f : 18f, High = old ? WallTop : big ? 14f : 12f };
					nc.Kind = old ? (count * 7 + (side > 0 ? 3 : 0)) % 5 : big ? 100 + (count / 3) % 6 : count % 7;
					if (!NicheFits(nc))
					{
						continue;
					}
					int key = plan.ContainsKey(i) ? -1 - i : i;
					plan[key] = nc;
				}
			}
			return plan;
		}

		private static bool NicheFits(Niche nc)
		{
			Road r = nc.Road;
			int a = nc.I, b = nc.I + nc.Span;
			if (Vector3.Angle(r.Forward(a), r.Forward(b)) > 9f)
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
			if (r == Route.Main && (InRange(a, Route.Mark("Plane foot") - 2, Route.Seam.Fork + 2) || InRange(a, Route.Mark("Plane head") - 4, Route.Mark("Plane foot") + 2)))
			{
				return false;   // the coal chutes' walls, and the plane where the runaway cars run
			}
			if (r == Route.Chute && (Mathf.Abs(a - Mathf.RoundToInt((r.Count - 1) * 0.3f)) < 5 || Mathf.Abs(a - Mathf.RoundToInt((r.Count - 1) * 0.62f)) < 5))
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
		private static void Rubble(Road r, Dictionary<string, Batch> b)
		{
			Batch rock = Get(b, "rubble", Tex.Rock, new Color(0.5f, 0.48f, 0.46f));
			rock.Shadows = false;
			System.Random rng = new System.Random(19);
			for (int i = 4; i < r.Count - 4; i++)
			{
				if (Route.InsideOther(r, r.At(i), 0f))
				{
					continue;
				}
				for (int k = 0; k < 2; k++)
				{
					float side = (rng.NextDouble() < 0.5 ? -1f : 1f) * (r.Half - 1.5f - (float)rng.NextDouble() * 2f);
					Vector3 at = r.At(i) + r.Right(i) * side + r.Forward(i) * (float)(rng.NextDouble() * 8.0);
					float s = 1.2f + (float)rng.NextDouble() * 2.2f;
					rock.Box(at + Vector3.up * s * 0.3f, new Vector3(s * 1.4f, s * 0.8f, s), Quaternion.Euler((float)rng.NextDouble() * 30f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 30f), 3f);
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
