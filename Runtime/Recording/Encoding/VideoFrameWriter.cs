#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;

namespace UniTestify
{
    /// <summary>借用した画素のエンコードと非同期書込を所有し、一時配列の破棄後に永続バッファを返却します。</summary>
    internal sealed class VideoFrameWriter
    {
        private const int JpegQuality = 90;
        private const int BytesPerPixel = 4;
        private const int NoRowBytes = 0;
        // AsyncGPUReadback は左下原点のため、JPG へ書く前に行を反転する。
        private const bool FlipVerticallyBeforeEncode = true;
        private readonly List<Task> _encodingTasks = new List<Task>();
        private readonly object _encodingTaskLock = new object();

        private VideoCaptureBuffers _captureBuffers;
        private VideoCaptureGeometry _geometry;
        private VideoRecordingArtifacts _artifacts;

        /// <summary>録画中を通して生存する借用元と出力履歴を結び付けます。</summary>
        internal void Initialize(VideoCaptureBuffers captureBuffers, VideoCaptureGeometry geometry, VideoRecordingArtifacts artifacts)
        {
            _captureBuffers = captureBuffers;
            _geometry = geometry;
            _artifacts = artifacts;
        }

        /// <summary>読み戻しが成功したバッファの書込を予約します。</summary>
        internal void Enqueue(int bufferIndex, string frameFilePath, int frameIndex)
        {
            var task = Task.Run(() => EncodeAndWriteFrame(bufferIndex, frameFilePath, frameIndex));
            lock (_encodingTaskLock)
            {
                _encodingTasks.Add(task);
            }
        }

        private void EncodeAndWriteFrame(int bufferIndex, string frameFilePath, int frameIndex)
        {
            NativeArray<byte> encodedBytes = default;
            NativeArray<byte> croppedBuffer = default;
            NativeArray<byte> flippedBuffer = default;
            try
            {
                var sourceBuffer = _captureBuffers.GetBuffer(bufferIndex);
                if (_geometry.RequiresCropping())
                {
                    croppedBuffer = CreateCroppedBuffer(sourceBuffer, _geometry.SourceWidth, _geometry.CropRect);
                    sourceBuffer = croppedBuffer;
                }

                if (FlipVerticallyBeforeEncode)
                {
                    flippedBuffer = CreateVerticallyFlippedBuffer(sourceBuffer, _geometry.Width, _geometry.Height);
                    sourceBuffer = flippedBuffer;
                }

                encodedBytes = ImageConversion.EncodeNativeArrayToJPG(sourceBuffer, _captureBuffers.GraphicsFormat, (uint)_geometry.Width, (uint)_geometry.Height, NoRowBytes, JpegQuality);
                File.WriteAllBytes(frameFilePath, encodedBytes.ToArray());
            }
            catch (Exception exception)
            {
                _artifacts.MarkFrameFailed(frameIndex);
                UnityEngine.Debug.LogWarning($"[VideoRecorder] フレームの書き出しに失敗しました。 frame={frameIndex} {exception.GetType().Name}: {exception.Message}");
            }
            finally
            {
                if (encodedBytes.IsCreated)
                {
                    encodedBytes.Dispose();
                }

                if (flippedBuffer.IsCreated)
                {
                    flippedBuffer.Dispose();
                }

                if (croppedBuffer.IsCreated)
                {
                    croppedBuffer.Dispose();
                }

                _captureBuffers.ReturnBuffer(bufferIndex);
            }
        }

        private static NativeArray<byte> CreateCroppedBuffer(NativeArray<byte> sourceBuffer, int sourceWidth, RectInt cropRect)
        {
            var rowByteCount = cropRect.width * BytesPerPixel;
            var croppedBuffer = new NativeArray<byte>(cropRect.width * cropRect.height * BytesPerPixel, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            for (var rowIndex = 0; rowIndex < cropRect.height; rowIndex++)
            {
                var sourceOffset = ((cropRect.y + rowIndex) * sourceWidth * BytesPerPixel) + (cropRect.x * BytesPerPixel);
                var destinationOffset = rowIndex * rowByteCount;
                NativeArray<byte>.Copy(sourceBuffer, sourceOffset, croppedBuffer, destinationOffset, rowByteCount);
            }

            return croppedBuffer;
        }

        private static NativeArray<byte> CreateVerticallyFlippedBuffer(NativeArray<byte> sourceBuffer, int width, int height)
        {
            var rowByteCount = width * BytesPerPixel;
            var flippedBuffer = new NativeArray<byte>(sourceBuffer.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            for (var rowIndex = 0; rowIndex < height; rowIndex++)
            {
                var sourceOffset = rowIndex * rowByteCount;
                var destinationOffset = (height - rowIndex - 1) * rowByteCount;
                NativeArray<byte>.Copy(sourceBuffer, sourceOffset, flippedBuffer, destinationOffset, rowByteCount);
            }

            return flippedBuffer;
        }

        /// <summary>GPU 読み戻し完了後に全書込を待ち、バッファの最終解放を可能にします。</summary>
        internal void WaitForEncodingTasks()
        {
            Task[] encodingTasks;
            lock (_encodingTaskLock)
            {
                encodingTasks = _encodingTasks.ToArray();
            }

            if (encodingTasks.Length == 0)
            {
                return;
            }

            try
            {
                Task.WaitAll(encodingTasks);
            }
            catch (AggregateException exception)
            {
                UnityEngine.Debug.LogError($"[VideoRecorder] エンコードまたは書き出しに失敗しました。 {exception.Flatten()}");
            }
        }
    }
}
#endif
