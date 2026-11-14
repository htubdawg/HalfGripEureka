using System.Collections.Generic;
using UnityEngine;

namespace EurekaMine
{
	/// <summary>
	/// The pithead, where the race starts: the hill with the drift mouth in its face (cut back to a highwall on three sides, as a strip
	/// mine leaves it), the breaker over the yard with its conveyor up the hill and a loading gallery across the road, culm banks, a
	/// siding of hopper cars, the headframe on the hill, the boiler house stack, a row of company double houses and the patch's church.
	/// Nobody is about.
	/// </summary>
	internal static class Pithead
	{
		/// <summary>The hill: from the drift mouth's face east to its back edge, and north to south.</summary>
		internal const float HillX0 = -1200f, HillX1 = -760f, HillBackFoot = -660f, HillZ0 = -330f, HillZ1 = 170f, HillTop = 50f;

		internal static void Build(Dictionary<string, Batch> b)
		{
			System.Random rng = new System.Random(1902);
			Ground(b);
			Hill(b);
			Breaker(b);
			CulmBanks(b, rng);
			Siding(b);
			Village.Build(b, rng);
			Headframe(b);
			Stack(b);
			YardDressing(b);
		}

		/// <summary>
		/// The yard round the grid: poles carrying the power down the road to the drift, the brick lamp house and wash house where the
		/// shift changes, a timber yard, and the safety board facing the grid.
		/// </summary>
		private static void YardDressing(Dictionary<string, Batch> b)
		{
			Batch pole = Get(b, "poles", Tex.Timber, new Color(0.6f, 0.55f, 0.5f));
			Batch wire = Get(b, "wires", Tex.Belt, Color.white);
			Road r = Route.Runin;
			Vector3 last0 = Vector3.zero, last1 = Vector3.zero;
			bool first = true;
			for (int k = 0; k <= RouteData.RuninMouth; k += 5)
			{
				Vector3 at = r.At(k) - r.Right(k) * (r.Half + 9f);
				pole.Cylinder(at, at + Vector3.up * 30f, 0.7f, 6, 4f, false);
				pole.Box(at + Vector3.up * 27.5f, new Vector3(0.8f, 0.8f, 9f), Quaternion.LookRotation(r.Forward(k), Vector3.up) * Quaternion.Euler(0f, 90f, 0f), 3f);
				Vector3 w0 = at + Vector3.up * 28f + r.Forward(k) * 3.5f, w1 = at + Vector3.up * 28f - r.Forward(k) * 3.5f;
				if (!first)
				{
					foreach (Vector3[] span in new[] { new[] { last0, w0 }, new[] { last1, w1 } })
					{
						Vector3 mid = (span[0] + span[1]) * 0.5f + Vector3.down * 2.5f;
						wire.Cylinder(span[0], mid, 0.12f, 3, 2f, false);
						wire.Cylinder(mid, span[1], 0.12f, 3, 2f, false);
					}
				}
				last0 = w0;
				last1 = w1;
				first = false;
			}

			Batch brick = Get(b, "boiler house", Tex.Brick, new Color(0.9f, 0.9f, 0.9f), true);
			brick.Wall = true;
			Batch roof = Get(b, "breaker roofs", Tex.Corrugated, new Color(0.6f, 0.58f, 0.56f));
			Batch windows = Get(b, "breaker windows", Tex.Plain, new Color(0.95f, 0.8f, 0.45f));
			// the lamp house and the wash house, south of the road beyond the siding, end on to it
			foreach (float x in new[] { -1660f, -1560f })
			{
				brick.Gable(new Vector3(x, 0f, -270f), 40f, 26f, 18f, 7f, Quaternion.identity, 8f, roof);
				for (float wx = x - 14f; wx <= x + 14f; wx += 7f)
				{
					windows.Quad(new Vector3(wx + 1.5f, 6f, -257.1f), new Vector3(wx + 1.5f, 11f, -257.1f), new Vector3(wx - 1.5f, 11f, -257.1f), new Vector3(wx - 1.5f, 6f, -257.1f), 4f);
				}
			}
			SceneBuilder.Labels.Add(new Label { Pos = new Vector3(-1660f, 21f, -256.6f), Facing = Vector3.forward, Text = "LAMP HOUSE", Color = new Color(0.95f, 0.9f, 0.8f), Size = new Vector2(30f, 4f) });
			SceneBuilder.Labels.Add(new Label { Pos = new Vector3(-1560f, 21f, -256.6f), Facing = Vector3.forward, Text = "WASH HOUSE", Color = new Color(0.95f, 0.9f, 0.8f), Size = new Vector2(30f, 4f) });

			// the timber yard north of the road, by the breaker
			Batch logs = Get(b, "timber yard", Tex.Timber, new Color(0.75f, 0.68f, 0.6f), true);
			for (int pile = 0; pile < 3; pile++)
			{
				for (int k = 0; k < 9; k++)
				{
					logs.Box(new Vector3(-1660f + pile * 28f, 1f + (k / 3) * 2f, 20f + (k % 3) * 2.2f), new Vector3(22f, 1.9f, 2f), Quaternion.identity, 4f);
				}
			}

			// the safety board facing the grid, on posts beside the road
			Batch board = Get(b, "safety board", Tex.Enamel, new Color(0.93f, 0.9f, 0.82f));
			int bi = RouteData.RuninMouth - 3;
			Vector3 c = r.At(bi) + r.Right(bi) * (r.Half + 16f) + Vector3.up * 16f;
			board.Box(c, new Vector3(1f, 12f, 30f), Quaternion.identity, 6f);
			pole.Box(c + new Vector3(0f, -11f, -10f), new Vector3(1.2f, 12f, 1.2f), Quaternion.identity, 3f);
			pole.Box(c + new Vector3(0f, -11f, 10f), new Vector3(1.2f, 12f, 1.2f), Quaternion.identity, 3f);
			SceneBuilder.Labels.Add(new Label { Pos = c + Vector3.left * 0.6f, Facing = Vector3.left, Text = "EUREKA MINE\nSAFETY FIRST\nTHINK - THEN ACT", Color = new Color(0.75f, 0.08f, 0.05f), Size = new Vector2(28f, 10f) });
			// and over the drift mouth
			Vector3 m = r.At(RouteData.RuninMouth);
			board.Box(new Vector3(HillX0 - 0.6f, Mine.Height + 13f, m.z), new Vector3(0.6f, 5f, 40f), Quaternion.identity, 6f);
			SceneBuilder.Labels.Add(new Label { Pos = new Vector3(HillX0 - 1f, Mine.Height + 13f, m.z), Facing = Vector3.left, Text = "SAFETY FIRST", Color = new Color(0.75f, 0.08f, 0.05f), Size = new Vector2(38f, 4.5f) });
		}

