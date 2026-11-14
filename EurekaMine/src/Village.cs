using System.Collections.Generic;
using UnityEngine;

namespace EurekaMine
{
	/// <summary>
	/// The patch town beside the pithead: two rows of company double houses (two homes under one side-gabled roof, the commonest company
	/// house in the anthracite patches) backing onto an alley with their coal sheds, and a white clapboard church with a steeple at the
	/// end of the street. Built as real buildings, not boxes: a stone foundation, clapboard walls whose laps run level, trimmed windows
	/// and doors set in them, a roof slab with eaves, barge boards and a soffit, one brick chimney on the ridge, and a front porch on posts.
	/// After the Open World mod's houses (htubdawg/HalfGripOpenWorld, <c>Buildings.cs</c>): storeys and bays, roofs as thick slabs.
	/// </summary>
	internal static class Village
	{
		/// <summary>A building's frame: x along its front (to the right seen from the street), y up, z away from the street.</summary>
		private struct Lot
		{
			internal Vector3 O, R, F;
			internal Quaternion Rot;

			internal Lot(Vector3 origin, float yaw)
			{
				O = origin;
				Rot = Quaternion.Euler(0f, yaw, 0f);
				R = Rot * Vector3.right;
				F = Rot * Vector3.forward;
			}

			internal Vector3 P(float x, float y, float z)
			{
				return O + R * x + Vector3.up * y + F * z;
			}
		}

		private static Dictionary<string, Batch> B;

		private static Batch Get(string name, Texture2D tex, Color tint, bool collide = false)
		{
			if (!B.TryGetValue(name, out Batch batch))
			{
				batch = new Batch(name, tex, tint) { Collide = collide };
				B[name] = batch;
			}
			return batch;
		}

		private static Batch Trim { get { return Get("house trim", Tex.Plain, new Color(0.88f, 0.86f, 0.80f)); } }
		private static Batch Glass { get { Batch g = Get("house glass", Tex.Plain, new Color(0.07f, 0.08f, 0.10f)); g.Smooth = 0.8f; return g; } }
		private static Batch Roofing { get { return Get("house roofs", Tex.Shingle, Color.white); } }
		private static Batch Footing { get { Batch f = Get("house foundations", Tex.Concrete, new Color(0.62f, 0.58f, 0.54f), true); f.Wall = true; return f; } }
		private static Batch Chimney { get { return Get("chimneys", Tex.Brick, Color.white); } }
		private static Batch Porch { get { return Get("porches", Tex.Boards, new Color(1.9f, 1.85f, 1.8f)); } }

		/// <summary>Faded company paint: white, barn red, mustard, grey-blue, green.</summary>
		private static readonly Color[] Paint =
		{
			new Color(0.84f, 0.82f, 0.76f), new Color(0.62f, 0.30f, 0.25f), new Color(0.74f, 0.66f, 0.42f), new Color(0.52f, 0.58f, 0.62f), new Color(0.50f, 0.58f, 0.46f)
		};
		private static readonly Color[] DoorPaint = { new Color(0.30f, 0.16f, 0.12f), new Color(0.16f, 0.22f, 0.16f), new Color(0.14f, 0.16f, 0.24f) };

		internal static void Build(Dictionary<string, Batch> b, System.Random rng)
		{
			B = b;
			for (int row = 0; row < 2; row++)
			{
				float z = 300f + row * 90f;
				// the first row fronts the road (-z), the second the church's street (+z); their backs share the alley between
				float yaw = row == 0 ? 0f : 180f;
				for (int k = 0; k < 9; k++)
				{
					float x = -2240f + k * 64f + (row == 1 ? 22f : 0f);
					DoubleHouse(new Lot(new Vector3(x, 0f, z), yaw), (k * 3 + row) % Paint.Length, rng);
				}
			}
			Church(new Vector3(-2050f, 0f, 560f));
		}

		// ---- the pieces ------------------------------------------------------------------------------------------------------------

