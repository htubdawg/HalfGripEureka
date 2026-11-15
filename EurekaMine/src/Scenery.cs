using System.Collections.Generic;
using UnityEngine;

namespace EurekaMine
{
	/// <summary>
	/// What lies round the pithead yard: a post-and-wire fence on three sides (the hill with the drift closes the fourth) with an invisible
	/// wall just inside it, so the race keeps to the yard and the road into the mine; shrubs along the fence; pines in the country beyond and
	/// on the hill over the drift; two rings of tree lines (the Forest map's own backdrop sprites) on the skyline; and the patch town on a
	/// hill to the north-west, seen across the trees and never reached. The ridges past all that are painted in the sky (<see cref="SkyPaint"/>).
	///
	/// The pines and the tree lines are the game's (picked up from the main menu by <see cref="Backdrop"/>); this only decides where they go.
	/// If the game's are not there (and in the preview), pines and tree lines are made here instead, so the yard is never bare.
	/// </summary>
	internal static class Scenery
	{
		/// <summary>The fence: west, south and north sides; the east is the hill's face.</summary>
		internal const float FenceW = -1780f, FenceS = -318f, FenceN = 168f;
		internal static float FenceE { get { return Pithead.HillX0; } }

		/// <summary>The invisible wall stands this far inside the fence posts, so nothing drawn stands in the drivable space.</summary>
		private const float WallIn = 2f;

		/// <summary>The middle of the yard, which the tree lines face.</summary>
		internal static readonly Vector3 Centre = new Vector3(-1460f, 0f, -80f);

		/// <summary>Where the patch town stands: moved from beside the yard out onto a hill (its top this high) across the trees.</summary>
		internal static readonly Vector3 VillageShift = new Vector3(-300f, 55f, 450f);

		/// <summary>The town's hill: its flat top (x0, x1, z0, z1) and how far its sides run out to the plain.</summary>
		private const float MesaX0 = -2610f, MesaX1 = -1940f, MesaZ0 = 700f, MesaZ1 = 1080f, MesaRun = 170f;

		internal struct Tree
		{
			internal Vector3 Pos;
			internal float Yaw, Height;
		}

		internal struct Line
		{
			/// <summary>The foot of the line's middle, and the way it faces (toward the yard).</summary>
			internal Vector3 Pos, Facing;
			internal float Width, Height;
			internal bool Far;
		}

		internal static readonly List<Tree> Trees = new List<Tree>();
		internal static readonly List<Line> Lines = new List<Line>();

		/// <summary>Set by <see cref="Backdrop"/> before the build when the game's pines and tree lines were found; else they are made here.</summary>
		internal static bool GamePines, GameLines;

