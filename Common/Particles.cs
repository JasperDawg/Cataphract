using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Utilities;
using static Cataphractal.Common.Mathematical;

namespace Cataphract.Common;

[Flags]
public enum ParticleComponentMask : uint
{
	None = 0,
	Position = 1 << 0,
	Velocity = 1 << 1,
	Acceleration = 1 << 2,
	Color = 1 << 3,
	Lifetime = 1 << 4,
	Scale = 1 << 5,
	Rotation = 1 << 6,
	AngularVelocity = 1 << 7,
	TextureRegion = 1 << 8,
	FrameAnimation = 1 << 9,
	Light = 1 << 10
}

public readonly struct ParticleComponentHandle<T>
{
	internal readonly int ComponentId;
	internal ParticleComponentHandle(int componentId) => ComponentId = componentId;
	public bool IsValid => ComponentId >= 0;
}

public readonly struct Particule
{
	public readonly int Id;
	public readonly int Generation;

	internal Particule(int id, int generation)
	{
		Id = id;
		Generation = generation;
	}

	public bool IsValid => Id >= 0;
}

public interface IParticleRenderAttachment
{
	void Draw(ParticleWorld world, SpriteBatch spriteBatch, Vector2 origin, float layerDepth);
}

public sealed class ParticleWorld
{
	private const int PositionComponentId = 0;
	private const int VelocityComponentId = 1;
	private const int AccelerationComponentId = 2;
	private const int ColorComponentId = 3;
	private const int LifetimeComponentId = 4;
	private const int ScaleComponentId = 5;
	private const int RotationComponentId = 6;
	private const int AngularVelocityComponentId = 7;
	private const int TextureRegionComponentId = 8;
	private const int FrameComponentId = 9;
	private const int LightComponentId = 10;
	private const int BuiltinComponentCount = LightComponentId + 1;

	private readonly int _capacity;
	private readonly Stack<int> _freeList;

	private readonly ParticleComponentMask[] _archetypes;
	private readonly int[] _generations;
	private readonly float[] _ages;
	private readonly float[] _lifetimes;

	private readonly Vector2[] _positions;
	private readonly Vector2[] _velocities;
	private readonly Vector2[] _accelerations;

	private readonly Color[] _colors;
	private readonly Vector2[] _scales;

	private readonly float[] _rotations;
	private readonly float[] _angularVelocities;

	private readonly Rectangle[] _textureRegions;
	private readonly float[] _frameTimers;
	private readonly Point[] _frameGrid;
	private readonly Vector2[] _frameSize;
	private readonly Vector2[] _frameStep;

	private readonly Vector3[] _lights;
	private readonly bool[] _alive;

	private readonly Queue<int> _pendingRemoval;

	private readonly ComponentMask[] _componentMasks;
	private int _maskWords;
	private int _nextComponentId = BuiltinComponentCount;
	private readonly List<IParticleComponentBuffer> _dynamicComponents = new();
	private readonly Dictionary<int, int> _dynamicComponentLookup = new();

	public Texture2D DefaultTexture { get; set; } = null!;
	public IParticleRenderAttachment? RenderAttachment { get; set; }
	public int Count { get; private set; }

	public ParticleWorld(int capacity = 8192)
	{
		_capacity = Math.Max(128, capacity);
		_freeList = new Stack<int>(_capacity);
		_archetypes = new ParticleComponentMask[_capacity];
		_generations = new int[_capacity];
		_pendingRemoval = new Queue<int>(256);

		_positions = new Vector2[_capacity];
		_velocities = new Vector2[_capacity];
		_accelerations = new Vector2[_capacity];

		_colors = new Color[_capacity];
		_scales = new Vector2[_capacity];

		_rotations = new float[_capacity];
		_angularVelocities = new float[_capacity];

		_ages = new float[_capacity];
		_lifetimes = new float[_capacity];

		_textureRegions = new Rectangle[_capacity];
		_frameTimers = new float[_capacity];
		_frameGrid = new Point[_capacity];
		_frameSize = new Vector2[_capacity];
		_frameStep = new Vector2[_capacity];

		_lights = new Vector3[_capacity];
		_alive = new bool[_capacity];
		_componentMasks = new ComponentMask[_capacity];
		RenderAttachment = null;
		EnsureMaskWords(BuiltinComponentCount);

		for (int i = _capacity - 1; i >= 0; i--)
			_freeList.Push(i);
	}

