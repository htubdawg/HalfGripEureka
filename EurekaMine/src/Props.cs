using System.Collections.Generic;
using UnityEngine;

namespace EurekaMine
{
	/// <summary>
	/// What makes the mine a mine and not a pipe: what stands in the side passages (brattice curtains, stoppings, supplies, barricades,
	/// pumps, first aid, a locomotive and its trip, a continuous miner, a shuttle car, power centres, the dispatcher's shanty), the
	/// water line, power cable and trolley wire along the walls, reflective chevrons on every bend, the signs, the shaft station and
	/// loading tracks at the pit bottom, and the caved rooms of the old workings. Machinery only: nobody is about.
	/// </summary>
	internal static class Props
	{
		private static Dictionary<string, Batch> B;

		private static Batch Get(string name, Texture2D tex, Color tint, bool collide = false)
		{
			if (!B.TryGetValue(name, out Batch batch))
			{
				batch = new Batch(name, tex, tint) { Collide = collide, Shadows = false };
				B[name] = batch;
			}
			return batch;
		}

		// the materials of the props
		private static Batch Timber { get { return Get("prop timber", Tex.Timber, Color.white); } }
		private static Batch Steel { get { return Get("prop steel", Tex.Steel, new Color(0.55f, 0.56f, 0.6f)); } }
		private static Batch DarkSteel { get { return Get("prop dark steel", Tex.Steel, new Color(0.25f, 0.25f, 0.27f)); } }
		private static Batch Rust { get { return Get("prop rust", Tex.Corrugated, new Color(0.62f, 0.38f, 0.26f)); } }
		private static Batch CoalLoad { get { return Get("prop coal", Tex.Coal, Color.white); } }
		private static Batch Rubber { get { return Get("prop rubber", Tex.Belt, Color.white); } }
		private static Batch Machine(string colour, Color tint) { return Get("machine " + colour, Tex.Enamel, tint, true); }
		private static Batch Glow(Color c) { Batch g = Get("glow " + ColorUtility.ToHtmlStringRGB(c), Tex.Plain, c); g.Emission = c * 1.6f; return g; }

		internal static void Build(Dictionary<string, Batch> b)
		{
			B = b;
			foreach (Mine.Niche nc in Mine.Niches)
			{
				Fill(nc);
			}
			Utilities(Route.Main, true);
			Utilities(Route.Runin, false);
			Utilities(Route.Chute, false);
			foreach (Road r in Route.All)
			{
				Chevrons(r);
			}
			PitBottom();
			Signs();
		}

		// ---- frames ----------------------------------------------------------------------------------------------------------

		/// <summary>A niche's own axes: x along its opening, y up, z into it from the tunnel.</summary>
		private struct Frame
		{
			public Vector3 O, X, Z;
			public Quaternion Q;

			public Frame(Vector3 origin, Vector3 into)
			{
				O = origin;
				Z = into.normalized;
				Q = Quaternion.LookRotation(Z, Vector3.up);
				X = Q * Vector3.right;
			}

			public Vector3 P(float x, float y, float z)
			{
				return O + X * x + Vector3.up * y + Z * z;
			}

			/// <summary>A turn about the vertical, applied after the frame's own.</summary>
			public Quaternion R(float yaw)
			{
				return Q * Quaternion.Euler(0f, yaw, 0f);
			}
		}