		private static Batch Get(Dictionary<string, Batch> b, string name, Texture2D tex, Color tint, bool collide = false)
		{
			if (!b.TryGetValue(name, out Batch batch))
			{
				batch = new Batch(name, tex, tint) { Collide = collide };
				b[name] = batch;
			}
			return batch;
		}

		/// <summary>Flat country at y 0 round the hill (the hill covers its own footprint), with a gravel yard round the grid.</summary>
		private static void Ground(Dictionary<string, Batch> b)
		{
			Batch grass = Get(b, "ground", Tex.Grass, new Color(0.85f, 0.85f, 0.8f), true);
			grass.Shadows = false;
			const float E = 5000f;
			Rect(grass, -E, HillX0, -E, E, 0f);
			Rect(grass, HillBackFoot, E, -E, E, 0f);
			Rect(grass, HillX0, HillBackFoot, -E, HillZ0, 0f);
			Rect(grass, HillX0, HillBackFoot, HillZ1, E, 0f);
			Batch yard = Get(b, "yard", Tex.Gravel, new Color(0.7f, 0.68f, 0.65f), true);
			yard.Shadows = false;
			yard.Ground = Surface.Yard;
			Rect(yard, -1720f, HillX0, -300f, 140f, 0.03f);
		}

		private static void Rect(Batch b, float x0, float x1, float z0, float z1, float y)
		{
			b.Quad(new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), new Vector3(x0, y, z0), 16f);
		}