	private bool IsAlive(Particule entity) =>
		entity.Id >= 0 &&
		entity.Id < _capacity &&
		_alive[entity.Id] &&
		_generations[entity.Id] == entity.Generation;

	public Particule Alloc()
	{
		if (_freeList.Count == 0)
			return default;
		int id = _freeList.Pop();

		_generations[id]++;
		_archetypes[id] = ParticleComponentMask.None;
		_componentMasks[id].Clear();
		_alive[id] = true;
		Count++;

		return new Particule(id, _generations[id]);
	}

	public void Despawn(Particule entity)
	{
		if (!IsAlive(entity))
			return;
		_alive[entity.Id] = false;
		_archetypes[entity.Id] = ParticleComponentMask.None;
		ResetDynamicComponents(entity.Id);
		_componentMasks[entity.Id].Clear();
		_pendingRemoval.Enqueue(entity.Id);
		Count = Math.Max(0, Count - 1);
	}

	private ref ParticleComponentMask Signature(Particule entity) => ref _archetypes[entity.Id];

	public void SetPosition(Particule entity, Vector2 position)
	{
		if (!IsAlive(entity))
			return;

		_positions[entity.Id] = position;

		Signature(entity) |= ParticleComponentMask.Position;
		SetComponentBit(entity.Id, PositionComponentId, true);
	}

	public void SetVelocity(Particule entity, Vector2 velocity)
	{
		if (!IsAlive(entity))
			return;

		_velocities[entity.Id] = velocity;

		Signature(entity) |= ParticleComponentMask.Velocity;
		SetComponentBit(entity.Id, VelocityComponentId, true);
	}

	public Vector2 GetPosition(Particule entity)
	{
		if (!IsAlive(entity))
			throw new InvalidOperationException("Entity is not alive.");
		return _positions[entity.Id];
	}

	public Vector2 GetVelocity(Particule entity)
	{
		if (!IsAlive(entity))
			throw new InvalidOperationException("Entity is not alive.");
		return _velocities[entity.Id];
	}

	public bool TryGetColor(Particule entity, out Color color)
	{
		if (!IsAlive(entity) || !_archetypes[entity.Id].HasFlag(ParticleComponentMask.Color))
		{
			color = default;
			return false;
		}
		color = _colors[entity.Id];
		return true;
	}

	public Color GetColor(Particule entity)
	{
		if (!TryGetColor(entity, out var color))
			throw new InvalidOperationException("Color component not present on entity.");
		return color;
	}

	public bool TryGetScale(Particule entity, out Vector2 scale)
	{
		if (!IsAlive(entity) || !_archetypes[entity.Id].HasFlag(ParticleComponentMask.Scale))
		{
			scale = default;
			return false;
		}
		scale = _scales[entity.Id];
		return true;
	}

	public Vector2 GetScale(Particule entity)
	{
		if (!TryGetScale(entity, out var scale))
			throw new InvalidOperationException("Scale component not present on entity.");

		return scale;
	}

	public void SetAcceleration(Particule entity, Vector2 acceleration)
	{
		if (!IsAlive(entity))
			return;
            
		_accelerations[entity.Id] = acceleration;

		Signature(entity) |= ParticleComponentMask.Acceleration;
		SetComponentBit(entity.Id, AccelerationComponentId, true);
	}

