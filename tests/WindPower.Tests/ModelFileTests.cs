using System.IO.Compression;
using System.Numerics;
using WindPower.Core;
using Xunit;

namespace WindPower.Tests;

public class ModelFileTests
{
    private static byte[] Asset(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "assets", name));

    private static readonly IReadOnlyDictionary<string, Vector3> Movers = TurbineRows.Large.Movers;

    private static readonly Model Turbine = Glb.Read(Asset(TurbineRows.Large.ModelFile), Movers);

    private static readonly Model Medium = Glb.Read(Asset(TurbineRows.Medium.ModelFile), TurbineRows.Medium.Movers);

    private static readonly Model Small = Glb.Read(Asset(TurbineRows.Small.ModelFile), TurbineRows.Small.Movers);

    private static Model Load(string size) => size switch { "Medium" => Medium, "Small" => Small, _ => Turbine };

    private static ModelPart Part(string name) => Turbine.Parts.Single(part => part.Name == name);

    [Fact]
    public void TheTurbineSplitsIntoTheStillBodyTheTurningTopAndTheRotor()
    {
        Assert.Equal(new[] { Glb.BodyName, "Head", "Rotor" }, Turbine.Parts.Select(part => part.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.All(Turbine.Parts, part => Assert.NotEmpty(part.Meshes));
    }

    // The rotor rides on the head, so when the top swings into a gust it swings with it.
    [Fact]
    public void TheRotorRidesOnTheHead()
    {
        Assert.Equal("Head", Part("Rotor").Parent);
        Assert.Null(Part("Head").Parent);
    }

    // The tail is not a part of its own: it is fixed to the head and only moves when the whole top does.
    [Fact]
    public void TheTailIsFixedToTheHead()
    {
        // A part's points are measured from its pivot, the top of the mast.
        var reach = Part("Head").Meshes.SelectMany(mesh => mesh.Positions).Max(point => new Vector2(point.X, point.Z).Length());

        Assert.DoesNotContain(Turbine.Parts, part => part.Name == "Tail");
        Assert.True(reach > 0.6f, $"the head's meshes reach only {reach} m from the mast, so the tail is missing from it");
    }

    [Fact]
    public void TheTopTurnsAboutTheUprightAtopTheMast()
    {
        var head = Part("Head");

        Assert.InRange(head.Pivot.Y, 3.8f, 4.7f);
        Assert.InRange(Math.Abs(head.Pivot.X) + Math.Abs(head.Pivot.Z), 0f, 0.05f);
        Assert.InRange(head.Axis.Y, 0.99f, 1.01f);
    }

    // The rotor hub sits on top of the mast, a blade's length below the tips (the export scales the model to about
    // 0.87, so the hub is at about 4.3 m and the tips reach about 6 m), and turns about a level axis.
    [Fact]
    public void TheRotorTurnsAboutALevelAxisAtTheTopOfTheMast()
    {
        var rotor = Part("Rotor");

        Assert.InRange(rotor.Pivot.Y, 4.0f, 5.0f);
        Assert.InRange(Turbine.Bounds().Max.Y - rotor.Pivot.Y, 1.2f, 2.0f);
        Assert.InRange(rotor.Axis.Length(), 0.99f, 1.01f);
        Assert.InRange(Math.Abs(rotor.Axis.Y), 0f, 0.05f);
    }

    [Fact]
    public void TheModelStandsOnTheGroundAboutSixMetresTallAndFitsAroundTheSlot()
    {
        var (min, max) = Turbine.Bounds();

        Assert.InRange(min.Y, -0.01f, 0.01f);
        Assert.InRange(max.Y, 5.5f, 6.5f);
        Assert.InRange(max.X - min.X, 1.5f, 3.5f);
        Assert.InRange(max.Z - min.Z, 1.5f, 3.5f);
    }

    // The medium turbine's rotor stands upright in its frame: the one moving part, with no head to ride on.
    [Fact]
    public void TheMediumTurbineSplitsIntoTheStillFrameAndTheRotor()
    {
        Assert.Equal(new[] { Glb.BodyName, "Rotor" }, Medium.Parts.Select(part => part.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.All(Medium.Parts, part => Assert.NotEmpty(part.Meshes));
        Assert.Null(Medium.Parts.Single(part => part.Name == "Rotor").Parent);
    }

    [Fact]
    public void TheMediumRotorTurnsAboutItsUprightShaft()
    {
        var rotor = Medium.Parts.Single(part => part.Name == "Rotor");

        Assert.InRange(rotor.Axis.Y, 0.99f, 1.01f);
        Assert.InRange(Math.Abs(rotor.Pivot.X) + Math.Abs(rotor.Pivot.Z), 0f, 0.05f);
        Assert.InRange(rotor.Pivot.Y, 1.3f, 1.6f);
    }

    // The Medium Solar Panel's spot is 2 m by 1 m, and the turbine stands about 3 m tall in it.
    [Fact]
    public void TheSmallTurbineStandsOnTheGroundUnderTheSmallSpotsHeight()
    {
        var (min, max) = Small.Bounds();

        Assert.InRange(min.Y, -0.01f, 0.01f);
        Assert.InRange(max.Y, 2.6f, 3.3f);
        Assert.InRange(max.X - min.X, 0.5f, 1.3f);
        Assert.InRange(max.Z - min.Z, 0.5f, 1.4f);
    }

    [Fact]
    public void TheSmallTurbinesHeadTurnsAboutTheUprightAndCarriesTheRotor()
    {
        var head = Small.Parts.Single(part => part.Name == "Head");
        var rotor = Small.Parts.Single(part => part.Name == "Rotor");

        Assert.InRange(head.Axis.Y, 0.99f, 1.01f);
        Assert.Equal("Head", rotor.Parent);
    }

    [Fact]
    public void TheMediumTurbineStandsOnTheGroundAndFitsTheMediumSpot()
    {
        var (min, max) = Medium.Bounds();

        Assert.InRange(min.Y, -0.01f, 0.01f);
        Assert.InRange(max.Y, 2.8f, 3.5f);
        Assert.InRange(max.X - min.X, 1.5f, 2.0f);
        Assert.InRange(max.Z - min.Z, 0.7f, 1.0f);
    }

    [Theory]
    [InlineData("Medium")]
    [InlineData("Large")]
    [InlineData("Small")]
    public void EveryMeshIsWholeAndPointsAtARealMaterial(string size)
    {
        var model = Load(size);

        Assert.InRange(model.TriangleCount, 1000, 10000);

        foreach (var mesh in model.Parts.SelectMany(part => part.Meshes))
        {
            Assert.InRange(mesh.Material, 0, model.Materials.Count - 1);
            Assert.Equal(mesh.Positions.Length, mesh.Normals.Length);
            Assert.Equal(mesh.Positions.Length, mesh.Uvs.Length);
            Assert.Equal(0, mesh.Triangles.Length % 3);
            Assert.All(mesh.Triangles, index => Assert.InRange(index, 0, mesh.Positions.Length - 1));
        }
    }

    [Theory]
    [InlineData("Medium")]
    [InlineData("Large")]
    [InlineData("Small")]
    public void EveryTextureInTheModelDecodes(string size)
    {
        var model = Load(size);
        var textured = model.Materials.Where(material => material.Image != null).ToList();

        Assert.NotEmpty(textured);

        foreach (var material in textured)
        {
            var picture = Png.Decode(model.Images[material.Image!.Value]);

            Assert.Equal(picture.Width * picture.Height * 4, picture.Rgba.Length);
            Assert.True(picture.Width >= 16 && picture.Height >= 16);
        }
    }

    [Theory]
    [InlineData("Medium")]
    [InlineData("Large")]
    [InlineData("Small")]
    public void TheIconIsTheSameSizeAsTheGamesOwn(string size)
    {
        var icon = Png.Decode(Asset(TurbineRows.All.Single(kind => kind.Size == size).IconFile));

        Assert.Equal((512, 512), (icon.Width, icon.Height));
    }

    // Unity's axes mirror glTF's left to right, so a point on glTF's +X lands on Unity's -X.
    [Fact]
    public void ModelSpaceIsMirroredIntoUnitysHandedness()
    {
        Assert.Equal(new Vector3(-1, 2, 3), Glb.ToUnity(new Vector3(1, 2, 3)));
    }

    [Fact]
    public void ANonGlbFileIsRefused()
    {
        Assert.Throws<FormatException>(() => Glb.Read(new byte[40], Movers));
    }

    // A 3x2 picture whose rows use filters 1 (left), 2 (up), so the decoder must undo each against the right neighbour.
    [Fact]
    public void FilteredRowsDecodeToTheOriginalPixels()
    {
        var expected = new byte[]
        {
            10, 20, 30, 255, 15, 25, 35, 255, 20, 30, 40, 128,
            11, 21, 31, 255, 16, 26, 36, 254, 25, 35, 45, 120,
        };
        var raw = new List<byte> { 1 };

        for (var x = 0; x < 12; x++) raw.Add((byte)(expected[x] - (x >= 4 ? expected[x - 4] : 0)));

        raw.Add(2);

        for (var x = 0; x < 12; x++) raw.Add((byte)(expected[12 + x] - expected[x]));

        var picture = Png.Decode(MakePng(3, 2, raw.ToArray()));

        Assert.Equal(expected, picture.Rgba);
    }

    [Fact]
    public void AnInterlacedPngIsRefused()
    {
        var png = MakePng(1, 1, new byte[] { 0, 1, 2, 3, 4 }, interlace: 1);

        Assert.Throws<FormatException>(() => Png.Decode(png));
    }

    private static byte[] MakePng(int width, int height, byte[] filteredRows, byte interlace = 0)
    {
        using var file = new MemoryStream();

        file.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        var header = new byte[13];

        BigEndian(header, 0, width);
        BigEndian(header, 4, height);
        header[8] = 8;
        header[9] = 6;
        header[12] = interlace;
        Chunk(file, "IHDR", header);

        using (var data = new MemoryStream())
        {
            using (var zlib = new ZLibStream(data, CompressionLevel.Optimal, leaveOpen: true)) zlib.Write(filteredRows);

            Chunk(file, "IDAT", data.ToArray());
        }

        Chunk(file, "IEND", Array.Empty<byte>());

        return file.ToArray();
    }

    // The decoder does not check chunk CRCs, so these are left zero.
    private static void Chunk(Stream file, string type, byte[] data)
    {
        var length = new byte[4];

        BigEndian(length, 0, data.Length);
        file.Write(length);
        file.Write(System.Text.Encoding.ASCII.GetBytes(type));
        file.Write(data);
        file.Write(new byte[4]);
    }

    private static void BigEndian(byte[] into, int at, int value)
    {
        into[at] = (byte)(value >> 24);
        into[at + 1] = (byte)(value >> 16);
        into[at + 2] = (byte)(value >> 8);
        into[at + 3] = (byte)value;
    }
}
