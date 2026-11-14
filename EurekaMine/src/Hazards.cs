using System.Collections.Generic;
using UnityEngine;

namespace EurekaMine
{
	/// <summary>
	/// The mine's own hazards (none of the game's are used here): runaway mine cars loose on the plane, chutes over the lower gangway
	/// dumping lump coal, and ventilation doors swinging shut across the coal chute. All of them are machinery: nobody is about.
	/// </summary>
	internal static class Hazards
	{
		internal static void Build(Transform parent, int layer, TrackManager tm)
		{
			Batch body = new Batch("mine car", Tex.Corrugated, new Color(0.55f, 0.38f, 0.28f));
			Batch coal = new Batch("mine car coal", Tex.Coal, Color.white);
			MineCarMesh(body, coal);
			if (Mod.RunawayCars.Value)
			{
				GameObject go = new GameObject("Runaway cars") { layer = layer };
				go.transform.SetParent(parent, false);
				go.AddComponent<RunawayCars>().Setup(tm, body.ToMesh(), coal.ToMesh(), Mats.Get(body.Tex, body.Tint, false, Color.black), Mats.Get(coal.Tex, coal.Tint, false, Color.black), layer);
			}
			if (Mod.CoalChutes.Value)
			{
				GameObject go = new GameObject("Coal chutes") { layer = layer };
				go.transform.SetParent(parent, false);
				go.AddComponent<CoalChutes>().Setup(tm, layer);
			}
			if (Mod.VentDoors.Value)
			{
				GameObject go = new GameObject("Ventilation doors") { layer = layer };
				go.transform.SetParent(parent, false);
				go.AddComponent<VentDoors>().Setup(layer);
			}
		}

		/// <summary>A mine car about two thirds of a race car's length: a steel tub on a frame, heaped with coal, built facing +Z.</summary>
		private static void MineCarMesh(Batch body, Batch coal)
		{
			body.Box(new Vector3(0f, 4.2f, 0f), new Vector3(6.5f, 4.4f, 9f), Quaternion.identity, 3f);
			body.Box(new Vector3(0f, 1.6f, 0f), new Vector3(5.2f, 1.2f, 9.6f), Quaternion.identity, 3f);
			foreach (float x in new[] { -2.6f, 2.6f })
			{
				foreach (float z in new[] { -3.2f, 3.2f })
				{
					body.Cylinder(new Vector3(x - 0.4f, 1.2f, z), new Vector3(x + 0.4f, 1.2f, z), 1.2f, 8, 2f, true);
				}
			}
			coal.Heap(new Vector3(0f, 6.4f, 0f), 3.4f, 1.8f, 8, 2, 0f, null, 2f);
		}

		/// <summary>A car's body from one of its colliders, and how fast it goes in dashboard units (as the speedometer reads).</summary>
		internal static bool Car(Collider c, out Rigidbody body, out float dash)
		{
			body = c.attachedRigidbody;
			dash = 0f;
			if (body == null)
			{
				return false;
			}
			CarControllerNew car = body.GetComponent<CarControllerNew>();
			if (car == null)
			{
				return false;
			}
			dash = Mathf.Abs(car.forwardSpeed) * SpeedDashboard.SpeedToDisplay;
			return true;
		}
	}

	/// <summary>
	/// A string of three loaded mine cars stands in a heading off the head of the plane. Now and then (every 14 to 22 seconds while the
	/// player is near) it breaks loose: it rolls out on its rails, swings onto the haulage's track down the middle of the plane, runs
	/// down faster and faster, and at the foot turns off into the other heading and brakes hard against the bumper. It is put back in
	/// the top heading only when no camera can see it and the player is well away. The cars are kinematic: they shove what they meet.
	/// (0.2.0 ran them in a lane beside the rails and made them vanish at the foot.)
	/// </summary>
	internal sealed class RunawayCars : MonoBehaviour
	{
		private const float Spacing = 11f;
		private readonly List<Rigidbody> cars = new List<Rigidbody>();
		private readonly List<Renderer> renderers = new List<Renderer>();
		private readonly List<Vector3> path = new List<Vector3>();
		private readonly List<float> along = new List<float>();
		private TrackManager tm;
		private float wait = 6f;
		private int state;   // 0 parked at the top, 1 running, 2 stopped at the bumper
		private float travelled, speed;
		private int runs;
		private Vector3 top, bottom;

