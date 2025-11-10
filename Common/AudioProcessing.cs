using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Numerics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using ReLogic.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;
using System.Linq;

namespace Cataphract.Common;

public interface IAudioDspEffect
{
    /// <summary>Called when the sample rate changes.</summary>
    void Initialize(float sampleRate);
    /// <summary>Processes the provided samples in place.</summary>
    /// <remarks>Samples are interleaved if channels > 1.</remarks>
    void Process(float[] samples, int channelCount);
}

public sealed class AudioDspProcessor
{
    private readonly List<IAudioDspEffect> _effects = new();
    private float _sampleRate = 44100f;

    public AudioDspProcessor(float sampleRate = 44100f) => SetSampleRate(sampleRate);

    public float SampleRate => _sampleRate;

    public void SetSampleRate(float sampleRate)
    {
        _sampleRate = Math.Max(1000f, sampleRate);
        foreach (var effect in _effects)
            effect.Initialize(_sampleRate);
    }

    public void AddEffect(IAudioDspEffect effect)
    {
        if (effect == null)
            throw new ArgumentNullException(nameof(effect));
        _effects.Add(effect);
        effect.Initialize(_sampleRate);
    }

    public void RemoveEffect(IAudioDspEffect effect) => _effects.Remove(effect);

    public void ClearEffects() => _effects.Clear();

    /// <summary>Runs the processor on the provided audio buffer.</summary>
    public void ProcessBuffer(float[] samples, int channelCount)
    {
        if (samples == null)
            throw new ArgumentNullException(nameof(samples));
        if (channelCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(channelCount));

        for (int i = 0; i < _effects.Count; i++)
            _effects[i].Process(samples, channelCount);
    }

    /// <summary>
    /// Processes the provided audio buffer and submits it to the given DynamicSoundEffectInstance.
    /// </summary>
    public void QueueProcessedBuffer(DynamicSoundEffectInstance instance, float[] samples, int channelCount)
    {
        ProcessBuffer(samples, channelCount);
        instance.SubmitFloatBufferEXT(samples);
    }
}

static class PcmSimdHelper
{
	private const float ShortToFloat = 1f / 32768f;
	private const float FloatToShort = 32767f;
	private static readonly Vector<float> NegOne = new(-1f);
	private static readonly Vector<float> PosOne = new(1f);
	private static readonly Vector<float> PolyC1 = new(27f);
	private static readonly Vector<float> PolyC2 = new(9f);

	public static void ApplyGain(Span<float> samples, float gain)
	{
		if (samples.IsEmpty)
			return;

		int i = 0;
		if (Vector.IsHardwareAccelerated)
		{
			int width = Vector<float>.Count;
			var vGain = new Vector<float>(gain);
			for (; i <= samples.Length - width; i += width)
			{
				var v = new Vector<float>(samples.Slice(i, width)) * vGain;
				v = Vector.Min(Vector.Max(v, NegOne), PosOne);
				v.CopyTo(samples.Slice(i, width));
			}
		}

		for (; i < samples.Length; i++)
		{
			float scaled = samples[i] * gain;
			samples[i] = MathHelper.Clamp(scaled, -1f, 1f);
		}
	}

	public static void ApplySoftClip(Span<float> samples, float drive)
	{
		if (samples.IsEmpty || drive <= 0f)
			return;

		int i = 0;
		if (Vector.IsHardwareAccelerated)
		{
			int width = Vector<float>.Count;
			var vDrive = new Vector<float>(drive);
			for (; i <= samples.Length - width; i += width)
			{
				var v = new Vector<float>(samples.Slice(i, width)) * vDrive;
				var v2 = v * v;
				var numerator = v * (PolyC1 + v2);
				var denominator = PolyC1 + (PolyC2 * v2);
				var soft = numerator / denominator;
				soft = Vector.Min(Vector.Max(soft, NegOne), PosOne);
				soft.CopyTo(samples.Slice(i, width));
			}
		}

		for (; i < samples.Length; i++)
		{
			float x = samples[i] * drive;
			float saturated = x * (27f + x * x) / (27f + 9f * x * x);
			samples[i] = MathHelper.Clamp(saturated, -1f, 1f);
		}
	}

	public static void ConvertS16ToF32(ReadOnlySpan<short> source, Span<float> destination)
	{
		int i = 0;
		if (Vector.IsHardwareAccelerated)
		{
			int width = Vector<short>.Count;
			var scale = new Vector<float>(ShortToFloat);
			for (; i <= source.Length - width; i += width)
			{
				var s = new Vector<short>(source.Slice(i, width));
				Vector.Widen(s, out Vector<int> lower, out Vector<int> upper);
				(Vector.ConvertToSingle(lower) * scale).CopyTo(destination.Slice(i, Vector<int>.Count));
				(Vector.ConvertToSingle(upper) * scale).CopyTo(destination.Slice(i + Vector<int>.Count, Vector<int>.Count));
			}
		}
		for (; i < source.Length; i++)
			destination[i] = MathHelper.Clamp(source[i] * ShortToFloat, -1f, 1f);
	}

	public static void ConvertF32ToS16(ReadOnlySpan<float> source, Span<short> destination)
	{
		int i = 0;
		if (Vector.IsHardwareAccelerated)
		{
			int width = Vector<float>.Count;
			var min = new Vector<float>(-1f);
			var max = new Vector<float>(1f);
			var scale = new Vector<float>(FloatToShort);
			Span<int> temp = stackalloc int[Vector<int>.Count];
			for (; i <= source.Length - width; i += width)
			{
				var v = new Vector<float>(source.Slice(i, width));
				v = Vector.Min(Vector.Max(v, min), max) * scale;
				Vector.ConvertToInt32(v).CopyTo(temp);
				for (int j = 0; j < temp.Length; j++)
					destination[i + j] = (short)Math.Clamp(temp[j], short.MinValue, short.MaxValue);
			}
		}
		for (; i < source.Length; i++)
		{
			float clamped = MathHelper.Clamp(source[i], -1f, 1f);
			destination[i] = (short)Math.Clamp((int)MathF.Round(clamped * FloatToShort), short.MinValue, short.MaxValue);
		}
	}

	public static void ReverseInPlace(Span<float> samples, int channelCount)
	{
		if (samples.IsEmpty || channelCount <= 0)
			return;

		if (channelCount == 1)
		{
			ReverseMono(samples);
			return;
		}

		int frameCount = samples.Length / channelCount;
		if (frameCount <= 1)
			return;

		int trimmed = frameCount * channelCount;
		Span<float> span = samples.Slice(0, trimmed);

		int head = 0;
		int tail = frameCount - 1;
		while (head < tail)
		{
			int headIndex = head * channelCount;
			int tailIndex = tail * channelCount;
			for (int c = 0; c < channelCount; c++)
			{
				float temp = span[headIndex + c];
				span[headIndex + c] = span[tailIndex + c];
				span[tailIndex + c] = temp;
			}

			head++;
			tail--;
		}
	}

