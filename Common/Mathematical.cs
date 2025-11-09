using System;
using Microsoft.Xna.Framework;

namespace Cataphractal.Common;

public static class Mathematical {
	public const float Epsilon = 1e-6f;
	public const float Tau = 2f * MathF.PI;

	public static float Saturate(float value) => Math.Clamp(value, 0f, 1f);

    /// <summary>
    /// Remaps a value from one range to another.
    /// </summary>
	public static float Remap(float value, float inMin, float inMax, float outMin, float outMax)
	{
		if (Math.Abs(inMax - inMin) <= Epsilon)
			return outMin;
		float t = (value - inMin) / (inMax - inMin);
		return MathHelper.Lerp(outMin, outMax, t);
	}

	public static float SmoothStep(float edge0, float edge1, float value)
	{
		float t = Saturate((value - edge0) / (edge1 - edge0));
		return t * t * (3f - 2f * t);
	}

	public static float SmootherStep(float edge0, float edge1, float value)
	{
		float t = Saturate((value - edge0) / (edge1 - edge0));
		return t * t * t * (t * (6f * t - 15f) + 10f);
	}

    /// <summary>
    /// Logistic function mapping any real value to the range (0, 1). <para/>
    /// The 'midpoint' parameter defines the value at which the output is 0.5, and 'steepness' controls the slope of the curve. <para/>
    /// Example: Logistic(0, 0, 1) = 0.5, Logistic(1, 0, 1) ~ 0.73, Logistic(-1, 0, 1) ~ 0.27 <para/>
    /// </summary>
	public static float Logistic(float value, float midpoint = 0f, float steepness = 1f)
	{
		float expo = MathF.Exp(-steepness * (value - midpoint));
		return 1f / (1f + expo);
	}
    /// <summary>
    /// Wraps a value to be within the specified range [min, max).
    /// </summary>
	public static float Wrap(float value, float min, float max)
	{
		float range = max - min;
		if (range <= Epsilon)
			return min;
		return value - range * MathF.Floor((value - min) / range);
	}

    /// <summary>
    /// Ping-pongs a value between 0 and the specified length.
    /// </summary>
	public static float PingPong(float value, float length = 1f)
	{
		length = Math.Max(Epsilon, length);
		float wrapped = Wrap(value, 0f, length * 2f);
		return length - MathF.Abs(wrapped - length);
	}

	public static float LerpAngle(float fromRadians, float toRadians, float t)
	{
		float delta = Wrap(toRadians - fromRadians, -MathF.PI, MathF.PI);
		return fromRadians + delta * t;
	}

	public static float AperiodicSine(float time, float baseFrequency = 1f, float modulationFrequency = 0.618f, float modulationDepth = 0.35f, float drift = 0.2f)
	{
		float mod = MathF.Sin(time * modulationFrequency)
			+ MathF.Sin(time * modulationFrequency * 1.7f) * modulationDepth
			+ MathF.Sin(time * 0.13f) * modulationDepth * 0.5f;

		float driftPhase = time * baseFrequency + MathF.Sin(time * 0.07f) * drift;
		float primary = MathF.Sin(driftPhase + mod);
		float secondary = MathF.Sin(driftPhase * 1.37f + mod * 0.72f);

		return primary * 0.7f + secondary * 0.3f;
	}

	public static float AperiodicCosine(float time, float baseFrequency = 1f, float modulationFrequency = 0.723f, float modulationDepth = 0.4f, float drift = 0.25f)
	{
		float mod = MathF.Sin(time * modulationFrequency)
			+ MathF.Cos(time * modulationFrequency * 1.9f) * modulationDepth
			+ MathF.Sin(time * 0.17f) * modulationDepth * 0.45f;

		float driftPhase = time * baseFrequency + MathF.Sin(time * 0.11f) * drift;
		float primary = MathF.Cos(driftPhase + mod);
		float tertiary = MathF.Cos(driftPhase * 1.12f - mod * 0.55f);

		return primary * 0.65f + tertiary * 0.35f;
	}