		/// <summary>The hill: a flat top, a vertical face with the drift mouth's opening, vertical highwalls north and south, a slope at the back.</summary>
		private static void Hill(Dictionary<string, Batch> b)
		{
			Batch rock = Get(b, "highwall", Tex.Rock, new Color(0.8f, 0.78f, 0.74f), true);
			rock.Wall = true;
			Batch top = Get(b, "hill top", Tex.Grass, new Color(0.75f, 0.78f, 0.7f), true);
			float t = HillTop;
			// the face, round the opening of the drift (its width and height a little more than the tunnel's)
			Road r = Route.Runin;
			Vector3 mouth = r.At(RouteData.RuninMouth);
			float oz0 = mouth.z - r.Half - 1.5f, oz1 = mouth.z + r.Half + 1.5f, oy = Mine.Height + 0.5f;
			float x = HillX0;
			FaceWest(rock, x, HillZ0, oz0, 0f, t);
			FaceWest(rock, x, oz1, HillZ1, 0f, t);
			FaceWest(rock, x, oz0, oz1, oy, t);
			// the top
			top.Quad(new Vector3(HillX0, t, HillZ1), new Vector3(HillX1, t, HillZ1), new Vector3(HillX1, t, HillZ0), new Vector3(HillX0, t, HillZ0), 16f);
			// the back slope
			top.Quad(new Vector3(HillX1, t, HillZ1), new Vector3(HillBackFoot, 0f, HillZ1), new Vector3(HillBackFoot, 0f, HillZ0), new Vector3(HillX1, t, HillZ0), 16f);
			// the highwalls north and south, and the triangles under the back slope
			rock.Quad(new Vector3(HillX1, 0f, HillZ1), new Vector3(HillX1, t, HillZ1), new Vector3(HillX0, t, HillZ1), new Vector3(HillX0, 0f, HillZ1), 12f);
			rock.Quad(new Vector3(HillX0, 0f, HillZ0), new Vector3(HillX0, t, HillZ0), new Vector3(HillX1, t, HillZ0), new Vector3(HillX1, 0f, HillZ0), 12f);
			rock.Tri(new Vector3(HillX1, 0f, HillZ1), new Vector3(HillBackFoot, 0f, HillZ1), new Vector3(HillX1, t, HillZ1), 12f);
			rock.Tri(new Vector3(HillX1, t, HillZ0), new Vector3(HillBackFoot, 0f, HillZ0), new Vector3(HillX1, 0f, HillZ0), 12f);
			// benches cut into the highwalls: a ledge every 16 units, as a strip pit leaves them
			Batch bench = Get(b, "benches", Tex.Gravel, new Color(0.55f, 0.53f, 0.5f));
			for (float y = 16f; y < t - 4f; y += 16f)
			{
				bench.Box(new Vector3((HillX0 + HillX1) * 0.5f, y, HillZ1 + 2f), new Vector3(HillX1 - HillX0, 1.2f, 4f), Quaternion.identity, 8f);
				bench.Box(new Vector3((HillX0 + HillX1) * 0.5f, y, HillZ0 - 2f), new Vector3(HillX1 - HillX0, 1.2f, 4f), Quaternion.identity, 8f);
			}
		}

		/// <summary>A piece of the west face at x, from z0 to z1 and height y0 to y1, looking west.</summary>
		private static void FaceWest(Batch b, float x, float z0, float z1, float y0, float y1)
		{
			b.Quad(new Vector3(x, y0, z1), new Vector3(x, y1, z1), new Vector3(x, y1, z0), new Vector3(x, y0, z0), 12f);
		}