	private static void ReverseMono(Span<float> samples)
	{
		if (samples.Length <= 1)
			return;

		if (Sse.IsSupported && samples.Length >= Vector128<float>.Count)
		{
			ReverseMonoSse(samples);
			return;
		}

		ReverseScalar(samples);
	}

	private static unsafe void ReverseMonoSse(Span<float> samples)
	{
		int len = samples.Length;
		int width = Vector128<float>.Count;

		fixed (float* ptr = samples)
		{
			float* leftPtr = ptr;
			float* rightPtr = ptr + len - width;

			while (leftPtr + width <= rightPtr)
			{
				Vector128<float> leftVec = Sse.LoadVector128(leftPtr);
				Vector128<float> rightVec = Sse.LoadVector128(rightPtr);

				Vector128<float> leftRev = Sse.Shuffle(leftVec, leftVec, 0x1B);
				Vector128<float> rightRev = Sse.Shuffle(rightVec, rightVec, 0x1B);

				Sse.Store(leftPtr, rightRev);
				Sse.Store(rightPtr, leftRev);

				leftPtr += width;
				rightPtr -= width;
			}

			int processed = (int)(leftPtr - ptr);
			int remainingLength = len - (processed * 2);
			if (remainingLength > 1)
				ReverseScalar(samples.Slice(processed, remainingLength));
		}
	}

	private static void ReverseScalar(Span<float> samples)
	{
		int i = 0;
		int j = samples.Length - 1;
		while (i < j)
		{
			float tmp = samples[i];
			samples[i] = samples[j];
			samples[j] = tmp;
			i++;
			j--;
		}
	}
}

#region  Audio effects
public abstract class OnePoleFilterBase : IAudioDspEffect
{
    protected float SampleRate = 44100f;
    protected float Alpha;
    private float[] _states = Array.Empty<float>();

    public float Cutoff
    {
        get => _cutoff;
        set
        {
            _cutoff = Math.Clamp(value, 10f, SampleRate * 0.49f);
            UpdateCoefficient();
        }
    }

    private float _cutoff = 1000f;

    public void Initialize(float sampleRate)
    {
        SampleRate = Math.Max(1000f, sampleRate);
        UpdateCoefficient();
        Array.Clear(_states, 0, _states.Length);
    }

    public void Process(float[] samples, int channelCount)
    {
        int oldLength = _states.Length;
        if (oldLength < channelCount)
        {
            Array.Resize(ref _states, channelCount);
            Array.Clear(_states, oldLength, channelCount - oldLength);
            HandleChannelCapacityIncrease(oldLength, channelCount);
        }

        for (int i = 0; i < samples.Length; i += channelCount)
        {
            for (int c = 0; c < channelCount && i + c < samples.Length; c++)
                samples[i + c] = ProcessSample(samples[i + c], c);
        }
    }

    protected abstract void UpdateCoefficient();
    protected abstract float ProcessSample(float input, int channel);
    protected virtual void HandleChannelCapacityIncrease(int oldLength, int newLength) { }

    protected ref float ChannelState(int channel) => ref _states[channel];
}

public sealed class LowPassOnePoleFilter : OnePoleFilterBase
{
    protected override void UpdateCoefficient()
    {
        float x = MathF.Exp(-2f * MathF.PI * Cutoff / SampleRate);
        Alpha = 1f - x;
    }

    protected override float ProcessSample(float input, int channel)
    {
        ref float state = ref ChannelState(channel);
        state += Alpha * (input - state);
        return state;
    }
}

public sealed class HighPassOnePoleFilter : OnePoleFilterBase
{
    private float[] _previousInputs = Array.Empty<float>();

    protected override void HandleChannelCapacityIncrease(int oldLength, int newLength)
    {
        int prevOld = _previousInputs.Length;
        if (prevOld < newLength)
        {
            Array.Resize(ref _previousInputs, newLength);
            Array.Clear(_previousInputs, prevOld, newLength - prevOld);
        }
    }


    protected override void UpdateCoefficient()
    {
        float x = MathF.Exp(-2f * MathF.PI * Cutoff / SampleRate);
        Alpha = x;
    }

    protected override float ProcessSample(float input, int channel)
    {
        float previous = _previousInputs[channel];
        ref float state = ref ChannelState(channel);
        state = Alpha * (state + input - previous);
        _previousInputs[channel] = input;
        return state;
    }
}

public abstract class BiquadFilterBase : IAudioDspEffect
{
    private float[] _z1 = Array.Empty<float>();
    private float[] _z2 = Array.Empty<float>();
    protected float a0, a1, a2, b1, b2;
    protected float SampleRate = 44100f;
    private float _frequency = 1000f;
    private float _q = 0.707f;
    private float _gainDb;

    public float Frequency
    {
        get => _frequency;
        set
        {
            _frequency = MathF.Min(MathF.Max(value, 10f), SampleRate * 0.49f);
            UpdateCoefficients();
        }
    }

    public float Q
    {
        get => _q;
        set
        {
            _q = MathF.Max(0.05f, value);
            UpdateCoefficients();
        }
    }

    public float GainDb
    {
        get => _gainDb;
        set
        {
            _gainDb = value;
            UpdateCoefficients();
        }
    }

    public void Initialize(float sampleRate)
    {
        SampleRate = Math.Max(1000f, sampleRate);
        UpdateCoefficients();
        Array.Clear(_z1, 0, _z1.Length);
        Array.Clear(_z2, 0, _z2.Length);
    }

    public void Process(float[] samples, int channelCount)
    {
        EnsureState(channelCount);
        for (int i = 0; i < samples.Length; i += channelCount)
        {
            for (int c = 0; c < channelCount; c++)
            {
                int idx = i + c;
                if (idx >= samples.Length)
                    break;
                float input = samples[idx];
                float output = a0 * input + _z1[c];
                _z1[c] = a1 * input + _z2[c] - b1 * output;
                _z2[c] = a2 * input - b2 * output;
                samples[idx] = output;
            }
        }
    }

    protected abstract void ComputeCoefficients(float omega, float sin, float cos, float alpha, float amplitude);

    protected void UpdateCoefficients()
    {
        float omega = MathHelper.TwoPi * Frequency / SampleRate;
        float sin = MathF.Sin(omega);
        float cos = MathF.Cos(omega);
        float alpha = sin / (2f * Q);
        float amplitude = MathF.Pow(10f, GainDb / 40f);
        ComputeCoefficients(omega, sin, cos, alpha, amplitude);
    }

