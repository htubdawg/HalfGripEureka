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
		// BULBS="gap,spread" tries other bulb spacings without editing Mine.cs
		string bulbs = Environment.GetEnvironmentVariable("BULBS");
		if (bulbs != null) { var bp = bulbs.Split(','); Mine.BulbGap = int.Parse(bp[0]); Mine.BulbSpread = int.Parse(bp[1]); }
		Tex.Make();
		var b = new Dictionary<string, Batch>();
		var t0 = DateTime.Now;
		Mine.Build(b);
		Pithead.Build(b);
		// GLOW="strength,near" tries other values of the painted light without editing Glow.cs
		string tune = Environment.GetEnvironmentVariable("GLOW");
		if (tune != null) { var parts = tune.Split(','); Glow.Strength = float.Parse(parts[0]); Glow.Near = float.Parse(parts[1]); }
		// DARK="ambient scale,fog start,fog end" tries other underground ambient and haze without editing Glow.cs
		string dk = Environment.GetEnvironmentVariable("DARK");
		if (dk != null) { var dp = dk.Split(','); float k = float.Parse(dp[0]); Dark.Sky *= k; Dark.Equator *= k; Dark.Ground *= k; Dark.FogStart = float.Parse(dp[1]); Dark.FogEnd = float.Parse(dp[2]); }
		if (Environment.GetEnvironmentVariable("INTRUDE") != null) Intrude.Run(b, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0])), "intrude.json"));
		var g0 = DateTime.Now;
		int painted = Glow.Build(b);
		Console.WriteLine("lamp light painted on " + painted + " triangles in " + (DateTime.Now - g0).TotalMilliseconds.ToString("0") + " ms");
		Console.WriteLine("built in " + (DateTime.Now - t0).TotalMilliseconds.ToString("0") + " ms, lamps " + Mine.Lamps.Count);
		var sb = new StringBuilder("{\"batches\":[");
		var textures = new Dictionary<string, Texture2D>();
		bool first = true;
		int tris = 0;
		foreach (var kv in b)
		{
			Mesh m = kv.Value.ToMesh();
			if (m.T.Count == 0 || kv.Value.Invisible) continue;
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
			if (m.C != null)
			{
				sb.Append("\"glow\":true,\"c\":[");
				for (int i = 0; i < m.C.Count; i++) { if (i > 0) sb.Append(','); sb.Append(m.C[i].r.ToString("0.###")).Append(',').Append(m.C[i].g.ToString("0.###")).Append(',').Append(m.C[i].b.ToString("0.###")); }
				sb.Append("],");
			}
			sb.Append("\"emissive\":").Append(kv.Value.Emission.maxColorComponent > 0.01f ? "true" : "false").Append(",\"v\":[");
			for (int i = 0; i < m.V.Count; i++) { if (i > 0) sb.Append(','); sb.Append(m.V[i].x.ToString("0.##")).Append(',').Append(m.V[i].y.ToString("0.##")).Append(',').Append(m.V[i].z.ToString("0.##")); }
			sb.Append("],\"t\":[").Append(string.Join(",", m.T)).Append("]}");
		}
		string C(Color c) => "[" + c.r.ToString("0.####") + "," + c.g.ToString("0.####") + "," + c.b.ToString("0.####") + "]";
		sb.Append("],\"dark\":{\"sky\":").Append(C(Dark.Sky)).Append(",\"equator\":").Append(C(Dark.Equator)).Append(",\"ground\":").Append(C(Dark.Ground))
			.Append(",\"fog\":").Append(C(Dark.Fog)).Append(",\"fogStart\":").Append(Dark.FogStart).Append(",\"fogEnd\":").Append(Dark.FogEnd).Append('}');
		sb.Append(",\"lamps\":[");
		for (int i = 0; i < Mine.Lamps.Count; i++) { if (i > 0) sb.Append(','); var p = Mine.Lamps[i]; sb.Append(p.x.ToString("0.#")).Append(',').Append(p.y.ToString("0.#")).Append(',').Append(p.z.ToString("0.#")); }
		sb.Append("],\"niches\":[");
		for (int i = 0; i < Mine.Niches.Count; i++)
		{
			var n = Mine.Niches[i]; if (i > 0) sb.Append(',');
			sb.Append("{\"kind\":").Append(n.Kind).Append(",\"road\":\"").Append(n.Road.Name).Append("\",\"mouth\":[").Append(n.Mouth.x.ToString("0.#")).Append(',').Append(n.Mouth.y.ToString("0.#")).Append(',').Append(n.Mouth.z.ToString("0.#"))
				.Append("],\"out\":[").Append(n.Out.x.ToString("0.###")).Append(',').Append(n.Out.y.ToString("0.###")).Append(',').Append(n.Out.z.ToString("0.###")).Append("],\"width\":").Append(n.Width.ToString("0.#")).Append(",\"depth\":").Append(n.Depth.ToString("0.#")).Append('}');
		}
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
