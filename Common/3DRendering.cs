using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using Cataphract.Common.Rendering;
using System.Diagnostics;
using Terraria.GameContent;
using static Cataphractal.Common.Mathematical;

namespace Cataphract.Common;
// TODO: Document public shtuff, fix some bugs, support an arbitrary shader, maybe stop supporting VertexPositionColor?
public sealed class PrimitiveRenderSystem : ModSystem
{
    public override void Load()
    {
        if (Main.dedServ)
            return;
        Main.QueueMainThreadAction(() =>
        {
            PrimitiveRenderer.Initialize();
        });
    }

    public override void Unload()
    {
        PrimitiveRenderer.Dispose();
    }
}

public static class PrimitiveRenderer
{
    private static GraphicsDevice _graphicsDevice => Main.graphics.GraphicsDevice;
    private static BasicEffect? _effect;

    public static bool IsReady =>
        _graphicsDevice != null &&
        !_graphicsDevice.IsDisposed &&
        _effect != null &&
        !_effect.IsDisposed;

    public static void Initialize()
    {
        if (_graphicsDevice == null)
            throw new ArgumentNullException(nameof(_graphicsDevice));

        _effect?.Dispose();
        _effect = new BasicEffect(_graphicsDevice)
        {
            VertexColorEnabled = true,
            TextureEnabled = true,
            LightingEnabled = false,
            FogEnabled = false,
            Texture = TextureAssets.Logo.Value
        };
    }

    public static void Dispose()
    {
        _effect?.Dispose();
    }

    /// <summary>
    /// Draws the specified mesh using the provided world, view, and projection matrices.
    /// </summary>
    /// <param name="world">World matrix.</param>
    /// <param name="view">View matrix.</param>
    /// <param name="projection">Projection matrix.</param>
    /// <param name="mesh">The mesh to draw.</param>
    /// <param name="rasterizerState">The rasterizer state to use. If null, defaults to <see cref="RasterizerState.CullNone"/>.</param>
    /// <param name="depthState">The depth stencil state to use. If null, defaults to <see cref="DepthStencilState.Default"/>.</param>
    /// <param name="blendState">The blend state to use. If null, defaults to <see cref="BlendState.AlphaBlend"/>.</param>
    /// <exception cref="InvalidOperationException">The renderer is not initialized.</exception>
    /// <exception cref="NotSupportedException">The primitive type is not supported. Supported types are: TriangleList, TriangleStrip, LineList, LineStrip, and PointListEXT.</exception>
    public static void DrawMesh(
        in Matrix world,
        in Matrix view,
        in Matrix projection,
        in PrimitiveMesh mesh,
        RasterizerState? rasterizerState = null,
        DepthStencilState? depthState = null,
        BlendState? blendState = null)
    {
        if (!IsReady)
            throw new InvalidOperationException("PrimitiveRenderer is not initialized.");
        if (!mesh.IsValid)
            return;

        Debug.Assert(_effect is not null);

        bool textured = mesh.UsesTexture;

        _effect.TextureEnabled = textured;
        _effect.Texture = textured ? Assets.Images.Particles.Star2.Asset.Value : null;
        _graphicsDevice.SamplerStates[0] = SamplerState.PointWrap;

        _effect.World = world;
        _effect.View = view;
        _effect.Projection = projection;

        var device = _graphicsDevice;

        var previousRasterizer = device.RasterizerState;
        var previousDepth = device.DepthStencilState;
        var previousBlend = device.BlendState;

        device.RasterizerState = rasterizerState ?? RasterizerState.CullNone;
        device.DepthStencilState = depthState ?? DepthStencilState.Default;
        device.BlendState = blendState ?? BlendState.AlphaBlend;

        int primitiveCount = mesh.PrimitiveType switch
        {
            PrimitiveType.TriangleList => mesh.Indices.Length / 3,
            PrimitiveType.TriangleStrip => Math.Max(mesh.Indices.Length - 2, 0),
            PrimitiveType.LineList => mesh.Indices.Length / 2,
            PrimitiveType.LineStrip => Math.Max(mesh.Indices.Length - 1, 0),
            PrimitiveType.PointListEXT => mesh.Indices.Length,
            _ => throw new NotSupportedException(mesh.PrimitiveType.ToString())
        };

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            if (textured)
            {
                device.DrawUserIndexedPrimitives(
                    mesh.PrimitiveType,
                    mesh.TexturedVertices,
                    0,
                    mesh.VertexCount,
                    mesh.Indices,
                    0,
                    primitiveCount);
            }
            else
            {
                device.DrawUserIndexedPrimitives(
                    mesh.PrimitiveType,
                    mesh.ColorVertices,
                    0,
                    mesh.VertexCount,
                    mesh.Indices,
                    0,
                    primitiveCount);
            }
        }

        device.RasterizerState = previousRasterizer;
        device.DepthStencilState = previousDepth;
        device.BlendState = previousBlend;
    }

    /** <summary>
    Draws a triangle strip using the specified parameters. <para/>
    Alias for <see cref="DrawMesh"/>. Assumes you want a 'triangle strip' primitive type.
    </summary>
    <param name="world">The world matrix.</param>
    <param name="view">The view matrix.</param>
    <param name="projection">The projection matrix.</param>
    <param name="vertices">The vertex positions and colors.</param>
    **/
    public static void DrawTriangleStrip(
        in Matrix world,
        in Matrix view,
        in Matrix projection,
        VertexPositionColorTexture[] vertices,
        RasterizerState? rasterizerState = null,
        DepthStencilState? depthState = null,
        BlendState? blendState = null)
    {
        if (vertices == null)
            throw new ArgumentNullException(nameof(vertices));
        if (vertices.Length < 3)
            return;
        if (vertices.Length > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(vertices), "Vertex count exceeds index buffer range.");

        DrawMesh(
            world,
            view,
            projection,
            PrimitiveMesh.FromSequential(vertices, PrimitiveType.TriangleStrip),
            rasterizerState,
            depthState,
            blendState);
    }

    /** <summary>
    Draws a triangle strip using the specified parameters. <para/>
    Alias for <see cref="DrawMesh"/>. Assumes you want a 'triangle strip' primitive type.
    </summary>
    <param name="world">The world matrix.</param>
    <param name="view">The view matrix.</param>
    <param name="projection">The projection matrix.</param>
    <param name="vertices">The vertex positions and colors.</param>
    **/
    public static void DrawTriangleStrip(
        in Matrix world,
        in Matrix view,
        in Matrix projection,
        VertexPositionColor[] vertices,
        RasterizerState? rasterizerState = null,
        DepthStencilState? depthState = null,
        BlendState? blendState = null)
    {
        if (vertices == null)
            throw new ArgumentNullException(nameof(vertices));
        if (vertices.Length < 3)
            return;
        if (vertices.Length > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(vertices), "Vertex count exceeds index buffer range.");

        DrawMesh(
            world,
            view,
            projection,
            PrimitiveMesh.FromSequential(vertices, PrimitiveType.TriangleStrip),
            rasterizerState,
            depthState,
            blendState);
    }

    /** <summary>
    Draws a triangle list using the specified parameters. <para/>
    Alias for <see cref="DrawMesh"/>. Assumes you want a 'triangle list' primitive type.
    </summary>
    <param name="world">The world matrix.</param>
    <param name="view">The view matrix.</param>
    <param name="projection">The projection matrix.</param>
    <param name="vertices">The vertex positions and colors.</param>
    **/
    public static void DrawTriangleList(
        in Matrix world,
        in Matrix view,
        in Matrix projection,
        VertexPositionColorTexture[] vertices,
        RasterizerState? rasterizerState = null,
        DepthStencilState? depthState = null,
        BlendState? blendState = null)
    {
        if (vertices == null)
            throw new ArgumentNullException(nameof(vertices));
        if (vertices.Length < 3 || vertices.Length % 3 != 0)
            return;
        if (vertices.Length > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(vertices), "Vertex count exceeds index buffer range.");

        DrawMesh(
            world,
            view,
            projection,
            PrimitiveMesh.FromSequential(vertices, PrimitiveType.TriangleList),
            rasterizerState,
            depthState,
            blendState);
    }

    /** <summary>
    Draws a triangle list using the specified parameters. <para/>
    Alias for <see cref="DrawMesh"/>. Assumes you want a 'triangle list' primitive type.
    </summary>
    <param name="world">The world matrix.</param>
    <param name="view">The view matrix.</param>
    <param name="projection">The projection matrix.</param>
    <param name="vertices">The vertex positions and colors.</param>
    **/
    public static void DrawTriangleList(
        in Matrix world,
        in Matrix view,
        in Matrix projection,
        VertexPositionColor[] vertices,
        RasterizerState? rasterizerState = null,
        DepthStencilState? depthState = null,
        BlendState? blendState = null)
    {
        if (vertices == null)
            throw new ArgumentNullException(nameof(vertices));
        if (vertices.Length < 3 || vertices.Length % 3 != 0)
            return;
        if (vertices.Length > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(vertices), "Vertex count exceeds index buffer range.");

        DrawMesh(
            world,
            view,
            projection,
            PrimitiveMesh.FromSequential(vertices, PrimitiveType.TriangleList),
            rasterizerState,
            depthState,
            blendState);
    }
}

