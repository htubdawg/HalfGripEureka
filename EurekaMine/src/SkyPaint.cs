using System;

namespace EurekaMine
{
	/// <summary>
	/// The sky over the pithead, painted in code: the half hour before sunrise in the coal country. Deep blue overhead with the last few
	/// stars in the west, the dawn glow low in the east-north-east where the sun is about to come up (behind the hill with the drift in it,
	/// ahead of the grid), the earth's shadow and the pink band over it on the western horizon, a few thin streaks of cloud lit pink from
	/// underneath, and the long even ridges of the Ridge and Valley country one behind another, fading into the haze. The ridges are in the
	/// picture rather than built: past the camera's reach, and at that distance they would not move against each other over the yard anyway.
	///
	/// The Stadium's sky shader is <c>Skybox/Cubemap</c> (its ash sky, <c>MAT_Planet_Sky_14</c>), so <see cref="Backdrop"/> fills a cubemap
	/// from <see cref="Colour"/>, one direction per texel. Nothing here uses Unity, so the preview paints the same sky. Screen (gamma) colours.
	/// After the Open World mod's <c>SkyPaint</c> (htubdawg/HalfGripOpenWorld).
	/// </summary>
	internal static class SkyPaint
	{
		/// <summary>Where the sun is: east-north-east (+x is east, toward the drift; +z north), 4 degrees under the horizon.</summary>
		internal static readonly float[] Sun = Normalize(0.92f, -0.07f, 0.38f);

		private static readonly float[] Zenith = { 0.07f, 0.10f, 0.24f };
		private static readonly float[] High = { 0.20f, 0.28f, 0.50f };
		private static readonly float[] Horizon = { 0.58f, 0.55f, 0.66f };
		private static readonly float[] Dawn = { 1.00f, 0.60f, 0.34f };
		private static readonly float[] DawnHigh = { 0.86f, 0.56f, 0.58f };
		private static readonly float[] Belt = { 0.66f, 0.50f, 0.60f };
		private static readonly float[] Shadow = { 0.32f, 0.36f, 0.52f };

		/// <summary>The haze at the foot of the sky, which the fog is set to so the ground runs into it (see <see cref="MapEntry"/>).</summary>
		internal static readonly float[] Haze = { 0.47f, 0.46f, 0.55f };

		/// <summary>The colour of the sky looking along (x, y, z), any length.</summary>
		internal static void Colour(float x, float y, float z, out float r, out float g, out float b)
		{
			float len = (float)Math.Sqrt(x * x + y * y + z * z);
			x /= len;
			y /= len;
			z /= len;
			float elev = (float)Math.Asin(Clamp(y, -1f, 1f));
			float az = (float)Math.Atan2(z, x);
			float h = (float)Math.Sqrt(x * x + z * z);
			// the sun's side: 1 looking straight at where it will rise, -1 straight away from it
			float sh = (float)Math.Sqrt(Sun[0] * Sun[0] + Sun[2] * Sun[2]);
			float toward = h > 1e-4f ? (x * Sun[0] + z * Sun[2]) / (h * sh) : 0f;

			float[] c = new float[3];
			if (elev >= 0f)
			{
				float t = elev / (float)(Math.PI / 2);
				// the gradient from the horizon up, steepest near the horizon as a clear dawn sky is
				float lo = (float)Math.Pow(1f - t, 5f);
				float mid = (float)Math.Pow(1f - t, 1.6f);
				for (int i = 0; i < 3; i++)
				{
					c[i] = Lerp(Lerp(Zenith[i], High[i], mid), Horizon[i], lo);
				}
				// the dawn glow round the sun's place, low and wide, orange at the horizon and rose above it
				float near = Math.Max(0f, toward);
				float glow = (float)Math.Pow(near, 3f) * (float)Math.Exp(-elev / 0.16f);
				float rose = (float)Math.Pow(near, 1.5f) * (float)Math.Exp(-elev / 0.45f) * 0.45f;
				for (int i = 0; i < 3; i++)
				{
					c[i] = Lerp(c[i], DawnHigh[i], Clamp01(rose));
					c[i] = Lerp(c[i], Dawn[i], Clamp01(glow));
				}
				// opposite the sun: the earth's shadow low on the horizon with the pink belt over it
				float away = Math.Max(0f, -toward);
				float e = elev * 180f / (float)Math.PI;
				float belt = away * Smooth(2f, 6f, e) * (1f - Smooth(9f, 18f, e)) * 0.7f;
				float shadow = away * (1f - Smooth(1f, 4.5f, e)) * 0.6f;
				for (int i = 0; i < 3; i++)
				{
					c[i] = Lerp(c[i], Belt[i], belt);
					c[i] = Lerp(c[i], Shadow[i], shadow);
				}
				// the last stars, high and away from the dawn
				float starSky = Smooth(15f, 40f, e) * Clamp01(0.3f - toward * 0.7f);
				if (starSky > 0f)
				{
					int sx = (int)Math.Floor((az + Math.PI) * 260f), sy = (int)Math.Floor(elev * 260f);
					uint hsh = Hash((uint)sx * 73856093u ^ (uint)sy * 19349663u);
					if ((hsh & 1023u) < 3u)
					{
						float s = starSky * (0.35f + (hsh >> 20 & 255u) / 255f * 0.5f);
						for (int i = 0; i < 3; i++)
						{
							c[i] = Lerp(c[i], 0.95f, s);
						}
					}
				}
				Clouds(az, elev, toward, c);
			}
			else
			{
				for (int i = 0; i < 3; i++)
				{
					c[i] = Lerp(Haze[i], Haze[i] * 0.6f, Clamp01(-elev / 0.3f));
				}
			}
			Ridges(az, elev, toward, c);
			r = Clamp01(c[0]);
			g = Clamp01(c[1]);
			b = Clamp01(c[2]);
		}

