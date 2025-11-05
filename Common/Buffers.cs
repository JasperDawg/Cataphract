using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace Cataphract.Common;

public readonly struct RenderTargetDescriptor
{
    public static RenderTargetDescriptor Default { get; } = new(SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.DiscardContents, false);

    public RenderTargetDescriptor(SurfaceFormat format, DepthFormat depth, int multiSampleCount, RenderTargetUsage usage, bool generateMipmaps)
    {
        Format = format;
        Depth = depth;
        MultiSampleCount = Math.Max(0, multiSampleCount);
        Usage = usage;
        GenerateMipmaps = generateMipmaps;
    }

    public SurfaceFormat Format { get; }
    public DepthFormat Depth { get; }
    public int MultiSampleCount { get; }
    public RenderTargetUsage Usage { get; }
    public bool GenerateMipmaps { get; }

    public RenderTarget2D Create(GraphicsDevice device, int width, int height) =>
        new(device, width, height, GenerateMipmaps, Format, Depth, MultiSampleCount, Usage);
}

public sealed class RenderTargetPool : IDisposable
{
    private record struct Key(int Width, int Height, SurfaceFormat Format, DepthFormat Depth, int Samples, RenderTargetUsage Usage, bool Mip)
    {
        public Key(int width, int height, RenderTargetDescriptor descriptor) : this(width, height, descriptor.Format, descriptor.Depth, descriptor.MultiSampleCount, descriptor.Usage, descriptor.GenerateMipmaps)
        {
        }

        public Key(RenderTarget2D target) : this(target.Width, target.Height, new RenderTargetDescriptor(
            target.Format,
            target.DepthStencilFormat,
            target.MultiSampleCount,
            target.RenderTargetUsage,
            target.LevelCount > 1))
        {
        }
    }

    private readonly Dictionary<Key, Stack<RenderTarget2D>> _cache = new();
    private bool _disposed;

    public RenderTargetLease Rent(GraphicsDevice device, int width, int height, RenderTargetDescriptor descriptor)
    {
        if (device == null)
            throw new ArgumentNullException(nameof(device));
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));

        ThrowIfDisposed();

        var key = new Key(width, height, descriptor);
        if (!_cache.TryGetValue(key, out var stack))
        {
            stack = new Stack<RenderTarget2D>();
            _cache[key] = stack;
        }

        RenderTarget2D target = stack.Count > 0
            ? stack.Pop()
            : descriptor.Create(device, width, height);

        return new RenderTargetLease(this, target);
    }

    public RenderTargetLease RentScaled(GraphicsDevice device, Point baseSize, float scale, RenderTargetDescriptor descriptor)
    {
        if (scale <= 0f)
            throw new ArgumentOutOfRangeException(nameof(scale));

        int width = Math.Max(1, (int)MathF.Ceiling(baseSize.X * scale));
        int height = Math.Max(1, (int)MathF.Ceiling(baseSize.Y * scale));
        return Rent(device, width, height, descriptor);
    }

    private void Return(RenderTarget2D target)
    {
        if (target == null || target.IsDisposed)
            return;

        var key = new Key(target);
        if (!_cache.TryGetValue(key, out var stack))
        {
            stack = new Stack<RenderTarget2D>();
            _cache[key] = stack;
        }

        stack.Push(target);
    }

    public void Trim()
    {
        foreach (var stack in _cache.Values)
            while (stack.Count > 0)
                stack.Pop().Dispose();
        _cache.Clear();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RenderTargetPool));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        Trim();
        _disposed = true;
    }

    public readonly struct RenderTargetLease : IDisposable
    {
        private readonly RenderTargetPool _pool;
        public RenderTarget2D Target { get; }
        private readonly bool _isValid;

        internal RenderTargetLease(RenderTargetPool pool, RenderTarget2D target)
        {
            _pool = pool;
            Target = target;
            _isValid = true;
        }

        public void Dispose()
        {
            if (!_isValid)
                return;
            _pool.Return(Target);
        }
    }
}

public readonly struct RenderTargetScope : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly RenderTargetBinding[] _previous;
    private readonly bool _hasPrevious;

    public RenderTargetScope(GraphicsDevice device, RenderTarget2D target, bool clear = false, Color? clearColor = null)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _previous = device.GetRenderTargets();
        _hasPrevious = _previous.Length > 0;

        device.SetRenderTarget(target);
        if (clear)
            device.Clear(clearColor ?? Color.Transparent);
    }

    public void Dispose()
    {
        if (_hasPrevious)
            _device.SetRenderTargets(_previous);
        else
            _device.SetRenderTarget(null);
    }
}

public static class OutlineRenderer
{
    private static readonly Point[] EightDirections =
    {
        new(-1, -1),
        new(0, -1),
        new(1, -1),
        new(-1, 0),
        new(1, 0),
        new(-1, 1),
        new(0, 1),
        new(1, 1)
    };