public readonly struct PrimitiveMesh
{
    private readonly VertexPositionColorTexture[]? _texturedVertices;
    private readonly VertexPositionColor[]? _colorVertices;

    public PrimitiveMesh(VertexPositionColorTexture[] vertices, short[] indices, PrimitiveType primitiveType)
    {
        _texturedVertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
        _colorVertices = null;
        Indices = indices ?? throw new ArgumentNullException(nameof(indices));
        PrimitiveType = primitiveType;

        if (_texturedVertices.Length == 0 || Indices.Length == 0)
            throw new ArgumentException("Mesh must contain vertices and indices.");
        if (_texturedVertices.Length > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(vertices), "Vertex count exceeds index buffer range.");

        UsesTexture = true;
    }

    public PrimitiveMesh(VertexPositionColor[] vertices, short[] indices, PrimitiveType primitiveType)
    {
        _texturedVertices = null;
        _colorVertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
        Indices = indices ?? throw new ArgumentNullException(nameof(indices));
        PrimitiveType = primitiveType;

        if (_colorVertices.Length == 0 || Indices.Length == 0)
            throw new ArgumentException("Mesh must contain vertices and indices.");
        if (_colorVertices.Length > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(vertices), "Vertex count exceeds index buffer range.");

        UsesTexture = false;
    }

    public bool UsesTexture { get; }
    public int VertexCount => UsesTexture ? _texturedVertices!.Length : _colorVertices!.Length;
    public VertexPositionColorTexture[] TexturedVertices => _texturedVertices ?? throw new InvalidOperationException("Mesh does not contain textured vertices.");
    public VertexPositionColor[] ColorVertices => _colorVertices ?? throw new InvalidOperationException("Mesh does not contain color-only vertices.");
    public short[] Indices { get; }
    public PrimitiveType PrimitiveType { get; }
    public bool IsValid => VertexCount > 0 && Indices.Length > 0;

    public static PrimitiveMesh FromSequential(VertexPositionColorTexture[] vertices, PrimitiveType primitiveType)
    {
        if (vertices == null)
            throw new ArgumentNullException(nameof(vertices));
        if (vertices.Length == 0)
            throw new ArgumentException("Vertices collection is empty.", nameof(vertices));
        if (vertices.Length > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(vertices), "Vertex count exceeds index buffer range.");

        var indices = new short[vertices.Length];
        PrimitiveSimd.FillSequentialIndices(indices.AsSpan());

        return new PrimitiveMesh(vertices, indices, primitiveType);
    }

    public static PrimitiveMesh FromSequential(VertexPositionColor[] vertices, PrimitiveType primitiveType)
    {
        if (vertices == null)
            throw new ArgumentNullException(nameof(vertices));
        if (vertices.Length == 0)
            throw new ArgumentException("Vertices collection is empty.", nameof(vertices));
        if (vertices.Length > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(vertices), "Vertex count exceeds index buffer range.");

        var indices = new short[vertices.Length];
        PrimitiveSimd.FillSequentialIndices(indices.AsSpan());

        return new PrimitiveMesh(vertices, indices, primitiveType);
    }
}

public static class PrimitiveMeshExtensions
{
    public static PrimitiveMesh Transform(this in PrimitiveMesh mesh, in Matrix transform)
    {
        if (!mesh.IsValid)
            return mesh;

        var indices = (short[])mesh.Indices.Clone();

        if (mesh.UsesTexture)
        {
            var source = mesh.TexturedVertices;
            var transformed = new VertexPositionColorTexture[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var v = source[i];
                transformed[i] = new VertexPositionColorTexture(
                    Vector3.Transform(v.Position, transform),
                    v.Color,
                    v.TextureCoordinate);
            }

            return new PrimitiveMesh(transformed, indices, mesh.PrimitiveType);
        }

        {
            var source = mesh.ColorVertices;
            var transformed = new VertexPositionColor[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var v = source[i];
                transformed[i] = new VertexPositionColor(
                    Vector3.Transform(v.Position, transform),
                    v.Color);
            }

            return new PrimitiveMesh(transformed, indices, mesh.PrimitiveType);
        }
    }

    public static PrimitiveMesh Translate(this in PrimitiveMesh mesh, in Vector3 offset) =>
        mesh.Transform(Matrix.CreateTranslation(offset));

    public static PrimitiveMesh Scale(this in PrimitiveMesh mesh, in Vector3 scale) =>
        mesh.Transform(Matrix.CreateScale(scale));

    public static PrimitiveMesh Rotate(this in PrimitiveMesh mesh, in Quaternion rotation) =>
        mesh.Transform(Matrix.CreateFromQuaternion(rotation));

    public static PrimitiveMesh Extrude(this in PrimitiveMesh mesh, in Vector3 direction, bool closeCaps = true)
    {
        if (!mesh.IsValid)
            throw new InvalidOperationException("Cannot extrude an invalid mesh.");
        if (mesh.PrimitiveType != PrimitiveType.TriangleList)
            throw new NotSupportedException("Extrusion currently supports triangle list meshes only.");
        if (direction.LengthSquared() <= Epsilon)
            return mesh;

        return mesh.UsesTexture
            ? ExtrudeTextured(mesh, direction, closeCaps)
            : ExtrudeColored(mesh, direction, closeCaps);
    }

    private static PrimitiveMesh ExtrudeTextured(in PrimitiveMesh mesh, Vector3 direction, bool closeCaps)
    {
        var top = mesh.TexturedVertices;
        var baseIndices = mesh.Indices;
        int originalVertexCount = top.Length;

        var vertices = new List<VertexPositionColorTexture>(originalVertexCount * 2);
        vertices.AddRange(top);

        for (int i = 0; i < originalVertexCount; i++)
        {
            var v = top[i];
            vertices.Add(new VertexPositionColorTexture(
                v.Position + direction,
                v.Color,
                v.TextureCoordinate));
        }

        var indices = new List<short>(closeCaps ? baseIndices.Length * 2 : baseIndices.Length + originalVertexCount * 6);
        indices.AddRange(baseIndices);

        if (closeCaps)
        {
            for (int i = 0; i < baseIndices.Length; i += 3)
            {
                short a = (short)(baseIndices[i] + originalVertexCount);
                short b = (short)(baseIndices[i + 1] + originalVertexCount);
                short c = (short)(baseIndices[i + 2] + originalVertexCount);
                indices.Add(c);
                indices.Add(b);
                indices.Add(a);
            }
        }

        var positions = new Vector3[originalVertexCount];
        for (int i = 0; i < originalVertexCount; i++)
            positions[i] = top[i].Position;

        var loops = BuildBoundaryLoops(positions, baseIndices);
        foreach (var loop in loops)
        {
            float perimeter = 0f;
            for (int i = 0; i < loop.Count; i++)
                perimeter += loop[i].Length;
            if (perimeter <= Epsilon)
                continue;

            float accumulated = 0f;
            foreach (var edge in loop)
            {
                float u0 = accumulated / perimeter;
                accumulated += edge.Length;
                float u1 = accumulated / perimeter;

                var startTop = top[edge.Start];
                var endTop = top[edge.End];
                var startBottomPos = startTop.Position + direction;
                var endBottomPos = endTop.Position + direction;

                short topStartIndex = AddVertex(vertices, new VertexPositionColorTexture(startTop.Position, startTop.Color, new Vector2(u0, 0f)));
                short bottomStartIndex = AddVertex(vertices, new VertexPositionColorTexture(startBottomPos, startTop.Color, new Vector2(u0, 1f)));
                short topEndIndex = AddVertex(vertices, new VertexPositionColorTexture(endTop.Position, endTop.Color, new Vector2(u1, 0f)));
                short bottomEndIndex = AddVertex(vertices, new VertexPositionColorTexture(endBottomPos, endTop.Color, new Vector2(u1, 1f)));

                indices.Add(topStartIndex);
                indices.Add(topEndIndex);
                indices.Add(bottomEndIndex);

                indices.Add(topStartIndex);
                indices.Add(bottomEndIndex);
                indices.Add(bottomStartIndex);
            }
        }

        return new PrimitiveMesh(vertices.ToArray(), indices.ToArray(), PrimitiveType.TriangleList);
    }

    private static PrimitiveMesh ExtrudeColored(in PrimitiveMesh mesh, Vector3 direction, bool closeCaps)
    {
        var top = mesh.ColorVertices;
        var baseIndices = mesh.Indices;
        int originalVertexCount = top.Length;

        var vertices = new List<VertexPositionColor>(originalVertexCount * 2);
        vertices.AddRange(top);

        for (int i = 0; i < originalVertexCount; i++)
        {
            var v = top[i];
            vertices.Add(new VertexPositionColor(v.Position + direction, v.Color));
        }

        var indices = new List<short>(closeCaps ? baseIndices.Length * 2 : baseIndices.Length + originalVertexCount * 6);
        indices.AddRange(baseIndices);

        if (closeCaps)
        {
            for (int i = 0; i < baseIndices.Length; i += 3)
            {
                short a = (short)(baseIndices[i] + originalVertexCount);
                short b = (short)(baseIndices[i + 1] + originalVertexCount);
                short c = (short)(baseIndices[i + 2] + originalVertexCount);
                indices.Add(c);
                indices.Add(b);
                indices.Add(a);
            }
        }

        var positions = new Vector3[originalVertexCount];
        for (int i = 0; i < originalVertexCount; i++)
            positions[i] = top[i].Position;

        var loops = BuildBoundaryLoops(positions, baseIndices);
        foreach (var loop in loops)
        {
            float perimeter = 0f;
            for (int i = 0; i < loop.Count; i++)
                perimeter += loop[i].Length;
            if (perimeter <= Epsilon)
                continue;

            float accumulated = 0f;
            foreach (var edge in loop)
            {
                float u0 = accumulated / perimeter;
                accumulated += edge.Length;
                float u1 = accumulated / perimeter;

                var startTop = top[edge.Start];
                var endTop = top[edge.End];
                var startBottomPos = startTop.Position + direction;
                var endBottomPos = endTop.Position + direction;

                short topStartIndex = AddVertex(vertices, new VertexPositionColor(startTop.Position, startTop.Color));
                short bottomStartIndex = AddVertex(vertices, new VertexPositionColor(startBottomPos, startTop.Color));
                short topEndIndex = AddVertex(vertices, new VertexPositionColor(endTop.Position, endTop.Color));
                short bottomEndIndex = AddVertex(vertices, new VertexPositionColor(endBottomPos, endTop.Color));

                indices.Add(topStartIndex);
                indices.Add(topEndIndex);
                indices.Add(bottomEndIndex);

                indices.Add(topStartIndex);
                indices.Add(bottomEndIndex);
                indices.Add(bottomStartIndex);
            }
        }

        return new PrimitiveMesh(vertices.ToArray(), indices.ToArray(), PrimitiveType.TriangleList);
    }

