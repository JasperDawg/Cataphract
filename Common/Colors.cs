using System;
using Cataphractal.Common;
using Microsoft.Xna.Framework;

namespace Cataphract.Common;

public readonly struct HSLColor
{
    public float Hue { get; }
    public float Saturation { get; }
    public float Lightness { get; }

    public HSLColor(float hue, float saturation, float lightness)
    {
        Hue = WrapHue(hue);
        Saturation = MathHelper.Clamp(saturation, 0f, 1f);
        Lightness = MathHelper.Clamp(lightness, 0f, 1f);
    }

    public static HSLColor FromColor(Color color)
    {
        float r = color.R / 255f;
        float g = color.G / 255f;
        float b = color.B / 255f;

        float max = Math.Max(r, Math.Max(g, b));
        float min = Math.Min(r, Math.Min(g, b));
        float chroma = max - min;

        float hue = 0f;
        if (chroma > Mathematical.Epsilon)
        {
            if (max == r)
                hue = (g - b) / chroma % 6f;
            else if (max == g)
                hue = 2f + (b - r) / chroma;
            else
                hue = 4f + (r - g) / chroma;

            hue *= 60f;
            if (hue < 0f)
                hue += 360f;
        }

        float light = (max + min) * 0.5f;
        float sat = chroma <= Mathematical.Epsilon ? 0f : chroma / (1f - Math.Abs(2f * light - 1f));

        return new HSLColor(hue, sat, light);
    }

    public Color ToColor()
    {
        if (Saturation <= Mathematical.Epsilon)
        {
            byte grey = (byte)MathHelper.Clamp(Lightness * 255f, 0f, 255f);
            return new Color(grey, grey, grey, 255);
        }

        float c = (1f - Math.Abs(2f * Lightness - 1f)) * Saturation;
        float hueSegment = Hue / 60f;
        float x = c * (1f - Math.Abs(hueSegment % 2f - 1f));

        float r = 0f, g = 0f, b = 0f;
        if (hueSegment < 1f) { r = c; g = x; }
        else if (hueSegment < 2f) { r = x; g = c; }
        else if (hueSegment < 3f) { g = c; b = x; }
        else if (hueSegment < 4f) { g = x; b = c; }
        else if (hueSegment < 5f) { r = x; b = c; }
        else { r = c; b = x; }

        float m = Lightness - c * 0.5f;
        r += m;
        g += m;
        b += m;

        byte ToByte(float channel) => (byte)MathHelper.Clamp(channel * 255f, 0f, 255f);
        return new Color(ToByte(r), ToByte(g), ToByte(b), 255);
    }

    public HSLColor WithHue(float hue) => new HSLColor(hue, Saturation, Lightness);
    public HSLColor WithSaturation(float saturation) => new HSLColor(Hue, saturation, Lightness);
    public HSLColor WithLightness(float lightness) => new HSLColor(Hue, Saturation, lightness);

    private static float WrapHue(float hue)
    {
        hue %= 360f;
        if (hue < 0f)
            hue += 360f;
        return hue;
    }
}

public static class ColorExtensions
{
    public static Color ToComplement(this Color color) => HSLColor.FromColor(color).Complement().ToColor();

    public static (Color left, Color right) TriadPair(this Color color)
    {
        var (left, right) = HSLColor.FromColor(color).TriadPair();
        return (left.ToColor(), right.ToColor());
    }

    public static HSLColor ToHSL(this Color color) => HSLColor.FromColor(color);

    public static HSLColor Complement(this HSLColor color) => color.WithHue(color.Hue + 180f);

    public static (HSLColor left, HSLColor right) TriadPair(this HSLColor color)
    {
        HSLColor left = color.WithHue(color.Hue + 120f);
        HSLColor right = color.WithHue(color.Hue + 240f);
        return (left, right);
    }
}
