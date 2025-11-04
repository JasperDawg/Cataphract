using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using ReLogic.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

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
        for (int i = 0; i < samples.Length; i++)
            samples[i] = MathF.Tanh(samples[i] * _drive);
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

public static class FactDspContext
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
            for (int i = 0; i < sampleCount; i++)
                processedSamples[i] = MathHelper.Clamp(pcm[i] / 32768f, -1f, 1f);

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
            FactDspContext.Processor.SetSampleRate(sampleRate);
            FactDspContext.Processor.ProcessBuffer(managed, channelCount);
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
    private readonly SoftClipSaturator _saturator = new() { Drive = 1.18f };
    private readonly BitCrusherEffect _bitTexture = new() { Bits = 14, DownsampleFactor = 1f };
    private readonly LowPassOnePoleFilter _lowPassMaster = new() { Cutoff = 20000f };
    private readonly HighPassOnePoleFilter _highPassMaster = new() { Cutoff = 200f };
    private readonly BandPassBiquadFilter _masterBand = new() { Frequency = 1000f, Q = 1f };

    private DynamicSoundEffectInstance? _exampleInstance;
    private float _timeAccumulator;
    private float _sampleRate = SampleRateDefault;
    private int _channelCount = ChannelCountDefault;
    private float[] _processedSamples = Array.Empty<float>();

    private const float SampleRateDefault = 44100f;
    private const int ChannelCountDefault = 2;
    private const float TriggerIntervalSeconds = 2.75f;

    public override void OnModLoad()
    {

        var baseEffect = Assets.Audio.Misc.StupidGun_HeavyFire.Asset.GetSoundEffect();
        _sampleRate = baseEffect.sampleRate;
        _channelCount = baseEffect.channels == (ushort)AudioChannels.Stereo ? 2 : 1;

        var processor = FactDspContext.Processor;
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


        processor.AddEffect(_motionBand);
        processor.AddEffect(_tremolo);
        processor.AddEffect(_delay);
        processor.AddEffect(_widener);
        processor.AddEffect(_masterBand);


        processor.AddEffect(_saturator);


        processor.AddEffect(_lowPassMaster);
        processor.AddEffect(_highPassMaster);



        _exampleInstance = baseEffect.CreateProcessedInstance(processor, out _processedSamples);
    }

    public override void PostUpdateEverything()
    {
        if (_exampleInstance == null || _processedSamples.Length == 0)
            return;

        float deltaTime = Main.frameRate <= 0 ? 1f / 60f : 1f / Main.frameRate;
        _timeAccumulator += deltaTime;

        if (_timeAccumulator >= TriggerIntervalSeconds && _exampleInstance.PendingBufferCount < 2)
        {
            _timeAccumulator = 0f;
            _exampleInstance.SubmitFloatBufferEXT(_processedSamples);
        }

        _lowPassMaster.Cutoff = 10000f;
        _highPassMaster.Cutoff = 500f + (Main.player[Main.myPlayer].velocity.Length() * 1000f);
        foreach (SlotVector<ActiveSound>.ItemPair item in (IEnumerable<SlotVector<ActiveSound>.ItemPair>)SoundEngine.SoundPlayer._trackedSounds)
        {
            ActiveSound value = item.Value;
            if (value.Sound == null || value.Sound.parentEffect == null)
                continue;
            value.Sound = value.Sound?.parentEffect.CreateProcessedInstance(FactDspContext.Processor, out _, true);


        }
    }

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

                                FactDspContext.Processor.SetSampleRate(Math.Max(_sampleRate, 1000f));
                                FactDspContext.Processor.ProcessBuffer(floatSamples, channels);

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