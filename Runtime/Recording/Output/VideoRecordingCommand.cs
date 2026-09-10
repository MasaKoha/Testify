#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;
using System.IO;

namespace UniTestify
{
    /// <summary>保存したフレーム一覧と音声を変換する ffmpeg コマンドの形式を管理します。</summary>
    internal static class VideoRecordingCommand
    {
        private const string AudioFileName = "audio.wav";

        /// <summary>連番 JPG を mp4 へ変換する ffmpeg コマンドを組み立てる。変換の実行は呼び出し側が行う。</summary>
        internal static string CreateFfmpegCommand(int framesPerSecond, string outputDirectory, string name, double durationSeconds, bool hasAudio = false)
        {
            var frameListFilePath = Path.Combine(outputDirectory, VideoRecorder.FrameListFileName);
            var outputFilePath = Path.Combine(outputDirectory, $"{name}.mp4");
            var durationArgument = durationSeconds.ToString("F6", CultureInfo.InvariantCulture);

            if (hasAudio)
            {
                var audioFilePath = Path.Combine(outputDirectory, AudioFileName);
                return $"ffmpeg -y -f concat -safe 0 -i \"{frameListFilePath}\" -i \"{audioFilePath}\" -r {framesPerSecond} -t {durationArgument} -c:v libx264 -pix_fmt yuv420p -vf \"pad=ceil(iw/2)*2:ceil(ih/2)*2\" -c:a aac -shortest \"{outputFilePath}\"";
            }

            return $"ffmpeg -y -f concat -safe 0 -i \"{frameListFilePath}\" -r {framesPerSecond} -t {durationArgument} -c:v libx264 -pix_fmt yuv420p -vf \"pad=ceil(iw/2)*2:ceil(ih/2)*2\" \"{outputFilePath}\"";
        }
    }
}
#endif
