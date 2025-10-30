using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using Cataphract.Common.Rendering;
using System.Diagnostics;
using Cataphract.Common.Easing;

namespace Cataphract.Common;
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
            TextureEnabled = false,
            LightingEnabled = false,
            FogEnabled = false
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

        // todo: Support an arbitrary effect

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
            device.DrawUserIndexedPrimitives(
                mesh.PrimitiveType,
                mesh.Vertices,
                0,
                mesh.Vertices.Length,
                mesh.Indices,
                0,
                primitiveCount);
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
    public PrimitiveMesh(VertexPositionColor[] vertices, short[] indices, PrimitiveType primitiveType)
    {
        Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
        Indices = indices ?? throw new ArgumentNullException(nameof(indices));
        PrimitiveType = primitiveType;

        if (Vertices.Length == 0 || Indices.Length == 0)
            throw new ArgumentException("Mesh must contain vertices and indices.");
        if (Vertices.Length > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(vertices), "Vertex count exceeds index buffer range.");
    }

    public VertexPositionColor[] Vertices { get; }
    public short[] Indices { get; }
    public PrimitiveType PrimitiveType { get; }
    public bool IsValid => Vertices.Length > 0 && Indices.Length > 0;

    public static PrimitiveMesh FromSequential(VertexPositionColor[] vertices, PrimitiveType primitiveType)
    {
        if (vertices == null)
            throw new ArgumentNullException(nameof(vertices));
        if (vertices.Length == 0)
            throw new ArgumentException("Vertices collection is empty.", nameof(vertices));
        if (vertices.Length > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(vertices), "Vertex count exceeds index buffer range.");

        var indices = new short[vertices.Length];
        for (short i = 0; i < indices.Length; i++)
            indices[i] = i;

        return new PrimitiveMesh(vertices, indices, primitiveType);
    }
}

public enum StripCapStyle
{
    None,
    Triangle,
    HalfCircle
}

public static class TriangleStripBuilder
{
    private const float Epsilon = 1e-6f;