		/// <summary>True if point i of a road has a niche on that side.</summary>
		private static bool HasNiche(Road r, int i, float side)
		{
			foreach (Mine.Niche nc in Mine.Niches)
			{
				if (nc.Road == r && nc.Side == side && r.Wrap(i) >= nc.I - 1 && r.Wrap(i) <= nc.I + nc.Span + 1)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Is the wall on this side of point i solid (no junction opening, no niche)?</summary>
		private static bool SolidWall(Road r, int i, float side)
		{
			Vector3 p = r.At(i) + r.Right(i) * side * r.HalfAt(i) + Vector3.up * 2f;
			return !Route.InsideOther(r, p, 1.5f) && !HasNiche(r, i, side);
		}

		// ---- the niches ------------------------------------------------------------------------------------------------------

		private static void Fill(Mine.Niche nc)
		{
			Frame f = new Frame(nc.Mouth, nc.Out);
			float w = nc.Width, d = nc.Depth, h = nc.High;
			switch (nc.Road == Route.Seam ? 300 + nc.Kind : nc.Kind)
			{
				case 0: Brattice(f, w, h); break;
				case 1: Stopping(f, w, h); break;
				case 2: CarInNiche(f, w); break;
				case 3: Supplies(f, w); break;
				case 4: Barricade(f, w, h); break;
				case 5: Pump(f, w, d); break;
				case 100: Locomotive(f.P(-5f, 0f, d * 0.55f), Quaternion.LookRotation(f.X, Vector3.up)); MineCar(f.P(9f, 0f, d * 0.55f), Quaternion.LookRotation(f.X, Vector3.up), true); Rails(f.P(-w * 0.5f, 0.2f, d * 0.55f), f.X, w); break;
				case 105: ManTrip(f.P(0f, 0f, d * 0.55f), Quaternion.LookRotation(f.X, Vector3.up)); Rails(f.P(-w * 0.5f, 0.2f, d * 0.55f), f.X, w); break;
				case 301: Heap(f.P(0f, 0f, d * 0.6f), 9f, 6f, 9); break;
				case 302: OldCar(f.P(1f, 0f, d * 0.5f), f.R(70f)); break;
				case 303: Crib(f.P(-3f, 0f, d * 0.5f), f.Q, 6); Crib(f.P(4f, 0f, d * 0.75f), f.R(20f), 5); break;
				case 304: Heap(f.P(0f, 0f, d * 0.85f), 13f, 9f, 11); break;
			}
		}

		/// <summary>A yellow brattice curtain hung across the crosscut on a timber header: it steers the air down the haulage.</summary>
		private static void Brattice(Frame f, float w, float h)
		{
			Batch cloth = Get("brattice", Tex.Brattice, Color.white);
			cloth.TwoSided = true;
			// hung in folds: a zigzag of panels
			int folds = 8;
			for (int k = 0; k < folds; k++)
			{
				float x0 = -w * 0.5f + w * k / folds, x1 = -w * 0.5f + w * (k + 1) / folds;
				float z0 = 3f + (k % 2) * 0.7f, z1 = 3f + ((k + 1) % 2) * 0.7f;
				cloth.Quad(f.P(x0, 0.4f, z0), f.P(x0, h - 0.6f, z0), f.P(x1, h - 0.6f, z1), f.P(x1, 0.4f, z1), 6f);
			}
			Timber.Box(f.P(0f, h - 0.5f, 3.3f), new Vector3(w, 1f, 1f), f.Q, 4f);
		}

		/// <summary>A concrete block stopping sealing the crosscut, with its steel man door.</summary>
		private static void Stopping(Frame f, float w, float h)
		{
			Get("stoppings", Tex.Concrete, Color.white, true).Box(f.P(0f, h * 0.5f, 5f), new Vector3(w, h, 1.6f), f.Q, 6f);
			DarkSteel.Box(f.P(w * 0.2f, 3.8f, 4.1f), new Vector3(4.2f, 7.4f, 0.3f), f.Q, 3f);
			Glow(new Color(0.9f, 0.2f, 0.1f)).Box(f.P(w * 0.2f, 8.2f, 4.1f), new Vector3(1f, 0.6f, 0.3f), f.Q, 1f);
		}

		private static void CarInNiche(Frame f, float w)
		{
			Rails(f.P(0f, 0.2f, 0f), f.Z, 16f);
			MineCar(f.P(0f, 0f, 9f), f.Q, true);
			Heap(f.P(-w * 0.32f, 0f, 4f), 3.2f, 2f, 7);
		}

		/// <summary>Props and timber stacked for the face, and rock dust in bags.</summary>
		private static void Supplies(Frame f, float w)
		{
			for (int k = 0; k < 6; k++)
			{
				Timber.Box(f.P(w * 0.08f, 0.7f + (k / 3) * 1.4f, 6f + (k % 3) * 1.5f), new Vector3(w * 0.7f, 1.3f, 1.3f), f.Q, 4f);
			}
			Batch bags = Get("rock dust bags", Tex.Enamel, new Color(0.92f, 0.92f, 0.9f));
			for (int k = 0; k < 12; k++)
			{
				bags.Box(f.P(-w * 0.33f + (k % 2) * 2.6f, 0.65f + (k / 4) * 1.3f, 3f + (k / 2 % 2) * 1.8f), new Vector3(2.4f, 1.2f, 1.6f), f.R((k * 13) % 9 - 4), 2f);
			}
		}

		/// <summary>Boards nailed across a crosscut to bad roof, with the sign.</summary>
		private static void Barricade(Frame f, float w, float h)
		{
			Batch boards = Get("barricades", Tex.Timber, new Color(0.85f, 0.8f, 0.72f), true);
			float a = Mathf.Atan2(h - 2f, w) * Mathf.Rad2Deg;
			boards.Box(f.P(0f, h * 0.5f, 1f), new Vector3(Mathf.Sqrt(w * w + h * h) - 2f, 1.2f, 0.5f), f.Q * Quaternion.Euler(0f, 0f, a), 3f);
			boards.Box(f.P(0f, h * 0.5f, 1f), new Vector3(Mathf.Sqrt(w * w + h * h) - 2f, 1.2f, 0.5f), f.Q * Quaternion.Euler(0f, 0f, -a), 3f);
			boards.Box(f.P(0f, h * 0.35f, 0.7f), new Vector3(w - 0.5f, 1.2f, 0.5f), f.Q, 3f);
			Placard(f.P(0f, h * 0.62f, 0.3f), -f.Z, 5f, 1.8f, "DANGER", new Color(0.75f, 0.08f, 0.05f), Tex.Enamel, new Color(0.95f, 0.93f, 0.88f));
		}

		/// <summary>A pump at the sump in the back of the crosscut, its discharge line out along the floor.</summary>
		private static void Pump(Frame f, float w, float d)
		{
			Get("sump water", Tex.Plain, new Color(0.03f, 0.05f, 0.05f)).Quad(f.P(-w * 0.45f, 0.1f, d - 8f), f.P(-w * 0.45f, 0.1f, d - 0.5f), f.P(w * 0.45f, 0.1f, d - 0.5f), f.P(w * 0.45f, 0.1f, d - 8f), 8f);
			Batch green = Machine("green", new Color(0.24f, 0.36f, 0.26f));
			green.Box(f.P(0f, 1.6f, d - 11f), new Vector3(4f, 3.2f, 3f), f.Q, 2f);
			green.Cylinder(f.P(-3.5f, 1.6f, d - 11f), f.P(-1.8f, 1.6f, d - 11f), 1.3f, 8, 2f, true);
			Rubber.Cylinder(f.P(0f, 0.5f, d - 12.6f), f.P(0f, 0.5f, 0.5f), 0.4f, 5, 2f, false);
			Steel.Cylinder(f.P(1.8f, 1.2f, d - 11f), f.P(2.2f, 0.4f, d - 6f), 0.35f, 5, 2f, false);
		}

		// ---- machines --------------------------------------------------------------------------------------------------------

		/// <summary>A mine car facing +z of rot: a steel tub on four wheels, heaped with coal when loaded.</summary>
		internal static void MineCar(Vector3 at, Quaternion rot, bool loaded)
		{
			Batch body = Get("mine cars", Tex.Corrugated, new Color(0.55f, 0.38f, 0.28f), true);
			body.Box(at + rot * new Vector3(0f, 4.2f, 0f), new Vector3(6.5f, 4.4f, 9f), rot, 3f);
			body.Box(at + rot * new Vector3(0f, 1.6f, 0f), new Vector3(5.2f, 1.2f, 9.6f), rot, 3f);
			foreach (float x in new[] { -2.6f, 2.6f })
			{
				foreach (float z in new[] { -3.2f, 3.2f })
				{
					DarkSteel.Cylinder(at + rot * new Vector3(x - 0.4f, 1.2f, z), at + rot * new Vector3(x + 0.4f, 1.2f, z), 1.2f, 8, 2f, true);
				}
			}
			if (loaded)
			{
				CoalLoad.Heap(at + rot * new Vector3(0f, 6.4f, 0f), 3.4f, 1.8f, 8, 2, 0f, null, 2f);
			}
		}

		/// <summary>An old car tipped on its side and rusted through, left in the old workings.</summary>
		private static void OldCar(Vector3 at, Quaternion rot)
		{
			Quaternion tipped = rot * Quaternion.Euler(0f, 0f, 95f);
			Rust.Box(at + rot * new Vector3(-1f, 3.3f, 0f), new Vector3(6.5f, 4.4f, 9f), tipped, 3f);
			DarkSteel.Cylinder(at + rot * new Vector3(2.2f, 0.4f, -3.2f), at + rot * new Vector3(2.2f, 1.2f, -3.2f), 1.2f, 8, 2f, true);
			CoalLoad.Heap(at + rot * new Vector3(-5f, 0f, 1f), 3.5f, 1.5f, 8, 2, 0f, null, 2f);
		}

		/// <summary>A trolley locomotive: a low armoured box with its cab, headlamps, and the pole up to the wire.</summary>
		private static void Locomotive(Vector3 at, Quaternion rot)
		{
			Batch body = Machine("loco", new Color(0.18f, 0.26f, 0.2f));
			body.Box(at + rot * new Vector3(0f, 2.6f, 0f), new Vector3(7f, 3.6f, 15f), rot, 3f);
			body.Box(at + rot * new Vector3(0f, 4.9f, -3.5f), new Vector3(6.6f, 1.6f, 5f), rot, 3f);
			DarkSteel.Box(at + rot * new Vector3(0f, 1f, 0f), new Vector3(5.6f, 1.6f, 14f), rot, 3f);
			Glow(new Color(1f, 0.9f, 0.6f)).Box(at + rot * new Vector3(-2f, 3.4f, 7.55f), new Vector3(1.2f, 1.2f, 0.1f), rot, 1f);
			Glow(new Color(1f, 0.9f, 0.6f)).Box(at + rot * new Vector3(2f, 3.4f, 7.55f), new Vector3(1.2f, 1.2f, 0.1f), rot, 1f);
			Steel.Cylinder(at + rot * new Vector3(0f, 5.5f, -1f), at + rot * new Vector3(0f, 13f, -7f), 0.25f, 5, 2f, false);
			Glow(new Color(0.9f, 0.2f, 0.1f)).Box(at + rot * new Vector3(0f, 5.8f, -7.6f), new Vector3(1f, 0.8f, 0.1f), rot, 1f);
		}

		/// <summary>A man trip: the low open car with benches that carries the shift in and out.</summary>
		private static void ManTrip(Vector3 at, Quaternion rot)
		{
			Batch body = Machine("mantrip", new Color(0.55f, 0.5f, 0.2f));
			body.Box(at + rot * new Vector3(0f, 1.6f, 0f), new Vector3(6f, 1.2f, 18f), rot, 3f);
			for (int k = 0; k < 4; k++)
			{
				Timber.Box(at + rot * new Vector3(0f, 2.8f, -6f + k * 4f), new Vector3(5.4f, 0.6f, 1.6f), rot, 2f);
				body.Box(at + rot * new Vector3(0f, 3.6f, -5.2f + k * 4f), new Vector3(5.4f, 1.8f, 0.3f), rot, 2f);
			}
			body.Box(at + rot * new Vector3(0f, 4.2f, 0f), new Vector3(6.2f, 0.3f, 18f), rot, 3f);
		}

		/// <summary>A timber crib: layers of short timbers laid crosswise, holding up bad roof.</summary>
		private static void Crib(Vector3 at, Quaternion rot, int layers)
		{
			for (int k = 0; k < layers; k++)
			{
				Quaternion q = rot * Quaternion.Euler(0f, k % 2 == 0 ? 0f : 90f, 0f);
				Timber.Box(at + Vector3.up * (0.6f + k * 1.2f) + q * new Vector3(-2f, 0f, 0f), new Vector3(1.2f, 1.2f, 6f), q, 3f);
				Timber.Box(at + Vector3.up * (0.6f + k * 1.2f) + q * new Vector3(2f, 0f, 0f), new Vector3(1.2f, 1.2f, 6f), q, 3f);
			}
		}

		private static void Heap(Vector3 at, float r, float h, int n)
		{
			Get("roof falls", Tex.Rock, new Color(0.6f, 0.58f, 0.55f)).Heap(at, r, h, n, 3, r * 0.25f, new System.Random((int)(at.x * 7 + at.z * 13)), 4f);
		}

		/// <summary>Two rails from a point along a direction, for a niche's track.</summary>
		private static void Rails(Vector3 from, Vector3 dir, float length)
		{
			Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
			Quaternion q = Quaternion.LookRotation(dir, Vector3.up);
			foreach (float s in new[] { -3.2f, 3.2f })
			{
				Steel.Box(from + side * s + dir * (length * 0.5f) + Vector3.up * 0.1f, new Vector3(0.5f, 0.4f, length), q, 4f);
			}
			for (float t = 1f; t < length; t += 3f)
			{
				Timber.Box(from + dir * t, new Vector3(9f, 0.2f, 1.1f), q, 4f);
			}
		}

		// ---- along the tunnels -----------------------------------------------------------------------------------------------

		/// <summary>The water line low on the left rib, the power cable sagging between hooks on the right (both just behind the smooth wall the cars touch), and on the haulage the trolley wire.</summary>
		private static void Utilities(Road r, bool trolley)
		{
			Batch pipe = Get("water line", Tex.Steel, new Color(0.25f, 0.27f, 0.3f));
			Batch cable = Get("power cable", Tex.Belt, Color.white);
			Batch wire = Get("trolley wire", Tex.Steel, new Color(0.7f, 0.5f, 0.3f));
			int start = r == Route.Runin ? RouteData.RuninMouth + 2 : 0;
			int last = r.Closed ? r.Count : r.Count - 2;
			for (int i = start; i < last; i++)
			{
				Vector3 a = r.At(i), c = r.At(i + 1);
				Vector3 ra = r.Right(i), rc = r.Right(i + 1);
				float ha = r.HalfAt(i), hc = r.HalfAt(i + 1);
				float ta = r.CeilAt(i) - (Mine.Height - Mine.WallTop), tc = r.CeilAt(i + 1) - (Mine.Height - Mine.WallTop);
				if (SolidWall(r, i, 1f) && SolidWall(r, i + 1, 1f))
				{
					// the cable hangs from a hook at every point and sags between
					Vector3 h0 = a + ra * (ha + 0.25f) + Vector3.up * (ta - 2.5f), h1 = c + rc * (hc + 0.25f) + Vector3.up * (tc - 2.5f);
					Vector3 mid = (h0 + h1) * 0.5f + Vector3.down * 0.9f;
					cable.Cylinder(h0, mid, 0.3f, 4, 2f, false);
					cable.Cylinder(mid, h1, 0.3f, 4, 2f, false);
					DarkSteel.Box(h0 + Vector3.up * 0.4f, new Vector3(0.3f, 1f, 0.3f), Quaternion.identity, 1f);
				}
				if (trolley && !Route.InsideOther(r, a, -2f))
				{
					Vector3 w0 = a + ra * (ha * 0.35f) + Vector3.up * (r.CeilAt(i) - 3.2f), w1 = c + rc * (hc * 0.35f) + Vector3.up * (r.CeilAt(i + 1) - 3.2f);
					wire.Cylinder(w0, w1, 0.12f, 4, 2f, false);
					if (i % 3 == 0)
					{
						DarkSteel.Box(w0 + Vector3.up * 1.4f, new Vector3(0.2f, 2.8f, 0.2f), Quaternion.identity, 1f);
						Get("insulators", Tex.Plain, new Color(0.75f, 0.65f, 0.4f)).Box(w0 + Vector3.up * 0.5f, new Vector3(0.6f, 0.6f, 0.6f), Quaternion.identity, 1f);
					}
				}
			}
		}

		/// <summary>
		/// Reflective chevron boards on the outside wall of every bend, pointing the way it goes: they glow a little, so a bend reads in
		/// the dark before the headlights reach it.
		/// </summary>
		private static void Chevrons(Road r)
		{
			Batch board = Get("chevrons", Tex.Chevron, Color.white);
			board.Emission = new Color(0.45f, 0.35f, 0.05f);
			int start = r == Route.Runin ? RouteData.RuninMouth + 2 : 2;
			int last = r.Closed ? r.Count : r.Count - 3;
			for (int i = start; i < last; i += 2)
			{
				Vector3 f0 = r.Forward(i - 2), f1 = r.Forward(i + 2);
				float turn = Vector3.Angle(f0, f1);
				if (turn < 12f)
				{
					continue;
				}
				bool left = Vector3.Cross(f0, f1).y < 0f;
				float outer = left ? 1f : -1f;
				if (!SolidWall(r, i, outer) || r.HalfAt(i) > r.Half + 1f)
				{
					continue;
				}
				Vector3 f = r.Forward(i);
				Vector3 normal = -r.Right(i) * outer;
				Vector3 at = r.At(i) + r.Right(i) * outer * (r.HalfAt(i) - 0.15f) + Vector3.up * 7.5f;
				Board(board, at, normal, f, 6f, 4.5f);
			}
		}

		/// <summary>A flat board on a wall facing normal, whose texture's +u points along f (the way the road goes).</summary>
		private static void Board(Batch b, Vector3 c, Vector3 normal, Vector3 f, float w, float h)
		{
			Vector3 up = Vector3.up;
			Vector3 p0 = c - f * (w * 0.5f) - up * (h * 0.5f), p1 = c - f * (w * 0.5f) + up * (h * 0.5f);
			Vector3 p2 = c + f * (w * 0.5f) + up * (h * 0.5f), p3 = c + f * (w * 0.5f) - up * (h * 0.5f);
			// the texture's arrow points +u: u grows toward +f
			Vector2 u0 = new Vector2(0f, 0f), u1 = new Vector2(0f, 1f), u2 = new Vector2(1f, 1f), u3 = new Vector2(1f, 0f);
			// (p0, p1, p2) faces Cross(up, f), the road's right: flip the order for a board that faces left
			if (Vector3.Dot(Vector3.Cross(up, f), normal) > 0f)
			{
				b.Quad(p0, p1, p2, p3, u0, u1, u2, u3);
			}
			else
			{
				b.Quad(p3, p2, p1, p0, u3, u2, u1, u0);
			}
		}

		// ---- signs -----------------------------------------------------------------------------------------------------------

		/// <summary>A painted board with lettering, facing normal.</summary>
		private static void Placard(Vector3 c, Vector3 normal, float w, float h, string text, Color ink, Texture2D tex, Color paint)
		{
			Get("sign boards " + ColorUtility.ToHtmlStringRGB(paint), tex, paint).Box(c, new Vector3(w, h, 0.3f), Quaternion.LookRotation(-normal, Vector3.up), 4f);
			SceneBuilder.Labels.Add(new Label { Pos = c + normal * 0.2f, Facing = normal, Text = text, Color = ink, Size = new Vector2(w * 0.9f, h * 0.85f) });
		}

		private static readonly Color Cream = new Color(0.93f, 0.9f, 0.82f), Ink = new Color(0.08f, 0.08f, 0.08f), Red = new Color(0.78f, 0.08f, 0.05f);

		/// <summary>A sign hung from the roof across part of the road at point i, facing the cars coming: offset is across the road (-1..1).</summary>
		private static void Hanging(Road r, int i, float offset, float w, string text, Color ink, Color paint)
		{
			Vector3 f = r.Forward(i);
			float ta = r.CeilAt(i) - (Mine.Height - Mine.WallTop);
			Vector3 c = r.At(i) + r.Right(i) * (offset * r.HalfAt(i)) + Vector3.up * (ta - 1.6f);
			Placard(c, -f, w, 3.2f, text, ink, Tex.Enamel, paint);
			DarkSteel.Box(c + Vector3.up * 2.6f + r.Right(i) * (w * 0.4f), new Vector3(0.2f, 2f, 0.2f), Quaternion.identity, 1f);
			DarkSteel.Box(c + Vector3.up * 2.6f - r.Right(i) * (w * 0.4f), new Vector3(0.2f, 2f, 0.2f), Quaternion.identity, 1f);
		}

		/// <summary>A sign on the wall at point i (side -1 left, 1 right), if the wall is there.</summary>
		private static void OnWall(Road r, int i, float side, float height, float w, float h, string text, Color ink, Color paint)
		{
			if (!SolidWall(r, i, side))
			{
				return;
			}
			Vector3 normal = -r.Right(i) * side;
			Vector3 c = r.At(i) + r.Right(i) * side * (r.HalfAt(i) - 0.25f) + Vector3.up * height;
			Placard(c, normal, w, h, text, ink, Tex.Enamel, paint);
		}

		/// <summary>
		/// Only the signs a real mine has where it would have them, at a believable size (0.4.3 had road-wide boards naming every
		/// stretch, arrows, and safety notes every 23 points: the owner found it "overly labeled"): the plane's warning, the section
		/// board at the plane's foot, the danger board at the old workings, the chute's door notice, no smoking on the slope.
		/// </summary>
		private static void Signs()
		{
			Road m = Route.Main;
			int plane = Route.Mark("Plane head"), foot = Route.Mark("Plane foot");
			Hanging(m, plane - 6, 0f, 12f, "ENGINE PLANE\nWATCH FOR RUNAWAYS", Red, new Color(0.95f, 0.85f, 0.15f));
			OnWall(m, foot + 4, -1f, 9f, 8f, 1.8f, "1 SOUTH", Ink, Cream);
			OnWall(Route.Seam, 3, -1f, 8f, 7f, 2.6f, "DANGER\nOLD WORKS", Red, Cream);
			OnWall(Route.Chute, 6, 1f, 8f, 8f, 1.8f, "KEEP DOORS SHUT", Red, Cream);
			OnWall(Route.Runin, RouteData.RuninMouth + 80, -1f, 8f, 6f, 1.8f, "NO SMOKING", Red, Cream);
		}

		// ---- the pit bottom --------------------------------------------------------------------------------------------------

		/// <summary>
		/// The pit bottom chamber round the start line: the shaft with its cage behind a gate on the left, the loading tracks with a
		/// locomotive and its trip along the left wall, a supply yard and the safety board on the right.
		/// </summary>
		private static void PitBottom()
		{
			Road m = Route.Main;
			// the cage in the shaft, set in the left wall at points 3 to 7
			int i = 5;
			Vector3 f = m.Forward(i), right = m.Right(i);
			float h = m.HalfAt(i);
			Vector3 wall = m.At(i) - right * (h - 0.5f);
			Quaternion q = Quaternion.LookRotation(f, Vector3.up);
			Batch shaft = Get("shaft", Tex.Concrete, new Color(0.7f, 0.7f, 0.68f), true);
			shaft.Wall = true;
			// the shaft's collar: a concrete surround standing out from the wall, with a gate across the cage
			shaft.Box(wall + right * 1.2f + Vector3.up * 13f - f * 9f, new Vector3(2.4f, 26f, 3f), q, 6f);
			shaft.Box(wall + right * 1.2f + Vector3.up * 13f + f * 9f, new Vector3(2.4f, 26f, 3f), q, 6f);
			shaft.Box(wall + right * 1.2f + Vector3.up * 25f, new Vector3(2.4f, 3f, 21f), q, 6f);
			// the cage behind it, lit inside
			Batch cage = Get("cage", Tex.Corrugated, new Color(0.45f, 0.45f, 0.47f));
			cage.Box(wall - right * 0.2f + Vector3.up * 11f, new Vector3(0.4f, 22f, 15f), q, 4f);
			Glow(new Color(1f, 0.85f, 0.5f)).Box(wall + right * 0.05f + Vector3.up * 18f, new Vector3(0.1f, 2f, 8f), q, 1f);
			// the gate: steel bars
			for (int k = -6; k <= 6; k += 2)
			{
				DarkSteel.Box(wall + right * 2.6f + f * k + Vector3.up * 6f, new Vector3(0.3f, 12f, 0.3f), q, 1f);
			}
			DarkSteel.Box(wall + right * 2.6f + Vector3.up * 11.8f, new Vector3(0.4f, 0.4f, 14f), q, 1f);
			DarkSteel.Box(wall + right * 2.6f + Vector3.up * 0.6f, new Vector3(0.4f, 0.4f, 14f), q, 1f);
			// the signal bell and its lamp
			Machine("grey", new Color(0.5f, 0.52f, 0.53f)).Box(wall + right * 1.0f + f * 12f + Vector3.up * 8f, new Vector3(1.6f, 3f, 2f), q, 2f);
			Glow(new Color(0.2f, 0.9f, 0.3f)).Box(wall + right * 1.85f + f * 12f + Vector3.up * 9f, new Vector3(0.1f, 0.6f, 0.6f), q, 1f);
			Placard(wall + right * 2.6f + Vector3.up * 28.6f, right, 12f, 2.2f, "EUREKA No. 9 SHAFT", Ink, Tex.Enamel, Cream);

			// the loading tracks along the left wall, with a locomotive and a trip of loaded cars
			int t0 = 9;
			Vector3 dir = m.Forward(t0 + 4);
			Vector3 track = m.At(t0) - m.Right(t0) * (m.HalfAt(t0) - 7f);
			Rails(track, dir, 70f);
			Quaternion tq = Quaternion.LookRotation(dir, Vector3.up);
			Locomotive(track + dir * 58f, tq);
			for (int k = 0; k < 4; k++)
			{
				MineCar(track + dir * (44f - k * 11f), tq, true);
			}

			// the right side: the supply yard behind the line, the safety board, and a bench
			int y0 = -9;
			Vector3 yard = m.At(y0) + m.Right(y0) * (m.HalfAt(y0) - 6f);
			Quaternion yq = Quaternion.LookRotation(m.Forward(y0), Vector3.up);
			for (int k = 0; k < 8; k++)
			{
				Timber.Box(yard + yq * new Vector3(-1.5f + (k % 2) * 1.6f, 0.7f + (k / 2) * 1.4f, 0f), new Vector3(1.3f, 1.3f, 24f), yq, 4f);
			}
			Crib(yard + yq * new Vector3(-2f, 0f, 18f), yq, 7);
			Batch bags = Get("rock dust bags", Tex.Enamel, new Color(0.92f, 0.92f, 0.9f));
			for (int k = 0; k < 18; k++)
			{
				bags.Box(yard + yq * new Vector3(-1f + (k % 3) * 2.5f, 0.65f + (k / 6) * 1.3f, -16f + (k / 3 % 2) * 1.8f), new Vector3(2.4f, 1.2f, 1.6f), yq, 2f);
			}
			int b0 = 2;
			Vector3 board = m.At(b0) + m.Right(b0) * (m.HalfAt(b0) - 0.3f) + Vector3.up * 14f;
			Placard(board, -m.Right(b0), 12f, 4f, "EUREKA MINE\nSAFETY FIRST", Red, Tex.Enamel, Cream);
			Placard(board + Vector3.down * 3.6f, -m.Right(b0), 12f, 2.4f, "DAYS WITHOUT\nAN ACCIDENT  0", Ink, Tex.Enamel, Cream);
			Vector3 bench = m.At(12) + m.Right(12) * (m.HalfAt(12) - 3f);
			Quaternion bq = Quaternion.LookRotation(m.Forward(12), Vector3.up);
			Timber.Box(bench + Vector3.up * 2f, new Vector3(2.4f, 0.5f, 14f), bq, 3f);
			Timber.Box(bench + Vector3.up * 1f + bq * new Vector3(0f, 0f, -6f), new Vector3(2f, 2f, 0.6f), bq, 3f);
			Timber.Box(bench + Vector3.up * 1f + bq * new Vector3(0f, 0f, 6f), new Vector3(2f, 2f, 0.6f), bq, 3f);
			Batch pails = Machine("pails", new Color(0.6f, 0.6f, 0.62f));
			for (int k = 0; k < 3; k++)
			{
				pails.Cylinder(bench + Vector3.up * 2.25f + bq * new Vector3(0f, 0f, -4f + k * 3f), bench + Vector3.up * 3.6f + bq * new Vector3(0f, 0f, -4f + k * 3f), 0.6f, 8, 1f, true);
			}
		}
	}
}