	public void SetColor(Particule entity, Color color)
	{
		if (!IsAlive(entity))
			return;

		_colors[entity.Id] = color;

		Signature(entity) |= ParticleComponentMask.Color;
		SetComponentBit(entity.Id, ColorComponentId, true);
	}

	public void SetLifetime(Particule entity, float lifetimeSeconds)
	{
		if (!IsAlive(entity))
			return;

		_lifetimes[entity.Id] = Math.Max(Epsilon, lifetimeSeconds);
		_ages[entity.Id] = 0f;

		Signature(entity) |= ParticleComponentMask.Lifetime;
		SetComponentBit(entity.Id, LifetimeComponentId, true);
	}

	public void SetScale(Particule entity, Vector2 scale)
	{
		if (!IsAlive(entity))
			return;

		_scales[entity.Id] = scale;

		Signature(entity) |= ParticleComponentMask.Scale;
		SetComponentBit(entity.Id, ScaleComponentId, true);
	}

	public void SetRotation(Particule entity, float radians, float angularVelocity = 0f)
	{
		if (!IsAlive(entity))
			return;

		_rotations[entity.Id] = radians;
		_angularVelocities[entity.Id] = angularVelocity;

		Signature(entity) |= ParticleComponentMask.Rotation;
		SetComponentBit(entity.Id, RotationComponentId, true);

		if (Math.Abs(angularVelocity) > Epsilon)
		{
			Signature(entity) |= ParticleComponentMask.AngularVelocity;
			SetComponentBit(entity.Id, AngularVelocityComponentId, true);
		}
	}

	public void SetTextureRegion(Particule entity, Rectangle region)
	{
		if (!IsAlive(entity))
			return;
            
		_textureRegions[entity.Id] = region;

		Signature(entity) |= ParticleComponentMask.TextureRegion;
		SetComponentBit(entity.Id, TextureRegionComponentId, true);
	}

	public void SetAnimation(Particule entity, Point tiles, Vector2 frameSizePixels, float fps, bool loop = true)
	{
		if (!IsAlive(entity) || tiles.X <= 0 || tiles.Y <= 0 || fps <= 0f)
			return;
		int id = entity.Id;

		_frameGrid[id] = new Point(Math.Max(1, tiles.X), Math.Max(1, tiles.Y));
		_frameSize[id] = frameSizePixels;
		_frameStep[id] = new Vector2(loop ? fps : -Math.Abs(fps), loop ? 1f : 0f);
		_frameTimers[id] = 0f;

		Signature(entity) |= ParticleComponentMask.FrameAnimation | ParticleComponentMask.TextureRegion;
		SetComponentBit(entity.Id, FrameComponentId, true);
		SetComponentBit(entity.Id, TextureRegionComponentId, true);

		_textureRegions[id] = new Rectangle(0, 0, (int)frameSizePixels.X, (int)frameSizePixels.Y);
	}

	public void SetLight(Particule entity, Vector3 color)
	{
		if (!IsAlive(entity))
			return;
		_lights[entity.Id] = color;

		Signature(entity) |= ParticleComponentMask.Light;

		SetComponentBit(entity.Id, LightComponentId, true);
	}

	public void ClearDeferred()
	{
		while (_pendingRemoval.Count > 0) // std::removeif would be great so this could be an iter :p
		{
			int id = _pendingRemoval.Dequeue();
			if (_alive[id])
				continue;

			_freeList.Push(id);
		}
	}

