using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UniTestify.Editor
{
    /// <summary>成果物集約の順序を統括し、ラン作成と索引更新の公開入口を維持します。</summary>
    public static class RunArchive
    {
        private const string VerificationRunsDirectoryName = "VerificationRuns";
        private const string MetaFileName = "meta.json";
        private const string PlayerLogFileName = "player-log.log";
        private const string CapturesDirectoryName = "captures";
        private const string SnapshotsDirectoryName = "snapshots";
        private const string RecordingsDirectoryName = "recordings";
        private const string ForensicsDirectoryName = "forensics";
        private const string VisualRegressionDirectoryName = "visual-regression";
        private const string MonkeyDirectoryName = "monkey";
        private const string PerformanceFileName = "performance.json";
        private const string ScenarioResultsDirectoryName = "scenario-results";
        private const string AuditFileNameSuffix = "-audit.json";

        /// <summary>
        /// 直近成果物を起点にランを再構成し、既存ツールへ手を入れずにラン単位の確認導線を作る。
        /// </summary>
        public static string CreateLatest()
        {
            var scenarioResultsDirectoryPath = Path.Combine(DebugOutputPath.DirectoryPath, ScenarioResultsDirectoryName);
            var latestScenarioResultPath = RunArchiveFileStorage.FindLatestFile(scenarioResultsDirectoryPath, "*.json");
            return CreateFromScenarioResult(latestScenarioResultPath);
        }

        /// <summary>
        /// シナリオ結果を起点に成果物を集約し直し、過去ランも同じ形式へ揃えられるようにする。
        /// </summary>
        public static string CreateFromScenarioResult(string scenarioResultPath)
        {
            var scenarioResult = RunArchiveScenarioResultWriter.LoadScenarioResult(scenarioResultPath);
            var projectRootDirectoryPath = RunArchiveFileStorage.GetProjectRootDirectoryPath();
            var verificationRunsDirectoryPath = Path.Combine(projectRootDirectoryPath, VerificationRunsDirectoryName);
            Directory.CreateDirectory(verificationRunsDirectoryPath);

            var anchorTime = RunArchiveTiming.ResolveAnchorTime(scenarioResultPath, scenarioResult);
            var runStartedAt = RunArchiveTiming.ResolveStartedAt(anchorTime, scenarioResult);
            var runFinishedAt = RunArchiveTiming.ResolveFinishedAt(anchorTime, scenarioResult, runStartedAt);
            var runDirectoryPath = RunArchiveFileStorage.CreateUniqueRunDirectory(verificationRunsDirectoryPath, runStartedAt);

            var captureOutputDirectoryPath = Path.Combine(runDirectoryPath, CapturesDirectoryName);
            var snapshotOutputDirectoryPath = Path.Combine(runDirectoryPath, SnapshotsDirectoryName);
            var recordingOutputDirectoryPath = Path.Combine(runDirectoryPath, RecordingsDirectoryName);
            var forensicOutputDirectoryPath = Path.Combine(runDirectoryPath, ForensicsDirectoryName);
            var monkeyOutputDirectoryPath = Path.Combine(runDirectoryPath, MonkeyDirectoryName);
            var visualRegressionOutputDirectoryPath = Path.Combine(runDirectoryPath, VisualRegressionDirectoryName);

            Directory.CreateDirectory(captureOutputDirectoryPath);
            Directory.CreateDirectory(snapshotOutputDirectoryPath);
            Directory.CreateDirectory(recordingOutputDirectoryPath);
            Directory.CreateDirectory(forensicOutputDirectoryPath);

            var evidenceCaptureMap = new Dictionary<string, string>(StringComparer.Ordinal);
            var evidenceSnapshotMap = new Dictionary<string, string>(StringComparer.Ordinal);
            var copiedCaptureFileCount = RunArchiveScenarioArtifacts.CopyScenarioCaptures(runStartedAt, runFinishedAt, captureOutputDirectoryPath);
            var copiedAuditFileCount = RunArchiveSummaryBuilder.CountFiles(captureOutputDirectoryPath, $"*{AuditFileNameSuffix}");
            var auditFindingCount = RunArchiveSummaryBuilder.CountAuditFindings(captureOutputDirectoryPath);

            RunArchiveScenarioArtifacts.CopyScenarioEvidenceFiles(scenarioResult, captureOutputDirectoryPath, snapshotOutputDirectoryPath, evidenceCaptureMap, evidenceSnapshotMap);
            RunArchiveScenarioArtifacts.CopyWindowSnapshots(runStartedAt, runFinishedAt, snapshotOutputDirectoryPath, evidenceSnapshotMap);

            var copiedRecordingNames = RunArchiveDirectoryArtifacts.CopyRecordings(scenarioResult, runStartedAt, runFinishedAt, recordingOutputDirectoryPath);
            var droppedFrameCount = RunArchiveSummaryBuilder.CountDroppedFrames(recordingOutputDirectoryPath);
            var copiedForensicDirectoryNames = RunArchiveDirectoryArtifacts.CopyForensics(scenarioResult, runStartedAt, runFinishedAt, forensicOutputDirectoryPath);
            RunArchiveDirectoryArtifacts.CopyMonkeyRuns(runStartedAt, runFinishedAt, monkeyOutputDirectoryPath);
            var visualRegressionSummary = RunArchiveReportArtifacts.CopyVisualRegression(runStartedAt, runFinishedAt, visualRegressionOutputDirectoryPath);
            var performanceSummary = RunArchiveReportArtifacts.CopyPerformance(runStartedAt, runFinishedAt, Path.Combine(runDirectoryPath, PerformanceFileName));
            RunArchiveReportArtifacts.CopyPlayerLog(runStartedAt, runFinishedAt, Path.Combine(runDirectoryPath, PlayerLogFileName));

            var archivedScenarioResult = RunArchiveScenarioResultWriter.RewriteScenarioResult(
                scenarioResult,
                evidenceCaptureMap,
                evidenceSnapshotMap,
                copiedRecordingNames,
                copiedForensicDirectoryNames);
            RunArchiveScenarioResultWriter.SaveScenarioResult(runDirectoryPath, archivedScenarioResult);

            copiedCaptureFileCount = RunArchiveSummaryBuilder.CountFiles(captureOutputDirectoryPath, "*.png");
            copiedAuditFileCount = RunArchiveSummaryBuilder.CountFiles(captureOutputDirectoryPath, $"*{AuditFileNameSuffix}");
            auditFindingCount = RunArchiveSummaryBuilder.CountAuditFindings(captureOutputDirectoryPath);

            var meta = RunArchiveSummaryBuilder.BuildMeta(
                archivedScenarioResult,
                runStartedAt,
                runFinishedAt,
                copiedCaptureFileCount,
                copiedAuditFileCount,
                auditFindingCount,
                droppedFrameCount,
                copiedRecordingNames,
                copiedForensicDirectoryNames.Length,
                visualRegressionSummary,
                performanceSummary);
            var metaFilePath = Path.Combine(runDirectoryPath, MetaFileName);
            File.WriteAllText(metaFilePath, JsonUtility.ToJson(meta, true));
            RunArchiveIndexWriter.RebuildIndex();
            return runDirectoryPath;
        }

        /// <summary>
        /// 配下ランから索引を再生成し、ギャラリーが常に最新の一覧を読める状態を保つ。
        /// </summary>
        public static string RebuildIndex()
        {
            return RunArchiveIndexWriter.RebuildIndex();
        }
    }
}
