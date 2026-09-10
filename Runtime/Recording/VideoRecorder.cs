#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace UniTestify
{
    /// <summary>連番 JPG 録画の時間進行と停止時の待機・解放順序を統括する使い捨てコンポーネントです。</summary>
    public sealed class VideoRecorder : MonoBehaviour
    {
        /// <summary>録画結果に添える manifest のファイル名。呼び出し側が参照するために公開する。</summary>
        public const string ManifestFileName = "recording-manifest.json";

        /// <summary>ffmpeg の concat デマルチプレクサへ渡すフレーム一覧のファイル名。</summary>
        public const string FrameListFileName = "frames.txt";

        private const int DefaultFramesPerSecond = 30;
        private const double CaptureIntervalTolerance = 0.9;
        private static readonly WaitForEndOfFrame WaitForEndOfFrameYieldInstruction = new WaitForEndOfFrame();
        private Coroutine _captureCoroutine;
        private double _targetFrameInterval;
        private double _recordingStartRealtime;
        private double _lastCaptureRealtime;
        private bool _isRecording;
        private bool _hasReleasedResources;
        private int _failedReadbackCount;

        private readonly VideoCaptureGeometry _geometry = new VideoCaptureGeometry();
        private readonly VideoCaptureBuffers _captureBuffers = new VideoCaptureBuffers();
        private readonly VideoFrameWriter _frameWriter = new VideoFrameWriter();
        private readonly VideoRecordingArtifacts _artifacts = new VideoRecordingArtifacts();
        private readonly VideoRecordingEnvironment _environment = new VideoRecordingEnvironment();

        /// <summary>
        /// 録画中かどうかを取得します。
        /// </summary>
        public bool IsRecording
        {
            get
            {
                return _isRecording;
            }
        }

        /// <summary>
        /// フォレンジックとシナリオ結果が動画上の位置へ辿れるよう、録画中の現在フレームを公開します。
        /// </summary>
        public int FrameCount
        {
            get
            {
                return _artifacts.FrameCount;
            }
        }

        /// <summary>
        /// 合否 JSON が録画負荷の破綻を画像確認なしで読めるようにします。
        /// </summary>
        public int DroppedFrameCount
        {
            get
            {
                return _artifacts.DroppedFrameCount;
            }
        }

        /// <summary>
        /// 録画を開始します。
        /// </summary>
        public static VideoRecorder StartRecording(string outputDirectory, string name, int framesPerSecond = DefaultFramesPerSecond, bool recordAudio = false)
        {
            return StartRecording(outputDirectory, name, framesPerSecond, recordAudio, true);
        }

        /// <summary>
        /// シナリオから録画中オーバーレイを抑制できるようにし、視覚回帰用の静止画汚染を避けます。
        /// </summary>
        public static VideoRecorder StartRecording(string outputDirectory, string name, int framesPerSecond, bool recordAudio, bool inputOverlayEnabled)
        {
            var recorderObject = new GameObject(nameof(VideoRecorder));
            DontDestroyOnLoad(recorderObject);

            var recorder = recorderObject.AddComponent<VideoRecorder>();
            recorder.Initialize(outputDirectory, name, framesPerSecond, recordAudio, inputOverlayEnabled);
            return recorder;
        }

        /// <summary>
        /// 現在フレームに目印を追加します。
        /// </summary>
        public void AddMarker(string label)
        {
            if (!_isRecording)
            {
                return;
            }

            _artifacts.AddMarker(label, Time.realtimeSinceStartupAsDouble - _recordingStartRealtime);
        }

        /// <summary>
        /// 録画を停止し、manifest を書き出した結果を返します。
        /// </summary>
        public VideoRecordingResult StopRecording()
        {
            if (!_isRecording)
            {
                return _artifacts.BuildResult();
            }

            StopCaptureLoop();
            _artifacts.SetDuration(Time.realtimeSinceStartupAsDouble - _recordingStartRealtime);
            _environment.StopAudioRecording();
            _environment.RestoreFrameRateSettings();
            WaitForPendingWorkAndReleaseResources();
            _environment.HideInputOverlayIfNeeded();

            var result = _artifacts.WriteManifestAndBuildResult(_geometry, _environment.AudioRecorder);
            UnityEngine.Debug.Log($"[VideoRecorder] 完了: frames={result.FrameCount} duration={_artifacts.DurationSeconds:F2}s dropped={_artifacts.DroppedFrameCount} failedReadback={_failedReadbackCount} output={result.OutputDirectory} ffmpeg={result.FfmpegCommand}");
            Destroy(gameObject);
            return result;
        }

        private void OnDestroy()
        {
            if (_isRecording)
            {
                StopCaptureLoop();
            }

            _environment.RestoreFrameRateSettings();
            _environment.StopAudioRecording();
            WaitForPendingWorkAndReleaseResources();
            _environment.HideInputOverlayIfNeeded();
        }

        private void Initialize(string outputDirectory, string name, int framesPerSecond, bool recordAudio, bool inputOverlayEnabled)
        {
            _artifacts.Initialize(outputDirectory ?? string.Empty,
                string.IsNullOrEmpty(name) ? nameof(VideoRecorder) : name,
                framesPerSecond > 0 ? framesPerSecond : DefaultFramesPerSecond, inputOverlayEnabled);
            _environment.Initialize(inputOverlayEnabled);
            _geometry.Initialize();
            _targetFrameInterval = 1.0 / _artifacts.FramesPerSecond;
            _recordingStartRealtime = Time.realtimeSinceStartupAsDouble;
            _lastCaptureRealtime = double.NegativeInfinity;

            Directory.CreateDirectory(_artifacts.OutputDirectory);
            _frameWriter.Initialize(_captureBuffers, _geometry, _artifacts);
            _captureBuffers.CreateCaptureResources(_geometry.SourceWidth, _geometry.SourceHeight);
            _environment.StartAudioRecordingIfNeeded(recordAudio, _artifacts.OutputDirectory);
            _artifacts.SetHasAudio(_environment.HasAudio);
            _environment.OverrideFrameRateSettings(_artifacts.FramesPerSecond);
            _environment.ShowInputOverlayIfNeeded();

            _isRecording = true;
            _captureCoroutine = StartCoroutine(CaptureFramesCoroutine());
        }

        private void StopCaptureLoop()
        {
            _isRecording = false;

            if (_captureCoroutine == null)
            {
                return;
            }

            StopCoroutine(_captureCoroutine);
            _captureCoroutine = null;
        }

        private IEnumerator CaptureFramesCoroutine()
        {
            while (_isRecording)
            {
                yield return WaitForEndOfFrameYieldInstruction;

                _environment.PumpAudioFrame();

                var now = Time.realtimeSinceStartupAsDouble;
                if (now - _lastCaptureRealtime < _targetFrameInterval * CaptureIntervalTolerance)
                {
                    continue;
                }

                _lastCaptureRealtime = now;
                CaptureFrame(now - _recordingStartRealtime);
            }
        }

        private void CaptureFrame(double elapsedSeconds)
        {
            _geometry.WarnIfCaptureGeometryChanged();

            if (!_captureBuffers.TryTakeAvailableBuffer(out var bufferIndex))
            {
                _artifacts.RecordDroppedFrame();
                return;
            }

            var frameIndex = _artifacts.RecordFrame(elapsedSeconds);

            _captureBuffers.RequestReadback(bufferIndex, request =>
            {
                HandleReadbackCompleted(request, bufferIndex, frameIndex);
            });
        }

        private void HandleReadbackCompleted(AsyncGPUReadbackRequest request, int bufferIndex, int frameIndex)
        {
            if (request.hasError)
            {
                _failedReadbackCount++;
                _artifacts.MarkFrameFailed(frameIndex);
                _captureBuffers.ReturnBuffer(bufferIndex);
                UnityEngine.Debug.LogWarning($"[VideoRecorder] GPU 読み戻しに失敗しました。 frame={frameIndex}");
                return;
            }

            var frameFilePath = _artifacts.GetFrameFilePath(frameIndex);
            _frameWriter.Enqueue(bufferIndex, frameFilePath, frameIndex);
        }

        private void WaitForPendingWorkAndReleaseResources()
        {
            if (_hasReleasedResources)
            {
                return;
            }

            // 書込は読み戻し完了時に予約されるため、GPU → 書込 → 所有リソース解放の順序を固定する。
            AsyncGPUReadback.WaitAllRequests();
            _frameWriter.WaitForEncodingTasks();
            _captureBuffers.ReleaseCaptureResources();
            _hasReleasedResources = true;
        }

        /// <summary>連番 JPG を mp4 へ変換する ffmpeg コマンドを組み立てる。変換の実行は呼び出し側が行う。</summary>
        public static string CreateFfmpegCommand(int framesPerSecond, string outputDirectory, string name, double durationSeconds, bool hasAudio = false)
        {
            return VideoRecordingCommand.CreateFfmpegCommand(framesPerSecond, outputDirectory, name, durationSeconds, hasAudio);
        }
    }
}
#endif
