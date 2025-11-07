using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Cataphractal.Common;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Cataphract.Common;

public static class SdfWorldgenUtils
{
	public sealed record BuildSettings(
		int TileSize = 16,
		int BoundarySampleStride = 1,
		int MaxDegreeOfParallelism = 0,
		CancellationToken Cancellation = default);
	// todo: parallel worldgen
}

public readonly struct SDFSample
{
	public readonly float Distance;
	public readonly Vector2 Gradient;

	public SDFSample(float distance, Vector2 gradient)
	{
		Distance = distance;
		Gradient = gradient.LengthSquared() > float.Epsilon ? Vector2.Normalize(gradient) : Vector2.UnitY;
	}
}

public static class SignedDistance
{
	private const float RotationEpsilon = 1e-6f;

	private static bool TryGetRotation(float radians, out float sin, out float cos)
	{
		if (Math.Abs(radians) <= RotationEpsilon)
		{
			sin = 0f;
			cos = 1f;
			return false;
		}

		sin = MathF.Sin(radians);
		cos = MathF.Cos(radians);
		return true;
	}

	private static Vector2 Rotate(Vector2 value, float sin, float cos) =>
		new(cos * value.X - sin * value.Y, sin * value.X + cos * value.Y);

	public static SDFSample Circle(Vector2 p, float radius, float rotationRadians = 0f)
	{
		if (TryGetRotation(rotationRadians, out float sin, out float cos))
			p = Rotate(p, sin, cos);

		float len = p.Length();
		float dist = len - radius;
		Vector2 grad = len > float.Epsilon ? p / len : Vector2.UnitY;
		return new SDFSample(dist, grad);
	}

	public static SDFSample Box(Vector2 p, Vector2 halfExtents, float rotationRadians = 0f)
	{
		if (TryGetRotation(rotationRadians, out float sin, out float cos))
			p = Rotate(p, sin, cos);

		Vector2 w = new(MathF.Abs(p.X), MathF.Abs(p.Y));
		Vector2 q = w - halfExtents;
		Vector2 max = Vector2.Max(q, Vector2.Zero);
		float outside = max.Length();
		float inside = MathF.Min(MathF.Max(q.X, q.Y), 0f);
		float dist = outside + inside;

		Vector2 grad;
		if (outside > float.Epsilon)
		{
			Vector2 n = max / outside;
			grad = new Vector2(MathF.Sign(p.X) * n.X, MathF.Sign(p.Y) * n.Y);
		}
		else
		{
			if (q.X > q.Y)
				grad = new Vector2(MathF.Sign(p.X), 0f);
			else if (q.Y > q.X)
				grad = new Vector2(0f, MathF.Sign(p.Y));
			else
				grad = Vector2.UnitY;
		}

		return new SDFSample(dist, grad);
	}

	public static SDFSample RoundedBox(Vector2 p, Vector2 halfExtents, float round, float rotationRadians = 0f)
	{
		if (TryGetRotation(rotationRadians, out float sin, out float cos))
			p = Rotate(p, sin, cos);

		Vector2 w = new(MathF.Abs(p.X), MathF.Abs(p.Y));
		Vector2 q = w - halfExtents + new Vector2(round);
		Vector2 max = Vector2.Max(q, Vector2.Zero);
		float outside = max.Length() - round;
		float inside = MathF.Min(MathF.Max(q.X, q.Y), 0f);
		float dist = outside + inside;

		Vector2 grad;
		if (max.Length() > float.Epsilon)
		{
			Vector2 n = max / max.Length();
			grad = new Vector2(MathF.Sign(p.X) * n.X, MathF.Sign(p.Y) * n.Y);
		}
		else
		{
			if (q.X > q.Y)
				grad = new Vector2(MathF.Sign(p.X), 0f);
			else if (q.Y > q.X)
				grad = new Vector2(0f, MathF.Sign(p.Y));
			else
				grad = Vector2.UnitY;
		}

		return new SDFSample(dist, grad);
	}

	public static SDFSample Segment(Vector2 p, Vector2 a, Vector2 b, float rotationRadians = 0f)
	{
		if (TryGetRotation(rotationRadians, out float sin, out float cos))
		{
			p = Rotate(p, sin, cos);
			a = Rotate(a, sin, cos);
			b = Rotate(b, sin, cos);
		}

		Vector2 pa = p - a;
		Vector2 ba = b - a;
		float denom = Vector2.Dot(ba, ba);
		float h = denom > float.Epsilon ? MathHelper.Clamp(Vector2.Dot(pa, ba) / denom, 0f, 1f) : 0f;
		Vector2 closest = a + ba * h;
		Vector2 diff = p - closest;
		float len = diff.Length();
		float dist = len;
		Vector2 grad = len > float.Epsilon ? diff / len : Vector2.UnitY;
		return new SDFSample(dist, grad);
	}

