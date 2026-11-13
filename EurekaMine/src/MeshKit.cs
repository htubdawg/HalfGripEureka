using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EurekaMine
{
	/// <summary>How the ground of a batch drives (a HandlingModifier on its collider), or None for the game's default.</summary>
	internal enum Surface
	{
		None,
		Rough,
		Coal,
		Yard
	}

	/// <summary>
	/// Triangles that share a texture, a tint and the same behaviour, grown piece by piece and turned into one object at the end, so the
	/// mine is a few dozen draw calls rather than thousands.
	/// </summary>
	internal sealed class Batch
	{
		internal readonly string Name;
		internal readonly Texture2D Tex;
		internal readonly Color Tint;
		internal bool Collide;
		internal bool Wall;
		internal bool Slippery;
		internal bool Shadows = true;
		internal Color Emission;
		internal Surface Ground;

		private readonly List<Vector3> v = new List<Vector3>();
		private readonly List<Vector2> uv = new List<Vector2>();
		private readonly List<int> t = new List<int>();

		internal Batch(string name, Texture2D tex, Color tint)
		{
			Name = name;
			Tex = tex;
			Tint = tint;
		}

		internal int Count
		{
			get { return t.Count; }
		}

		/// <summary>A quad a-b-c-d, wound so its front faces the side from which a, b, c run clockwise. UVs in world units divided by tile.</summary>
		internal void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float tile)
		{
			float w = (b - a).magnitude / tile;
			float h = (d - a).magnitude / tile;
			Quad(a, b, c, d, new Vector2(0f, 0f), new Vector2(w, 0f), new Vector2(w, h), new Vector2(0f, h));
		}

		internal void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
		{
			int i = v.Count;
			v.Add(a);
			v.Add(b);
			v.Add(c);
			v.Add(d);
			uv.Add(ua);
			uv.Add(ub);
			uv.Add(uc);
			uv.Add(ud);
			t.Add(i);
			t.Add(i + 1);
			t.Add(i + 2);
			t.Add(i);
			t.Add(i + 2);
			t.Add(i + 3);
		}

		internal void Tri(Vector3 a, Vector3 b, Vector3 c, float tile)
		{
			int i = v.Count;
			v.Add(a);
			v.Add(b);
			v.Add(c);
			uv.Add(new Vector2(a.x + a.y, a.z + a.y) / tile);
			uv.Add(new Vector2(b.x + b.y, b.z + b.y) / tile);
			uv.Add(new Vector2(c.x + c.y, c.z + c.y) / tile);
			t.Add(i);
			t.Add(i + 1);
			t.Add(i + 2);
		}

		/// <summary>A box of the given full size, centred at c and turned by rot. Every face looks outward.</summary>
		internal void Box(Vector3 c, Vector3 size, Quaternion rot, float tile)
		{
			Vector3 x = rot * Vector3.right * (size.x * 0.5f);
			Vector3 y = rot * Vector3.up * (size.y * 0.5f);
			Vector3 z = rot * Vector3.forward * (size.z * 0.5f);
			Vector3 p000 = c - x - y - z, p100 = c + x - y - z, p010 = c - x + y - z, p110 = c + x + y - z;
			Vector3 p001 = c - x - y + z, p101 = c + x - y + z, p011 = c - x + y + z, p111 = c + x + y + z;
			Quad(p000, p010, p110, p100, tile);   // -z
			Quad(p101, p111, p011, p001, tile);   // +z
			Quad(p001, p011, p010, p000, tile);   // -x
			Quad(p100, p110, p111, p101, tile);   // +x
			Quad(p010, p011, p111, p110, tile);   // +y
			Quad(p001, p000, p100, p101, tile);   // -y
		}

		/// <summary>A prism with a gable: a box of footprint (w along rot's x, d along z) and wall height h, with a ridge of rise along z.</summary>
		internal void Gable(Vector3 baseCentre, float w, float d, float h, float rise, Quaternion rot, float tile, Batch roof)
		{
			Box(baseCentre + rot * Vector3.up * (h * 0.5f), new Vector3(w, h, d), rot, tile);
			Vector3 x = rot * Vector3.right * (w * 0.5f + 0.8f);
			Vector3 z = rot * Vector3.forward * (d * 0.5f + 0.8f);
			Vector3 top = baseCentre + rot * Vector3.up * h;
			Vector3 ridge = rot * Vector3.up * rise;
			Batch r = roof ?? this;
			r.Quad(top - x - z, top - x + z, top + z + ridge, top - z + ridge, tile);
			r.Quad(top + x + z, top + x - z, top - z + ridge, top + z + ridge, tile);
			Tri(top - x - z, top - z + ridge, top + x - z, tile);
			Tri(top + x + z, top + z + ridge, top - x + z, tile);
		}

		/// <summary>A cylinder (n sides) from a to b of radius r, open ended unless caps.</summary>
		internal void Cylinder(Vector3 a, Vector3 b, float r, int n, float tile, bool caps)
		{
			Vector3 axis = (b - a).normalized;
			Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
			Vector3 up = Vector3.Cross(side, axis);
			for (int i = 0; i < n; i++)
			{
				float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
				Vector3 d0 = (side * Mathf.Cos(a0) + up * Mathf.Sin(a0)) * r;
				Vector3 d1 = (side * Mathf.Cos(a1) + up * Mathf.Sin(a1)) * r;
				Quad(a + d0, b + d0, b + d1, a + d1, tile);
				if (caps)
				{
					Tri(a + d1, a, a + d0, tile);
					Tri(b + d0, b, b + d1, tile);
				}
			}
		}

		/// <summary>A cone or a heap: a ring of radius r at the base, rising to the apex, made rough by jitter (0 for smooth).</summary>
		internal void Heap(Vector3 baseCentre, float r, float height, int n, int rings, float jitter, System.Random rng, float tile)
		{
			Vector3[,] p = new Vector3[rings + 1, n];
			for (int k = 0; k <= rings; k++)
			{
				float f = (float)k / rings;
				float rr = r * (1f - f);
				float hh = height * (1f - (1f - f) * (1f - f));
				for (int i = 0; i < n; i++)
				{
					float a = i * Mathf.PI * 2f / n;
					float j = k == 0 || k == rings || rng == null ? 0f : (float)(rng.NextDouble() - 0.5) * jitter;
					p[k, i] = baseCentre + new Vector3(Mathf.Cos(a) * (rr + j), hh + j * 0.4f, Mathf.Sin(a) * (rr + j));
				}
			}
			for (int k = 0; k < rings; k++)
			{
				for (int i = 0; i < n; i++)
				{
					int i1 = (i + 1) % n;
					Quad(p[k, i], p[k + 1, i], p[k + 1, i1], p[k, i1], tile);
				}
			}
		}

		internal Mesh ToMesh()
		{
			Mesh m = new Mesh { name = Name, indexFormat = IndexFormat.UInt32 };
			m.SetVertices(v);
			m.SetUVs(0, uv);
			m.SetTriangles(t, 0);
			m.RecalculateNormals();
			m.RecalculateBounds();
			return m;
		}
	}

	/// <summary>Materials made from the scene's own ground shader, one per texture, tint and look. Freed with the scene (no HideAndDontSave, as ProcTrack 1.0.29 learned).</summary>
	internal static class Mats
	{
		private static Material source;
		private static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

		internal static void Reset(Material from)
		{
			source = from;
			cache.Clear();
		}

		internal static Material Get(Texture2D tex, Color tint, bool twoSided, Color emission)
		{
			string key = (tex != null ? tex.GetInstanceID().ToString() : "-") + "|" + ColorUtility.ToHtmlStringRGBA(tint) + "|" + twoSided + "|" + ColorUtility.ToHtmlStringRGB(emission);
			if (cache.TryGetValue(key, out Material m) && m != null)
			{
				return m;
			}
			Shader shader = source != null ? source.shader : null;
			m = shader != null ? new Material(shader) : null;
			if (m == null || !(m.HasProperty("_BaseColor") || m.HasProperty("_Color")) || !(m.HasProperty("_BaseMap") || m.HasProperty("_MainTex")))
			{
				m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default"));
			}
			m.name = "Eureka " + (tex != null ? tex.name : "plain");
			if (tex != null)
			{
				if (m.HasProperty("_BaseMap"))
				{
					m.SetTexture("_BaseMap", tex);
				}
				if (m.HasProperty("_MainTex"))
				{
					m.SetTexture("_MainTex", tex);
				}
			}
			if (m.HasProperty("_BaseColor"))
			{
				m.SetColor("_BaseColor", tint);
			}
			if (m.HasProperty("_Color"))
			{
				m.SetColor("_Color", tint);
			}
			if (m.HasProperty("_Smoothness"))
			{
				m.SetFloat("_Smoothness", 0.05f);
			}
			if (m.HasProperty("_Metallic"))
			{
				m.SetFloat("_Metallic", 0f);
			}
			if (emission.maxColorComponent > 0.01f && m.HasProperty("_EmissionColor"))
			{
				m.EnableKeyword("_EMISSION");
				m.SetColor("_EmissionColor", emission);
				m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
			}
			if (twoSided && m.HasProperty("_Cull"))
			{
				m.SetFloat("_Cull", 0f);
			}
			cache[key] = m;
			return m;
		}
	}
}
