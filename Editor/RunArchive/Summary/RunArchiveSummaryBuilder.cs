using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace UniTestify.Editor
{
    /// <summary>成果物の件数と実行情報からラン概要を構築します。</summary>
    internal static class RunArchiveSummaryBuilder
    {
        private const string AuditFileNameSuffix = "-audit.json";
        private const string GitExecutableName = "git";

        /// <summary>シナリオ結果と成果物集計を従来の優先順位でラン概要にまとめます。</summary>
        internal static RunArchiveMeta BuildMeta(
            RunArchiveScenarioResult scenarioResult,
            DateTimeOffset runStartedAt,
            DateTimeOffset runFinishedAt,
            int captureCount,
            int auditCount,
            int auditFindingCount,
            int droppedFrameCount,
            string[] recordingNames,
            int forensicDirectoryCount,
            RunArchiveVisualRegressionSummary visualRegressionSummary,
            RunArchivePerformanceSummary performanceSummary)
        {
            var scenarioName = scenarioResult == null ? string.Empty : scenarioResult.scenario;
            var verdict = scenarioResult == null ? string.Empty : scenarioResult.verdict;
            var startedAtText = scenarioResult == null || string.IsNullOrEmpty(scenarioResult.startedAt)
                ? runStartedAt.ToString("o")
                : scenarioResult.startedAt;
            var finishedAtText = scenarioResult == null || string.IsNullOrEmpty(scenarioResult.finishedAt)
                ? runFinishedAt.ToString("o")
                : scenarioResult.finishedAt;
            var durationSeconds = scenarioResult == null || scenarioResult.durationSeconds <= 0.0f
                ? (float)Math.Max(0.0, (runFinishedAt - runStartedAt).TotalSeconds)
                : scenarioResult.durationSeconds;
            var exceptionCount = scenarioResult == null || scenarioResult.exceptionCount <= 0
                ? forensicDirectoryCount
                : scenarioResult.exceptionCount;
            var warningCount = scenarioResult == null ? 0 : scenarioResult.warningCount;
            return new RunArchiveMeta(
                scenarioName,
                verdict,
                startedAtText,
                finishedAtText,
                durationSeconds,
                captureCount,
                auditCount,
                auditFindingCount,
                exceptionCount,
                warningCount,
                droppedFrameCount,
                recordingNames,
                visualRegressionSummary,
                performanceSummary,
                ResolveGitCommitHash(),
                Application.unityVersion);
        }

        /// <summary>録画 manifest のドロップ数を合算します。</summary>
        internal static int CountDroppedFrames(string recordingsDirectoryPath)
        {
            if (!Directory.Exists(recordingsDirectoryPath))
            {
                return 0;
            }

            var manifestFilePaths = Directory.GetFiles(recordingsDirectoryPath, VideoRecorder.ManifestFileName, SearchOption.AllDirectories);
            var droppedFrameCount = 0;
            for (var fileIndex = 0; fileIndex < manifestFilePaths.Length; fileIndex++)
            {
                var manifestJson = File.ReadAllText(manifestFilePaths[fileIndex]);
                var manifest = JsonUtility.FromJson<VideoRecordingManifest>(manifestJson);
                if (manifest == null)
                {
                    continue;
                }

                droppedFrameCount += manifest.droppedFrameCount;
            }

            return droppedFrameCount;
        }

        /// <summary>監査レポートの指摘数を合算します。</summary>
        internal static int CountAuditFindings(string captureDirectoryPath)
        {
            if (!Directory.Exists(captureDirectoryPath))
            {
                return 0;
            }

            var auditFilePaths = Directory.GetFiles(captureDirectoryPath, $"*{AuditFileNameSuffix}", SearchOption.TopDirectoryOnly);
            var findingCount = 0;
            for (var fileIndex = 0; fileIndex < auditFilePaths.Length; fileIndex++)
            {
                var reportJson = File.ReadAllText(auditFilePaths[fileIndex]);
                var report = JsonUtility.FromJson<UiLayoutAuditReport>(reportJson);
                if (report == null || report.entries == null)
                {
                    continue;
                }

                findingCount += report.entries.Length;
            }

            return findingCount;
        }

        /// <summary>指定種類の直下ファイル数を返します。</summary>
        internal static int CountFiles(string directoryPath, string searchPattern)
        {
            if (!Directory.Exists(directoryPath))
            {
                return 0;
            }

            return Directory.GetFiles(directoryPath, searchPattern, SearchOption.TopDirectoryOnly).Length;
        }

        private static string ResolveGitCommitHash()
        {
            try
            {
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = GitExecutableName,
                        Arguments = $"rev-parse --short HEAD",
                        WorkingDirectory = RunArchiveFileStorage.GetProjectRootDirectoryPath(),
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                    };

                    if (!process.Start())
                    {
                        return string.Empty;
                    }

                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(3000);
                    if (process.ExitCode != 0)
                    {
                        return string.Empty;
                    }

                    return string.IsNullOrWhiteSpace(output) ? string.Empty : output.Trim();
                }
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
