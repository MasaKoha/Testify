#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace UniTestify
{
    /// <summary>フレーム履歴と失敗記録から、manifest・フレーム一覧・録画結果を従来形式で生成します。</summary>
    internal sealed class VideoRecordingArtifacts
    {
        private const string FrameFileNameFormat = "frame-{0:D5}.jpg";
        private const double MinimumFrameDuration = 0.0001;
        private readonly List<VideoRecordingMarker> _markers = new List<VideoRecordingMarker>();
        private readonly HashSet<int> _failedFrameIndexes = new HashSet<int>();
        private readonly object _failedFrameLock = new object();
        private readonly List<double> _frameTimestamps = new List<double>();
        private string _outputDirectory;
        private string _name;
        private string _startedAtRealtime;
        private int _framesPerSecond;
        private int _frameCount;
        private int _droppedFrameCount;
        private double _durationSeconds;
        private bool _hasAudio;
        private bool _inputOverlayEnabled = true;

        /// <summary>出力先は初期化前なら未設定です。</summary>
        internal string OutputDirectory => _outputDirectory;

        /// <summary>録画に採用したフレームレートです。</summary>
        internal int FramesPerSecond => _framesPerSecond;

        /// <summary>読み戻しを予約したフレーム数です。</summary>
        internal int FrameCount => _frameCount;

        /// <summary>空きバッファ不足で取りこぼしたフレーム数です。</summary>
        internal int DroppedFrameCount => _droppedFrameCount;

        /// <summary>停止時までの実測時間です。</summary>
        internal double DurationSeconds => _durationSeconds;

        /// <summary>録画開始時の出力情報を固定します。</summary>
        internal void Initialize(string outputDirectory, string name, int framesPerSecond, bool inputOverlayEnabled)
        {
            _outputDirectory = outputDirectory;
            _name = name;
            _framesPerSecond = framesPerSecond;
            _startedAtRealtime = DateTime.Now.ToString("o");
            _inputOverlayEnabled = inputOverlayEnabled;
        }

        /// <summary>音声開始結果を記録し、停止前の結果取得にも反映します。</summary>
        internal void SetHasAudio(bool hasAudio)
        {
            _hasAudio = hasAudio;
        }

        /// <summary>停止時の実測時間を記録します。</summary>
        internal void SetDuration(double durationSeconds)
        {
            _durationSeconds = durationSeconds;
        }

        /// <summary>バッファ不足による取りこぼしを記録します。</summary>
        internal void RecordDroppedFrame()
        {
            _droppedFrameCount++;
        }

        /// <summary>読み戻し予約前に時刻と連番を確定します。</summary>
        internal int RecordFrame(double elapsedSeconds)
        {
            var frameIndex = _frameCount;
            _frameTimestamps.Add(elapsedSeconds);
            _frameCount++;
            return frameIndex;
        }

        /// <summary>録画開始からの実測時間を目印に添えます。</summary>
        internal void AddMarker(string label, double elapsedSeconds)
        {
            _markers.Add(new VideoRecordingMarker
            {
                frame = _frameCount,
                timeSeconds = (float)elapsedSeconds,
                label = label ?? string.Empty,
            });
        }

        /// <summary>連番 JPG の保存パスを従来の命名規則で返します。</summary>
        internal string GetFrameFilePath(int frameIndex)
        {
            return Path.Combine(_outputDirectory, string.Format(FrameFileNameFormat, frameIndex));
        }

        /// <summary>ファイルが残らなかったフレームを控える。ワーカースレッドからも呼ばれる。</summary>
        internal void MarkFrameFailed(int frameIndex)
        {
            lock (_failedFrameLock)
            {
                _failedFrameIndexes.Add(frameIndex);
            }
        }

        /// <summary>GPU と書込が完了した後の履歴で成果物を保存します。</summary>
        internal VideoRecordingResult WriteManifestAndBuildResult(VideoCaptureGeometry geometry, AudioRecorder audioRecorder)
        {
            WriteFrameListFile();
            var manifest = CreateManifest(_name, _outputDirectory, geometry, audioRecorder);
            var manifestFilePath = Path.Combine(_outputDirectory, VideoRecorder.ManifestFileName);
            var manifestJson = JsonUtility.ToJson(manifest, true);
            File.WriteAllText(manifestFilePath, manifestJson);
            return new VideoRecordingResult(manifest.name, _outputDirectory, _frameCount, _framesPerSecond, _durationSeconds, manifestFilePath, manifest.ffmpegCommand, _hasAudio);
        }

        /// <summary>
        /// ffmpeg の concat デマルチプレクサ用のフレーム一覧を書き出す。
        /// 各フレームに実測の表示時間を持たせることで、動画の尺が録画した実時間と一致する。
        /// ファイル名は相対で書く（録画後にディレクトリを移動しても壊れないため）。
        /// </summary>
        private void WriteFrameListFile()
        {
            if (_frameTimestamps.Count == 0)
            {
                return;
            }

            // 失敗したフレームはファイルが無いため除外する。除外分の表示時間は
            // 直前の生き残りフレームへ吸収される（次の生存フレームとの差を取るため自動的にそうなる）
            var survivingFrameIndexes = new List<int>(_frameTimestamps.Count);
            for (var index = 0; index < _frameTimestamps.Count; index++)
            {
                if (!_failedFrameIndexes.Contains(index))
                {
                    survivingFrameIndexes.Add(index);
                }
            }

            if (survivingFrameIndexes.Count == 0)
            {
                return;
            }

            var lineBuilder = new StringBuilder();
            for (var position = 0; position < survivingFrameIndexes.Count; position++)
            {
                var frameIndex = survivingFrameIndexes[position];
                var nextTimestamp = position + 1 < survivingFrameIndexes.Count
                    ? _frameTimestamps[survivingFrameIndexes[position + 1]]
                    : _durationSeconds;
                var frameDuration = Math.Max(nextTimestamp - _frameTimestamps[frameIndex], MinimumFrameDuration);

                lineBuilder.Append("file '").Append(string.Format(FrameFileNameFormat, frameIndex)).Append("'\n");
                lineBuilder.Append("duration ").Append(frameDuration.ToString("F6", CultureInfo.InvariantCulture)).Append('\n');
            }

            lineBuilder.Append("file '").Append(string.Format(FrameFileNameFormat, survivingFrameIndexes[survivingFrameIndexes.Count - 1])).Append("'\n");

            File.WriteAllText(Path.Combine(_outputDirectory, VideoRecorder.FrameListFileName), lineBuilder.ToString());
        }

        /// <summary>停止済みまたは初期化前の結果を既存の補完規則で返します。</summary>
        internal VideoRecordingResult BuildResult()
        {
            var manifestFilePath = Path.Combine(_outputDirectory ?? string.Empty, VideoRecorder.ManifestFileName);
            var ffmpegCommand = VideoRecordingCommand.CreateFfmpegCommand(_framesPerSecond, _outputDirectory ?? string.Empty, _name ?? string.Empty, _durationSeconds, _hasAudio);
            return new VideoRecordingResult(_name, _outputDirectory, _frameCount, _framesPerSecond, _durationSeconds, manifestFilePath, ffmpegCommand, _hasAudio);
        }

        private VideoRecordingManifest CreateManifest(string name, string outputDirectory, VideoCaptureGeometry geometry, AudioRecorder audioRecorder)
        {
            return new VideoRecordingManifest
            {
                name = name,
                framesPerSecond = _framesPerSecond,
                frameCount = _frameCount,
                droppedFrameCount = _droppedFrameCount,
                durationSeconds = (float)_durationSeconds,
                width = geometry.Width,
                height = geometry.Height,
                capturedWidth = geometry.SourceWidth,
                capturedHeight = geometry.SourceHeight,
                cropRect = new[] { geometry.CropRect.x, geometry.CropRect.y, geometry.CropRect.width, geometry.CropRect.height },
                hasAudio = _hasAudio,
                audioSampleRate = audioRecorder != null && _hasAudio ? audioRecorder.SampleRate : 0,
                audioChannelCount = audioRecorder != null && _hasAudio ? audioRecorder.ChannelCount : 0,
                inputOverlay = _inputOverlayEnabled,
                startedAtRealtime = _startedAtRealtime,
                ffmpegCommand = VideoRecordingCommand.CreateFfmpegCommand(_framesPerSecond, outputDirectory, name, _durationSeconds, _hasAudio),
                markers = _markers.ToArray(),
            };
        }
    }
}
#endif
