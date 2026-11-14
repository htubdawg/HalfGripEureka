using System.Collections.Generic;
using UnityEngine;

namespace EurekaMine
{
	/// <summary>
	/// The mine's bulbs never move, so their light is worked out once, at build time, and painted onto every surface near them: each
	/// surface gets a thin twin whose vertex colours are the light falling there, drawn added over it with the surface's own texture (so
	/// the result is the rock's colour times the light, as a real light gives). Every bulb lights its stretch at any distance, nothing
	/// switches on or off as the camera moves, and it costs no real lights (0.4.1 moved 8 real lights to the nearest bulbs: everything
	/// past them was black). The cars' headlights stay real.
	/// </summary>
	internal static class Glow
	{
		/// <summary>How far a bulb's light reaches.</summary>
		internal const float Range = 46f;

		/// <summary>Brightness at the bulb, and the distance at which it has fallen to half.</summary>
		internal static float Strength = 7.5f;
		internal static float Near = 8f;

		/// <summary>A little light on faces turned away from the bulb too (light bouncing off wet rock), so the rough faces don't go stark.</summary>
		private const float Wrap = 0.2f;

		/// <summary>
		/// Triangles longer than this are cut smaller (a floor tile is one quad across the road) wherever the light changes across them by
		/// more than <see cref="Smooth"/>, so a pool is round, not smeared; past <see cref="Longest"/> they are always cut.
		/// </summary>
		private const float Edge = 3f;
		private const float Smooth = 0.04f;
		private const float Longest = 12f;

		/// <summary>The painted twin sits this far in front of its surface, so the two never fight over depth.</summary>
		private const float Lift = 0.04f;

		/// <summary>Brightest a painted vertex may be (right under a bulb).</summary>
		private const float Cap = 4f;

		/// <summary>
		/// The bulbs' colour as the renderer works with it: the real lights' (1, 0.8, 0.55) is a screen colour, which Unity turns into
		/// (1, 0.60, 0.26) before lighting with it; vertex colours are used as they are, so the painted light takes the converted value
		/// (0.4.2 used the screen colour and came out nearly white).
		/// </summary>
		private static readonly Color Warm = new Color(1f, 0.604f, 0.262f);

		private const float Slack = 6f;

		private const float LampCell = Range;
		private const float AirCell = 16f;
		private static readonly Dictionary<long, List<Vector3>> lamps = new Dictionary<long, List<Vector3>>();
		private static readonly Dictionary<long, List<Vector3>> air = new Dictionary<long, List<Vector3>>();
		private static readonly Dictionary<long, List<Vector2>> airSize = new Dictionary<long, List<Vector2>>();
		private static readonly Dictionary<long, List<Mine.Niche>> passages = new Dictionary<long, List<Mine.Niche>>();

		private static long Key(float x, float z, float cell)
		{
			return ((long)Mathf.FloorToInt(x / cell) << 32) ^ (uint)Mathf.FloorToInt(z / cell);
		}

		private static void Index()
		{
			lamps.Clear();
			air.Clear();
			airSize.Clear();
			passages.Clear();
			foreach (Mine.Niche n in Mine.Niches)
			{
				if (n.Width <= 0f)
				{
					continue;
				}
				Vector3 mid = n.Mouth + n.Out * (n.Depth * 0.5f);
				float reach = n.Depth * 0.5f + n.Width * 0.5f + 6f;
				for (int cx = Mathf.FloorToInt((mid.x - reach) / AirCell); cx <= Mathf.FloorToInt((mid.x + reach) / AirCell); cx++)
				{
					for (int cz = Mathf.FloorToInt((mid.z - reach) / AirCell); cz <= Mathf.FloorToInt((mid.z + reach) / AirCell); cz++)
					{
						long k = ((long)cx << 32) ^ (uint)cz;
						if (!passages.TryGetValue(k, out List<Mine.Niche> list))
						{
							passages[k] = list = new List<Mine.Niche>();
						}
						list.Add(n);
					}
				}
			}
			foreach (Vector3 p in Mine.Lamps)
			{
				long k = Key(p.x, p.z, LampCell);
				if (!lamps.TryGetValue(k, out List<Vector3> list))
				{
					lamps[k] = list = new List<Vector3>();
				}
				list.Add(p + Vector3.down * 0.6f);
			}
			foreach (Road r in Route.All)
			{
				for (int i = 0; i < r.P.Length; i++)
				{
					long k = Key(r.P[i].x, r.P[i].z, AirCell);
					if (!air.TryGetValue(k, out List<Vector3> list))
					{
						air[k] = list = new List<Vector3>();
						airSize[k] = new List<Vector2>();
					}
					list.Add(r.P[i]);
					airSize[k].Add(new Vector2(r.HalfW[i], r.Ceil[i]));
				}
			}
		}

