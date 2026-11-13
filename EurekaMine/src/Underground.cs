using System.Collections.Generic;
using UnityEngine;

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

		internal void Setup(TrackManager tm)
		{
			track = tm;
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
				Color tint = new Color(0.55f, 0.48f, 0.40f);
				RenderSettings.ambientSkyColor = Color.Lerp(skyColor, skyColor * 0.35f * tint, dark);
				RenderSettings.ambientEquatorColor = Color.Lerp(equatorColor, equatorColor * 0.35f * tint, dark);
				RenderSettings.ambientGroundColor = Color.Lerp(groundColor, groundColor * 0.35f * tint, dark);
				RenderSettings.ambientIntensity = Mathf.Lerp(ambientIntensity, ambientIntensity * 0.8f, dark);
				RenderSettings.fog = true;
				RenderSettings.fogColor = Color.Lerp(fogColor, new Color(0.05f, 0.04f, 0.035f), dark);
				float haze = dark * dark;
				RenderSettings.fogStartDistance = Mathf.Lerp(fogStart, 10f, haze);
				RenderSettings.fogEndDistance = Mathf.Lerp(fogEnd, 520f, haze);
			}
			else if (captured)
			{
				Restore();
			}

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
					h.spotlight.SetActive(Depth(h.transform.position) > 0.2f);
				}
			}
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
		}
	}

	/// <summary>
	/// The mine's lamps glow on their own; a handful of real point lights are moved to the lamps nearest the camera a few times a second,
	/// so the haulage is lit in pools round the player without paying for hundreds of lights.
	/// </summary>
	internal sealed class LampLights : MonoBehaviour
	{
		private readonly List<Light> pool = new List<Light>();
		private float next;
		private readonly List<int> order = new List<int>();

		internal void Setup(int count)
		{
			for (int i = 0; i < Mathf.Clamp(count, 0, 32); i++)
			{
				GameObject go = new GameObject("lamp light");
				go.transform.SetParent(transform, false);
				Light l = go.AddComponent<Light>();
				l.type = LightType.Point;
				l.range = 62f;
				l.intensity = 3.2f;
				l.color = new Color(1f, 0.78f, 0.5f);
				l.shadows = LightShadows.None;
				l.renderMode = LightRenderMode.ForcePixel;
				go.SetActive(false);
				pool.Add(l);
			}
		}

		private void LateUpdate()
		{
			if (pool.Count == 0 || Time.time < next)
			{
				return;
			}
			next = Time.time + 0.25f;
			Camera c = Camera.main;
			if (c == null)
			{
				return;
			}
			Vector3 eye = c.transform.position;
			List<Vector3> lamps = Mine.Lamps;
			order.Clear();
			for (int i = 0; i < lamps.Count; i++)
			{
				if ((lamps[i] - eye).sqrMagnitude < 400f * 400f)
				{
					order.Add(i);
				}
			}
			order.Sort((a, b) => (lamps[a] - eye).sqrMagnitude.CompareTo((lamps[b] - eye).sqrMagnitude));
			for (int k = 0; k < pool.Count; k++)
			{
				bool use = k < order.Count;
				pool[k].gameObject.SetActive(use);
				if (use)
				{
					pool[k].transform.position = lamps[order[k]] + Vector3.down * 1.5f;
				}
			}
		}
	}
}