		internal void Setup(TrackManager track, Mesh body, Mesh load, Material bodyMat, Material loadMat, int layer)
		{
			tm = track;
			BuildPath();
			Batch steel = new Batch("runaway rails", Tex.Steel, new Color(0.6f, 0.6f, 0.62f));
			Batch wood = new Batch("runaway ties", Tex.Timber, new Color(0.6f, 0.5f, 0.42f));
			LayRails(steel, wood);
			foreach (Batch b in new[] { steel, wood })
			{
				GameObject go = new GameObject(b.Name) { layer = layer };
				go.transform.SetParent(transform, false);
				go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh();
				go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get(b.Tex, b.Tint, false, Color.black);
			}
			for (int k = 0; k < 3; k++)
			{
				GameObject go = new GameObject("mine car " + k) { layer = layer };
				go.transform.SetParent(transform, false);
				go.AddComponent<MeshFilter>().sharedMesh = body;
				renderers.Add(go.AddComponent<MeshRenderer>());
				renderers[renderers.Count - 1].sharedMaterial = bodyMat;
				GameObject topLoad = new GameObject("load") { layer = layer };
				topLoad.transform.SetParent(go.transform, false);
				topLoad.AddComponent<MeshFilter>().sharedMesh = load;
				renderers.Add(topLoad.AddComponent<MeshRenderer>());
				renderers[renderers.Count - 1].sharedMaterial = loadMat;
				BoxCollider box = go.AddComponent<BoxCollider>();
				box.center = new Vector3(0f, 3.8f, 0f);
				box.size = new Vector3(6.5f, 6.4f, 9.6f);
				Rigidbody rb = go.AddComponent<Rigidbody>();
				rb.isKinematic = true;
				rb.interpolation = RigidbodyInterpolation.Interpolate;
				cars.Add(rb);
			}
			Park();
		}

		/// <summary>
		/// The cars' way: from the back of the top heading out to its mouth, a curve onto the middle of the plane, down it, a curve into
		/// the bottom heading and on to its bumper. Without the headings (no room), straight down the plane's middle.
		/// </summary>
		private void BuildPath()
		{
			Road r = Route.Main;
			Mine.Niche head = Mine.Niches.Find(nc => nc.Road == r && nc.Kind == 400);
			Mine.Niche foot = Mine.Niches.Find(nc => nc.Road == r && nc.Kind == 401);
			int k0 = head != null ? head.I + head.Span + 4 : Route.Mark("Plane head") - 2;
			int k1 = foot != null ? foot.I - 4 : Route.Mark("Plane foot") + 4;
			if (head != null)
			{
				Vector3 back = head.Mouth + head.Out * (head.Depth - 4f);
				Add(back);
				Add(head.Mouth);
				Curve(head.Mouth, head.Mouth - head.Out * 14f, r.At(k0) - r.Forward(k0) * 16f, r.At(k0));
			}
			for (int k = k0; k <= k1; k++)
			{
				Add(r.At(k));
			}
			if (foot != null)
			{
				Curve(r.At(k1), r.At(k1) + r.Forward(k1) * 16f, foot.Mouth - foot.Out * 14f, foot.Mouth);
				Add(foot.Mouth + foot.Out * (foot.Depth - 4f));
			}
			top = path[0];
			bottom = path[path.Count - 1];
		}

		private void Add(Vector3 p)
		{
			if (path.Count > 0 && Vector3.Distance(path[path.Count - 1], p) < 0.5f)
			{
				return;
			}
			along.Add(path.Count == 0 ? 0f : along[along.Count - 1] + Vector3.Distance(path[path.Count - 1], p));
			path.Add(p);
		}

