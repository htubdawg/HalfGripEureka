using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace EurekaMine
{
	/// <summary>
	/// The dark: once the camera is inside the drift the sun and the sky's light fade almost out and a brown haze of coal dust closes in,
	/// and every car's headlights come on while it is underground. Coming out (a replay, the wreck camera) puts the weather back exactly
	/// as it was. The method is Temple of Drift's cave atmosphere (4l3x4nd3r, MIT).
	/// </summary>
	internal sealed class Underground : MonoBehaviour
	{
		private TrackManager track;
		private Light sun;
		private HeadLightsController[] heads = new HeadLightsController[0];
		private float headsAt = -1f;
		private float amount;
		private bool captured;

		private float sunIntensity;
		private Color skyColor, equatorColor, groundColor, fogColor;
		private float ambientIntensity, fogStart, fogEnd;
		private bool fogOn;

		private ParticleSystem dust;

		/// <summary>
		/// Coal dust hanging in the air round the camera underground: faint specks drifting slowly, made with the material of one of the
		/// scene's own particle systems (the Stadium's ash), so it renders the way the game's particles do.
		/// </summary>
		private void MakeDust()
		{
			Material source = null;
			foreach (ParticleSystemRenderer pr in Resources.FindObjectsOfTypeAll<ParticleSystemRenderer>())
			{
				if (pr != null && pr.sharedMaterial != null && pr.sharedMaterial.shader != null && pr.sharedMaterial.shader.name.Contains("Particles"))
				{
					source = pr.sharedMaterial;
					break;
				}
			}
			if (source == null)
			{
				Mod.Info("No particle material in the scene: no dust in the air");
				return;
			}
			GameObject go = new GameObject("coal dust");
			go.transform.SetParent(transform, false);
			dust = go.AddComponent<ParticleSystem>();
			dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
			ParticleSystem.MainModule main = dust.main;
			main.loop = true;
			main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
			main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
			main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.2f);
			main.startColor = new Color(0.75f, 0.7f, 0.62f, 0.55f);
			main.maxParticles = 600;
			main.simulationSpace = ParticleSystemSimulationSpace.World;
			main.gravityModifier = 0.002f;
			ParticleSystem.EmissionModule emission = dust.emission;
			emission.rateOverTime = 90f;
			ParticleSystem.ShapeModule shape = dust.shape;
			shape.enabled = true;
			shape.shapeType = ParticleSystemShapeType.Box;
			shape.scale = new Vector3(70f, 16f, 70f);
			ParticleSystem.NoiseModule noise = dust.noise;
			noise.enabled = true;
			noise.strength = 0.25f;
			noise.frequency = 0.15f;
			ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
			renderer.sharedMaterial = new Material(source);
			renderer.renderMode = ParticleSystemRenderMode.Billboard;
		}

		internal void Setup(TrackManager tm)
		{
			track = tm;
			try
			{
				MakeDust();
			}
			catch (System.Exception e)
			{
				Mod.Log.LogWarning("Could not make the dust in the air: " + e.Message);
				dust = null;
			}
			WeatherController weather = FindFirstObjectByType<WeatherController>();
			sun = weather != null ? weather.directionalLight : null;
			if (sun == null)
			{
				foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
				{
					if (l.type == LightType.Directional && (sun == null || l.intensity > sun.intensity))
					{
						sun = l;
					}
				}
			}
		}

		/// <summary>How far underground a point is: 0 in the open, 1 forty units inside the drift mouth or anywhere below the yard.</summary>
		internal static float Depth(Vector3 p)
		{
			if (p.y < -14f)
			{
				return 1f;
			}
			// in the hill: past the face of the drift and inside the slope's tunnel
			if (p.x > Pithead.HillX0 - 2f && p.x < Pithead.HillBackFoot && p.y < Pithead.HillTop - 2f && p.z > Pithead.HillZ0 && p.z < Pithead.HillZ1)
			{
				return Mathf.Clamp01((p.x - Pithead.HillX0) / 40f);
			}
			return Mathf.Clamp01(-p.y / 14f) * (p.x > Pithead.HillX0 ? 1f : 0f);
		}

		private static bool Observer(TrackManager tm, out Vector3 pos)
		{
			Camera c = Camera.main;
			if (c != null && c.isActiveAndEnabled)
			{
				pos = c.transform.position;
				return true;
			}
			if (tm != null && tm.playerCarController != null)
			{
				pos = tm.playerCarController.transform.position;
				return true;
			}
			pos = Vector3.zero;
			return false;
		}

		private void LateUpdate()
		{
			if (track == null)
			{
				return;
			}
			float target = Observer(track, out Vector3 observer) ? Depth(observer) : 0f;
			amount = Mathf.MoveTowards(amount, target, Time.deltaTime * 1.5f);
			if (amount > 0.002f)
			{
				if (!captured)
				{
					Capture();
				}
				float dark = Mathf.SmoothStep(0f, 1f, amount);
				if (sun != null)
				{
					sun.intensity = sunIntensity * Mathf.Lerp(1f, 0.0f, dark);
				}
				// a faint fixed fill underground, not a share of the sky's light (Dark): the bulbs' pools and the headlights do the lighting
				RenderSettings.ambientSkyColor = Color.Lerp(skyColor, Dark.Sky, dark);
				RenderSettings.ambientEquatorColor = Color.Lerp(equatorColor, Dark.Equator, dark);
				RenderSettings.ambientGroundColor = Color.Lerp(groundColor, Dark.Ground, dark);
				RenderSettings.ambientIntensity = Mathf.Lerp(ambientIntensity, 1f, dark);
				RenderSettings.fog = true;
				RenderSettings.fogColor = Color.Lerp(fogColor, Dark.Fog, dark);
				float haze = dark * dark;
				RenderSettings.fogStartDistance = Mathf.Lerp(fogStart, Dark.FogStart, haze);
				RenderSettings.fogEndDistance = Mathf.Lerp(fogEnd, Dark.FogEnd, haze);
			}
			else if (captured)
			{
				Restore();
			}

			// the dust follows the camera, and only underground
			if (dust != null)
			{
				bool on = amount > 0.5f;
				if (on)
				{
					dust.transform.position = observer;
					if (!dust.isPlaying)
					{
						dust.Play();
					}
				}
				else if (dust.isPlaying)
				{
					dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
				}
			}

			Cabin(Mathf.SmoothStep(0f, 1f, amount));

			// cars come and go (a late rival): look the headlights up now and then
			if (Time.time - headsAt > 2f)
			{
				headsAt = Time.time;
				heads = FindObjectsByType<HeadLightsController>(FindObjectsSortMode.None);
			}
			foreach (HeadLightsController h in heads)
			{
				if (h != null && h.spotlight != null)
				{
					float depth = Depth(h.transform.position);
					h.spotlight.SetActive(depth > 0.2f);
					Headlamp(h, depth);
				}
			}
		}

		private Light cabin;
		private bool cabinTried;
		private float cabinAt;

		/// <summary>
		/// The cockpit is drawn by its own camera on the INTERIOR layer and lit by the game's <c>SyncInteriorLight</c>, a directional light
		/// that follows the sun and goes out when rays from the car to the sun are blocked: underground it is 0, and the mine's ambient (a
		/// faint fill, see <see cref="Dark"/>) is shared with the cabin, so the gauges and the shifter went nearly black (owner, 0.4.11).
		/// A dash lamp: one soft point light in the cabin between the driver's head and the instruments, lighting the INTERIOR layer only
		/// (so not one pixel of the mine changes), eased in with the camera's depth like the dark itself (<see cref="Mod.CabinLight"/>).
		/// </summary>
		private void Cabin(float dark)
		{
			if (cabin == null)
			{
				if (cabinTried && Time.time - cabinAt < 2f)
				{
					return;
				}
				cabinTried = true;
				cabinAt = Time.time;
				MakeCabin();
				if (cabin == null)
				{
					return;
				}
			}
			float level = Mathf.Max(0f, Mod.CabinLight.Value);
			cabin.enabled = dark > 0.01f && level > 0f;
			cabin.intensity = cabinScale * level * dark;
		}

		private float cabinScale;

		private void MakeCabin()
		{
			try
			{
				CarLink link = null;
				foreach (CarLink l in FindObjectsByType<CarLink>(FindObjectsSortMode.None))
				{
					if (l != null && l.cc != null && l.cc.interiorCamera != null)
					{
						link = l;
						break;
					}
				}
				int layer = LayerMask.NameToLayer("INTERIOR");
				if (link == null || layer < 0)
				{
					return;
				}
				Transform cam = link.cc.interiorCamera.transform;
				// the instruments and the shifter, where the game's car has them
				Vector3 sum = Vector3.zero;
				int n = 0;
				foreach (Component c in new Component[] { link.dialrpm, link.dialspeed, link.sa, link.sw })
				{
					if (c != null)
					{
						sum += c.transform.position;
						n++;
					}
				}
				Vector3 dash = n > 0 ? sum / n : cam.position + cam.forward * 8f - cam.up * 3f;
				float d = Mathf.Max(0.5f, Vector3.Distance(cam.position, dash));
				GameObject go = new GameObject("Eureka cabin lamp");
				go.transform.SetParent(cam.parent != null ? cam.parent : cam, true);
				// over the driver's knees, a little above the line from the eyes to the dash: the light falls on the gauges' faces and the gate
				go.transform.position = Vector3.Lerp(cam.position, dash, 0.45f) + cam.up * (0.2f * d);
				cabin = go.AddComponent<Light>();
				cabin.type = LightType.Point;
				cabin.cullingMask = 1 << layer;
				cabin.shadows = LightShadows.None;
				cabin.renderMode = LightRenderMode.ForcePixel;
				cabin.color = new Color(1f, 0.88f, 0.72f);
				float reach = Vector3.Distance(go.transform.position, dash);
				cabin.range = Mathf.Max(reach * 3f, d * 1.6f);
				// URP falls off as intensity over distance squared: scaled so CabinLight is the light that reaches the instruments
				cabinScale = reach * reach;
				cabin.intensity = 0f;
				Mod.Log.LogInfo("Cabin lamp: " + n + " instruments found, " + d.ToString("0.00") + " from the eyes to them, range " + cabin.range.ToString("0.00")
					+ ", intensity " + cabinScale.ToString("0.00") + " x CabinLight " + Mod.CabinLight.Value + " underground");
			}
			catch (System.Exception e)
			{
				Mod.Log.LogWarning("Could not make the cabin lamp: " + e.Message);
				cabin = null;
			}
		}

		/// <summary>A car's headlight as the game made it, kept to put back and to scale from.</summary>
		private sealed class Lamp
		{
			internal float Intensity, Range, Angle, Inner, Written = -1f;
		}

		private readonly Dictionary<Light, Lamp> lamps = new Dictionary<Light, Lamp>();
		private bool lampLogged;

		/// <summary>
		/// The game's headlight is a spot made for a night on open tarmac (intensity 500 over distance squared, range 150): in a dark bore it
		/// lands on the rock at a grazing angle, and the rock is dark, so it lit next to nothing (owner, 0.4.9). Underground it is made
		/// brighter, wider and longer (<see cref="Mod.HeadlightBoost"/>, <see cref="Mod.HeadlightAngle"/>), eased in with the car's depth.
		/// Still one light per car, so the renderer's 8 lights per object are enough for the whole field. Written only when the target
		/// changes, so a bot's warning flash (PitGuard sets the same light's intensity while it flashes) is left alone.
		/// </summary>
		private void Headlamp(HeadLightsController h, float depth)
		{
			Light light = h.spotlight.GetComponent<Light>();
			if (light == null)
			{
				return;
			}
			if (!lamps.TryGetValue(light, out Lamp lamp))
			{
				lamp = new Lamp { Intensity = light.intensity, Range = light.range, Angle = light.spotAngle, Inner = light.innerSpotAngle };
				lamps[light] = lamp;
				if (!lampLogged && track != null && track.playerCarController != null && h.GetComponentInParent<CarControllerNew>() == track.playerCarController)
				{
					lampLogged = true;
					LogLamp(light);
				}
			}
			float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 1f, depth));
			float boost = Mathf.Max(0f, Mod.HeadlightBoost.Value);
			float target = lamp.Intensity * Mathf.Lerp(1f, boost, k);
			if (Mathf.Abs(target - lamp.Written) < 0.5f)
			{
				return;
			}
			lamp.Written = target;
			light.intensity = target;
			float angle = Mathf.Clamp(Mod.HeadlightAngle.Value, 1f, 179f);
			light.spotAngle = Mathf.Lerp(lamp.Angle, Mathf.Max(lamp.Angle, angle), k);
			light.innerSpotAngle = Mathf.Lerp(lamp.Inner, Mathf.Max(lamp.Inner, angle * 0.55f), k);
			light.range = Mathf.Lerp(lamp.Range, Mathf.Max(lamp.Range, 220f), k);
		}

		/// <summary>
		/// Once, the player's headlight as the game set it up and whether anything of the mod's could keep it off the rock: the light's
		/// culling mask against the mine's layer, its rendering layers against the mine's renderers'.
		/// </summary>
		private void LogLamp(Light light)
		{
			try
			{
				int layer = SceneBuilder.MineLayer;
				uint ours = SceneBuilder.MineRenderingLayers;
				uint theirs = (uint)light.renderingLayerMask;
				Mod.Log.LogInfo("Headlight: " + light.type + " intensity " + light.intensity + " range " + light.range + " cone " + light.spotAngle
					+ "/" + light.innerSpotAngle + " aim (local) " + light.transform.localEulerAngles + " render mode " + light.renderMode
					+ "; culling mask " + (((light.cullingMask >> layer) & 1) != 0 ? "includes" : "EXCLUDES") + " the mine's layer " + LayerMask.LayerToName(layer)
					+ "; rendering layers light " + theirs + ", mine " + ours + ((theirs & ours) != 0 ? " (shared)" : " (NONE SHARED)")
					+ "; underground x" + Mod.HeadlightBoost.Value + ", cone " + Mod.HeadlightAngle.Value);
			}
			catch (System.Exception e)
			{
				Mod.Log.LogWarning("Could not describe the headlight: " + e.Message);
			}
		}

		/// <summary>The headlights as the game made them (coming out into the yard, or the map unloading).</summary>
		private void RestoreLamps()
		{
			foreach (KeyValuePair<Light, Lamp> e in lamps)
			{
				if (e.Key != null)
				{
					e.Key.intensity = e.Value.Intensity;
					e.Key.range = e.Value.Range;
					e.Key.spotAngle = e.Value.Angle;
					e.Key.innerSpotAngle = e.Value.Inner;
				}
			}
			lamps.Clear();
		}

		private void Capture()
		{
			captured = true;
			sunIntensity = sun != null ? sun.intensity : 1f;
			skyColor = RenderSettings.ambientSkyColor;
			equatorColor = RenderSettings.ambientEquatorColor;
			groundColor = RenderSettings.ambientGroundColor;
			ambientIntensity = RenderSettings.ambientIntensity;
			fogOn = RenderSettings.fog;
			fogColor = RenderSettings.fogColor;
			fogStart = RenderSettings.fogStartDistance;
			fogEnd = RenderSettings.fogEndDistance;
		}

		private void Restore()
		{
			captured = false;
			if (sun != null)
			{
				sun.intensity = sunIntensity;
			}
			RenderSettings.ambientSkyColor = skyColor;
			RenderSettings.ambientEquatorColor = equatorColor;
			RenderSettings.ambientGroundColor = groundColor;
			RenderSettings.ambientIntensity = ambientIntensity;
			RenderSettings.fog = fogOn;
			RenderSettings.fogColor = fogColor;
			RenderSettings.fogStartDistance = fogStart;
			RenderSettings.fogEndDistance = fogEnd;
		}

		private void OnDestroy()
		{
			if (captured)
			{
				Restore();
			}
			RestoreLamps();
			if (cabin != null)
			{
				Destroy(cabin.gameObject);
			}
		}
	}

	/// <summary>Materials for the painted lamp light (<see cref="Glow"/>). Freed with the scene.</summary>
	internal static class GlowMats
	{
		private static Material glowSource;
		private static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

		internal static void Reset()
		{
			glowSource = null;
			cache.Clear();
		}

		/// <summary>
		/// Whether the painted lamp light can be drawn: it needs URP's Particles/Unlit (texture times vertex colour, no lighting of its
		/// own), which the Stadium loads for its rain. A copy of a material already using it is taken, so the shader variant is one the
		/// game's build kept.
		/// </summary>
		internal static bool CanGlow()
		{
			if (glowSource != null)
			{
				return true;
			}
			const string name = "Universal Render Pipeline/Particles/Unlit";
			foreach (Material m in Resources.FindObjectsOfTypeAll<Material>())
			{
				if (m != null && m.shader != null && m.shader.name == name)
				{
					glowSource = m;
					break;
				}
			}
			if (glowSource == null)
			{
				Shader shader = Shader.Find(name);
				if (shader != null)
				{
					glowSource = new Material(shader);
				}
			}
			if (glowSource != null)
			{
				Mod.Log.LogInfo("Lamp glow drawn with a copy of '" + glowSource.name + "', keywords: " + string.Join(" ", glowSource.shaderKeywords));
			}
			return glowSource != null;
		}

		/// <summary>
		/// The painted lamp light over a surface: its texture times the vertex colours, added to what is beneath (so the light falls on the
		/// rock's own colours), no depth written, no fading near the camera or the walls.
		/// </summary>
		internal static Material For(Texture2D tex, bool twoSided)
		{
			string key = "glow|" + (tex != null ? tex.GetInstanceID().ToString() : "-") + "|" + twoSided;
			if (cache.TryGetValue(key, out Material m) && m != null)
			{
				return m;
			}
			m = new Material(glowSource) { name = "Eureka glow " + (tex != null ? tex.name : "plain") };
			foreach (string k in new[] { "_SOFTPARTICLES_ON", "_FADING_ON", "_DISTORTION_ON", "_FLIPBOOKBLENDING_ON", "_ALPHATEST_ON", "_ALPHAPREMULTIPLY_ON", "_ALPHAMODULATE_ON", "_COLOROVERLAY_ON", "_COLORCOLOR_ON", "_COLORADDSUBDIFF_ON" })
			{
				m.DisableKeyword(k);
			}
			m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
			m.SetTexture("_BaseMap", tex);
			m.SetTextureScale("_BaseMap", Vector2.one);
			m.SetTextureOffset("_BaseMap", Vector2.zero);
			m.SetColor("_BaseColor", Color.white);
			m.SetFloat("_Surface", 1f);
			m.SetFloat("_Blend", 2f);
			m.SetFloat("_SrcBlend", (float)BlendMode.One);
			m.SetFloat("_DstBlend", (float)BlendMode.One);
			m.SetFloat("_ZWrite", 0f);
			m.SetFloat("_Cull", twoSided ? 0f : 2f);
			m.SetFloat("_SoftParticlesEnabled", 0f);
			m.SetFloat("_CameraFadingEnabled", 0f);
			m.renderQueue = (int)RenderQueue.Transparent - 10;
			cache[key] = m;
			return m;
		}

	}

	/// <summary>
	/// Only when the lamps' light cannot be painted (<see cref="Glow"/>, no Particles/Unlit shader in the scene): a handful of real point
	/// lights sit at the lamps nearest the camera, so the haulage is lit in
	/// pools round the player without paying for hundreds of lights. Nothing may switch on or off where it can be seen (0.4.0 moved the
	/// whole pool to the nearest lamps four times a second, so the pools far ahead blinked in and out): each light keeps its lamp for as
	/// long as that lamp stays among the nearest, and its brightness falls smoothly to nothing at the distance of the nearest lamp left
	/// out of the pool. That distance moves smoothly as the camera does, so a lamp joins the pool dark and leaves it dark.
	/// </summary>
	internal sealed class LampLights : MonoBehaviour
	{
		private const float Intensity = 70f;
		private const float Reach = 400f;

		/// <summary>Height counts three times over in "nearest": a lamp on the level above or below, behind 30 units of rock, lights nothing on the road.</summary>
		private const float Vertical = 3f;

		private readonly List<Light> pool = new List<Light>();
		private int[] lampOf = new int[0];
		private int[] nearest = new int[0];
		private float[] nearestD = new float[0];
		private readonly HashSet<int> wanted = new HashSet<int>();

		private UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp;
		private int perObjectBefore = -1;

		internal void Setup(int count)
		{
			for (int i = 0; i < Mathf.Clamp(count, 0, 32); i++)
			{
				GameObject go = new GameObject("lamp light");
				go.transform.SetParent(transform, false);
				Light l = go.AddComponent<Light>();
				l.type = LightType.Point;
				// not Important: the renderer gives each object only a few lights, and the cars' headlights must win them
				l.range = 48f;
				l.intensity = Intensity;
				l.color = new Color(1f, 0.8f, 0.55f);
				l.shadows = LightShadows.None;
				l.renderMode = LightRenderMode.Auto;
				go.SetActive(false);
				pool.Add(l);
			}
			lampOf = new int[pool.Count];
			for (int k = 0; k < lampOf.Length; k++)
			{
				lampOf[k] = -1;
			}
			nearest = new int[pool.Count + 1];
			nearestD = new float[pool.Count + 1];
			RaiseLightLimit();
		}

		/// <summary>
		/// URP's forward renderer lights each object with only its few most important lights (the asset's per-object limit; the game's is
		/// already 8, the most, on 0.419): raised to the most while the mine is loaded, in case a later game lowers it, and put back after.
		/// </summary>
		private void RaiseLightLimit()
		{
			try
			{
				urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
				if (urp == null)
				{
					Mod.Log.LogWarning("Not running URP: the lamps' per-object light limit is left alone");
					return;
				}
				object mode = null;
				try
				{
					mode = Traverse.Create(urp.scriptableRenderer).Property("renderingModeActual").GetValue();
				}
				catch (System.Exception)
				{
				}
				perObjectBefore = urp.maxAdditionalLightsCount;
				urp.maxAdditionalLightsCount = 8;
				Mod.Log.LogInfo("Mine lamps: URP " + (mode != null ? mode.ToString() : "(rendering mode unknown)") + ", additional lights "
					+ urp.additionalLightsRenderingMode + ", per-object limit " + perObjectBefore + " -> " + urp.maxAdditionalLightsCount);
			}
			catch (System.Exception e)
			{
				Mod.Log.LogWarning("Could not raise the per-object light limit: " + e.Message);
				urp = null;
			}
		}

		private void OnDestroy()
		{
			if (urp != null && perObjectBefore >= 0)
			{
				urp.maxAdditionalLightsCount = perObjectBefore;
			}
		}

		private static float Distance(Vector3 lamp, Vector3 eye)
		{
			Vector3 d = lamp - eye;
			d.y *= Vertical;
			return d.magnitude;
		}

		private void LateUpdate()
		{
			int n = pool.Count;
			Camera c = Camera.main;
			if (n == 0 || c == null)
			{
				return;
			}
			Vector3 eye = c.transform.position;
			List<Vector3> lamps = Mine.Lamps;

			// the n + 1 nearest lamps, kept sorted (a few hundred lamps: cheap enough every frame, so nothing steps)
			int found = 0;
			for (int i = 0; i < lamps.Count; i++)
			{
				float d = Distance(lamps[i], eye);
				if (d >= Reach || found == n + 1 && d >= nearestD[n])
				{
					continue;
				}
				int at = found < n + 1 ? found++ : n;
				while (at > 0 && nearestD[at - 1] > d)
				{
					nearestD[at] = nearestD[at - 1];
					nearest[at] = nearest[at - 1];
					at--;
				}
				nearestD[at] = d;
				nearest[at] = i;
			}
			// where the pool's light has faded to nothing: the nearest lamp outside the pool
			float edge = found == n + 1 ? nearestD[n] : Reach;
			wanted.Clear();
			for (int k = 0; k < Mathf.Min(found, n); k++)
			{
				wanted.Add(nearest[k]);
			}

			// lights whose lamp has left the pool are free (it left at the edge, already dark); the lamps new to the pool take them
			for (int k = 0; k < n; k++)
			{
				if (lampOf[k] >= 0 && !wanted.Remove(lampOf[k]))
				{
					lampOf[k] = -1;
				}
			}
			foreach (int lamp in wanted)
			{
				for (int k = 0; k < n; k++)
				{
					if (lampOf[k] < 0)
					{
						lampOf[k] = lamp;
						break;
					}
				}
			}

			for (int k = 0; k < n; k++)
			{
				Light l = pool[k];
				float fade = 0f;
				if (lampOf[k] >= 0)
				{
					Vector3 p = lamps[lampOf[k]];
					fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge * 0.5f, edge, Distance(p, eye)));
					l.transform.position = p + Vector3.down * 1.5f;
				}
				bool on = fade > 0.001f;
				if (l.gameObject.activeSelf != on)
				{
					l.gameObject.SetActive(on);
				}
				l.intensity = Intensity * fade;
			}
		}
	}
}