		/// <summary>A wall from bottom corner p to q (outside on the right walking p to q), y0 to y1 high.</summary>
		private static void Wall(Batch b, Vector3 p, Vector3 q, float y0, float y1, float tile)
		{
			Vector3 lo = Vector3.up * y0, hi = Vector3.up * y1;
			b.Face(p + lo, p + hi, q + hi, q + lo, tile);
		}

		/// <summary>A sash window: a trim surround and sill standing proud of the wall, dark glass, a meeting rail and a muntin.</summary>
		private static void Window(Vector3 c, Vector3 n, float w, float h)
		{
			Quaternion rot = Quaternion.LookRotation(-n, Vector3.up);
			Batch trim = Trim;
			trim.Box(c + n * 0.2f, new Vector3(w + 1.6f, h + 1.6f, 0.4f), rot, 4f);
			Glass.Box(c + n * 0.3f, new Vector3(w, h, 0.4f), rot, 4f);
			trim.Box(c + n * 0.55f, new Vector3(w, 0.35f, 0.3f), rot, 4f);
			trim.Box(c + n * 0.55f, new Vector3(0.3f, h, 0.3f), rot, 4f);
			trim.Box(c + n * 0.5f + Vector3.down * (h * 0.5f + 0.9f), new Vector3(w + 2.4f, 0.5f, 1.2f), rot, 4f);
			trim.Box(c + n * 0.4f + Vector3.up * (h * 0.5f + 1.0f), new Vector3(w + 2.2f, 0.7f, 0.8f), rot, 4f);
		}

		/// <summary>A panelled door in a trim surround, a little glass light at the top.</summary>
		private static void Door(Vector3 bottom, Vector3 n, float w, float h, Color paint)
		{
			Quaternion rot = Quaternion.LookRotation(-n, Vector3.up);
			Trim.Box(bottom + Vector3.up * (h * 0.5f + 0.3f) + n * 0.2f, new Vector3(w + 1.4f, h + 0.9f, 0.4f), rot, 4f);
			Batch door = Get("doors " + ColorUtility.ToHtmlStringRGB(paint), Tex.Boards, paint * 3.2f);
			door.Box(bottom + Vector3.up * (h * 0.5f) + n * 0.35f, new Vector3(w, h, 0.4f), rot, 4f);
			Glass.Box(bottom + Vector3.up * (h - 1.6f) + n * 0.5f, new Vector3(w * 0.6f, 1.6f, 0.3f), rot, 4f);
		}

		/// <summary>
		/// A side-gabled roof over walls w wide, d deep, h high in the lot: two slabs <paramref name="thick"/> thick from the eaves
		/// (<paramref name="eave"/> past the front and back walls) to the ridge <paramref name="rise"/> over the wall tops, running
		/// <paramref name="end"/> past the gables; their tops, undersides, fascias and barge boards; and the gable ends in the wall's batch.
		/// </summary>
		private static void Roof(Lot l, Batch walls, float w, float d, float h, float rise, float eave, float end, float thick)
		{
			float hw = w * 0.5f + end, hd = d * 0.5f;
			float drop = eave * rise / hd;
			Vector3 t = Vector3.up * thick;
			// the slabs' undersides: at the wall line they sit on the wall tops
			Vector3 fL = l.P(-hw, h - drop, -hd - eave), fR = l.P(hw, h - drop, -hd - eave);
			Vector3 bL = l.P(-hw, h - drop, hd + eave), bR = l.P(hw, h - drop, hd + eave);
			Vector3 rL = l.P(-hw, h + rise, 0f), rR = l.P(hw, h + rise, 0f);
			Batch roof = Roofing, trim = Trim;
			roof.Face(fL + t, rL + t, rR + t, fR + t, 6f);
			roof.Face(bR + t, rR + t, rL + t, bL + t, 6f);
			trim.Face(fR, rR, rL, fL, 6f);
			trim.Face(bL, rL, rR, bR, 6f);
			trim.Face(fL, fL + t, fR + t, fR, 4f);
			trim.Face(bR, bR + t, bL + t, bL, 4f);
			trim.Face(fR, fR + t, rR + t, rR, 4f);
			trim.Face(rR, rR + t, bR + t, bR, 4f);
			trim.Face(bL, bL + t, rL + t, rL, 4f);
			trim.Face(rL, rL + t, fL + t, fL, 4f);
			// the gables, in the walls' clapboard
			float gx = w * 0.5f;
			walls.FlatTri(l.P(gx, h, -hd), l.P(gx, h + rise, 0f), l.P(gx, h, hd), 6f);
			walls.FlatTri(l.P(-gx, h, hd), l.P(-gx, h + rise, 0f), l.P(-gx, h, -hd), 6f);
		}

