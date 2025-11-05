using System;
using System.Threading;
using Cataphractal.Common;
using Microsoft.Xna.Framework;

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
