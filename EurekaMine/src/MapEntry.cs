using System;
using System.Linq;
using UnityEngine;

namespace EurekaMine
{
	/// <summary>Adds Eureka Mine to <c>RunConfig.maps</c>. A saved run finds its maps there by name, so this must happen before one is restored.</summary>
	internal static class MapEntry
	{
		/// <summary>The scene the map is built in: the Stadium's.</summary>
		internal static int HostTrack;

		/// <summary>The Stadium's lap, which its time objectives are for.</summary>
		private const float StadiumLap = 1457f;

		/// <summary>The index the map reports as its track, so its records do not mix with another map's (Highway 20, Temple of Drift and ProcTrack 21-25, Open World 30).</summary>
		internal const int TrackId = 40;

		/// <summary>The map's name in a save. Never change it.</summary>
		internal const string MapName = "EurekaMine";

		internal static void Ensure()
		{
			try
			{
				RunConfig config = RunConfig.Instance;
				if (config == null || config.maps == null)
				{
					return;
				}
				if (Mod.Map == null)
				{
					MapDefinition source = config.FindMap("StadiumAsh") ?? config.maps.FirstOrDefault(m => m != null);
					if (source == null)
					{
						Mod.Log.LogWarning("The run config has no map to copy: Eureka Mine is not added.");
						return;
					}
					Mod.Map = Make(source);
					Mod.Log.LogInfo("Eureka Mine map made from " + source.name + " (host track " + HostTrack + ", benchmark " + Mod.Map.timeEarly.ToString("0") + "/" + Mod.Map.timeMid.ToString("0") + "/" + Mod.Map.timeLate.ToString("0") + " s)");
				}
				if (!config.maps.Contains(Mod.Map))
				{
					config.maps.Add(Mod.Map);
				}
			}
			catch (Exception e)
			{
				Mod.Log.LogError("Could not add the Eureka Mine map: " + e);
			}
		}

		internal static void Remove()
		{
			RunConfig config = RunConfig.Instance;
			if (config != null && Mod.Map != null)
			{
				config.maps.RemoveAll(m => m == Mod.Map);
			}
		}

		private static MapDefinition Make(MapDefinition source)
		{
			HostTrack = source.trackIndex;
			MapDefinition map = UnityEngine.Object.Instantiate(source);
			map.hideFlags = HideFlags.HideAndDontSave;
			map.name = MapName;
			map.displayName = "Eureka Mine";
			map.conditions = string.Empty;
			map.trackIndex = TrackId;
			map.allowMirror = false;
			map.allowReverse = false;
			map.allowMirrorReverse = false;
			map.mirrorIcon = null;
			map.reverseIcon = null;
			map.mirrorReverseIcon = null;
			map.excludeFromLast = false;
			map.notBesideSameTrack = false;
			map.altMusicTrack = null;
			map.altMusicChance = 0f;

			// A race is the run in from the pithead once, plus the laps: the run in is spread over three laps' worth of time.
			float scale = (Route.Main.Length + Route.Runin.Length / 3f) / StadiumLap * Mod.BenchmarkFactor.Value;
			map.timeEarly = source.timeEarly * scale;
			map.timeMid = source.timeMid * scale;
			map.timeLate = source.timeLate * scale;

			// The Stadium's grey ash day without the ash: an overcast coal-country sky over the pithead.
			if (source.weather != null)
			{
				WeatherProfile weather = UnityEngine.Object.Instantiate(source.weather);
				weather.hideFlags = HideFlags.HideAndDontSave;
				weather.rain = false;
				weather.snow = false;
				weather.ash = false;
				weather.headlightsActive = false;
				weather.fogActive = true;
				weather.fogColor = new Color(0.46f, 0.45f, 0.44f);
				weather.fogStart = 80f;
				weather.fogEnd = 2200f;
				weather.gripExponent = 1f;
				weather.gripMultiplier = 1f;
				map.weather = weather;
			}

			Sprite icon = Mod.Ctx.Sprites.LoadSprite("eureka_icon.png", source.icon);
			if (icon != null)
			{
				map.icon = icon;
			}
			return map;
		}
	}
}
