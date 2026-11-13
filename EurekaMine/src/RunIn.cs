using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace EurekaMine
{
	/// <summary>
	/// The run in: the race starts in the pithead yard and drives down the slope once, joining the lap just before the start line, and
	/// every lap after that stays underground. The run in is not part of the closed lap, so the game cannot count it: until a car reaches
	/// the lap, the lap counter is told it is on lap -1 that far behind the line (positions come out right), a reset puts it back on the
	/// slope, and a bot drives a path of its own down the slope and is handed its lap path at the bottom. Once the field is through, a
	/// timber gate shuts across the bottom of the slope so nobody turns up it later.
	/// </summary>
	internal sealed class RunIn : MonoBehaviour
	{
		internal static RunIn Instance;

		private static readonly HashSet<GameObject> released = new HashSet<GameObject>();
		private static readonly Dictionary<EnemyController, PathManager> lapPaths = new Dictionary<EnemyController, PathManager>();

		private TrackManager tm;
		private PathManager line;
		private PathManager slopePath;
		/// <summary>The lap line's sample where the slope joins, and the distance along the lap line to it.</summary>
		private int mergeSample;
		private float mergeS;
		private GameObject gate;
		private float gateClosed = -1f;

		internal static void Reset()
		{
			Instance = null;
			released.Clear();
			lapPaths.Clear();
		}

		internal void Setup(TrackManager track, PathManager lapLine, Transform parent, int layer)
		{
			Instance = this;
			tm = track;
			line = lapLine;
			Road m = Route.Main;
			mergeSample = line.GetClosestPointIndex(m.At(Route.Runin.Join));
			mergeS = line.segmentLengths.TryGetValue(mergeSample, out float s) ? s : 0f;

			// the bots' path down the slope: the run in and the first stretch of the lap, closed back to the yard (never driven that far)
			PathManager template = tm.lapManager.enemyTrackLines != null && tm.lapManager.enemyTrackLines.Count > 0 ? tm.lapManager.enemyTrackLines[0] : line;
			slopePath = Instantiate(template.gameObject, parent).GetComponent<PathManager>();
			slopePath.gameObject.name = "EurekaSlopePath";
			List<Vector3> pts = new List<Vector3>(Route.Runin.P);
			List<float> half = new List<float>();
			foreach (Vector3 unused in Route.Runin.P)
			{
				half.Add(Route.Runin.Half);
			}
			for (int k = 1; k <= 50; k++)
			{
				pts.Add(m.At(Route.Runin.Join + k));
				half.Add(m.Half);
			}
			SceneBuilder.Fill(slopePath, pts, half, 3f, 2, true);
			// it must not be dealt to anybody as a lap path
			tm.lapManager.enemyTrackLines.Remove(slopePath);

			gate = BuildGate(parent, layer);
			Mod.Info("Run in: joins the lap at its sample " + mergeSample + " (" + mergeS.ToString("0") + " of " + line.trackLength.ToString("0") + " units)");
		}

		/// <summary>True while a car is still on its way down from the yard (or the race has no run in).</summary>
		internal static bool OnRunIn(GameObject car)
		{
			return Instance != null && car != null && !released.Contains(car);
		}

		/// <summary>How far down the run in a point is, and how far it is from the run in's middle line.</summary>
		private static float Along(Vector3 p, out float off, out int index)
		{
			Road r = Route.Runin;
			index = r.Nearest(p, out float sqr);
			off = Mathf.Sqrt(sqr);
			return r.S[index];
		}

		/// <summary>
		/// For the lap counter (a prefix on its per-car update): a car on the run in is on lap -1, the rest of the slope and the stretch of
		/// lap before the line behind it. False when the car was handled here.
		/// </summary>
		internal static bool Project(LapManager lap, GameObject car)
		{
			RunIn self = Instance;
			if (!OnRunIn(car) || lap.leaderboard == null || !lap.leaderboard.TryGetValue(car, out LapManager.LeaderboardEntry entry) || entry.isFinished)
			{
				return true;
			}
			Road r = Route.Runin;
			float along = Along(car.transform.position, out float off, out int index);
			float remaining = Mathf.Max(0f, r.Length - along);
			// the bottom of the slope, or a car put somewhere else entirely (a reset that found the lap): it is on the lap from now on
			bool arrived = index >= r.Count - 3 || off > r.Half + 30f;
			entry.currentLap = -1;
			entry.backwardsLaps = 0;
			entry.justStarted = true;
			entry.totalDistance = self.mergeS - self.line.trackLength - remaining;
			entry.lapProgress = 0f;
			entry.projectedPoint = r.P[index];
			entry.lapTime += Time.deltaTime;
			if (lap.raceStarted)
			{
				entry.totalTime += Time.deltaTime;
			}
			lap.leaderboard[car] = entry;
			if (arrived)
			{
				released.Add(car);
				Dictionary<GameObject, int> last = Traverse.Create(lap).Field("carLastClosestIndex").GetValue<Dictionary<GameObject, int>>();
				if (last != null)
				{
					last[car] = self.line.GetClosestPointIndex(car.transform.position, self.mergeSample);
				}
				Mod.Info(car.name + " reached the lap from the run in");
			}
			return false;
		}

		/// <summary>For the player's reset (a prefix): on the run in, the car goes back onto the slope a little behind where it was. False when handled.</summary>
		internal static bool ResetPlayer(CarControllerNew player)
		{
			if (player == null || !OnRunIn(player.gameObject))
			{
				return true;
			}
			Road r = Route.Runin;
			Along(player.transform.position, out float off, out int index);
			if (off > r.Half + 30f)
			{
				return true;
			}
			int at = Mathf.Max(1, index - 2);
			Vector3 pos = r.P[at] + Vector3.up * 2f;
			Quaternion rot = Quaternion.LookRotation(r.Forward(at), Vector3.up);
			player.transform.SetPositionAndRotation(pos, rot);
			Rigidbody body = player.rb != null ? player.rb : player.GetComponent<Rigidbody>();
			if (body != null)
			{
				body.position = pos;
				body.rotation = rot;
				body.linearVelocity = Vector3.zero;
				body.angularVelocity = Vector3.zero;
			}
			return false;
		}

		/// <summary>After the rivals are spawned: each keeps the lap path it was dealt for later and drives the slope path first.</summary>
		internal static void TakeOverBots(TrackManager track)
		{
			RunIn self = Instance;
			if (self == null || self.slopePath == null)
			{
				return;
			}
			foreach (EnemyController enemy in track.Enemies)
			{
				if (enemy == null || lapPaths.ContainsKey(enemy))
				{
					continue;
				}
				lapPaths[enemy] = enemy.pathManager;
				enemy.pathManager = self.slopePath;
			}
		}

		private void FixedUpdate()
		{
			// a bot that reached the lap gets its lap path back
			if (lapPaths.Count > 0)
			{
				List<EnemyController> done = null;
				foreach (KeyValuePair<EnemyController, PathManager> kv in lapPaths)
				{
					if (kv.Key == null || released.Contains(kv.Key.gameObject))
					{
						if (kv.Key != null)
						{
							kv.Key.pathManager = kv.Value;
							Traverse.Create(kv.Key).Field("lastSearchIndex").SetValue(-1);
						}
						(done ??= new List<EnemyController>()).Add(kv.Key);
					}
				}
				if (done != null)
				{
					foreach (EnemyController e in done)
					{
						lapPaths.Remove(e);
					}
				}
			}
			// the gate shuts once every car is on the lap (or a minute in, for one that never comes down)
			if (gate != null && gateClosed < 0f && tm != null && tm.raceStarted)
			{
				bool all = !OnRunIn(tm.playerCarController.gameObject);
				foreach (EnemyController e in tm.Enemies)
				{
					all &= e == null || !OnRunIn(e.gameObject);
				}
				if (all || tm.TimeSinceRaceStart > 60f)
				{
					gateClosed = Time.time;
				}
			}
			if (gate != null && gateClosed >= 0f)
			{
				float f = Mathf.Clamp01((Time.time - gateClosed) / 2.5f);
				foreach (Transform leaf in gate.transform)
				{
					if (leaf.name.StartsWith("leaf"))
					{
						float side = leaf.name.EndsWith("L") ? 1f : -1f;
						leaf.localRotation = Quaternion.Euler(0f, side * Mathf.Lerp(88f, 0f, f), 0f);
					}
				}
			}
		}

		/// <summary>Two timber doors at the bottom of the slope, standing open against the walls until the field is through.</summary>
		private static GameObject BuildGate(Transform parent, int layer)
		{
			Road r = Route.Runin;
			// far enough up that the run in no longer touches the lap's road (its middle is 55 units from the lap's here)
			int at = r.Count - 22;
			Vector3 p = r.At(at);
			Vector3 f = r.Forward(at);
			GameObject g = new GameObject("Slope gate") { layer = layer };
			g.transform.SetParent(parent, false);
			g.transform.SetPositionAndRotation(p, Quaternion.LookRotation(f, Vector3.up));
			float w = r.Half;
			foreach (float side in new[] { -1f, 1f })
			{
				// a leaf hinged at its wall (side -1 left, +1 right), its planks reaching toward the middle of the road
				float dir = -side;
				Batch planks = new Batch("gate", Tex.Timber, new Color(0.7f, 0.6f, 0.5f));
				planks.Box(new Vector3(dir * w * 0.5f, Mine.WallTop * 0.5f, 0f), new Vector3(w, Mine.WallTop, 1.2f), Quaternion.identity, 4f);
				for (float y = 2f; y < Mine.WallTop; y += 5f)
				{
					planks.Box(new Vector3(dir * w * 0.5f, y, -0.8f), new Vector3(w - 1f, 1f, 0.6f), Quaternion.identity, 4f);
				}
				planks.Box(new Vector3(dir * w * 0.5f, Mine.WallTop * 0.5f, -0.8f), new Vector3(w * 1.05f, 1f, 0.6f), Quaternion.Euler(0f, 0f, dir * Mathf.Atan2(Mine.WallTop, w) * Mathf.Rad2Deg), 4f);
				GameObject hinge = new GameObject(side < 0f ? "leafL" : "leafR") { layer = layer };
				hinge.transform.SetParent(g.transform, false);
				hinge.transform.localPosition = new Vector3(side * w, 0f, 0f);
				hinge.AddComponent<MeshFilter>().sharedMesh = planks.ToMesh();
				hinge.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get(planks.Tex, planks.Tint, false, Color.black);
				BoxCollider box = hinge.AddComponent<BoxCollider>();
				box.center = new Vector3(dir * w * 0.5f, Mine.WallTop * 0.5f, 0f);
				box.size = new Vector3(w, Mine.WallTop, 1.6f);
				// open: swung back against the wall, uphill
				hinge.transform.localRotation = Quaternion.Euler(0f, (side < 0f ? 1f : -1f) * 88f, 0f);
			}
			return g;
		}
	}

	[HarmonyPatch(typeof(LapManager), "GetCarProjectedPoint")]
	internal static class RunInLapPatch
	{
		private static bool Prefix(LapManager __instance, GameObject car)
		{
			return RunIn.Project(__instance, car);
		}
	}

	[HarmonyPatch(typeof(PlayerResetLogic), nameof(PlayerResetLogic.ResetOnTrack))]
	internal static class RunInResetPatch
	{
		private static bool Prefix(PlayerResetLogic __instance)
		{
			if (RunIn.Instance == null)
			{
				return true;
			}
			return RunIn.ResetPlayer(Traverse.Create(__instance).Field("player").GetValue<CarControllerNew>());
		}
	}

	[HarmonyPatch(typeof(TrackManager), "SpawnEnemies")]
	internal static class RunInSpawnPatch
	{
		private static void Postfix(TrackManager __instance)
		{
			RunIn.TakeOverBots(__instance);
		}
	}
}
