using WindPower.Core;
using Xunit;

namespace WindPower.Tests;

// The crafting card offers a colour only when a recipe of the plain recipe's materials plus one dye crafts a package of
// the same furniture, so each turbine needs those rows, at ids nothing else uses, copied from the matching solar panel.
public class DyesTests
{
    private static readonly int[] Materials = { 20004, 20004, 20104 };

    private static Dictionary<string, object> Fields(RowPlan row) => row.Fields.ToDictionary(field => field.Name, field => field.Value);

    [Fact]
    public void EachColourRecipeIsThePlainMaterialsPlusItsOneDye()
    {
        foreach (var kind in TurbineRows.All)
        foreach (var dye in Dyes.All)
        {
            var recipe = Fields(Dyes.Rows(kind, dye, Materials).Single(row => row.Table == Table.Recipe));
            var made = (int[])recipe["ProductID"];

            Assert.Equal(Materials.Append(dye.Item), (int[])recipe["MaterialList"]);
            Assert.Equal(new[] { Dyes.Ids(kind.Ids, dye).Item }, made);
        }
    }

    [Fact]
    public void EachColourPackageUnpacksIntoThePlainTurbineThroughItsOwnAction()
    {
        foreach (var kind in TurbineRows.All)
        foreach (var dye in Dyes.All)
        {
            var ids = Dyes.Ids(kind.Ids, dye);
            var rows = Dyes.Rows(kind, dye, Materials);
            var item = Fields(rows.Single(row => row.Table == Table.Item));
            var action = Fields(rows.Single(row => row.Table == Table.Action));

            Assert.Equal(kind.Ids.Furniture, item["TargetFurnitureID"]);
            Assert.Equal(ids.Action, item["UseAction"]);
            Assert.Equal(ids.Effect, action["EffectConfigID"]);
            Assert.NotEqual(kind.Ids.Action, ids.Action);
        }
    }

    [Fact]
    public void NoTwoRowsShareAnIdAndNoneTakesAPlainTurbinesId()
    {
        var plain = TurbineRows.All.SelectMany(kind => new[] { (Table.Item, kind.Ids.Item), (Table.Recipe, kind.Ids.Recipe), (Table.Action, kind.Ids.Action), (Table.Effect, kind.Ids.Effect) });
        var dyed = TurbineRows.All.SelectMany(kind => Dyes.All.SelectMany(dye => Dyes.Rows(kind, dye, Materials).Select(row => (row.Table, row.Id)))).ToList();

        Assert.Equal(dyed.Count, dyed.Distinct().Count());
        Assert.Empty(dyed.Intersect(plain));
    }

    // From the Data Dump (game 1.1.20719): recipe 1648 makes 14748, the Sage Green Small Solar Panel Package, and
    // recipe 1671 makes 14771, the Ochre Red Large one, which unpacks through action 100014771.
    [Fact]
    public void TheRowsAreCopiedFromTheSolarPanelOfTheSameSizeAndColour()
    {
        var sageSmall = Dyes.StockIds(TurbineRows.Small, Dyes.All[0]);
        var ochreLarge = Dyes.StockIds(TurbineRows.Large, Dyes.All[7]);

        Assert.Equal((14748, 1648), (sageSmall.Item, sageSmall.Recipe));
        Assert.Equal((14771, 1671, 100014771), (ochreLarge.Item, ochreLarge.Recipe, ochreLarge.Action));
        Assert.Equal(20370, Dyes.All[7].Item);
    }

    [Fact]
    public void APackageIsNamedForItsColour()
    {
        var texts = Dyes.Texts(TurbineRows.Large, Dyes.All[3]);

        Assert.Equal("Dusk Blue Large Wind Turbine Package", texts[0].En);
        Assert.Equal("暮蓝大型风力发电机包裹", texts[0].Zh);
        Assert.Equal("Unpack Dusk Blue Large Wind Turbine Package", texts[1].En);
    }

    [Fact]
    public void AColourPackageIsFoundFromItsId()
    {
        var ids = Dyes.Ids(TurbineRows.Medium.Ids, Dyes.All[4]);

        Assert.Equal((TurbineRows.Medium, Dyes.All[4]), Dyes.OfPackage(ids.Item, TurbineRows.All));
        Assert.Null(Dyes.OfPackage(TurbineRows.Medium.Ids.Item, TurbineRows.All));
    }

    [Fact]
    public void UninstallTurnsAColourPackageIntoTheSolarPanelPackageOfThatColour()
    {
        var ids = Dyes.Ids(TurbineRows.Large.Ids, Dyes.All[7]);

        Assert.Equal(14771, Uninstall.StockItem(ids.Item, TurbineRows.All));
    }

    [Fact]
    public void ADyeInTheMaterialsSettingIsRefused()
    {
        var (materials, problem) = MaterialList.Choose("20004:2, 20366:1", TurbineRows.Medium, _ => true, _ => null, Array.Empty<(TurbineKind, IReadOnlyList<int>, bool)>());

        Assert.NotNull(problem);
        Assert.Equal(MaterialList.Standard(TurbineRows.Medium.Defaults.Materials), materials);
    }

    [Fact]
    public void NoTurbinesDefaultMaterialsHoldADye()
    {
        foreach (var kind in TurbineRows.All) Assert.DoesNotContain(MaterialList.Standard(kind.Defaults.Materials), Dyes.IsDye);
    }

    // The coloured icon is the plain one put through the dye shader: the olive paint takes the dye, and the rest of
    // the picture, and every pixel's transparency, stays as it was.
    [Fact]
    public void AColourIconRecoloursOnlyThePaint()
    {
        var plain = Png.Decode(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "assets", TurbineRows.Large.IconFile)));
        var mask = Paint.Mask(plain).Rgba;
        var blue = Paint.DyedIcons(plain, new[] { Dyes.All[3].Colour }).Single();
        int changedPaint = 0, paint = 0;

        for (var p = 0; p < plain.Width * plain.Height; p++)
        {
            Assert.Equal(plain.Rgba[p * 4 + 3], blue.Rgba[p * 4 + 3]);

            if (mask[p * 4] == 0)
            {
                Assert.Equal((plain.Rgba[p * 4], plain.Rgba[p * 4 + 1], plain.Rgba[p * 4 + 2]), (blue.Rgba[p * 4], blue.Rgba[p * 4 + 1], blue.Rgba[p * 4 + 2]));

                continue;
            }

            if (mask[p * 4] < 200 || plain.Rgba[p * 4 + 3] == 0) continue;

            paint++;

            // Dusk Blue has more blue than red; the olive paint had more red than blue.
            if (blue.Rgba[p * 4 + 2] > blue.Rgba[p * 4]) changedPaint++;
        }

        Assert.True(paint > 1000, $"only {paint} painted pixels in the icon");
        Assert.True(changedPaint > paint * 9 / 10, $"{changedPaint} of {paint} painted pixels turned blue");
    }

    [Fact]
    public void AnEncodedPictureDecodesToTheSamePixels()
    {
        var picture = new Picture(3, 2, Enumerable.Range(0, 24).Select(i => (byte)(i * 10)).ToArray());

        var back = Png.Decode(Png.Encode(picture));

        Assert.Equal((3, 2), (back.Width, back.Height));
        Assert.Equal(picture.Rgba, back.Rgba);
    }
}