		private void Curve(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
		{
			for (int k = 1; k <= 12; k++)
			{
				float t = k / 12f, u = 1f - t;
				Add(p0 * (u * u * u) + p1 * (3f * u * u * t) + p2 * (3f * u * t * t) + p3 * (t * t * t));
			}
		}

		/// <summary>Rails where the cars leave the haulage's own track: in the headings and on the curves, and a bumper at each end.</summary>
		private void LayRails(Batch steel, Batch wood)
		{
			Road r = Route.Main;
			for (int k = 0; k < path.Count - 1; k++)
			{
				Vector3 a = path[k], c = path[k + 1];
				int near = r.NearestFlat(a, out float off);
				bool onHaulage = off < 1f && Mathf.Abs(a.y - r.P[near].y) < 2f;
				if (onHaulage)
				{
					continue;
				}
				Vector3 dir = (c - a).normalized, side = Vector3.Cross(Vector3.up, dir).normalized;
				Quaternion q = Quaternion.LookRotation(dir, Vector3.up);
				foreach (float s in new[] { -3.2f, 3.2f })
				{
					steel.Box((a + c) * 0.5f + side * s + Vector3.up * 0.25f, new Vector3(0.5f, 0.4f, Vector3.Distance(a, c) + 0.3f), q, 4f);
				}
				wood.Box(a + Vector3.up * 0.08f, new Vector3(9f, 0.2f, 1.1f), q, 4f);
			}
			foreach (Vector3[] end in new[] { new[] { path[0], path[0] - path[1] }, new[] { path[path.Count - 1], path[path.Count - 1] - path[path.Count - 2] } })
			{
				Vector3 dir = end[1].normalized;
				Quaternion q = Quaternion.LookRotation(dir, Vector3.up);
				wood.Box(end[0] + dir * 6f + Vector3.up * 1.6f, new Vector3(9f, 3.2f, 2f), q, 3f);
				steel.Box(end[0] + dir * 5f + Vector3.up * 2.4f, new Vector3(7f, 0.8f, 0.8f), q, 2f);
			}
		}

		private void Park()
		{
			state = 0;
			travelled = 2f * Spacing + 1f;
			speed = 0f;
			PlaceAll();
		}

		private void FixedUpdate()
		{
			if (tm == null || !tm.raceStarted || path.Count < 2)
			{
				return;
			}
			Vector3 player = tm.playerCarController != null ? tm.playerCarController.transform.position : Vector3.zero;
			float length = along[along.Count - 1];
			if (state == 0)
			{
				bool near = (player - top).sqrMagnitude < 700f * 700f;
				wait -= near ? Time.fixedDeltaTime : 0f;
				if (wait <= 0f)
				{
					state = 1;
					speed = 4f;
					runs++;
				}
				return;
			}
			if (state == 1)
			{
				// faster and faster down the plane, braking hard into the bottom heading so it stops at the bumper
				speed = Mathf.Min(48f, speed + 9f * Time.fixedDeltaTime);
				speed = Mathf.Min(speed, Mathf.Sqrt(2f * 30f * Mathf.Max(0f, length - travelled)) + 0.5f);
				travelled = Mathf.Min(length, travelled + speed * Time.fixedDeltaTime);
				PlaceAll();
				if (travelled >= length - 0.01f)
				{
					state = 2;
				}
				return;
			}
			// stopped: back to the top once nobody can see either end and the player is well away
			bool seen = false;
			foreach (Renderer rd in renderers)
			{
				seen |= rd.isVisible;
			}
			if (!seen && (player - bottom).sqrMagnitude > 180f * 180f && (player - top).sqrMagnitude > 180f * 180f)
			{
				Park();
				wait = 14f + (runs * 5) % 9;
			}
		}

		private void PlaceAll()
		{
			for (int k = 0; k < cars.Count; k++)
			{
				Place(cars[k], Mathf.Max(0f, travelled - k * Spacing));
			}
		}

		/// <summary>Puts a car s units along the path, facing along it.</summary>
		private void Place(Rigidbody car, float s)
		{
			int i = 0;
			while (i < along.Count - 2 && along[i + 1] < s)
			{
				i++;
			}
			float seg = along[i + 1] - along[i];
			float t = seg > 0f ? Mathf.Clamp01((s - along[i]) / seg) : 0f;
			Vector3 p = Vector3.Lerp(path[i], path[i + 1], t);
			Vector3 dir = path[i + 1] - path[i];
			car.MovePosition(p + Vector3.up * 0.2f);
			car.MoveRotation(Quaternion.LookRotation(dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward, Vector3.up));
		}
	}

	/// <summary>
	/// Three chutes stick out of the walls over the lower gangway. Each, in its turn, lets go a burst of lump coal onto the road: loose
	/// lumps that roll, get knocked about and are gone ten seconds later. Only while the player is near.
	/// </summary>
	internal sealed class CoalChutes : MonoBehaviour
	{
		private sealed class Chute
		{
			public Vector3 Mouth;
			public Vector3 Toward;
			public float Next;
		}

