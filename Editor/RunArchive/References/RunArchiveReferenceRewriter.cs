using System;
using System.IO;
using UnityEngine;

namespace UniTestify.Editor
{
    /// <summary>録画と視覚回帰の参照先を、配送済みの成果物へ結び直します。</summary>
    internal static class RunArchiveReferenceRewriter
    {
        private const string CapturesDirectoryName = "captures";
        private const string VisualRegressionDirectoryName = "visual-regression";
        private const string ReportFileName = "report.json";
        private const string CaptureExtension = ".png";

        /// <summary>配送先に合わせて録画の変換コマンドを再構築します。</summary>
        internal static void RewriteRecordingManifest(string recordingDirectoryPath, string recordingName)
        {
            var manifestFilePath = Path.Combine(recordingDirectoryPath, VideoRecorder.ManifestFileName);
            if (!File.Exists(manifestFilePath))
            {
                return;
            }

            var manifestJson = File.ReadAllText(manifestFilePath);
            var manifest = JsonUtility.FromJson<VideoRecordingManifest>(manifestJson);
            if (manifest == null)
            {
                return;
            }

            manifest.name = string.IsNullOrEmpty(manifest.name) ? recordingName : manifest.name;
            manifest.ffmpegCommand = VideoRecorder.CreateFfmpegCommand(
                manifest.framesPerSecond,
                recordingDirectoryPath,
                manifest.name,
                manifest.durationSeconds,
                manifest.hasAudio);
            File.WriteAllText(manifestFilePath, JsonUtility.ToJson(manifest, true));
        }

        /// <summary>比較画像を集約し、レポートをラン相対パスへ書き換えます。</summary>
        internal static void RewriteVisualRegressionReport(VisualRegressionReport report, string outputDirectoryPath)
        {
            report.outputDirectory = RunArchiveFileStorage.NormalizePathForJson(VisualRegressionDirectoryName);
            report.capturesDirectory = RunArchiveFileStorage.NormalizePathForJson(CapturesDirectoryName);
            if (report.results != null)
            {
                for (var resultIndex = 0; resultIndex < report.results.Length; resultIndex++)
                {
                    var result = report.results[resultIndex];
                    if (result == null)
                    {
                        continue;
                    }

                    result.actualPath = CopyVisualRegressionAsset(result.actualPath, outputDirectoryPath, $"{result.capture}-actual{CaptureExtension}");
                    result.diffPath = CopyVisualRegressionAsset(result.diffPath, outputDirectoryPath, $"{result.capture}-diff{CaptureExtension}");
                    result.baselinePath = CopyVisualRegressionAsset(result.baselinePath, outputDirectoryPath, $"{result.capture}-baseline{CaptureExtension}");
                }
            }

            var outputReportPath = Path.Combine(outputDirectoryPath, ReportFileName);
            File.WriteAllText(outputReportPath, JsonUtility.ToJson(report, true));
        }

        private static string CopyVisualRegressionAsset(string sourceFilePath, string outputDirectoryPath, string preferredFileName)
        {
            if (string.IsNullOrEmpty(sourceFilePath))
            {
                return string.Empty;
            }

            var sourceExists = File.Exists(sourceFilePath);
            var destinationFileName = string.IsNullOrEmpty(preferredFileName) ? Path.GetFileName(sourceFilePath) : preferredFileName;
            var destinationFilePath = Path.Combine(outputDirectoryPath, destinationFileName);
            if (sourceExists && !Path.GetFullPath(sourceFilePath).Equals(Path.GetFullPath(destinationFilePath), StringComparison.Ordinal))
            {
                File.Copy(sourceFilePath, destinationFilePath, true);
            }

            if (!File.Exists(destinationFilePath))
            {
                return string.Empty;
            }

            return RunArchiveFileStorage.NormalizePathForJson(Path.Combine(VisualRegressionDirectoryName, destinationFileName));
        }
    }
}