	public void Update(float deltaSeconds, Vector2 globalAcceleration, Func<Vector2, Vector3, bool>? lightSink = null)
	{
		if (deltaSeconds <= 0f)
			return;

		Vector2 accelerationDt = globalAcceleration * deltaSeconds;

		for (int i = 0; i < _capacity; i++)
		{
			if (!_alive[i])
				continue;
			var signature = _archetypes[i];
			if (signature == ParticleComponentMask.None)
				continue;

			if (signature.HasFlag(ParticleComponentMask.Lifetime))
			{
				_ages[i] += deltaSeconds;
				if (_ages[i] >= _lifetimes[i])
				{
					_alive[i] = false;
					_archetypes[i] = ParticleComponentMask.None;
					ResetDynamicComponents(i);
					_componentMasks[i].Clear();
					_pendingRemoval.Enqueue(i);
					Count = Math.Max(0, Count - 1);
					continue;
				}
			}

			if (signature.HasFlag(ParticleComponentMask.Acceleration))
				_velocities[i] += _accelerations[i] * deltaSeconds;

			if (signature.HasFlag(ParticleComponentMask.Velocity))
				_positions[i] += _velocities[i] * deltaSeconds + accelerationDt * 0.5f;

			if (signature.HasFlag(ParticleComponentMask.AngularVelocity))
				_rotations[i] += _angularVelocities[i] * deltaSeconds;

			if (signature.HasFlag(ParticleComponentMask.FrameAnimation))
				UpdateAnimation(i, deltaSeconds);

			if (lightSink != null && signature.HasFlag(ParticleComponentMask.Light))
				lightSink(_positions[i], _lights[i]);
		}

		ClearDeferred();
	}

	private void UpdateAnimation(int index, float deltaSeconds)
	{
		_frameTimers[index] += deltaSeconds;
		float fps = Math.Abs(_frameStep[index].X);
		float totalFrames = _frameGrid[index].X * _frameGrid[index].Y;
		float frameIndex = _frameTimers[index] * fps;
		bool loop = _frameStep[index].X >= 0f;

		if (!loop && frameIndex >= totalFrames)
		{
			frameIndex = totalFrames - 1f;
			_frameStep[index].X = 0f;
		}
		else if (loop)
		{
			frameIndex %= totalFrames;
		}

		int frame = (int)frameIndex;
		int frameX = frame % _frameGrid[index].X;
		int frameY = frame / _frameGrid[index].X;

		var size = _frameSize[index];
		_textureRegions[index] = new Rectangle(
			(int)(frameX * size.X),
			(int)(frameY * size.Y),
			(int)size.X,
			(int)size.Y);
	}

	public void Draw(SpriteBatch spriteBatch, Vector2 origin, float layerDepth = 0f)
	{
		if (spriteBatch == null)
			throw new ArgumentNullException(nameof(spriteBatch));
		if (RenderAttachment != null)
		{
			RenderAttachment.Draw(this, spriteBatch, origin, layerDepth);
			return;
		}
		DrawSprites(spriteBatch, origin, layerDepth);
	}

	internal void DrawSprites(SpriteBatch spriteBatch, Vector2 origin, float layerDepth)
	{
		if (DefaultTexture == null)
			return;

		for (int i = 0; i < _capacity; i++)
		{
			var signature = _archetypes[i];
			if (signature == ParticleComponentMask.None || !signature.HasFlag(ParticleComponentMask.Position))
				continue;

			var position = _positions[i];
			var color = signature.HasFlag(ParticleComponentMask.Color) ? _colors[i] : Color.White;
			var scale = signature.HasFlag(ParticleComponentMask.Scale) ? _scales[i] : Vector2.One;
			float rotation = signature.HasFlag(ParticleComponentMask.Rotation) ? _rotations[i] : 0f;

			var sourceRect = signature.HasFlag(ParticleComponentMask.TextureRegion)
				? _textureRegions[i]
				: DefaultTexture.Bounds;

			var size = new Vector2(sourceRect.Width, sourceRect.Height);

			spriteBatch.Draw(
				DefaultTexture,
				position,
				sourceRect,
				color,
				rotation,
				size * 0.5f + origin,
				scale,
				SpriteEffects.None,
				layerDepth);
		}
	}

	public Particule Emit(Action<Particule, ParticleWorld> configure)
	{
		var entity = Alloc();

		if (!entity.IsValid)
			return entity;

		configure(entity, this);
		return entity;
	}

