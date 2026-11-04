using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle;
using GameCore.HotUpdate.Battle.Show;
using GameCore.Scripts;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;
using WindPower.Core;
using Numerics = System.Numerics;
using Object = UnityEngine.Object;

namespace WindPower.Adapters;

/// <summary>
/// Gives each turbine its own model. Its furniture row names a prefab the game does not have; when the game asks for
/// it, it gets a copy of the prefab of the solar panel the turbine copies (so every part the game looks for is there:
/// the head bar, the collider, the animator) with the panel's meshes swapped for the turbine's, built from the model
/// file inside the DLL. The rotor's speed follows the turbine's output, and on the large turbine the whole top swings
/// round into gusts (<see cref="TurbineMotion"/>). The panel's dye palette gets the turbine's own painted materials
/// on the game's dye shader, so the game's dyes recolour it. If a model cannot be built, the game gets the solar panel's own
/// prefab, so the turbine still works, looking like a panel.
/// </summary>
internal static class TurbineModel
{
    private const string Folder = "Assets/RuntimeAssets/Prefabs/Furniture/";
    private const string ModelName = "WindTurbine";
    private const string HeadName = "Head";
    private const string RotorName = "Rotor";
    private const float HeadBarAboveTop = 0.2f;

    private static readonly Look[] Looks = TurbineRows.All.Select(kind => new Look(kind)).ToArray();

    /// <summary>Every turbine in the world, by its rotor.</summary>
    private static readonly Dictionary<IntPtr, Rig> Rigs = new();

    private static readonly System.Random Phases = new();
    private static Action<string> _log = _ => { };
    private static GameObject? _holder;

    public static void Apply(Harmony harmony, Action<string> log)
    {
        _log = log;

        Patch(harmony, AccessTools.Method(typeof(BattleResource), "LoadPrefabWithFallback", new[] { typeof(string) }), prefix: nameof(GiveOurModel));
        Patch(harmony, AccessTools.Method(typeof(PreLoaderDataExtension), "AddChild"), prefix: nameof(SkipPreloadingOurModel));
        Patch(harmony, AccessTools.Method(typeof(BattleResource), "LoadGameObject", new[] { typeof(string) }), postfix: nameof(FindRotor));
        Patch(harmony, AccessTools.Method(typeof(BattleResource), "LoadGameObject", new[] { typeof(string), typeof(GameObject), typeof(bool) }), postfix: nameof(FindRotorParented));
    }

    /// <summary>Called every frame on the main thread: moves every turbine that is still in the world.</summary>
    public static void Animate()
    {
        if (Rigs.Count == 0) return;

        var share = WindPatches.Wind();
        var seconds = Time.deltaTime;
        var time = Time.time;
        List<IntPtr>? gone = null;

        foreach (var pair in Rigs)
        {
            var rig = pair.Value;

            if (rig.Rotor == null || rig.Rotor.WasCollected)
            {
                (gone ??= new List<IntPtr>()).Add(pair.Key);

                continue;
            }

            rig.Motion.Step(seconds, time, share);

            if (rig.Head != null) Turn(rig.Look, rig.Head, HeadName, rig.Motion.HeadYaw);

            Turn(rig.Look, rig.Rotor, RotorName, rig.Motion.RotorAngle);
        }

        if (gone != null)
            foreach (var key in gone) Rigs.Remove(key);
    }

    private static void Turn(Look look, Transform part, string name, float degrees)
    {
        if (look.Axes.TryGetValue(name, out var axis)) part.localRotation = Quaternion.AngleAxis(degrees, axis);
    }

    /// <summary>The turbine whose prefab is at <paramref name="path"/>, or null.</summary>
    private static Look? LookAt(string? path)
    {
        for (var i = 0; i < Looks.Length; i++)
            if (Looks[i].Path == path) return Looks[i];

        return null;
    }

    private static bool GiveOurModel(string __0, ref GameObject __result)
    {
        if (LookAt(__0) is not { } look) return true;

        try
        {
            __result = Template(look) ?? BattleResource.LoadPrefabWithFallback(look.StockPath);
        }
        catch (Exception ex)
        {
            if (!look.HasFailed) _log($"the {look.Kind.Name}'s model could not be built, so it looks like a solar panel: {ex}");

            look.HasFailed = true;
            __result = BattleResource.LoadPrefabWithFallback(look.StockPath);
        }

        return false;
    }

