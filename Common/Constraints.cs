using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace Cataphract.Common;

public interface IFabrikJointConstraint
{
    Vector3 Constrain(Vector3 root, Vector3 parent, Vector3 current);
}

public sealed class FabrikPlaneConstraint : IFabrikJointConstraint
{
    private readonly Vector3 _point;
    private readonly Vector3 _normal;
    private readonly bool _followRoot;

    public FabrikPlaneConstraint(Vector3 point, Vector3 normal, bool followRoot = false)
    {
        _point = point;
        _normal = normal.LengthSquared() < 1e-6f ? Vector3.UnitZ : Vector3.Normalize(normal);
        _followRoot = followRoot;
    }

    public Vector3 Constrain(Vector3 root, Vector3 parent, Vector3 current)
    {
        var planePoint = _followRoot ? root : _point;
        float distance = Vector3.Dot(current - planePoint, _normal);
        return current - _normal * distance;
    }
}

/// <summary>
/// Represents a constraint for a Fabrik joint that constrains the joint to rotation within a sphere.
/// </summary>
public sealed class FabrikSphereConstraint : IFabrikJointConstraint
{
    private readonly float _minRadius;
    private readonly float _maxRadius;
    private readonly bool _followRoot;
    private readonly Vector3 _center;

    public FabrikSphereConstraint(float minRadius, float maxRadius, bool followRoot = true, Vector3 center = default)
    {
        _minRadius = Math.Max(0f, Math.Min(minRadius, maxRadius));
        _maxRadius = Math.Max(_minRadius, maxRadius);
        _followRoot = followRoot;
        _center = center;
    }

    public Vector3 Constrain(Vector3 root, Vector3 parent, Vector3 current)
    {
        Vector3 center = _followRoot ? root : _center;
        Vector3 offset = current - center;
        float distance = offset.Length();
        float clamped = MathHelper.Clamp(distance, _minRadius, _maxRadius);

        if (distance <= 1e-6f)
            return center + Vector3.UnitX * clamped;

        if (Math.Abs(clamped - distance) <= float.Epsilon)
            return current;

        return center + offset * (clamped / distance);
    }
}

/// <summary>
/// Represents a constraint for a Fabrik joint that constrains the joint to rotation within a cone.
/// </summary>
public sealed class FabrikConeConstraint : IFabrikJointConstraint
{
    private readonly float _cosLimit;
    private readonly float _sinLimit;
    private readonly Vector3 _axisHint;
    private readonly bool _useParentAxis;

    public FabrikConeConstraint(float maxAngleDegrees, Vector3 axisHint, bool useParentAxis = true)
    {
        float radians = MathHelper.ToRadians(MathHelper.Clamp(maxAngleDegrees, 0f, 175f));
        _cosLimit = MathF.Cos(radians);
        _sinLimit = MathF.Sin(radians);
        _axisHint = axisHint;
        _useParentAxis = useParentAxis;
    }

    public Vector3 Constrain(Vector3 root, Vector3 parent, Vector3 current)
    {
        Vector3 dir = current - parent;
        float length = dir.Length();
        if (length <= 1e-6f)
            return parent;

        dir /= length;

        Vector3 axis = Vector3.UnitX;
        if (_useParentAxis && parent != root)
            axis = Vector3.Normalize(parent - root);
        else if (_axisHint.LengthSquared() > 1e-6f)
            axis = Vector3.Normalize(_axisHint);

        if (axis.LengthSquared() <= 1e-6f)
            axis = Vector3.UnitX;

        float dot = Vector3.Dot(dir, axis);
        if (dot >= _cosLimit)
            return current;

        Vector3 lateral = dir - axis * dot;
        if (lateral.LengthSquared() <= 1e-6f)
            lateral = Vector3.Normalize(Vector3.Cross(axis, Vector3.UnitY));
        else
            lateral = Vector3.Normalize(lateral);

        Vector3 limitedDir = axis * _cosLimit + lateral * _sinLimit;
        return parent + limitedDir * length;
    }
}