    protected void SetCoefficients(float b0, float b1, float b2, float a0Coef, float a1Coef, float a2Coef)
    {
        float invA0 = 1f / MathF.Max(1e-6f, a0Coef);
        a0 = b0 * invA0;
        a1 = b1 * invA0;
        a2 = b2 * invA0;
        this.b1 = a1Coef * invA0;
        this.b2 = a2Coef * invA0;
    }

    private void EnsureState(int channelCount)
    {
        if (_z1.Length == channelCount)
            return;
        Array.Resize(ref _z1, channelCount);
        Array.Resize(ref _z2, channelCount);
    }
}

public sealed class BandPassBiquadFilter : BiquadFilterBase
{
    protected override void ComputeCoefficients(float omega, float sin, float cos, float alpha, float amplitude)
    {
        SetCoefficients(alpha, 0f, -alpha, 1f + alpha, -2f * cos, 1f - alpha);
    }
}

public sealed class NotchBiquadFilter : BiquadFilterBase
{
    protected override void ComputeCoefficients(float omega, float sin, float cos, float alpha, float amplitude)
    {
        SetCoefficients(1f, -2f * cos, 1f, 1f + alpha, -2f * cos, 1f - alpha);
    }
}

public sealed class PeakingEqFilter : BiquadFilterBase
{
    protected override void ComputeCoefficients(float omega, float sin, float cos, float alpha, float amplitude)
    {
        float alphaA = alpha * amplitude;
        float alphaDiv = alpha / amplitude;
        SetCoefficients(1f + alphaA, -2f * cos, 1f - alphaA, 1f + alphaDiv, -2f * cos, 1f - alphaDiv);
    }
}

public sealed class BitCrusherEffect : IAudioDspEffect
{
    public int Bits
    {
        get => _bits;
        set => _bits = Math.Clamp(value, 1, 16);
    }

    public float DownsampleFactor
    {
        get => _downsampleFactor;
        set => _downsampleFactor = MathF.Max(1f, value);
    }

    private int _bits = 10;
    private float _downsampleFactor = 1f;
    private int _frameCounter;
    private float[] _heldSamples = Array.Empty<float>();

    public void Initialize(float sampleRate)
    {
        _frameCounter = 0;
        Array.Clear(_heldSamples, 0, _heldSamples.Length);
    }

    public void Process(float[] samples, int channelCount)
    {
        if (_heldSamples.Length != channelCount)
            _heldSamples = new float[channelCount];

        int step = Math.Max(1, (int)MathF.Round(_downsampleFactor));
        float levels = MathF.Pow(2f, Bits) - 1f;

        for (int i = 0; i < samples.Length; i += channelCount)
        {
            int frameEnd = Math.Min(samples.Length, i + channelCount);

            if ((_frameCounter % step) == 0)
            {
                for (int channel = 0; channel < channelCount; channel++)
                {
                    int idx = i + channel;
                    if (idx >= frameEnd)
                        break;

                    float normalized = MathHelper.Clamp(samples[idx] * 0.5f + 0.5f, 0f, 1f);
                    float quantized = MathF.Round(normalized * levels) / levels;
                    _heldSamples[channel] = quantized * 2f - 1f;
                }
            }

            for (int channel = 0; channel < channelCount; channel++)
            {
                int idx = i + channel;
                if (idx >= frameEnd)
                    break;
                samples[idx] = _heldSamples[channel];
            }

            _frameCounter++;
            if (_frameCounter >= int.MaxValue - 1024)
                _frameCounter = 0;
        }
    }
}

public sealed class SoftClipSaturator : IAudioDspEffect
{
    public float Drive
    {
        get => _drive;
        set => _drive = MathF.Max(0.1f, value);
    }

    private float _drive = 1.5f;

    public void Initialize(float sampleRate) { }

    public void Process(float[] samples, int channelCount)
    {
        PcmSimdHelper.ApplySoftClip(samples.AsSpan(), _drive);
    }
}

public sealed class StereoWidenerEffect : IAudioDspEffect
{
    public float Width
    {
        get => _width;
        set => _width = MathHelper.Clamp(value, -1f, 1f);
    }

    private float _width = 0.25f;

    public void Initialize(float sampleRate) { }

    public void Process(float[] samples, int channelCount)
    {
        if (channelCount < 2)
            return;

        if (channelCount == 2)
        {
            Span<float> span = samples;
            int frameCount = span.Length / 2;
            var stereo = MemoryMarshal.Cast<float, System.Numerics.Vector2>(span.Slice(0, frameCount * 2));
            float widthScale = 0.5f * (1f + _width);

            for (int i = 0; i < stereo.Length; i++)
            {
                System.Numerics.Vector2 lr = stereo[i];
                float mid = (lr.X + lr.Y) * 0.5f;
                float side = (lr.X - lr.Y) * widthScale;
                stereo[i] = new System.Numerics.Vector2(
                    MathHelper.Clamp(mid + side, -1f, 1f),
                    MathHelper.Clamp(mid - side, -1f, 1f));
            }

            return;
        }

        for (int i = 0; i < samples.Length; i += channelCount)
        {
            int leftIndex = i;
            int rightIndex = i + 1;
            if (rightIndex >= samples.Length)
                break;

            float left = samples[leftIndex];
            float right = samples[rightIndex];
            float mid = (left + right) * 0.5f;
            float side = (left - right) * 0.5f * (1f + _width);
            samples[leftIndex] = MathHelper.Clamp(mid + side, -1f, 1f);
            samples[rightIndex] = MathHelper.Clamp(mid - side, -1f, 1f);
        }
    }
}

public sealed class TremoloEffect : IAudioDspEffect
{
    public float Rate
    {
        get => _rate;
        set => _rate = MathF.Max(0.01f, value);
    }

    public float Depth
    {
        get => _depth;
        set => _depth = MathHelper.Clamp(value, 0f, 1f);
    }

    private float _sampleRate = 44100f;
    private float _phase;
    private float _rate = 5f;
    private float _depth = 0.5f;

    public void Initialize(float sampleRate)
    {
        _sampleRate = Math.Max(1000f, sampleRate);
        _phase = 0f;
    }

    public void Process(float[] samples, int channelCount)
    {
        float phaseIncrement = MathHelper.TwoPi * _rate / _sampleRate;
        for (int i = 0; i < samples.Length; i += channelCount)
        {
            float mod = 1f - _depth + _depth * (0.5f * (MathF.Sin(_phase) + 1f));
            int frameEnd = Math.Min(samples.Length, i + channelCount);

            for (int channel = 0; channel < channelCount; channel++)
            {
                int idx = i + channel;
                if (idx >= frameEnd)
                    break;
                samples[idx] *= mod;
            }

            _phase += phaseIncrement;
            if (_phase > MathHelper.TwoPi)
                _phase -= MathHelper.TwoPi;
        }
    }
}