		/// <summary>
		/// The breaker: a stepped tower of dust-black boards north of the road, its tallest part up against the hill, a conveyor gallery
		/// slanting up to it from the hilltop, and a loading gallery that crosses the road on trestles to the culm bank's side.
		/// </summary>
		private static void Breaker(Dictionary<string, Batch> b)
		{
			Batch boards = Get(b, "breaker", Tex.Boards, new Color(0.9f, 0.9f, 0.9f), true);
			boards.Wall = true;
			Batch roof = Get(b, "breaker roofs", Tex.Corrugated, new Color(0.6f, 0.58f, 0.56f));
			Batch timber = Get(b, "trestles", Tex.Timber, new Color(0.7f, 0.65f, 0.6f), true);
			timber.Wall = true;
			Batch windows = Get(b, "breaker windows", Tex.Plain, new Color(0.95f, 0.8f, 0.45f));
			windows.Emission = new Color(0.9f, 0.6f, 0.2f);
			Quaternion q = Quaternion.identity;
			// three steps, the highest at the back (east): coal goes in at the top and falls through the breaker by gravity
			Vector3 c = new Vector3(-1390f, 0f, 90f);
			boards.Gable(c + new Vector3(-55f, 0f, 0f), 50f, 60f, 34f, 10f, q, 8f, roof);
			boards.Gable(c + new Vector3(-5f, 0f, 0f), 50f, 64f, 58f, 12f, q, 8f, roof);
			boards.Gable(c + new Vector3(45f, 0f, 0f), 50f, 68f, 86f, 14f, q, 8f, roof);
			boards.Gable(c + new Vector3(45f, 86f, 0f), 22f, 26f, 18f, 8f, q, 6f, roof);
			// rows of small lit windows
			for (int step = 0; step < 3; step++)
			{
				float h = new[] { 34f, 58f, 86f }[step];
				float x0 = c.x - 55f + step * 50f;
				for (float y = 10f; y < h - 6f; y += 12f)
				{
					for (float x = x0 - 18f; x <= x0 + 18f; x += 9f)
					{
						windows.Quad(new Vector3(x - 1.6f, y, c.z - 30.5f - step * 2f), new Vector3(x - 1.6f, y + 3.5f, c.z - 30.5f - step * 2f),
							new Vector3(x + 1.6f, y + 3.5f, c.z - 30.5f - step * 2f), new Vector3(x + 1.6f, y, c.z - 30.5f - step * 2f), 4f);
					}
				}
			}
			// the conveyor gallery from the hilltop down to the top of the breaker
			Vector3 g0 = new Vector3(-1060f, HillTop + 10f, 60f), g1 = new Vector3(c.x + 56f, 96f, 80f);
			Quaternion gq = Quaternion.LookRotation(g1 - g0, Vector3.up);
			boards.Box((g0 + g1) * 0.5f, new Vector3(9f, 8f, (g1 - g0).magnitude), gq, 6f);
			for (float f = 0.15f; f < 0.95f; f += 0.2f)
			{
				Vector3 p = Vector3.Lerp(g0, g1, f);
				timber.Box(new Vector3(p.x, p.y * 0.5f, p.z), new Vector3(2.5f, p.y, 2.5f), q, 4f);
			}
			// the loading gallery over the road, from the breaker's front to the bank's side, high enough to drive under
			Vector3 l0 = new Vector3(c.x - 40f, 46f, c.z - 34f), l1 = new Vector3(c.x - 40f, 40f, -260f);
			boards.Box((l0 + l1) * 0.5f, new Vector3(10f, 9f, (l1 - l0).magnitude), Quaternion.LookRotation(l1 - l0, Vector3.up), 6f);
			foreach (float z in new[] { 20f, -30f, -130f, -180f, -230f })
			{
				float y = Mathf.Lerp(l0.y, l1.y, Mathf.InverseLerp(l0.z, l1.z, z)) - 4.5f;
				timber.Box(new Vector3(l0.x - 4f, y * 0.5f, z), new Vector3(2.2f, y, 2.2f), q, 4f);
				timber.Box(new Vector3(l0.x + 4f, y * 0.5f, z), new Vector3(2.2f, y, 2.2f), q, 4f);
			}
		}

		private static void CulmBanks(Dictionary<string, Batch> b, System.Random rng)
		{
			Batch culm = Get(b, "culm banks", Tex.Culm, new Color(0.9f, 0.9f, 0.9f), true);
			culm.Heap(new Vector3(-1420f, 0f, -560f), 240f, 120f, 28, 6, 14f, rng, 10f);
			culm.Heap(new Vector3(-1820f, 0f, -470f), 150f, 64f, 22, 5, 10f, rng, 10f);
			culm.Heap(new Vector3(-1080f, 0f, -560f), 110f, 48f, 20, 4, 8f, rng, 10f);
		}

