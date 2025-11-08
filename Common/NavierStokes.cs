using System;
using Cataphractal.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace Cataphract.Common;

public sealed class FluidField
{
	private int _n;
	private float _diffusion;
	private float _viscosity;
	private float _densityDecay;
	private float[] _u;
	private float[] _v;
	private float[] _u0;
	private float[] _v0;
	private float[] _dens;
	private float[] _dens0;
	private float _dt;
	private readonly bool[] _obstacles;

	public Vector2 Origin { get; }
	public float CellSize { get; }
	public int Resolution => _n;

	public FluidField(int gridSize, float diffusion, float viscosity, Vector2 origin, float cellSize, float densityDecay = 0.996f)
	{
		_n = Math.Max(4, gridSize);
		_diffusion = Math.Max(0f, diffusion);
		_viscosity = Math.Max(0f, viscosity);
		_densityDecay = MathHelper.Clamp(densityDecay, 0f, 1f);
		Origin = origin;
		CellSize = Math.Max(1e-3f, cellSize);

		int count = (_n + 2) * (_n + 2);
		_u = new float[count];
		_v = new float[count];
		_u0 = new float[count];
		_v0 = new float[count];
		_dens = new float[count];
		_dens0 = new float[count];
		_obstacles = new bool[count];
	}

	public void AddImpulse(Vector2 worldPos, Vector2 velocity, float density)
	{
		if (!TryCell(worldPos, out int i, out int j))
			return;

		int idx = Index(i, j);
		if (_obstacles[idx])
			return;

		_u[idx] += velocity.X;
		_v[idx] += velocity.Y;
		_dens[idx] += Math.Max(0f, density);
	}

	public void Step(float deltaTime)
	{
		_dt = Math.Clamp(deltaTime, Mathematical.Epsilon, 1f / 30f);

		Swap(ref _u0, ref _u);
		Diffuse(1, _u, _u0, _viscosity);
		ApplyObstacleConstraints(_u, _v, _dens);

		Swap(ref _v0, ref _v);
		Diffuse(2, _v, _v0, _viscosity);
		ApplyObstacleConstraints(_u, _v, _dens);

		Project(_u, _v, _u0, _v0);
		ApplyObstacleConstraints(_u, _v, _dens);

		Swap(ref _u0, ref _u);
		Swap(ref _v0, ref _v);
		Advect(1, _u, _u0, _u0, _v0);
		Advect(2, _v, _v0, _u0, _v0);
		ApplyObstacleConstraints(_u, _v, _dens);

		Project(_u, _v, _u0, _v0);
		ApplyObstacleConstraints(_u, _v, _dens);

		Swap(ref _dens0, ref _dens);
		Diffuse(0, _dens, _dens0, _diffusion);
		Advect(0, _dens, _dens0, _u, _v);
		ApplyObstacleConstraints(_u, _v, _dens);

		if (_densityDecay < 0.999f)
		{
			float decay = MathF.Pow(_densityDecay, Math.Max(1f, _dt * 60f));
			for (int i = 0; i < _dens.Length; i++)
				_dens[i] *= decay;
		}
	}

	public PrimitiveMesh BuildDensityMesh(Color lowColor, Color highColor)
	{
		int cells = _n;
		int quadCount = cells * cells;
		if (quadCount == 0)
			return default;

		var vertices = new VertexPositionColor[quadCount * 4];
		var indices = new short[quadCount * 6];
		int vi = 0;
		int ii = 0;

        Vector2 actualOrigin = Origin / 16;
        actualOrigin = new Vector2((float)Math.Floor(actualOrigin.X) * 16, (float)Math.Floor(actualOrigin.Y) * 16);
		for (int y = 0; y < cells; y++)
		{
			for (int x = 0; x < cells; x++)
			{
				int idx = Index(x + 1, y + 1);
				bool blocked = _obstacles[idx];
				float density = blocked ? 0f : MathHelper.Clamp(_dens[idx], 0f, 1.5f);
				float t = MathHelper.Clamp(density, 0f, 1f);
				Color color = blocked ? Color.Transparent : Color.Lerp(lowColor, highColor, t);

				Vector2 topLeft = actualOrigin + new Vector2(x * CellSize, y * CellSize);
				Vector2 topRight = topLeft + new Vector2(CellSize, 0f);
				Vector2 bottomRight = topLeft + new Vector2(CellSize, CellSize);
				Vector2 bottomLeft = topLeft + new Vector2(0f, CellSize);

				vertices[vi + 0] = new VertexPositionColor(new Vector3(topLeft, 0f), color);
				vertices[vi + 1] = new VertexPositionColor(new Vector3(topRight, 0f), color);
				vertices[vi + 2] = new VertexPositionColor(new Vector3(bottomRight, 0f), color);
				vertices[vi + 3] = new VertexPositionColor(new Vector3(bottomLeft, 0f), color);

				indices[ii + 0] = (short)(vi + 0);
				indices[ii + 1] = (short)(vi + 1);
				indices[ii + 2] = (short)(vi + 2);
				indices[ii + 3] = (short)(vi + 0);
				indices[ii + 4] = (short)(vi + 2);
				indices[ii + 5] = (short)(vi + 3);

				vi += 4;
				ii += 6;
			}
		}

		return new PrimitiveMesh(vertices, indices, PrimitiveType.TriangleList);
	}

