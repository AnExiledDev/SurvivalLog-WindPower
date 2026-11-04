using WindPower.Core;
using Xunit;

namespace WindPower.Tests;

public class PaintTests
{
    private static byte[] Asset(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "assets", name));

    private static readonly DyeColour DuskBlue = Paint.GameDyes[3];

    private static Model Load(TurbineKind kind) => Glb.Read(Asset(kind.ModelFile), kind.Movers);

    private static Picture Texture(TurbineKind kind, string material)
    {
        var model = Load(kind);
        var found = model.Materials.Single(m => m.Name == material);

        return Png.Decode(model.Images[found.Image!.Value]);
    }

    private static Picture Pixels(params (byte R, byte G, byte B, byte A)[] pixels) =>
        new(pixels.Length, 1, pixels.SelectMany(p => new[] { p.R, p.G, p.B, p.A }).ToArray());

    // What the game draws for a whole picture once it is dyed: our grey base and mask through the dye shader.
    private static Picture Dyed(Picture picture, DyeColour dye)
    {
        var greyBase = Paint.DyeBase(picture);
        var mask = Paint.Mask(picture);
        var rgba = (byte[])greyBase.Rgba.Clone();

        for (var p = 0; p < picture.Width * picture.Height; p++)
        {
            var (r, g, b) = Paint.Shaded(greyBase.Rgba[p * 4], greyBase.Rgba[p * 4 + 1], greyBase.Rgba[p * 4 + 2], mask.Rgba[p * 4], dye);

            (rgba[p * 4], rgba[p * 4 + 1], rgba[p * 4 + 2]) = (r, g, b);
        }

        return picture with { Rgba = rgba };
    }

    private static (double R, double G, double B) Average(Picture picture, Func<int, bool>? where = null)
    {
        double r = 0, g = 0, b = 0;
        var n = 0;

        for (var p = 0; p < picture.Width * picture.Height; p++)
        {
            if (where != null && !where(p)) continue;

            r += picture.Rgba[p * 4];
            g += picture.Rgba[p * 4 + 1];
            b += picture.Rgba[p * 4 + 2];
            n++;
        }

        return (r / n, g / n, b / n);
    }

    // The solar panels' palettes hold eight dyes, and the game picks a variant by slot 1 to 8.
    [Fact]
    public void ThereIsOneColourForEachOfTheGamesEightDyes()
    {
        Assert.Equal(8, Paint.GameDyes.Count);
        Assert.Equal(8, Paint.GameDyes.Distinct().Count());
    }

    [Fact]
    public void OlivePaintCountsAsPaintAndOrangeRustGreyAndWhiteDoNot()
    {
        Assert.True(Paint.PaintWeight(94, 104, 62) > 0.9f);
        Assert.Equal(0f, Paint.PaintWeight(201, 111, 47));
        Assert.Equal(0f, Paint.PaintWeight(140, 70, 40));
        Assert.Equal(0f, Paint.PaintWeight(121, 125, 127));
        Assert.Equal(0f, Paint.PaintWeight(230, 230, 228));
    }

    // Every turbine has paint the dyes can change, and the parts that are not painted keep their look.
    [Theory]
    [InlineData("Small")]
    [InlineData("Medium")]
    [InlineData("Large")]
    public void EachTurbineHasPaintedPartsAndLeavesTheOthersAlone(string size)
    {
        var kind = TurbineRows.All.Single(k => k.Size == size);
        var model = Load(kind);
        var painted = model.Materials.Where(m => m.Image is int i && Paint.PaintShare(Png.Decode(model.Images[i])) >= Paint.MinimumShare).Select(m => m.Name).ToList();

        Assert.Contains("Olive", painted);
        Assert.DoesNotContain("Orange", painted);
        Assert.DoesNotContain("Steel", painted);
        Assert.DoesNotContain("Concrete", painted);
        Assert.DoesNotContain("Slate", painted);
        Assert.DoesNotContain("Blade", painted);
    }

    // The shader reads the mask's red channel: full on the paint, nothing on the stripe.
    [Fact]
    public void TheMaskIsFullOnPaintAndEmptyElsewhere()
    {
        var mask = Paint.Mask(Pixels((94, 104, 62, 100), (201, 111, 47, 255)));

        Assert.True(mask.Rgba[0] > 230, $"paint mask {mask.Rgba[0]}");
        Assert.Equal(255, mask.Rgba[3]);
        Assert.Equal(0, mask.Rgba[4]);
    }

    // The dye shader's own formula: no mask leaves a pixel alone, and a mid grey (linear 0.5) shows the dye as it is.
    [Fact]
    public void TheShaderShowsTheDyeAtTwiceTheBasesBrightness()
    {
        Assert.Equal(((byte)94, (byte)104, (byte)62), Paint.Shaded(94, 104, 62, 0, DuskBlue));

        var (r, g, b) = Paint.Shaded(188, 188, 188, 255, DuskBlue);

        Assert.InRange(r, DuskBlue.R * 255 - 3, DuskBlue.R * 255 + 3);
        Assert.InRange(g, DuskBlue.G * 255 - 3, DuskBlue.G * 255 + 3);
        Assert.InRange(b, DuskBlue.B * 255 - 3, DuskBlue.B * 255 + 3);
    }

    [Fact]
    public void DyedPaintTakesTheDyesColourAtAboutItsBrightness()
    {
        var olive = Texture(TurbineRows.Large, "Olive");
        var paint = new Func<int, bool>(p => Paint.PaintWeight(olive.Rgba[p * 4], olive.Rgba[p * 4 + 1], olive.Rgba[p * 4 + 2]) > 0.9f);
        var (r, g, b) = Average(Dyed(olive, DuskBlue), paint);

        // Dusk blue is bluest, then green, then red; olive was greenest and least blue.
        Assert.True(b > g && g > r, $"average {r:F0}, {g:F0}, {b:F0}");
        // At the dye level the paint shows about as bright as the dye itself, not dark like olive run through the shader.
        Assert.InRange(b, DuskBlue.B * 255 * 0.8, DuskBlue.B * 255 * 1.05);
    }

    // Even paint shows the dye at exactly the dye level: the grey is chosen for the shader's doubling, in linear light.
    [Fact]
    public void EvenPaintShowsTheDyeAtTheDyeLevel()
    {
        static double Linear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        static double Srgb(double c) => c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;

        var dyed = Dyed(Pixels((94, 104, 62, 255), (94, 104, 62, 255)), DuskBlue);
        var expected = Srgb(Paint.DyeLevel * Linear(DuskBlue.B)) * 255;

        Assert.InRange(dyed.Rgba[2], expected - 2, expected + 2);
    }

    // The tail's orange stripe is a warning mark, not paint.
    [Fact]
    public void TheOrangeStripeOnTheTailStaysOrange()
    {
        var tail = Texture(TurbineRows.Large, "Tail");
        var dyed = Dyed(tail, DuskBlue);
        var stripe = 0;

        for (var p = 0; p < tail.Width * tail.Height; p++)
        {
            if (Paint.PaintWeight(tail.Rgba[p * 4], tail.Rgba[p * 4 + 1], tail.Rgba[p * 4 + 2]) > 0f) continue;

            stripe++;
            Assert.Equal(tail.Rgba.AsSpan(p * 4, 4).ToArray(), dyed.Rgba.AsSpan(p * 4, 4).ToArray());
        }

        Assert.True(stripe > tail.Width * 4, "the tail texture should have an unpainted stripe");
    }

    [Fact]
    public void ShadingSurvivesTheDye()
    {
        var dyed = Dyed(Pixels((60, 70, 40, 255), (120, 135, 80, 255)), DuskBlue);

        Assert.True(dyed.Rgba[2] < dyed.Rgba[6], "the darker paint should stay darker");
        Assert.True(dyed.Rgba[6] > dyed.Rgba[4], "the lighter paint should be blue now");
    }

    // The grey base is grey: the dye shader takes only its brightness, so any colour left in it would be lost anyway.
    [Fact]
    public void ThePaintInTheDyeBaseIsGrey()
    {
        var greyBase = Paint.DyeBase(Pixels((94, 104, 62, 255), (120, 135, 80, 255)));

        Assert.Equal(greyBase.Rgba[0], greyBase.Rgba[1]);
        Assert.Equal(greyBase.Rgba[1], greyBase.Rgba[2]);
        Assert.True(greyBase.Rgba[0] < greyBase.Rgba[4], "the lighter paint should be the lighter grey");
    }

    // Where the paint meets a stripe the pixels are partly paint; they blend, so the edge stays soft.
    [Fact]
    public void APixelThatIsPartlyPaintIsOnlyPartlyDyed()
    {
        var edge = (R: (byte)150, G: (byte)138, B: (byte)90);
        var weight = Paint.PaintWeight(edge.R, edge.G, edge.B);

        Assert.InRange(weight, 0.3f, 0.7f);

        var dyed = Dyed(Pixels((94, 104, 62, 255), (edge.R, edge.G, edge.B, 255)), DuskBlue);

        // Warmth is red less blue: the edge's own is +60, fully dyed paint's is below zero, and a half-dyed edge sits between.
        var full = dyed.Rgba[0] - dyed.Rgba[2];
        var half = dyed.Rgba[4] - dyed.Rgba[6];

        Assert.True(full < half && half < edge.R - edge.B - 20, $"full paint {full}, edge {half}, edge before {edge.R - edge.B}");
    }

    [Fact]
    public void SizeAndAlphaStayAndTheSourceIsNotChanged()
    {
        var picture = Pixels((94, 104, 62, 200), (201, 111, 47, 255));
        var before = picture.Rgba.ToArray();
        var greyBase = Paint.DyeBase(picture);

        Assert.Equal((2, 1), (greyBase.Width, greyBase.Height));
        Assert.Equal(200, greyBase.Rgba[3]);
        Assert.Equal(before, picture.Rgba);
        Assert.Equal(before.AsSpan(4, 4).ToArray(), greyBase.Rgba.AsSpan(4, 4).ToArray());
    }

    [Fact]
    public void APictureWithNoPaintComesBackTheSame()
    {
        var picture = Pixels((201, 111, 47, 255), (121, 125, 127, 255));

        Assert.Equal(picture.Rgba, Paint.DyeBase(picture).Rgba);
        Assert.Equal(picture.Rgba, Dyed(picture, DuskBlue).Rgba);
    }
}