		/// <summary>Four walls round the lot's footprint, y0 to y1.</summary>
		private static void Walls(Lot l, Batch b, float w, float d, float y0, float y1, float tile)
		{
			float hw = w * 0.5f, hd = d * 0.5f;
			Vector3[] c = { l.P(-hw, 0f, -hd), l.P(hw, 0f, -hd), l.P(hw, 0f, hd), l.P(-hw, 0f, hd) };
			for (int k = 0; k < 4; k++)
			{
				Wall(b, c[k], c[(k + 1) % 4], y0, y1, tile);
			}
		}

		/// <summary>
		/// A company double house: two storeys of clapboard on a stone foundation, two homes side by side under one side-gabled roof
		/// with one shared chimney on the ridge, their front doors together in the middle of a porch that runs the whole front, and a coal
		/// shed each behind.
		/// </summary>
		private static void DoubleHouse(Lot l, int colour, System.Random rng)
		{
			const float w = 34f, d = 24f, floor = 1.8f, storey = 9.6f, h = floor + storey * 2f, hw = w * 0.5f, hd = d * 0.5f;
			Batch walls = Get("houses " + colour, Tex.Clapboard, Paint[colour], true);
			walls.Wall = true;
			Footing.Box(l.P(0f, floor * 0.5f - 0.4f, 0f), new Vector3(w + 0.8f, floor + 0.8f, d + 0.8f), l.Rot, 6f);
			Walls(l, walls, w, d, floor, h, 8f);
			// corner boards
			foreach (float sx in new[] { -1f, 1f })
			{
				foreach (float sz in new[] { -1f, 1f })
				{
					Trim.Box(l.P(sx * (hw - 0.2f), (floor + h) * 0.5f, sz * (hd - 0.2f)), new Vector3(1.2f, h - floor, 1.2f), l.Rot, 4f);
				}
			}
			Roof(l, walls, w, d, h, 9f, 2.2f, 1.4f, 0.9f);
			// one chimney through the ridge, between the two homes
			Chimney.Box(l.P(0f, h + 8.5f, 1.2f), new Vector3(3.4f, 9f, 3.4f), l.Rot, 3f);
			Chimney.Box(l.P(0f, h + 13.3f, 1.2f), new Vector3(4.2f, 0.8f, 4.2f), l.Rot, 3f);

			Color door = DoorPaint[rng.Next(DoorPaint.Length)];
			float g = floor + 5.2f, u = floor + storey + 5f;
			Vector3 front = -l.F, back = l.F;
			// front: a window and a door each side of the middle below, four windows above
			foreach (float x in new[] { -12.5f, 12.5f })
			{
				Window(l.P(x, g, -hd), front, 3.6f, 5.6f);
			}
			foreach (float x in new[] { -4.6f, 4.6f })
			{
				Door(l.P(x, floor, -hd), front, 3.6f, 7.2f, door);
			}
			foreach (float x in new[] { -12.5f, -5f, 5f, 12.5f })
			{
				Window(l.P(x, u, -hd), front, 3.6f, 5.6f);
				Window(l.P(x, u, hd), back, 3.6f, 5.6f);
			}
			foreach (float x in new[] { -12.5f, -4.5f, 4.5f, 12.5f })
			{
				if (x == -4.5f || x == 4.5f)
				{
					Door(l.P(x, floor, hd), back, 3.4f, 7f, door);
				}
				else
				{
					Window(l.P(x, g, hd), back, 3.6f, 5.6f);
				}
			}
			// the gable ends: a window each floor, and one small one in the attic
			foreach (float sx in new[] { -1f, 1f })
			{
				Vector3 n = l.R * sx;
				Window(l.P(sx * hw, g, -4f), n, 3.4f, 5.4f);
				Window(l.P(sx * hw, u, -4f), n, 3.4f, 5.4f);
				Window(l.P(sx * hw, h + 3.2f, 0f), n, 2.4f, 3f);
			}

			// the porch: a deck on the foundation's height, posts, a rail with a gap at each pair of steps, a shed roof
			Batch porch = Porch;
			float pd = 8f, pz = -hd - pd * 0.5f;
			porch.Box(l.P(0f, floor - 0.3f, pz), new Vector3(w - 1f, 0.6f, pd), l.Rot, 6f);
			Footing.Box(l.P(0f, (floor - 0.6f) * 0.5f, pz), new Vector3(w - 2f, floor - 0.6f, pd - 1f), l.Rot, 6f);
			foreach (float x in new[] { -2.8f, 2.8f })
			{
				porch.Box(l.P(x, floor * 0.3f, -hd - pd - 1f), new Vector3(4.4f, floor * 0.6f, 2f), l.Rot, 6f);
			}
			float top = floor + 8.5f;
			foreach (float x in new[] { -16f, -8.5f, -0.8f, 0.8f, 8.5f, 16f })
			{
				Trim.Box(l.P(x, (floor + top) * 0.5f, -hd - pd + 0.7f), new Vector3(0.8f, top - floor, 0.8f), l.Rot, 4f);
			}
			foreach (float[] span in new[] { new[] { -16f, -5f }, new[] { 5f, 16f } })
			{
				float mid = (span[0] + span[1]) * 0.5f, len = span[1] - span[0];
				Trim.Box(l.P(mid, floor + 3.4f, -hd - pd + 0.7f), new Vector3(len, 0.5f, 0.5f), l.Rot, 4f);
				Trim.Box(l.P(mid, floor + 0.9f, -hd - pd + 0.7f), new Vector3(len, 0.4f, 0.4f), l.Rot, 4f);
			}
			// the rail between the two homes' halves of the porch
			Trim.Box(l.P(0f, floor + 3.4f, pz), new Vector3(0.5f, 0.5f, pd - 1f), l.Rot, 4f);
			Trim.Box(l.P(0f, top - 0.5f, -hd - pd + 0.7f), new Vector3(w - 1f, 1f, 0.6f), l.Rot, 4f);
			Quaternion pitch = l.Rot * Quaternion.Euler(-9f, 0f, 0f);
			Roofing.Box(l.P(0f, top + 1.1f, pz - 0.6f), new Vector3(w + 0.6f, 0.6f, pd + 2.4f), pitch, 6f);

			// a coal shed behind each home, across the yard
			foreach (float sx in new[] { -1f, 1f })
			{
				Lot s = new Lot(l.P(sx * (8f + (float)rng.NextDouble() * 4f), 0f, hd + 18f), 0f);
				s.Rot = l.Rot;
				s.R = l.R;
				s.F = l.F;
				Batch boards = Get("sheds", Tex.Boards, new Color(1.5f, 1.45f, 1.4f), true);
				boards.Wall = true;
				Walls(s, boards, 8f, 7f, 0f, 7f, 6f);
				Door(s.P(0f, 0f, -3.5f), -l.F, 3f, 5.6f, door);
				Roofing.Box(s.P(0f, 7.6f, 0f), new Vector3(9.6f, 0.5f, 9f), l.Rot * Quaternion.Euler(8f, 0f, 0f), 6f);
			}
		}

