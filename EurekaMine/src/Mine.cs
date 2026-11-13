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
			foreach (Road r in Route.All)
			{
				Tunnel(r, b);
			}
			Rails(Route.Main, b);
			Rubble(Route.Seam, b);
			Portal(b);
			StartLine(b);
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

		private static void Tunnel(Road r, Dictionary<string, Batch> b)
		{
			string key = r == Route.Main ? "haulage" : r == Route.Seam ? "seam" : r == Route.Chute ? "chute" : "slope";
			Batch floor = Get(b, key + " floor", r.Ground == Surface.Coal ? Tex.Coal : r.Ground == Surface.Rough ? Tex.Culm : Tex.Gravel,
				r.Ground == Surface.Rough ? new Color(0.75f, 0.72f, 0.7f) : new Color(0.62f, 0.6f, 0.58f));
			floor.Collide = true;
			floor.Ground = r.Ground;
			floor.Shadows = false;
			Batch yard = Get(b, "yard road", Tex.Gravel, new Color(0.8f, 0.78f, 0.74f));
			yard.Collide = true;
			yard.Ground = Surface.Yard;
			yard.Shadows = false;
			Batch walls = Get(b, key + " walls", Tex.Rock, r == Route.Seam ? new Color(0.62f, 0.6f, 0.58f) : new Color(0.85f, 0.83f, 0.8f));
			walls.Collide = true;
			walls.Wall = true;
			walls.Slippery = true;
			walls.Shadows = false;
			Batch roof = Get(b, key + " roof", Tex.Rock, new Color(0.55f, 0.53f, 0.5f));
			roof.Collide = true;
			roof.Shadows = false;
			Batch timber = Get(b, "timber sets", Tex.Timber, Color.white);
			timber.Shadows = false;
			Batch lamp = Get(b, "lamps", Tex.Plain, new Color(1f, 0.85f, 0.55f));
			lamp.Emission = new Color(2.4f, 1.7f, 0.8f);
			lamp.Shadows = false;

			float h = r.Half;
			int n = r.Count;
			int last = r.Closed ? n : n - 1;
			int inside = FirstInside(r);
			int setEvery = r == Route.Seam ? 2 : 3;
			int lampEvery = 6;
			for (int i = 0; i < last; i++)
			{
				Vector3 a = r.At(i), c = r.At(i + 1);
				Vector3 ra = r.Right(i), rc = r.Right(i + 1);
				Vector3 aL = a - ra * h, aR = a + ra * h, cL = c - rc * h, cR = c + rc * h;
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
				Vector3 aLc = a - ra * (h - Chamfer) + up * Height, aRc = a + ra * (h - Chamfer) + up * Height;
				Vector3 cLc = c - rc * (h - Chamfer) + up * Height, cRc = c + rc * (h - Chamfer) + up * Height;
				Vector3 down = Vector3.down * 1.5f;

				Vector3 leftBase = (aL + cL) * 0.5f, rightBase = (aR + cR) * 0.5f;
				bool leftOpen = Route.InsideOther(r, leftBase + Vector3.up * 2f, 1.5f);
				bool rightOpen = Route.InsideOther(r, rightBase + Vector3.up * 2f, 1.5f);
				if (!leftOpen)
				{
					walls.Quad(aL + down, aL + up * WallTop, cL + up * WallTop, cL + down, 10f);
					walls.Quad(aL + up * WallTop, aLc, cLc, cL + up * WallTop, 10f);
				}
				if (!rightOpen)
				{
					walls.Quad(cR + down, cR + up * WallTop, aR + up * WallTop, aR + down, 10f);
					walls.Quad(cR + up * WallTop, cRc, aRc, aR + up * WallTop, 10f);
				}
				// the roof (left in even over a junction: there both roofs are at about the same height)
				roof.Quad(aRc, cRc, cLc, aLc, 12f);
				if (leftOpen || rightOpen)
				{
					continue;
				}

				Quaternion turn = Quaternion.LookRotation(r.Forward(i), Vector3.up);
				if (i % setEvery == 0)
				{
					// a set: two posts against the walls and a cap under the roof
					float lean = r == Route.Seam ? ((i * 37) % 7 - 3) * 0.6f : 0f;
					timber.Box(a - ra * (h - 0.9f) + up * (WallTop * 0.5f), new Vector3(1.6f, WallTop + 3f, 1.6f), turn * Quaternion.Euler(0f, 0f, lean), 4f);
					timber.Box(a + ra * (h - 0.9f) + up * (WallTop * 0.5f), new Vector3(1.6f, WallTop + 3f, 1.6f), turn * Quaternion.Euler(0f, 0f, -lean), 4f);
					timber.Box(a + up * (WallTop + 1f), new Vector3(2f * h - 0.6f, 1.6f, 1.8f), turn, 4f);
				}
				if (r != Route.Seam && i % lampEvery == 3)
				{
					Vector3 at = a + up * (WallTop + 0.4f) + ra * 1.5f;
					lamp.Box(at, new Vector3(0.9f, 1.1f, 0.9f), turn, 1f);
					timber.Box(at + up * 1.0f, new Vector3(0.2f, 1.2f, 0.2f), turn, 1f);
					Lamps.Add(at);
				}
			}
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
