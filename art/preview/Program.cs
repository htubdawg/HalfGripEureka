using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using EurekaMine;
using UnityEngine;

internal static class Program
{
	private static void Main(string[] args)
	{
		CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
		Route.Load();
		Tex.Make();
		var b = new Dictionary<string, Batch>();
		var t0 = DateTime.Now;
		Mine.Build(b);
		Pithead.Build(b);
		Console.WriteLine("built in " + (DateTime.Now - t0).TotalMilliseconds.ToString("0") + " ms, lamps " + Mine.Lamps.Count);
		var sb = new StringBuilder("{\"batches\":[");
		var textures = new Dictionary<string, Texture2D>();
		bool first = true;
		int tris = 0;
		foreach (var kv in b)
		{
			Mesh m = kv.Value.ToMesh();
			if (m.T.Count == 0) continue;
			tris += m.T.Count / 3;
			// the texture's average colour times the tint
			Color avg = new Color(0, 0, 0);
			Texture2D tex = kv.Value.Tex;
			if (tex != null && tex.px != null) { foreach (Color c in tex.px) { avg.r += c.r; avg.g += c.g; avg.b += c.b; } avg = avg * (1f / tex.px.Length); } else avg = Color.white;
			Color tint = kv.Value.Tint;
			if (!first) sb.Append(','); first = false;
			sb.Append("{\"name\":\"").Append(kv.Key).Append("\",\"color\":[").Append(avg.r * tint.r).Append(',').Append(avg.g * tint.g).Append(',').Append(avg.b * tint.b).Append("],");
			sb.Append("\"tex\":\"").Append(tex != null ? tex.name : "").Append("\",\"tint\":[").Append(tint.r).Append(',').Append(tint.g).Append(',').Append(tint.b).Append("],\"two\":").Append(kv.Value.TwoSided ? "true" : "false").Append(',');
			sb.Append("\"uv\":[");
			for (int i = 0; i < m.U.Count; i++) { if (i > 0) sb.Append(','); sb.Append(m.U[i].x.ToString("0.###")).Append(',').Append(m.U[i].y.ToString("0.###")); }
			sb.Append("],");
			if (tex != null && tex.px != null) textures[tex.name] = tex;
			sb.Append("\"emissive\":").Append(kv.Value.Emission.maxColorComponent > 0.01f ? "true" : "false").Append(",\"v\":[");
			for (int i = 0; i < m.V.Count; i++) { if (i > 0) sb.Append(','); sb.Append(m.V[i].x.ToString("0.##")).Append(',').Append(m.V[i].y.ToString("0.##")).Append(',').Append(m.V[i].z.ToString("0.##")); }
			sb.Append("],\"t\":[").Append(string.Join(",", m.T)).Append("]}");
		}
		sb.Append("],\"lamps\":[");
		for (int i = 0; i < Mine.Lamps.Count; i++) { if (i > 0) sb.Append(','); var p = Mine.Lamps[i]; sb.Append(p.x.ToString("0.#")).Append(',').Append(p.y.ToString("0.#")).Append(',').Append(p.z.ToString("0.#")); }
		sb.Append("],\"textures\":{");
		bool f3 = true;
		foreach (var kv in textures)
		{
			if (!f3) sb.Append(','); f3 = false;
			var bytes = new byte[kv.Value.px.Length * 4];
			for (int i = 0; i < kv.Value.px.Length; i++) { Color c = kv.Value.px[i]; bytes[i * 4] = (byte)(Math.Clamp(c.r, 0, 1) * 255); bytes[i * 4 + 1] = (byte)(Math.Clamp(c.g, 0, 1) * 255); bytes[i * 4 + 2] = (byte)(Math.Clamp(c.b, 0, 1) * 255); bytes[i * 4 + 3] = 255; }
			sb.Append('"').Append(kv.Key).Append("\":{\"size\":").Append(kv.Value.size).Append(",\"px\":\"").Append(Convert.ToBase64String(bytes)).Append("\"}");
		}
		sb.Append("},\"roads\":{");
		bool f2 = true;
		foreach (Road r in Route.All)
		{
			if (!f2) sb.Append(','); f2 = false;
			sb.Append('"').Append(r.Name).Append("\":[");
			for (int i = 0; i < r.Count; i++) { if (i > 0) sb.Append(','); sb.Append(r.P[i].x.ToString("0.#")).Append(',').Append(r.P[i].y.ToString("0.#")).Append(',').Append(r.P[i].z.ToString("0.#")); }
			sb.Append(']');
		}
		sb.Append("}}");
		File.WriteAllText(args[0], sb.ToString());
		Console.WriteLine(b.Count + " batches, " + tris + " triangles -> " + args[0]);
	}
}
