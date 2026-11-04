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

    private static (double R, double G, double B) Average(Picture picture)
    {
        double r = 0, g = 0, b = 0;
        var n = picture.Width * picture.Height;

        for (var p = 0; p < n; p++)
        {
            r += picture.Rgba[p * 4];
            g += picture.Rgba[p * 4 + 1];
            b += picture.Rgba[p * 4 + 2];
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

    [Fact]
    public void DyedPaintTakesTheDyesColour()
    {
        var olive = Texture(TurbineRows.Large, "Olive");
        var (r, g, b) = Average(Paint.Recolour(olive, DuskBlue));

        // Dusk blue is bluest, then green, then red; olive was greenest and least blue.
        Assert.True(b > g && g > r, $"average {r:F0}, {g:F0}, {b:F0}");
    }

    // The tail's orange stripe is a warning mark, not paint.
    [Fact]
    public void TheOrangeStripeOnTheTailStaysOrange()
    {
        var tail = Texture(TurbineRows.Large, "Tail");
        var dyed = Paint.Recolour(tail, DuskBlue);
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
        var picture = Pixels((60, 70, 40, 255), (120, 135, 80, 255));
        var dyed = Paint.Recolour(picture, DuskBlue);

        Assert.True(dyed.Rgba[2] < dyed.Rgba[6], "the darker paint should stay darker");
        Assert.True(dyed.Rgba[6] > dyed.Rgba[4], "the lighter paint should be blue now");
    }

    // Where the paint meets a stripe the pixels are partly paint; they blend, so the edge stays soft.
    [Fact]
    public void APixelThatIsPartlyPaintIsOnlyPartlyDyed()
    {
        var edge = (R: (byte)150, G: (byte)138, B: (byte)90);
        var weight = Paint.PaintWeight(edge.R, edge.G, edge.B);

        Assert.InRange(weight, 0.3f, 0.7f);

        var dyed = Paint.Recolour(Pixels((94, 104, 62, 255), (edge.R, edge.G, edge.B, 255)), DuskBlue);

        Assert.NotEqual(edge.B, dyed.Rgba[6]);
        Assert.True(dyed.Rgba[4] > dyed.Rgba[6], $"half paint keeps half its own warm colour, got {dyed.Rgba[4]}, {dyed.Rgba[5]}, {dyed.Rgba[6]}");
    }

    [Fact]
    public void SizeAndAlphaStayAndTheSourceIsNotChanged()
    {
        var picture = Pixels((94, 104, 62, 200), (201, 111, 47, 255));
        var before = picture.Rgba.ToArray();
        var dyed = Paint.Recolour(picture, DuskBlue);

        Assert.Equal((2, 1), (dyed.Width, dyed.Height));
        Assert.Equal(200, dyed.Rgba[3]);
        Assert.Equal(before, picture.Rgba);
        Assert.Equal(before.AsSpan(4, 4).ToArray(), dyed.Rgba.AsSpan(4, 4).ToArray());
    }

    [Fact]
    public void APictureWithNoPaintComesBackTheSame()
    {
        var picture = Pixels((201, 111, 47, 255), (121, 125, 127, 255));

        Assert.Equal(picture.Rgba, Paint.Recolour(picture, DuskBlue).Rgba);
    }
}
