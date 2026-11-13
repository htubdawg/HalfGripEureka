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
	/// A string of three loaded mine cars breaks loose at the head of the plane and runs down it in one lane, faster and faster, then off
	/// into a side heading at the foot. One goes every 14 to 22 seconds while the player is anywhere near the plane; the lane changes each
	/// time. The cars are kinematic: they shove what they meet and are not stopped by it.
	/// </summary>
	internal sealed class RunawayCars : MonoBehaviour
	{
		private readonly List<Rigidbody> cars = new List<Rigidbody>();
		private TrackManager tm;
		private int head, foot;
		private float wait = 6f;
		private bool running;
		private float travelled;
		private float speed;
		private float lane;
		private int runs;

		internal void Setup(TrackManager track, Mesh body, Mesh load, Material bodyMat, Material loadMat, int layer)
		{
			tm = track;
			head = Route.Mark("Plane head") - 4;
			foot = Route.Mark("Plane foot") + 6;
			for (int k = 0; k < 3; k++)
			{
				GameObject go = new GameObject("mine car " + k) { layer = layer };
				go.transform.SetParent(transform, false);
				go.AddComponent<MeshFilter>().sharedMesh = body;
				go.AddComponent<MeshRenderer>().sharedMaterial = bodyMat;
				GameObject top = new GameObject("load") { layer = layer };
				top.transform.SetParent(go.transform, false);
				top.AddComponent<MeshFilter>().sharedMesh = load;
				top.AddComponent<MeshRenderer>().sharedMaterial = loadMat;
				BoxCollider box = go.AddComponent<BoxCollider>();
				box.center = new Vector3(0f, 3.8f, 0f);
				box.size = new Vector3(6.5f, 6.4f, 9.6f);
				Rigidbody rb = go.AddComponent<Rigidbody>();
				rb.isKinematic = true;
				rb.interpolation = RigidbodyInterpolation.Interpolate;
				go.SetActive(false);
				cars.Add(rb);
			}
		}

		private void FixedUpdate()
		{
			if (tm == null || !tm.raceStarted)
			{
				return;
			}
			Road r = Route.Main;
			if (!running)
			{
				Vector3 player = tm.playerCarController != null ? tm.playerCarController.transform.position : Vector3.zero;
				bool near = (player - r.At((head + foot) / 2)).sqrMagnitude < 700f * 700f;
				wait -= near ? Time.fixedDeltaTime : 0f;
				if (wait <= 0f)
				{
					running = true;
					travelled = 0f;
					speed = 8f;
					lane = (runs % 2 == 0 ? 1f : -1f) * r.Half * 0.45f;
					runs++;
					foreach (Rigidbody c in cars)
					{
						c.gameObject.SetActive(true);
					}
				}
				return;
			}
			speed = Mathf.Min(48f, speed + 9f * Time.fixedDeltaTime);
			travelled += speed * Time.fixedDeltaTime;
			float length = r.S[foot] - r.S[head];
			for (int k = 0; k < cars.Count; k++)
			{
				float s = travelled - k * 11f;
				Place(cars[k], Mathf.Clamp(s, 0f, length));
			}
			if (travelled - 2f * 11f > length)
			{
				running = false;
				wait = 14f + (runs * 5) % 9;
				foreach (Rigidbody c in cars)
				{
					c.gameObject.SetActive(false);
				}
			}
		}

		/// <summary>Puts a car s units down the plane from its head, in this run's lane.</summary>
		private void Place(Rigidbody car, float s)
		{
			Road r = Route.Main;
			int i = head;
			while (i < foot && r.S[i + 1] - r.S[head] < s)
			{
				i++;
			}
			float seg = r.S[i + 1] - r.S[i];
			float t = seg > 0f ? Mathf.Clamp01((s - (r.S[i] - r.S[head])) / seg) : 0f;
			Vector3 p = Vector3.Lerp(r.P[i], r.P[i + 1], t) + r.Right(i) * lane;
			Vector3 dir = (r.P[i + 1] - r.P[i]).normalized;
			car.MovePosition(p + Vector3.up * 0.2f);
			car.MoveRotation(Quaternion.LookRotation(dir, Vector3.up));
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
	/// Two pairs of timber ventilation doors across the coal chute. They swing shut and open again on a cycle (open 3.2 s, shut 2.2 s).
	/// A car that meets a shut door fast enough smashes through it and keeps most of its speed; slower, the door holds. A smashed door is
	/// back up twelve seconds later.
	/// </summary>
	internal sealed class VentDoors : MonoBehaviour
	{
		private sealed class Door
		{
			public Transform Left, Right;
			public Collider[] Solid;
			public float Phase;
			public float BrokenUntil;
			public float Shut;
		}

		private const float Open = 3.2f, Swing = 0.6f, Closed = 2.2f;
		private readonly List<Door> doors = new List<Door>();

		internal void Setup(int layer)
		{
			Road r = Route.Chute;
			foreach (float f in new[] { 0.3f, 0.62f })
			{
				int i = Mathf.RoundToInt((r.Count - 1) * f);
				Vector3 p = r.P[i];
				Quaternion turn = Quaternion.LookRotation(r.Forward(i), Vector3.up);
				GameObject frame = new GameObject("door frame") { layer = layer };
				frame.transform.SetParent(transform, false);
				frame.transform.SetPositionAndRotation(p, turn);
				float w = r.Half;
				Door d = new Door { Phase = f * 3.1f };
				List<Collider> solid = new List<Collider>();
				foreach (float side in new[] { -1f, 1f })
				{
					float dir = -side;
					Batch planks = new Batch("vent door", Tex.Boards, new Color(0.85f, 0.8f, 0.72f));
					planks.Box(new Vector3(dir * w * 0.5f, Mine.WallTop * 0.5f, 0f), new Vector3(w, Mine.WallTop, 0.9f), Quaternion.identity, 4f);
					planks.Box(new Vector3(dir * w * 0.5f, Mine.WallTop * 0.3f, -0.6f), new Vector3(w - 1f, 0.9f, 0.4f), Quaternion.identity, 4f);
					planks.Box(new Vector3(dir * w * 0.5f, Mine.WallTop * 0.75f, -0.6f), new Vector3(w - 1f, 0.9f, 0.4f), Quaternion.identity, 4f);
					GameObject hinge = new GameObject(side < 0f ? "left" : "right") { layer = layer };
					hinge.transform.SetParent(frame.transform, false);
					hinge.transform.localPosition = new Vector3(side * w, 0f, 0f);
					hinge.AddComponent<MeshFilter>().sharedMesh = planks.ToMesh();
					hinge.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get(planks.Tex, planks.Tint, false, Color.black);
					BoxCollider box = hinge.AddComponent<BoxCollider>();
					box.center = new Vector3(dir * w * 0.5f, Mine.WallTop * 0.5f, 0f);
					box.size = new Vector3(w, Mine.WallTop, 1.4f);
					Rigidbody rb = hinge.AddComponent<Rigidbody>();
					rb.isKinematic = true;
					solid.Add(box);
					if (side < 0f)
					{
						d.Left = hinge.transform;
					}
					else
					{
						d.Right = hinge.transform;
					}
				}
				d.Solid = solid.ToArray();
				// the sensor: a box a little deeper than the door each side, that smashes it ahead of a fast car
				// on Ignore Raycast: the cars find the ground with rays that would otherwise stop on it
				GameObject sensor = new GameObject("door sensor") { layer = 2 };
				sensor.transform.SetParent(frame.transform, false);
				BoxCollider zone = sensor.AddComponent<BoxCollider>();
				zone.isTrigger = true;
				zone.center = new Vector3(0f, Mine.WallTop * 0.5f, 0f);
				zone.size = new Vector3(2f * w, Mine.WallTop, 12f);
				sensor.AddComponent<DoorSensor>().Setup(this, doors.Count);
				doors.Add(d);
			}
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
			foreach (Door d in doors)
			{
				bool broken = Time.time < d.BrokenUntil;
				d.Shut = broken ? 0f : Shutness(Time.time + d.Phase);
				float angle = Mathf.Lerp(88f, 0f, Mathf.SmoothStep(0f, 1f, d.Shut));
				d.Left.gameObject.SetActive(!broken);
				d.Right.gameObject.SetActive(!broken);
				d.Left.localRotation = Quaternion.Euler(0f, angle, 0f);
				d.Right.localRotation = Quaternion.Euler(0f, -angle, 0f);
			}
		}

		/// <summary>A car is at a door: smash it if it is shut (or shutting) and the car is fast enough.</summary>
		internal void Meet(int index, Collider other)
		{
			Door d = doors[index];
			if (Time.time < d.BrokenUntil || d.Shut < 0.25f || !Hazards.Car(other, out Rigidbody body, out float dash))
			{
				return;
			}
			if (dash < Mod.DoorBreakSpeed.Value)
			{
				return;
			}
			d.BrokenUntil = Time.time + 12f;
			d.Left.gameObject.SetActive(false);
			d.Right.gameObject.SetActive(false);
			body.linearVelocity *= Mod.DoorSpeedKept.Value;
			Mod.Info(body.name + " smashed a ventilation door at " + dash.ToString("0"));
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
