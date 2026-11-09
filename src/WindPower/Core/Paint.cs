using System;
using System.Collections.Generic;

namespace WindPower.Core;

/// <summary>A dye colour as the game stores it: red, green and blue from 0 to 1.</summary>
public readonly record struct DyeColour(float R, float G, float B);

/// <summary>
/// Gets a turbine's olive paint ready for the game's dye shader, SL/LitDye (game 1.1.20151). That shader takes the
/// albedo (base map times base colour, in linear light), a mask (its red channel, 1 = dyed) and the dye colour, and
/// draws <c>lerp(albedo, saturate(2 * luma(albedo)) * dye, mask * dye.a)</c>, where luma weighs red, green and blue
/// 0.299, 0.587 and 0.114. So a dyed part needs a grey base: its brightness, doubled, is how bright the dye shows.
/// <see cref="Mask"/> marks the paint; <see cref="DyeBase"/> turns the paint grey, keeping its light and shade. The
/// orange stripes, the rust and the pale chips are other colours, so they stay as they are.
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

    /// <summary>
    /// How bright the dye shows on average, as the shader's <c>saturate(2 * luma)</c>. Under the solar panel's mask
    /// half the pixels show the dye at full strength and the grime brings the average to 0.78; the turbine's paint is
    /// cleaner, so 0.9 keeps it as bright with room for its shading.
    /// </summary>
    public const float DyeLevel = 0.9f;

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

    /// <summary>The dye mask for the shader: each pixel's paint weight in red, green and blue, alpha 255. Stored linear.</summary>
    public static Picture Mask(Picture picture)
    {
        var pixels = picture.Width * picture.Height;
        var mask = new byte[pixels * 4];

        for (var p = 0; p < pixels; p++)
        {
            var value = (byte)Math.Clamp((int)MathF.Round(Weight(picture.Rgba, p) * 255f), 0, 255);

            mask[p * 4] = mask[p * 4 + 1] = mask[p * 4 + 2] = value;
            mask[p * 4 + 3] = 255;
        }

        return picture with { Rgba = mask };
    }

    /// <summary>
    /// The base map for the dyed materials: the paint turned grey, as bright as <see cref="DyeLevel"/> asks on
    /// average and lighter or darker where the paint was, and partly-paint pixels only partly grey. Everything else,
    /// and alpha, stays as it was. The picture is sRGB, as the game reads base maps.
    /// </summary>
    public static Picture DyeBase(Picture picture)
    {
        var pixels = picture.Width * picture.Height;
        var source = picture.Rgba;
        var weights = new float[pixels];
        double weighted = 0, lumaTotal = 0;

        for (var p = 0; p < pixels; p++)
        {
            weights[p] = Weight(source, p);
            weighted += weights[p];
            lumaTotal += Luma(source, p) * weights[p];
        }

        var result = (byte[])source.Clone();

        if (weighted <= 0) return picture with { Rgba = result };

        var average = (float)(lumaTotal / weighted);

        for (var p = 0; p < pixels; p++)
        {
            var weight = weights[p];

            if (weight <= 0f) continue;

            // The shader shows the dye at 2 * luma, so a grey of linear value level / 2 shows it at that level.
            var level = average > 0f ? Math.Min(1f, Luma(source, p) / average * DyeLevel) : DyeLevel;
            var grey = ToSrgb(level / 2f) * 255f;

            for (var c = 0; c < 3; c++)
            {
                var from = source[p * 4 + c];

                result[p * 4 + c] = (byte)Math.Clamp((int)MathF.Round(from + (grey - from) * weight), 0, 255);
            }
        }

        return picture with { Rgba = result };
    }

    /// <summary>
    /// What SL/LitDye draws for one pixel before lighting, in sRGB bytes: the base pixel, the mask byte and the dye,
    /// at full strength. It is the shader's formula, here so the tests can check the look the game will give.
    /// </summary>
    public static (byte R, byte G, byte B) Shaded(byte r, byte g, byte b, byte mask, DyeColour dye)
    {
        float red = ToLinear(r / 255f), green = ToLinear(g / 255f), blue = ToLinear(b / 255f);
        var level = Math.Clamp(2f * (0.299f * red + 0.587f * green + 0.114f * blue), 0f, 1f);
        var k = mask / 255f;

        static byte Out(float from, float to, float k) => (byte)Math.Clamp((int)MathF.Round(ToSrgb(from + (to - from) * k) * 255f), 0, 255);

        // The dye colour is a material colour, which Unity turns from sRGB to linear before the shader sees it.
        return (Out(red, level * ToLinear(dye.R), k), Out(green, level * ToLinear(dye.G), k), Out(blue, level * ToLinear(dye.B), k));
    }

    /// <summary>
    /// The icon as the dye shader would colour it: the paint made grey as on the dyed model, then the dye laid over it
    /// through the paint mask. Everything else, and alpha, stays as it was.
    /// </summary>
    public static IEnumerable<Picture> DyedIcons(Picture icon, IEnumerable<DyeColour> dyes)
    {
        var grey = DyeBase(icon).Rgba;
        var mask = Mask(icon).Rgba;

        foreach (var dye in dyes) yield return Dyed(icon, grey, mask, dye);
    }

    private static Picture Dyed(Picture icon, byte[] grey, byte[] mask, DyeColour dye)
    {
        var result = (byte[])icon.Rgba.Clone();

        for (var p = 0; p < icon.Width * icon.Height; p++)
        {
            if (mask[p * 4] == 0) continue;

            var (r, g, b) = Shaded(grey[p * 4], grey[p * 4 + 1], grey[p * 4 + 2], mask[p * 4], dye);

            result[p * 4] = r;
            result[p * 4 + 1] = g;
            result[p * 4 + 2] = b;
        }

        return icon with { Rgba = result };
    }

    private static float Weight(byte[] rgba, int p) => PaintWeight(rgba[p * 4], rgba[p * 4 + 1], rgba[p * 4 + 2]);

    // Brightness in linear light, as the shader measures it.
    private static float Luma(byte[] rgba, int p) =>
        0.299f * ToLinear(rgba[p * 4] / 255f) + 0.587f * ToLinear(rgba[p * 4 + 1] / 255f) + 0.114f * ToLinear(rgba[p * 4 + 2] / 255f);

    private static float ToLinear(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);

    private static float ToSrgb(float c) => c <= 0.0031308f ? c * 12.92f : 1.055f * MathF.Pow(c, 1f / 2.4f) - 0.055f;

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