	private void Diffuse(int b, float[] x, float[] x0, float diff)
	{
		float a = _dt * diff * _n * _n;
		float invC = 1f / (1f + 4f * a);

		for (int k = 0; k < 20; k++)
		{
			for (int j = 1; j <= _n; j++)
			{
				for (int i = 1; i <= _n; i++)
				{
					int idx = Index(i, j);
					if (_obstacles[idx])
					{
						x[idx] = 0f;
						continue;
					}

					float sum = SampleField(x, i - 1, j) + SampleField(x, i + 1, j) + SampleField(x, i, j - 1) + SampleField(x, i, j + 1);
					x[idx] = (x0[idx] + a * sum) * invC;
				}
			}
			SetBounds(b, x);
		}
	}

	private void Advect(int b, float[] d, float[] d0, float[] u, float[] v)
	{
		float dt0 = _dt * _n;

		for (int j = 1; j <= _n; j++)
		{
			for (int i = 1; i <= _n; i++)
			{
				int idx = Index(i, j);
				if (_obstacles[idx])
				{
					d[idx] = 0f;
					continue;
				}

				float x = i - dt0 * u[Index(i, j)];
				float y = j - dt0 * v[Index(i, j)];

				x = MathHelper.Clamp(x, 0.5f, _n + 0.5f);
				y = MathHelper.Clamp(y, 0.5f, _n + 0.5f);

				int i0 = (int)MathF.Floor(x);
				int i1 = i0 + 1;
				int j0 = (int)MathF.Floor(y);
				int j1 = j0 + 1;

				float s1 = x - i0;
				float s0 = 1f - s1;
				float t1 = y - j0;
				float t0 = 1f - t1;

				float sample =
					s0 * (t0 * SampleField(d0, i0, j0) + t1 * SampleField(d0, i0, j1)) +
					s1 * (t0 * SampleField(d0, i1, j0) + t1 * SampleField(d0, i1, j1));

				d[idx] = sample;
			}
		}
		SetBounds(b, d);
	}

	private void Project(float[] u, float[] v, float[] p, float[] div)
	{
		float h = 1f / _n;

		for (int j = 1; j <= _n; j++)
		{
			for (int i = 1; i <= _n; i++)
			{
				int idx = Index(i, j);
				if (_obstacles[idx])
				{
					div[idx] = 0f;
					p[idx] = 0f;
					continue;
				}

				div[idx] = -0.5f * h * (
					SampleField(u, i + 1, j) - SampleField(u, i - 1, j) +
					SampleField(v, i, j + 1) - SampleField(v, i, j - 1));
				p[idx] = 0f;
			}
		}
		SetBounds(0, div);
		SetBounds(0, p);
		LinearSolve(0, p, div, 1f, 4f);

		for (int j = 1; j <= _n; j++)
		{
			for (int i = 1; i <= _n; i++)
			{
				int idx = Index(i, j);
				if (_obstacles[idx])
					continue;

				u[idx] -= 0.5f * (SampleField(p, i + 1, j) - SampleField(p, i - 1, j)) / h;
				v[idx] -= 0.5f * (SampleField(p, i, j + 1) - SampleField(p, i, j - 1)) / h;
			}
		}
		SetBounds(1, u);
		SetBounds(2, v);
	}

	private void LinearSolve(int b, float[] x, float[] x0, float a, float c)
	{
		float invC = 1f / c;

		for (int k = 0; k < 20; k++)
		{
			for (int j = 1; j <= _n; j++)
			{
				for (int i = 1; i <= _n; i++)
				{
					int idx = Index(i, j);
					if (_obstacles[idx])
					{
						x[idx] = 0f;
						continue;
					}

					float sum = SampleField(x, i - 1, j) + SampleField(x, i + 1, j) + SampleField(x, i, j - 1) + SampleField(x, i, j + 1);
					x[idx] = (x0[idx] + a * sum) * invC;
				}
			}
			SetBounds(b, x);
		}
	}