    /// <summary>The game preloads every furniture prefab a level uses from its asset bundles, and ours are in none.</summary>
    private static bool SkipPreloadingOurModel(string __1) => LookAt(__1) == null;

    private static void FindRotor(string __0, GameObject __result) => Remember(__0, __result);

    private static void FindRotorParented(string __0, GameObject __result) => Remember(__0, __result);

    private static void Remember(string path, GameObject model)
    {
        if (model == null || LookAt(path) is not { } look) return;

        var top = model.transform.Find(ModelName);

        if (top == null) return;

        // The large turbine's rotor rides on its head; the medium turbine has no head, and its rotor stands on the body.
        var head = top.Find(HeadName);
        var rotor = (head ?? top).Find(RotorName);

        if (rotor == null) return;

        // Each turbine gets its own head start on the gusts, so a row of them never turns in step.
        var motion = new TurbineMotion((float)Phases.NextDouble() * 600f, look.Kind.SpinScale, look.Kind.TurnsIntoGusts);

        Rigs[rotor.Pointer] = new Rig(look, head, rotor, motion);
    }

    private static GameObject? Template(Look look)
    {
        if (look.Template != null && !look.Template.WasCollected) return look.Template;

        if (look.HasFailed) return null;

        var kind = look.Kind;
        var stock = BattleResource.LoadPrefabWithFallback(look.StockPath);

        if (stock == null)
        {
            _log($"the solar panel's prefab {kind.StockPrefab} is missing, so the {kind.Name} has no model");
            look.HasFailed = true;

            return null;
        }

        look.Model ??= Glb.Read(Resource(kind.ModelFile), kind.Movers);

        if (_holder == null || _holder.WasCollected)
        {
            _holder = new GameObject("WindPower models");
            _holder.SetActive(false);
            Object.DontDestroyOnLoad(_holder);
        }

        var copy = Object.Instantiate(stock, _holder.transform);

        copy.name = kind.ResName;
        Build(copy, look, look.Model);
        look.Template = copy;

        return copy;
    }

    private static void Build(GameObject copy, Look look, Model model)
    {
        var root = copy.transform;
        var renderers = copy.GetComponentsInChildren<MeshRenderer>(true);
        var stockMaterial = renderers.Select(renderer => renderer.sharedMaterial).FirstOrDefault(material => material != null);
        var layer = renderers.Length > 0 ? renderers[0].gameObject.layer : copy.layer;
        var (stockMin, stockMax, hasStockBounds) = StockBounds(root, copy.GetComponentsInChildren<MeshFilter>(true));

        _log($"building the {look.Kind.Name} on the solar panel's prefab: {renderers.Length} mesh renderers, layer {layer}, shader {(stockMaterial == null ? "none" : stockMaterial.shader.name)}, panel box {stockMin} to {stockMax}");

        if (stockMaterial == null) throw new InvalidOperationException("the solar panel's prefab has no material to copy");

        // The panel's own meshes go; everything else on the prefab stays, so the game finds what it looks for.
        foreach (var renderer in copy.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Object.DestroyImmediate(renderer);

        foreach (var renderer in renderers) Object.DestroyImmediate(renderer);

        foreach (var filter in copy.GetComponentsInChildren<MeshFilter>(true)) Object.DestroyImmediate(filter);

        var materials = model.Materials.Select(material => MakeMaterial(stockMaterial, material, model.Images)).ToArray();
        var ours = new GameObject(ModelName);
        var scale = root.lossyScale;

        ours.layer = layer;
        ours.transform.SetParent(root, false);
        ours.transform.localRotation = Quaternion.identity;
        ours.transform.localScale = new UnityEngine.Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);

        var made = new List<MeshRenderer>();
        var holders = Pivots(ours.transform, model, layer, look.Axes);

        foreach (var part in model.Parts)
            for (var i = 0; i < part.Meshes.Count; i++) made.Add(MakeMesh(holders[part.Name], $"{part.Name}_{i}", part.Meshes[i], materials[part.Meshes[i].Material], layer));

        // Stand the turbine where the panel stood: its base centred on the panel's footprint, its foot at the panel's foot.
        var (min, max) = model.Bounds();
        var (baseMin, baseMax) = model.Footprint();
        var centre = (baseMin + baseMax) / 2f;
        var foot = hasStockBounds ? new UnityEngine.Vector3((stockMin.x + stockMax.x) / 2f, stockMin.y, (stockMin.z + stockMax.z) / 2f) : UnityEngine.Vector3.zero;

        ours.transform.localPosition = foot - Divide(new UnityEngine.Vector3(centre.X, min.Y, centre.Z), scale);

        var worldMin = ours.transform.TransformPoint(Unity(min));
        var worldMax = ours.transform.TransformPoint(Unity(max));

        foreach (var collider in copy.GetComponentsInChildren<BoxCollider>(true)) Fit(collider, worldMin, worldMax);

        var animator = copy.GetComponent<ModelAnimator>();

        if (animator != null)
        {
            var list = new Il2CppSystem.Collections.Generic.List<MeshRenderer>();

            foreach (var renderer in made) list.Add(renderer);

            animator.Renderers = list;
            animator.Skinneds = new Il2CppSystem.Collections.Generic.List<SkinnedMeshRenderer>();

            if (animator.HeadBar != null)
            {
                var bar = animator.HeadBar.transform;

                bar.position = new UnityEngine.Vector3(bar.position.x, Math.Max(worldMin.y, worldMax.y) + HeadBarAboveTop, bar.position.z);
            }
        }
        else
        {
            _log("the solar panel's prefab has no model animator; the turbine may not light up when picked");
        }

        SetDyes(copy, look.Kind, model, materials, made);

        _log($"{look.Kind.Name} model built: {model.TriangleCount} triangles, {made.Count} meshes, {model.Materials.Count} materials");
    }