    public static PrimitiveMesh CurveEdges(this in PrimitiveMesh mesh, in Vector3 axis, float magnitude, float exponent = 2f)
    {
        if (!mesh.IsValid)
            return mesh;
        if (axis.LengthSquared() <= Epsilon)
            throw new ArgumentException("Axis must be non-zero.", nameof(axis));
        if (mesh.PrimitiveType != PrimitiveType.TriangleList && mesh.PrimitiveType != PrimitiveType.TriangleStrip)
            throw new NotSupportedException("CurveEdges supports triangle-based meshes.");

        float clampedExponent = Math.Max(exponent, 1e-3f);
        Vector3 axisDir = Vector3.Normalize(axis);
        var indices = (short[])mesh.Indices.Clone();

        if (mesh.UsesTexture)
        {
            var source = mesh.TexturedVertices;
            var vertices = new VertexPositionColorTexture[source.Length];
            ComputeCentroidAndRadius(source, out var centroid, out float radius);

            for (int i = 0; i < source.Length; i++)
            {
                var v = source[i];
                Vector3 diff = v.Position - centroid;
                float radial = radius <= Epsilon ? 0f : diff.Length() / radius;
                float factor = MathF.Pow(MathHelper.Clamp(radial, 0f, 1f), clampedExponent);
                Vector3 offset = axisDir * magnitude * factor;

                vertices[i] = new VertexPositionColorTexture(v.Position + offset, v.Color, v.TextureCoordinate);
            }

            return new PrimitiveMesh(vertices, indices, mesh.PrimitiveType);
        }
        else
        {
            var source = mesh.ColorVertices;
            var vertices = new VertexPositionColor[source.Length];
            ComputeCentroidAndRadius(source, out var centroid, out float radius);

            for (int i = 0; i < source.Length; i++)
            {
                var v = source[i];
                Vector3 diff = v.Position - centroid;
                float radial = radius <= Epsilon ? 0f : diff.Length() / radius;
                float factor = MathF.Pow(MathHelper.Clamp(radial, 0f, 1f), clampedExponent);
                Vector3 offset = axisDir * magnitude * factor;

                vertices[i] = new VertexPositionColor(v.Position + offset, v.Color);
            }

            return new PrimitiveMesh(vertices, indices, mesh.PrimitiveType);
        }
    }

    private static void ComputeCentroidAndRadius(VertexPositionColorTexture[] vertices, out Vector3 centroid, out float radius)
    {
        centroid = Vector3.Zero;
        for (int i = 0; i < vertices.Length; i++)
            centroid += vertices[i].Position;
        centroid /= vertices.Length;

        radius = Epsilon;
        for (int i = 0; i < vertices.Length; i++)
        {
            float length = Vector3.Distance(centroid, vertices[i].Position);
            if (length > radius)
                radius = length;
        }
    }

    private static void ComputeCentroidAndRadius(VertexPositionColor[] vertices, out Vector3 centroid, out float radius)
    {
        centroid = Vector3.Zero;
        for (int i = 0; i < vertices.Length; i++)
            centroid += vertices[i].Position;
        centroid /= vertices.Length;

        radius = Epsilon;
        for (int i = 0; i < vertices.Length; i++)
        {
            float length = Vector3.Distance(centroid, vertices[i].Position);
            if (length > radius)
                radius = length;
        }
    }

    private static List<List<OrientedEdge>> BuildBoundaryLoops(IReadOnlyList<Vector3> positions, short[] indices)
    {
        var edgeMap = new Dictionary<EdgeKey, EdgeAccumulator>();
        for (int i = 0; i < indices.Length; i += 3)
        {
            RegisterEdge(edgeMap, indices[i], indices[i + 1]);
            RegisterEdge(edgeMap, indices[i + 1], indices[i + 2]);
            RegisterEdge(edgeMap, indices[i + 2], indices[i]);
        }

        var edges = new List<OrientedEdge>();
        foreach (var pair in edgeMap)
        {
            if (pair.Value.Count == 1)
            {
                var oriented = pair.Value.Orientation;
                float length = Vector3.Distance(positions[oriented.Start], positions[oriented.End]);
                if (length > Epsilon)
                    edges.Add(new OrientedEdge(oriented.Start, oriented.End, length));
            }
        }

        var loops = new List<List<OrientedEdge>>();
        var used = new bool[edges.Count];

        for (int i = 0; i < edges.Count; i++)
        {
            if (used[i])
                continue;

            var loop = new List<OrientedEdge>();
            used[i] = true;
            loop.Add(edges[i]);

            short head = edges[i].End;
            bool closed = head == loop[0].Start;

            while (!closed)
            {
                bool found = false;
                for (int j = 0; j < edges.Count; j++)
                {
                    if (used[j])
                        continue;

                    var candidate = edges[j];
                    if (candidate.Start == head)
                    {
                        used[j] = true;
                        loop.Add(candidate);
                        head = candidate.End;
                        found = true;
                    }
                    else if (candidate.End == head)
                    {
                        candidate = candidate.Reversed();
                        edges[j] = candidate;
                        used[j] = true;
                        loop.Add(candidate);
                        head = candidate.End;
                        found = true;
                    }

                    if (found)
                    {
                        closed = head == loop[0].Start;
                        break;
                    }
                }

                if (!found)
                    break;
            }

            if (loop.Count > 1 && closed)
                loops.Add(loop);
        }

        return loops;
    }

    private static void RegisterEdge(Dictionary<EdgeKey, EdgeAccumulator> edgeMap, short start, short end)
    {
        var key = new EdgeKey(Math.Min(start, end), Math.Max(start, end));
        if (edgeMap.TryGetValue(key, out var accumulator))
        {
            accumulator.Count++;
            edgeMap[key] = accumulator;
        }
        else
        {
            edgeMap[key] = new EdgeAccumulator
            {
                Count = 1,
                Orientation = new OrientedEdge(start, end, 0f)
            };
        }
    }

    private static short AddVertex(List<VertexPositionColorTexture> vertices, VertexPositionColorTexture vertex)
    {
        if (vertices.Count >= short.MaxValue)
            throw new InvalidOperationException("Primitive mesh exceeded 16-bit vertex capacity.");
        vertices.Add(vertex);
        return (short)(vertices.Count - 1);
    }

    private static short AddVertex(List<VertexPositionColor> vertices, VertexPositionColor vertex)
    {
        if (vertices.Count >= short.MaxValue)
            throw new InvalidOperationException("Primitive mesh exceeded 16-bit vertex capacity.");
        vertices.Add(vertex);
        return (short)(vertices.Count - 1);
    }

    private readonly struct EdgeKey : IEquatable<EdgeKey>
    {
        public readonly short Min;
        public readonly short Max;

        public EdgeKey(short min, short max)
        {
            Min = min;
            Max = max;
        }

        public bool Equals(EdgeKey other) => Min == other.Min && Max == other.Max;

        public override bool Equals(object? obj) => obj is EdgeKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Min * 397) ^ Max;
            }
        }
    }

    private struct EdgeAccumulator
    {
        public int Count;
        public OrientedEdge Orientation;
    }

    private struct OrientedEdge
    {
        public short Start;
        public short End;
        public float Length;

        public OrientedEdge(short start, short end, float length)
        {
            Start = start;
            End = end;
            Length = length;
        }

        public OrientedEdge Reversed() => new OrientedEdge(End, Start, Length);
    }
}

public enum StripCapStyle
{
    None,
    Triangle,
    HalfCircle
}

public enum StripWidthAttenuation
{
    None,
    ContinuitySquared
}

public enum StripJoinStyle
{
    Perpendicular,
    Miter
}

public enum StripCurveType
{
    CatmullRom,
    Linear,
    CubicBezier,
    Hermite
}

public static class TriangleStripBuilder
{

    /// <summary>
    /// Builds a triangle strip mesh along the specified path with a uniform color. <para/>
    /// </summary>
    /// <param name="path">The path along which the strip is built.</param>
    /// <param name="width">The width of the strip.</param>
    /// <param name="color">The color of the strip.</param>
    /// <param name="upHint">An optional hint for the up vector.</param>
    /// <param name="smoothingSegments">The number of segments to use for smoothing.</param>
    /// <param name="startCap">The style of cap at the start of the strip.</param>
    /// <param name="endCap">The style of cap at the end of the strip.</param>
    /// <param name="capSegments">The number of segments to use for each cap.</param>
    /// <param name="joinStyle">The style of join between segments.</param>
    /// <param name="widthAttenuation">The style of width attenuation along the strip.</param>
    /// <param name="smoothingCurve">The curve type for smoothing.</param>
    /// <returns>A triangle strip mesh.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static PrimitiveMesh BuildStrip(IReadOnlyList<Vector3> path, float width, Color color, Vector3? upHint = null, int smoothingSegments = 0, StripCapStyle startCap = StripCapStyle.None, StripCapStyle endCap = StripCapStyle.None, int capSegments = 8, StripJoinStyle joinStyle = StripJoinStyle.Perpendicular, bool textured = true, StripCurveType smoothingCurve = StripCurveType.CatmullRom, StripWidthAttenuation widthAttenuation = StripWidthAttenuation.None)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));
        if (width <= 0f)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");