/// <summary>
/// Represents a composite constraint for a Fabrik joint that applies multiple constraints in sequence.
/// </summary>
public sealed class FabrikCompositeConstraint : IFabrikJointConstraint
{
    private readonly IFabrikJointConstraint[] _constraints;

    public FabrikCompositeConstraint(params IFabrikJointConstraint[] constraints)
    {
        _constraints = constraints ?? Array.Empty<IFabrikJointConstraint>();
    }

    public Vector3 Constrain(Vector3 root, Vector3 parent, Vector3 current)
    {
        foreach (var constraint in _constraints)
            current = constraint.Constrain(root, parent, current);
        return current;
    }
}

public sealed class FabrikChain
{
    private readonly Vector3[] _joints;
    private readonly float[] _segmentLengths;
    private readonly float _chainLength;
    private readonly IFabrikJointConstraint?[] _constraints;
    private readonly Vector3[] _previousJoints;
    private const float Epsilon = 1e-6f;

    public float Responsiveness { get; set; } = 18f;

    public FabrikChain(IReadOnlyList<Vector3> initialJoints)
    {
        if (initialJoints == null)
            throw new ArgumentNullException(nameof(initialJoints));
        if (initialJoints.Count < 2)
            throw new ArgumentOutOfRangeException(nameof(initialJoints), "FABRIK requires at least two joints.");

        _joints = new Vector3[initialJoints.Count];
        for (int i = 0; i < initialJoints.Count; i++)
            _joints[i] = initialJoints[i];

        _segmentLengths = new float[_joints.Length - 1];
        float total = 0f;

        for (int i = 0; i < _segmentLengths.Length; i++)
        {
            float length = Vector3.Distance(_joints[i + 1], _joints[i]);
            if (length <= Epsilon)
                length = Epsilon;

            _segmentLengths[i] = length;
            total += length;
        }

        _chainLength = total;
        _constraints = new IFabrikJointConstraint[_joints.Length];
        _previousJoints = new Vector3[_joints.Length];
        Array.Copy(_joints, _previousJoints, _joints.Length);
    }

    public int JointCount => _joints.Length;
    public ReadOnlySpan<Vector3> Joints => _joints;
    public float TotalLength => _chainLength;

