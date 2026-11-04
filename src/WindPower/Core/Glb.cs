using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace WindPower.Core;

/// <summary>A material as the model file gives it. <see cref="Image"/> indexes <see cref="Model.Images"/>.</summary>
public sealed record ModelMaterial(string Name, Vector4 BaseColor, float Metallic, float Roughness, int? Image);

/// <summary>Every triangle of one part that uses one material, ready for a Unity mesh.</summary>
public sealed record ModelMesh(int Material, Vector3[] Positions, Vector3[] Normals, Vector2[] Uvs, int[] Triangles);

/// <summary>
/// A piece of the model that moves as one. The body has a zero pivot and axis. A moving part's vertices are relative
/// to <see cref="Pivot"/>, and it turns about <see cref="Axis"/>, both in model space as the model stands at rest.
/// <see cref="Parent"/> names the moving part it rides on, if any (the rotor and the tail ride on the head).
/// </summary>
public sealed record ModelPart(string Name, Vector3 Pivot, Vector3 Axis, string? Parent, IReadOnlyList<ModelMesh> Meshes);

/// <summary>A model in Unity's axes: +Y up, left-handed, 1 unit = 1 m. <see cref="Images"/> are the png files as stored.</summary>
public sealed record Model(IReadOnlyList<ModelPart> Parts, IReadOnlyList<ModelMaterial> Materials, IReadOnlyList<byte[]> Images)
{
    public int TriangleCount => Parts.Sum(part => part.Meshes.Sum(mesh => mesh.Triangles.Length / 3));

    /// <summary>The corners of the box around every vertex, with each spinning part placed at its pivot.</summary>
    public (Vector3 Min, Vector3 Max) Bounds()
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        foreach (var part in Parts)
            foreach (var mesh in part.Meshes)
                foreach (var position in mesh.Positions)
                {
                    min = Vector3.Min(min, position + part.Pivot);
                    max = Vector3.Max(max, position + part.Pivot);
                }

        return (min, max);
    }

    /// <summary>
    /// The corners of the box around the points that touch the ground: the turbine's base. Blades and tails reach
    /// out further on one side than the other, so the base, not the whole model, is what goes in the middle of a spot.
    /// </summary>
    public (Vector3 Min, Vector3 Max) Footprint(float height = 0.05f)
    {
        var ground = Bounds().Min.Y + height;
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        foreach (var part in Parts)
            foreach (var mesh in part.Meshes)
                foreach (var position in mesh.Positions)
                {
                    var point = position + part.Pivot;

                    if (point.Y > ground) continue;

                    min = Vector3.Min(min, point);
                    max = Vector3.Max(max, point);
                }

        return (min, max);
    }
}

/// <summary>
/// Reads a binary glTF (.glb) as three.js's exporter writes it: triangle meshes with positions, normals and one set of
/// uvs, png textures inside the file, and node transforms as matrices or as translation, rotation and scale.
/// </summary>
public static class Glb
{
    public const string BodyName = "Body";

    private const uint Magic = 0x46546C67;
    private const uint JsonChunk = 0x4E4F534A;
    private const uint BinChunk = 0x004E4942;
    private const int Triangles = 4;

    /// <summary>
    /// The model in <paramref name="bytes"/>, split into the body and one part for each node named in
    /// <paramref name="movers"/>, each turning about the given axis of its node's own space (three.js turns a rotor
    /// about its X, a head about its Y).
    /// </summary>
    public static Model Read(byte[] bytes, IReadOnlyDictionary<string, Vector3> movers)
    {
        var (json, bin) = Chunks(bytes);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var file = new GltfFile(root, bin);

        var groups = new Dictionary<string, PartBuilder> { [BodyName] = new PartBuilder(Matrix4x4.Identity, null, null) { Name = BodyName } };
        var scene = root.TryGetProperty("scene", out var sceneIndex) ? sceneIndex.GetInt32() : 0;

        foreach (var node in root.GetProperty("scenes")[scene].GetProperty("nodes").EnumerateArray())
            Walk(file, node.GetInt32(), Matrix4x4.Identity, groups[BodyName], groups, movers);

        var parts = groups.Select(pair => pair.Value.Build(pair.Key)).Where(part => part.Meshes.Count > 0).ToList();

        return new Model(parts, Materials(root), Images(root, bin));
    }