    /// <summary>
    /// A pivot for each moving part at its turning point, each hung on the part it rides on (the large turbine's rotor
    /// on its head, so it turns with it). Every pivot starts unturned, so each axis, read with the model at rest, holds
    /// in its pivot's own space. The body's meshes hang straight on the model.
    /// </summary>
    private static Dictionary<string, Transform> Pivots(Transform model, Model source, int layer, Dictionary<string, UnityEngine.Vector3> axes)
    {
        var holders = new Dictionary<string, Transform> { [Glb.BodyName] = model };

        foreach (var part in source.Parts.Where(part => part.Name != Glb.BodyName))
        {
            var pivot = new GameObject(part.Name) { layer = layer };

            pivot.transform.SetParent(model, false);
            pivot.transform.localPosition = Unity(part.Pivot);
            holders[part.Name] = pivot.transform;
            axes[part.Name] = Unity(part.Axis);
        }

        foreach (var part in source.Parts)
            if (part.Parent != null && holders.TryGetValue(part.Parent, out var parent)) holders[part.Name].SetParent(parent, true);

        return holders;
    }

    private static MeshRenderer MakeMesh(Transform parent, string name, ModelMesh source, Material material, int layer)
    {
        var piece = new GameObject(name) { layer = layer };

        piece.transform.SetParent(parent, false);

        var mesh = new Mesh { name = name };

        if (source.Positions.Length > 65535) mesh.indexFormat = IndexFormat.UInt32;

        mesh.vertices = source.Positions.Select(Unity).ToArray();
        mesh.normals = source.Normals.Select(Unity).ToArray();
        mesh.uv = source.Uvs.Select(uv => new UnityEngine.Vector2(uv.X, uv.Y)).ToArray();
        mesh.triangles = source.Triangles;
        mesh.RecalculateBounds();
        mesh.UploadMeshData(true);

        piece.AddComponent<MeshFilter>().sharedMesh = mesh;

        var renderer = piece.AddComponent<MeshRenderer>();

        renderer.sharedMaterial = material;

        return renderer;
    }