		/// <summary>A siding south of the road with a string of loaded hopper cars.</summary>
		private static void Siding(Dictionary<string, Batch> b)
		{
			Batch steel = Get(b, "hoppers", Tex.Corrugated, new Color(0.45f, 0.3f, 0.25f), true);
			steel.Wall = true;
			Batch coal = Get(b, "hopper coal", Tex.Coal, Color.white);
			Batch rails = Get(b, "siding rails", Tex.Steel, new Color(0.6f, 0.6f, 0.62f));
			Batch ties = Get(b, "siding ties", Tex.Timber, new Color(0.5f, 0.45f, 0.4f));
			const float z = -210f;
			for (float x = -1900f; x < -1260f; x += 6f)
			{
				ties.Box(new Vector3(x, 0.1f, z), new Vector3(1.4f, 0.25f, 10f), Quaternion.identity, 4f);
			}
			foreach (float side in new[] { -2.8f, 2.8f })
			{
				rails.Box(new Vector3(-1580f, 0.4f, z + side), new Vector3(640f, 0.5f, 0.5f), Quaternion.identity, 4f);
			}
			for (int k = 0; k < 7; k++)
			{
				float x = -1840f + k * 27f;
				// a hopper: a box body over the wheels, with its sloping chutes under it
				steel.Box(new Vector3(x, 8.5f, z), new Vector3(24f, 9f, 9.5f), Quaternion.identity, 6f);
				steel.Box(new Vector3(x - 5f, 3.2f, z), new Vector3(7f, 3f, 7f), Quaternion.Euler(0f, 0f, 20f), 6f);
				steel.Box(new Vector3(x + 5f, 3.2f, z), new Vector3(7f, 3f, 7f), Quaternion.Euler(0f, 0f, -20f), 6f);
				coal.Heap(new Vector3(x, 12.9f, z), 4.8f, 2.4f, 8, 2, 0f, null, 3f);
			}
		}

		/// <summary>The headframe over the air shaft on the hilltop: a steel A-frame with two sheave wheels.</summary>
		private static void Headframe(Dictionary<string, Batch> b)
		{
			Batch steel = Get(b, "headframe", Tex.Steel, new Color(0.35f, 0.3f, 0.28f));
			Vector3 c = new Vector3(-930f, HillTop, -40f);
			const float h = 56f;
			foreach (float dx in new[] { -9f, 9f })
			{
				foreach (float dz in new[] { -7f, 7f })
				{
					steel.Cylinder(c + new Vector3(dx, 0f, dz), c + new Vector3(dx * 0.35f, h, dz * 0.5f), 0.9f, 6, 4f, false);
				}
				steel.Cylinder(c + new Vector3(dx, 0f, 0f), c + new Vector3(dx * 2.6f, h * 0.8f, 0f), 0.8f, 6, 4f, false);
			}
			for (float y = 12f; y < h; y += 12f)
			{
				steel.Box(c + new Vector3(0f, y, -6f), new Vector3(16f - y * 0.18f, 0.8f, 0.8f), Quaternion.identity, 4f);
				steel.Box(c + new Vector3(0f, y, 6f), new Vector3(16f - y * 0.18f, 0.8f, 0.8f), Quaternion.identity, 4f);
			}
			foreach (float dz in new[] { -3f, 3f })
			{
				Vector3 hub = c + new Vector3(0f, h + 4f, dz);
				steel.Cylinder(hub - Vector3.forward * 0.5f, hub + Vector3.forward * 0.5f, 6f, 14, 4f, true);
			}
			steel.Box(c + new Vector3(0f, 6f, 0f), new Vector3(10f, 12f, 10f), Quaternion.identity, 4f);
		}

		/// <summary>The boiler house and its brick stack beside the breaker.</summary>
		private static void Stack(Dictionary<string, Batch> b)
		{
			Batch brick = Get(b, "boiler house", Tex.Brick, new Color(0.9f, 0.9f, 0.9f), true);
			brick.Wall = true;
			Batch roof = Get(b, "breaker roofs", Tex.Corrugated, new Color(0.6f, 0.58f, 0.56f));
			brick.Gable(new Vector3(-1560f, 0f, 200f), 60f, 30f, 22f, 8f, Quaternion.identity, 8f, roof);
			brick.Cylinder(new Vector3(-1600f, 0f, 225f), new Vector3(-1600f, 110f, 225f), 6f, 12, 8f, false);
		}
	}
}
