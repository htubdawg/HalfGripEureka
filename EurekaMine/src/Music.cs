using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace EurekaMine
{
	/// <summary>
	/// The map's race song, "Last Exit" (the owner's), from <c>music/Last Exit.ogg</c> in the mod's folder. The game plays a map's
	/// <c>MapDefinition.musicTrack</c> for its races (<c>RunState.BeginRace</c> picks it, <c>TrackManager.PlayRaceMusic</c> plays it) and
	/// a random race song when it is empty; HalfGripTools has no audio loader, so the file is read with Unity's own.
	/// </summary>
	internal static class Music
	{
		internal const string SongName = "Last Exit";

		/// <summary>The song, once loaded; null until then or if it could not be read (the Stadium's song stays).</summary>
		internal static AudioClip Clip;

		private static bool started;

		/// <summary>Starts reading the song in the background. It is ready long before a race can start, and the map entry takes it when it is.</summary>
		internal static void Load()
		{
			if (started)
			{
				return;
			}
			started = true;
			string path = Path.Combine(Mod.Ctx.Folder, "music", SongName + ".ogg");
			if (!File.Exists(path))
			{
				Mod.Log.LogWarning("Race music not found at " + path + ": the map plays the game's own songs.");
				return;
			}
			try
			{
				UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, AudioType.OGGVORBIS);
				// kept as Vorbis in memory (a few MB) rather than decoded to PCM, and decoded whole so the loop is seamless
				((DownloadHandlerAudioClip)request.downloadHandler).compressed = true;
				((DownloadHandlerAudioClip)request.downloadHandler).streamAudio = false;
				request.SendWebRequest().completed += _ => Done(request, path);
			}
			catch (Exception e)
			{
				Mod.Log.LogError("Could not start reading the race music: " + e);
			}
		}

		private static void Done(UnityWebRequest request, string path)
		{
			try
			{
				if (request.result != UnityWebRequest.Result.Success)
				{
					Mod.Log.LogWarning("Could not read the race music " + path + ": " + request.error);
					return;
				}
				AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
				if (clip == null || clip.length <= 0f)
				{
					Mod.Log.LogWarning("The race music " + path + " is empty or not Ogg Vorbis.");
					return;
				}
				// the clip's name is the song's name, as the game's own are ("Forest OST"); kept through scene changes' asset unloading
				clip.name = SongName;
				clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
				Clip = clip;
				if (Mod.Map != null)
				{
					Mod.Map.musicTrack = clip;
				}
				Mod.Log.LogInfo("Race music loaded: \"" + SongName + "\", " + clip.length.ToString("0.0") + " s, " + clip.frequency + " Hz, " + clip.channels + " channels");
			}
			catch (Exception e)
			{
				Mod.Log.LogError("Could not load the race music: " + e);
			}
			finally
			{
				request.Dispose();
			}
		}
	}
}