    private static (string Json, byte[] Bin) Chunks(byte[] bytes)
    {
        if (bytes.Length < 20 || BitConverter.ToUInt32(bytes, 0) != Magic) throw new FormatException("not a glb file");

        string? json = null;
        byte[]? bin = null;
        var at = 12;

        while (at + 8 <= bytes.Length)
        {
            var length = (int)BitConverter.ToUInt32(bytes, at);
            var type = BitConverter.ToUInt32(bytes, at + 4);

            if (at + 8 + length > bytes.Length) throw new FormatException("glb chunk runs past the end of the file");

            if (type == JsonChunk) json = Encoding.UTF8.GetString(bytes, at + 8, length);
            else if (type == BinChunk) bin = bytes.AsSpan(at + 8, length).ToArray();

            at += 8 + length;
        }

        return (json ?? throw new FormatException("glb has no JSON chunk"), bin ?? Array.Empty<byte>());
    }

    private static void Walk(GltfFile file, int index, Matrix4x4 parentWorld, PartBuilder part, Dictionary<string, PartBuilder> groups, IReadOnlyDictionary<string, Vector3> movers)
    {
        var node = file.Root.GetProperty("nodes")[index];
        var world = LocalMatrix(node) * parentWorld;
        var name = node.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";

        if (movers.TryGetValue(name, out var axis) && !groups.ContainsKey(name))
        {
            part = new PartBuilder(world, axis, part.Name) { Name = name };
            groups[name] = part;
        }

        if (node.TryGetProperty("mesh", out var mesh)) part.Add(file, mesh.GetInt32(), world);

        if (node.TryGetProperty("children", out var children))
            foreach (var child in children.EnumerateArray())
                Walk(file, child.GetInt32(), world, part, groups, movers);
    }

    /// <summary>
    /// glTF stores matrices column by column for column vectors. Read straight into System.Numerics' row-by-row layout
    /// they become the transpose, which is what its row-vector maths wants, so world = local * parent.
    /// </summary>
    private static Matrix4x4 LocalMatrix(JsonElement node)
    {
        if (node.TryGetProperty("matrix", out var matrix))
        {
            var m = matrix.EnumerateArray().Select(value => value.GetSingle()).ToArray();

            return new Matrix4x4(m[0], m[1], m[2], m[3], m[4], m[5], m[6], m[7], m[8], m[9], m[10], m[11], m[12], m[13], m[14], m[15]);
        }

        var scale = node.TryGetProperty("scale", out var s) ? Vector3Of(s) : Vector3.One;
        var rotation = node.TryGetProperty("rotation", out var r) ? QuaternionOf(r) : Quaternion.Identity;
        var translation = node.TryGetProperty("translation", out var t) ? Vector3Of(t) : Vector3.Zero;

        return Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(translation);
    }

    private static Vector3 Vector3Of(JsonElement array) => new(array[0].GetSingle(), array[1].GetSingle(), array[2].GetSingle());

    private static Quaternion QuaternionOf(JsonElement array) => new(array[0].GetSingle(), array[1].GetSingle(), array[2].GetSingle(), array[3].GetSingle());

    /// <summary>three.js is right-handed and Unity left-handed: mirror X. The caller reverses each triangle to match.</summary>
    public static Vector3 ToUnity(Vector3 v) => new(-v.X, v.Y, v.Z);

