// A small stand-in for the parts of UnityEngine the mod's geometry code uses, so it runs outside the game for previews.
using System;
using System.Collections.Generic;
namespace UnityEngine.Rendering { public enum IndexFormat { UInt16, UInt32 } public enum ShadowCastingMode { Off, On } }
namespace UnityEngine
{
	public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; }
		public static Vector2 operator /(Vector2 a, float d) => new Vector2(a.x / d, a.y / d); }
	public struct Vector3
	{
		public float x, y, z;
		public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
		public static Vector3 zero => new Vector3(0, 0, 0); public static Vector3 one => new Vector3(1, 1, 1);
		public static Vector3 up => new Vector3(0, 1, 0); public static Vector3 down => new Vector3(0, -1, 0);
		public static Vector3 right => new Vector3(1, 0, 0); public static Vector3 left => new Vector3(-1, 0, 0);
		public static Vector3 forward => new Vector3(0, 0, 1); public static Vector3 back => new Vector3(0, 0, -1);
		public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
		public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
		public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
		public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);
		public static Vector3 operator *(float d, Vector3 a) => new Vector3(a.x * d, a.y * d, a.z * d);
		public static Vector3 operator /(Vector3 a, float d) => new Vector3(a.x / d, a.y / d, a.z / d);
		public float sqrMagnitude => x * x + y * y + z * z; public float magnitude => (float)Math.Sqrt(sqrMagnitude);
		public Vector3 normalized { get { float m = magnitude; return m > 1e-6f ? this / m : zero; } }
		public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
		public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
		public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
		public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { t = Mathf.Clamp01(t); return a + (b - a) * t; }
	}
	public struct Quaternion
	{
		public float x, y, z, w;
		public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
		public static Quaternion identity => new Quaternion(0, 0, 0, 1);
		public static Quaternion AngleAxis(float deg, Vector3 axis) { axis = axis.normalized; float h = deg * Mathf.Deg2Rad * 0.5f; float s = (float)Math.Sin(h); return new Quaternion(axis.x * s, axis.y * s, axis.z * s, (float)Math.Cos(h)); }
		// Unity applies z, then x, then y
		public static Quaternion Euler(float x, float y, float z) => AngleAxis(y, Vector3.up) * AngleAxis(x, Vector3.right) * AngleAxis(z, Vector3.forward);
		public static Quaternion operator *(Quaternion a, Quaternion b) => new Quaternion(
			a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y, a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
			a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w, a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);
		public static Vector3 operator *(Quaternion q, Vector3 v)
		{
			Vector3 u = new Vector3(q.x, q.y, q.z); Vector3 t = 2f * Vector3.Cross(u, v);
			return v + q.w * t + Vector3.Cross(u, t);
		}
		public static Quaternion LookRotation(Vector3 f, Vector3 up)
		{
			f = f.normalized; Vector3 r = Vector3.Cross(up, f).normalized; Vector3 u = Vector3.Cross(f, r);
			float m00 = r.x, m01 = u.x, m02 = f.x, m10 = r.y, m11 = u.y, m12 = f.y, m20 = r.z, m21 = u.z, m22 = f.z;
			float tr = m00 + m11 + m22; Quaternion q;
			if (tr > 0) { float s = (float)Math.Sqrt(tr + 1) * 2; q = new Quaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25f * s); }
			else if (m00 > m11 && m00 > m22) { float s = (float)Math.Sqrt(1 + m00 - m11 - m22) * 2; q = new Quaternion(0.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s); }
			else if (m11 > m22) { float s = (float)Math.Sqrt(1 + m11 - m00 - m22) * 2; q = new Quaternion((m01 + m10) / s, 0.25f * s, (m12 + m21) / s, (m02 - m20) / s); }
			else { float s = (float)Math.Sqrt(1 + m22 - m00 - m11) * 2; q = new Quaternion((m02 + m20) / s, (m12 + m21) / s, 0.25f * s, (m10 - m01) / s); }
			return q;
		}
	}
	public static class Mathf
	{
		public const float PI = (float)Math.PI, Deg2Rad = PI / 180f, Rad2Deg = 180f / PI;
		public static float Sin(float a) => (float)Math.Sin(a); public static float Cos(float a) => (float)Math.Cos(a);
		public static float Sqrt(float a) => (float)Math.Sqrt(a); public static float Abs(float a) => Math.Abs(a);
		public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
		public static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
		public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
		public static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v;
		public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
		public static float InverseLerp(float a, float b, float v) => a == b ? 0 : Clamp01((v - a) / (b - a));
		public static int RoundToInt(float v) => (int)Math.Round(v); public static float Max(float a, float b) => Math.Max(a, b);
		public static int Max(int a, int b) => Math.Max(a, b); public static float Min(float a, float b) => Math.Min(a, b);
	}
	public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a = 1) { this.r = r; this.g = g; this.b = b; this.a = a; }
		public static Color white => new Color(1, 1, 1); public static Color black => new Color(0, 0, 0);
		public float maxColorComponent => Math.Max(r, Math.Max(g, b));
		public static Color operator *(Color c, float d) => new Color(c.r * d, c.g * d, c.b * d, c.a);
		public static Color Lerp(Color a, Color b, float t) { t = Mathf.Clamp01(t); return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t); } }
	public static class ColorUtility { public static string ToHtmlStringRGBA(Color c) => c.ToString(); public static string ToHtmlStringRGB(Color c) => c.ToString(); }
	public class Object { public string name; public int GetInstanceID() => 0; }
	public enum TextureFormat { RGBA32 } public enum FilterMode { Point } public enum TextureWrapMode { Repeat }
	public enum MaterialGlobalIlluminationFlags { RealtimeEmissive }
	public class Texture2D : Object { public Color[] px; public int size;
		public Texture2D(int w, int h, TextureFormat f, bool m) { size = w; } public FilterMode filterMode; public TextureWrapMode wrapMode;
		public void SetPixels(Color[] c) { px = c; } public void Apply(bool a, bool b) { } }
	public class Shader : Object { public static Shader Find(string n) => null; }
	public class Material : Object { public Shader shader; public Material(Shader s) { } public bool HasProperty(string n) => false;
		public void SetTexture(string n, Texture2D t) { } public void SetColor(string n, Color c) { } public void SetFloat(string n, float f) { }
		public void EnableKeyword(string k) { } public MaterialGlobalIlluminationFlags globalIlluminationFlags; }
	public class Mesh : Object { public UnityEngine.Rendering.IndexFormat indexFormat;
		public List<Vector3> V; public List<int> T;
		public void SetVertices(List<Vector3> v) { V = v; } public void SetUVs(int c, List<Vector2> u) { } public void SetTriangles(List<int> t, int s) { T = t; }
		public void RecalculateNormals() { } public void RecalculateBounds() { } }
}
namespace EurekaMine
{
	internal sealed class Label { public UnityEngine.Vector3 Pos; public UnityEngine.Vector3 Facing; public string Text; public UnityEngine.Color Color; public UnityEngine.Vector2 Size; }
	internal static class SceneBuilder { internal static readonly List<Label> Labels = new List<Label>(); }
}
