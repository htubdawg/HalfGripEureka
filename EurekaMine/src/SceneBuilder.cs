using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EurekaMine
{
	/// <summary>A line of text on a sign (TextMeshPro, the game's pixel font).</summary>
	internal sealed class Label
	{
		public Vector3 Pos;
		public Vector3 Facing;
		public string Text;
		public Color Color;
		public Vector2 Size;
	}

	/// <summary>
	/// Turns the Stadium scene into Eureka Mine when one of its races loads. Runs on <c>sceneLoaded</c>: after every Awake and before any
	/// Start, so the lap counter, the grid, the bots and the reset logic all find the new roads.
	/// </summary>
	internal static class SceneBuilder
	{
		internal static readonly List<Label> Labels = new List<Label>();

		private static GameObject root;
		private static readonly List<GameObject> hidden = new List<GameObject>();
		private static PhysicsMaterial frictionless;

		/// <summary>Samples between two waypoints of a bot path, and the waypoint spacing: the bots aim about 30 units ahead (see ProcTrack's BuildBotPath).</summary>
		private const int BotSamples = 2;
		private const float WaypointStep = 20f;

		internal static bool Active
		{
			get { return root != null; }
		}

		internal static void OnSceneLoaded(Scene scene)
		{
			root = null;
			hidden.Clear();
			RunIn.Reset();
			if (Mod.Map == null || RunState.ActiveMap != Mod.Map)
			{
				return;
			}
			TrackManager tm = UnityEngine.Object.FindFirstObjectByType<TrackManager>();
			if (tm == null || tm.playerCarController == null || tm.lapManager == null || tm.lapManager.trackLine == null)
			{
				return;
			}
			try
			{
				float t0 = Time.realtimeSinceStartup;
				Build(scene, tm);
				Mod.Log.LogInfo("Eureka Mine built in " + ((Time.realtimeSinceStartup - t0) * 1000f).ToString("0") + " ms: lap " + Route.Main.Length.ToString("0") + " units, run in " + Route.Runin.Length.ToString("0"));
			}
			catch (Exception e)
			{
				Mod.Log.LogError("Eureka Mine could not be built, the Stadium is put back: " + e);
				Unload();
			}
		}

		internal static void Unload()
		{
			if (root != null)
			{
				UnityEngine.Object.Destroy(root);
			}
			foreach (GameObject go in hidden)
			{
				if (go != null)
				{
					go.SetActive(true);
				}
			}
			hidden.Clear();
			root = null;
			RunIn.Reset();
		}

		private static void Hide(GameObject go)
		{
			if (go != null && go.activeSelf)
			{
				go.SetActive(false);
				hidden.Add(go);
			}
		}

		private static void Build(Scene scene, TrackManager tm)
		{
			GameObject env = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "ENVIROMENT");
			if (env == null)
			{
				throw new InvalidOperationException("this scene has no ENVIROMENT object, so it is not the Stadium scene");
			}
			Transform ground = env.transform.Find("Ground");
			int layer = ground != null ? ground.gameObject.layer : 0;
			Material source = ground != null && ground.TryGetComponent(out MeshRenderer groundRenderer) ? groundRenderer.sharedMaterial : null;
			foreach (string name in new[] { "Ground", "Track", "Decoration" })
			{
				Transform t = env.transform.Find(name);
				if (t != null)
				{
					Hide(t.gameObject);
				}
			}
			Transform rain = env.transform.Find("Weather/ParticleColliderPlane");
			if (rain != null && rain.TryGetComponent(out Collider rainCollider))
			{
				rainCollider.enabled = false;
			}
			// the Stadium's own hazard spots are round its old track: none of the game's hazards are dealt here
			foreach (HazardsSpawner_FINAL spawner in UnityEngine.Object.FindObjectsByType<HazardsSpawner_FINAL>(FindObjectsSortMode.None))
			{
				Hide(spawner.gameObject);
			}

			Tex.Make();
			Mats.Reset(source);
			Labels.Clear();
			root = new GameObject("EurekaMine");

			Dictionary<string, Batch> batches = new Dictionary<string, Batch>();
			Mine.Build(batches);
			Pithead.Build(batches);
			int triangles = 0, meshes = 0;
			foreach (Batch batch in batches.Values)
			{
				if (batch.Count > 0)
				{
					triangles += Spawn(batch, root.transform, layer);
					meshes++;
				}
			}
			BuildLabels(layer);
			Mod.Info("Eureka Mine: " + meshes + " meshes, " + triangles + " triangles, " + Mine.Lamps.Count + " lamps");

			PathManager line = Rewire(tm);
			PlaceGrid(tm, env);

			GameObject logic = new GameObject("Mine logic") { layer = layer };
			logic.transform.SetParent(root.transform, false);
			logic.AddComponent<Underground>().Setup(tm);
			logic.AddComponent<LampLights>().Setup(Mod.Lights.Value);
			RunIn tracker = logic.AddComponent<RunIn>();
			tracker.Setup(tm, line, root.transform, layer);
			Hazards.Build(root.transform, layer, tm);
		}

		/// <summary>No friction and no bounce: a car touching a wall slides along it instead of catching.</summary>
		private static PhysicsMaterial Frictionless
		{
			get
			{
				if (frictionless == null)
				{
					frictionless = new PhysicsMaterial("EurekaFrictionless")
					{
						dynamicFriction = 0f,
						staticFriction = 0f,
						bounciness = 0f,
						frictionCombine = PhysicsMaterialCombine.Minimum,
						bounceCombine = PhysicsMaterialCombine.Minimum
					};
				}
				return frictionless;
			}
		}

		/// <summary>
		/// Size of the cells the batches are cut into (see <see cref="Batch.ToMeshes"/>). 60, not 120 (0.4.0): a 120-unit stretch of
		/// gangway was in reach of six or seven lamps, more than the renderer lights one object with; at 60 nearly all are in reach of
		/// six or fewer, which leaves room for the headlights.
		/// </summary>
		private const float Cell = 60f;

		/// <summary>Makes the objects (one per cell), renderers, colliders and surface behaviour of a batch. Returns its triangle count.</summary>
		private static int Spawn(Batch batch, Transform parent, int layer)
		{
			int triangles = 0;
			Material material = Mats.Get(batch.Tex, batch.Tint, batch.TwoSided, batch.Emission, batch.Smooth);
			foreach (Mesh mesh in batch.ToMeshes(Cell))
			{
				GameObject go = new GameObject(batch.Name) { layer = layer };
				go.transform.SetParent(parent, false);
				if (!batch.Invisible)
				{
					go.AddComponent<MeshFilter>().sharedMesh = mesh;
					MeshRenderer mr = go.AddComponent<MeshRenderer>();
					mr.sharedMaterial = material;
					mr.shadowCastingMode = batch.Shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
					mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
					mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
				}
				if (batch.Collide)
				{
					MeshCollider mc = go.AddComponent<MeshCollider>();
					mc.sharedMesh = mesh;
					if (batch.Slippery)
					{
						mc.sharedMaterial = Frictionless;
					}
				}
				if (batch.Wall)
				{
					try
					{
						go.tag = "Wall";
					}
					catch (Exception)
					{
						Mod.Info("The tag Wall does not exist: walls keep the default tag.");
					}
				}
				switch (batch.Ground)
				{
					case Surface.Rough:
						AddHandling(go, Mod.RoughGrip.Value, Mod.RoughDrag.Value, true);
						break;
					case Surface.Coal:
						AddHandling(go, Mod.CoalGrip.Value, Mod.CoalDrag.Value, true);
						break;
					case Surface.Yard:
						AddHandling(go, Mod.YardGrip.Value, Mod.YardDrag.Value, true);
						break;
				}
				triangles += mesh.triangles.Length / 3;
			}
			return triangles;
		}

		private static void AddHandling(GameObject go, float grip, float drag, bool background)
		{
			HandlingModifier handling = go.AddComponent<HandlingModifier>();
			handling.gripModifier = grip;
			handling.resistanceModifier = drag;
			handling.background = background;
		}

		private static void BuildLabels(int layer)
		{
			if (Labels.Count == 0)
			{
				return;
			}
			TMP_FontAsset font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(f => f.name.Contains("m6x11"))
				?? Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault();
			if (font == null)
			{
				Mod.Log.LogWarning("No TextMeshPro font found: the signs stay blank.");
				return;
			}
			foreach (Label l in Labels)
			{
				GameObject go = new GameObject("Sign text") { layer = layer };
				go.transform.SetParent(root.transform, false);
				// TextMeshPro reads from its -Z side, so it looks away from the side it is read from
				go.transform.SetPositionAndRotation(l.Pos, Quaternion.LookRotation(-l.Facing, Vector3.up));
				TextMeshPro text = go.AddComponent<TextMeshPro>();
				text.font = font;
				text.text = l.Text;
				text.color = l.Color;
				text.alignment = TextAlignmentOptions.Center;
				text.rectTransform.sizeDelta = l.Size;
				text.enableAutoSizing = true;
				text.fontSizeMin = 1f;
				text.fontSizeMax = 200f;
				text.overflowMode = TextOverflowModes.Overflow;
			}
		}

		// ---- the lap line, the bots' paths, the grid -------------------------------------------------------------------------

		/// <summary>Points of a road from index a to b (inclusive, wrapping on a closed road).</summary>
		private static IEnumerable<Vector3> Span(Road r, int a, int b)
		{
			int n = r.Count;
			int count = r.Closed ? ((b - a) % n + n) % n + 1 : b - a + 1;
			for (int k = 0; k < count; k++)
			{
				yield return r.At(a + k);
			}
		}

		/// <summary>A whole lap as points, taking the given branches instead of the main lap between their fork and join.</summary>
		internal static List<Vector3> Lap(bool seam, bool chute, out List<float> halfWidths)
		{
			List<Vector3> pts = new List<Vector3>();
			halfWidths = new List<float>();
			Road m = Route.Main;
			int i = 0;
			while (i < m.Count)
			{
				Road branch = seam && i == Route.Seam.Fork ? Route.Seam : chute && i == Route.Chute.Fork ? Route.Chute : null;
				if (branch != null)
				{
					for (int k = 0; k < branch.Count; k++)
					{
						pts.Add(branch.P[k]);
						halfWidths.Add(branch.HalfW[k]);
					}
					i = branch.Join + 1;
					continue;
				}
				pts.Add(m.P[i]);
				halfWidths.Add(m.HalfW[i]);
				i++;
			}
			return pts;
		}

		/// <summary>The points thinned to one every step units (evenly, so the game's spline does not kink), as indexes.</summary>
		private static List<int> Thin(List<Vector3> d, float step, bool closed)
		{
			List<int> keep = new List<int> { 0 };
			float since = 0f;
			for (int i = 1; i < d.Count; i++)
			{
				since += (d[i] - d[i - 1]).magnitude;
				if (since >= step)
				{
					keep.Add(i);
					since = 0f;
				}
			}
			while (closed && keep.Count > 4 && Vector3.Distance(d[keep[keep.Count - 1]], d[0]) < step * 0.4f)
			{
				keep.RemoveAt(keep.Count - 1);
			}
			return keep;
		}

		private static void AddWaypoint(Transform parent, string name, Vector3 at, float right, float left)
		{
			GameObject point = new GameObject(name);
			point.transform.SetParent(parent, true);
			point.transform.position = at;
			// x: the room to the right of the point, z: to its left (PathManager.SampleControlPoints); y is the grip, left at 1
			point.transform.localScale = new Vector3(right, 1f, left);
			point.AddComponent<PathControlPointEditorController>();
		}

		/// <summary>Puts waypoints on a path (its old ones removed), samples it and bakes its lanes.</summary>
		internal static void Fill(PathManager path, List<Vector3> pts, List<float> half, float edgeKeep, int samples, bool closed)
		{
			foreach (Transform child in path.transform.Cast<Transform>().ToList())
			{
				child.SetParent(null);
				UnityEngine.Object.Destroy(child.gameObject);
			}
			List<int> keep = Thin(pts, WaypointStep, closed);
			for (int k = 0; k < keep.Count; k++)
			{
				float room = Mathf.Max(1f, half[keep[k]] - edgeKeep);
				AddWaypoint(path.transform, "EM" + (k + 1), pts[keep[k]] + Vector3.up * 0.5f, room, room);
			}
			path.gameObject.SetActive(true);
			path.enabled = true;
			if (samples > 0)
			{
				Traverse.Create(path).Field("samplesPerSegment").SetValue(samples);
			}
			AccessTools.Method(typeof(PathManager), "InitializePath").Invoke(path, null);
			Traverse.Create(path).Field("snapLanesToGround").SetValue(false);
			// drops look for the ground by a ray from this high above the line: in the mine a higher one would find the level above
			Traverse.Create(path).Field("snapRayHeight").SetValue(8f);
			path.BakeRacingLine();
			if (Mod.ShowPath.Value && path.TryGetComponent(out LineRenderer renderer))
			{
				renderer.enabled = true;
			}
		}

		/// <summary>
		/// The lap counter's line is the main lap. The bots get four paths, one for each way round (the main lap, the upper seam, the
		/// coal chute, both), and the game deals each bot one of them, so the field spreads over the alternate routes. Returns the lap line.
		/// </summary>
		private static PathManager Rewire(TrackManager tm)
		{
			LapManager lap = tm.lapManager;
			PathManager line = lap.trackLine;
			List<Vector3> main = Lap(false, false, out List<float> mainHalf);
			Fill(line, main, mainHalf, 3f, 0, true);

			PathManager template = lap.enemyTrackLines?.FirstOrDefault(p => p != null && p != line);
			List<PathManager> bots = new List<PathManager>();
			bool[][] ways = { new[] { false, false }, new[] { true, false }, new[] { false, true }, new[] { true, true } };
			foreach (bool[] way in ways)
			{
				PathManager path;
				if (template != null)
				{
					path = bots.Count == 0 ? template : UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent).GetComponent<PathManager>();
					if (bots.Count > 0)
					{
						path.gameObject.name = "EurekaPath" + (bots.Count + 1);
						path.transform.SetParent(root.transform, true);
					}
				}
				else
				{
					path = UnityEngine.Object.Instantiate(line.gameObject, root.transform).GetComponent<PathManager>();
				}
				List<Vector3> pts = Lap(way[0], way[1], out List<float> half);
				Fill(path, pts, half, 3f, BotSamples, true);
				bots.Add(path);
			}
			lap.enemyTrackLines = bots;
			foreach (PathManager other in UnityEngine.Object.FindObjectsByType<PathManager>(FindObjectsSortMode.None))
			{
				if (other != line && !bots.Contains(other))
				{
					Hide(other.gameObject);
				}
			}

			// several levels share the same ground plan: the lap counter and the bots follow each car from where it last was
			tm.allowFigureEightTracks = true;
			// a reset looks down for the road from just above the line, or it would land on the level above
			tm.resetRayHeight = 8f;
			tm.resetRayLength = 40f;

			TrackScatter scatter = UnityEngine.Object.FindFirstObjectByType<TrackScatter>();
			if (scatter != null)
			{
				scatter.path = line;
			}
			Mod.Info("Lap line: " + line.sampledPoints.Count + " samples, " + line.trackLength.ToString("0") + " units; bot paths: " + string.Join(", ", bots.Select(b => b.trackLength.ToString("0"))));
			return line;
		}

		/// <summary>Seven rivals; the Stadium has five grid slots, so two are added to its list (as ProcTrack does).</summary>
		private const int Rivals = 7;

		/// <summary>
		/// The grid goes in the pithead yard, two by two behind the drift mouth, heading +X as the Stadium's own grid does. The rivals are
		/// spawned one per slot in TrackManager's list (read in its Awake, spawned in its Start, and this runs between).
		/// </summary>
		private static void PlaceGrid(TrackManager tm, GameObject env)
		{
			Transform container = tm.startingPositionsContainer.transform;
			Transform playerSlot = Traverse.Create(tm).Field("playerStart").GetValue<Transform>() ?? container.Cast<Transform>().FirstOrDefault(t => t.name == "PlayerPosition");
			CarControllerNew car = tm.playerCarController;
			float lift = playerSlot != null ? Mathf.Clamp(car.transform.position.y - playerSlot.position.y, 0.2f, 3f) : 0.5f;
			List<Transform> slots = Traverse.Create(tm).Field("startPositions").GetValue<List<Transform>>();
			if (slots == null)
			{
				slots = container.Cast<Transform>().Where(t => t != playerSlot && t.gameObject.activeInHierarchy).ToList();
			}
			else
			{
				while (slots.Count < Rivals && slots.Count > 0)
				{
					Transform added = UnityEngine.Object.Instantiate(slots[0].gameObject, container).transform;
					added.name = "EurekaSlot" + slots.Count;
					slots.Add(added);
				}
			}

			Road r = Route.Runin;
			Vector3 front = r.At(RouteData.RuninMouth - 6);
			Vector3 fwd = Vector3.right;
			Vector3 side = Vector3.forward;
			for (int k = 0; k < slots.Count; k++)
			{
				slots[k].position = new Vector3(front.x - 26f * k, 0f, front.z + (k % 2 == 0 ? 9f : -9f));
			}
			Vector3 playerAt = new Vector3(front.x - 26f * slots.Count, 0f, front.z + (slots.Count % 2 == 0 ? 9f : -9f));
			if (playerSlot != null)
			{
				playerSlot.position = playerAt;
			}

			Transform ct = car.transform;
			AudioListener listener = car.GetComponentInChildren<AudioListener>(true);
			bool listenerOn = listener != null && listener.enabled;
			if (listenerOn)
			{
				listener.enabled = false;
			}
			Vector3 target = playerAt + Vector3.up * lift;
			ct.SetPositionAndRotation(target, Quaternion.LookRotation(fwd, Vector3.up));
			Rigidbody body = car.rb != null ? car.rb : car.GetComponent<Rigidbody>();
			if (body != null)
			{
				body.position = target;
				body.rotation = Quaternion.LookRotation(fwd, Vector3.up);
				body.linearVelocity = Vector3.zero;
				body.angularVelocity = Vector3.zero;
			}
			if (listenerOn)
			{
				listener.enabled = true;
			}
			Physics.SyncTransforms();

			Transform semaphore = env.transform.Find("RaceStarterSemaphore");
			if (semaphore != null)
			{
				semaphore.position = new Vector3(front.x + 30f, semaphore.position.y, front.z + side.z * 25f);
			}
			Mod.Info("Grid: " + slots.Count + " rivals and the player in the yard, front row at x " + front.x.ToString("0"));
		}
	}
}