	public static float FractalSine(float time, float baseFrequency = 1f, int harmonics = 4, float persistence = 0.5f)
	{
		float amplitude = 1f;
		float frequency = baseFrequency;
		float sum = 0f;
		float norm = 0f;

		for (int i = 0; i < harmonics; i++)
		{
			sum += MathF.Sin(time * frequency) * amplitude;
			norm += amplitude;
			amplitude *= persistence;
			frequency *= 2f;
		}

		return norm > Epsilon ? sum / norm : 0f;
	}

	public static float FractalCosine(float time, float baseFrequency = 1f, int harmonics = 4, float persistence = 0.5f)
	{
		float amplitude = 1f;
		float frequency = baseFrequency;
		float sum = 0f;
		float norm = 0f;

		for (int i = 0; i < harmonics; i++)
		{
			sum += MathF.Cos(time * frequency) * amplitude;
			norm += amplitude;
			amplitude *= persistence;
			frequency *= 2f;
		}

		return norm > Epsilon ? sum / norm : 0f;
	}

	public static float InverseLerp(float a, float b, float value)
	{
		if (Math.Abs(b - a) <= Epsilon)
			return 0f;
		return (value - a) / (b - a);
	}

	public static float LerpClamped(float a, float b, float t) =>
		MathHelper.Lerp(a, b, Saturate(t));

        /// <summary>
        /// Approaches a target value at a maximum rate.
        /// </summary>
	public static float Approach(float current, float target, float maxDelta)
	{
		float delta = target - current;
		if (MathF.Abs(delta) <= maxDelta)
			return target;
		return current + MathF.Sign(delta) * maxDelta;
	}

    /// <summary>
    /// Performs Catmull-Rom interpolation between four points.
    /// </summary>
    /// <param name="p0"></param>
    /// <param name="p1"></param>
    /// <param name="p2"></param>
    /// <param name="p3"></param>
    /// <param name="t"></param>
    /// <returns></returns>
	public static float CatmullRom(float p0, float p1, float p2, float p3, float t)
	{
		float t2 = t * t;
		float t3 = t2 * t;
		return 0.5f * ((2f * p1) +
			(-p0 + p2) * t +
			(2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
			(-p0 + 3f * p1 - 3f * p2 + p3) * t3);
	}

    /// <summary>
    /// Performs Hermite interpolation between two points with specified tangents.
    /// </summary>
    /// <param name="p0"></param>
    /// <param name="m0"></param>
    /// <param name="p1"></param>
    /// <param name="m1"></param>
    /// <param name="t"></param>
    /// <returns></returns>
	public static float Hermite(float p0, float m0, float p1, float m1, float t)
	{
		float t2 = t * t;
		float t3 = t2 * t;
		float h00 = 2f * t3 - 3f * t2 + 1f;
		float h10 = t3 - 2f * t2 + t;
		float h01 = -2f * t3 + 3f * t2;
		float h11 = t3 - t2;
		return h00 * p0 + h10 * m0 + h01 * p1 + h11 * m1;
	}

	public static float Bias(float value, float bias)
	{
		if (bias <= Epsilon)
			return 0f;
		if (bias >= 1f - Epsilon)
			return 1f;
		return MathF.Pow(value, MathF.Log(bias, 0.5f));
	}

	public static float Gain(float value, float gain)
	{
		if (value < 0.5f)
			return 0.5f * Bias(value * 2f, gain);
		return 1f - 0.5f * Bias(2f - value * 2f, gain);
	}

	public static float SmoothMax(float a, float b, float smoothness)
	{
		float h = MathF.Max(smoothness - MathF.Abs(a - b), 0f) / smoothness;
		return MathF.Max(a, b) + h * h * smoothness * 0.25f;
	}

	public static float SmoothMin(float a, float b, float smoothness)
	{
		float h = MathF.Max(smoothness - MathF.Abs(a - b), 0f) / smoothness;
		return MathF.Min(a, b) - h * h * smoothness * 0.25f;
	}

	public static bool Approximately(float a, float b, float tolerance = Epsilon) =>
		MathF.Abs(a - b) <= tolerance;
}