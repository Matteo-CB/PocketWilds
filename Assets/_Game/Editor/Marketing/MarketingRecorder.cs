using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PocketWilds.Editor.Marketing
{
    /// <summary>
    /// Records the refuge camera for a marketing video. The camera renders into a supersampled target (the output size
    /// times 1, 2 or 4, with the pipeline's MSAA) in the normal frame, as on a phone in portrait: aspect, culling and the
    /// streaming footprint follow the 9:16 target, not the editor's Game view. Each kept frame is box filtered down to the
    /// output size, read back asynchronously and encoded to PNG on worker threads (a few in flight, the main thread waits
    /// when they fall behind). The game's audio is rendered offline in step with the frames (AudioRenderer) into a 32-bit
    /// float WAV. Editor tool: allocations per frame are fine here.
    /// </summary>
    internal sealed class MarketingRecorder : IDisposable
    {
        private const int MaxReadbacks = 4;

        private readonly int _width;
        private readonly int _height;
        private readonly string _framesDirectory;
        private readonly bool _flipY;
        private readonly RenderTexture _target;
        private readonly List<RenderTexture> _chain = new List<RenderTexture>();
        private readonly Queue<(AsyncGPUReadbackRequest request, int index)> _readbacks = new Queue<(AsyncGPUReadbackRequest, int)>();
        private readonly int _encoderCount;
        private readonly SemaphoreSlim _encoders;
        private readonly ConcurrentBag<byte[]> _rgbaPool = new ConcurrentBag<byte[]>();
        private readonly ConcurrentBag<byte[]> _rgbPool = new ConcurrentBag<byte[]>();
        private Camera _camera;
        private RenderTexture _previousTarget;
        private float _previousNear;
        private volatile string _error;
        private int _encoded;

        private WavFloatWriter _wav;
        private int _channels;
        private bool _audioStarted;

        public MarketingRecorder(string framesDirectory, int width, int height, int supersample, bool flipY)
        {
            _framesDirectory = framesDirectory;
            _width = width;
            _height = height;
            _flipY = flipY;
            int msaa = 1;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                msaa = Mathf.Max(1, urp.msaaSampleCount);
            var descriptor = new RenderTextureDescriptor(width * supersample, height * supersample, GraphicsFormat.R8G8B8A8_SRGB, 24)
            {
                msaaSamples = msaa,
                useMipMap = false,
                autoGenerateMips = false,
            };
            _target = new RenderTexture(descriptor) { name = "MarketingCaptureTarget", filterMode = FilterMode.Bilinear };
            _target.Create();
            // Box filter by halving: each 2:1 bilinear tap averages four texels (in linear space, the targets are sRGB).
            for (int scale = supersample / 2; scale >= 1; scale /= 2)
            {
                var rt = new RenderTexture(width * scale, height * scale, 0, GraphicsFormat.R8G8B8A8_SRGB)
                {
                    name = "MarketingCaptureDown" + scale,
                    filterMode = FilterMode.Bilinear,
                };
                rt.Create();
                _chain.Add(rt);
                if (scale == 1)
                    break;
            }
            if (_chain.Count == 0)
            {
                var rt = new RenderTexture(width, height, 0, GraphicsFormat.R8G8B8A8_SRGB) { name = "MarketingCaptureOut" };
                rt.Create();
                _chain.Add(rt);
            }
            _encoderCount = Mathf.Clamp(SystemInfo.processorCount - 2, 2, 8);
            _encoders = new SemaphoreSlim(_encoderCount, _encoderCount);
            Directory.CreateDirectory(framesDirectory);
        }

        public string Error => _error;
        public int Encoded => Volatile.Read(ref _encoded);
        public int AudioChannels => _channels;
        public int AudioSampleRate { get; private set; }
        public long AudioFrames => _wav != null ? _wav.SampleFrames : 0;
        public int MsaaSamples => _target.antiAliasing;

        /// <summary>The camera renders into the capture target from now on (its near plane a little closer for close-ups).</summary>
        public void Attach(Camera camera)
        {
            _camera = camera;
            _previousTarget = camera.targetTexture;
            _previousNear = camera.nearClipPlane;
            camera.nearClipPlane = Mathf.Min(camera.nearClipPlane, 0.08f);
            camera.targetTexture = _target;
        }

        public void Detach()
        {
            if (_camera == null)
                return;
            _camera.targetTexture = _previousTarget;
            _camera.nearClipPlane = _previousNear;
            _camera = null;
        }

        /// <summary>After the frame rendered: down to the output size, then a readback for frame <paramref name="index"/>.</summary>
        public void CaptureFrame(int index)
        {
            Texture source = _target;
            foreach (var rt in _chain)
            {
                Graphics.Blit(source, rt);
                source = rt;
            }
            var output = _chain[_chain.Count - 1];
            if (SystemInfo.supportsAsyncGPUReadback)
            {
                _readbacks.Enqueue((AsyncGPUReadback.Request(output, 0, TextureFormat.RGBA32), index));
                while (_readbacks.Count > MaxReadbacks)
                    CompleteOldest(true);
                Poll();
            }
            else
            {
                CaptureSync(output, index);
            }
        }

        /// <summary>Hands finished readbacks to the encoders (call once a frame).</summary>
        public void Poll()
        {
            while (_readbacks.Count > 0 && _readbacks.Peek().request.done)
                CompleteOldest(false);
        }

        /// <summary>Every readback and every PNG written (waits up to <paramref name="timeoutSeconds"/> for the encoders).</summary>
        public bool Drain(float timeoutSeconds)
        {
            while (_readbacks.Count > 0)
                CompleteOldest(true);
            var until = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            int taken = 0;
            while (taken < _encoderCount && DateTime.UtcNow < until)
            {
                if (_encoders.Wait(100))
                    taken++;
            }
            if (taken > 0)
                _encoders.Release(taken);
            return taken == _encoderCount;
        }

        private void CompleteOldest(bool wait)
        {
            var (request, index) = _readbacks.Dequeue();
            if (wait)
                request.WaitForCompletion();
            if (request.hasError)
            {
                _error = "GPU readback failed at frame " + index;
                return;
            }
            var data = request.GetData<byte>();
            var rgba = Rent(_rgbaPool, data.Length);
            data.CopyTo(rgba);
            _encoders.Wait();
            ThreadPool.QueueUserWorkItem(_ => Encode(rgba, index));
        }

        private void CaptureSync(RenderTexture output, int index)
        {
            var previous = RenderTexture.active;
            var texture = new Texture2D(_width, _height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = output;
                texture.ReadPixels(new Rect(0, 0, _width, _height), 0, 0);
                texture.Apply();
                var rgba = Rent(_rgbaPool, _width * _height * 4);
                texture.GetRawTextureData<byte>().CopyTo(rgba);
                _encoders.Wait();
                ThreadPool.QueueUserWorkItem(_ => Encode(rgba, index));
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>Worker thread: RGBA to RGB (the PNG carries no alpha), encode, write.</summary>
        private void Encode(byte[] rgba, int index)
        {
            byte[] rgb = null;
            try
            {
                rgb = Rent(_rgbPool, _width * _height * 3);
                int rowIn = _width * 4;
                int rowOut = _width * 3;
                for (int y = 0; y < _height; y++)
                {
                    int src = (_flipY ? _height - 1 - y : y) * rowIn;
                    int dst = y * rowOut;
                    for (int x = 0; x < _width; x++)
                    {
                        rgb[dst] = rgba[src];
                        rgb[dst + 1] = rgba[src + 1];
                        rgb[dst + 2] = rgba[src + 2];
                        dst += 3;
                        src += 4;
                    }
                }
                byte[] png = ImageConversion.EncodeArrayToPNG(rgb, GraphicsFormat.R8G8B8_SRGB, (uint)_width, (uint)_height);
                File.WriteAllBytes(Path.Combine(_framesDirectory, index.ToString("D6") + ".png"), png);
                Interlocked.Increment(ref _encoded);
            }
            catch (Exception ex)
            {
                _error = $"frame {index}: {ex.Message}";
            }
            finally
            {
                _rgbaPool.Add(rgba);
                if (rgb != null)
                    _rgbPool.Add(rgb);
                _encoders.Release();
            }
        }

        private static byte[] Rent(ConcurrentBag<byte[]> pool, int length)
        {
            while (pool.TryTake(out var buffer))
                if (buffer.Length == length)
                    return buffer;
            return new byte[length];
        }

        // ---------------- Audio ----------------

        /// <summary>
        /// Offline audio from now on: the mixer runs only when <see cref="RenderAudio"/> asks, one capture frame at a time
        /// (Time.captureFramerate must be set first). The speakers stay silent meanwhile.
        /// </summary>
        public bool StartAudio(string wavPath)
        {
            _channels = ChannelsOf(AudioSettings.speakerMode);
            AudioSampleRate = AudioSettings.outputSampleRate;
            if (!AudioRenderer.Start())
                return false;
            _audioStarted = true;
            _wav = new WavFloatWriter(wavPath, _channels, AudioSampleRate);
            return true;
        }

        /// <summary>Mixes this frame's audio; kept in the WAV only for recorded frames (the cuts of the edit).</summary>
        public void RenderAudio(bool keep)
        {
            if (!_audioStarted)
                return;
            int frames = AudioRenderer.GetSampleCountForCaptureFrame();
            if (frames <= 0)
                return;
            var buffer = new NativeArray<float>(frames * _channels, Allocator.Temp);
            try
            {
                AudioRenderer.Render(buffer);
                if (keep && _wav != null)
                    _wav.Write(buffer.ToArray());
            }
            finally
            {
                buffer.Dispose();
            }
        }

        public void StopAudio()
        {
            if (_audioStarted)
            {
                _audioStarted = false;
                AudioRenderer.Stop();
            }
            _wav?.Dispose();
            _wav = null;
        }

        private static int ChannelsOf(AudioSpeakerMode mode)
        {
            switch (mode)
            {
                case AudioSpeakerMode.Mono: return 1;
                case AudioSpeakerMode.Quad: return 4;
                case AudioSpeakerMode.Surround: return 5;
                case AudioSpeakerMode.Mode5point1: return 6;
                case AudioSpeakerMode.Mode7point1: return 8;
                default: return 2;
            }
        }

        public void Dispose()
        {
            StopAudio();
            Detach();
            _target.Release();
            UnityEngine.Object.DestroyImmediate(_target);
            foreach (var rt in _chain)
            {
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
            _chain.Clear();
        }
    }

    /// <summary>A WAV of 32-bit float samples (format 3), interleaved; the sizes are written on Dispose.</summary>
    internal sealed class WavFloatWriter : IDisposable
    {
        private readonly FileStream _stream;
        private readonly BinaryWriter _writer;
        private readonly int _channels;
        private long _dataBytes;
        private byte[] _bytes = Array.Empty<byte>();

        public WavFloatWriter(string path, int channels, int sampleRate)
        {
            _channels = channels;
            _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            _writer = new BinaryWriter(_stream);
            _writer.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            _writer.Write(0);
            _writer.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            _writer.Write(16);
            _writer.Write((short)3);
            _writer.Write((short)channels);
            _writer.Write(sampleRate);
            _writer.Write(sampleRate * channels * 4);
            _writer.Write((short)(channels * 4));
            _writer.Write((short)32);
            _writer.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            _writer.Write(0);
        }

        public long SampleFrames => _dataBytes / (4L * _channels);

        public void Write(float[] samples)
        {
            int bytes = samples.Length * 4;
            if (_bytes.Length < bytes)
                _bytes = new byte[bytes];
            Buffer.BlockCopy(samples, 0, _bytes, 0, bytes);
            _writer.Write(_bytes, 0, bytes);
            _dataBytes += bytes;
        }

        public void Dispose()
        {
            _writer.Flush();
            _stream.Seek(4, SeekOrigin.Begin);
            _writer.Write((int)(36 + _dataBytes));
            _stream.Seek(40, SeekOrigin.Begin);
            _writer.Write((int)_dataBytes);
            _writer.Flush();
            _writer.Dispose();
        }
    }
}