public sealed class DelayEffect : IAudioDspEffect
{
    public float DelayMilliseconds
    {
        get => _delayMs;
        set => _delayMs = MathHelper.Clamp(value, 1f, 2000f);
    }

    public float Feedback
    {
        get => _feedback;
        set => _feedback = MathHelper.Clamp(value, 0f, 0.95f);
    }

    public float Wet
    {
        get => _wet;
        set => _wet = MathHelper.Clamp(value, 0f, 1f);
    }

    private float[] _buffer = Array.Empty<float>();
    private int _writeIndex;
    private float _sampleRate = 44100f;
    private int _channels = 2;
    private float _delayMs = 250f;
    private float _feedback = 0.35f;
    private float _wet = 0.3f;

    public void Initialize(float sampleRate)
    {
        _sampleRate = Math.Max(1000f, sampleRate);
        _writeIndex = 0;
        _buffer = Array.Empty<float>();
    }

    public void Process(float[] samples, int channelCount)
    {
        int delaySamples = Math.Max(1, (int)(_sampleRate * DelayMilliseconds / 1000f));
        int targetLength = delaySamples * Math.Max(1, channelCount);
        if (_buffer.Length != targetLength || _channels != channelCount)
        {
            _buffer = new float[targetLength];
            _channels = channelCount;
            _writeIndex = 0;
        }

        int frameStride = Math.Max(1, channelCount);
        for (int i = 0; i < samples.Length; i += frameStride)
        {
            int frameEnd = Math.Min(samples.Length, i + frameStride);
            int readIndex = _writeIndex - delaySamples * frameStride;
            if (readIndex < 0)
                readIndex += _buffer.Length;

            for (int channel = 0; channel < frameStride; channel++)
            {
                int idx = i + channel;
                if (idx >= frameEnd)
                    break;

                int read = (readIndex + channel) % _buffer.Length;
                int write = (_writeIndex + channel) % _buffer.Length;
                float delayed = _buffer[read];
                float dry = samples[idx];
                float mixed = MathHelper.Clamp(dry * (1f - _wet) + delayed * _wet, -1f, 1f);
                samples[idx] = mixed;
                _buffer[write] = dry + delayed * _feedback;
            }

            _writeIndex = (_writeIndex + frameStride) % _buffer.Length;
        }
    }
}

public sealed class AmplifierEffect : IAudioDspEffect
{
	private float _gain = 1f;

	public float Gain
	{
		get => _gain;
		set => _gain = MathF.Max(0f, value);
	}

	public void Initialize(float sampleRate) { }

	public void Process(float[] samples, int channelCount)
	{
		if (_gain <= 0f || samples.Length == 0)
		{
			if (_gain <= 0f)
				Array.Clear(samples, 0, samples.Length);
			return;
		}

		PcmSimdHelper.ApplyGain(samples.AsSpan(), _gain);
	}
}

public sealed class ReverseAudioEffect : IAudioDspEffect
{
	public void Initialize(float sampleRate) { }

	public void Process(float[] samples, int channelCount)
	{
		PcmSimdHelper.ReverseInPlace(samples.AsSpan(), channelCount);
	}
}

#endregion
#region Subtractive synthesizer

public enum Waveform
{
	Saw,
	Square,
	Triangle,
	Noise
}

public enum LfoWaveform
{
	Sine,
	Triangle,
	Square,
	Saw
}

public enum SynthModTarget
{
	None,
	Frequency,
	Amplitude,
	PulseWidth,
	FilterCutoff
}

public sealed record SynthModulator(
	LfoWaveform Waveform,
	SynthModTarget Target,
	float RateHz,
	float Depth,
	float Offset = 0f);

public sealed record SubtractiveSynthPreset(
	string Name,
	Waveform Waveform,
	float PulseWidth = 0.5f,
	float FilterCutoff = 1200f,
	float FilterResonance = 0.1f,
	float AttackSeconds = 0.02f,
	float DecaySeconds = 0.12f,
	float SustainLevel = 0.6f,
	float ReleaseSeconds = 0.18f,
	SynthModulator? Modulator = null,
	bool EnablePwm = false);

public static class SubtractiveSynthPresets
{
	public static readonly SubtractiveSynthPreset ClassicSaw = new(
		"The Saw", Waveform.Saw, FilterCutoff: 1800f, FilterResonance: 0.2f);

	public static readonly SubtractiveSynthPreset SquareLead = new(
		"Basic Square Lead", Waveform.Square, PulseWidth: 0.42f, FilterCutoff: 1500f, FilterResonance: 0.35f,
		AttackSeconds: 0.01f, DecaySeconds: 0.18f, SustainLevel: 0.55f, ReleaseSeconds: 0.22f);

	public static readonly SubtractiveSynthPreset TriangleSoft = new(
		"Triangle Soft", Waveform.Triangle, FilterCutoff: 2200f, FilterResonance: 0.1f,
		AttackSeconds: 0.04f, DecaySeconds: 0.2f, SustainLevel: 0.8f, ReleaseSeconds: 0.4f);

	public static readonly SubtractiveSynthPreset FilterSweepPad = new(
		"Filter Sweep Pad", Waveform.Saw, FilterCutoff: 900f, FilterResonance: 0.45f,
		AttackSeconds: 0.35f, DecaySeconds: 0.6f, SustainLevel: 0.65f, ReleaseSeconds: 0.9f,
		Modulator: new SynthModulator(LfoWaveform.Sine, SynthModTarget.FilterCutoff, RateHz: 0.3f, Depth: 0.45f));

	public static readonly SubtractiveSynthPreset VibratoLead = new(
		"Vibrato Lead", Waveform.Triangle, FilterCutoff: 2600f, FilterResonance: 0.12f,
		AttackSeconds: 0.015f, DecaySeconds: 0.12f, SustainLevel: 0.7f, ReleaseSeconds: 0.3f,
		Modulator: new SynthModulator(LfoWaveform.Sine, SynthModTarget.Frequency, RateHz: 5.5f, Depth: 0.015f));

	public static readonly SubtractiveSynthPreset PulseEnsemble = new(
		"Pulse Ensemble", Waveform.Square, PulseWidth: 0.45f, FilterCutoff: 1600f, FilterResonance: 0.28f,
		AttackSeconds: 0.08f, DecaySeconds: 0.26f, SustainLevel: 0.75f, ReleaseSeconds: 0.65f,
		Modulator: new SynthModulator(LfoWaveform.Triangle, SynthModTarget.Amplitude, RateHz: 0.85f, Depth: 0.25f));