    public void ResetJoint(int index, Vector3 position, bool resetHistory = false)
    {
        if (index < 0 || index >= _joints.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        _joints[index] = position;
        if (resetHistory)
            _previousJoints[index] = position;
    }

    public void SetConstraint(int index, IFabrikJointConstraint? constraint)
    {
        if (index < 0 || index >= _constraints.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        _constraints[index] = constraint;
    }

    private void ApplyConstraint(int index, int parentIndex)
    {
        if (index < 0 || index >= _constraints.Length)
            return;

        var constraint = _constraints[index];
        if (constraint == null)
            return;

        Vector3 parent = _joints[Math.Clamp(parentIndex, 0, _joints.Length - 1)];
        _joints[index] = constraint.Constrain(_joints[0], parent, _joints[index]);
    }

    /// <summary>
    /// Solves the FABRIK chain for the given target position.
    /// </summary>
    /// <param name="target">The target position to solve for.</param>
    /// <param name="rootOverride">The root position to use instead of the current root.</param>
    /// <param name="tolerance">The tolerance for convergence.</param>
    /// <param name="maxIterations">The maximum number of iterations to perform.</param>
    /// <param name="deltaTime">The delta time for temporal smoothing.</param>
    public void Solve(Vector3 target, Vector3? rootOverride = null, float tolerance = 1e-3f, int maxIterations = 12, float deltaTime = 1f / 60f)
    {
        tolerance = Math.Max(Epsilon, tolerance);
        maxIterations = Math.Max(1, maxIterations);
        deltaTime = Math.Max(1e-4f, deltaTime);

        Vector3 root = rootOverride ?? _joints[0];
        _joints[0] = root;
        ApplyConstraint(0, 0);

        float targetDist = Vector3.Distance(target, root);
        if (targetDist >= _chainLength)
        {
            for (int i = 0; i < _segmentLengths.Length; i++)
            {
                Vector3 dir = Vector3.Normalize(target - _joints[i]);
                if (dir.LengthSquared() <= Epsilon)
                    dir = Vector3.UnitY;

                _joints[i + 1] = _joints[i] + dir * _segmentLengths[i];
                ApplyConstraint(i + 1, i);
            }

            ApplyTemporalSmoothing(root, deltaTime);
            return;
        }

        float toleranceSq = tolerance * tolerance;
        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            _joints[^1] = target;
            ApplyConstraint(_joints.Length - 1, _joints.Length - 2);

            for (int i = _joints.Length - 2; i >= 0; i--)
            {
                Vector3 dir = _joints[i] - _joints[i + 1];
                float dist = dir.Length();
                if (dist <= Epsilon)
                    dir = Vector3.UnitY;
                else
                    dir /= dist;

                _joints[i] = _joints[i + 1] + dir * _segmentLengths[i];
                ApplyConstraint(i, i + 1);
            }

            _joints[0] = root;
            ApplyConstraint(0, 0);

            for (int i = 0; i < _segmentLengths.Length; i++)
            {
                Vector3 dir = _joints[i + 1] - _joints[i];
                float dist = dir.Length();
                if (dist <= Epsilon)
                    dir = Vector3.UnitY;
                else
                    dir /= dist;

                _joints[i + 1] = _joints[i] + dir * _segmentLengths[i];
                ApplyConstraint(i + 1, i);
            }

            if (Vector3.DistanceSquared(_joints[^1], target) <= toleranceSq)
                break;
        }

        ApplyTemporalSmoothing(root, deltaTime);
    }
    
    private void ApplyTemporalSmoothing(Vector3 root, float deltaTime)
    {
        if (Responsiveness <= 0f)
        {
            Array.Copy(_joints, _previousJoints, _joints.Length);
            EnforceLengths(root);
            return;
        }

        float blend = 1f - MathF.Exp(-Responsiveness * deltaTime);
        for (int i = 0; i < _joints.Length; i++)
        {
            Vector3 smoothed = Vector3.Lerp(_previousJoints[i], _joints[i], blend);
            _previousJoints[i] = smoothed;
            _joints[i] = smoothed;
        }

        EnforceLengths(root);
    }

    private void EnforceLengths(Vector3 root)
    {
        _joints[0] = root;
        _previousJoints[0] = root;
        ApplyConstraint(0, 0);

        for (int i = 0; i < _segmentLengths.Length; i++)
        {
            Vector3 dir = _joints[i + 1] - _joints[i];
            float dist = dir.Length();
            if (dist <= Epsilon)
                dir = Vector3.UnitY * _segmentLengths[i];
            else
                dir *= _segmentLengths[i] / dist;

            _joints[i + 1] = _joints[i] + dir;
            ApplyConstraint(i + 1, i);
        }
    }
}

/// <summary>
/// Represents a Verlet rope.
/// </summary>
public sealed class VerletRope
{
    private readonly Vector3[] _positions;
    private readonly Vector3[] _previousPositions;
    private readonly float[] _restLengths;
    private const float Epsilon = 1e-6f;

    public VerletRope(Vector3 start, Vector3 end, int segments)
    {
        if (segments < 2)
            throw new ArgumentOutOfRangeException(nameof(segments), "Rope needs at least two segments.");

        _positions = new Vector3[segments];
        _previousPositions = new Vector3[segments];
        _restLengths = new float[segments - 1];

        Vector3 delta = (end - start) / (segments - 1);
        for (int i = 0; i < segments; i++)
        {
            Vector3 p = start + delta * i;
            _positions[i] = p;
            _previousPositions[i] = p;
            if (i < segments - 1)
                _restLengths[i] = delta.Length();
        }
    }