		/// <summary>
		/// Whether a point is in the open air of some road: inside its width plus <see cref="Slack"/> (the rough rock stands well back from
		/// the guard in places, and the side passages open off it), between its floor and a little over its roof. Rock is everything else.
		/// </summary>
		private static bool InAir(Vector3 q)
		{
			int cx = Mathf.FloorToInt(q.x / AirCell), cz = Mathf.FloorToInt(q.z / AirCell);
			// a side passage: a box from inside the road out to its far face, as wide and high as its opening and a little more
			if (passages.TryGetValue(((long)cx << 32) ^ (uint)cz, out List<Mine.Niche> near))
			{
				foreach (Mine.Niche n in near)
				{
					Vector3 d = q - n.Mouth;
					float o = Vector3.Dot(d, n.Out), a = Vector3.Dot(d, n.Along);
					if (o > -4f && o < n.Depth + 4f && Mathf.Abs(a) < n.Width * 0.5f + 3f && d.y > -3f && d.y < n.High + 4f)
					{
						return true;
					}
				}
			}
			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dz = -1; dz <= 1; dz++)
				{
					long k = ((long)(cx + dx) << 32) ^ (uint)(cz + dz);
					if (!air.TryGetValue(k, out List<Vector3> list))
					{
						continue;
					}
					List<Vector2> size = airSize[k];
					for (int i = 0; i < list.Count; i++)
					{
						Vector3 p = list[i];
						float h = size[i].x + Slack;
						float y = q.y - p.y;
						if (y > -3f && y < size[i].y + 2f && (p.x - q.x) * (p.x - q.x) + (p.z - q.z) * (p.z - q.z) < h * h)
						{
							return true;
						}
					}
				}
			}
			return false;
		}

		/// <summary>A bulb lights a point only if the way between them runs through open air (not through the rock to the next level or the next gangway).</summary>
		private static bool Sees(Vector3 lamp, Vector3 p)
		{
			return InAir(Vector3.Lerp(lamp, p, 0.3f)) && InAir(Vector3.Lerp(lamp, p, 0.5f)) && InAir(Vector3.Lerp(lamp, p, 0.65f));
		}

		/// <summary>The light falling on a point with normal n: every bulb in reach that sees it, falling off with distance and with the angle.</summary>
		internal static float Light(Vector3 p, Vector3 n, bool twoSided)
		{
			float sum = 0f;
			int cx = Mathf.FloorToInt(p.x / LampCell), cz = Mathf.FloorToInt(p.z / LampCell);
			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dz = -1; dz <= 1; dz++)
				{
					long k = ((long)(cx + dx) << 32) ^ (uint)(cz + dz);
					if (!lamps.TryGetValue(k, out List<Vector3> list))
					{
						continue;
					}
					foreach (Vector3 lamp in list)
					{
						Vector3 d = lamp - p;
						float dist = d.magnitude;
						if (dist >= Range || dist < 0.01f)
						{
							continue;
						}
						float facing = Vector3.Dot(n, d / dist);
						if (twoSided)
						{
							facing = Mathf.Abs(facing);
						}
						facing = (facing + Wrap) / (1f + Wrap);
						if (facing <= 0f)
						{
							continue;
						}
						// falls to nothing at Range smoothly, as URP's point lights do
						float r = dist / Range;
						float w = 1f - r * r * r * r;
						float window = w * w;
						float q = dist / Near;
						float light = Strength * facing * window / (1f + q * q);
						if (light < 0.003f || !Sees(lamp, p))
						{
							continue;
						}
						sum += light;
					}
				}
			}
			return Mathf.Min(sum, Cap);
		}

		/// <summary>
		/// Paints every lit batch: a "glow" batch per texture (the tint goes into the vertex colours) holding the lit triangles, cut small
		/// enough for the pools to be round. Unlit triangles are left out. Returns the number of painted triangles.
		/// </summary>
		internal static int Build(Dictionary<string, Batch> b)
		{
			Index();
			int count = 0;
			List<Batch> sources = new List<Batch>(b.Values);
			foreach (Batch src in sources)
			{
				if (src.Invisible || src.Glow || src.Emission.maxColorComponent > 0.01f || src.Count == 0)
				{
					continue;
				}
				string name = "glow " + (src.Tex != null ? src.Tex.name : "plain") + (src.TwoSided ? " two" : "");
				if (!b.TryGetValue(name, out Batch glow))
				{
					glow = new Batch(name, src.Tex, Color.white) { Glow = true, TwoSided = src.TwoSided, Shadows = false };
					b[name] = glow;
				}
				IReadOnlyList<Vector3> v = src.Vertices;
				IReadOnlyList<Vector2> uv = src.Uvs;
				IReadOnlyList<int> t = src.Indices;
				// vertex normals as the renderer works them out: each vertex shared by triangles gets the sum of their normals
				Vector3[] normal = new Vector3[v.Count];
				for (int k = 0; k < t.Count; k += 3)
				{
					Vector3 f = Vector3.Cross(v[t[k + 1]] - v[t[k]], v[t[k + 2]] - v[t[k]]);
					normal[t[k]] += f;
					normal[t[k + 1]] += f;
					normal[t[k + 2]] += f;
				}
				float[] lit = new float[v.Count];
				for (int i = 0; i < lit.Length; i++)
				{
					lit[i] = -1f;
					normal[i] = normal[i].normalized;
				}
				for (int k = 0; k < t.Count; k += 3)
				{
					int ia = t[k], ib = t[k + 1], ic = t[k + 2];
					Vector3 mid = (v[ia] + v[ib] + v[ic]) / 3f;
					if (!NearLamp(mid))
					{
						continue;
					}
					for (int j = 0; j < 3; j++)
					{
						int i = t[k + j];
						if (lit[i] < 0f)
						{
							lit[i] = Light(v[i], normal[i], src.TwoSided);
						}
					}
					Corner a = new Corner(v[ia], uv[ia], normal[ia], lit[ia]);
					Corner c1 = new Corner(v[ib], uv[ib], normal[ib], lit[ib]);
					Corner c2 = new Corner(v[ic], uv[ic], normal[ic], lit[ic]);
					count += Paint(glow, src.Tint, src.TwoSided, a, c1, c2, 0);
				}
			}
			return count;
		}

		private struct Corner
		{
			public Vector3 P;
			public Vector2 Uv;
			public Vector3 N;
			public float L;

			public Corner(Vector3 p, Vector2 uv, Vector3 n, float l)
			{
				P = p;
				Uv = uv;
				N = n;
				L = l;
			}
		}

		private static bool NearLamp(Vector3 p)
		{
			int cx = Mathf.FloorToInt(p.x / LampCell), cz = Mathf.FloorToInt(p.z / LampCell);
			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dz = -1; dz <= 1; dz++)
				{
					if (lamps.TryGetValue(((long)(cx + dx) << 32) ^ (uint)(cz + dz), out List<Vector3> list))
					{
						foreach (Vector3 lamp in list)
						{
							if ((lamp - p).sqrMagnitude < (Range + 6f) * (Range + 6f))
							{
								return true;
							}
						}
					}
				}
			}
			return false;
		}

		/// <summary>Adds one triangle to the glow batch, halving its longest edge first while that is longer than <see cref="Edge"/> and the light on it matters.</summary>
		private static int Paint(Batch glow, Color tint, bool twoSided, Corner a, Corner b, Corner c, int depth)
		{
			float most = Mathf.Max(a.L, Mathf.Max(b.L, c.L));
			if (most < 0.004f)
			{
				return 0;
			}
			float ab = (a.P - b.P).sqrMagnitude, bc = (b.P - c.P).sqrMagnitude, ca = (c.P - a.P).sqrMagnitude;
			float longest = Mathf.Max(ab, Mathf.Max(bc, ca));
			if (longest > Edge * Edge && depth < 8)
			{
				// split the longest edge at its middle, light the new corner and paint both halves
				if (bc >= ab && bc >= ca)
				{
					Corner t = a;
					a = b;
					b = c;
					c = t;
				}
				else if (ca >= ab && ca >= bc)
				{
					Corner t = a;
					a = c;
					c = b;
					b = t;
				}
				// now ab is the longest, and a, b, c keep their winding
				Vector3 n = (a.N + b.N).normalized;
				Vector3 p = (a.P + b.P) * 0.5f;
				Corner m = new Corner(p, (a.Uv + b.Uv) * 0.5f, n, Light(p, n, twoSided));
				// where the light runs straight across (far from any bulb, or an even stretch) the halves would add nothing
				if (longest < Longest * Longest && Mathf.Abs(m.L - (a.L + b.L) * 0.5f) < Smooth && Mathf.Abs(c.L - (a.L + b.L) * 0.5f) < Smooth * 2f)
				{
					glow.ColoredTri(a.P + a.N * Lift, b.P + b.N * Lift, c.P + c.N * Lift, a.Uv, b.Uv, c.Uv, Paint(tint, a.L), Paint(tint, b.L), Paint(tint, c.L));
					return 1;
				}
				return Paint(glow, tint, twoSided, a, m, c, depth + 1) + Paint(glow, tint, twoSided, m, b, c, depth + 1);
			}
			glow.ColoredTri(a.P + a.N * Lift, b.P + b.N * Lift, c.P + c.N * Lift, a.Uv, b.Uv, c.Uv, Paint(tint, a.L), Paint(tint, b.L), Paint(tint, c.L));
			return 1;
		}

		private static Color Paint(Color tint, float light)
		{
			return new Color(tint.r * Warm.r * light, tint.g * Warm.g * light, tint.b * Warm.b * light, 1f);
		}
	}
}