        return BuildStripCore(
            path,
            _ => color,
            _ => width,
            upHint ?? Vector3.UnitZ,
            smoothingSegments,
            startCap,
            endCap,
            capSegments,
            joinStyle,
            textured,
            smoothingCurve,
            widthAttenuation);
    }

    /// <summary>
    /// Builds a triangle strip mesh along the specified path with varying colors.
    /// </summary>
    /// <param name="path">The path along which the strip is built.</param>
    /// <param name="width">The width of the strip.</param>
    /// <param name="colors">The colors of the strip at each point.</param>
    /// <param name="upHint">An optional hint for the up vector.</param>
    /// <param name="smoothingSegments">The number of segments to use for smoothing.</param>
    /// <param name="startCap">The style of cap at the start of the strip.</param>
    /// <param name="endCap">The style of cap at the end of the strip.</param>
    /// <param name="capSegments">The number of segments to use for each cap.</param>
    /// <param name="joinStyle">The style of join between segments.</param>
    /// <param name="widthAttenuation">The style of width attenuation along the strip.</param>
    /// <param name="smoothingCurve">The curve type for smoothing.</param>
    /// <returns>A triangle strip mesh.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static PrimitiveMesh BuildStrip(IReadOnlyList<Vector3> path, float width, IReadOnlyList<Color> colors, Vector3? upHint = null, int smoothingSegments = 0, StripCapStyle startCap = StripCapStyle.None, StripCapStyle endCap = StripCapStyle.None, int capSegments = 8, StripJoinStyle joinStyle = StripJoinStyle.Perpendicular, bool textured = true, StripCurveType smoothingCurve = StripCurveType.CatmullRom, StripWidthAttenuation widthAttenuation = StripWidthAttenuation.None)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));
        if (colors == null)
            throw new ArgumentNullException(nameof(colors));
        if (colors.Count != path.Count)
            throw new ArgumentException("Color count must match path length.", nameof(colors));
        if (width <= 0f)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");

        return BuildStripCore(
            path,
            progress => SampleColor(colors, progress),
            _ => width,
            upHint ?? Vector3.UnitZ,
            smoothingSegments,
            startCap,
            endCap,
            capSegments,
            joinStyle,
            textured,
            smoothingCurve,
            widthAttenuation);
    }
    /// <summary>
    /// Builds a triangle strip mesh along the specified path. <para/>
    /// </summary>
    /// <param name="path">The path along which the strip is built.</param>
    /// <param name="widthFunc">A function that maps progress (0 to 1) to the width at that point.</param>
    /// <param name="color">The color of the strip.</param>
    /// <param name="easing">An optional easing function to apply to the progress.</param>
    /// <param name="upHint">An optional hint for the up vector.</param>
    /// <param name="smoothingSegments">The number of segments to use for smoothing.</param>
    /// <param name="startCap">The style of cap at the start of the strip.</param>
    /// <param name="endCap">The style of cap at the end of the strip.</param>
    /// <param name="capSegments">The number of segments to use for each cap.</param>
    /// <param name="joinStyle">The style of join between segments.</param>
    /// <param name="widthAttenuation">The style of width attenuation along the strip.</param>
    /// <param name="smoothingCurve">The curve type for smoothing.</param>
    /// <returns>A triangle strip mesh.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static PrimitiveMesh BuildStrip(
        IReadOnlyList<Vector3> path,
        Func<float, float> widthFunc,
        Color color,
        Func<float, float>? easing = null,
        Vector3? upHint = null,
        int smoothingSegments = 0,
        StripCapStyle startCap = StripCapStyle.None,
        StripCapStyle endCap = StripCapStyle.None,
        int capSegments = 8,
        StripJoinStyle joinStyle = StripJoinStyle.Perpendicular,
        bool textured = true,
        StripCurveType smoothingCurve = StripCurveType.CatmullRom,
        StripWidthAttenuation widthAttenuation = StripWidthAttenuation.None)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));
        if (widthFunc == null)
            throw new ArgumentNullException(nameof(widthFunc));

        return BuildStripCore(
            path,
            _ => color,
            progress => EvaluateWidth(widthFunc, easing, progress),
            upHint ?? Vector3.UnitZ,
            smoothingSegments,
            startCap,
            endCap,
            capSegments,
            joinStyle,
            textured,
            smoothingCurve,
            widthAttenuation);
    }
    /// <summary> 
    /// Builds a triangle strip mesh along the specified path with a gradient color. <para/>
    /// </summary>
    /// <param name="path">The path along which the strip is built.</param>
    /// <param name="widthFunc">A function that maps progress (0 to 1) to the width at that point.</param>
    /// <param name="colors">The colors of the strip.</param>
    /// <param name="easing">An optional easing function to apply to the progress.</param>
    /// <param name="upHint">An optional hint for the up vector.</param>
    /// <param name="smoothingSegments">The number of segments to use for smoothing.</param>
    /// <param name="startCap">The style of cap at the start of the strip.</param>
    /// <param name="endCap">The style of cap at the end of the strip.</param>
    /// <param name="capSegments">The number of segments to use for each cap.</param>
    /// <param name="joinStyle">The style of join between segments.</param>
    /// <returns>A triangle strip mesh.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static PrimitiveMesh BuildStrip(
        IReadOnlyList<Vector3> path,
        Func<float, float> widthFunc,
        IReadOnlyList<Color> colors,
        Func<float, float>? easing = null,
        Vector3? upHint = null,
        int smoothingSegments = 0,
        StripCapStyle startCap = StripCapStyle.None,
        StripCapStyle endCap = StripCapStyle.None,
        int capSegments = 8,
        StripJoinStyle joinStyle = StripJoinStyle.Perpendicular,
        bool textured = true,
        StripCurveType smoothingCurve = StripCurveType.CatmullRom,
        StripWidthAttenuation widthAttenuation = StripWidthAttenuation.None)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));
        if (colors == null)
            throw new ArgumentNullException(nameof(colors));
        if (colors.Count != path.Count)
            throw new ArgumentException("Color count must match path length.", nameof(colors));
        if (widthFunc == null)
            throw new ArgumentNullException(nameof(widthFunc));

        return BuildStripCore(
            path,
            progress => SampleColor(colors, progress),
            progress => EvaluateWidth(widthFunc, easing, progress),
            upHint ?? Vector3.UnitZ,
            smoothingSegments,
            startCap,
            endCap,
            capSegments,
            joinStyle,
            textured,
            smoothingCurve,
            widthAttenuation);
    }
    /// <summary>
    /// Builds a triangle strip mesh along the specified path with varying width and color. <para/>
    /// </summary>
    /// <param name="path">The path along which the strip is built.</param>
    /// <param name="widthFunc">A function that maps progress (0 to 1) to the width at that point.</param>
    /// <param name="colorFunc">A function that maps progress (0 to 1) to the color at that point.</param>
    /// <param name="easing">An optional easing function to apply to the progress.</param>
    /// <param name="upHint">An optional hint for the up vector.</param>
    /// <param name="smoothingSegments">The number of segments to use for smoothing.</param>
    /// <param name="startCap">The style of cap at the start of the strip.</param>
    /// <param name="endCap">The style of cap at the end of the strip.</param>
    /// <param name="capSegments">The number of segments to use for each cap.</param>
    /// <param name="joinStyle">The style of join between segments.</param>
    /// <param name="widthAttenuation">The style of width attenuation.</param>
    /// <param name="smoothingCurve">The curve type for smoothing.</param>
    /// <returns>A triangle strip mesh.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static PrimitiveMesh BuildStrip(
        IReadOnlyList<Vector3> path,
        Func<float, float> widthFunc,
        Func<float, Color> colorFunc,
        Func<float, float>? easing = null,
        Vector3? upHint = null,
        int smoothingSegments = 0,
        StripCapStyle startCap = StripCapStyle.None,
        StripCapStyle endCap = StripCapStyle.None,
        int capSegments = 8,
        StripJoinStyle joinStyle = StripJoinStyle.Perpendicular,
        bool textured = true,
        StripCurveType smoothingCurve = StripCurveType.CatmullRom,
        StripWidthAttenuation widthAttenuation = StripWidthAttenuation.None)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));
        if (widthFunc == null)
            throw new ArgumentNullException(nameof(widthFunc));
        if (colorFunc == null)
            throw new ArgumentNullException(nameof(colorFunc));

        return BuildStripCore(
            path,
            progress => colorFunc(MathHelper.Clamp(progress, 0f, 1f)),
            progress => EvaluateWidth(widthFunc, easing, progress),
            upHint ?? Vector3.UnitZ,
            smoothingSegments,
            startCap,
            endCap,
            capSegments,
            joinStyle,
            textured,
            smoothingCurve,
            widthAttenuation);
    }
    /// <summary>
    /// Core implementation for building a triangle strip mesh.
    /// </summary>
    /// <param name="path">The path along which the strip is built.</param>
    /// <param name="colorResolver">A function that resolves the color at a given progress.</param>
    /// <param name="widthResolver">A function that resolves the width at a given progress.</param>
    /// <param name="up">The up vector for the strip.</param>
    private static PrimitiveMesh BuildStripCore(
        IReadOnlyList<Vector3> path,
        Func<float, Color> colorResolver,
        Func<float, float> widthResolver,
        Vector3 up,
        int smoothingSegments,
        StripCapStyle startCap,
        StripCapStyle endCap,
        int capSegments,
        StripJoinStyle joinStyle,
        bool textured,
        StripCurveType smoothingCurve,
        StripWidthAttenuation widthAttenuation)
    {
        if (path.Count < 2)
            throw new ArgumentException("At least two points are required.", nameof(path));
        if (colorResolver == null)
            throw new ArgumentNullException(nameof(colorResolver));
        if (widthResolver == null)
            throw new ArgumentNullException(nameof(widthResolver));

        var workingPath = RemoveDegenerates(MaybeSmoothPath(path, smoothingSegments, smoothingCurve));
        var progress = ComputeProgress(workingPath, out _);

        var texturedVertices = textured ? new VertexPositionColorTexture[workingPath.Count * 2] : null;
        var colorVertices = textured ? null : new VertexPositionColor[workingPath.Count * 2];

        if ((textured ? texturedVertices!.Length : colorVertices!.Length) > short.MaxValue)
            throw new InvalidOperationException("Strip produced more vertices than supported by the index buffer.");

        var tangents = new Vector3[workingPath.Count];
        var rights = new Vector3[workingPath.Count];
        var centers = new Vector3[workingPath.Count];
        var sectionColors = new Color[workingPath.Count];
        var halfWidths = new float[workingPath.Count];

        var upNormalized = up.LengthSquared() < Epsilon ? Vector3.UnitZ : Vector3.Normalize(up);

        int segmentCount = workingPath.Count - 1;
        var segmentDirs = new Vector3[segmentCount];
        var segmentRights = new Vector3[segmentCount];

        for (int i = 0; i < segmentCount; i++)
        {
            Vector3 dir = workingPath[i + 1] - workingPath[i];
            if (dir.LengthSquared() < Epsilon)
                dir = i > 0 ? segmentDirs[i - 1] : Vector3.UnitY;
            dir.Normalize();

            Vector3 right = Vector3.Cross(upNormalized, dir);
            if (right.LengthSquared() < Epsilon)
                right = FindPerpendicular(dir);
            else
                right.Normalize();

            segmentDirs[i] = dir;
            segmentRights[i] = right;
        }

        Vector3 lastTangent = segmentDirs[0];

        for (int i = 0; i < workingPath.Count; i++)
        {
            var position = workingPath[i];
            float t = progress[i];
            float baseWidth = Math.Max(0f, widthResolver(MathHelper.Clamp(t, 0f, 1f)));

            Vector3 prevDir = segmentDirs[Math.Max(i - 1, 0)];
            Vector3 nextDir = segmentDirs[Math.Min(i, segmentCount - 1)];

            Vector3 tangent;
            if (i == 0)
                tangent = nextDir;
            else if (i == segmentCount)
                tangent = prevDir;
            else
            {
                tangent = prevDir + nextDir;
                if (tangent.LengthSquared() < Epsilon)
                    tangent = nextDir;
                else
                    tangent.Normalize();
            }

            float width = baseWidth;


            if (i > 0 && widthAttenuation == StripWidthAttenuation.ContinuitySquared)
            {
                float continuity = MathHelper.Clamp((Vector3.Dot(lastTangent, tangent) + 1f) * 0.5f, 0f, 1f);
                width *= continuity * continuity;
            }

            float halfWidth = width * 0.5f;

            Vector3 prevRight = segmentRights[Math.Max(i - 1, 0)];
            Vector3 nextRight = segmentRights[Math.Min(i, segmentCount - 1)];

            Vector3 rightOffset;
            Vector3 leftOffset;

            if (joinStyle == StripJoinStyle.Miter)
            {
                rightOffset = ComputeMiterOffset(prevRight, nextRight, i == 0, i == segmentCount, halfWidth);
                leftOffset = ComputeMiterOffset(-prevRight, -nextRight, i == 0, i == segmentCount, halfWidth);
            }
            else
            {
                Vector3 joinNormal;
                if (i == 0)
                    joinNormal = nextRight;
                else if (i == segmentCount)
                    joinNormal = prevRight;
                else
                {
                    joinNormal = prevRight + nextRight;
                    if (joinNormal.LengthSquared() < Epsilon)
                        joinNormal = nextRight;
                }

                if (joinNormal.LengthSquared() < Epsilon)
                    joinNormal = Vector3.UnitY;

                joinNormal.Normalize();
                rightOffset = joinNormal * halfWidth;
                leftOffset = -joinNormal * halfWidth;
            }

            Vector3 leftPos = position + leftOffset;
            Vector3 rightPos = position + rightOffset;
            Vector3 chord = rightPos - leftPos;
            float chordLength = chord.Length();
            Vector3 lateralDir = chordLength > Epsilon ? chord / chordLength : (nextRight.LengthSquared() > Epsilon ? nextRight : Vector3.UnitX);
            Vector3 crossCenter = (leftPos + rightPos) * 0.5f;
            float effectiveHalfWidth = chordLength * 0.5f;

            float uCoord = progress[i];
            var color = colorResolver(t);

            if (textured)
            {
                texturedVertices![i * 2] = CreateEdgeVertex(leftPos, color, uCoord, isLeft: true);
                texturedVertices[i * 2 + 1] = CreateEdgeVertex(rightPos, color, uCoord, isLeft: false);
            }
            else
            {
                colorVertices![i * 2] = new VertexPositionColor(leftPos, color);
                colorVertices[i * 2 + 1] = new VertexPositionColor(rightPos, color);
            }

            Vector3 rightForCap = lateralDir.LengthSquared() > Epsilon ? lateralDir : Vector3.UnitX;

            tangents[i] = tangent;
            rights[i] = Vector3.Normalize(rightForCap);
            centers[i] = crossCenter;
            sectionColors[i] = color;
            halfWidths[i] = effectiveHalfWidth;
            lastTangent = tangent;
        }

        bool needCaps = startCap != StripCapStyle.None || endCap != StripCapStyle.None;
        if (!needCaps)
        {
            var stripIndices = new short[textured ? texturedVertices!.Length : colorVertices!.Length];
            PrimitiveSimd.FillSequentialIndices(stripIndices.AsSpan());

            return textured
                ? new PrimitiveMesh(texturedVertices!, stripIndices, PrimitiveType.TriangleStrip)
                : new PrimitiveMesh(colorVertices!, stripIndices, PrimitiveType.TriangleStrip);
        }

        if (textured)
        {
            var vertexList = new List<VertexPositionColorTexture>(texturedVertices!);
            var indexList = new List<short>();
            AppendStripAsTriangles(indexList, vertexList.Count);

            int startLeftIndex = 0;
            int startRightIndex = 1;
            int endLeftIndex = texturedVertices!.Length - 2;
            int endRightIndex = texturedVertices!.Length - 1;
            int capSteps = Math.Max(2, capSegments);
            float startU = progress[0];
            float endU = progress[^1];

            if (startCap == StripCapStyle.Triangle)
                AddTriangleCap(vertexList, indexList, centers[0], tangents[0], rights[0], halfWidths[0], sectionColors[0], startU, startLeftIndex, startRightIndex, true);
            else if (startCap == StripCapStyle.HalfCircle)
                AddHalfCircleCap(vertexList, indexList, centers[0], tangents[0], rights[0], halfWidths[0], sectionColors[0], startU, startLeftIndex, startRightIndex, true, capSteps);

            if (endCap == StripCapStyle.Triangle)
                AddTriangleCap(vertexList, indexList, centers[^1], tangents[^1], rights[^1], halfWidths[^1], sectionColors[^1], endU, endLeftIndex, endRightIndex, false);
            else if (endCap == StripCapStyle.HalfCircle)
                AddHalfCircleCap(vertexList, indexList, centers[^1], tangents[^1], rights[^1], halfWidths[^1], sectionColors[^1], endU, endLeftIndex, endRightIndex, false, capSteps);

            return new PrimitiveMesh(vertexList.ToArray(), indexList.ToArray(), PrimitiveType.TriangleList);
        }
        else
        {
            var vertexList = new List<VertexPositionColor>(colorVertices!);
            var indexList = new List<short>();
            AppendStripAsTriangles(indexList, vertexList.Count);

            int startLeftIndex = 0;
            int startRightIndex = 1;
            int endLeftIndex = colorVertices!.Length - 2;
            int endRightIndex = colorVertices!.Length - 1;
            int capSteps = Math.Max(2, capSegments);

            if (startCap == StripCapStyle.Triangle)
                AddTriangleCap(vertexList, indexList, centers[0], tangents[0], rights[0], halfWidths[0], sectionColors[0], startLeftIndex, startRightIndex, true);
            else if (startCap == StripCapStyle.HalfCircle)
                AddHalfCircleCap(vertexList, indexList, centers[0], tangents[0], rights[0], halfWidths[0], sectionColors[0], startLeftIndex, startRightIndex, true, capSteps);

            if (endCap == StripCapStyle.Triangle)
                AddTriangleCap(vertexList, indexList, centers[^1], tangents[^1], rights[^1], halfWidths[^1], sectionColors[^1], endLeftIndex, endRightIndex, false);
            else if (endCap == StripCapStyle.HalfCircle)
                AddHalfCircleCap(vertexList, indexList, centers[^1], tangents[^1], rights[^1], halfWidths[^1], sectionColors[^1], endLeftIndex, endRightIndex, false, capSteps);

            return new PrimitiveMesh(vertexList.ToArray(), indexList.ToArray(), PrimitiveType.TriangleList);
        }
    }

    private static IReadOnlyList<Vector3> MaybeSmoothPath(IReadOnlyList<Vector3> path, int subdivisions, StripCurveType curveType)
    {
        if (subdivisions <= 0 || path.Count < 2)
            return path;

        var result = new List<Vector3>((path.Count - 1) * (subdivisions + 1) + 1);

        for (int i = 0; i < path.Count - 1; i++)
        {
            var p0 = path[Math.Max(i - 1, 0)];
            var p1 = path[i];
            var p2 = path[i + 1];
            var p3 = path[Math.Min(i + 2, path.Count - 1)];

            if (i == 0)
                result.Add(p1);

            for (int s = 1; s <= subdivisions; s++)
            {
                float t = s / (float)(subdivisions + 1);
                var point = EvaluateCurve(curveType, p0, p1, p2, p3, t, path.Count);
                result.Add(point);
            }

            result.Add(p2);
        }

        return result;
    }

    private static Vector3 EvaluateCurve(StripCurveType curveType, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t, int count)
    {
        switch (curveType)
        {
            case StripCurveType.Linear:
                return Vector3.Lerp(p1, p2, t);

            case StripCurveType.CatmullRom when count >= 4:
                return Vector3.CatmullRom(p0, p1, p2, p3, t);

            case StripCurveType.CubicBezier when count >= 4:
                {
                    Vector3 c1 = p1 + (p2 - p0) / 6f;
                    Vector3 c2 = p2 - (p3 - p1) / 6f;
                    float inv = 1f - t;
                    return inv * inv * inv * p1
                         + 3f * inv * inv * t * c1
                         + 3f * inv * t * t * c2
                         + t * t * t * p2;
                }

            case StripCurveType.Hermite when count >= 4:
                {
                    Vector3 tan1 = (p2 - p0) * 0.5f;
                    Vector3 tan2 = (p3 - p1) * 0.5f;
                    return Vector3.Hermite(p1, tan1, p2, tan2, t);
                }

            default:
                return Vector3.Lerp(p1, p2, t);
        }
    }

    private static Color SampleColor(IReadOnlyList<Color> colors, float progress)
    {
        if (colors.Count == 1)
            return colors[0];

        float scaled = MathHelper.Clamp(progress, 0f, 1f) * (colors.Count - 1);
        int index = Math.Min(colors.Count - 2, (int)MathF.Floor(scaled));
        float localT = scaled - index;
        return Color.Lerp(colors[index], colors[index + 1], localT);
    }

    private static float[] ComputeProgress(IReadOnlyList<Vector3> path, out float totalLength)
    {
        var progress = new float[path.Count];
        float cumulative = 0f;

        for (int i = 1; i < path.Count; i++)
        {
            cumulative += Vector3.Distance(path[i - 1], path[i]);
            progress[i] = cumulative;
        }

        totalLength = cumulative;

        if (cumulative > Epsilon)
        {
            float inv = 1f / cumulative;
            for (int i = 1; i < progress.Length; i++)
                progress[i] *= inv;
        }

        return progress;
    }

    private static float EvaluateWidth(Func<float, float> widthFunc, Func<float, float>? easing, float progress)
    {
        float eased = easing?.Invoke(MathHelper.Clamp(progress, 0f, 1f)) ?? MathHelper.Clamp(progress, 0f, 1f);
        return Math.Max(0f, widthFunc(MathHelper.Clamp(eased, 0f, 1f)));
    }

    private static Vector3 FindPerpendicular(Vector3 vector)
    {
        Vector3 axis = Math.Abs(vector.Y) < Math.Abs(vector.X)
            ? Vector3.UnitY
            : Vector3.UnitX;

        var perpendicular = Vector3.Cross(vector, axis);
        if (perpendicular.LengthSquared() < Epsilon)
        {
            perpendicular = Vector3.Cross(vector, Vector3.UnitZ);
        }

        perpendicular.Normalize();
        return perpendicular;
    }

    // maybe find a better name for this?
    private static IReadOnlyList<Vector3> RemoveDegenerates(IReadOnlyList<Vector3> path)
    {
        if (path.Count < 2)
            return path;

        var result = new List<Vector3>(path.Count);
        var last = path[0];
        result.Add(last);

        for (int i = 1; i < path.Count; i++)
        {
            if (Vector3.DistanceSquared(last, path[i]) <= Epsilon)
                continue;

            last = path[i];
            result.Add(last);
        }

        if (result.Count == 1)
            result.Add(path[path.Count - 1]);

        return result;
    }

    private static void AppendStripAsTriangles(List<short> indices, int vertexCount)
    {
        for (int i = 0; i < vertexCount - 2; i++)
        {
            if ((i & 1) == 0)
            {
                indices.Add((short)i);
                indices.Add((short)(i + 1));
                indices.Add((short)(i + 2));
            }
            else
            {
                indices.Add((short)(i + 1));
                indices.Add((short)i);
                indices.Add((short)(i + 2));
            }
        }
    }

    private static void AddTriangleCap(
        List<VertexPositionColorTexture> vertices,
        List<short> indices,
        Vector3 center,
        Vector3 tangent,
        Vector3 rightDir,
        float halfWidth,
        Color color,
        float uCoord,
        int leftIndex,
        int rightIndex,
        bool isStart)
    {
        if (halfWidth <= Epsilon)
            return;

        var outward = isStart ? -tangent : tangent;
        short apexIndex = AddVertex(vertices, CreateCapVertex(center + outward * halfWidth, color, uCoord, center, rightDir, halfWidth));

        if (isStart)
        {
            indices.Add(apexIndex);
            indices.Add((short)rightIndex);
            indices.Add((short)leftIndex);
        }
        else
        {
            indices.Add(apexIndex);
            indices.Add((short)leftIndex);
            indices.Add((short)rightIndex);
        }
    }

    private static void AddTriangleCap(
        List<VertexPositionColor> vertices,
        List<short> indices,
        Vector3 center,
        Vector3 tangent,
        Vector3 rightDir, // todo: remove
        float halfWidth,
        Color color,
        int leftIndex,
        int rightIndex,
        bool isStart)
    {
        if (halfWidth <= Epsilon)
            return;

        var outward = isStart ? -tangent : tangent;
        short apexIndex = AddVertex(vertices, new VertexPositionColor(center + outward * halfWidth, color));

        if (isStart)
        {
            indices.Add(apexIndex);
            indices.Add((short)rightIndex);
            indices.Add((short)leftIndex);
        }
        else
        {
            indices.Add(apexIndex);
            indices.Add((short)leftIndex);
            indices.Add((short)rightIndex);
        }
    }

    private static void AddHalfCircleCap(
        List<VertexPositionColorTexture> vertices,
        List<short> indices,
        Vector3 center,
        Vector3 tangent,
        Vector3 rightDir,
        float halfWidth,
        Color color,
        float uCoord,
        int leftIndex,
        int rightIndex,
        bool isStart,
        int segments)
    {
        if (halfWidth <= Epsilon)
            return;

        var outward = isStart ? -tangent : tangent;
        short centerIndex = AddVertex(vertices, CreateCapVertex(center, color, uCoord, center, rightDir, halfWidth));

        var arcVertices = new List<short>
        {
            isStart ? (short)rightIndex : (short)leftIndex
        };

        float step = MathF.PI / segments;
        for (int i = 1; i < segments; i++)
        {
            float angle = isStart ? step * i : MathF.PI - step * i;
            Vector3 offset = rightDir * MathF.Cos(angle) + outward * MathF.Sin(angle);
            short arcIndex = AddVertex(vertices, CreateCapVertex(center + offset * halfWidth, color, uCoord, center, rightDir, halfWidth));
            arcVertices.Add(arcIndex);
        }

        arcVertices.Add(isStart ? (short)leftIndex : (short)rightIndex);

        for (int i = 0; i < arcVertices.Count - 1; i++)
        {
            if (isStart)
            {
                indices.Add(centerIndex);
                indices.Add(arcVertices[i]);
                indices.Add(arcVertices[i + 1]);
            }
            else
            {
                indices.Add(centerIndex);
                indices.Add(arcVertices[i + 1]);
                indices.Add(arcVertices[i]);
            }
        }
    }

    private static void AddHalfCircleCap(
        List<VertexPositionColor> vertices,
        List<short> indices,
        Vector3 center,
        Vector3 tangent,
        Vector3 rightDir,
        float halfWidth,
        Color color,
        int leftIndex,
        int rightIndex,
        bool isStart,
        int segments)
    {
        if (halfWidth <= Epsilon)
            return;

        var outward = isStart ? -tangent : tangent;
        short centerIndex = AddVertex(vertices, new VertexPositionColor(center, color));

        var arcVertices = new List<short>
        {
            isStart ? (short)rightIndex : (short)leftIndex
        };

        float step = MathF.PI / segments;
        for (int i = 1; i < segments; i++)
        {
            float angle = isStart ? step * i : MathF.PI - step * i;
            Vector3 offset = rightDir * MathF.Cos(angle) + outward * MathF.Sin(angle);
            short arcIndex = AddVertex(vertices, new VertexPositionColor(center + offset * halfWidth, color));
            arcVertices.Add(arcIndex);
        }

        arcVertices.Add(isStart ? (short)leftIndex : (short)rightIndex);

        for (int i = 0; i < arcVertices.Count - 1; i++)
        {
            if (isStart)
            {
                indices.Add(centerIndex);
                indices.Add(arcVertices[i]);
                indices.Add(arcVertices[i + 1]);
            }
            else
            {
                indices.Add(centerIndex);
                indices.Add(arcVertices[i + 1]);
                indices.Add(arcVertices[i]);
            }
        }
    }

    private static short AddVertex(List<VertexPositionColorTexture> vertices, VertexPositionColorTexture vertex)
    {
        if (vertices.Count >= short.MaxValue)
            throw new InvalidOperationException("Primitive mesh exceeded 16-bit index capacity.");
        vertices.Add(vertex);
        return (short)(vertices.Count - 1);
    }

    private static short AddVertex(List<VertexPositionColor> vertices, VertexPositionColor vertex)
    {
        if (vertices.Count >= short.MaxValue)
            throw new InvalidOperationException("Primitive mesh exceeded 16-bit index capacity.");
        vertices.Add(vertex);
        return (short)(vertices.Count - 1);
    }

    private static VertexPositionColorTexture CreateEdgeVertex(Vector3 position, Color color, float u, bool isLeft)
    {
        return new VertexPositionColorTexture(
            position,
            color,
            new Vector2(MathHelper.Clamp(u, 0f, 1f), isLeft ? 0f : 1f));
    }

    private static VertexPositionColorTexture CreateCapVertex(Vector3 position, Color color, float u, Vector3 center, Vector3 rightDir, float halfWidth)
    {
        Vector3 right = rightDir.LengthSquared() > Epsilon ? Vector3.Normalize(rightDir) : Vector3.UnitX;
        float width = Math.Max(halfWidth, Epsilon);
        float lateral = MathHelper.Clamp(Vector3.Dot(position - center, right) / width, -1f, 1f);
        float v = 0.5f + 0.5f * lateral;
        return new VertexPositionColorTexture(
            position,
            color,
            new Vector2(MathHelper.Clamp(u, 0f, 1f), MathHelper.Clamp(v, 0f, 1f)));
    }

    private static Vector3 ComputeMiterOffset(Vector3 prevNormal, Vector3 nextNormal, bool isStart, bool isEnd, float halfWidth)
    {
        if (halfWidth <= Epsilon)
            return Vector3.Zero;

        if (isStart)
            return nextNormal * halfWidth;
        if (isEnd)
            return prevNormal * halfWidth;

        float prevLenSq = prevNormal.LengthSquared();
        float nextLenSq = nextNormal.LengthSquared();
        if (prevLenSq < Epsilon || nextLenSq < Epsilon)
            return (nextLenSq >= prevLenSq ? nextNormal : prevNormal) * halfWidth;

        Vector3 sum = prevNormal + nextNormal;
        float sumLenSq = sum.LengthSquared();
        if (sumLenSq < 1e-4f)
            return nextNormal * halfWidth;

        Vector3 miter = sum / MathF.Sqrt(sumLenSq);
        float denom = Vector3.Dot(miter, nextNormal);
        float absDenom = MathF.Abs(denom);
        if (absDenom <= 1e-3f)
            return nextNormal * halfWidth;

        float scale = halfWidth / denom;
        const float MiterLimit = 4f;
        float maxScale = halfWidth * MiterLimit;
        if (MathF.Abs(scale) > maxScale)
            scale = MathF.Sign(scale) * maxScale;

        return miter * scale;
    }
}

