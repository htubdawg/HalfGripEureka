using UnityEngine;

namespace EurekaMine
{
	/// <summary>
	/// The map's textures, painted in code at a low resolution and drawn without filtering, so they sit with the game's own pixel look.
	/// Each is made once per session from a fixed seed.
	/// </summary>
	internal static class Tex
	{
		internal static Texture2D Rock, Coal, Timber, Boards, Corrugated, Brick, Grass, Gravel, Steel, Clapboard, Culm, Plain, Shingle;

		private static bool made;

		internal static void Make()
		{
			if (made && Rock != null)
			{
				return;
			}
			made = true;
			Rock = Paint("rock", 64, 1, (x, y, n) =>
			{
				// grey-brown sandstone with a black seam of coal through it
				float seam = Mathf.Abs(Mathf.Sin((y + n.Next(3)) * 0.19f));
				Color c = Color.Lerp(new Color(0.36f, 0.33f, 0.30f), new Color(0.27f, 0.25f, 0.23f), n.Value());
				if (seam < 0.18f)
				{
					c = Color.Lerp(new Color(0.06f, 0.06f, 0.07f), c, seam * 2f);
				}
				return c;
			});
			Coal = Paint("coal", 32, 2, (x, y, n) =>
			{
				float v = n.Value();
				return v > 0.93f ? new Color(0.32f, 0.33f, 0.36f) : Color.Lerp(new Color(0.05f, 0.05f, 0.06f), new Color(0.13f, 0.13f, 0.15f), v);
			});
			Timber = Paint("timber", 32, 3, (x, y, n) =>
			{
				float grain = Mathf.Sin(x * 0.9f + n.Next(2) * 0.6f) * 0.5f + 0.5f;
				return Color.Lerp(new Color(0.36f, 0.24f, 0.13f), new Color(0.50f, 0.35f, 0.20f), grain * 0.6f + n.Value() * 0.4f);
			});
			Boards = Paint("boards", 32, 4, (x, y, n) =>
			{
				// weathered vertical boards of a breaker, gone grey-black with coal dust
				bool gap = x % 8 == 0;
				Color c = Color.Lerp(new Color(0.16f, 0.15f, 0.14f), new Color(0.27f, 0.25f, 0.22f), n.Value());
				return gap ? c * 0.45f : c;
			});
			Corrugated = Paint("corrugated", 32, 5, (x, y, n) =>
			{
				float rib = Mathf.Sin(x * Mathf.PI / 4f) * 0.5f + 0.5f;
				Color metal = Color.Lerp(new Color(0.30f, 0.30f, 0.31f), new Color(0.48f, 0.47f, 0.45f), rib);
				return n.Value() > 0.8f ? Color.Lerp(metal, new Color(0.45f, 0.24f, 0.12f), 0.7f) : metal;
			});
			Brick = Paint("brick", 32, 6, (x, y, n) =>
			{
				int row = y / 4;
				bool mortar = y % 4 == 0 || (x + (row % 2) * 4) % 8 == 0;
				return mortar ? new Color(0.55f, 0.52f, 0.48f) : Color.Lerp(new Color(0.42f, 0.17f, 0.12f), new Color(0.55f, 0.24f, 0.16f), n.Value());
			});
			Grass = Paint("grass", 32, 7, (x, y, n) =>
			{
				float v = n.Value();
				return v > 0.85f ? new Color(0.30f, 0.27f, 0.20f) : Color.Lerp(new Color(0.24f, 0.30f, 0.16f), new Color(0.33f, 0.37f, 0.20f), v);
			});
			Gravel = Paint("gravel", 32, 8, (x, y, n) =>
			{
				float v = n.Value();
				return Color.Lerp(new Color(0.22f, 0.21f, 0.20f), new Color(0.42f, 0.40f, 0.37f), v * v);
			});
			Steel = Paint("steel", 16, 9, (x, y, n) => Color.Lerp(new Color(0.35f, 0.36f, 0.38f), new Color(0.50f, 0.50f, 0.52f), n.Value()));
			Clapboard = Paint("clapboard", 32, 10, (x, y, n) =>
			{
				bool lap = y % 4 == 0;
				Color c = Color.Lerp(new Color(0.80f, 0.78f, 0.72f), new Color(0.90f, 0.88f, 0.82f), n.Value());
				return lap ? c * 0.7f : c;
			});
			Culm = Paint("culm", 32, 11, (x, y, n) =>
			{
				float v = n.Value();
				return v > 0.9f ? new Color(0.35f, 0.30f, 0.27f) : Color.Lerp(new Color(0.08f, 0.08f, 0.09f), new Color(0.18f, 0.17f, 0.17f), v);
			});
			Shingle = Paint("shingle", 32, 12, (x, y, n) =>
			{
				bool edge = y % 4 == 0 || (x + (y / 4 % 2) * 3) % 6 == 0;
				Color c = Color.Lerp(new Color(0.18f, 0.18f, 0.20f), new Color(0.28f, 0.27f, 0.29f), n.Value());
				return edge ? c * 0.6f : c;
			});
			Plain = Paint("plain", 4, 13, (x, y, n) => Color.white);
		}

		private delegate Color Painter(int x, int y, Noise n);

		private static Texture2D Paint(string name, int size, int seed, Painter painter)
		{
			Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
			{
				name = "eureka_" + name,
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Repeat
			};
			Noise n = new Noise(seed);
			Color[] px = new Color[size * size];
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					px[y * size + x] = painter(x, y, n);
				}
			}
			tex.SetPixels(px);
			tex.Apply(false, true);
			return tex;
		}

		/// <summary>A small fixed-seed generator for the painters (xorshift).</summary>
		private sealed class Noise
		{
			private uint s;

			internal Noise(int seed)
			{
				s = (uint)(seed * 2654435761u) | 1u;
			}

			internal float Value()
			{
				s ^= s << 13;
				s ^= s >> 17;
				s ^= s << 5;
				return (s & 0xFFFFFF) / (float)0x1000000;
			}

			internal int Next(int max)
			{
				return (int)(Value() * max);
			}
		}
	}
}
