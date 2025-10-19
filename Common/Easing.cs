using System;
using Microsoft.Xna.Framework;

namespace Cataphract.Common.Easing;

public static class Easing
{
    public static float InOutSine(float t)
    {
        return -0.5f * (MathF.Cos(MathF.PI * t) - 1f);
    }

    public static float PiecewiseLinearLerp(float value, params (float point, float time)[] segments)
    {
        if (segments.Length < 2)
            return 0f;

        float totalTime = 0f;
        for (int i = 1; i < segments.Length; i++)
        {
            totalTime += segments[i].time;
        }

        float currentTime = value * totalTime;
        float accumulatedTime = 0f;

        for (int i = 0; i < segments.Length - 1; i++)
        {
            float segmentTime = segments[i + 1].time;

            if (currentTime >= accumulatedTime && currentTime <= accumulatedTime + segmentTime)
            {
                float localT = (currentTime - accumulatedTime) / segmentTime;
                return MathHelper.Lerp(segments[i].point, segments[i + 1].point, localT);
            }

            accumulatedTime += segmentTime;
        }

        return segments[^1].point;
    }
}