internal static class PrimitiveSimd
{
    private static readonly System.Numerics.Vector<int> LaneOffsets = CreateLaneOffsets();

    public static void FillSequentialIndices(Span<short> indices)
    {
        int i = 0;
        if (System.Numerics.Vector.IsHardwareAccelerated)
        {
            int width = System.Numerics.Vector<int>.Count;
            Span<int> temp = width <= 16
                ? stackalloc int[width]
                : new int[width];

            while (i <= indices.Length - width)
            {
                (LaneOffsets + new System.Numerics.Vector<int>(i)).CopyTo(temp);
                for (int lane = 0; lane < width; lane++)
                    indices[i + lane] = (short)temp[lane];
                i += width;
            }
        }

        for (; i < indices.Length; i++)
            indices[i] = (short)i;
    }

    private static System.Numerics.Vector<int> CreateLaneOffsets()
    {
        int width = System.Numerics.Vector<int>.Count;
        Span<int> lanes = stackalloc int[width];
        for (int i = 0; i < width; i++)
            lanes[i] = i;
        return new System.Numerics.Vector<int>(lanes);
    }
}

public static class PrimitiveShapeBuilder
{
    public static PrimitiveMesh BuildRectangularQuad(
        Vector3 center,
        Vector2 size,
        Color color,
        Vector3 normal,
        Vector3 upHint,
        bool textured = false)
    {
        BuildFrame(normal, upHint, out var right, out var up);

        var halfRight = right * (size.X * 0.5f);
        var halfUp = up * (size.Y * 0.5f);

        if (textured)
        {
            var vertices = new[]
            {
                new VertexPositionColorTexture(center - halfRight - halfUp, color, new Vector2(0f, 1f)),
                new VertexPositionColorTexture(center + halfRight - halfUp, color, new Vector2(1f, 1f)),
                new VertexPositionColorTexture(center + halfRight + halfUp, color, new Vector2(1f, 0f)),
                new VertexPositionColorTexture(center - halfRight + halfUp, color, new Vector2(0f, 0f))
            };
            var indices = new short[] { 0, 1, 2, 0, 2, 3 };
            return new PrimitiveMesh(vertices, indices, PrimitiveType.TriangleList);
        }

        var colorVertices = new[]
        {
            new VertexPositionColor(center - halfRight - halfUp, color),
            new VertexPositionColor(center + halfRight - halfUp, color),
            new VertexPositionColor(center + halfRight + halfUp, color),
            new VertexPositionColor(center - halfRight + halfUp, color)
        };
        var colorIndices = new short[] { 0, 1, 2, 0, 2, 3 };
        return new PrimitiveMesh(colorVertices, colorIndices, PrimitiveType.TriangleList);
    }

