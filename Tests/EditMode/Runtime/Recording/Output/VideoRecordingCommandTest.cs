#if UNITY_EDITOR
using System.Globalization;
using System.IO;
using NUnit.Framework;

namespace UniTestify.Tests
{
    /// <summary>公開入口の ffmpeg コマンドを、音声の有無とカルチャを含めて固定します。</summary>
    public sealed class VideoRecordingCommandTest
    {
        private const int FramesPerSecond = 24;
        private const double DurationSeconds = 1.25;

        /// <summary>引用符・引数順・小数点・音声オプションを維持します。</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void CreateFfmpegCommandPreservesExactArguments(bool hasAudio)
        {
            var previousCulture = CultureInfo.CurrentCulture;
            var outputDirectory = Path.Combine("recordings", "with space");
            var frameList = Path.Combine(outputDirectory, "frames.txt");
            var audioFile = Path.Combine(outputDirectory, "audio.wav");
            var outputFile = Path.Combine(outputDirectory, "take.mp4");
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var expected = $"ffmpeg -y -f concat -safe 0 -i \"{frameList}\"";
                if (hasAudio)
                {
                    expected += $" -i \"{audioFile}\"";
                }

                expected += " -r 24 -t 1.250000 -c:v libx264 -pix_fmt yuv420p -vf \"pad=ceil(iw/2)*2:ceil(ih/2)*2\"";
                if (hasAudio)
                {
                    expected += " -c:a aac -shortest";
                }

                expected += $" \"{outputFile}\"";

                Assert.That(VideoRecorder.CreateFfmpegCommand(FramesPerSecond, outputDirectory, "take", DurationSeconds, hasAudio),
                    Is.EqualTo(expected));
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        }
    }
}
#endif
