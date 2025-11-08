using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;

namespace Cataphract.Common;

public interface IReactiveValue<T>
{
	T Value { get; set; }
	event Action<T> ValueChanged;
}

public sealed class ReactiveValue<T> : IReactiveValue<T>
{
	private readonly EqualityComparer<T> _comparer = EqualityComparer<T>.Default;
	private T _value;

	public ReactiveValue(T initial = default!)
	{
		_value = initial;
	}

	public event Action<T>? ValueChanged;

	public T Value
	{
		get => _value;
		set
		{
			if (_comparer.Equals(_value, value))
				return;

			_value = value;
			ValueChanged?.Invoke(_value);
		}
	}
}

public abstract class ReactiveElement : IDisposable
{
	private readonly List<Action<GameTime>> _updateHooks = new();

	public Vector2 LocalPosition { get; set; }
	public List<ReactiveElement> Children { get; } = new();

	public void AddUpdater(Action<GameTime> updater)
	{
		if (updater != null)
			_updateHooks.Add(updater);
	}

	public virtual void Update(GameTime gameTime)
	{
		for (int i = 0; i < _updateHooks.Count; i++)
			_updateHooks[i]?.Invoke(gameTime);

		for (int i = 0; i < Children.Count; i++)
			Children[i].Update(gameTime);
	}

	public void Render(SpriteBatch spriteBatch) => RenderRecursive(spriteBatch, Vector2.Zero);

	private void RenderRecursive(SpriteBatch spriteBatch, Vector2 accumulated)
	{
		Vector2 position = accumulated + LocalPosition;
		RenderSelf(spriteBatch, position);

		for (int i = 0; i < Children.Count; i++)
			Children[i].RenderRecursive(spriteBatch, position);
	}

	protected virtual void RenderSelf(SpriteBatch spriteBatch, Vector2 position) { }

	public virtual void Dispose()
	{
		for (int i = 0; i < Children.Count; i++)
			Children[i].Dispose();

		Children.Clear();
		_updateHooks.Clear();
	}
}

public sealed class ReactivePanel : ReactiveElement
{
	public ReactivePanel(Vector2 localPosition) => LocalPosition = localPosition;
}

public sealed class RLabel : ReactiveElement
{
	private readonly Func<DynamicSpriteFont> _fontFunc;
	private readonly Color _color;
	private readonly float _layerDepth;
	private readonly IDisposable _binding;
	private readonly IDisposable? _textSourceLifetime;
	private string _text;

	public RLabel(
		IReactiveValue<string> textSource,
		Func<DynamicSpriteFont> fontProvider,
		Vector2 localPosition,
		Color color,
		float layerDepth = 0f)
	{
		if (textSource == null)
			throw new ArgumentNullException(nameof(textSource));
		if (fontProvider == null)
			throw new ArgumentNullException(nameof(fontProvider));

		LocalPosition = localPosition;
		_fontFunc = fontProvider;
		_color = color;
		_layerDepth = layerDepth;
		_text = textSource.Value ?? string.Empty;
		_binding = textSource.Subscribe(value => _text = value ?? string.Empty);
		_textSourceLifetime = textSource as IDisposable;
	}

	protected override void RenderSelf(SpriteBatch spriteBatch, Vector2 position)
	{
		DynamicSpriteFont font = _fontFunc();
		if (font == null || string.IsNullOrEmpty(_text)) // consider throwing?
			return;

		spriteBatch.DrawString(font, _text, position, _color, 0f, Vector2.Zero, 1f, SpriteEffects.None, _layerDepth);
	}

	public override void Dispose()
	{
		_binding.Dispose();
		_textSourceLifetime?.Dispose();
		base.Dispose();
	}
}

public static class ReactiveValueExtensions
{
	public static IDisposable Subscribe<T>(this IReactiveValue<T> source, Action<T> handler)
	{
		if (source == null)
			throw new ArgumentNullException(nameof(source));
		if (handler == null)
			throw new ArgumentNullException(nameof(handler));

		var subscription = new Subscription<T>(source, handler);
		handler(source.Value);
		return subscription;
	}

	public static IReactiveValue<TResult> Select<TSource, TResult>(this IReactiveValue<TSource> source, Func<TSource, TResult> projector)
	{
		if (source == null)
			throw new ArgumentNullException(nameof(source));
		if (projector == null)
			throw new ArgumentNullException(nameof(projector));

		return new ReactiveProjection<TSource, TResult>(source, projector);
	}

	private sealed class Subscription<T> : IDisposable
	{
		private IReactiveValue<T>? _source;
		private Action<T>? _handler;

		public Subscription(IReactiveValue<T> source, Action<T> handler)
		{
			_source = source;
			_handler = handler;
			source.ValueChanged += handler;
		}

		public void Dispose()
		{
			if (_source != null && _handler != null)
				_source.ValueChanged -= _handler;

			_source = null;
			_handler = null;
		}
	}

    /// <summary>
    /// A reactive value that projects another reactive value through a transformation function.
    /// </summary>
    /// <typeparam name="TSource"></typeparam>
    /// <typeparam name="TResult"></typeparam>
	private sealed class ReactiveProjection<TSource, TResult> : IReactiveValue<TResult>, IDisposable
	{
		private readonly Func<TSource, TResult> _projector;
		private readonly IReactiveValue<TSource> _source;
		private readonly IDisposable _subscription;
		private readonly EqualityComparer<TResult> _comparer = EqualityComparer<TResult>.Default;
		private bool _disposed;
		private TResult _value = default!;
        
		public ReactiveProjection(IReactiveValue<TSource> source, Func<TSource, TResult> projector)
		{
			_source = source;
			_projector = projector;
			_value = _projector(_source.Value);
			_subscription = _source.Subscribe(OnSourceChanged);
		}

		public event Action<TResult>? ValueChanged;

		public TResult Value
		{
			get => _value;
			set => throw new InvalidOperationException("Reactive projection is read-only.");
		}

		private void OnSourceChanged(TSource value)
		{
			if (_disposed)
				return;

			TResult next = _projector(value);
			if (_comparer.Equals(_value, next))
				return;

			_value = next;
			ValueChanged?.Invoke(_value);
		}

		public void Dispose()
		{
			if (_disposed)
				return;

			_disposed = true;
			_subscription.Dispose();
		}
	}
}