    public static PrimitiveMesh BuildRegularPolygon(
        Vector3 center,
        float radius,
        int sides,
        Color color,
        Vector3 normal,
        Vector3 upHint,
        bool textured = false)
    {
        if (sides < 3)
            throw new ArgumentOutOfRangeException(nameof(sides), "Polygon requires at least three sides.");

        BuildFrame(normal, upHint, out var right, out var up);

        if (textured)
        {
            var texturedVertices = new VertexPositionColorTexture[sides + 1];
            texturedVertices[0] = new VertexPositionColorTexture(center, color, new Vector2(0.5f, 0.5f));

            float safeRadius = Math.Max(radius, Epsilon);

            for (int i = 0; i < sides; i++)
            {
                var direction = PolarToCartesian(i, sides, right, up) * radius;
                var point = center + direction;
                float u = 0.5f + 0.5f * MathHelper.Clamp(Vector3.Dot(direction, right) / safeRadius, -1f, 1f);
                float v = 0.5f - 0.5f * MathHelper.Clamp(Vector3.Dot(direction, up) / safeRadius, -1f, 1f);
                texturedVertices[i + 1] = new VertexPositionColorTexture(point, color, new Vector2(u, v));
            }

            var indices = new short[sides * 3];
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                int baseIndex = i * 3;
                indices[baseIndex] = 0;
                indices[baseIndex + 1] = (short)(i + 1);
                indices[baseIndex + 2] = (short)(next + 1);
            }

            return new PrimitiveMesh(texturedVertices, indices, PrimitiveType.TriangleList);
        }