	public ParticleComponentHandle<T> RegisterComponent<T>(T defaultValue = default)
	{
		int componentId = _nextComponentId++;
		EnsureMaskWords(_nextComponentId);

		var buffer = new ParticleComponentBuffer<T>(_capacity, defaultValue);
		int bufferIndex = _dynamicComponents.Count;
		_dynamicComponents.Add(buffer);
		_dynamicComponentLookup[componentId] = bufferIndex;

		return new ParticleComponentHandle<T>(componentId);
	}

	public ref T AddComponent<T>(Particule entity, ParticleComponentHandle<T> handle)
	{
		if (!IsAlive(entity))
			return ref Unsafe.NullRef<T>();

		var buffer = GetBuffer(handle);
		SetComponentBit(entity.Id, handle.ComponentId, true);
		return ref buffer[entity.Id];
	}

	public bool HasComponent<T>(Particule entity, ParticleComponentHandle<T> handle)
	{
		if (!IsAlive(entity))
			return false;
		return HasComponentBit(entity.Id, handle.ComponentId);
	}

	public ref T GetComponent<T>(Particule entity, ParticleComponentHandle<T> handle)
	{
		if (!HasComponent(entity, handle))
			throw new InvalidOperationException("Component not present on entity; expected " + typeof(T).FullName + ".");
		return ref GetBuffer(handle)[entity.Id];
	}

	public void RemoveComponent<T>(Particule entity, ParticleComponentHandle<T> handle)
	{
		if (!HasComponent(entity, handle))
			return;
		var buffer = GetBuffer(handle);
		buffer.Clear(entity.Id);
		SetComponentBit(entity.Id, handle.ComponentId, false);
	}

	public void ForEach(Action<Particule> action)
	{
		if (action == null)
			throw new ArgumentNullException(nameof(action));
		for (int i = 0; i < _capacity; i++)
		{
			if (!_alive[i])
				continue;
			if (_archetypes[i] == ParticleComponentMask.None)
				continue;
			action(new Particule(i, _generations[i]));
		}
	}

