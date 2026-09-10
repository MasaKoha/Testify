using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UniTestify.Editor
{
    /// <summary>シナリオ結果の参照をラン内パスへ置き換え、同じ JSON 形式で保存します。</summary>
    internal static class RunArchiveScenarioResultWriter
    {
        private const string ScenarioResultFileName = "scenario-result.json";
        private const string RecordingsDirectoryName = "recordings";
        private const string ForensicsDirectoryName = "forensics";

        /// <summary>書換え済みシナリオ結果を従来の形式で保存します。</summary>
        internal static void SaveScenarioResult(string runDirectoryPath, RunArchiveScenarioResult scenarioResult)
        {
            if (scenarioResult == null)
            {
                return;
            }

            var outputFilePath = Path.Combine(runDirectoryPath, ScenarioResultFileName);
            File.WriteAllText(outputFilePath, JsonUtility.ToJson(scenarioResult, true));
        }

        /// <summary>証拠の対応表を使い、ラン内の成果物への参照に置き換えます。</summary>
        internal static RunArchiveScenarioResult RewriteScenarioResult(
            RunArchiveScenarioResult scenarioResult,
            Dictionary<string, string> evidenceCaptureMap,
            Dictionary<string, string> evidenceSnapshotMap,
            string[] recordingNames,
            string[] forensicDirectoryNames)
        {
            if (scenarioResult == null)
            {
                return null;
            }

            if (scenarioResult.steps != null)
            {
                for (var stepIndex = 0; stepIndex < scenarioResult.steps.Length; stepIndex++)
                {
                    var step = scenarioResult.steps[stepIndex];
                    if (step == null || step.evidence == null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(step.evidence.capture) && evidenceCaptureMap.TryGetValue(step.evidence.capture, out var relativeCapturePath))
                    {
                        step.evidence.capture = relativeCapturePath;
                    }

                    if (!string.IsNullOrEmpty(step.evidence.snapshot) && evidenceSnapshotMap.TryGetValue(step.evidence.snapshot, out var relativeSnapshotPath))
                    {
                        step.evidence.snapshot = relativeSnapshotPath;
                    }
                }
            }

            scenarioResult.recordings = CreateRelativeRecordingPaths(recordingNames);
            scenarioResult.exceptions = CreateRelativeDirectoryPaths(ForensicsDirectoryName, forensicDirectoryNames);
            return scenarioResult;
        }

        private static string[] CreateRelativeRecordingPaths(string[] recordingNames)
        {
            if (recordingNames == null || recordingNames.Length == 0)
            {
                return Array.Empty<string>();
            }

            var relativePaths = new string[recordingNames.Length];
            for (var recordingIndex = 0; recordingIndex < recordingNames.Length; recordingIndex++)
            {
                relativePaths[recordingIndex] = RunArchiveFileStorage.NormalizePathForJson(Path.Combine(RecordingsDirectoryName, recordingNames[recordingIndex]));
            }

            return relativePaths;
        }

        private static string[] CreateRelativeDirectoryPaths(string parentDirectoryName, string[] directoryNames)
        {
            if (directoryNames == null || directoryNames.Length == 0)
            {
                return Array.Empty<string>();
            }

            var relativePaths = new string[directoryNames.Length];
            for (var directoryIndex = 0; directoryIndex < directoryNames.Length; directoryIndex++)
            {
                relativePaths[directoryIndex] = RunArchiveFileStorage.NormalizePathForJson(Path.Combine(parentDirectoryName, directoryNames[directoryIndex]));
            }

            return relativePaths;
        }

        /// <summary>結果ファイルが指定されている場合に集約用データを読み込みます。</summary>
        internal static RunArchiveScenarioResult LoadScenarioResult(string scenarioResultPath)
        {
            if (string.IsNullOrEmpty(scenarioResultPath) || !File.Exists(scenarioResultPath))
            {
                return null;
            }

            var scenarioResultJson = File.ReadAllText(scenarioResultPath);
            return JsonUtility.FromJson<RunArchiveScenarioResult>(scenarioResultJson);
        }
    }
}