    private static List<ModelMaterial> Materials(JsonElement root)
    {
        var materials = new List<ModelMaterial>();

        if (!root.TryGetProperty("materials", out var list)) return materials;

        var textures = root.TryGetProperty("textures", out var t) ? t : default;

        foreach (var material in list.EnumerateArray())
        {
            var name = material.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var color = Vector4.One;
            var metallic = 1f;
            var roughness = 1f;
            int? image = null;

            if (material.TryGetProperty("pbrMetallicRoughness", out var pbr))
            {
                if (pbr.TryGetProperty("baseColorFactor", out var c)) color = new Vector4(c[0].GetSingle(), c[1].GetSingle(), c[2].GetSingle(), c[3].GetSingle());
                if (pbr.TryGetProperty("metallicFactor", out var m)) metallic = m.GetSingle();
                if (pbr.TryGetProperty("roughnessFactor", out var r)) roughness = r.GetSingle();
                if (pbr.TryGetProperty("baseColorTexture", out var texture) && textures.ValueKind == JsonValueKind.Array && textures[texture.GetProperty("index").GetInt32()].TryGetProperty("source", out var source)) image = source.GetInt32();
            }

            materials.Add(new ModelMaterial(name, color, metallic, roughness, image));
        }

        return materials;
    }

    private static List<byte[]> Images(JsonElement root, byte[] bin)
    {
        var images = new List<byte[]>();

        if (!root.TryGetProperty("images", out var list)) return images;

        foreach (var image in list.EnumerateArray())
        {
            if (!image.TryGetProperty("bufferView", out var view)) throw new FormatException("glb image is not stored inside the file");

            var (offset, length, _) = BufferView(root, view.GetInt32());

            images.Add(bin.AsSpan(offset, length).ToArray());
        }

        return images;
    }

    private static (int Offset, int Length, int Stride) BufferView(JsonElement root, int index)
    {
        var view = root.GetProperty("bufferViews")[index];

        if (view.TryGetProperty("buffer", out var buffer) && buffer.GetInt32() != 0) throw new FormatException("glb uses a buffer outside the file");

        return (view.TryGetProperty("byteOffset", out var o) ? o.GetInt32() : 0, view.GetProperty("byteLength").GetInt32(), view.TryGetProperty("byteStride", out var s) ? s.GetInt32() : 0);
    }

    private sealed class GltfFile
    {
        public GltfFile(JsonElement root, byte[] bin)
        {
            Root = root;
            Bin = bin;
        }

        public JsonElement Root { get; }

        public byte[] Bin { get; }

        public float[] Floats(int accessor, int width)
        {
            var a = Root.GetProperty("accessors")[accessor];

            if (a.GetProperty("componentType").GetInt32() != 5126) throw new FormatException("glb vertex data is not 32-bit float");

            var count = a.GetProperty("count").GetInt32();
            var (offset, _, stride) = BufferView(Root, a.GetProperty("bufferView").GetInt32());

            offset += a.TryGetProperty("byteOffset", out var o) ? o.GetInt32() : 0;
            stride = stride == 0 ? width * 4 : stride;

            var values = new float[count * width];

            for (var i = 0; i < count; i++)
                for (var k = 0; k < width; k++)
                    values[i * width + k] = BitConverter.ToSingle(Bin, offset + i * stride + k * 4);

            return values;
        }

        public int[] Indices(int accessor)
        {
            var a = Root.GetProperty("accessors")[accessor];
            var count = a.GetProperty("count").GetInt32();
            var (offset, _, _) = BufferView(Root, a.GetProperty("bufferView").GetInt32());

            offset += a.TryGetProperty("byteOffset", out var o) ? o.GetInt32() : 0;

            var values = new int[count];

            switch (a.GetProperty("componentType").GetInt32())
            {
                case 5121:
                    for (var i = 0; i < count; i++) values[i] = Bin[offset + i];
                    break;
                case 5123:
                    for (var i = 0; i < count; i++) values[i] = BitConverter.ToUInt16(Bin, offset + i * 2);
                    break;
                case 5125:
                    for (var i = 0; i < count; i++) values[i] = checked((int)BitConverter.ToUInt32(Bin, offset + i * 4));
                    break;
                default:
                    throw new FormatException("glb indices are not 8, 16 or 32-bit");
            }

            return values;
        }
    }