	public PrimitiveMesh BuildBillboards(float depth = 0f, bool textured = true)
	{
		if (Count == 0)
			return default;

		var active = new List<int>(Count);
		for (int i = 0; i < _capacity; i++)
		{
			if (!_alive[i])
				continue;
			if (_archetypes[i] != ParticleComponentMask.None && _componentMasks[i].IsSet(PositionComponentId))
				active.Add(i);
		}

		if (active.Count == 0)
			return default;

		if (textured)
		{
			var vertices = new List<VertexPositionColorTexture>(active.Count * 4);
			var indices = new List<short>(active.Count * 6);

			foreach (int id in active)
			{
				if (vertices.Count + 4 > short.MaxValue)
					break;

				var signature = _archetypes[id];
				var position = _positions[id];
				var color = signature.HasFlag(ParticleComponentMask.Color) ? _colors[id] : Color.White;
				var scale = signature.HasFlag(ParticleComponentMask.Scale) ? _scales[id] : Vector2.One;
				var region = signature.HasFlag(ParticleComponentMask.TextureRegion)
					? _textureRegions[id]
					: DefaultTexture?.Bounds ?? new Rectangle(0, 0, 1, 1);

				float rotation = signature.HasFlag(ParticleComponentMask.Rotation) ? _rotations[id] : 0f;
				Vector2 half = new(region.Width * scale.X * 0.5f, region.Height * scale.Y * 0.5f);
				float cos = MathF.Cos(rotation);
				float sin = MathF.Sin(rotation);

				Vector2 Corner(Vector2 corner)
				{
					Vector2 rotated = new(corner.X * cos - corner.Y * sin, corner.X * sin + corner.Y * cos);
					return position + rotated;
				}

				float texWidth = DefaultTexture?.Width > 0 ? DefaultTexture.Width : Math.Max(region.Width, 1);
				float texHeight = DefaultTexture?.Height > 0 ? DefaultTexture.Height : Math.Max(region.Height, 1);
				float invWidth = 1f / texWidth;
				float invHeight = 1f / texHeight;
				bool applyPadding = DefaultTexture != null && (region.Width < DefaultTexture.Width || region.Height < DefaultTexture.Height);
				float paddingX = applyPadding ? 0.5f * invWidth : 0f;
				float paddingY = applyPadding ? 0.5f * invHeight : 0f;

				float uMin = region.Left * invWidth + paddingX;
				float uMax = region.Right * invWidth - paddingX;
				float vMin = region.Top * invHeight + paddingY;
				float vMax = region.Bottom * invHeight - paddingY;

				uMin = MathHelper.Clamp(uMin, 0f, 1f);
				uMax = MathHelper.Clamp(uMax, 0f, 1f);
				vMin = MathHelper.Clamp(vMin, 0f, 1f);
				vMax = MathHelper.Clamp(vMax, 0f, 1f);

				if (uMax <= uMin)
					uMax = Math.Min(uMin + invWidth, 1f);
				if (vMax <= vMin)
					vMax = Math.Min(vMin + invHeight, 1f);

				int vertBase = vertices.Count;
				Vector2 bl = Corner(new Vector2(-half.X, half.Y));
				Vector2 br = Corner(new Vector2(half.X, half.Y));
				Vector2 tr = Corner(new Vector2(half.X, -half.Y));
				Vector2 tl = Corner(new Vector2(-half.X, -half.Y));

				Vector2 uvBL = new(uMin, vMax);
				Vector2 uvBR = new(uMax, vMax);
				Vector2 uvTR = new(uMax, vMin);
				Vector2 uvTL = new(uMin, vMin);

				vertices.Add(new VertexPositionColorTexture(new Vector3(bl, depth), color, uvBL));
				vertices.Add(new VertexPositionColorTexture(new Vector3(br, depth), color, uvBR));
				vertices.Add(new VertexPositionColorTexture(new Vector3(tr, depth), color, uvTR));
				vertices.Add(new VertexPositionColorTexture(new Vector3(tl, depth), color, uvTL));

				indices.Add((short)vertBase);
				indices.Add((short)(vertBase + 1));
				indices.Add((short)(vertBase + 2));
				indices.Add((short)vertBase);
				indices.Add((short)(vertBase + 2));
				indices.Add((short)(vertBase + 3));
			}

			if (vertices.Count == 0)
				return default;

			return new PrimitiveMesh(vertices.ToArray(), indices.ToArray(), PrimitiveType.TriangleList);
		}
		else
		{
			var vertices = new List<VertexPositionColor>(active.Count * 4);
			var indices = new List<short>(active.Count * 6);

			foreach (int id in active)
			{
				if (vertices.Count + 4 > short.MaxValue)
					break;

				var signature = _archetypes[id];
				var position = _positions[id];
				var color = signature.HasFlag(ParticleComponentMask.Color) ? _colors[id] : Color.White;
				var scale = signature.HasFlag(ParticleComponentMask.Scale) ? _scales[id] : Vector2.One;
				float rotation = signature.HasFlag(ParticleComponentMask.Rotation) ? _rotations[id] : 0f;
				Vector2 half = new(scale.X * 0.5f, scale.Y * 0.5f);
				float cos = MathF.Cos(rotation);
				float sin = MathF.Sin(rotation);

				Vector2 Corner(Vector2 corner)
				{
					Vector2 rotated = new(corner.X * cos - corner.Y * sin, corner.X * sin + corner.Y * cos);
					return position + rotated;
				}

				int vertBase = vertices.Count;
				Vector2 bl = Corner(new Vector2(-half.X, half.Y));
				Vector2 br = Corner(new Vector2(half.X, half.Y));
				Vector2 tr = Corner(new Vector2(half.X, -half.Y));
				Vector2 tl = Corner(new Vector2(-half.X, -half.Y));

				vertices.Add(new VertexPositionColor(new Vector3(bl, depth), color));
				vertices.Add(new VertexPositionColor(new Vector3(br, depth), color));
				vertices.Add(new VertexPositionColor(new Vector3(tr, depth), color));
				vertices.Add(new VertexPositionColor(new Vector3(tl, depth), color));

				indices.Add((short)vertBase);
				indices.Add((short)(vertBase + 1));
				indices.Add((short)(vertBase + 2));
				indices.Add((short)vertBase);
				indices.Add((short)(vertBase + 2));
				indices.Add((short)(vertBase + 3));
			}

			if (vertices.Count == 0)
				return default;

			return new PrimitiveMesh(vertices.ToArray(), indices.ToArray(), PrimitiveType.TriangleList);
		}
	}

