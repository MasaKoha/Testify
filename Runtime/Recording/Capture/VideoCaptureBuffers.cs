#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace UniTestify
{
    /// <summary>GPU テクスチャと永続バッファを所有します。返却は書込側、最終解放の指示は録画入口が担います。</summary>
    internal sealed class VideoCaptureBuffers
    {
        private const int BufferPoolSize = 4;
        private const int BytesPerPixel = 4;
        private const int RenderTextureDepth = 0;
        private const int FirstMipIndex = 0;
        private NativeArray<byte>[] _buffers;
        private RenderTexture _renderTexture;
        private GraphicsFormat _graphicsFormat;
        private readonly Queue<int> _availableBufferIndexes = new Queue<int>();
        private readonly object _bufferPoolLock = new object();

        /// <summary>読み戻し元テクスチャの画素形式です。</summary>
        internal GraphicsFormat GraphicsFormat => _graphicsFormat;

        /// <summary>読み戻し元とバッファプールを確保します。解放は全書込の完了後に限ります。</summary>
        internal void CreateCaptureResources(int width, int height)
        {
            var readWrite = QualitySettings.activeColorSpace == ColorSpace.Linear
                ? RenderTextureReadWrite.sRGB
                : RenderTextureReadWrite.Default;
            _renderTexture = new RenderTexture(width, height, RenderTextureDepth, RenderTextureFormat.ARGB32, readWrite);
            _renderTexture.Create();
            _graphicsFormat = _renderTexture.graphicsFormat;

            var bufferLength = width * height * BytesPerPixel;
            _buffers = new NativeArray<byte>[BufferPoolSize];
            for (var bufferIndex = 0; bufferIndex < _buffers.Length; bufferIndex++)
            {
                _buffers[bufferIndex] = new NativeArray<byte>(bufferLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                _availableBufferIndexes.Enqueue(bufferIndex);
            }
        }

        /// <summary>空きバッファの所有権を読み戻しと書込へ貸し出します。</summary>
        internal bool TryTakeAvailableBuffer(out int bufferIndex)
        {
            lock (_bufferPoolLock)
            {
                if (_availableBufferIndexes.Count == 0)
                {
                    bufferIndex = -1;
                    return false;
                }

                bufferIndex = _availableBufferIndexes.Dequeue();
                return true;
            }
        }

        /// <summary>借用中のバッファを GPU 読み戻し先に指定します。</summary>
        internal void RequestReadback(int bufferIndex, Action<AsyncGPUReadbackRequest> onCompleted)
        {
            ScreenCapture.CaptureScreenshotIntoRenderTexture(_renderTexture);
            AsyncGPUReadback.RequestIntoNativeArray(ref _buffers[bufferIndex], _renderTexture, FirstMipIndex, onCompleted);
        }

        /// <summary>借用中のバッファを返します。書込側で永続領域を破棄してはいけません。</summary>
        internal NativeArray<byte> GetBuffer(int bufferIndex)
        {
            return _buffers[bufferIndex];
        }

        /// <summary>読み戻し失敗または書込完了後に借用中のバッファをプールへ戻します。</summary>
        internal void ReturnBuffer(int bufferIndex)
        {
            lock (_bufferPoolLock)
            {
                _availableBufferIndexes.Enqueue(bufferIndex);
            }
        }

        /// <summary>録画入口が GPU と書込の完了を待った後、テクスチャと永続バッファを解放します。</summary>
        internal void ReleaseCaptureResources()
        {
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                UnityEngine.Object.Destroy(_renderTexture);
                _renderTexture = null;
            }

            if (_buffers == null)
            {
                return;
            }

            for (var bufferIndex = 0; bufferIndex < _buffers.Length; bufferIndex++)
            {
                if (_buffers[bufferIndex].IsCreated)
                {
                    _buffers[bufferIndex].Dispose();
                }
            }

            _buffers = null;
            lock (_bufferPoolLock)
            {
                _availableBufferIndexes.Clear();
            }
        }
    }
}
#endif