    public VerletRope(IReadOnlyList<Vector3> controlPoints)
    {
        if (controlPoints == null)
            throw new ArgumentNullException(nameof(controlPoints));
        if (controlPoints.Count < 2)
            throw new ArgumentOutOfRangeException(nameof(controlPoints), "Rope needs at least two control points.");

        _positions = new Vector3[controlPoints.Count];
        _previousPositions = new Vector3[controlPoints.Count];
        _restLengths = new float[controlPoints.Count - 1];

        for (int i = 0; i < controlPoints.Count; i++)
        {
            _positions[i] = controlPoints[i];
            _previousPositions[i] = controlPoints[i];
            if (i < controlPoints.Count - 1)
            {
                float length = Vector3.Distance(controlPoints[i + 1], controlPoints[i]);
                _restLengths[i] = Math.Max(length, Epsilon);
            }
        }
    }

    public int NodeCount => _positions.Length;
    public ReadOnlySpan<Vector3> Positions => _positions;

    public void SetNode(int index, Vector3 position, bool overrideVelocity = true)
    {
        if (index < 0 || index >= _positions.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        _positions[index] = position;
        if (overrideVelocity)
            _previousPositions[index] = position;
    }

    /// <summary>
    /// Simulates the Verlet rope with the given parameters.
    /// </summary>
    /// <param name="deltaTime">The time step for the simulation.</param>
    /// <param name="acceleration">The acceleration to apply to each node.</param>
    /// <param name="constraintIterations">The number of iterations to perform per step.</param>
    /// <param name="pinnedStart">The position to pin the start of the rope, or null if not pinned.</param>
    /// <param name="pinnedEnd">The position to pin the end of the rope, or null if not pinned.</param>
    /// <param name="damping">The damping factor for velocity.</param>
    /// <param name="substeps">The number of substeps to take per frame.</param>
    /// <param name="maxVelocity">The maximum velocity allowed for each node.</param>
    /// <param name="stiffness">The stiffness factor for the constraints.</param>
    public void Simulate(float deltaTime, Vector3 acceleration, int constraintIterations = 4, Vector3? pinnedStart = null, Vector3? pinnedEnd = null, float damping = 0.98f, int substeps = 1, float maxVelocity = 20f, float stiffness = 0.85f)
    {
        if (deltaTime <= 0f)
            return;

        substeps = Math.Max(1, substeps);
        constraintIterations = Math.Max(1, constraintIterations);
        damping = MathHelper.Clamp(damping, 0f, 1f);
        maxVelocity = Math.Max(0f, maxVelocity);
        stiffness = MathHelper.Clamp(stiffness, 0f, 1f);

        float stepDt = deltaTime / substeps;
        float stepDtSq = stepDt * stepDt;
        float maxVelSq = maxVelocity * maxVelocity;

        for (int step = 0; step < substeps; step++)
        {
            if (pinnedStart.HasValue)
            {
                _positions[0] = pinnedStart.Value;
                _previousPositions[0] = pinnedStart.Value;
            }

            if (pinnedEnd.HasValue)
            {
                int last = _positions.Length - 1;
                _positions[last] = pinnedEnd.Value;
                _previousPositions[last] = pinnedEnd.Value;
            }

            for (int i = 0; i < _positions.Length; i++)
            {
                bool pinned = (pinnedStart.HasValue && i == 0) || (pinnedEnd.HasValue && i == _positions.Length - 1);
                if (pinned)
                    continue;

                Vector3 current = _positions[i];
                Vector3 velocity = (current - _previousPositions[i]) * damping;

                if (maxVelocity > 0f && velocity.LengthSquared() > maxVelSq)
                    velocity = Vector3.Normalize(velocity) * maxVelocity;

                _previousPositions[i] = current;
                _positions[i] = current + velocity + acceleration * stepDtSq;
            }

            float iterationStiffness = stiffness <= 0f
                ? 0f
                : 1f - MathF.Pow(1f - stiffness, 1f / constraintIterations);

            for (int iter = 0; iter < constraintIterations; iter++)
            {
                for (int i = 0; i < _restLengths.Length; i++)
                {
                    int a = i;
                    int b = i + 1;

                    Vector3 delta = _positions[b] - _positions[a];
                    float dist = delta.Length();
                    if (dist <= Epsilon)
                        continue;

                    float diff = (dist - _restLengths[i]) / dist;
                    Vector3 correction = delta * diff * iterationStiffness;

                    float invMassA = (pinnedStart.HasValue && a == 0) ? 0f : 1f;
                    float invMassB = (pinnedEnd.HasValue && b == _positions.Length - 1) ? 0f : 1f;
                    float invMassSum = invMassA + invMassB;

                    if (invMassSum <= Epsilon)
                        continue;

                    if (invMassA > 0f)
                        _positions[a] += correction * (invMassA / invMassSum);
                    if (invMassB > 0f)
                        _positions[b] -= correction * (invMassB / invMassSum);
                }

                if (pinnedStart.HasValue)
                    _positions[0] = pinnedStart.Value;
                if (pinnedEnd.HasValue)
                    _positions[^1] = pinnedEnd.Value;
            }
        }
    }

    /// <summary>
    /// Builds a triangle strip mesh along the rope.
    /// </summary>
    /// <param name="width">The width of the triangle strip.</param>
    /// <param name="color">The color of the triangle strip.</param>
    /// <param name="textured">Whether the triangle strip should be textured.</param>
    /// <param name="joinStyle">The style to use for joining segments.</param>
    /// <param name="startCap">The style to use for the start cap.</param>
    /// <param name="endCap">The style to use for the end cap.</param>
    /// <param name="capSegments">The number of segments to use for the caps.</param>
    public PrimitiveMesh BuildTriangleStrip(float width, Color color, bool textured = false, StripJoinStyle joinStyle = StripJoinStyle.Perpendicular, StripCapStyle startCap = StripCapStyle.None, StripCapStyle endCap = StripCapStyle.None, int capSegments = 8)
    {
        var path = new List<Vector3>(_positions.Length);
        for (int i = 0; i < _positions.Length; i++)
            path.Add(_positions[i]);

        return TriangleStripBuilder.BuildStrip(
            path,
            width,
            color,
            upHint: Vector3.UnitZ,
            smoothingSegments: 0,
            startCap: startCap,
            endCap: endCap,
            capSegments: capSegments,
            joinStyle: joinStyle,
            textured: textured);
    }
}

/// <summary>
/// Represents a Verlet cloth simulation. A verlet cloth is a grid of points connected by constraints that simulate cloth-like behavior. <para/>
/// The one dimensional equivalent would be <see cref="VerletRope"/>.
/// </summary>
public sealed class VerletCloth
{
    private readonly int _widthSegments;
    private readonly int _heightSegments;
    private readonly Vector3[] _positions;
    private readonly Vector3[] _previousPositions;
    private readonly bool[] _pinned;
    private readonly Vector3[] _pinTargets;
    private readonly float _restHorizontal;
    private readonly float _restVertical;
    private readonly float _restDiagonal;
    private const float Epsilon = 1e-6f;