	private void ResetDynamicComponents(int id)
	{
		for (int i = 0; i < _dynamicComponents.Count; i++)
			_dynamicComponents[i].Clear(id);
	}

	private void EnsureMaskWords(int componentCount)
	{
		int requiredWords = (componentCount + 31) / 32;
		if (requiredWords <= _maskWords)
			return;

		for (int i = 0; i < _componentMasks.Length; i++)
			_componentMasks[i].EnsureWords(requiredWords);

		_maskWords = requiredWords;
	}

	private void SetComponentBit(int entityId, int componentId, bool value)
	{
		int word = componentId >> 5;
		int bit = componentId & 31;
		uint mask = 1u << bit;
		if (value)
			_componentMasks[entityId].Bits[word] |= mask;
		else
			_componentMasks[entityId].Bits[word] &= ~mask;
	}

	private bool HasComponentBit(int entityId, int componentId)
	{
		int word = componentId >> 5;
		int bit = componentId & 31;
		return (_componentMasks[entityId].Bits[word] & (1u << bit)) != 0;
	}

	private ParticleComponentBuffer<T> GetBuffer<T>(ParticleComponentHandle<T> handle)
	{
		if (!_dynamicComponentLookup.TryGetValue(handle.ComponentId, out int bufferIndex))
			throw new InvalidOperationException("Component handle not registered.");
		if (_dynamicComponents[bufferIndex] is ParticleComponentBuffer<T> typed)
			return typed;
		throw new InvalidOperationException("Incorrect type for component handle; expected " + typeof(T).FullName + ".");
	}

	private struct ComponentMask
	{
		public uint[] Bits;
		public void EnsureWords(int words)
		{
			if (Bits == null || Bits.Length < words)
				Array.Resize(ref Bits, words);
		}
		public void Clear()
		{
			if (Bits != null)
				Array.Clear(Bits, 0, Bits.Length);
		}
		public bool IsSet(int componentId)
		{
			int word = componentId >> 5;
			int bit = componentId & 31;
			return Bits != null && (Bits[word] & (1u << bit)) != 0;
		}
	}

	private interface IParticleComponentBuffer
	{
		void EnsureCapacity(int capacity);
		void Clear(int id);
	}

	private sealed class ParticleComponentBuffer<T> : IParticleComponentBuffer
	{
		private T[] _data;
		private readonly T _default;

		public ParticleComponentBuffer(int capacity, T defaultValue)
		{
			_data = new T[Math.Max(1, capacity)];
			_default = defaultValue;

			if (!EqualityComparer<T>.Default.Equals(defaultValue, default))
				for (int i = 0; i < _data.Length; i++)
					_data[i] = defaultValue;
		}

		public ref T this[int index] => ref _data[index];

		public void EnsureCapacity(int capacity)
		{
			if (_data.Length >= capacity)
				return;
			int oldLength = _data.Length;
			Array.Resize(ref _data, capacity);
			for (int i = oldLength; i < _data.Length; i++)
				_data[i] = _default;
		}

		public void Clear(int id) => _data[id] = _default;
	}
}