    /// <summary>
    /// Fills the solar panel's dye palette with the turbine's own. Each painted material gets eight copies on the
    /// game's dye shader (SL/LitDye, taken from the panel's own dyed materials), one per dye, sharing one grey base map
    /// and one mask (<see cref="Paint"/>), so the game colours the turbine the way it colours its own furniture. The
    /// game swaps them in when the player dyes the turbine. The colours come from the panel's own palette.
    /// </summary>
    private static void SetDyes(GameObject copy, TurbineKind kind, Model model, Material[] materials, List<MeshRenderer> made)
    {
        var palette = copy.GetComponent<FurnitureDyePalette>();
        var stock = palette?.Entries != null && palette.Entries.Length > 0 ? palette.Entries[0]?.Variants : null;
        var shader = stock != null && stock.Length > 0 ? stock[0]?.shader : null;

        if (palette == null || stock == null || shader == null)
        {
            _log($"the solar panel's prefab has no dyed materials, so the {kind.Name} cannot be dyed");

            return;
        }

        var colours = new List<Color>();

        for (var i = 0; i < stock.Length; i++)
        {
            var dyed = stock[i];

            if (dyed == null || !dyed.HasProperty("_DyeColor"))
            {
                _log($"the solar panel's dye {i + 1} has no colour, so the {kind.Name} cannot be dyed");

                return;
            }

            colours.Add(dyed.GetColor("_DyeColor"));
        }

        var entries = new List<FurnitureDyePalette.DyeEntry>();

        for (var m = 0; m < model.Materials.Count; m++)
        {
            if (model.Materials[m].Image is not int image) continue;

            var picture = Png.Decode(model.Images[image]);

            if (Paint.PaintShare(picture) < Paint.MinimumShare) continue;

            var name = model.Materials[m].Name;
            var greyBase = MakeTexture(name + "_DyeBase", Paint.DyeBase(picture));
            var mask = MakeTexture(name + "_DyeMask", Paint.Mask(picture), linear: true);
            var variants = new Il2CppReferenceArray<Material>(colours.Count);

            for (var slot = 0; slot < colours.Count; slot++)
            {
                // Swapping the shader keeps every property both shaders share: our maps, colours and keywords.
                var variant = new Material(materials[m]) { name = $"{materials[m].name}_Dye{slot + 1:00}", shader = shader };

                foreach (var map in new[] { "_BaseMap", "_MainTex" })
                    if (variant.HasProperty(map)) variant.SetTexture(map, greyBase);

                variant.SetTexture("_DyeMask", mask);
                variant.SetColor("_DyeColor", colours[slot]);
                variants[slot] = variant;
            }

            entries.Add(new FurnitureDyePalette.DyeEntry { BaseMaterial = materials[m], Variants = variants });
        }

        palette.Entries = new Il2CppReferenceArray<FurnitureDyePalette.DyeEntry>(entries.ToArray());
        palette.Targets = new Il2CppReferenceArray<Renderer>(made.Count);

        for (var i = 0; i < made.Count; i++) palette.Targets[i] = made[i];

        _log($"{kind.Name} dyes: {entries.Count} painted materials on {shader.name}, {colours.Count} colours each");
    }

    private static Material MakeMaterial(Material stock, ModelMaterial source, IReadOnlyList<byte[]> images)
    {
        var material = new Material(stock) { name = ModelName + "_" + source.Name };

        foreach (var map in new[] { "_BumpMap", "_MetallicGlossMap", "_OcclusionMap", "_EmissionMap", "_DetailAlbedoMap", "_DetailNormalMap", "_SpecGlossMap" })
            if (material.HasProperty(map)) material.SetTexture(map, null);

        foreach (var keyword in new[] { "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP", "_EMISSION", "_DETAIL_MULX2", "_SPECGLOSSMAP" }) material.DisableKeyword(keyword);

        Texture texture = source.Image is int image ? MakeTexture(source.Name, Png.Decode(images[image])) : Texture2D.whiteTexture;

        foreach (var map in new[] { "_BaseMap", "_MainTex" })
        {
            if (!material.HasProperty(map)) continue;

            material.SetTexture(map, texture);
            material.SetTextureScale(map, UnityEngine.Vector2.one);
            material.SetTextureOffset(map, UnityEngine.Vector2.zero);
        }

        // glTF colours are linear; Unity takes material colours as sRGB and converts them itself.
        var colour = new Color(source.BaseColor.X, source.BaseColor.Y, source.BaseColor.Z, source.BaseColor.W).gamma;

        foreach (var name in new[] { "_BaseColor", "_Color" })
            if (material.HasProperty(name)) material.SetColor(name, colour);

        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", source.Metallic);

        foreach (var name in new[] { "_Smoothness", "_Glossiness" })
            if (material.HasProperty(name)) material.SetFloat(name, 1f - source.Roughness);

        return material;
    }

    /// <summary>
    /// Png rows run from the top down and Unity's from the bottom up, and glTF's uvs start at the top while Unity's
    /// start at the bottom: the two flips cancel, so the rows go in as they are.
    /// </summary>
    private static Texture2D MakeTexture(string name, Picture picture, bool linear = false)
    {
        // A mask is data, not colour, so it is stored linear; colour maps are sRGB.
        var texture = new Texture2D(picture.Width, picture.Height, TextureFormat.RGBA32, true, linear)
        {
            name = ModelName + "_" + name,
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear,
        };
        var pixels = new Il2CppStructArray<Color32>(picture.Width * picture.Height);

        for (var p = 0; p < pixels.Length; p++)
            pixels[p] = new Color32(picture.Rgba[p * 4], picture.Rgba[p * 4 + 1], picture.Rgba[p * 4 + 2], picture.Rgba[p * 4 + 3]);

        texture.SetPixels32(pixels);
        texture.Apply(true, true);

        return texture;
    }