    public static PrimitiveMesh BuildStrip(IReadOnlyList<Vector3> path, float width, Color color, Vector3? upHint = null, int smoothingSegments = 0, StripCapStyle startCap = StripCapStyle.None, StripCapStyle endCap = StripCapStyle.None, int capSegments = 8)
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
            capSegments);
    }

    public static PrimitiveMesh BuildStrip(IReadOnlyList<Vector3> path, float width, IReadOnlyList<Color> colors, Vector3? upHint = null, int smoothingSegments = 0, StripCapStyle startCap = StripCapStyle.None, StripCapStyle endCap = StripCapStyle.None, int capSegments = 8)
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
            capSegments);
    }

    public static PrimitiveMesh BuildStrip(
        IReadOnlyList<Vector3> path,
        Func<float, float> widthFunc,
        Color color,
        Func<float, float>? easing = null,
        Vector3? upHint = null,
        int smoothingSegments = 0,
        StripCapStyle startCap = StripCapStyle.None,
        StripCapStyle endCap = StripCapStyle.None,
        int capSegments = 8)
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
            capSegments);
    }

    public static PrimitiveMesh BuildStrip(
        IReadOnlyList<Vector3> path,
        Func<float, float> widthFunc,
        IReadOnlyList<Color> colors,
        Func<float, float>? easing = null,
        Vector3? upHint = null,
        int smoothingSegments = 0,
        StripCapStyle startCap = StripCapStyle.None,
        StripCapStyle endCap = StripCapStyle.None,
        int capSegments = 8)
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
            capSegments);
    }

    public static PrimitiveMesh BuildStrip(
        IReadOnlyList<Vector3> path,
        Func<float, float> widthFunc,
        Func<float, Color> colorFunc,
        Func<float, float>? easing = null,
        Vector3? upHint = null,
        int smoothingSegments = 0,
        StripCapStyle startCap = StripCapStyle.None,
        StripCapStyle endCap = StripCapStyle.None,
        int capSegments = 8)
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
            capSegments);
    }

    private static PrimitiveMesh BuildStripCore(
        IReadOnlyList<Vector3> path,
        Func<float, Color> colorResolver,
        Func<float, float> widthResolver,
        Vector3 up,
        int smoothingSegments,
        StripCapStyle startCap,
        StripCapStyle endCap,
        int capSegments)
    {
        if (path.Count < 2)
            throw new ArgumentException("At least two points are required.", nameof(path));
        if (colorResolver == null)
            throw new ArgumentNullException(nameof(colorResolver));
        if (widthResolver == null)
            throw new ArgumentNullException(nameof(widthResolver));

        var workingPath = RemoveDegenerates(MaybeSmoothPath(path, smoothingSegments));
        var progress = ComputeProgress(workingPath, out _);
        var vertices = new VertexPositionColor[workingPath.Count * 2];

        if (vertices.Length > short.MaxValue)
            throw new InvalidOperationException("Strip produced more vertices than supported by the index buffer.");

        var tangents = new Vector3[workingPath.Count];
        var rights = new Vector3[workingPath.Count];
        var centers = new Vector3[workingPath.Count];
        var sectionColors = new Color[workingPath.Count];
        var halfWidths = new float[workingPath.Count];

        var upNormalized = up.LengthSquared() < Epsilon ? Vector3.UnitZ : Vector3.Normalize(up);
        Vector3 transportedUp = upNormalized;
        Vector3 transportedRight = Vector3.Zero;
        Vector3 lastTangent = Vector3.Zero;
        Vector3 lastRight = Vector3.Zero;

        for (int i = 0; i < workingPath.Count; i++)
        {
            var position = workingPath[i];
            float t = progress[i];
            float baseWidth = Math.Max(0f, widthResolver(MathHelper.Clamp(t, 0f, 1f)));
            float halfWidth; // = baseWidth * 0.5f;

            var prev = i > 0 ? workingPath[i - 1] : position;
            var next = i < workingPath.Count - 1 ? workingPath[i + 1] : position;

            var forward = next - position;
            var backward = position - prev;

            bool hasForward = forward.LengthSquared() >= Epsilon;
            bool hasBackward = backward.LengthSquared() >= Epsilon;

            if (hasForward)
                forward.Normalize();
            if (hasBackward)
                backward.Normalize();

            Vector3 tangent = Vector3.Zero;
            if (hasForward)
                tangent += forward;
            if (hasBackward)
                tangent += backward;
            if (tangent.LengthSquared() < Epsilon)
                tangent = hasForward ? forward : hasBackward ? backward : lastTangent != Vector3.Zero ? lastTangent : Vector3.UnitY;

            tangent.Normalize();

            if (i == 0)
            {
                transportedRight = Vector3.Cross(transportedUp, tangent);
                if (transportedRight.LengthSquared() < Epsilon)
                    transportedRight = FindPerpendicular(tangent);
                else
                    transportedRight.Normalize();

                transportedUp = Vector3.Normalize(Vector3.Cross(tangent, transportedRight));
            }
            else
            {
                Vector3 projectedRight = transportedRight - tangent * Vector3.Dot(transportedRight, tangent);

                if (projectedRight.LengthSquared() < Epsilon)
                    projectedRight = FindPerpendicular(tangent);
                else
                    projectedRight.Normalize();

                if (lastRight != Vector3.Zero && Vector3.Dot(projectedRight, lastRight) < 0f)
                    projectedRight = -projectedRight;

                transportedRight = projectedRight;
                transportedUp = Vector3.Normalize(Vector3.Cross(tangent, transportedRight));
            }

            float width = baseWidth;
            if (i > 0)
            {
                float continuity = MathHelper.Clamp((Vector3.Dot(lastTangent, tangent) + 1f) * 0.5f, 0f, 1f);
                width *= continuity * continuity;
            }

            halfWidth = width * 0.5f;

            lastTangent = tangent;
            lastRight = transportedRight;

            var right = transportedRight;
            var offset = halfWidth > 0f ? right * halfWidth : Vector3.Zero;
            var color = colorResolver(t);

            vertices[i * 2] = new VertexPositionColor(position - offset, color);
            vertices[i * 2 + 1] = new VertexPositionColor(position + offset, color);

            tangents[i] = tangent;
            rights[i] = transportedRight;
            centers[i] = (vertices[i * 2].Position + vertices[i * 2 + 1].Position) * 0.5f;
            sectionColors[i] = color;
            halfWidths[i] = halfWidth;
        }

        bool needCaps = startCap != StripCapStyle.None || endCap != StripCapStyle.None;
        if (!needCaps)
        {
            var stripIndices = new short[vertices.Length];
            for (short i = 0; i < stripIndices.Length; i++)
                stripIndices[i] = i;
            return new PrimitiveMesh(vertices, stripIndices, PrimitiveType.TriangleStrip);
        }

        var vertexList = new List<VertexPositionColor>(vertices);
        var indexList = new List<short>();
        AppendStripAsTriangles(indexList, vertexList.Count);

        int startLeftIndex = 0;
        int startRightIndex = 1;
        int endLeftIndex = vertices.Length - 2;
        int endRightIndex = vertices.Length - 1;
        int capSteps = Math.Max(2, capSegments);

        if (startCap == StripCapStyle.Triangle)
        {
            AddTriangleCap(vertexList, indexList, centers[0], tangents[0], halfWidths[0], sectionColors[0], startLeftIndex, startRightIndex, true);
        }
        else if (startCap == StripCapStyle.HalfCircle)
        {
            AddHalfCircleCap(vertexList, indexList, centers[0], tangents[0], rights[0], halfWidths[0], sectionColors[0], startLeftIndex, startRightIndex, true, capSteps);
        }

        if (endCap == StripCapStyle.Triangle)
        {
            AddTriangleCap(vertexList, indexList, centers[^1], tangents[^1], halfWidths[^1], sectionColors[^1], endLeftIndex, endRightIndex, false);
        }
        else if (endCap == StripCapStyle.HalfCircle)
        {
            AddHalfCircleCap(vertexList, indexList, centers[^1], tangents[^1], rights[^1], halfWidths[^1], sectionColors[^1], endLeftIndex, endRightIndex, false, capSteps);
        }

        return new PrimitiveMesh(vertexList.ToArray(), indexList.ToArray(), PrimitiveType.TriangleList);
    }

    private static IReadOnlyList<Vector3> MaybeSmoothPath(IReadOnlyList<Vector3> path, int subdivisions)
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
                var point = path.Count >= 4
                    ? Vector3.CatmullRom(p0, p1, p2, p3, t)
                    : Vector3.Lerp(p1, p2, t);
                result.Add(point);
            }

            result.Add(p2);
        }

        return result;
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
        List<VertexPositionColor> vertices,
        List<short> indices,
        Vector3 center,
        Vector3 tangent,
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
        List<VertexPositionColor> vertices,
        List<short> indices,
        Vector3 center,
        Vector3 tangent,
        Vector3 right,
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
            Vector3 offset = right * MathF.Cos(angle) + outward * MathF.Sin(angle);
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

    private static short AddVertex(List<VertexPositionColor> vertices, VertexPositionColor vertex)
    {
        if (vertices.Count >= short.MaxValue)
            throw new InvalidOperationException("Primitive mesh exceeded 16-bit index capacity.");
        vertices.Add(vertex);
        return (short)(vertices.Count - 1);
    }
}