		/// <summary>
		/// Thin streaks of stratus between 3 and 25 degrees up, lying along the horizon: pink and gold underneath toward the dawn, grey-mauve
		/// away from it. Noise in (azimuth, distance on a cloud deck), stretched along the horizon.
		/// </summary>
		private static void Clouds(float az, float elev, float toward, float[] c)
		{
			float e = elev * 180f / (float)Math.PI;
			if (e < 1.5f || e > 30f)
			{
				return;
			}
			// a point on a flat deck of cloud: low clouds far away, so the streaks crowd toward the horizon. Streaks lie south-west to
			// north-east (stretched across that line), as the ridges do; working on the deck keeps the picture whole round the horizon
			float deck = 1f / Math.Max(0.05f, (float)Math.Tan(elev));
			float px = (float)Math.Cos(az) * deck, pz = (float)Math.Sin(az) * deck;
			float along = (px + pz) * 0.7071f, across = (pz - px) * 0.7071f;
			float u = along * 0.45f, v = across * 1.5f;
			float n = Noise(u, v) * 0.65f + Noise(u * 2.3f + 5f, v * 2.1f) * 0.35f;
			float cover = Smooth(0.6f, 0.8f, n) * Smooth(4f, 9f, e) * (1f - Smooth(14f, 26f, e));
			if (cover <= 0f)
			{
				return;
			}
			float lit = Clamp01(0.55f + toward * 0.45f);
			float[] under = { Lerp(0.40f, 0.92f, lit), Lerp(0.36f, 0.56f, lit), Lerp(0.46f, 0.52f, lit) };
			// thinner clouds let the sky through
			cover *= 0.55f;
			for (int i = 0; i < 3; i++)
			{
				c[i] = Lerp(c[i], under[i], cover);
			}
		}

		/// <summary>
		/// The ridges: four even-crested ranges round the whole horizon, nearest the darkest and the highest, each farther one a little lower
		/// and paler (more haze between), with the odd water gap. Away from the dawn they read as dark blue-grey; toward it, as silhouettes.
		/// </summary>
		private static void Ridges(float az, float elev, float toward, float[] c)
		{
			float e = elev * 180f / (float)Math.PI;
			if (e > 9f)
			{
				return;
			}
			for (int layer = 3; layer >= 0; layer--)
			{
				float seed = layer * 17.3f;
				// long crests: the Pennsylvania ridges run north-east, so they rise and fall slowly round the horizon
				// nearest (0) in front and dark; the two behind it rise higher, so they show over it, and the farthest is low and pale
				float[] mean = { 4.4f, 6.2f, 5.2f, 3.6f };
				float crest = mean[layer]
					+ (Ring(az, 2.6f, seed) - 0.5f) * 3.2f
					+ (Ring(az, 8f, seed + 3f) - 0.5f) * 0.9f
					+ (Ring(az, 30f, seed + 7f) - 0.5f) * 0.18f;
				// a water gap now and then, where a river cuts through the ridge
				float gap = Ring(az, 4.5f, seed * 2f + 11f);
				crest -= Smooth(0.74f, 0.84f, gap) * crest * 0.7f;
				crest = Math.Max(0.3f, crest);
				if (e > crest)
				{
					continue;
				}
				float haze = 0.35f + layer * 0.17f;
				float[] land = { 0.10f, 0.12f, 0.15f };
				for (int i = 0; i < 3; i++)
				{
					float tone = Lerp(land[i], Haze[i], haze);
					// a little of the dawn on the ridges facing it is not there yet: before sunrise the sides toward it are darker, not lit
					tone *= 1f - Math.Max(0f, toward) * 0.12f * (1f - haze);
					c[i] = tone;
				}
			}
		}

		// ---- helpers -------------------------------------------------------------------------------------------------------------------

		private static float Noise(float x, float y)
		{
			int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
			float fx = x - x0, fy = y - y0;
			fx = fx * fx * (3f - 2f * fx);
			fy = fy * fy * (3f - 2f * fy);
			float a = Corner(x0, y0), b = Corner(x0 + 1, y0), c = Corner(x0, y0 + 1), d = Corner(x0 + 1, y0 + 1);
			return Lerp(Lerp(a, b, fx), Lerp(c, d, fx), fy);
		}

		/// <summary>Noise round the horizon: sampled on a circle, so it joins up where the azimuth wraps.</summary>
		private static float Ring(float az, float scale, float seed)
		{
			return Noise((float)Math.Cos(az) * scale + seed, (float)Math.Sin(az) * scale + seed * 0.37f);
		}

		private static float Corner(int x, int y)
		{
			return (Hash((uint)x * 2654435761u ^ (uint)y * 40503u) & 65535u) / 65535f;
		}

		private static uint Hash(uint h)
		{
			h ^= h >> 16;
			h *= 0x7feb352du;
			h ^= h >> 15;
			h *= 0x846ca68bu;
			h ^= h >> 16;
			return h;
		}

		private static float[] Normalize(float x, float y, float z)
		{
			float l = (float)Math.Sqrt(x * x + y * y + z * z);
			return new[] { x / l, y / l, z / l };
		}

		private static float Lerp(float a, float b, float t)
		{
			return a + (b - a) * t;
		}

		private static float Clamp(float v, float lo, float hi)
		{
			return v < lo ? lo : v > hi ? hi : v;
		}

		private static float Clamp01(float v)
		{
			return Clamp(v, 0f, 1f);
		}

		private static float Smooth(float a, float b, float v)
		{
			float t = Clamp01((v - a) / (b - a));
			return t * t * (3f - 2f * t);
		}
	}
}