		private readonly List<Chute> chutes = new List<Chute>();
		private readonly Queue<KeyValuePair<float, GameObject>> lumps = new Queue<KeyValuePair<float, GameObject>>();
		private TrackManager tm;
		private int layer;
		private Mesh lumpMesh;
		private Material lumpMat;
		private System.Random rng = new System.Random(1877);

		internal void Setup(TrackManager track, int lay)
		{
			tm = track;
			layer = lay;
			Road r = Route.Main;
			int a = Route.Mark("Plane foot") + 8, b = Route.Seam.Fork - 6;
			Batch steel = new Batch("chutes", Tex.Corrugated, new Color(0.5f, 0.45f, 0.42f));
			for (int k = 0; k < 3; k++)
			{
				int i = a + (b - a) * (k + 1) / 4;
				float side = k % 2 == 0 ? 1f : -1f;
				Vector3 wall = r.P[i] + r.Right(i) * side * (r.Half - 0.5f) + Vector3.up * (Mine.WallTop - 1f);
				Vector3 mouth = r.P[i] + r.Right(i) * side * (r.Half - 7f) + Vector3.up * (Mine.WallTop - 4f);
				Quaternion turn = Quaternion.LookRotation(mouth - wall, Vector3.up);
				steel.Box((wall + mouth) * 0.5f, new Vector3(5f, 1.2f, (mouth - wall).magnitude + 2f), turn, 3f);
				steel.Box((wall + mouth) * 0.5f + turn * new Vector3(2.5f, 1.2f, 0f), new Vector3(0.6f, 2.6f, (mouth - wall).magnitude + 2f), turn, 3f);
				steel.Box((wall + mouth) * 0.5f + turn * new Vector3(-2.5f, 1.2f, 0f), new Vector3(0.6f, 2.6f, (mouth - wall).magnitude + 2f), turn, 3f);
				chutes.Add(new Chute { Mouth = mouth, Toward = -r.Right(i) * side, Next = 4f + k * 2.7f });
			}
			GameObject go = new GameObject("chute steel") { layer = layer };
			go.transform.SetParent(transform, false);
			go.AddComponent<MeshFilter>().sharedMesh = steel.ToMesh();
			go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get(steel.Tex, steel.Tint, true, Color.black);
			Batch lump = new Batch("lump", Tex.Coal, Color.white);
			lump.Box(Vector3.zero, Vector3.one, Quaternion.Euler(12f, 30f, 8f), 1f);
			lumpMesh = lump.ToMesh();
			lumpMat = Mats.Get(lump.Tex, lump.Tint, false, Color.black);
		}

		private void FixedUpdate()
		{
			while (lumps.Count > 0 && (lumps.Peek().Key < Time.time || lumps.Count > 70))
			{
				GameObject old = lumps.Dequeue().Value;
				if (old != null)
				{
					Destroy(old);
				}
			}
			if (tm == null || !tm.raceStarted || tm.playerCarController == null)
			{
				return;
			}
			Vector3 player = tm.playerCarController.transform.position;
			foreach (Chute c in chutes)
			{
				c.Next -= Time.fixedDeltaTime;
				if (c.Next > 0f)
				{
					continue;
				}
				c.Next = 7f + (float)rng.NextDouble() * 4f;
				if ((player - c.Mouth).sqrMagnitude > 450f * 450f)
				{
					continue;
				}
				for (int k = 0; k < 9; k++)
				{
					Spawn(c);
				}
			}
		}

		private void Spawn(Chute c)
		{
			float s = 1.4f + (float)rng.NextDouble() * 1.4f;
			GameObject go = new GameObject("lump") { layer = layer };
			go.transform.SetParent(transform, false);
			go.transform.position = c.Mouth + new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble(), (float)rng.NextDouble() - 0.5f) * 2.5f;
			go.transform.rotation = Quaternion.Euler((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, 0f);
			go.transform.localScale = Vector3.one * s;
			go.AddComponent<MeshFilter>().sharedMesh = lumpMesh;
			go.AddComponent<MeshRenderer>().sharedMaterial = lumpMat;
			go.AddComponent<BoxCollider>();
			Rigidbody rb = go.AddComponent<Rigidbody>();
			rb.mass = 20f * s;
			rb.linearVelocity = c.Toward * (6f + (float)rng.NextDouble() * 8f) + Vector3.down * 2f;
			rb.angularVelocity = Random.insideUnitSphere * 4f;
			lumps.Enqueue(new KeyValuePair<float, GameObject>(Time.time + 10f, go));
		}
	}