	public static SDFSample Annulus(Vector2 p, float innerRadius, float outerRadius, float rotationRadians = 0f)
	{
		if (TryGetRotation(rotationRadians, out float sin, out float cos))
			p = Rotate(p, sin, cos);

		float len = p.Length();
		float dist = MathF.Max(len - outerRadius, innerRadius - len);

		Vector2 grad;
		if (len <= float.Epsilon)
			grad = Vector2.UnitY;
		else if (len > outerRadius)
			grad = p / len;
		else if (len < innerRadius)
			grad = -p / len;
		else
			grad = Vector2.UnitY;

		return new SDFSample(dist, grad);
	}

	public static SDFSample Polygon(Vector2 p, ReadOnlySpan<Vector2> vertices, float rotationRadians = 0f)
	{
		if (vertices.Length == 0)
			return new SDFSample(float.PositiveInfinity, Vector2.UnitY);

		bool rotated = TryGetRotation(rotationRadians, out float sin, out float cos);
		if (rotated)
			p = Rotate(p, sin, cos);

		float minDist = float.MaxValue;
		Vector2 closestVec = Vector2.UnitY;
		bool inside = false;

		for (int i = 0; i < vertices.Length; i++)
		{
			Vector2 vi = rotated ? Rotate(vertices[i], sin, cos) : vertices[i];
			Vector2 vj = rotated ? Rotate(vertices[(i + 1) % vertices.Length], sin, cos) : vertices[(i + 1) % vertices.Length];

			var seg = Segment(p, vi, vj);
			if (seg.Distance < minDist)
			{
				minDist = seg.Distance;
				closestVec = seg.Gradient;
			}

			bool cond = ((vi.Y > p.Y) != (vj.Y > p.Y)) &&
						(p.X < (vj.X - vi.X) * (p.Y - vi.Y) / (vj.Y - vi.Y + float.Epsilon) + vi.X);
			if (cond)
				inside = !inside;
		}

		float dist = inside ? -minDist : minDist;
		return new SDFSample(dist, closestVec);
	}
}

public static class SdfOperators
{
	public static SDFSample SmoothMin(SDFSample a, SDFSample b, float k)
	{
		if (k <= Mathematical.Epsilon)
			return a.Distance < b.Distance ? a : b;

		float h = MathHelper.Clamp(0.5f + 0.5f * (b.Distance - a.Distance) / k, 0f, 1f);
		float distance = MathHelper.Lerp(b.Distance, a.Distance, h) - k * h * (1f - h);
		Vector2 gradient = Vector2.Lerp(b.Gradient, a.Gradient, h);
		return new SDFSample(distance, gradient);
	}

	public static SDFSample Xor(SDFSample a, SDFSample b)
	{
		float min1 = MathF.Min(a.Distance, -b.Distance);
		float min2 = MathF.Min(-a.Distance, b.Distance);

		Vector2 grad1 = a.Distance < -b.Distance ? a.Gradient : -b.Gradient;
		Vector2 grad2 = -a.Distance < b.Distance ? -a.Gradient : b.Gradient;

		if (min1 > min2)
			return new SDFSample(min1, grad1);
		if (min2 > min1)
			return new SDFSample(min2, grad2);

		Vector2 blended = grad1 + grad2;
		return new SDFSample(min1, blended);
	}

	public static SDFSample Union(SDFSample a, SDFSample b)
	{
		if (a.Distance < b.Distance)
			return a;
		if (b.Distance < a.Distance)
			return b;

		Vector2 blended = a.Gradient + b.Gradient;
		return blended.LengthSquared() > Mathematical.Epsilon
			? new SDFSample(a.Distance, blended)
			: a;
	}

	public static SDFSample Intersection(SDFSample a, SDFSample b)
	{
		if (a.Distance > b.Distance)
			return a;
		if (b.Distance > a.Distance)
			return b;

		Vector2 blended = a.Gradient + b.Gradient;
		return blended.LengthSquared() > Mathematical.Epsilon
			? new SDFSample(a.Distance, blended)
			: a;
	}

