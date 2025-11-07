using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace Cataphract.Common;

public readonly struct AtlasRegion
{
	public AtlasRegion(string key, Rectangle source, Point atlasSize)
	{
		Key = key;
		Source = source;
		float invW = atlasSize.X > 0 ? 1f / atlasSize.X : 0f;
		float invH = atlasSize.Y > 0 ? 1f / atlasSize.Y : 0f;
		MinUV = new Vector2(source.Left * invW, source.Top * invH);
		MaxUV = new Vector2(source.Right * invW, source.Bottom * invH);
		Size = source.Size().ToPoint();
	}

	public string Key { get; }
	public Rectangle Source { get; }
	public Vector2 MinUV { get; }
	public Vector2 MaxUV { get; }
	public Point Size { get; }
}

public sealed class TextureAtlas
{
	private readonly Dictionary<string, AtlasRegion> _regions;

	internal TextureAtlas(Texture2D texture, Dictionary<string, AtlasRegion> regions)
	{
		Texture = texture ?? throw new ArgumentNullException(nameof(texture));
		_regions = regions ?? throw new ArgumentNullException(nameof(regions));
	}

	public Texture2D Texture { get; }

	public bool TryGetRegion(string key, out AtlasRegion region) => _regions.TryGetValue(key, out region);

	public AtlasRegion this[string key] => _regions[key];

	public IReadOnlyDictionary<string, AtlasRegion> Regions => _regions;
}

public sealed class TextureAtlasBuilder
{
	private sealed record Entry(string Key, Texture2D Texture, int Padding);

	private readonly List<Entry> _entries = new();

	public void Clear() => _entries.Clear();

	public void Add(string key, Texture2D texture, int padding = 1)
	{
		if (string.IsNullOrWhiteSpace(key))
			throw new ArgumentException("Key must be non-empty.", nameof(key));
		if (texture == null)
			throw new ArgumentNullException(nameof(texture));
		if (padding < 0)
			throw new ArgumentOutOfRangeException(nameof(padding));

		_entries.Add(new Entry(key, texture, padding));
	}

	public TextureAtlas Build(GraphicsDevice device, bool forcePowerOfTwo = true, bool disposeSource = false)
	{
		if (device == null)
			throw new ArgumentNullException(nameof(device));
		if (_entries.Count == 0)
			throw new InvalidOperationException("No textures added to the atlas builder.");

		var ordered = _entries
			.OrderByDescending(e => e.Texture.Height)
			.ThenByDescending(e => e.Texture.Width)
			.ToArray();

		int maxDimension = device.GraphicsProfile == GraphicsProfile.HiDef ? 8192 : 4096;
		int initialWidth = NextPowerOfTwo(ordered.Max(e => e.Texture.Width + e.Padding * 2));
		int atlasWidth = Math.Max(64, initialWidth);

		var placements = new Rectangle[ordered.Length];
		int atlasHeight = 0;

		while (atlasWidth <= maxDimension)
		{
			if (TryPack(ordered, atlasWidth, placements, out atlasHeight))
				break;

			atlasWidth <<= 1;
		}

		if (atlasWidth > maxDimension || atlasHeight > maxDimension)
			throw new InvalidOperationException("Failed to pack atlas within device limits.");

		if (forcePowerOfTwo)
		{
			atlasWidth = NextPowerOfTwo(atlasWidth);
			atlasHeight = NextPowerOfTwo(atlasHeight);
		}

		var renderTarget = new RenderTarget2D(device, atlasWidth, atlasHeight, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
		var previousTargets = device.GetRenderTargets();
		device.SetRenderTarget(renderTarget);
		device.Clear(Color.Transparent);

		using (var spriteBatch = new SpriteBatch(device))
		{
			spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
			for (int i = 0; i < ordered.Length; i++)
				spriteBatch.Draw(ordered[i].Texture, placements[i].Location.ToVector2(), Color.White);
			spriteBatch.End();
		}

		device.SetRenderTargets(previousTargets);
		var atlasTexture = new Texture2D(device, atlasWidth, atlasHeight);
		var data = new Color[atlasWidth * atlasHeight];
		renderTarget.GetData(data);
		atlasTexture.SetData(data);
		renderTarget.Dispose();

		if (disposeSource)
		{
			foreach (var entry in ordered)
				entry.Texture.Dispose();
		}

		var regions = new Dictionary<string, AtlasRegion>(ordered.Length, StringComparer.Ordinal);
		for (int i = 0; i < ordered.Length; i++)
		{
			var region = new AtlasRegion(ordered[i].Key, placements[i], new Point(atlasWidth, atlasHeight));
			regions[ordered[i].Key] = region;
		}

		return new TextureAtlas(atlasTexture, regions);
	}

	private static bool TryPack(Entry[] entries, int width, Rectangle[] output, out int height)
	{
		int x = 0;
		int y = 0;
		int rowHeight = 0;

		for (int i = 0; i < entries.Length; i++)
		{
			var entry = entries[i];
			int paddedWidth = entry.Texture.Width + entry.Padding * 2;
			int paddedHeight = entry.Texture.Height + entry.Padding * 2;

			if (paddedWidth > width)
			{
				height = 0;
				return false;
			}

			if (x + paddedWidth > width)
			{
				y += rowHeight;
				x = 0;
				rowHeight = 0;
			}

			var rect = new Rectangle(x + entry.Padding, y + entry.Padding, entry.Texture.Width, entry.Texture.Height);
			output[i] = rect;

			x += paddedWidth;
			rowHeight = Math.Max(rowHeight, paddedHeight);
		}

		height = y + rowHeight;
		return true;
	}

	private static int NextPowerOfTwo(int value)
	{
		value = Math.Max(1, value);
		value--;
		value |= value >> 1;
		value |= value >> 2;
		value |= value >> 4;
		value |= value >> 8;
		value |= value >> 16;
		return value + 1;
	}
}