    private sealed class PartBuilder
    {
        private readonly Vector3 _pivot;
        private readonly Vector3 _axis;
        private readonly Dictionary<int, (List<Vector3> Positions, List<Vector3> Normals, List<Vector2> Uvs, List<int> Triangles)> _byMaterial = new();

        private readonly string? _parent;

        /// <param name="axis">The axis in the node's own space, or null for the body, which does not move.</param>
        /// <param name="parent">The part the node sits in: the body, or a moving part it rides on.</param>
        public PartBuilder(Matrix4x4 world, Vector3? axis, string? parent)
        {
            _pivot = axis == null ? Vector3.Zero : ToUnity(world.Translation);
            _axis = axis is { } local ? Vector3.Normalize(ToUnity(Vector3.TransformNormal(local, world))) : Vector3.Zero;
            _parent = parent == BodyName ? null : parent;
        }

        public string Name { get; init; } = "";

        public void Add(GltfFile file, int meshIndex, Matrix4x4 world)
        {
            // Normals need the inverse transpose: a scaled node (the rotor's spinner is 1 x 1.5 x 1) would bend them otherwise.
            Matrix4x4.Invert(world, out var inverse);

            var normalMatrix = Matrix4x4.Transpose(inverse);

            foreach (var primitive in file.Root.GetProperty("meshes")[meshIndex].GetProperty("primitives").EnumerateArray())
            {
                if (primitive.TryGetProperty("mode", out var mode) && mode.GetInt32() != Triangles) throw new FormatException("glb mesh is not made of triangles");

                var attributes = primitive.GetProperty("attributes");
                var positions = file.Floats(attributes.GetProperty("POSITION").GetInt32(), 3);
                var normals = attributes.TryGetProperty("NORMAL", out var n) ? file.Floats(n.GetInt32(), 3) : throw new FormatException("glb mesh has no normals");
                var count = positions.Length / 3;
                var uvs = attributes.TryGetProperty("TEXCOORD_0", out var t) ? file.Floats(t.GetInt32(), 2) : new float[count * 2];
                var indices = primitive.TryGetProperty("indices", out var i) ? file.Indices(i.GetInt32()) : Enumerable.Range(0, count).ToArray();
                var material = primitive.TryGetProperty("material", out var m) ? m.GetInt32() : -1;

                if (!_byMaterial.TryGetValue(material, out var group))
                {
                    group = (new List<Vector3>(), new List<Vector3>(), new List<Vector2>(), new List<int>());
                    _byMaterial[material] = group;
                }

                var first = group.Positions.Count;

                for (var v = 0; v < count; v++)
                {
                    var position = Vector3.Transform(new Vector3(positions[v * 3], positions[v * 3 + 1], positions[v * 3 + 2]), world);
                    var normal = Vector3.TransformNormal(new Vector3(normals[v * 3], normals[v * 3 + 1], normals[v * 3 + 2]), normalMatrix);

                    group.Positions.Add(ToUnity(position) - _pivot);
                    group.Normals.Add(normal == Vector3.Zero ? Vector3.UnitY : Vector3.Normalize(ToUnity(normal)));
                    group.Uvs.Add(new Vector2(uvs[v * 2], uvs[v * 2 + 1]));
                }

                // Mirroring X turns every triangle inside out; swapping two corners turns it back.
                for (var k = 0; k + 2 < indices.Length; k += 3)
                {
                    group.Triangles.Add(first + indices[k]);
                    group.Triangles.Add(first + indices[k + 2]);
                    group.Triangles.Add(first + indices[k + 1]);
                }
            }
        }

        public ModelPart Build(string name) => new(name, _pivot, _axis, _parent, _byMaterial.OrderBy(pair => pair.Key).Select(pair => new ModelMesh(pair.Key, pair.Value.Positions.ToArray(), pair.Value.Normals.ToArray(), pair.Value.Uvs.ToArray(), pair.Value.Triangles.ToArray())).ToList());
    }
}