	private void SetBounds(int b, float[] x)
	{
		for (int i = 1; i <= _n; i++)
		{
			x[Index(0, i)] = b == 1 ? -x[Index(1, i)] : x[Index(1, i)];
			x[Index(_n + 1, i)] = b == 1 ? -x[Index(_n, i)] : x[Index(_n, i)];
			x[Index(i, 0)] = b == 2 ? -x[Index(i, 1)] : x[Index(i, 1)];
			x[Index(i, _n + 1)] = b == 2 ? -x[Index(i, _n)] : x[Index(i, _n)];
		}

		x[Index(0, 0)] = 0.5f * (x[Index(1, 0)] + x[Index(0, 1)]);
		x[Index(0, _n + 1)] = 0.5f * (x[Index(1, _n + 1)] + x[Index(0, _n)]);
		x[Index(_n + 1, 0)] = 0.5f * (x[Index(_n, 0)] + x[Index(_n + 1, 1)]);
		x[Index(_n + 1, _n + 1)] = 0.5f * (x[Index(_n, _n + 1)] + x[Index(_n + 1, _n)]);

		ZeroObstacles(x);
	}

	private bool TryCell(Vector2 worldPos, out int i, out int j)
	{
		Vector2 local = (worldPos - Origin) / CellSize;
		float x = local.X + 1f;
		float y = local.Y + 1f;

		i = (int)MathF.Floor(x);
		j = (int)MathF.Floor(y);

		if (i < 1 || i > _n || j < 1 || j > _n)
			return false;

		i = Math.Clamp(i, 1, _n);
		j = Math.Clamp(j, 1, _n);
		return true;
	}

	private int Index(int i, int j) => i + (_n + 2) * j;

	private static void Swap(ref float[] a, ref float[] b) => (a, b) = (b, a);

	public void SyncTilesFromWorld(bool includePlatforms = false)
	{
		for (int j = 1; j <= _n; j++)
		{
			for (int i = 1; i <= _n; i++)
			{
				Vector2 center = Origin + new Vector2((i - 0.5f) * CellSize, (j - 0.5f) * CellSize) + Main.screenPosition - new Vector2(_n, _n);
				int tileX = (int)MathF.Floor(center.X / 16f);
				int tileY = (int)MathF.Floor(center.Y / 16f);

				bool solid = tileX < 0 || tileX >= Main.maxTilesX || tileY < 0 || tileY >= Main.maxTilesY;

				if (!solid)
				{
					Tile tile = Main.tile[tileX, tileY];
					if (tile != null && tile.HasTile && !tile.IsActuated)
					{
						int type = tile.TileType;
						bool isSolid = Main.tileSolid[type] && !Main.tileSolidTop[type];
						bool platform = includePlatforms && Main.tileSolidTop[type];
						solid = isSolid || platform ;
					}
				}

				_obstacles[Index(i, j)] = solid;
			}
		}

		ZeroObstacles(_u);
		ZeroObstacles(_v);
		ZeroObstacles(_dens);
		ZeroObstacles(_u0);
		ZeroObstacles(_v0);
		ZeroObstacles(_dens0);
		ApplyObstacleConstraints(_u, _v, _dens);
	}

	private float SampleField(float[] field, int i, int j)
	{
		int idx = Index(i, j);
		return _obstacles[idx] ? 0f : field[idx];
	}

	private void ZeroObstacles(float[] field)
	{
		for (int j = 1; j <= _n; j++)
			for (int i = 1; i <= _n; i++)
			{
				int idx = Index(i, j);
				if (_obstacles[idx])
					field[idx] = 0f;
			}
	}

	private void ApplyObstacleConstraints(float[] u, float[] v, float[] dens)
	{
		for (int j = 1; j <= _n; j++)
		{
			for (int i = 1; i <= _n; i++)
			{
				int idx = Index(i, j);
				if (!_obstacles[idx])
					continue;

				u[idx] = 0f;
				v[idx] = 0f;
				dens[idx] = 0f;

				if (!_obstacles[Index(i - 1, j)])
					u[Index(i - 1, j)] = MathF.Min(0f, u[Index(i - 1, j)]);
				if (!_obstacles[Index(i + 1, j)])
					u[Index(i + 1, j)] = MathF.Max(0f, u[Index(i + 1, j)]);
				if (!_obstacles[Index(i, j - 1)])
					v[Index(i, j - 1)] = MathF.Min(0f, v[Index(i, j - 1)]);
				if (!_obstacles[Index(i, j + 1)])
					v[Index(i, j + 1)] = MathF.Max(0f, v[Index(i, j + 1)]);
			}
		}
	}
}
