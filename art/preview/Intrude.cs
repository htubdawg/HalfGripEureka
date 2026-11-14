using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using EurekaMine;
using UnityEngine;

/// <summary>
/// INTRUDE=1: finds drawn triangles that stand inside a road's drivable space (inside the guard, short of its walls and roof by a
/// margin). Rock seen that way shows from one road and is culled from the other: a sheet of rock across the road from one way,
/// see-through from the other (owner, 0.4.7). Writes intrude.json with each offending triangle's middle, road and point.
/// </summary>
internal static class Intrude
{
	/// <summary>Is p inside road r's drivable space: within its half width less the margin, over its floor, under its guard's arch less the margin?</summary>
	private static bool Inside(Road r, Vector3 p, float margin, out int at)
	{
		at = -1;
		int n = r.Count, last = r.Closed ? n : n - 1;
		float best = float.MaxValue;
		bool inside = false;
		for (int i = 0; i < last; i++)
		{
			Vector3 a = r.At(i), c = r.At(i + 1);
			Vector3 ab = c - a; ab.y = 0f;
			Vector3 ap = p - a; ap.y = 0f;
			float len2 = ab.sqrMagnitude;
			float f = len2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(ap, ab) / len2) : 0f;
			Vector3 q = Vector3.Lerp(a, c, f);
			Vector3 d = p - q;
			float lat2 = d.x * d.x + d.z * d.z;
			if (lat2 > 60f * 60f) continue;
			float h = Mathf.Lerp(r.HalfAt(i), r.HalfAt(i + 1), f), ceil = Mathf.Lerp(r.CeilAt(i), r.CeilAt(i + 1), f);
			float lat = Mathf.Sqrt(lat2);
			float y = d.y;
			if (y < 0.6f || y > ceil + 2f) continue;
			if (lat > h - margin) continue;
			float roof = Mine.WallTop + (ceil - Mine.WallTop) * Mathf.Sqrt(Mathf.Max(0f, 1f - (lat / h) * (lat / h)));
			if (y > roof - margin - 2.4f) continue;   // the arch may sag 2.4 under the guard by design (Mine.Rock)
			if (lat2 < best) { best = lat2; at = i; inside = true; }
		}
		return inside;
	}

	internal static void Run(Dictionary<string, Batch> batches, string outPath)
	{
		var sb = new StringBuilder("[");
		int total = 0;
		var byBatch = new Dictionary<string, int>();
		foreach (var kv in batches)
		{
			if (kv.Value.Invisible || kv.Key.StartsWith("glow")) continue;
			if (!(kv.Key.Contains("rock") || kv.Key.Contains("roof") || kv.Key.Contains("passage") || kv.Key.Contains("niche"))) continue;
			if (kv.Key.Contains("house") || kv.Key.Contains("breaker")) continue;
			Mesh m = kv.Value.ToMesh();
			for (int t = 0; t < m.T.Count; t += 3)
			{
				Vector3 a = m.V[m.T[t]], b = m.V[m.T[t + 1]], c = m.V[m.T[t + 2]];
				Vector3 mid = (a + b + c) / 3f;
				foreach (Road r in Route.All)
				{
					if (Inside(r, mid, 1.0f, out int at))
					{
						total++;
						byBatch[kv.Key] = byBatch.TryGetValue(kv.Key, out int k) ? k + 1 : 1;
						if (total < 4000)
						{
							if (sb.Length > 1) sb.Append(',');
							sb.Append("{\"batch\":\"").Append(kv.Key).Append("\",\"road\":\"").Append(r.Name).Append("\",\"i\":").Append(at)
								.Append(",\"p\":[").Append(mid.x.ToString("0.#")).Append(',').Append(mid.y.ToString("0.#")).Append(',').Append(mid.z.ToString("0.#")).Append("]}");
						}
						break;
					}
				}
			}
		}
		sb.Append(']');
		File.WriteAllText(outPath, sb.ToString());
		Console.WriteLine("intruding triangles: " + total);
		foreach (var kv in byBatch) Console.WriteLine("  " + kv.Key + ": " + kv.Value);
	}
}