		/// <summary>A four-sided pyramid (a spire or a steeple's roof) on a square of half side s centred at c, rising h.</summary>
		private static void Pyramid(Batch b, Lot l, Vector3 c, float s, float h)
		{
			Vector3[] k = { c + (-l.R - l.F) * s, c + (l.R - l.F) * s, c + (l.R + l.F) * s, c + (-l.R + l.F) * s };
			Vector3 apex = c + Vector3.up * h;
			for (int i = 0; i < 4; i++)
			{
				b.FlatTri(k[i], apex, k[(i + 1) % 4], 6f);
			}
		}

		/// <summary>A pointed (lancet) window: dark glass and a trim surround, the point a third of its width high.</summary>
		private static void Lancet(Vector3 bottom, Vector3 n, float w, float h)
		{
			// to the right as seen from outside, so the faces are wound to face out
			Vector3 across = Vector3.Cross(n, Vector3.up).normalized;
			float point = w * 0.75f;
			foreach (bool glass in new[] { false, true })
			{
				Batch b = glass ? Glass : Trim;
				float m = glass ? 0f : 0.8f;
				Vector3 o = n * (glass ? 0.35f : 0.2f);
				Vector3 bl = bottom - across * (w * 0.5f + m) + o + Vector3.down * m, br = bottom + across * (w * 0.5f + m) + o + Vector3.down * m;
				Vector3 tl = bottom - across * (w * 0.5f + m) + o + Vector3.up * h, tr = bottom + across * (w * 0.5f + m) + o + Vector3.up * h;
				b.Face(bl, tl, tr, br, 4f);
				b.FlatTri(tl, bottom + o + Vector3.up * (h + point + m), tr, 4f);
			}
			Trim.Box(bottom + n * 0.6f + Vector3.up * (h * 0.45f), new Vector3(0.3f, h * 0.9f, 0.3f), Quaternion.LookRotation(-n, Vector3.up), 4f);
			Trim.Box(bottom + n * 0.5f + Vector3.down * 1.2f, new Vector3(0.6f, 0.6f, 0.6f) + Abs(across) * (w + 2f), Quaternion.identity, 4f);
		}