		internal static void Build(Dictionary<string, Batch> b)
		{
			System.Random rng = new System.Random(4163);
			Trees.Clear();
			Lines.Clear();
			Fence(b);
			Mesa(b);
			Shrubs(b, rng);
			PlaceTrees(rng);
			PlaceLines(rng);
			if (!GamePines)
			{
				DrawPines(b, rng);
			}
			if (!GameLines)
			{
				DrawLines(b, rng);
			}
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

		/// <summary>The ground's height at a point outside the yard: the plain, the hill over the drift, or the town's hill.</summary>
		internal static float GroundY(float x, float z)
		{
			if (z > Pithead.HillZ0 && z < Pithead.HillZ1)
			{
				if (x >= Pithead.HillX0 && x <= Pithead.HillX1)
				{
					return Pithead.HillTop;
				}
				if (x > Pithead.HillX1 && x < Pithead.HillBackFoot)
				{
					return Mathf.Lerp(Pithead.HillTop, 0f, (x - Pithead.HillX1) / (Pithead.HillBackFoot - Pithead.HillX1));
				}
			}
			float dx = Mathf.Max(MesaX0 - x, 0f, x - MesaX1), dz = Mathf.Max(MesaZ0 - z, 0f, z - MesaZ1);
			float d = Mathf.Max(dx, dz);
			return d < MesaRun ? VillageShift.y * (1f - d / MesaRun) : 0f;
		}

		/// <summary>How far a point is outside the fence (negative inside).</summary>
		private static float OutsideFence(float x, float z)
		{
			if (x > FenceE)
			{
				return Mathf.Max(FenceS - z, z - FenceN, 0f) + (z > FenceS && z < FenceN ? -1f : 0f);
			}
			float dx = Mathf.Max(FenceW - x, 0f), dz = Mathf.Max(FenceS - z, z - FenceN, 0f);
			if (dx > 0f || dz > 0f)
			{
				return Mathf.Sqrt(dx * dx + dz * dz);
			}
			return -Mathf.Min(x - FenceW, z - FenceS, FenceN - z);
		}

		// ---- the fence and the wall ----------------------------------------------------------------------------------------------------

		/// <summary>
		/// Timber posts every 10 units with three strands of wire, from the hill's north corner round the west side to its south corner. The
		/// invisible wall (built both ways, frictionless, like the mine's guards) stands 2 inside it and 80 high, so no car leaves the yard.
		/// </summary>
		private static void Fence(Dictionary<string, Batch> b)
		{
			Batch post = Get(b, "fence posts", Tex.Timber, new Color(0.62f, 0.56f, 0.5f));
			Batch wire = Get(b, "fence wire", Tex.Steel, new Color(0.35f, 0.34f, 0.33f));
			wire.Shadows = false;
			Batch wall = Get(b, "boundary", Tex.Plain, Color.white);
			wall.Invisible = true;
			wall.Collide = true;
			wall.Wall = true;
			wall.Slippery = true;
			Vector3[] corners =
			{
				new Vector3(FenceE, 0f, FenceN), new Vector3(FenceW, 0f, FenceN), new Vector3(FenceW, 0f, FenceS), new Vector3(FenceE, 0f, FenceS)
			};
			Vector3[] inner =
			{
				new Vector3(FenceE + 1f, 0f, FenceN - WallIn), new Vector3(FenceW + WallIn, 0f, FenceN - WallIn), new Vector3(FenceW + WallIn, 0f, FenceS + WallIn), new Vector3(FenceE + 1f, 0f, FenceS + WallIn)
			};
			for (int s = 0; s < 3; s++)
			{
				Vector3 a = inner[s], c = inner[s + 1];
				wall.QuadBoth(a, a + Vector3.up * 80f, c + Vector3.up * 80f, c, 16f);
				Vector3 p0 = corners[s], p1 = corners[s + 1];
				float len = Vector3.Distance(p0, p1);
				int n = Mathf.CeilToInt(len / 10f);
				Vector3 last = p0;
				for (int k = 0; k <= n; k++)
				{
					Vector3 at = Vector3.Lerp(p0, p1, k / (float)n);
					// a slight lean and a little variety in height, as old fence posts have
					float lean = (Mine.Hash(k, s, 7, 31) - 0.5f) * 0.6f;
					float h = 7f + Mine.Hash(k, s, 13, 32) * 0.8f;
					post.Cylinder(at + Vector3.down * 0.5f, at + new Vector3(lean, h, lean * 0.5f), 0.45f, 6, 3f, true);
					if (k > 0)
					{
						foreach (float y in new[] { 2.4f, 4.4f, 6.4f })
						{
							Vector3 w0 = last + Vector3.up * y, w1 = at + Vector3.up * y;
							Vector3 mid = (w0 + w1) * 0.5f + Vector3.down * 0.15f;
							wire.Cylinder(w0, mid, 0.08f, 3, 2f, false);
							wire.Cylinder(mid, w1, 0.08f, 3, 2f, false);
						}
					}
					last = at;
				}
			}
		}

		/// <summary>Low shrubs and scrub along the outside of the fence, in clumps, so the fence stands in growth rather than on a lawn.</summary>
		private static void Shrubs(Dictionary<string, Batch> b, System.Random rng)
		{
			Batch shrub = Get(b, "shrubs", Tex.Grass, new Color(0.42f, 0.5f, 0.36f));
			Batch dark = Get(b, "shrubs dark", Tex.Grass, new Color(0.3f, 0.38f, 0.3f));
			Vector3[] corners = { new Vector3(FenceE, 0f, FenceN), new Vector3(FenceW, 0f, FenceN), new Vector3(FenceW, 0f, FenceS), new Vector3(FenceE, 0f, FenceS) };
			for (int s = 0; s < 3; s++)
			{
				Vector3 p0 = corners[s], p1 = corners[s + 1];
				Vector3 along = (p1 - p0).normalized;
				Vector3 outward = Vector3.Cross(Vector3.up, along);
				float len = Vector3.Distance(p0, p1);
				for (float d = 6f; d < len - 6f; d += 9f + (float)rng.NextDouble() * 16f)
				{
					Vector3 at = p0 + along * d + outward * (5f + (float)rng.NextDouble() * 8f);
					// the siding's rails run out through the west fence: keep them clear
					if (Mathf.Abs(at.z + 210f) < 12f && at.x < FenceW + 20f)
					{
						continue;
					}
					// and the boiler house stands just outside the north fence
					if (at.z > FenceN && at.x > -1600f && at.x < -1520f)
					{
						continue;
					}
					float r = 3f + (float)rng.NextDouble() * 4f;
					(rng.NextDouble() < 0.5 ? shrub : dark).Heap(at, r, r * (0.7f + (float)rng.NextDouble() * 0.4f), 7, 2, r * 0.25f, rng, 4f);
				}
			}
		}

		// ---- the town's hill -----------------------------------------------------------------------------------------------------------

		/// <summary>A broad grassy hill with a flat top for the patch town, its sides sloping out to the plain.</summary>
		private static void Mesa(Dictionary<string, Batch> b)
		{
			Batch grass = Get(b, "town hill", Tex.Grass, new Color(0.78f, 0.8f, 0.72f));
			grass.Shadows = false;
			float y = VillageShift.y, R = MesaRun;
			Vector3 a = new Vector3(MesaX0, y, MesaZ1), c = new Vector3(MesaX1, y, MesaZ1), d = new Vector3(MesaX1, y, MesaZ0), e = new Vector3(MesaX0, y, MesaZ0);
			grass.Quad(a, c, d, e, 16f);
			Vector3 A = new Vector3(MesaX0 - R, 0f, MesaZ1 + R), C = new Vector3(MesaX1 + R, 0f, MesaZ1 + R), D = new Vector3(MesaX1 + R, 0f, MesaZ0 - R), E = new Vector3(MesaX0 - R, 0f, MesaZ0 - R);
			Vector3 an = new Vector3(MesaX0, 0f, MesaZ1 + R), cn = new Vector3(MesaX1, 0f, MesaZ1 + R);
			Vector3 ce = new Vector3(MesaX1 + R, 0f, MesaZ1), de = new Vector3(MesaX1 + R, 0f, MesaZ0);
			Vector3 ds = new Vector3(MesaX1, 0f, MesaZ0 - R), es = new Vector3(MesaX0, 0f, MesaZ0 - R);
			Vector3 ew = new Vector3(MesaX0 - R, 0f, MesaZ0), aw = new Vector3(MesaX0 - R, 0f, MesaZ1);
			// the four sides (each seen from outside), and the corners between them
			grass.Quad(an, cn, c, a, 16f);
			grass.Quad(c, ce, de, d, 16f);
			grass.Quad(e, d, ds, es, 16f);
			grass.Quad(aw, a, e, ew, 16f);
			grass.Tri(A, an, a, 16f);
			grass.Tri(A, a, aw, 16f);
			grass.Tri(C, c, cn, 16f);
			grass.Tri(C, ce, c, 16f);
			grass.Tri(D, d, de, 16f);
			grass.Tri(D, ds, d, 16f);
			grass.Tri(E, ew, e, 16f);
			grass.Tri(E, e, es, 16f);
		}

		// ---- pines ---------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Pines in the country round the fence, thickening with distance (40 to 700 out), on the hill over the drift (not round the headframe
		/// or under the conveyor) and round the town; none on the culm banks, the siding, the boiler house or the town's street.
		/// </summary>
		private static void PlaceTrees(System.Random rng)
		{
			int tries = 0;
			while (Trees.Count < 480 && tries++ < 40000)
			{
				float x = Centre.x + ((float)rng.NextDouble() * 2f - 1f) * 1250f;
				float z = Centre.z + ((float)rng.NextDouble() * 2f - 1f) * 1250f;
				float out_ = OutsideFence(x, z);
				bool onHill = x > Pithead.HillX0 + 25f && x < Pithead.HillX1 - 15f && z > Pithead.HillZ0 + 15f && z < Pithead.HillZ1 - 15f;
				if (onHill)
				{
					// round the headframe and under the conveyor gallery from the hilltop to the breaker
					if (Vector2.Distance(new Vector2(x, z), new Vector2(-930f, -40f)) < 45f || (x < -1020f && z > 20f && z < 110f))
					{
						continue;
					}
					// keep the face's edge over the drift mouth bare, so the mouth reads from the grid
					if (x < Pithead.HillX0 + 60f && Mathf.Abs(z + 80f) < 60f)
					{
						continue;
					}
					if (rng.NextDouble() > 0.35)
					{
						continue;
					}
				}
				else
				{
					if (out_ < 40f || x > Pithead.HillX0 - 10f && x < Pithead.HillBackFoot + 40f && z > Pithead.HillZ0 - 20f && z < Pithead.HillZ1 + 20f)
					{
						continue;
					}
					// in groves with open ground between, thin near the fence, close-set further out
					float grove = Mathf.Clamp01((Mine.Noise(new Vector3(x, 0f, z), 220f, 41) - 0.35f) * 2.6f);
					if (rng.NextDouble() > Mathf.Clamp01(0.1f + out_ / 450f) * grove)
					{
						continue;
					}
				}
				if (Blocked(x, z))
				{
					continue;
				}
				float h = 34f + (float)rng.NextDouble() * 26f;
				Trees.Add(new Tree { Pos = new Vector3(x, GroundY(x, z) - 0.5f, z), Yaw = (float)rng.NextDouble() * 360f, Height = h });
			}
		}

		/// <summary>Places nothing grows: the culm banks, the siding beyond the fence, the boiler house, the town's street and its view.</summary>
		private static bool Blocked(float x, float z)
		{
			Vector2 p = new Vector2(x, z);
			if (Vector2.Distance(p, new Vector2(-1420f, -560f)) < 265f || Vector2.Distance(p, new Vector2(-1820f, -470f)) < 175f || Vector2.Distance(p, new Vector2(-1080f, -560f)) < 135f)
			{
				return true;
			}
			if (x < FenceW && Mathf.Abs(z + 210f) < 18f && x > -1920f)
			{
				return true;
			}
			if (Vector2.Distance(p, new Vector2(-1570f, 210f)) < 70f)
			{
				return true;
			}
			// the town: its houses and street on the hill's top
			if (x > MesaX0 + 20f && x < MesaX1 - 20f && z > MesaZ0 + 15f && z < MesaZ1 - 15f)
			{
				return true;
			}
			// a clear view from the yard to the town: few trees in the band between them
			Vector2 from = new Vector2(Centre.x, FenceN), to = new Vector2((MesaX0 + MesaX1) * 0.5f, MesaZ0);
			Vector2 dir = (to - from).normalized;
			float t = Vector2.Dot(p - from, dir);
			if (t > 0f && t < (to - from).magnitude)
			{
				float side = Mathf.Abs(dir.x * (p.y - from.y) - dir.y * (p.x - from.x));
				if (side < 160f + t * 0.25f && Mine.Hash((int)x, 0, (int)z, 33) < 0.8f)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Pines made here when the game's are missing: a trunk and three stacked cones of needles.</summary>
		private static void DrawPines(Dictionary<string, Batch> b, System.Random rng)
		{
			Batch trunk = Get(b, "pine trunks", Tex.Timber, new Color(0.45f, 0.36f, 0.3f));
			Batch needles = Get(b, "pine needles", Tex.Grass, new Color(0.34f, 0.44f, 0.34f));
			foreach (Tree t in Trees)
			{
				float h = t.Height, r = h * 0.2f;
				trunk.Cylinder(t.Pos, t.Pos + Vector3.up * h * 0.35f, h * 0.025f, 5, 4f, false);
				for (int k = 0; k < 3; k++)
				{
					float y0 = h * (0.2f + k * 0.22f), y1 = Mathf.Min(h, y0 + h * 0.42f);
					Cone(needles, t.Pos + Vector3.up * y0, y1 - y0, r * (1f - k * 0.25f), 7, t.Yaw + k * 20f);
				}
			}
		}

		private static void Cone(Batch b, Vector3 foot, float h, float r, int n, float yaw)
		{
			Vector3 apex = foot + Vector3.up * h;
			for (int k = 0; k < n; k++)
			{
				float a0 = (yaw + k * 360f / n) * Mathf.Deg2Rad, a1 = (yaw + (k + 1) * 360f / n) * Mathf.Deg2Rad;
				Vector3 p0 = foot + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r, p1 = foot + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r;
				b.Tri(p1, p0, apex, 6f);
				b.Tri(p0, p1, foot, 6f);
			}
		}

		// ---- tree lines ----------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Two rings of tree lines round the yard, facing it: a near ring 760 out (40 high) and a far one 1360 out (76 high), about 3 degrees up from the yard, so the painted ridges show over them, each piece a
		/// quarter wider than the gap to the next so the ring has no gaps. None of the near ring in front of the town; the far one stands behind it.
		/// </summary>
		private static void PlaceLines(System.Random rng)
		{
			Ring(rng, 760f, 40f, 26, false);
			Ring(rng, 1360f, 76f, 30, true);
		}

		private static void Ring(System.Random rng, float radius, float height, int count, bool far)
		{
			float town = Mathf.Atan2((MesaZ0 + MesaZ1) * 0.5f - Centre.z, (MesaX0 + MesaX1) * 0.5f - Centre.x);
			float chord = 2f * radius * Mathf.Sin(Mathf.PI / count);
			for (int k = 0; k < count; k++)
			{
				float a = (k + (float)rng.NextDouble() * 0.3f) * Mathf.PI * 2f / count;
				float r = radius * (0.94f + (float)rng.NextDouble() * 0.12f);
				if (!far && Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, town * Mathf.Rad2Deg)) < 22f)
				{
					continue;
				}
				float x = Centre.x + Mathf.Cos(a) * r, z = Centre.z + Mathf.Sin(a) * r;
				Vector3 facing = new Vector3(Centre.x - x, 0f, Centre.z - z).normalized;
				float h = height * (0.85f + (float)rng.NextDouble() * 0.3f);
				Lines.Add(new Line { Pos = new Vector3(x, GroundY(x, z) - 2f, z), Facing = facing, Width = chord * 1.3f, Height = h, Far = far });
			}
		}

		/// <summary>Tree lines made here when the game's sprites are missing: a dark band with a ragged top of spruce points.</summary>
		private static void DrawLines(Dictionary<string, Batch> b, System.Random rng)
		{
			Batch near = Get(b, "tree line", Tex.Grass, new Color(0.26f, 0.34f, 0.28f));
			Batch far = Get(b, "tree line far", Tex.Grass, new Color(0.24f, 0.3f, 0.3f));
			near.Shadows = far.Shadows = false;
			foreach (Line l in Lines)
			{
				Batch batch = l.Far ? far : near;
				Vector3 right = Vector3.Cross(Vector3.up, l.Facing);
				Vector3 left = l.Pos - right * (l.Width * 0.5f);
				int spikes = Mathf.Max(8, Mathf.RoundToInt(l.Width / (l.Height * 0.18f)));
				float step = l.Width / spikes, body = l.Height * 0.5f;
				for (int k = 0; k < spikes; k++)
				{
					Vector3 p0 = left + right * (k * step), p1 = left + right * ((k + 1) * step);
					float top = body + (l.Height - body) * (0.45f + (float)rng.NextDouble() * 0.55f);
					// wound to face the yard (the viewer's right is the line's left)
					batch.Quad(p1, p1 + Vector3.up * body, p0 + Vector3.up * body, p0, 8f);
					batch.Tri(p1 + Vector3.up * body, p0 + right * (step * 0.5f) + Vector3.up * top, p0 + Vector3.up * body, 8f);
				}
			}
		}
	}
}
