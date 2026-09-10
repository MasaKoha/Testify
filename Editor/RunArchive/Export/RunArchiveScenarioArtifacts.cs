using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UniTestify.Editor
{
    /// <summary>シナリオのキャプチャとスナップショットを証拠の対応表とともに集約します。</summary>
    internal static class RunArchiveScenarioArtifacts
    {
        private const string CapturesDirectoryName = "captures";
        private const string SnapshotsDirectoryName = "snapshots";
        private const string DefaultScenarioCaptureDirectoryName = "ui-scenario";
        private const string SnapshotSourceDirectoryName = "snapshots";
        private const string AuditFileNameSuffix = "-audit.json";
        private const string SnapshotCompactTextExtension = ".txt";

        /// <summary>ラン期間のキャプチャと監査を配送します。</summary>
        internal static int CopyScenarioCaptures(DateTimeOffset runStartedAt, DateTimeOffset runFinishedAt, string outputDirectoryPath)
        {
            var sourceDirectoryPath = Path.Combine(DebugOutputPath.DirectoryPath, DefaultScenarioCaptureDirectoryName);
            if (!Directory.Exists(sourceDirectoryPath))
            {
                return 0;
            }

            var copiedFileCount = 0;
            var sourceFilePaths = Directory.GetFiles(sourceDirectoryPath, "*", SearchOption.TopDirectoryOnly);
            Array.Sort(sourceFilePaths, StringComparer.Ordinal);
            for (var fileIndex = 0; fileIndex < sourceFilePaths.Length; fileIndex++)
            {
                var sourceFilePath = sourceFilePaths[fileIndex];
                var fileName = Path.GetFileName(sourceFilePath);
                var isCaptureFile = fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
                var isAuditFile = fileName.EndsWith(AuditFileNameSuffix, StringComparison.OrdinalIgnoreCase);
                if (!isCaptureFile && !isAuditFile)
                {
                    continue;
                }

                if (!RunArchiveFileStorage.IsArtifactInWindow(sourceFilePath, runStartedAt, runFinishedAt))
                {
                    continue;
                }

                var destinationFilePath = Path.Combine(outputDirectoryPath, fileName);
                File.Copy(sourceFilePath, destinationFilePath, true);
                copiedFileCount++;
            }

            return copiedFileCount;
        }

        /// <summary>明示された証拠を配送し、参照書換え用の対応表を更新します。</summary>
        internal static void CopyScenarioEvidenceFiles(
            RunArchiveScenarioResult scenarioResult,
            string captureOutputDirectoryPath,
            string snapshotOutputDirectoryPath,
            Dictionary<string, string> evidenceCaptureMap,
            Dictionary<string, string> evidenceSnapshotMap)
        {
            if (scenarioResult == null || scenarioResult.steps == null)
            {
                return;
            }

            for (var stepIndex = 0; stepIndex < scenarioResult.steps.Length; stepIndex++)
            {
                var step = scenarioResult.steps[stepIndex];
                if (step == null || step.evidence == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(step.evidence.capture) && File.Exists(step.evidence.capture))
                {
                    var destinationFilePath = Path.Combine(captureOutputDirectoryPath, Path.GetFileName(step.evidence.capture));
                    File.Copy(step.evidence.capture, destinationFilePath, true);
                    evidenceCaptureMap[step.evidence.capture] = RunArchiveFileStorage.NormalizePathForJson(Path.Combine(CapturesDirectoryName, Path.GetFileName(destinationFilePath)));
                }

                if (string.IsNullOrEmpty(step.evidence.snapshot) || !File.Exists(step.evidence.snapshot))
                {
                    continue;
                }

                var snapshotFileName = Path.GetFileName(step.evidence.snapshot);
                var destinationSnapshotPath = Path.Combine(snapshotOutputDirectoryPath, snapshotFileName);
                File.Copy(step.evidence.snapshot, destinationSnapshotPath, true);
                WriteSnapshotCompactText(destinationSnapshotPath);
                evidenceSnapshotMap[step.evidence.snapshot] = RunArchiveFileStorage.NormalizePathForJson(Path.Combine(SnapshotsDirectoryName, snapshotFileName));
            }
        }

        /// <summary>証拠として未配送の観測をラン期間から補完します。</summary>
        internal static void CopyWindowSnapshots(DateTimeOffset runStartedAt, DateTimeOffset runFinishedAt, string snapshotOutputDirectoryPath, Dictionary<string, string> evidenceSnapshotMap)
        {
            var sourceDirectoryPath = Path.Combine(DebugOutputPath.DirectoryPath, SnapshotSourceDirectoryName);
            if (!Directory.Exists(sourceDirectoryPath))
            {
                return;
            }

            var snapshotFilePaths = Directory.GetFiles(sourceDirectoryPath, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(snapshotFilePaths, StringComparer.Ordinal);
            for (var fileIndex = 0; fileIndex < snapshotFilePaths.Length; fileIndex++)
            {
                var sourceFilePath = snapshotFilePaths[fileIndex];
                if (!RunArchiveFileStorage.IsArtifactInWindow(sourceFilePath, runStartedAt, runFinishedAt))
                {
                    continue;
                }

                if (evidenceSnapshotMap.ContainsKey(sourceFilePath))
                {
                    continue;
                }

                var destinationFilePath = Path.Combine(snapshotOutputDirectoryPath, Path.GetFileName(sourceFilePath));
                File.Copy(sourceFilePath, destinationFilePath, true);
                WriteSnapshotCompactText(destinationFilePath);
                evidenceSnapshotMap[sourceFilePath] = RunArchiveFileStorage.NormalizePathForJson(Path.Combine(SnapshotsDirectoryName, Path.GetFileName(destinationFilePath)));
            }
        }

        private static void WriteSnapshotCompactText(string snapshotFilePath)
        {
            var snapshotJson = File.ReadAllText(snapshotFilePath);
            var snapshot = JsonUtility.FromJson<UiSnapshotDocument>(snapshotJson);
            if (snapshot == null)
            {
                return;
            }

            var compactText = UiSnapshot.ToCompactText(snapshot);
            var compactTextPath = Path.ChangeExtension(snapshotFilePath, SnapshotCompactTextExtension);
            File.WriteAllText(compactTextPath, compactText);
        }
    }
}