	/// <summary>
	/// Two pairs of timber ventilation doors across the coal chute. They swing shut and open again on a cycle (open 3.2 s, shut 2.2 s),
	/// They are never
	/// solid: a car that meets a shut door bursts through it, planks flying, and loses a share of its speed (DoorSpeedKept). A smashed
	/// pair is back up twelve seconds later. (0.2.0 held a car under a set speed, and its sensor was too short to act before a fast car
	/// arrived: cars hit a solid door that then broke.)
	/// </summary>
	internal sealed class VentDoors : MonoBehaviour
	{
		private sealed class Door
		{
			public Transform Left, Right, Frame;
			public float Phase;
			public float BrokenUntil;
			public float Shut;
		}

		private const float Open = 3.2f, Swing = 0.6f, Closed = 2.2f;
		private readonly List<Door> doors = new List<Door>();
		private readonly Queue<KeyValuePair<float, GameObject>> planks = new Queue<KeyValuePair<float, GameObject>>();
		private Mesh plankMesh;
		private Material plankMat;
		private int layer;

		internal void Setup(int lay)
		{
			layer = lay;
			Road r = Route.Chute;
			float high = Mine.Height - 1.5f;
			foreach (float f in new[] { 0.3f, 0.62f })
			{
				int i = Mathf.RoundToInt((r.Count - 1) * f);
				Vector3 p = r.P[i];
				Quaternion turn = Quaternion.LookRotation(r.Forward(i), Vector3.up);
				GameObject frame = new GameObject("door frame") { layer = layer };
				frame.transform.SetParent(transform, false);
				frame.transform.SetPositionAndRotation(p, turn);
				float w = r.HalfAt(i);
				Door d = new Door { Phase = f * 3.1f, Frame = frame.transform };
				// the frame: a header beam and two posts, standing in against the ribs
				Batch beams = new Batch("door frame", Tex.Timber, new Color(0.8f, 0.72f, 0.62f));
				beams.Box(new Vector3(0f, high + 0.6f, 0f), new Vector3(2f * w + 3f, 1.4f, 1.6f), Quaternion.identity, 4f);
				beams.Box(new Vector3(-w - 0.8f, high * 0.5f, 0f), new Vector3(1.6f, high + 1f, 1.6f), Quaternion.identity, 4f);
				beams.Box(new Vector3(w + 0.8f, high * 0.5f, 0f), new Vector3(1.6f, high + 1f, 1.6f), Quaternion.identity, 4f);
				AddMesh(frame.transform, "beams", beams, Vector3.zero);
				foreach (float side in new[] { -1f, 1f })
				{
					float dir = -side;
					Batch boards = new Batch("vent door", Tex.Boards, new Color(0.85f, 0.8f, 0.72f));
					boards.Box(new Vector3(dir * w * 0.5f, high * 0.5f, 0f), new Vector3(w, high, 0.9f), Quaternion.identity, 4f);
					boards.Box(new Vector3(dir * w * 0.5f, high * 0.3f, -0.6f), new Vector3(w - 1f, 0.9f, 0.4f), Quaternion.identity, 4f);
					boards.Box(new Vector3(dir * w * 0.5f, high * 0.75f, -0.6f), new Vector3(w - 1f, 0.9f, 0.4f), Quaternion.identity, 4f);
					Transform hinge = AddMesh(frame.transform, side < 0f ? "left" : "right", boards, new Vector3(side * w, 0f, 0f));
					if (side < 0f)
					{
						d.Left = hinge;
					}
					else
					{
						d.Right = hinge;
					}
				}
				// the sensor: the doorway itself, on Ignore Raycast (the cars find the ground with rays that would otherwise stop on it)
				GameObject sensor = new GameObject("door sensor") { layer = 2 };
				sensor.transform.SetParent(frame.transform, false);
				BoxCollider zone = sensor.AddComponent<BoxCollider>();
				zone.isTrigger = true;
				zone.center = new Vector3(0f, high * 0.5f, 0f);
				zone.size = new Vector3(2f * w, high, 5f);
				sensor.AddComponent<DoorSensor>().Setup(this, doors.Count);
				doors.Add(d);
			}
			Batch plank = new Batch("plank", Tex.Boards, new Color(0.85f, 0.8f, 0.72f));
			plank.Box(Vector3.zero, new Vector3(1f, 0.4f, 6f), Quaternion.identity, 3f);
			plankMesh = plank.ToMesh();
			plankMat = Mats.Get(plank.Tex, plank.Tint, false, Color.black);
		}