	public static readonly SubtractiveSynthPreset PWMPad = new(
		"PWM Pad", Waveform.Square, PulseWidth: 0.5f, FilterCutoff: 1400f, FilterResonance: 0.32f,
		AttackSeconds: 0.3f, DecaySeconds: 0.7f, SustainLevel: 0.8f, ReleaseSeconds: 1.2f,
		Modulator: new SynthModulator(LfoWaveform.Sine, SynthModTarget.PulseWidth, RateHz: 0.4f, Depth: 0.4f),
		EnablePwm: true);

	public static IEnumerable<SubtractiveSynthPreset> All
	{
		get
		{
			yield return ClassicSaw;
			yield return SquareLead;
			yield return TriangleSoft;
			yield return FilterSweepPad;
			yield return VibratoLead;
			yield return PulseEnsemble;
			yield return PWMPad;
		}
	}
}

public sealed class SubtractiveSynth
{
	private readonly float _sampleRate;

	public SubtractiveSynth(float sampleRate = 44100f) =>
		_sampleRate = Math.Max(1000f, sampleRate);

	public float[] RenderNote(SubtractiveSynthPreset preset, float frequency, float durationSeconds, float gain = 0.7f)
	{
		int samples = Math.Max(1, (int)MathF.Round(durationSeconds * _sampleRate));
		var buffer = new float[samples];
		float basePhaseIncrement = frequency / _sampleRate;
		float phase = 0f;
		float basePulse = MathHelper.Clamp(preset.PulseWidth, 0.05f, 0.95f);

		float baseCutoff = MathHelper.Clamp(preset.FilterCutoff, 50f, _sampleRate * 0.45f);
		float resonance = MathHelper.Clamp(preset.FilterResonance, 0f, 0.95f);

		int attackSamples = Math.Max(1, (int)(_sampleRate * preset.AttackSeconds));
		int decaySamples = Math.Max(1, (int)(_sampleRate * preset.DecaySeconds));
		int releaseSamples = Math.Max(1, (int)(_sampleRate * preset.ReleaseSeconds));
		int sustainStart = attackSamples + decaySamples;
		int sustainEnd = Math.Max(sustainStart, samples - releaseSamples);

		SynthModulator? mod = preset.Modulator;
		float modPhase = 0f;
		float modIncrement = mod != null ? mod.RateHz / _sampleRate : 0f;

		float filterState = 0f;

		for (int i = 0; i < samples; i++)
		{
			float modValue = 0f;
			if (mod != null && mod.Target != SynthModTarget.None)
			{
				modPhase += modIncrement;
				if (modPhase >= 1f)
					modPhase -= 1f;
				float lfo = EvaluateLfo(mod.Waveform, modPhase);
				modValue = lfo * mod.Depth + mod.Offset;
			}

			float phaseIncrement = basePhaseIncrement;
			if (mod != null && mod.Target == SynthModTarget.Frequency)
				phaseIncrement = Math.Max(basePhaseIncrement * (1f + modValue), 1e-6f);

			phase += phaseIncrement;
			if (phase >= 1f)
				phase -= 1f;

			float pulse = basePulse;
			if (preset.EnablePwm && mod != null && mod.Target == SynthModTarget.PulseWidth)
				pulse = MathHelper.Clamp(basePulse + modValue * 0.5f, 0.05f, 0.95f);

			float osc = preset.Waveform switch
			{
				Waveform.Saw => (phase * 2f) - 1f,
				Waveform.Square => phase < pulse ? 1f : -1f,
				Waveform.Triangle => 2f * MathF.Abs(2f * ((phase % 1f) - 0.5f)) - 1f,
				Waveform.Noise => (float)(Main.rand.NextDouble() * 2.0 - 1.0),
				_ => 0f
			};

			float envelope;
			if (i < attackSamples)
				envelope = i / (float)attackSamples;
			else if (i < sustainStart)
			{
				float t = (i - attackSamples) / (float)Math.Max(1, decaySamples);
				envelope = MathHelper.Lerp(1f, preset.SustainLevel, t);
			}
			else if (i < sustainEnd)
				envelope = preset.SustainLevel;
			else
			{
				float t = (i - sustainEnd) / (float)Math.Max(1, releaseSamples);
				envelope = MathHelper.Lerp(preset.SustainLevel, 0f, t);
			}

			float moddedGain = gain;
			if (mod != null && mod.Target == SynthModTarget.Amplitude)
				moddedGain = MathHelper.Clamp(gain * (1f + modValue), 0f, 1.5f);

			float currentCutoff = baseCutoff;
			if (mod != null && mod.Target == SynthModTarget.FilterCutoff)
				currentCutoff = MathHelper.Clamp(baseCutoff * (1f + modValue), 20f, _sampleRate * 0.45f);

			float pole = MathF.Exp(-2f * MathF.PI * currentCutoff / _sampleRate);
			float alpha = 1f - pole;

			float input = osc * envelope * moddedGain;
			filterState += alpha * ((input + resonance * filterState) - filterState);
			buffer[i] = MathHelper.Clamp(filterState, -1f, 1f);
		}

		return buffer;
	}

	private static float EvaluateLfo(LfoWaveform waveform, float phase)
	{
		float t = phase - MathF.Floor(phase);
		return waveform switch
		{
			LfoWaveform.Sine => MathF.Sin(t * MathHelper.TwoPi),
			LfoWaveform.Triangle => 2f * MathF.Abs(2f * (t - 0.5f)) - 1f,
			LfoWaveform.Square => t < 0.5f ? 1f : -1f,
			LfoWaveform.Saw => 2f * t - 1f,
			_ => 0f
		};
	}
}

public sealed class PwmSynth
{
	private readonly SubtractiveSynth _inner;

	public PwmSynth(float sampleRate = 44100f) => _inner = new SubtractiveSynth(sampleRate);

	public float[] RenderPwm(SubtractiveSynthPreset basePreset, float frequency, float durationSeconds, float lfoRateHz, float depth, float gain = 0.7f)
	{
		var preset = basePreset with
		{
			EnablePwm = true,
			Modulator = new SynthModulator(LfoWaveform.Sine, SynthModTarget.PulseWidth, lfoRateHz, depth)
		};
		return _inner.RenderNote(preset, frequency, durationSeconds, gain);
	}
}
#endregion

public static class SubtractiveSynthDemo
{
	public static DynamicSoundEffectInstance CreateTwelveTetDemo(
		AudioDspProcessor? processor,
		SubtractiveSynthPreset preset,
		float rootFrequency = 220f,
		float noteDuration = 0.35f,
		float sampleRate = 44100f,
		bool autoPlay = true)
	{
		var synth = new SubtractiveSynth(sampleRate);
		var instance = new DynamicSoundEffectInstance((int)sampleRate, AudioChannels.Mono);

		for (int semitone = 0; semitone < 12; semitone++)
		{
			float frequency = rootFrequency * MathF.Pow(2f, semitone / 12f);
			float[] note = synth.RenderNote(preset, frequency, noteDuration);
			processor?.SetSampleRate(sampleRate);
			processor?.ProcessBuffer(note, 1);
			instance.SubmitFloatBufferEXT(note);
		}

		if (autoPlay)
			instance.Play();

		return instance;
	}