    /// <summary>
    /// Initializes a new instance of the <see cref="VerletCloth"/> class.
    /// </summary>
    /// <param name="origin">The top-left corner of the cloth.</param>
    /// <param name="rightExtent">The length of the cloth to the right.</param>
    /// <param name="downExtent">The length of the cloth downwards.</param>
    /// <param name="widthSegments">The number of segments along the width.</param>
    /// <param name="heightSegments">The number of segments along the height.</param>
    public VerletCloth(Vector3 origin, Vector3 rightExtent, Vector3 downExtent, int widthSegments, int heightSegments)
    {
        if (widthSegments < 1)
            throw new ArgumentOutOfRangeException(nameof(widthSegments));
        if (heightSegments < 1)
            throw new ArgumentOutOfRangeException(nameof(heightSegments));

        _widthSegments = widthSegments;
        _heightSegments = heightSegments;

        int columns = widthSegments + 1;
        int rows = heightSegments + 1;
        int count = columns * rows;

        _positions = new Vector3[count];
        _previousPositions = new Vector3[count];
        _pinned = new bool[count];
        _pinTargets = new Vector3[count];

        _restHorizontal = rightExtent.Length() / widthSegments;
        _restVertical = downExtent.Length() / heightSegments;
        _restDiagonal = MathF.Sqrt(_restHorizontal * _restHorizontal + _restVertical * _restVertical);

        Vector3 rightStep = rightExtent / widthSegments;
        Vector3 downStep = downExtent / heightSegments;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                int idx = Index(x, y);
                Vector3 position = origin + rightStep * x + downStep * y;
                _positions[idx] = position;
                _previousPositions[idx] = position;
            }
        }
    }