		private Transform AddMesh(Transform parent, string name, Batch batch, Vector3 at)
		{
			GameObject go = new GameObject(name) { layer = layer };
			go.transform.SetParent(parent, false);
			go.transform.localPosition = at;
			go.AddComponent<MeshFilter>().sharedMesh = batch.ToMesh();
			go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get(batch.Tex, batch.Tint, false, batch.Emission);
			return go.transform;
		}

		/// <summary>How shut a door is now: 0 open, 1 shut.</summary>
		private static float Shutness(float t)
		{
			float cycle = Open + Swing + Closed + Swing;
			t %= cycle;
			if (t < Open)
			{
				return 0f;
			}
			if (t < Open + Swing)
			{
				return (t - Open) / Swing;
			}
			if (t < Open + Swing + Closed)
			{
				return 1f;
			}
			return 1f - (t - Open - Swing - Closed) / Swing;
		}

		private void FixedUpdate()
		{
			while (planks.Count > 0 && planks.Peek().Key < Time.time)
			{
				GameObject old = planks.Dequeue().Value;
				if (old != null)
				{
					Destroy(old);
				}
			}
			foreach (Door d in doors)
			{
				bool broken = Time.time < d.BrokenUntil;
				float t = Time.time + d.Phase;
				d.Shut = broken ? 0f : Shutness(t);
				float angle = Mathf.Lerp(88f, 0f, Mathf.SmoothStep(0f, 1f, d.Shut));
				d.Left.gameObject.SetActive(!broken);
				d.Right.gameObject.SetActive(!broken);
				d.Left.localRotation = Quaternion.Euler(0f, angle, 0f);
				d.Right.localRotation = Quaternion.Euler(0f, -angle, 0f);
			}
		}

		/// <summary>A car is in a doorway: if the doors are shut (or shutting) it bursts through them.</summary>
		internal void Meet(int index, Collider other)
		{
			Door d = doors[index];
			if (Time.time < d.BrokenUntil || d.Shut < 0.35f || !Hazards.Car(other, out Rigidbody body, out float dash))
			{
				return;
			}
			d.BrokenUntil = Time.time + 12f;
			d.Left.gameObject.SetActive(false);
			d.Right.gameObject.SetActive(false);
			body.linearVelocity *= Mod.DoorSpeedKept.Value;
			// the planks go flying ahead of the car
			Vector3 v = body.linearVelocity;
			for (int k = 0; k < 8; k++)
			{
				GameObject go = new GameObject("plank") { layer = layer };
				go.transform.SetParent(transform, false);
				go.transform.position = d.Frame.position + d.Frame.right * Random.Range(-8f, 8f) + Vector3.up * Random.Range(2f, 12f);
				go.transform.rotation = Random.rotation;
				go.AddComponent<MeshFilter>().sharedMesh = plankMesh;
				go.AddComponent<MeshRenderer>().sharedMaterial = plankMat;
				go.AddComponent<BoxCollider>();
				Rigidbody rb = go.AddComponent<Rigidbody>();
				rb.mass = 2f;
				rb.linearVelocity = v * Random.Range(0.6f, 1.1f) + Random.insideUnitSphere * 6f;
				rb.angularVelocity = Random.insideUnitSphere * 8f;
				planks.Enqueue(new KeyValuePair<float, GameObject>(Time.time + 6f, go));
			}
			Mod.Info(body.name + " burst through a ventilation door at " + dash.ToString("0"));
		}
	}

	/// <summary>Passes a car entering a door's sensor to its doors.</summary>
	internal sealed class DoorSensor : MonoBehaviour
	{
		private VentDoors owner;
		private int index;

		internal void Setup(VentDoors doors, int i)
		{
			owner = doors;
			index = i;
		}

		private void OnTriggerEnter(Collider other)
		{
			if (owner != null)
			{
				owner.Meet(index, other);
			}
		}

		private void OnTriggerStay(Collider other)
		{
			if (owner != null)
			{
				owner.Meet(index, other);
			}
		}
	}
}