	public static DynamicSoundEffectInstance CreateMelodyDemo(
		AudioDspProcessor? processor,
		SubtractiveSynthPreset preset,
		float rootFrequency,
		ReadOnlySpan<(int Semitone, float Duration)> melody,
		float noteGain = 0.7f,
		float sampleRate = 44100f,
		bool autoPlay = true)
	{
		if (melody.IsEmpty)
			throw new ArgumentException("Melody cannot be empty.", nameof(melody));

		var synth = new SubtractiveSynth(sampleRate);
		var instance = new DynamicSoundEffectInstance((int)sampleRate, AudioChannels.Mono);

		for (int i = 0; i < melody.Length; i++)
		{
			var (semitone, duration) = melody[i];
			float noteDuration = Math.Max(duration, 0.05f);
			float frequency = rootFrequency * MathF.Pow(2f, semitone / 12f);
			float[] note = synth.RenderNote(preset, frequency, noteDuration, noteGain);
			processor?.SetSampleRate(sampleRate);
			processor?.ProcessBuffer(note, 1);
			instance.SubmitFloatBufferEXT(note);
		}

		if (autoPlay)
			instance.Play();

		return instance;
	}
}

public static class TestDSP
{
    public static readonly AudioDspProcessor Processor = new();
}

public static class SoundEffectDspExtensions
{
    public static DynamicSoundEffectInstance CreateProcessedInstance(this SoundEffect source, AudioDspProcessor processor, out float[] processedSamples, bool autoPlay = false)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (processor == null)
            throw new ArgumentNullException(nameof(processor));

        int sampleRate = (int)source.sampleRate;
        AudioChannels channels = source.channels == (ushort)AudioChannels.Stereo ? AudioChannels.Stereo : AudioChannels.Mono;
        int channelCount = channels == AudioChannels.Stereo ? 2 : 1;

        int sampleCount = SoundEffect.GetSampleSizeInBytes(source.Duration, sampleRate, channels) / sizeof(short);
        if (sampleCount <= 0)
        {
            processedSamples = Array.Empty<float>();
            return new DynamicSoundEffectInstance(sampleRate, channels);
        }

        var pcm = ArrayPool<short>.Shared.Rent(sampleCount);
        try
        {
            Marshal.Copy(source.handle.pAudioData, pcm, 0, sampleCount);

            processedSamples = new float[sampleCount];
            PcmSimdHelper.ConvertS16ToF32(new ReadOnlySpan<short>(pcm, 0, sampleCount), processedSamples.AsSpan());

            processor.SetSampleRate(sampleRate);
            processor.ProcessBuffer(processedSamples, channelCount);

            var instance = new DynamicSoundEffectInstance(sampleRate, channels);
            instance.SubmitFloatBufferEXT(processedSamples);
            if (autoPlay)
                instance.Play();
            return instance;
        }
        finally
        {
            ArrayPool<short>.Shared.Return(pcm);
        }
    }
}

public static class ExampleFactUsage
{
    public static unsafe void OnFactVoiceSubmit(float* samples, int frameCount, int channelCount, float sampleRate)
    {
        if (samples == null || frameCount <= 0 || channelCount <= 0)
            return;

        var span = new Span<float>(samples, frameCount * channelCount);
        var managed = ArrayPool<float>.Shared.Rent(span.Length);
        try
        {
            span.CopyTo(managed);
            TestDSP.Processor.SetSampleRate(sampleRate);
            TestDSP.Processor.ProcessBuffer(managed, channelCount);
            managed.AsSpan(0, span.Length).CopyTo(span);
        }
        finally
        {
            ArrayPool<float>.Shared.Return(managed);
        }
    }
}

public sealed class TestMusicFilterSystem : ModSystem
{
    private readonly HighPassOnePoleFilter _rumbleCut = new() { Cutoff = 80f };
    private readonly LowPassOnePoleFilter _sparkle = new() { Cutoff = 7000f };
    private readonly PeakingEqFilter _presence = new() { Frequency = 2200f, Q = 0.9f, GainDb = 3f };
    private readonly BandPassBiquadFilter _motionBand = new() { Frequency = 600f, Q = 2f };
    private readonly TremoloEffect _tremolo = new() { Rate = 5f, Depth = 0.08f };
    private readonly DelayEffect _delay = new() { DelayMilliseconds = 180f, Feedback = 0.25f, Wet = 0.15f };
    private readonly StereoWidenerEffect _widener = new() { Width = 0.35f };
    private readonly SoftClipSaturator _saturator = new() { Drive = 1.80f };
    private readonly BitCrusherEffect _bitTexture = new() { Bits = 8, DownsampleFactor = 1f };
    private readonly LowPassOnePoleFilter _lowPassMaster = new() { Cutoff = 20000f };
    private readonly HighPassOnePoleFilter _highPassMaster = new() { Cutoff = 200f };
    private readonly BandPassBiquadFilter _masterBand = new() { Frequency = 1000f, Q = 1f };
    private readonly AmplifierEffect _masterAmplifier = new() { Gain = 1.5f };
    private readonly ReverseAudioEffect _reverseEffect = new();

    private DynamicSoundEffectInstance? _exampleInstance;
    private DynamicSoundEffectInstance? _melodyInstance;
    private float _timeAccumulator;
    private float _sampleRate = SampleRateDefault;
    private int _channelCount = ChannelCountDefault;
    private float[] _processedSamples = Array.Empty<float>();

    private const float SampleRateDefault = 44100f;
    private const int ChannelCountDefault = 2;
    private const float TriggerIntervalSeconds = 2.75f;
    private static readonly (int Semitone, float Duration)[] DemoMelody =
    {
        (0, 0.40f),
        (2, 0.35f),
        (4, 0.35f),
        (5, 0.25f),
        (7, 0.50f),
        (5, 0.30f),
        (4, 0.30f),
        (2, 0.45f),
        (9, 1.50f),
        (7, 1.75f)
    };