public static class ParticleEmitters
{
	private static UnifiedRandom _r => Main.rand;

	public static void EmitSparks(ParticleWorld world, Vector2 origin, int count = 32)
	{
		for (int i = 0; i < count; i++)
		{
			world.Emit((entity, w) =>
			{
				float angle = MathHelper.TwoPi * (float)_r.NextFloat();
				float speed = 180f + 240f * (float)_r.NextFloat();
				Vector2 velocity = new(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed - 60f);

				w.SetPosition(entity, origin);
				w.SetVelocity(entity, velocity);
				w.SetAcceleration(entity, new Vector2(0f, 180f));
				w.SetLifetime(entity, 0.4f + 0.35f * (float)_r.NextFloat());
				w.SetScale(entity, Vector2.One * (0.4f + 0.35f * (float)_r.NextFloat()));
				w.SetColor(entity, Color.Lerp(Color.Gold, Color.OrangeRed, (float)_r.NextFloat()));
				w.SetRotation(entity, angle, angularVelocity: MathHelper.Lerp(-6f, 6f, (float)_r.NextFloat()));
				w.SetTextureRegion(entity, new Rectangle(0, 0, 8, 8));
				w.SetLight(entity, new Vector3(1.2f, 0.8f, 0.3f));
			});
		}
	}

	public static void EmitSmoke(ParticleWorld world, Vector2 origin, int count = 18)
	{
		for (int i = 0; i < count; i++)
		{
			world.Emit((entity, w) =>
			{
				Vector2 velocity = new(
					MathHelper.Lerp(-30f, 30f, _r.NextFloat()),
					MathHelper.Lerp(-20f, -60f, _r.NextFloat()));

				w.SetPosition(entity, origin + new Vector2(MathHelper.Lerp(-6f, 6f, _r.NextFloat()), 0f));
				w.SetVelocity(entity, velocity);
				w.SetAcceleration(entity, new Vector2(0f, -20f));
				w.SetLifetime(entity, 0.8f + 0.6f * _r.NextFloat());
				w.SetScale(entity, Vector2.One * (0.6f + 0.45f * _r.NextFloat()));
				w.SetColor(entity, new Color(120, 120, 120, 200));
				w.SetRotation(entity, 0f, angularVelocity: MathHelper.Lerp(-1.5f, 1.5f, _r.NextFloat()));
				w.SetTextureRegion(entity, new Rectangle(16, 0, 32, 32));
			});
		}
	}
}

public sealed class ParticleSystemRunner
{
	private readonly ParticleWorld _world;
	private readonly Texture2D _atlas;
	private readonly Vector2 _origin;

	public ParticleSystemRunner(Texture2D atlas, int capacity = 8192)
	{
		_world = new ParticleWorld(capacity);
		_atlas = atlas ?? throw new ArgumentNullException(nameof(atlas));
		_world.DefaultTexture = _atlas;
		_origin = new Vector2(_atlas.Width * 0.5f, _atlas.Height * 0.5f);
	}

	public void Update(GameTime gameTime)
	{
		float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
		_world.Update(dt, new Vector2(0f, 120f));
	}

	public void Draw(SpriteBatch spriteBatch)
	{
		if (spriteBatch == null)
			throw new ArgumentNullException(nameof(spriteBatch));
		_world.Draw(spriteBatch, -_origin);
	}

	public void Demo(Vector2 position)
	{
		ParticleEmitters.EmitSparks(_world, position);
		ParticleEmitters.EmitSmoke(_world, position + Main.MouseScreen);
	}
}

public sealed class SpriteParticleRenderAttachment : IParticleRenderAttachment
{
	public static readonly SpriteParticleRenderAttachment Instance = new();

	private SpriteParticleRenderAttachment() { }

	public void Draw(ParticleWorld world, SpriteBatch spriteBatch, Vector2 origin, float layerDepth) =>
		world.DrawSprites(spriteBatch, origin, layerDepth);
}
