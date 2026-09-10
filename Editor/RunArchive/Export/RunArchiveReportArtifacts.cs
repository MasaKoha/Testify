using System;
using System.IO;
using UnityEngine;

namespace UniTestify.Editor
{
    /// <summary>視覚回帰・性能・ログの成果物を時間範囲から選択して集約します。</summary>
    internal static class RunArchiveReportArtifacts
    {
        private const string PerformanceSourceDirectoryName = "performance";
        private const string VisualRegressionSourceDirectoryName = "visual-regression";
        private const string PlayerLogFileNamePrefix = "player-log-";
        private const string ReportFileName = "report.json";

        /// <summary>期間内で最後の視覚回帰レポートを配送し、集計を返します。</summary>
        internal static RunArchiveVisualRegressionSummary CopyVisualRegression(DateTimeOffset runStartedAt, DateTimeOffset runFinishedAt, string outputDirectoryPath)
        {
            var visualRegressionRootDirectoryPath = Path.Combine(DebugOutputPath.DirectoryPath, VisualRegressionSourceDirectoryName);
            if (!Directory.Exists(visualRegressionRootDirectoryPath))
            {
                return new RunArchiveVisualRegressionSummary(0, 0, 0);
            }

            var candidateDirectoryPaths = Directory.GetDirectories(visualRegressionRootDirectoryPath, "*", SearchOption.TopDirectoryOnly);
            Array.Sort(candidateDirectoryPaths, StringComparer.Ordinal);
            for (var directoryIndex = candidateDirectoryPaths.Length - 1; directoryIndex >= 0; directoryIndex--)
            {
                var candidateDirectoryPath = candidateDirectoryPaths[directoryIndex];
                if (!RunArchiveFileStorage.IsArtifactInWindow(candidateDirectoryPath, runStartedAt, runFinishedAt))
                {
                    continue;
                }

                var reportFilePath = Path.Combine(candidateDirectoryPath, ReportFileName);
                if (!File.Exists(reportFilePath))
                {
                    continue;
                }

                RunArchiveFileStorage.CopyDirectory(candidateDirectoryPath, outputDirectoryPath);
                var reportJson = File.ReadAllText(reportFilePath);
                var report = JsonUtility.FromJson<VisualRegressionReport>(reportJson);
                if (report == null)
                {
                    return new RunArchiveVisualRegressionSummary(0, 0, 0);
                }

                RunArchiveReferenceRewriter.RewriteVisualRegressionReport(report, outputDirectoryPath);
                return new RunArchiveVisualRegressionSummary(report.passCount, report.failCount, report.noBaselineCount);
            }

            return new RunArchiveVisualRegressionSummary(0, 0, 0);
        }

        /// <summary>期間内で最後の性能レポートを配送し、集計を返します。</summary>
        internal static RunArchivePerformanceSummary CopyPerformance(DateTimeOffset runStartedAt, DateTimeOffset runFinishedAt, string outputFilePath)
        {
            var performanceDirectoryPath = Path.Combine(DebugOutputPath.DirectoryPath, PerformanceSourceDirectoryName);
            if (!Directory.Exists(performanceDirectoryPath))
            {
                return new RunArchivePerformanceSummary(0.0f);
            }

            var candidateFilePaths = Directory.GetFiles(performanceDirectoryPath, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(candidateFilePaths, StringComparer.Ordinal);
            for (var fileIndex = candidateFilePaths.Length - 1; fileIndex >= 0; fileIndex--)
            {
                var candidateFilePath = candidateFilePaths[fileIndex];
                if (!RunArchiveFileStorage.IsArtifactInWindow(candidateFilePath, runStartedAt, runFinishedAt))
                {
                    continue;
                }

                File.Copy(candidateFilePath, outputFilePath, true);
                var reportJson = File.ReadAllText(candidateFilePath);
                var report = JsonUtility.FromJson<PerformanceReport>(reportJson);
                var percentile95 = report == null || report.summary == null ? 0.0f : report.summary.frameMsP95;
                return new RunArchivePerformanceSummary(percentile95);
            }

            return new RunArchivePerformanceSummary(0.0f);
        }

        /// <summary>期間内で最後のプレイヤーログを配送します。</summary>
        internal static void CopyPlayerLog(DateTimeOffset runStartedAt, DateTimeOffset runFinishedAt, string outputFilePath)
        {
            var debugOutputDirectoryPath = DebugOutputPath.DirectoryPath;
            if (!Directory.Exists(debugOutputDirectoryPath))
            {
                return;
            }

            var candidateFilePaths = Directory.GetFiles(debugOutputDirectoryPath, $"{PlayerLogFileNamePrefix}*.log", SearchOption.TopDirectoryOnly);
            Array.Sort(candidateFilePaths, StringComparer.Ordinal);
            for (var fileIndex = candidateFilePaths.Length - 1; fileIndex >= 0; fileIndex--)
            {
                var candidateFilePath = candidateFilePaths[fileIndex];
                if (!RunArchiveFileStorage.IsArtifactInWindow(candidateFilePath, runStartedAt, runFinishedAt))
                {
                    continue;
                }

                File.Copy(candidateFilePath, outputFilePath, true);
                return;
            }
        }
    }
}