    public override void OnModLoad()
    {
        var baseEffect = Assets.Audio.Misc.StupidGun_HeavyFire.Asset.GetSoundEffect();
        _sampleRate = baseEffect.sampleRate;
        _channelCount = baseEffect.channels == (ushort)AudioChannels.Stereo ? 2 : 1;

        var processor = TestDSP.Processor;
        processor.ClearEffects();
        processor.SetSampleRate(_sampleRate);

        _rumbleCut.Cutoff = 320f;
        _sparkle.Cutoff = 1900f;
        _presence.Frequency = 1600f;
        _presence.GainDb = 6f;
        _motionBand.Frequency = 900f;
        _tremolo.Rate = 1f;
        _tremolo.Depth = 1f;
        _delay.DelayMilliseconds = 260f;
        _delay.Feedback = 0.45f;
        _delay.Wet = 0.42f;
        _widener.Width = 0.75f;
        _saturator.Drive = 2.2f;
        _bitTexture.Bits = 4;
        _bitTexture.DownsampleFactor = 12f;


        //processor.AddEffect(_saturator);
        processor.AddEffect(_widener);


        _exampleInstance = baseEffect.CreateProcessedInstance(processor, out _processedSamples);


        _melodyInstance = SubtractiveSynthDemo.CreateMelodyDemo(
            processor,
            SubtractiveSynthPresets.PulseEnsemble,
            rootFrequency: 220f,
            melody: DemoMelody,
            noteGain: 0.65f,
            sampleRate: _sampleRate,
            autoPlay: true);
    }

    public override void PostUpdateEverything()
    {

        if (_exampleInstance == null || _processedSamples.Length == 0)
            return;

        float deltaTime = Main.frameRate <= 0 ? 1f / 60f : 1f / Main.frameRate;
        _timeAccumulator += deltaTime;
        /*
        if (_timeAccumulator >= TriggerIntervalSeconds && _exampleInstance.PendingBufferCount < 2)
        {
            _timeAccumulator = 0f;
            _exampleInstance.SubmitFloatBufferEXT(_processedSamples);
        }
        */

        _lowPassMaster.Cutoff = 10000f;
        _highPassMaster.Cutoff = 500f + (Main.player[Main.myPlayer].velocity.Length() * 1000f);
        _masterAmplifier.Gain = 7.2f;

        if (_exampleInstance.State != SoundState.Playing)
            _exampleInstance = SubtractiveSynthDemo.CreateTwelveTetDemo(
                TestDSP.Processor,
                SubtractiveSynthPresets.SquareLead,
                rootFrequency: 110f,
                noteDuration: 0.5f,
                sampleRate: _sampleRate,
                autoPlay: true);

        if (_melodyInstance != null && _melodyInstance.State != SoundState.Playing)
            _melodyInstance = SubtractiveSynthDemo.CreateMelodyDemo(
                TestDSP.Processor,
                SubtractiveSynthPresets.PWMPad,
                rootFrequency: 220f,
                melody: DemoMelody,
                noteGain: 0.65f,
                sampleRate: _sampleRate,
                autoPlay: true);
    }
    
#region The Dredge
    public override void PreUpdateEntities()
    {
        return; // TODO: Live modify ASoundEffectBasedAudioTrack, see if you can modify CueAudioTrack maybe in the far future
        var audioSystem = Main.audioSystem as LegacyAudioSystem;
        foreach (var audioTrack in audioSystem!.AudioTracks)
        {

            if (audioTrack is ASoundEffectBasedAudioTrack track)
            {
                unsafe
                {
                    FAudioVoice* handle = (FAudioVoice*)track._soundEffectInstance.handle;
                    if (handle == null)
                        continue;

                        handle = null;
                }
            }

            #region  Work In Progress
            if (audioTrack is CueAudioTrack soundTrack)
            {
                if (!soundTrack.IsPlaying || soundTrack.IsStopped)
                    continue;
                var handle = soundTrack._cue.handle;

                unsafe
                {
                    try
                    {
                        Native_FACTCue* nativeCue = (Native_FACTCue*)handle;
                        if (nativeCue == null || nativeCue->playingSound == null)
                            continue;

                        var tracks = nativeCue->playingSound->tracks;
                        if (tracks == null)
                            continue;

                        var wave = tracks->activeWave.wave;
                        if (wave == null || wave->streamCache == null || wave->streamSize == 0)
                            continue;

                        int byteCount = (int)wave->streamSize;
                        var buffer = ArrayPool<byte>.Shared.Rent(byteCount);

                        return;
                        try
                        {
                            Marshal.Copy((IntPtr)wave->streamCache, buffer, 0, byteCount);
                            int sampleCount = byteCount / sizeof(short);
                            if (sampleCount <= 0)
                                continue;

                            var shortSpan = MemoryMarshal.Cast<byte, short>(buffer.AsSpan(0, sampleCount * sizeof(short)));
                            var floatSamples = ArrayPool<float>.Shared.Rent(sampleCount);
                            try
                            {
                                for (int i = 0; i < sampleCount; i++)
                                    if (i < shortSpan.Length)
                                        floatSamples[i] = MathHelper.Clamp(shortSpan[i] / 32768f, -1f, 1f);

                                int channels = (int)(nativeCue->srcChannels != 0 ? nativeCue->srcChannels : (uint)Math.Max(1, _channelCount));
                                if (channels <= 0)
                                    channels = 1;

                                TestDSP.Processor.SetSampleRate(Math.Max(_sampleRate, 1000f));
                                TestDSP.Processor.ProcessBuffer(floatSamples, channels);

                                for (int i = 0; i < sampleCount; i++)
                                    if (i < shortSpan.Length)
                                        shortSpan[i] = (short)Math.Clamp(MathF.Round(floatSamples[i] * 32767f), short.MinValue, short.MaxValue);

                                Marshal.Copy(buffer, 0, (IntPtr)wave->streamCache, sampleCount * sizeof(short));
                            }
                            finally
                            {
                                ArrayPool<float>.Shared.Return(floatSamples);
                            }
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(buffer);
                        }
                    }
                    catch
                    {
                        throw;
                    }
                }
            }
            #endregion
        }

        base.PreUpdateEntities();
    }
}


[StructLayout(LayoutKind.Sequential)]
unsafe struct Native_FACTCue
{
    void* parentBank;
    void* next;
    byte managed;
    ushort index;
    byte notifyOnDestroy;
    void* usercontext;

    /* Sound data */
    void* data;
    void* union;


    /* Instance data */
    float* variableValues;
    float interactive;

    /* Playback */
    uint state;
    Native_FactWave* simpleWave;
    public FACTSoundInstance* playingSound;

    byte active3D;
    public uint srcChannels;
    public uint dstChannels;
}
[StructLayout(LayoutKind.Explicit)]
unsafe struct FAUDIONAMELESSDeity
{
    [FieldOffset(0)]
    void* variation;
    [FieldOffset(0)]
    void* sound;
}
[StructLayout(LayoutKind.Sequential)]
unsafe struct Native_FactWave
{
    /* Engine references */
    void* parentBank;
    void* parentCue;
    ushort index;
    byte notifyOnDestroy;
    void* usercontext;
    /* Playback */
    void* state;
    float volume;
    ushort pitch;
    byte loopCount;
    /* Stream data */
    public uint streamSize;
    public uint streamOffset;
    public byte* streamCache;
    /* FAudio references */
    ushort srcChannels;
    public FAudioVoice* voice;
}


