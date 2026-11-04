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
/// Gives the turbine its own model. Its furniture row names a prefab the game does not have; when the game asks for
/// it, it gets a copy of the Large Solar Panel's prefab (so every part the game looks for is there: the head bar, the
/// collider, the animator) with the panel's meshes swapped for the turbine's, built from the model file inside the
/// DLL. The rotor turns faster the harder the wind blows. If the model cannot be built, the game gets the solar panel's
/// own prefab, so the turbine still works, looking like a panel.
/// </summary>
internal static class TurbineModel
{
    private const string Folder = "Assets/RuntimeAssets/Prefabs/Furniture/";
    private const string Path = Folder + TurbineRows.ResName;
    private const string StockPath = Folder + "P_RoofEquipment_009_S1_M02_Large";
    private const string ModelName = "WindTurbine";
    private const string RotorName = "Rotor";
    private const float HeadBarAboveTop = 0.2f;

    /// <summary>Degrees a second: a slow turn in still air, about one turn a second in the worst cold snap.</summary>
    private const float CalmSpin = 40f;
    private const float SpinPerWind = 500f;

    private static readonly Dictionary<IntPtr, Transform> Rotors = new();
    private static Action<string> _log = _ => { };
    private static GameObject? _holder;
    private static GameObject? _template;
    private static Model? _model;
    private static UnityEngine.Vector3 _rotorAxis = UnityEngine.Vector3.right;
    private static bool _hasFailed;

    public static void Apply(Harmony harmony, Action<string> log)
    {
        _log = log;

        Patch(harmony, AccessTools.Method(typeof(BattleResource), "LoadPrefabWithFallback", new[] { typeof(string) }), prefix: nameof(GiveOurModel));
        Patch(harmony, AccessTools.Method(typeof(PreLoaderDataExtension), "AddChild"), prefix: nameof(SkipPreloadingOurModel));
        Patch(harmony, AccessTools.Method(typeof(BattleResource), "LoadGameObject", new[] { typeof(string) }), postfix: nameof(FindRotor));
        Patch(harmony, AccessTools.Method(typeof(BattleResource), "LoadGameObject", new[] { typeof(string), typeof(GameObject), typeof(bool) }), postfix: nameof(FindRotorParented));
    }

    /// <summary>Called every frame on the main thread: turns every turbine's rotor that is still in the world.</summary>
    public static void Spin()
    {
        if (Rotors.Count == 0) return;

        var step = (CalmSpin + SpinPerWind * Math.Max(0f, WindPatches.Wind(out _))) * Time.deltaTime;

        if (step == 0f) return;

        List<IntPtr>? gone = null;

        foreach (var pair in Rotors)
        {
            if (pair.Value == null || pair.Value.WasCollected)
            {
                (gone ??= new List<IntPtr>()).Add(pair.Key);

                continue;
            }

            pair.Value.Rotate(_rotorAxis, step, Space.Self);
        }

        if (gone != null)
            foreach (var key in gone) Rotors.Remove(key);
    }

    private static bool GiveOurModel(string __0, ref GameObject __result)
    {
        if (__0 != Path) return true;

        try
        {
            __result = Template() ?? BattleResource.LoadPrefabWithFallback(StockPath);
        }
        catch (Exception ex)
        {
            if (!_hasFailed) _log("the Large Wind Turbine's model could not be built, so it looks like a solar panel: " + ex);

            _hasFailed = true;
            __result = BattleResource.LoadPrefabWithFallback(StockPath);
        }

        return false;
    }

    /// <summary>The game preloads every furniture prefab a level uses from its asset bundles, and ours is in none.</summary>
    private static bool SkipPreloadingOurModel(string __1) => __1 != Path;

    private static void FindRotor(string __0, GameObject __result) => Remember(__0, __result);

    private static void FindRotorParented(string __0, GameObject __result) => Remember(__0, __result);

    private static void Remember(string path, GameObject model)
    {
        if (path != Path || model == null) return;

        var rotor = model.transform.Find(ModelName + "/" + RotorName);

        if (rotor != null) Rotors[rotor.Pointer] = rotor;
    }

    private static GameObject? Template()
    {
        if (_template != null && !_template.WasCollected) return _template;

        if (_hasFailed) return null;

        var stock = BattleResource.LoadPrefabWithFallback(StockPath);

        if (stock == null)
        {
            _log("the Large Solar Panel's prefab is missing, so the Large Wind Turbine has no model");
            _hasFailed = true;

            return null;
        }

        _model ??= Glb.Read(Resource("WindTurbineLarge.glb"), new[] { RotorName });

        if (_holder == null || _holder.WasCollected)
        {
            _holder = new GameObject("WindPower models");
            _holder.SetActive(false);
            Object.DontDestroyOnLoad(_holder);
        }

        var copy = Object.Instantiate(stock, _holder.transform);

        copy.name = TurbineRows.ResName;
        Build(copy, _model);
        _template = copy;

        return copy;
    }

    private static void Build(GameObject copy, Model model)
    {
        var root = copy.transform;
        var renderers = copy.GetComponentsInChildren<MeshRenderer>(true);
        var stockMaterial = renderers.Select(renderer => renderer.sharedMaterial).FirstOrDefault(material => material != null);
        var layer = renderers.Length > 0 ? renderers[0].gameObject.layer : copy.layer;
        var (stockMin, stockMax, hasStockBounds) = StockBounds(root, copy.GetComponentsInChildren<MeshFilter>(true));

        _log($"building the Large Wind Turbine on the solar panel's prefab: {renderers.Length} mesh renderers, layer {layer}, shader {(stockMaterial == null ? "none" : stockMaterial.shader.name)}, panel box {stockMin} to {stockMax}");

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

        foreach (var part in model.Parts)
        {
            var holder = ours.transform;

            if (part.Name != Glb.BodyName)
            {
                var pivot = new GameObject(part.Name) { layer = layer };

                pivot.transform.SetParent(ours.transform, false);
                pivot.transform.localPosition = Unity(part.Pivot);
                holder = pivot.transform;
                _rotorAxis = Unity(part.Axis);
            }

            for (var i = 0; i < part.Meshes.Count; i++) made.Add(MakeMesh(holder, $"{part.Name}_{i}", part.Meshes[i], materials[part.Meshes[i].Material], layer));
        }

        // Stand the turbine where the panel stood: centred on the panel's footprint, its foot at the panel's foot.
        var (min, max) = model.Bounds();
        var centre = (min + max) / 2f;
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

        var dye = copy.GetComponent<FurnitureDyePalette>();

        if (dye != null) dye.Targets = new Il2CppReferenceArray<Renderer>(0);

        _log($"Large Wind Turbine model built: {model.TriangleCount} triangles, {made.Count} meshes, {model.Materials.Count} materials");
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
    private static Texture2D MakeTexture(string name, Picture picture)
    {
        var texture = new Texture2D(picture.Width, picture.Height, TextureFormat.RGBA32, true)
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
            _log("cannot find a model loading method; the Large Wind Turbine may look like a solar panel");

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