        var colorVertices = new VertexPositionColor[sides + 1];
        colorVertices[0] = new VertexPositionColor(center, color);

        for (int i = 0; i < sides; i++)
        {
            var direction = PolarToCartesian(i, sides, right, up) * radius;
            colorVertices[i + 1] = new VertexPositionColor(center + direction, color);
        }

        var colorIndices = new short[sides * 3];
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            int baseIndex = i * 3;
            colorIndices[baseIndex] = 0;
            colorIndices[baseIndex + 1] = (short)(i + 1);
            colorIndices[baseIndex + 2] = (short)(next + 1);
        }

        return new PrimitiveMesh(colorVertices, colorIndices, PrimitiveType.TriangleList);
    }

    public static PrimitiveMesh BuildArbitraryPolygon(IReadOnlyList<Vector3> points, Color color, bool textured = false)
    {
        if (points == null)
            throw new ArgumentNullException(nameof(points));
        if (points.Count < 3)
            throw new ArgumentOutOfRangeException(nameof(points), "Polygon requires at least three points.");

        if (textured)
        {
            float minX = points[0].X, maxX = points[0].X;
            float minY = points[0].Y, maxY = points[0].Y;
            for (int i = 1; i < points.Count; i++)
            {
                var p = points[i];
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;
            }
            float width = Math.Max(maxX - minX, Epsilon);
            float height = Math.Max(maxY - minY, Epsilon);

            var texturedVertices = new VertexPositionColorTexture[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                float u = (point.X - minX) / width;
                float v = (point.Y - minY) / height;
                texturedVertices[i] = new VertexPositionColorTexture(point, color, new Vector2(u, v));
            }

            var indices = new short[(points.Count - 2) * 3];
            for (int i = 0; i < points.Count - 2; i++)
            {
                indices[i * 3] = 0;
                indices[i * 3 + 1] = (short)(i + 1);
                indices[i * 3 + 2] = (short)(i + 2);
            }

            return new PrimitiveMesh(texturedVertices, indices, PrimitiveType.TriangleList);
        }

        var colorVertices = new VertexPositionColor[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            var point = points[i];
            colorVertices[i] = new VertexPositionColor(point, color);
        }

        var colorIndices = new short[(points.Count - 2) * 3];
        for (int i = 0; i < points.Count - 2; i++)
        {
            colorIndices[i * 3] = 0;
            colorIndices[i * 3 + 1] = (short)(i + 1);
            colorIndices[i * 3 + 2] = (short)(i + 2);
        }

        return new PrimitiveMesh(colorVertices, colorIndices, PrimitiveType.TriangleList);
    }

    public static PrimitiveMesh BuildEllipse(
        Vector3 center,
        Vector2 radii,
        int segments,
        Color color,
        Vector3 normal,
        Vector3 upHint,
        bool textured = false)
    {
        if (segments < 3)
            throw new ArgumentOutOfRangeException(nameof(segments), "Ellipse requires at least three segments.");

        BuildFrame(normal, upHint, out var right, out var up);

        if (textured)
        {
            var vertices = new VertexPositionColorTexture[segments + 1];
            vertices[0] = new VertexPositionColorTexture(center, color, new Vector2(0.5f, 0.5f));

            float safeX = Math.Max(radii.X, Epsilon);
            float safeY = Math.Max(radii.Y, Epsilon);

            for (int i = 0; i < segments; i++)
            {
                var offset = EllipseDirection(i, segments, right, up, radii);
                var point = center + offset;

                float u = 0.5f + 0.5f * MathHelper.Clamp(Vector3.Dot(offset, right) / safeX, -1f, 1f);
                float v = 0.5f - 0.5f * MathHelper.Clamp(Vector3.Dot(offset, up) / safeY, -1f, 1f);

                vertices[i + 1] = new VertexPositionColorTexture(point, color, new Vector2(u, v));
            }

            var indices = new short[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int baseIndex = i * 3;
                indices[baseIndex] = 0;
                indices[baseIndex + 1] = (short)(i + 1);
                indices[baseIndex + 2] = (short)(next + 1);
            }

            return new PrimitiveMesh(vertices, indices, PrimitiveType.TriangleList);
        }

        var colorVertices = new VertexPositionColor[segments + 1];
        colorVertices[0] = new VertexPositionColor(center, color);

        for (int i = 0; i < segments; i++)
        {
            colorVertices[i + 1] = new VertexPositionColor(center + EllipseDirection(i, segments, right, up, radii), color);
        }

        var colorIndices = new short[segments * 3];
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            int baseIndex = i * 3;
            colorIndices[baseIndex] = 0;
            colorIndices[baseIndex + 1] = (short)(i + 1);
            colorIndices[baseIndex + 2] = (short)(next + 1);
        }

        return new PrimitiveMesh(colorVertices, colorIndices, PrimitiveType.TriangleList);
    }

    public static PrimitiveMesh BuildSphere(
        Vector3 center,
        float radius,
        int latitudeSegments,
        int longitudeSegments,
        Func<Vector3, Color>? colorFunc = null,
        bool textured = false)
    {
        if (latitudeSegments < 2)
            throw new ArgumentOutOfRangeException(nameof(latitudeSegments), "Sphere requires at least two latitude segments.");
        if (longitudeSegments < 3)
            throw new ArgumentOutOfRangeException(nameof(longitudeSegments), "Sphere requires at least three longitude segments.");

        colorFunc ??= static _ => Color.White;

        int vertexRows = latitudeSegments + 1;
        int vertexCols = longitudeSegments + 1;
        int vertexCount = vertexRows * vertexCols;

        if (vertexCount > short.MaxValue)
            throw new InvalidOperationException("Sphere produced more vertices than supported by the index buffer.");

        if (textured)
        {
            var vertices = new VertexPositionColorTexture[vertexCount];
            int v = 0;

            for (int lat = 0; lat <= latitudeSegments; lat++)
            {
                float phi = MathF.PI * lat / latitudeSegments;
                float sinPhi = MathF.Sin(phi);
                float cosPhi = MathF.Cos(phi);

                float vCoord = 1f - (float)lat / latitudeSegments;

                for (int lon = 0; lon <= longitudeSegments; lon++)
                {
                    float theta = MathHelper.TwoPi * lon / longitudeSegments;
                    var normal = SphericalDirection(sinPhi, cosPhi, theta);
                    float uCoord = (float)lon / longitudeSegments;

                    vertices[v++] = new VertexPositionColorTexture(center + normal * radius, colorFunc(normal), new Vector2(uCoord, vCoord));
                }
            }

            var indices = new List<short>(latitudeSegments * longitudeSegments * 6);
            for (int lat = 0; lat < latitudeSegments; lat++)
            {
                for (int lon = 0; lon < longitudeSegments; lon++)
                {
                    int current = lat * vertexCols + lon;
                    int next = current + vertexCols;

                    indices.Add((short)current);
                    indices.Add((short)(current + 1));
                    indices.Add((short)next);

                    indices.Add((short)(current + 1));
                    indices.Add((short)(next + 1));
                    indices.Add((short)next);
                }
            }

            return new PrimitiveMesh(vertices, indices.ToArray(), PrimitiveType.TriangleList);
        }

        var colorVertices = new VertexPositionColor[vertexCount];
        int vIndex = 0;

        for (int lat = 0; lat <= latitudeSegments; lat++)
        {
            float phi = MathF.PI * lat / latitudeSegments;
            float sinPhi = MathF.Sin(phi);
            float cosPhi = MathF.Cos(phi);

            for (int lon = 0; lon <= longitudeSegments; lon++)
            {
                float theta = MathHelper.TwoPi * lon / longitudeSegments;
                var normal = SphericalDirection(sinPhi, cosPhi, theta);
                colorVertices[vIndex++] = new VertexPositionColor(center + normal * radius, colorFunc(normal));
            }
        }

        var colorIndices = new List<short>(latitudeSegments * longitudeSegments * 6);
        for (int lat = 0; lat < latitudeSegments; lat++)
        {
            for (int lon = 0; lon < longitudeSegments; lon++)
            {
                int current = lat * vertexCols + lon;
                int next = current + vertexCols;

                colorIndices.Add((short)current);
                colorIndices.Add((short)(current + 1));
                colorIndices.Add((short)next);

                colorIndices.Add((short)(current + 1));
                colorIndices.Add((short)(next + 1));
                colorIndices.Add((short)next);
            }
        }

        return new PrimitiveMesh(colorVertices, colorIndices.ToArray(), PrimitiveType.TriangleList);
    }

    public static PrimitiveMesh BuildTriangle(Vector3 a, Vector3 b, Vector3 c, Color color, bool textured = false)
    {
        if (textured)
        {
            var vertices = new[]
            {
                new VertexPositionColorTexture(a, color, new Vector2(0f, 1f)),
                new VertexPositionColorTexture(b, color, new Vector2(1f, 1f)),
                new VertexPositionColorTexture(c, color, new Vector2(0.5f, 0f))
            };
            return new PrimitiveMesh(vertices, new short[] { 0, 1, 2 }, PrimitiveType.TriangleList);
        }

        var colorVertices = new[]
        {
            new VertexPositionColor(a, color),
            new VertexPositionColor(b, color),
            new VertexPositionColor(c, color)
        };
        return new PrimitiveMesh(colorVertices, new short[] { 0, 1, 2 }, PrimitiveType.TriangleList);
    }

    public static PrimitiveMesh BuildSemiCircle(
        Vector3 center,
        float radius,
        int segments,
        Color color,
        Vector3 normal,
        Vector3 forward,
        bool textured = false)
    {
        if (segments < 2)
            throw new ArgumentOutOfRangeException(nameof(segments), "Semi-circle requires at least two segments.");

        var n = normal.LengthSquared() < Epsilon ? Vector3.Backward : Vector3.Normalize(normal);
        var f = forward.LengthSquared() < Epsilon ? Vector3.Forward : Vector3.Normalize(forward);

        if (MathF.Abs(Vector3.Dot(f, n)) > 0.999f)
            f = Vector3.Normalize(Vector3.Cross(n, Vector3.Right));

        var right = Vector3.Normalize(Vector3.Cross(n, f));
        var tangent = Vector3.Normalize(Vector3.Cross(right, n));

        if (textured)
        {
            var texturedVertices = new VertexPositionColorTexture[segments + 2];
            texturedVertices[0] = new VertexPositionColorTexture(center, color, new Vector2(0.5f, 1f));

            float safeRadius = Math.Max(radius, Epsilon); // todo: remove
            float newStep = MathF.PI / segments;

            for (int i = 0; i <= segments; i++)
            {
                float angle = newStep * i;
                Vector3 offset = right * MathF.Cos(angle) + tangent * MathF.Sin(angle);
                Vector3 point = center + offset * radius;

                float x = MathHelper.Clamp(Vector3.Dot(offset, right), -1f, 1f);
                float y = MathHelper.Clamp(Vector3.Dot(offset, tangent), 0f, 1f);

                float u = 0.5f + 0.5f * x;
                float v = 1f - y;

                texturedVertices[i + 1] = new VertexPositionColorTexture(point, color, new Vector2(u, v));
            }

            var indices = new short[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                int baseIndex = i * 3;
                indices[baseIndex] = 0;
                indices[baseIndex + 1] = (short)(i + 1);
                indices[baseIndex + 2] = (short)(i + 2);
            }

            return new PrimitiveMesh(texturedVertices, indices, PrimitiveType.TriangleList);
        }

        var colorVertices = new VertexPositionColor[segments + 2];
        colorVertices[0] = new VertexPositionColor(center, color);

        float step = MathF.PI / segments;
        for (int i = 0; i <= segments; i++)
        {
            float angle = step * i;
            Vector3 offset = right * MathF.Cos(angle) + tangent * MathF.Sin(angle);
            colorVertices[i + 1] = new VertexPositionColor(center + offset * radius, color);
        }

        var colorIndices = new short[segments * 3];
        for (int i = 0; i < segments; i++)
        {
            int baseIndex = i * 3;
            colorIndices[baseIndex] = 0;
            colorIndices[baseIndex + 1] = (short)(i + 1);
            colorIndices[baseIndex + 2] = (short)(i + 2);
        }

        return new PrimitiveMesh(colorVertices, colorIndices, PrimitiveType.TriangleList);
    }

    private static void BuildFrame(Vector3 normal, Vector3 upHint, out Vector3 right, out Vector3 up)
    {
        var n = normal.LengthSquared() < Epsilon ? Vector3.Backward : Vector3.Normalize(normal);
        up = upHint.LengthSquared() < Epsilon ? Vector3.Up : Vector3.Normalize(upHint);

        if (MathF.Abs(Vector3.Dot(n, up)) > 0.999f)
            up = Vector3.Normalize(Vector3.Cross(n, Vector3.Right));

        right = Vector3.Cross(up, n);
        if (right.LengthSquared() < Epsilon)
            right = Vector3.Cross(Vector3.Forward, n);

        right.Normalize();
        up = Vector3.Normalize(Vector3.Cross(n, right));
    }

    private static Vector3 PolarToCartesian(int index, int total, Vector3 right, Vector3 up)
    {
        float angle = MathHelper.TwoPi * index / total;
        return right * MathF.Cos(angle) + up * MathF.Sin(angle);
    }

    private static Vector3 EllipseDirection(int index, int total, Vector3 right, Vector3 up, Vector2 radii)
    {
        float angle = MathHelper.TwoPi * index / total;
        return right * (MathF.Cos(angle) * radii.X) + up * (MathF.Sin(angle) * radii.Y);
    }

    private static Vector3 SphericalDirection(float sinPhi, float cosPhi, float theta)
    {
        float cosTheta = MathF.Cos(theta);
        float sinTheta = MathF.Sin(theta);
        return new Vector3(sinPhi * cosTheta, cosPhi, sinPhi * sinTheta);
    }
}