[StructLayout(LayoutKind.Sequential)]
unsafe struct FACTSoundInstance
{
    /* Base Sound reference */
    void* sound;

    /* Per-instance track information */
    public FACTTrackInstance* tracks;

    /* RPC instance data */
    void* rpcData;

    /* Fade data */
    uint fadeStart;
    ushort fadeTarget;
    byte fadeType; /* In (1), Out (2), Release RPC (3) */

    /* Engine references */
    void* parentCue;
}

[StructLayout(LayoutKind.Sequential)]
unsafe struct FACTTrackInstance
{
    /* Tracks which events have fired */
    void* events;

    /* RPC instance data */
    FACTInstanceRPCData rpcData;

    /* SetPitch/SetVolume data */
    float evtPitch;
    float evtVolume;

    /* Wave playback */
    public WaveHandle activeWave, upcomingWave;
    void* waveEvt;
    void* waveEvtInst;
}

[StructLayout(LayoutKind.Sequential)]
unsafe struct WaveHandle
{
    public Native_FactWave* wave;
    float baseVolume;
    ushort basePitch;
    float baseQFactor;
    float baseFrequency;
}
[StructLayout(LayoutKind.Sequential)]
struct FACTInstanceRPCData
{
    float rpcVolume;
    float rpcPitch;
    float rpcReverbSend;
    float rpcFilterFreq;
    float rpcFilterQFactor;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct FAudioVoice
{
    public void* audio;
    public uint flags;
    public FAudioVoiceType type;
    public FAudioVoiceSends sends;
    public float** sendCoefficients;
    public float** mixCoefficients;
    public void* sendMix;
    public void* sendFilter;
    public void** sendFilterState;
    public AudioEffectsStruct effects;
    public FAudioFilterParametersEXT filter;
    public void* filterState;
    public void* sendLock;
    public void* effectLock;
    public void* filterLock;
    public float volume;
    public float* channelVolume;
    public uint outputChannels;
    public void* volumeLock;
    public FAUDIONAMELESSDeityTwo union;
}


public enum FAudioVoiceType : uint
{
    FAUDIO_VOICE_SOURCE,
    FAUDIO_VOICE_SUBMIX,
    FAUDIO_VOICE_MASTER
};

[StructLayout(LayoutKind.Sequential)]
public unsafe struct FAudioVoiceSends
{
    uint SendCount;
    void* pSends;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct AudioEffectsStruct
{
    FAPOBufferFlags state;
    uint count;
    void* desc;
    void** parameters;
    uint* parameterSizes;
    byte* parameterUpdates;
    byte* inPlaceProcessing;
}
[StructLayout(LayoutKind.Sequential)]
public unsafe struct MixStruct
{
    /* Sample storage */
    uint inputSamples;
    uint outputSamples;
    float* inputCache;
    ulong resampleStep;
    void* resample;

    /* Read-only */
    uint inputChannels;
    uint inputSampleRate;
    uint processingStage;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct SourceStruct
{
    /* Sample storage */
    uint decodeSamples;
    uint resampleSamples;

    /* Resampler */
    float resampleFreq;
    ulong resampleStep;
    ulong resampleOffset;
    ulong curBufferOffsetDec;
    uint curBufferOffset;

    /* WMA decoding */
    void* wmadec; // void struct FAudioWMADEC *, wmadec optional

    /* Read-only */
    float maxFreqRatio;
    FAudioWaveFormatEx* format; // FAudioWaveFormatEx
    void* decode;
    void* resample;
    void* callback; // FAudioVoiceCallback

    /* Dynamic */
    byte active;
    float freqRatio;
    byte newBuffer;
    ulong totalSamples;
    FAudioBufferEntry* bufferList;
    FAudioBufferEntry* flushList;
    void* bufferLock;
}

[StructLayout(LayoutKind.Sequential)]
unsafe struct FAudioWaveFormatEx
{
	ushort wFormatTag;
	ushort nChannels;
	uint nSamplesPerSec;
	uint nAvgBytesPerSec;
	ushort nBlockAlign;
	ushort wBitsPerSample;
	ushort cbSize;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct MasterStruct

{
    /* Output stream, allocated by Platform */
    float* output;

    /* Needed when inputChannels != outputChannels */
    float* effectCache;

    /* Read-only */
    uint inputChannels;
    uint inputSampleRate;
}

[StructLayout(LayoutKind.Explicit)]
public unsafe struct FAUDIONAMELESSDeityTwo
{
    [FieldOffset(0)]
    public SourceStruct src;
    [FieldOffset(0)]
    public MixStruct mix;
    [FieldOffset(0)]
    public MasterStruct master;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct FAudioFilterParametersEXT
{
    FAudioFilterType Type;
    float Frequency;    /* [0, FAUDIO_MAX_FILTER_FREQUENCY] */
    float OneOverQ;     /* [0, FAUDIO_MAX_FILTER_ONEOVERQ] */
    float WetDryMix;	/* [0, 1] */
}

enum FAudioFilterType
{
    FAudioLowPassFilter,
    FAudioBandPassFilter,
    FAudioHighPassFilter,
    FAudioNotchFilter
};

enum FAPOBufferFlags
{
    FAPO_BUFFER_SILENT,
    FAPO_BUFFER_VALID
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct FAudioBufferEntry
{
	public FAudio.FAudioBuffer buffer;
	public FAudio.FAudioBufferWMA bufferWMA;
	public FAudioBufferEntry *next;
};

[StructLayout(LayoutKind.Sequential)]
unsafe struct FAudioBuffer
{
	/* Either 0 or FAUDIO_END_OF_STREAM */
	uint Flags;
	/* Pointer to wave data, memory block size.
	 * Note that pAudioData is not copied; FAudio reads directly from your
	 * pointer! This pointer must be valid until FAudio has finished using
	 * it, at which point an OnBufferEnd callback will be generated.
	 */
	uint AudioBytes;
	byte* pAudioData;
	/* Play region, in sample frames. */
	uint PlayBegin;
	uint PlayLength;
	/* Loop region, in sample frames.
	 * This can be used to loop a subregion of the wave instead of looping
	 * the whole thing, i.e. if you have an intro/outro you can set these
	 * to loop the middle sections instead. If you don't need this, set both
	 * values to 0.
	 */
	uint LoopBegin;
	uint LoopLength;
	/* [0, FAUDIO_LOOP_INFINITE] */
	uint LoopCount;
	/* This is sent to callbacks as pBufferContext */
	void* pContext;
}

[StructLayout(LayoutKind.Sequential)]
unsafe struct FAudioBufferWMA
{
	uint* pDecodedPacketCumulativeBytes;
	uint PacketCount;
}

#endregion