		private static Vector3 Abs(Vector3 v)
		{
			return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
		}

		/// <summary>
		/// The patch's church, as the Methodist and Lutheran congregations of the coal towns built them: a white clapboard nave under a
		/// steep roof, tall pointed windows down its sides, and a square bell tower over the front door with a louvred belfry and a spire.
		/// Its front faces the street (-z), at the end of the houses.
		/// </summary>
		private static void Church(Vector3 c)
		{
			Batch walls = Get("church", Tex.Clapboard, new Color(0.93f, 0.92f, 0.88f), true);
			walls.Wall = true;
			const float length = 56f, width = 30f, floor = 2.4f, h = 26f;
			// the nave's frame runs along its length, so its gables are its front (x +) and back (x -); the street is to -z
			Lot nave = new Lot(c, 90f);
			Footing.Box(nave.P(0f, floor * 0.5f - 0.4f, 0f), new Vector3(length + 1f, floor + 0.8f, width + 1f), nave.Rot, 6f);
			Walls(nave, walls, length, width, floor, h, 8f);
			Roof(nave, walls, length, width, h, 17f, 2.2f, 1.6f, 1.0f);
			foreach (float side in new[] { -1f, 1f })
			{
				Vector3 n = nave.F * side;
				for (int k = 0; k < 4; k++)
				{
					float x = -18f + k * 11f;
					Lancet(nave.P(x, floor + 5f, side * width * 0.5f), n, 4.2f, 11f);
				}
			}
			// the back gable: one tall window
			Lancet(nave.P(-length * 0.5f, floor + 5f, 0f), -nave.R, 5f, 12f);

			// the bell tower over the front door, standing half out of the front gable
			Vector3 towerAt = nave.P(length * 0.5f + 3f, 0f, 0f);
			Lot tower = new Lot(towerAt, 0f);
			const float ts = 7f, body = 44f, belfry = 11f;
			Footing.Box(towerAt + Vector3.up * (floor * 0.5f - 0.4f), new Vector3(ts * 2f + 1f, floor + 0.8f, ts * 2f + 1f), Quaternion.identity, 6f);
			Walls(tower, walls, ts * 2f, ts * 2f, floor, body, 8f);
			Vector3 street = Vector3.back;
			Vector3 doorAt = towerAt + street * ts;
			foreach (float x in new[] { -1.9f, 1.9f })
			{
				Door(doorAt + Vector3.right * x + Vector3.up * floor, street, 3.6f, 9.5f, new Color(0.34f, 0.17f, 0.10f));
			}
			Trim.Box(doorAt + Vector3.up * (floor + 11.5f) + street * 0.5f, new Vector3(10f, 1f, 1.4f), Quaternion.identity, 4f);
			Lancet(doorAt + Vector3.up * (floor + 17f), street, 3.6f, 8f);
			foreach (Vector3 n in new[] { Vector3.left, Vector3.right })
			{
				Lancet(towerAt + n * ts + Vector3.up * (floor + 17f), n, 3f, 7f);
			}
			// the belfry: a cornice, louvred openings on all four sides, a cornice, the spire with a cross
			Batch trim = Trim;
			trim.Box(towerAt + Vector3.up * (body + 0.6f), new Vector3(ts * 2f + 2f, 1.2f, ts * 2f + 2f), Quaternion.identity, 4f);
			float b0 = body + 1.2f, b1 = b0 + belfry;
			Walls(new Lot(towerAt, 0f), walls, ts * 2f - 1f, ts * 2f - 1f, b0, b1, 8f);
			foreach (Vector3 n in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
			{
				Vector3 across = Vector3.Cross(Vector3.up, n);
				Vector3 at = towerAt + n * (ts - 0.5f) + Vector3.up * (b0 + 1.5f);
				Quaternion rot = Quaternion.LookRotation(-n, Vector3.up);
				Glass.Box(at + Vector3.up * 4f + n * 0.2f, new Vector3(6f, 8f, 0.4f), rot, 4f);
				for (int s = 0; s < 6; s++)
				{
					trim.Box(at + Vector3.up * (1f + s * 1.25f) + n * 0.5f, new Vector3(6f, 0.35f, 0.6f), Quaternion.LookRotation(-n, Vector3.up) * Quaternion.Euler(30f, 0f, 0f), 4f);
				}
				trim.Box(at + Vector3.up * 4f + n * 0.4f + across * 3.4f, new Vector3(0.8f, 8.6f, 0.8f), rot, 4f);
				trim.Box(at + Vector3.up * 4f + n * 0.4f - across * 3.4f, new Vector3(0.8f, 8.6f, 0.8f), rot, 4f);
			}
			trim.Box(towerAt + Vector3.up * (b1 + 0.6f), new Vector3(ts * 2f + 1.6f, 1.2f, ts * 2f + 1.6f), Quaternion.identity, 4f);
			Pyramid(Roofing, tower, towerAt + Vector3.up * (b1 + 1.2f), ts + 0.2f, 34f);
			Batch cross = Get("church cross", Tex.Plain, new Color(0.15f, 0.15f, 0.16f));
			float apex = b1 + 1.2f + 34f;
			cross.Box(towerAt + Vector3.up * (apex + 3.5f), new Vector3(0.7f, 7f, 0.7f), Quaternion.identity, 2f);
			cross.Box(towerAt + Vector3.up * (apex + 5f), new Vector3(4f, 0.7f, 0.7f), Quaternion.identity, 2f);
			// steps up to the doors
			for (int s = 0; s < 3; s++)
			{
				Footing.Box(doorAt + street * (1.5f + (2 - s) * 1.6f) + Vector3.up * (s * 0.8f + 0.4f), new Vector3(11f, 0.8f, 1.6f), Quaternion.identity, 4f);
			}
		}
	}
}