    public void PinNode(int x, int y, Vector3 target)
    {
        int idx = Index(x, y);
        _pinned[idx] = true;
        _pinTargets[idx] = target;
        _positions[idx] = target;
        _previousPositions[idx] = target;
    }

    public void ReleaseNode(int x, int y)
    {
        int idx = Index(x, y);
        _pinned[idx] = false;
    }

    /// <summary>
    /// Simulates the cloth with the given parameters.
    /// </summary>
    /// <param name="deltaTime">The time step for the simulation.</param>
    /// <param name="acceleration">The acceleration to apply to each node.</param>
    /// <param name="constraintIterations">The number of iterations to perform per step.</param>
    /// <param name="substeps">The number of substeps to take per frame.</param>
    /// <param name="damping">The damping factor for velocity.</param>
    /// <param name="stiffness">The stiffness factor for the constraints.</param>
    /// <param name="maxVelocity">The maximum velocity allowed for each node.</param>
    public void Simulate(float deltaTime, Vector3 acceleration, int constraintIterations = 6, int substeps = 1, float damping = 0.99f, float stiffness = 0.85f, float maxVelocity = 25f)
    {
        if (deltaTime <= 0f)
            return;

        substeps = Math.Max(1, substeps);
        constraintIterations = Math.Max(1, constraintIterations);
        damping = MathHelper.Clamp(damping, 0f, 1f);
        stiffness = MathHelper.Clamp(stiffness, 0f, 1f);
        maxVelocity = Math.Max(0f, maxVelocity);

        float stepDt = deltaTime / substeps;
        float stepDtSq = stepDt * stepDt;
        float maxVelSq = maxVelocity * maxVelocity;

        for (int step = 0; step < substeps; step++)
        {
            EnforcePins();

            for (int i = 0; i < _positions.Length; i++)
            {
                if (_pinned[i])
                    continue;

                Vector3 current = _positions[i];
                Vector3 velocity = (current - _previousPositions[i]) * damping;
                if (maxVelocity > 0f && velocity.LengthSquared() > maxVelSq)
                    velocity = Vector3.Normalize(velocity) * maxVelocity;

                _previousPositions[i] = current;
                _positions[i] = current + velocity + acceleration * stepDtSq;
            }

            float iterationStiffness = stiffness <= 0f
                ? 0f
                : 1f - MathF.Pow(1f - stiffness, 1f / constraintIterations);

            for (int iter = 0; iter < constraintIterations; iter++)
            {
                SolveStructural(iterationStiffness, _restHorizontal, 1, 0);
                SolveStructural(iterationStiffness, _restVertical, 0, 1);
                SolveStructural(iterationStiffness, _restDiagonal, 1, 1);
                SolveStructural(iterationStiffness, _restDiagonal, -1, 1);
                EnforcePins();
            }
        }
    }

    /// <summary>
    /// Builds a mesh representing the cloth.
    /// </summary>
    /// <param name="color">The color to use for the mesh.</param>
    /// <param name="textured">Whether to include texture coordinates in the mesh.</param>
    /// <returns>A PrimitiveMesh representing the cloth.</returns>
    public PrimitiveMesh BuildMesh(Color color, bool textured = false)
    {
        int columns = _widthSegments + 1;
        int rows = _heightSegments + 1;

        if (textured)
        {
            var vertices = new VertexPositionColorTexture[_positions.Length];
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int idx = Index(x, y);
                    Vector2 uv = new Vector2(x / (float)_widthSegments, y / (float)_heightSegments);
                    vertices[idx] = new VertexPositionColorTexture(_positions[idx], color, uv);
                }
            }