	public static SDFSample Subtraction(SDFSample a, SDFSample b)
	{
		float da = a.Distance;
		float db = -b.Distance;

		if (da > db)
			return a;
		if (db > da)
			return new SDFSample(db, -b.Gradient);

		Vector2 blended = a.Gradient - b.Gradient;
		return blended.LengthSquared() > Mathematical.Epsilon
			? new SDFSample(da, blended)
			: a;
	}
}

public static class SDFTests
{
	public static bool[,] RasterizeFilledCircle(int width, int height, Vector2 center, float radius)
	{
		var field = new bool[width, height];
		for (int y = 0; y < height; y++)
			for (int x = 0; x < width; x++)
			{
				var p = new Vector2(x, y) - center;
				var a = SignedDistance.Circle(p, radius);
				var b = SignedDistance.Annulus(p, radius - 5f, radius - 2f);
				var final = SdfOperators.Xor(a, b);

				field[x, y] = final.Distance <= 0f;
			}
		return field;
	}
}

public static class WorldgenNoise
{
	private static readonly Vector2[] SimplexGradients =
	{
		new(1f, 1f), new(-1f, 1f), new(1f, -1f), new(-1f, -1f),
		new(1f, 0f), new(-1f, 0f), new(0f, 1f), new(0f, -1f)
	};

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static uint Hash(uint x)
	{
		x ^= x >> 16;
		x *= 0x7feb352d;
		x ^= x >> 15;
		x *= 0x846ca68b;
		x ^= x >> 16;
		return x;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static uint Hash(int x, int y, int seed)
	{
		uint h = Hash((uint)(x) * 0x45d9f3u ^ (uint)(y) * 0x27d4eb2du ^ (uint)seed * 0x165667b1u);
		return h;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float HashFloat(int x, int y, int seed)
	{
		return (Hash(x, y, seed) & 0xffffffu) / 16777215f;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float Lerp(float a, float b, float t) => a + (b - a) * t;

	public static float FastValue(Vector2 p, int seed = 0)
	{
		int ix = (int)MathF.Floor(p.X);
		int iy = (int)MathF.Floor(p.Y);

		float fx = ix;
		float fy = iy;

		float v00 = HashFloat(ix, iy, seed);
		float v10 = HashFloat(ix + 1, iy, seed);
		float v01 = HashFloat(ix, iy + 1, seed);
		float v11 = HashFloat(ix + 1, iy + 1, seed);

		float u = Fade(fx);
		float v = Fade(fy);

		float x0 = Lerp(v00, v10, u);
		float x1 = Lerp(v01, v11, u);
		return Lerp(x0, x1, v);
	}

	public static float FastSimplex(Vector2 p, int seed = 0)
	{
		const float F2 = 0.366025403f;   // (sqrt(3) - 1) / 2
		const float G2 = 0.211324865f;   // (3 - sqrt(3)) / 6

		float s = (p.X + p.Y) * F2;
		int i = (int)MathF.Floor(p.X + s);
		int j = (int)MathF.Floor(p.Y + s);

		float t = (i + j) * G2;
		float X0 = i - t;
		float Y0 = j - t;
		float x0 = p.X - X0;
		float y0 = p.Y - Y0;

		int i1 = x0 > y0 ? 1 : 0;
		int j1 = x0 > y0 ? 0 : 1;

		float x1 = x0 - i1 + G2;
		float y1 = y0 - j1 + G2;
		float x2 = x0 - 1f + 2f * G2;
		float y2 = y0 - 1f + 2f * G2;

		float n0 = 0f, n1 = 0f, n2 = 0f;

		int gi0 = (int)(Hash(i, j, seed) % SimplexGradients.Length);
		int gi1 = (int)(Hash(i + i1, j + j1, seed) % SimplexGradients.Length);
		int gi2 = (int)(Hash(i + 1, j + 1, seed) % SimplexGradients.Length);

		float t0 = 0.5f - x0 * x0 - y0 * y0;
		if (t0 > 0f)
		{
			t0 *= t0;
			Vector2 g = SimplexGradients[gi0];
			n0 = t0 * t0 * (g.X * x0 + g.Y * y0);
		}

		float t1 = 0.5f - x1 * x1 - y1 * y1;
		if (t1 > 0f)
		{
			t1 *= t1;
			Vector2 g = SimplexGradients[gi1];
			n1 = t1 * t1 * (g.X * x1 + g.Y * y1);
		}

		float t2 = 0.5f - x2 * x2 - y2 * y2;
		if (t2 > 0f)
		{
			t2 *= t2;
			Vector2 g = SimplexGradients[gi2];
			n2 = t2 * t2 * (g.X * x2 + g.Y * y2);
		}

		float value = 70f * (n0 + n1 + n2);
		return MathHelper.Clamp(value * 0.5f + 0.5f, 0f, 1f);
	}



	// partially based on https://lygia.xyz/generative/worley
	public static float FastCellular(Vector2 p, int seed = 0, float jitter = 0.9f)
	{
		int ix = (int)MathF.Floor(p.X);
		int iy = (int)MathF.Floor(p.Y);

		float fx = p.X - ix;
		float fy = p.Y - iy;

		float minDist = float.MaxValue;

		for (int y = -1; y <= 1; y++)
		{
			for (int x = -1; x <= 1; x++)
			{
				int cx = ix + x;
				int cy = iy + y;

				uint h = Hash(cx, cy, seed);
				float rx = ((h & 0xffu) / 255f - 0.5f) * jitter + x;
				float ry = (((h >> 8) & 0xffu) / 255f - 0.5f) * jitter + y;

				float dx = fx - rx;
				float dy = fy - ry;
				float dist = dx * dx + dy * dy;
				if (dist < minDist)
					minDist = dist;
			}
		}

		return MathHelper.Clamp(MathF.Sqrt(minDist) * 1.4142f, 0f, 1f);
	}

	public static float SimplexFbm(Vector2 p, int seed = 0, int octaves = 4, float lacunarity = 2f, float gain = 0.5f, float scale = 1f)
	{
		float amplitude = 1f;
		float frequency = 1f;
		float sum = 0f;
		float norm = 0f;

		for (int i = 0; i < octaves; i++)
		{
			sum += FastSimplex(p * frequency * scale, seed + i * 53) * amplitude;
			norm += amplitude;
			amplitude *= gain;
			frequency *= lacunarity;
		}

		return norm > 0f ? sum / norm : 0.5f;
	}

	public static float DomainWarp(Vector2 p, int seed = 0, float amplitude = 1.5f, int iterations = 2, Func<Vector2, int, float>? noise = null)
	{
		noise ??= FastSimplex;
		Vector2 warp = Vector2.Zero;
		float amp = amplitude;
		float freq = 1f;

		for (int i = 0; i < iterations; i++)
		{
			float nx = noise(p * freq + warp, seed + i * 37);
			float ny = noise(p * freq + warp + new Vector2(37.2f, 17.9f), seed + i * 37 + 1);
			warp += new Vector2(nx, ny) * amp;
			freq *= 2f;
			amp *= 0.5f;
		}

		return noise(p + warp, seed + iterations * 97);
	}
}

public class TestingModSystem : ModSystem
{
	public override void PostWorldGen()
	{
		for (int i = 0; i < Main.maxTilesX; i++)
		{
			for (int j = 0; j < Main.maxTilesY; j++)
			{
				Tile tile = Main.tile[i, j];
				if (tile != null && tile.active())
				{
					float noiseValue = WorldgenNoise.SimplexFbm(new Vector2(i, j), seed: 12345, octaves: 24, lacunarity: 2f, gain: 0.5f, scale: 0.005f);

					if (noiseValue > 0.75f)
					{
						tile.type = Terraria.ID.TileID.Gold;
					}
					else if (noiseValue > 0.5f)
					{
						tile.type = Terraria.ID.TileID.Demonite;
					}
					else if (noiseValue > 0.25f)
					{
						tile.type = Terraria.ID.TileID.Stone;
					}
					else
					{
						tile.type = Terraria.ID.TileID.Dirt;
					}


					continue;
					SDFSample sdfSample = SignedDistance.Circle(new Vector2(i - Main.maxTilesX / 2, j - Main.maxTilesY / 2), 100f);
					sdfSample = SdfOperators.SmoothMin(sdfSample, SignedDistance.RoundedBox(new Vector2(i - 130 - Main.maxTilesX / 2, j - Main.maxTilesY / 2), new Vector2(50f, 75f), 20f, rotationRadians: MathHelper.PiOver4), 10f);
					if (sdfSample.Distance < 0f)
					{
						tile.type = Terraria.ID.TileID.Gold;

						Vector2 gradient = sdfSample.Gradient;
						float angle = MathF.Atan2(gradient.Y, gradient.X);
						if (angle < 0f)
							angle += MathHelper.TwoPi;

						int paintIndex = 1 + (int)MathF.Round(angle / MathHelper.TwoPi * 29f);
						paintIndex = (int)MathHelper.Clamp(paintIndex, 1f, 29f);
						tile.color((byte)paintIndex);
					}
				}
			}
		}
		base.PostWorldGen();
	}
}
