#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace UniTestify.Tests
{
    /// <summary>GPU に依存せず、失敗フレームを含む録画成果物の互換性を固定します。</summary>
    public sealed class VideoRecordingArtifactsTest
    {
        private const int FramesPerSecond = 30;
        private const double DurationSeconds = 1.0;
        private const double MarkerTimeSeconds = 0.5;
        private const double FirstFrameTime = 0.0;
        private const double SecondFrameTime = 0.1;
        private const double ThirdFrameTime = 0.4;
        private const double FourthFrameTime = 0.8;
        private const string RecordingName = "take";
        private string _outputDirectory;

        /// <summary>既存の録画成果物を変更しない一時出力先を用意します。</summary>
        [SetUp]
        public void SetUp()
        {
            _outputDirectory = Path.Combine(Path.GetTempPath(), "unitestify-video-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_outputDirectory);
        }

        /// <summary>このテストが作成した成果物だけを削除します。</summary>
        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_outputDirectory, true);
        }

        /// <summary>失敗した中間・末尾フレームの時間を、生存フレームへ吸収します。</summary>
        [Test]
        public void FrameListPreservesSurvivingFramesAndDurations()
        {
            var artifacts = CreateArtifacts();
            artifacts.RecordFrame(FirstFrameTime);
            var failedMiddleFrame = artifacts.RecordFrame(SecondFrameTime);
            artifacts.RecordFrame(ThirdFrameTime);
            var failedLastFrame = artifacts.RecordFrame(FourthFrameTime);
            artifacts.MarkFrameFailed(failedMiddleFrame);
            artifacts.MarkFrameFailed(failedLastFrame);
            artifacts.RecordDroppedFrame();
            artifacts.AddMarker("quote\"\n", MarkerTimeSeconds);
            var previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                // 成果物の契約だけを検証するため、画面寸法の観測は開始しない。
                var result = artifacts.WriteManifestAndBuildResult(new VideoCaptureGeometry(), null);

                Assert.That(File.ReadAllText(Path.Combine(_outputDirectory, VideoRecorder.FrameListFileName)), Is.EqualTo(
                    "file 'frame-00000.jpg'\nduration 0.400000\n" +
                    "file 'frame-00002.jpg'\nduration 0.600000\nfile 'frame-00002.jpg'\n"));
                var manifest = JsonUtility.FromJson<VideoRecordingManifest>(File.ReadAllText(result.ManifestFilePath));
                Assert.That(manifest.frameCount, Is.EqualTo(4));
                Assert.That(manifest.droppedFrameCount, Is.EqualTo(1));
                Assert.That(manifest.durationSeconds, Is.EqualTo(DurationSeconds));
                Assert.That(manifest.inputOverlay, Is.False);
                Assert.That(manifest.hasAudio, Is.False);
                Assert.That(JsonUtility.ToJson(manifest.markers[0]), Is.EqualTo(
                    "{\"frame\":4,\"timeSeconds\":0.5,\"label\":\"quote\\\"\\n\"}"));
                Assert.That(result.ManifestFilePath, Is.EqualTo(Path.Combine(_outputDirectory, "recording-manifest.json")));
                Assert.That(result.FfmpegCommand, Is.EqualTo(manifest.ffmpegCommand));
                Assert.That(result.FrameCount, Is.EqualTo(4));
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        }

        /// <summary>空録画と全フレーム失敗時は、存在しない JPG の一覧を作りません。</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void FrameListIsAbsentWithoutSurvivingFrames(bool recordFailedFrame)
        {
            var artifacts = CreateArtifacts();
            if (recordFailedFrame)
            {
                artifacts.MarkFrameFailed(artifacts.RecordFrame(FirstFrameTime));
            }

            var result = artifacts.WriteManifestAndBuildResult(new VideoCaptureGeometry(), null);

            Assert.That(File.Exists(Path.Combine(_outputDirectory, VideoRecorder.FrameListFileName)), Is.False);
            Assert.That(File.Exists(result.ManifestFilePath), Is.True);
        }

        /// <summary>同時刻のフレームでも最小表示時間と末尾の再掲を維持します。</summary>
        [Test]
        public void FrameListPreservesMinimumDuration()
        {
            var artifacts = CreateArtifacts();
            artifacts.RecordFrame(DurationSeconds);

            artifacts.WriteManifestAndBuildResult(new VideoCaptureGeometry(), null);

            Assert.That(File.ReadAllText(Path.Combine(_outputDirectory, VideoRecorder.FrameListFileName)), Is.EqualTo(
                "file 'frame-00000.jpg'\nduration 0.000100\nfile 'frame-00000.jpg'\n"));
        }

        private VideoRecordingArtifacts CreateArtifacts()
        {
            var artifacts = new VideoRecordingArtifacts();
            artifacts.Initialize(_outputDirectory, RecordingName, FramesPerSecond, false);
            artifacts.SetDuration(DurationSeconds);
            return artifacts;
        }
    }
}
#endif