            var indices = BuildGridIndices();
            return new PrimitiveMesh(vertices, indices, PrimitiveType.TriangleList);
        }
        else
        {
            var vertices = new VertexPositionColor[_positions.Length];
            for (int i = 0; i < _positions.Length; i++)
                vertices[i] = new VertexPositionColor(_positions[i], color);

            var indices = BuildGridIndices();
            return new PrimitiveMesh(vertices, indices, PrimitiveType.TriangleList);
        }
    }

    private int Index(int x, int y) => y * (_widthSegments + 1) + x;

    private void EnforcePins()
    {
        for (int i = 0; i < _positions.Length; i++)
        {
            if (!_pinned[i])
                continue;

            _positions[i] = _pinTargets[i];
            _previousPositions[i] = _pinTargets[i];
        }
    }

    private void SolveStructural(float stiffnessStep, float restLength, int dx, int dy)
    {
        int columns = _widthSegments + 1;
        int rows = _heightSegments + 1;

        for (int y = 0; y < rows; y++)
        {
            int ny = y + dy;
            if (ny < 0 || ny >= rows)
                continue;

            for (int x = 0; x < columns; x++)
            {
                int nx = x + dx;
                if (nx < 0 || nx >= columns)
                    continue;

                int a = Index(x, y);
                int b = Index(nx, ny);

                Vector3 delta = _positions[b] - _positions[a];
                float dist = delta.Length();
                if (dist <= Epsilon)
                    continue;

                float diff = (dist - restLength) / dist;
                Vector3 correction = delta * diff * stiffnessStep;

                if (_pinned[a] && _pinned[b])
                    continue;

                if (_pinned[a])
                {
                    _positions[b] -= correction;
                }
                else if (_pinned[b])
                {
                    _positions[a] += correction;
                }
                else
                {
                    Vector3 half = correction * 0.5f;
                    _positions[a] += half;
                    _positions[b] -= half;
                }
            }
        }
    }

    private short[] BuildGridIndices()
    {
        var indices = new short[_widthSegments * _heightSegments * 6];
        int cursor = 0;

        for (int y = 0; y < _heightSegments; y++)
        {
            for (int x = 0; x < _widthSegments; x++)
            {
                int topLeft = Index(x, y);
                int topRight = Index(x + 1, y);
                int bottomLeft = Index(x, y + 1);
                int bottomRight = Index(x + 1, y + 1);

                indices[cursor++] = (short)topLeft;
                indices[cursor++] = (short)bottomLeft;
                indices[cursor++] = (short)topRight;

                indices[cursor++] = (short)topRight;
                indices[cursor++] = (short)bottomLeft;
                indices[cursor++] = (short)bottomRight;
            }
        }

        return indices;
    }
}

public static class ConstraintDebugAttachments
{
	private static float ComputeRopeLength(VerletRope rope)
	{
		var positions = rope.Positions;
		if (positions.Length <= 1)
			return 0f;

		float total = 0f;
		for (int i = 1; i < positions.Length; i++)
			total += Vector3.Distance(positions[i - 1], positions[i]);
		return total;
	}

	public static ReactivePanel CreateRopeInspector(VerletRope rope)
	{
		var lengthValue = new ReactiveValue<float>(ComputeRopeLength(rope));
        var pos = new ReactiveValue<Vector3>(rope.Positions[..^1].ToArray()[0]);
        
		var panel = new ReactivePanel(new Vector2(16f, 16f));

		panel.AddUpdater(_ => lengthValue.Value = ComputeRopeLength(rope));
        panel.AddUpdater(_ => pos.Value = rope.Positions[..^1].ToArray()[0]);
        panel.AddUpdater(_ => panel.LocalPosition = new Vector2(pos.Value.X, pos.Value.Y));

		panel.Children.Add(new RLabel(
			lengthValue.Select(value => $"Rope length: {value:F2}"),
			() => FontAssets.MouseText.Value,
			Vector2.Zero,
			Color.White));

		return panel;
	}
}