public class TestPrimitiveRenderSystem : ModSystem
{
    private static FluidField? _fluidField;
    private static PrimitiveMesh _fluidMesh;
    private static Vector2 _previousFluidMouse;
    private static float _fluidImpulseTimer;
    private static bool _fluidPrimed;

    public override void PostDrawInterface(SpriteBatch spriteBatch)
    {
        if (!PrimitiveRenderer.IsReady)
            return;
        Main.spriteBatch.End(out var ss);

        float deltaTime = 1f / (Main.frameRate <= 0 ? 60f : Main.frameRate);
        var device = Main.graphics.GraphicsDevice;

        var path = new List<Vector3>
            {
                new Vector3(100, 100, 0),
                new Vector3(200, 150, 0),
                new Vector3(300, 100, 0),
                new Vector3(400, 150, 0),
                new Vector3(Main.MouseScreen, 0)
            };
        var gradientColors = new Color[]
        {
            Color.Orange,
            Color.White,
            Color.Red,
            Color.White,
            Color.Violet
        };

        var strip = TriangleStripBuilder.BuildStrip(path, width: 20f, gradientColors, smoothingSegments: 0, joinStyle: StripJoinStyle.Miter, textured: false);

        var world = Matrix.Identity;
        var view = Matrix.Identity;
        var projection = Matrix.CreateOrthographicOffCenter(
            0, Main.screenWidth,
            Main.screenHeight, 0,
         -500f, 500f);

        var fluid = _fluidField ??= new FluidField(
            gridSize: 60,
            diffusion: 0.000008f,
            viscosity: 0.0004f,
            origin: new Vector2(720f, 520f),
            cellSize: 8f);
        fluid.SyncTilesFromWorld();
        Vector2 fluidMouse = Main.MouseScreen;
        if (!_fluidPrimed)
        {
            _previousFluidMouse = fluidMouse;
            _fluidPrimed = true;
        }
        Vector2 fluidVelocity = (fluidMouse - _previousFluidMouse) / Math.Max(deltaTime, Epsilon);

        if (Main.mouseLeft)
            fluid.AddImpulse(fluidMouse, fluidVelocity * 0.00015f, 0.45f);

        if (_fluidImpulseTimer <= 0f)
        {
            float width = fluid.Resolution * fluid.CellSize;
            Vector2 fountain = fluid.Origin + new Vector2(width * 0.5f, width * 0.1f);
            fluid.AddImpulse(fountain, new Vector2(Main.rand.NextFloat(-1f, 1f), 14f), 30.9f);
            _fluidImpulseTimer = 0.1f;
        }
        else
        {
            _fluidImpulseTimer -= deltaTime;
        }

        fluid.Step(deltaTime);
        _fluidMesh = fluid.BuildDensityMesh(new Color(0, 0, 0, 0), new Color(255, 255, 255));
        PrimitiveRenderer.DrawMesh(Matrix.Identity, view, projection, _fluidMesh, blendState: BlendState.AlphaBlend);

        Main.spriteBatch.Begin(ss);
    }

}