    private static readonly Point[] FourDirections =
    {
        new(0, -1),
        new(-1, 0),
        new(1, 0),
        new(0, 1)
    };

    public static void DrawOutlined(
        GraphicsDevice device,
        RenderTargetPool pool,
        Point outputSize,
        Vector2 destination,
        Color outlineColor,
        Action<SpriteBatch> drawContent,
        int thickness = 2,
        float contentScale = 1f,
        BlendState? blendState = null,
        Color? fillTint = null,
        bool useFourDirections = true)
    {
        if (device == null)
            throw new ArgumentNullException(nameof(device));
        if (pool == null)
            throw new ArgumentNullException(nameof(pool));
        if (drawContent == null)
            throw new ArgumentNullException(nameof(drawContent));
        if (thickness < 1)
            thickness = 1;
        var passBatch = Main.spriteBatch;
        using var contentLease = pool.RentScaled(device, outputSize, contentScale, RenderTargetDescriptor.Default);
        using var outlineLease = pool.Rent(device, contentLease.Target.Width, contentLease.Target.Height, RenderTargetDescriptor.Default);

        using (var scope = new RenderTargetScope(device, contentLease.Target, true, Color.Transparent))
        {
            passBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            drawContent(passBatch);
            passBatch.End();
        }

        var directions = useFourDirections ? FourDirections : EightDirections;

        using (var scope = new RenderTargetScope(device, outlineLease.Target, true, Color.Transparent))
        {
            passBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            for (int r = 1; r <= thickness; r++)
            {
                foreach (var dir in directions)
                {
                    Vector2 offset = new(dir.X * r, dir.Y * r);
                    passBatch.Draw(contentLease.Target, offset, outlineColor);
                }
            }

            passBatch.End();
        }

        float scaleX = outputSize.X / (float)contentLease.Target.Width;
        float scaleY = outputSize.Y / (float)contentLease.Target.Height;


        passBatch.Begin(SpriteSortMode.Deferred, blendState ?? BlendState.AlphaBlend);
        passBatch.Draw(outlineLease.Target, destination, null, Color.White, 0f, Vector2.Zero, new Vector2(scaleX, scaleY), SpriteEffects.None, 0f);
        passBatch.Draw(contentLease.Target, destination, null, fillTint ?? Color.White, 0f, Vector2.Zero, new Vector2(scaleX, scaleY), SpriteEffects.None, 0f);
        passBatch.End();
    }
}

public static class SizeMatrices {
    public static readonly Matrix Half = Matrix.CreateScale(0.5f, 0.5f, 1f);
    public static readonly Matrix Double = Matrix.CreateScale(2f, 2f, 1f);

    public static Vector2 Scale(this Vector2 vector, Matrix matrix) =>
        new(
            matrix.M11 * vector.X,
            matrix.M22 * vector.Y
        );

    public static Vector2 Transform(this Vector2 vector, Matrix matrix) =>
            Vector2.Transform(vector, matrix);  
}

public static class SpritebatchExtensions {
	public static RenderTargetPool.RenderTargetLease DrawHalfScaledToTarget(
		this SpriteBatch spriteBatch,
		GraphicsDevice device,
		RenderTargetPool pool,
		Point targetSize,
		Action<SpriteBatch> drawAction,
		bool clear = true,
		Color? clearColor = null,
		RenderTargetDescriptor? descriptor = null,
		SpriteSortMode sortMode = SpriteSortMode.Deferred,
		BlendState? blendState = null,
		SamplerState? samplerState = null,
		DepthStencilState? depthStencilState = null,
		RasterizerState? rasterizerState = null,
		Effect? effect = null, bool screenPositionOffset = true)
	{
		if (spriteBatch == null)
			throw new ArgumentNullException(nameof(spriteBatch));
		if (device == null)
			throw new ArgumentNullException(nameof(device));
		if (pool == null)
			throw new ArgumentNullException(nameof(pool));
		if (drawAction == null)
			throw new ArgumentNullException(nameof(drawAction));

		var lease = pool.Rent(
			device,
			Math.Max(1, targetSize.X),
			Math.Max(1, targetSize.Y),
			descriptor ?? RenderTargetDescriptor.Default);

		using (var scope = new RenderTargetScope(device, lease.Target, clear, clearColor))
		{
			var transform = SizeMatrices.Half + (screenPositionOffset
                ? Matrix.CreateTranslation(-Main.screenPosition.X * 0.5f, -Main.screenPosition.Y * 0.5f, 0f)
                : Matrix.Identity);
                
			spriteBatch.Begin(
				sortMode,
				blendState ?? BlendState.AlphaBlend,
				samplerState ?? SamplerState.LinearClamp,
				depthStencilState ?? DepthStencilState.None,
				rasterizerState ?? RasterizerState.CullCounterClockwise,
				effect,
				transform);
			try
			{
				drawAction(spriteBatch);
			}
			finally
			{
				spriteBatch.End();
			}
		}

		return lease;
	}
}