public static class PrimitiveShapeBuilder
{
    private const float Epsilon = 1e-6f;

    public static PrimitiveMesh BuildRectangularQuad(
        Vector3 center,
        Vector2 size,
        Color color,
        Vector3 normal,
        Vector3 upHint)
    {
        BuildFrame(normal, upHint, out var right, out var up);

        var halfRight = right * (size.X * 0.5f);
        var halfUp = up * (size.Y * 0.5f);

        var vertices = new[]
        {
            new VertexPositionColor(center - halfRight - halfUp, color),
            new VertexPositionColor(center + halfRight - halfUp, color),
            new VertexPositionColor(center + halfRight + halfUp, color),
            new VertexPositionColor(center - halfRight + halfUp, color)
        };

        var indices = new short[] { 0, 1, 2, 0, 2, 3 };
        return new PrimitiveMesh(vertices, indices, PrimitiveType.TriangleList);
    }

    public static PrimitiveMesh BuildRegularPolygon(
        Vector3 center,
        float radius,
        int sides,
        Color color,
        Vector3 normal,
        Vector3 upHint)
    {
        if (sides < 3)
            throw new ArgumentOutOfRangeException(nameof(sides), "Polygon requires at least three sides.");

        BuildFrame(normal, upHint, out var right, out var up);

        var vertices = new VertexPositionColor[sides + 1];
        vertices[0] = new VertexPositionColor(center, color);

        for (int i = 0; i < sides; i++)
        {
            var direction = PolarToCartesian(i, sides, right, up) * radius;
            vertices[i + 1] = new VertexPositionColor(center + direction, color);
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

        return new PrimitiveMesh(vertices, indices, PrimitiveType.TriangleList);
    }

    public static PrimitiveMesh BuildEllipse(
        Vector3 center,
        Vector2 radii,
        int segments,
        Color color,
        Vector3 normal,
        Vector3 upHint)
    {
        if (segments < 3)
            throw new ArgumentOutOfRangeException(nameof(segments), "Ellipse requires at least three segments.");

        BuildFrame(normal, upHint, out var right, out var up);

        var vertices = new VertexPositionColor[segments + 1];
        vertices[0] = new VertexPositionColor(center, color);

        for (int i = 0; i < segments; i++)
        {
            vertices[i + 1] = new VertexPositionColor(center + EllipseDirection(i, segments, right, up, radii), color);
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

    public static PrimitiveMesh BuildSphere(
        Vector3 center,
        float radius,
        int latitudeSegments,
        int longitudeSegments,
        Func<Vector3, Color>? colorFunc = null)
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

        var vertices = new VertexPositionColor[vertexCount];
        int v = 0;

        for (int lat = 0; lat <= latitudeSegments; lat++)
        {
            float phi = MathF.PI * lat / latitudeSegments;
            float sinPhi = MathF.Sin(phi);
            float cosPhi = MathF.Cos(phi);

            for (int lon = 0; lon <= longitudeSegments; lon++)
            {
                float theta = MathHelper.TwoPi * lon / longitudeSegments;
                var normal = SphericalDirection(sinPhi, cosPhi, theta);
                vertices[v++] = new VertexPositionColor(center + normal * radius, colorFunc(normal));
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

    public static PrimitiveMesh BuildTriangle(Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        var vertices = new[]
        {
            new VertexPositionColor(a, color),
            new VertexPositionColor(b, color),
            new VertexPositionColor(c, color)
        };
        return new PrimitiveMesh(vertices, [0, 1, 2], PrimitiveType.TriangleList);
    }

    public static PrimitiveMesh BuildSemiCircle(
        Vector3 center,
        float radius,
        int segments,
        Color color,
        Vector3 normal,
        Vector3 forward)
    {
        if (segments < 2)
            throw new ArgumentOutOfRangeException(nameof(segments), "Semi-circle requires at least two segments.");

        var n = normal.LengthSquared() < Epsilon ? Vector3.Backward : Vector3.Normalize(normal);
        var f = forward.LengthSquared() < Epsilon ? Vector3.Forward : Vector3.Normalize(forward);

        if (MathF.Abs(Vector3.Dot(f, n)) > 0.999f)
            f = Vector3.Normalize(Vector3.Cross(n, Vector3.Right));

        var right = Vector3.Normalize(Vector3.Cross(n, f));
        var tangent = Vector3.Normalize(Vector3.Cross(right, n));

        var vertices = new VertexPositionColor[segments + 2];
        vertices[0] = new VertexPositionColor(center, color);

        float step = MathF.PI / segments;
        for (int i = 0; i <= segments; i++)
        {
            float angle = step * i;
            Vector3 offset = right * MathF.Cos(angle) + tangent * MathF.Sin(angle);
            vertices[i + 1] = new VertexPositionColor(center + offset * radius, color);
        }

        var indices = new short[segments * 3];
        for (int i = 0; i < segments; i++)
        {
            int baseIndex = i * 3;
            indices[baseIndex] = 0;
            indices[baseIndex + 1] = (short)(i + 1);
            indices[baseIndex + 2] = (short)(i + 2);
        }

        return new PrimitiveMesh(vertices, indices, PrimitiveType.TriangleList);
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
    public override void PostDrawInterface(SpriteBatch spriteBatch)
    {
        if (!PrimitiveRenderer.IsReady)
            return;

        Main.spriteBatch.End(out var ss);

        var path = new List<Vector3>
            {
                new Vector3(100, 100, 0),
                new Vector3(200, 150, 0),
                new Vector3(300, 100, 0),
                new Vector3(400, 150, 0),
                new Vector3(400 * MathF.Abs(MathF.Cos(Main.GlobalTimeWrappedHourly)), 200 * MathF.Sin(Main.GlobalTimeWrappedHourly), 0)
            };

        var strip = TriangleStripBuilder.BuildStrip(path, width: 20f, Color.Red, smoothingSegments: 2);

        var world = Matrix.Identity;
        var view = Matrix.Identity;
        var projection = Matrix.CreateOrthographicOffCenter(
            0, Main.screenWidth,
            Main.screenHeight, 0,
            -500f, 500f);

        PrimitiveRenderer.DrawMesh(
            world, view, projection,
            strip,
            blendState: BlendState.AlphaBlend);

        var gradientColors = new Color[]
        {
            Color.Red,
            Color.Yellow,
            Color.Green,
            Color.Blue,
            Color.Purple
        };

        var coloredStrip = TriangleStripBuilder.BuildStrip(
            path,
            t => MathHelper.Lerp(0f, 40f, t),
            gradientColors,
            easing: Easing.Easing.InOutSine,
            smoothingSegments: 128,
            startCap: StripCapStyle.HalfCircle,
            endCap: StripCapStyle.Triangle,
            capSegments: 16);

        world = Matrix.CreateTranslation(0, 300, 0);

        PrimitiveRenderer.DrawMesh(
            world, view, projection,
            coloredStrip,
            blendState: BlendState.AlphaBlend);

        var customStrip = TriangleStripBuilder.BuildStrip(
            path,
            t => MathHelper.Lerp(30f, 10f, t),
            progress => Color.Lerp(Color.Aquamarine, Color.MediumPurple, progress),
            easing: Easing.Easing.InOutSine,
            upHint: Vector3.UnitZ,
            smoothingSegments: 64,
            startCap: StripCapStyle.Triangle,
            endCap: StripCapStyle.HalfCircle,
            capSegments: 12);

        world = Matrix.CreateTranslation(0, 540, 0);

        PrimitiveRenderer.DrawMesh(
            world, view, projection,
            customStrip,
            blendState: BlendState.AlphaBlend);

        var quad = PrimitiveShapeBuilder.BuildRectangularQuad(
            new Vector3(560f, 140f, 0f),
            new Vector2(120f, 70f),
            Color.CadetBlue,
            Vector3.Backward,
            Vector3.Up);

        PrimitiveRenderer.DrawMesh(
            Matrix.Identity, view, projection,
            quad,
            blendState: BlendState.AlphaBlend);

        var hexagon = PrimitiveShapeBuilder.BuildRegularPolygon(
            new Vector3(720f, 160f, 0f),
            radius: 60f,
            sides: 6,
            color: Color.Orange,
            normal: Vector3.Backward,
            upHint: Vector3.Up);

        PrimitiveRenderer.DrawMesh(
            Matrix.Identity, view, projection,
            hexagon,
            blendState: BlendState.AlphaBlend);

        var ellipse = PrimitiveShapeBuilder.BuildEllipse(
            new Vector3(600f, 420f, 0f),
            new Vector2(90f, 45f),
            segments: 48,
            color: Color.MediumPurple,
            normal: Vector3.Backward,
            upHint: Vector3.Up);

        PrimitiveRenderer.DrawMesh(
            Matrix.Identity, view, projection,
            ellipse,
            blendState: BlendState.AlphaBlend);

        var semiCircle = PrimitiveShapeBuilder.BuildSemiCircle(
            new Vector3(860f, 420f, 0f),
            radius: 60f,
            segments: 24,
            color: Color.Crimson,
            normal: Vector3.Backward,
            forward: Vector3.UnitX);

        PrimitiveRenderer.DrawMesh(
            Matrix.Identity, view, projection,
            semiCircle,
            blendState: BlendState.AlphaBlend);

        var triangle = PrimitiveShapeBuilder.BuildTriangle(
            new Vector3(900f, 120f, 0f),
            new Vector3(980f, 200f, 0f),
            new Vector3(820f, 200f, 0f),
            Color.ForestGreen);

        PrimitiveRenderer.DrawMesh(
            Matrix.Identity, view, projection,
            triangle,
            blendState: BlendState.AlphaBlend);

        var sphere = PrimitiveShapeBuilder.BuildSphere(
            new Vector3(100f, 420f, 0f),
            radius: 80f,
            latitudeSegments: 8,
            longitudeSegments: 8,
            colorFunc: normal => Color.Lerp(Color.White, Color.Purple, (normal.Y + 1f) * 0.5f));

        PrimitiveRenderer.DrawMesh(
            Matrix.Identity, view, projection,
            sphere,
            blendState: BlendState.AlphaBlend);

        Main.spriteBatch.Begin(ss);
    }
}