    /// <summary>The box around the panel's meshes in the prefab's own space, before they are taken away.</summary>
    private static (UnityEngine.Vector3 Min, UnityEngine.Vector3 Max, bool Found) StockBounds(Transform root, Il2CppArrayBase<MeshFilter> filters)
    {
        var min = new UnityEngine.Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new UnityEngine.Vector3(float.MinValue, float.MinValue, float.MinValue);
        var found = false;

        foreach (var filter in filters)
        {
            var mesh = filter.sharedMesh;

            if (mesh == null) continue;

            var bounds = mesh.bounds;

            foreach (var corner in Corners(bounds.min, bounds.max))
            {
                var point = root.InverseTransformPoint(filter.transform.TransformPoint(corner));

                min = UnityEngine.Vector3.Min(min, point);
                max = UnityEngine.Vector3.Max(max, point);
                found = true;
            }
        }

        return (min, max, found);
    }

    private static void Fit(BoxCollider collider, UnityEngine.Vector3 worldMin, UnityEngine.Vector3 worldMax)
    {
        var min = new UnityEngine.Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new UnityEngine.Vector3(float.MinValue, float.MinValue, float.MinValue);

        foreach (var corner in Corners(worldMin, worldMax))
        {
            var point = collider.transform.InverseTransformPoint(corner);

            min = UnityEngine.Vector3.Min(min, point);
            max = UnityEngine.Vector3.Max(max, point);
        }

        collider.center = (min + max) / 2f;
        collider.size = max - min;
    }

    private static IEnumerable<UnityEngine.Vector3> Corners(UnityEngine.Vector3 a, UnityEngine.Vector3 b)
    {
        for (var i = 0; i < 8; i++) yield return new UnityEngine.Vector3((i & 1) == 0 ? a.x : b.x, (i & 2) == 0 ? a.y : b.y, (i & 4) == 0 ? a.z : b.z);
    }

    private static UnityEngine.Vector3 Divide(UnityEngine.Vector3 v, UnityEngine.Vector3 by) => new(v.x / by.x, v.y / by.y, v.z / by.z);

    private static UnityEngine.Vector3 Unity(Numerics.Vector3 v) => new(v.X, v.Y, v.Z);

    private sealed record Rig(Look Look, Transform? Head, Transform Rotor, TurbineMotion Motion);

    /// <summary>One turbine's model: where the game asks for it, the panel it is built on, and the copy once built.</summary>
    private sealed class Look
    {
        public Look(TurbineKind kind)
        {
            Kind = kind;
            Path = Folder + kind.ResName;
            StockPath = Folder + kind.StockPrefab;
        }

        public TurbineKind Kind { get; }

        public string Path { get; }

        public string StockPath { get; }

        /// <summary>The axis each moving part turns about, in its pivot's own space.</summary>
        public Dictionary<string, UnityEngine.Vector3> Axes { get; } = new();

        public GameObject? Template { get; set; }

        public Model? Model { get; set; }

        public bool HasFailed { get; set; }
    }

    internal static byte[] Resource(string name)
    {
        using var stream = typeof(TurbineModel).Assembly.GetManifestResourceStream(name) ?? throw new FileNotFoundException("the mod's DLL is missing " + name);
        using var copy = new MemoryStream();

        stream.CopyTo(copy);

        return copy.ToArray();
    }

    private static void Patch(Harmony harmony, System.Reflection.MethodInfo? target, string? prefix = null, string? postfix = null)
    {
        if (target == null)
        {
            _log("cannot find a model loading method; the wind turbines may look like solar panels");

            return;
        }

        try
        {
            harmony.Patch(target, prefix: prefix == null ? null : new HarmonyMethod(typeof(TurbineModel), prefix), postfix: postfix == null ? null : new HarmonyMethod(typeof(TurbineModel), postfix));
        }
        catch (Exception ex)
        {
            _log($"cannot hook {target.DeclaringType?.Name}.{target.Name}: {ex.Message}");
        }
    }
}
