using System;
using System.Collections.Generic;

namespace WindPower.Core;

/// <summary>A dye colour as the game stores it: red, green and blue from 0 to 1.</summary>
public readonly record struct DyeColour(float R, float G, float B);

/// <summary>
/// Recolours a turbine's olive paint for the game's furniture dyes. Only the paint changes: the orange stripes, the
/// rust and the pale chips are other colours, so they stay as they are. The paint's light and shade are kept, scaled
/// so its average sits where the game's own dyed solar panel frames do.
/// </summary>
public static class Paint
{
    /// <summary>The game's eight dyes, in slot order, as the solar panels carry them (game 1.1.20151).</summary>
    public static readonly IReadOnlyList<DyeColour> GameDyes = new DyeColour[]
    {
        new(0.616f, 0.722f, 0.541f),
        new(0.784f, 0.439f, 0.247f),
        new(0.910f, 0.831f, 0.561f),
        new(0.420f, 0.478f, 0.588f),
        new(0.910f, 0.706f, 0.753f),
        new(0.902f, 0.871f, 0.784f),
        new(0.639f, 0.600f, 0.678f),
        new(0.647f, 0.361f, 0.302f),
    };

    /// <summary>A picture with less paint than this share has no dyed look; the dye leaves that part alone.</summary>
    public const float MinimumShare = 0.05f;

    // The light grey under the solar panel's dye mask averages 193 of 255; a dyed part is about that bright.
    private const float Brightness = 193f / 255f;

    /// <summary>How much of a pixel is olive paint, from 0 (none) to 1: yellow-green hues with some colour in them.</summary>
    public static float PaintWeight(byte r, byte g, byte b)
    {
        var (hue, saturation) = HueAndSaturation(r, g, b);

        return Smooth(40f, 55f, hue) * (1f - Smooth(110f, 130f, hue)) * Smooth(0.12f, 0.22f, saturation);
    }

    /// <summary>The share of a picture that is paint, weighted.</summary>
    public static float PaintShare(Picture picture)
    {
        var pixels = picture.Width * picture.Height;

        if (pixels == 0) return 0f;

        var total = 0f;

        for (var p = 0; p < pixels; p++) total += Weight(picture.Rgba, p);

        return total / pixels;
    }

    /// <summary>The picture with its paint in the dye's colour. Alpha and everything that is not paint stay as they were.</summary>
    public static Picture Recolour(Picture picture, DyeColour dye)
    {
        var pixels = picture.Width * picture.Height;
        var source = picture.Rgba;
        var weights = new float[pixels];
        double weighted = 0, lightTotal = 0;

        for (var p = 0; p < pixels; p++)
        {
            weights[p] = Weight(source, p);
            weighted += weights[p];
            lightTotal += Light(source, p) * weights[p];
        }

        var result = (byte[])source.Clone();

        if (weighted <= 0) return picture with { Rgba = result };

        var average = (float)(lightTotal / weighted);
        var channels = new[] { dye.R, dye.G, dye.B };

        for (var p = 0; p < pixels; p++)
        {
            var weight = weights[p];

            if (weight <= 0f) continue;

            var shade = average > 0f ? Math.Min(1f, Light(source, p) / average * Brightness) : Brightness;

            for (var c = 0; c < 3; c++)
            {
                var from = source[p * 4 + c];
                var to = channels[c] * shade * 255f;

                result[p * 4 + c] = (byte)Math.Clamp((int)MathF.Round(from + (to - from) * weight), 0, 255);
            }
        }

        return picture with { Rgba = result };
    }

    private static float Weight(byte[] rgba, int p) => PaintWeight(rgba[p * 4], rgba[p * 4 + 1], rgba[p * 4 + 2]);

    private static float Light(byte[] rgba, int p) => 0.299f * rgba[p * 4] + 0.587f * rgba[p * 4 + 1] + 0.114f * rgba[p * 4 + 2];

    private static (float Hue, float Saturation) HueAndSaturation(byte r, byte g, byte b)
    {
        float red = r / 255f, green = g / 255f, blue = b / 255f;
        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var range = max - min;

        if (max <= 0f || range <= 0f) return (0f, 0f);

        float hue;

        if (max == red) hue = 60f * ((green - blue) / range % 6f);
        else if (max == green) hue = 60f * ((blue - red) / range + 2f);
        else hue = 60f * ((red - green) / range + 4f);

        if (hue < 0f) hue += 360f;

        return (hue, range / max);
    }

    private static float Smooth(float from, float to, float x)
    {
        var t = Math.Clamp((x - from) / (to - from), 0f, 1f);

        return t * t * (3f - 2f * t);
    }
}
