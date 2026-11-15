using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EurekaMine
{
	/// <summary>
	/// The game's own pieces for the country round the pithead, and the sky over it. The Forest map's pines (terrain tree prototypes
	/// <c>ForrestTree1..4</c>) and its backdrop of tree lines (sprites <c>TreeLineClose</c> and <c>TreeLineDistant</c> on
	/// <c>MAT_TreeLine</c>) are loaded only with a scene that uses them; the main menu has both, so they are copied when it loads and the
	/// copies kept in a hidden holder, which keeps their meshes, sprites and materials alive for the Stadium (the Open World mod's way with
	/// the pines, after the Highway mod's TreeSource). <see cref="Scenery"/> says where they go; this puts them there. The sky is
	/// <see cref="SkyPaint"/>'s, in a cubemap on a copy of the Stadium's own <c>Skybox/Cubemap</c> material.
	/// </summary>
	internal static class Backdrop
	{
		private static GameObject keeper;
		private static readonly List<GameObject> pines = new List<GameObject>();
		private static readonly List<float> pineHeights = new List<float>();
		private static GameObject lineNear, lineFar;
		private static bool linesSought;

		/// <summary>Looks for the pines and tree lines in the scene just loaded; does nothing once both are found.</summary>
		internal static void Capture()
		{
			try
			{
				if (pines.Count == 0)
				{
					CapturePines();
				}
				if (lineNear == null || lineFar == null)
				{
					CaptureLines();
				}
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning("Could not copy the Forest's trees: " + e.Message);
			}
		}

		private static Transform Keeper()
		{
			if (keeper == null)
			{
				keeper = new GameObject("EurekaMine backdrop keeper") { hideFlags = HideFlags.HideAndDontSave };
				keeper.SetActive(false);
				UnityEngine.Object.DontDestroyOnLoad(keeper);
			}
			return keeper.transform;
		}

		private static void CapturePines()
		{
			List<GameObject> prefabs = new List<GameObject>();
			foreach (Terrain terrain in Resources.FindObjectsOfTypeAll<Terrain>())
			{
				if (terrain == null || terrain.terrainData == null)
				{
					continue;
				}
				foreach (TreePrototype proto in terrain.terrainData.treePrototypes)
				{
					if (proto.prefab != null && proto.prefab.name.StartsWith("ForrestTree", StringComparison.Ordinal) && !prefabs.Contains(proto.prefab))
					{
						prefabs.Add(proto.prefab);
					}
				}
			}
			if (prefabs.Count == 0)
			{
				return;
			}
			prefabs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
			foreach (GameObject prefab in prefabs)
			{
				GameObject copy = UnityEngine.Object.Instantiate(prefab, Keeper(), false);
				copy.name = prefab.name;
				copy.transform.localPosition = Vector3.zero;
				copy.transform.localRotation = Quaternion.identity;
				copy.transform.localScale = Vector3.one;
				// colliders are of no use past the fence
				foreach (Collider c in copy.GetComponentsInChildren<Collider>(true))
				{
					UnityEngine.Object.DestroyImmediate(c);
				}
				float height = Height(copy);
				if (height <= 0.01f)
				{
					UnityEngine.Object.DestroyImmediate(copy);
					continue;
				}
				pines.Add(copy);
				pineHeights.Add(height);
			}
			Mod.Log.LogInfo("Forest pines copied: " + string.Join(", ", pines.Select((p, i) => p.name + " (" + pineHeights[i].ToString("0.0") + " high)").ToArray()));
		}

		/// <summary>A model's height from its meshes' bounds in its own frame (its renderers' world bounds are not valid while it is inactive).</summary>
		private static float Height(GameObject go)
		{
			float lo = float.MaxValue, hi = float.MinValue;
			Matrix4x4 toRoot = go.transform.worldToLocalMatrix;
			foreach (MeshFilter f in go.GetComponentsInChildren<MeshFilter>(true))
			{
				if (f.sharedMesh == null)
				{
					continue;
				}
				Matrix4x4 m = toRoot * f.transform.localToWorldMatrix;
				Bounds b = f.sharedMesh.bounds;
				for (int i = 0; i < 8; i++)
				{
					Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
					float y = m.MultiplyPoint3x4(corner).y;
					lo = Mathf.Min(lo, y);
					hi = Mathf.Max(hi, y);
				}
			}
			return hi > lo ? hi - lo : 0f;
		}

		private static void CaptureLines()
		{
			foreach (SpriteRenderer sr in Resources.FindObjectsOfTypeAll<SpriteRenderer>())
			{
				if (sr == null || sr.sprite == null || sr.gameObject.scene.name == null || !sr.gameObject.scene.IsValid())
				{
					continue;
				}
				string name = sr.gameObject.name;
				bool near = name.StartsWith("TreeLineClose", StringComparison.Ordinal), far = name.StartsWith("TreeLineDistant", StringComparison.Ordinal);
				if ((near && lineNear == null) || (far && lineFar == null))
				{
					GameObject copy = UnityEngine.Object.Instantiate(sr.gameObject, Keeper(), false);
					copy.name = near ? "TreeLineClose" : "TreeLineDistant";
					copy.transform.localPosition = Vector3.zero;
					copy.transform.localRotation = Quaternion.identity;
					copy.transform.localScale = Vector3.one;
					// the strip of forest floor under the sprite is the Forest's ground colour, not this grass: left out, the trees stand in the grass
					foreach (SpriteRenderer child in copy.GetComponentsInChildren<SpriteRenderer>(true))
					{
						if (child.gameObject != copy)
						{
							child.gameObject.SetActive(false);
						}
					}
					if (near)
					{
						lineNear = copy;
					}
					else
					{
						lineFar = copy;
					}
					Bounds b = sr.sprite.bounds;
					Mod.Log.LogInfo("Forest tree line copied: " + copy.name + " sprite " + sr.sprite.name + " " + b.size.x.ToString("0.00") + " x " + b.size.y.ToString("0.00")
						+ " (foot " + b.min.y.ToString("0.00") + "), material " + (sr.sharedMaterial != null ? sr.sharedMaterial.name + " (" + sr.sharedMaterial.shader.name + ")" : "none"));
				}
			}
			if (!linesSought && (lineNear == null || lineFar == null) && SceneHasMenu())
			{
				linesSought = true;
				Mod.Info("No tree line sprites in the main menu: the tree lines are made in code");
			}
		}

		private static bool SceneHasMenu()
		{
			return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("MENU");
		}

		/// <summary>Before the build: tells <see cref="Scenery"/> which of the game's pieces it can count on.</summary>
		internal static void Prepare()
		{
			Scenery.GamePines = pines.Count > 0;
			Scenery.GameLines = lineNear != null || lineFar != null;
			if (!Scenery.GamePines || !Scenery.GameLines)
			{
				Mod.Log.LogInfo("Backdrop: " + (Scenery.GamePines ? "" : "no Forest pines (made in code) ") + (Scenery.GameLines ? "" : "no tree line sprites (made in code)"));
			}
		}

		/// <summary>After the build: the copies of the game's pines and tree lines where <see cref="Scenery"/> put them.</summary>
		internal static void Place(Transform root)
		{
			GameObject holder = new GameObject("Backdrop");
			holder.transform.SetParent(root, false);
			int trees = 0, lines = 0;
			if (Scenery.GamePines)
			{
				for (int k = 0; k < Scenery.Trees.Count; k++)
				{
					Scenery.Tree t = Scenery.Trees[k];
					int m = k % pines.Count;
					GameObject go = UnityEngine.Object.Instantiate(pines[m], holder.transform, false);
					go.transform.SetPositionAndRotation(t.Pos, Quaternion.Euler(0f, t.Yaw, 0f));
					go.transform.localScale = Vector3.one * (t.Height / pineHeights[m]);
					trees++;
				}
			}
			if (Scenery.GameLines)
			{
				foreach (Scenery.Line l in Scenery.Lines)
				{
					GameObject source = (l.Far ? lineFar : lineNear) ?? lineNear ?? lineFar;
					Sprite sprite = source.GetComponent<SpriteRenderer>().sprite;
					Bounds b = sprite.bounds;
					float s = l.Height / Mathf.Max(0.01f, b.size.y);
					float w = b.size.x * s;
					// as many side by side as it takes to cover the line's width, overlapping a little
					int n = Mathf.Max(1, Mathf.CeilToInt(l.Width / (w * 0.9f)));
					Vector3 right = Vector3.Cross(Vector3.up, l.Facing);
					for (int k = 0; k < n; k++)
					{
						float along = (k - (n - 1) * 0.5f) * Mathf.Min(w * 0.9f, l.Width / n);
						GameObject go = UnityEngine.Object.Instantiate(source, holder.transform, false);
						// the sprite faces -z: turned so its face is toward the yard; its foot (a little sunk) on the ground
						Vector3 at = l.Pos + right * along + Vector3.up * (-b.min.y * s - l.Height * 0.04f);
						go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(-l.Facing, Vector3.up));
						go.transform.localScale = new Vector3(k % 2 == 0 ? s : -s, s, s);
						lines++;
					}
				}
			}
			Mod.Info("Backdrop: " + trees + " Forest pines, " + lines + " tree line sprites placed");
		}

		// ---- the sky -------------------------------------------------------------------------------------------------------------------

		private static Material sky;
		private static Cubemap cube;
		private static Light sun;
		private static Quaternion sunWas;
		private static readonly Dictionary<Camera, float> farWas = new Dictionary<Camera, float>();

		/// <summary>How far the cameras see on the surface: the Stadium's main camera stops at 1200, nearer than the tree lines and the town.</summary>
		private const float Reach = 3200f;

		/// <summary>
		/// The sky and the light over the yard: the painted dawn sky, the sun's light turned to come low from the dawn glow in the
		/// east-north-east, and the cameras made to see the skyline. The weather (colours of the light, ambient, fog) is set in
		/// <see cref="MapEntry"/>.
		/// </summary>
		internal static void Sky()
		{
			if (!Mod.DawnSky.Value)
			{
				return;
			}
			try
			{
				Material m = SkyMaterial(RenderSettings.skybox);
				if (m != null)
				{
					RenderSettings.skybox = m;
					if (Mod.Map != null && Mod.Map.weather != null)
					{
						Mod.Map.weather.skyboxMaterial = m;
					}
					DynamicGI.UpdateEnvironment();
				}
				WeatherController weather = UnityEngine.Object.FindFirstObjectByType<WeatherController>();
				sun = weather != null ? weather.directionalLight : null;
				if (sun != null)
				{
					sunWas = sun.transform.rotation;
					float[] s = SkyPaint.Sun;
					// the light that comes before the sun: from the glow above where it will rise, about 12 degrees up
					Vector3 from = new Vector3(s[0], 0.2f, s[2]).normalized;
					sun.transform.rotation = Quaternion.LookRotation(-from, Vector3.up);
				}
				foreach (Camera c in Camera.allCameras)
				{
					if (c != null && c.name != "InteriorCamera" && c.farClipPlane < Reach && c.farClipPlane >= 500f)
					{
						farWas[c] = c.farClipPlane;
						c.farClipPlane = Reach;
					}
				}
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning("Could not set up the dawn sky: " + e.Message);
			}
		}

		/// <summary>Puts the sun and the cameras back (the map unloading).</summary>
		internal static void Restore()
		{
			if (sun != null)
			{
				sun.transform.rotation = sunWas;
			}
			sun = null;
			foreach (KeyValuePair<Camera, float> e in farWas)
			{
				if (e.Key != null)
				{
					e.Key.farClipPlane = e.Value;
				}
			}
			farWas.Clear();
		}

		private const int Face = 256;

		/// <summary>The painted sky in a cubemap on a copy of a <c>Skybox/Cubemap</c> material (the Stadium's own, so the shader is in the build).</summary>
		private static Material SkyMaterial(Material template)
		{
			if (sky != null)
			{
				return sky;
			}
			Shader shader = template != null && template.shader != null && template.shader.name == "Skybox/Cubemap" ? template.shader : Shader.Find("Skybox/Cubemap");
			if (shader == null)
			{
				Mod.Log.LogWarning("No Skybox/Cubemap shader: the sky stays the weather's own");
				return null;
			}
			float t0 = Time.realtimeSinceStartup;
			cube = new Cubemap(Face, TextureFormat.RGBA32, false) { name = "EurekaDawn", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
			Color[] px = new Color[Face * Face];
			foreach (CubemapFace face in new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ })
			{
				for (int j = 0; j < Face; j++)
				{
					// a cubemap face's pixels start at its top left (unlike a Texture2D's)
					float tc = 2f * (j + 0.5f) / Face - 1f;
					for (int i = 0; i < Face; i++)
					{
						float sc = 2f * (i + 0.5f) / Face - 1f;
						Vector3 d = Direction(face, sc, tc);
						SkyPaint.Colour(d.x, d.y, d.z, out float r, out float g, out float b);
						px[j * Face + i] = new Color(r, g, b, 1f);
					}
				}
				cube.SetPixels(px, face);
			}
			cube.Apply(false, true);
			sky = template != null && template.shader == shader ? new Material(template) : new Material(shader);
			sky.name = "EurekaDawnSky";
			sky.hideFlags = HideFlags.HideAndDontSave;
			sky.SetTexture("_Tex", cube);
			sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
			sky.SetFloat("_Exposure", 1f);
			sky.SetFloat("_Rotation", 0f);
			Mod.Log.LogInfo("Dawn sky painted (" + Face + " a face) in " + ((Time.realtimeSinceStartup - t0) * 1000f).ToString("0") + " ms on a copy of " + (template != null ? template.name : shader.name));
			return sky;
		}

		/// <summary>The direction through a cubemap texel (sc right, tc down, both -1 to 1), in Unity's (Direct3D's) layout of the faces.</summary>
		private static Vector3 Direction(CubemapFace face, float sc, float tc)
		{
			switch (face)
			{
				case CubemapFace.PositiveX:
					return new Vector3(1f, -tc, -sc);
				case CubemapFace.NegativeX:
					return new Vector3(-1f, -tc, sc);
				case CubemapFace.PositiveY:
					return new Vector3(sc, 1f, tc);
				case CubemapFace.NegativeY:
					return new Vector3(sc, -1f, -tc);
				case CubemapFace.PositiveZ:
					return new Vector3(sc, -tc, 1f);
				default:
					return new Vector3(-sc, -tc, -1f);
			}
		}
	}
}
