using System;
using System.Collections.Generic;
using System.IO;

namespace UniTestify.Editor
{
    /// <summary>録画・フォレンジック・探索の成果物ディレクトリを選択して配送します。</summary>
    internal static class RunArchiveDirectoryArtifacts
    {
        private const string RecordingSourceDirectoryName = "recordings";
        private const string ForensicsSourceDirectoryName = "forensics";
        private const string MonkeySourceDirectoryName = "monkey";

        /// <summary>明示された録画を優先し、なければラン期間から選択します。</summary>
        internal static string[] CopyRecordings(RunArchiveScenarioResult scenarioResult, DateTimeOffset runStartedAt, DateTimeOffset runFinishedAt, string outputDirectoryPath)
        {
            var sourceDirectoryPaths = new List<string>();
            if (scenarioResult != null && scenarioResult.recordings != null && scenarioResult.recordings.Length > 0)
            {
                for (var recordingIndex = 0; recordingIndex < scenarioResult.recordings.Length; recordingIndex++)
                {
                    var sourceDirectoryPath = scenarioResult.recordings[recordingIndex];
                    if (string.IsNullOrEmpty(sourceDirectoryPath) || !Directory.Exists(sourceDirectoryPath))
                    {
                        continue;
                    }

                    sourceDirectoryPaths.Add(sourceDirectoryPath);
                }
            }
            else
            {
                CollectWindowRecordings(runStartedAt, runFinishedAt, sourceDirectoryPaths);
            }

            var copiedRecordingNames = new List<string>(sourceDirectoryPaths.Count);
            for (var sourceIndex = 0; sourceIndex < sourceDirectoryPaths.Count; sourceIndex++)
            {
                var sourceDirectoryPath = sourceDirectoryPaths[sourceIndex];
                var directoryName = Path.GetFileName(sourceDirectoryPath);
                var destinationDirectoryPath = Path.Combine(outputDirectoryPath, directoryName);
                RunArchiveFileStorage.CopyDirectory(sourceDirectoryPath, destinationDirectoryPath);
                RunArchiveReferenceRewriter.RewriteRecordingManifest(destinationDirectoryPath, directoryName);
                copiedRecordingNames.Add(directoryName);
            }

            return copiedRecordingNames.ToArray();
        }

        /// <summary>明示された例外記録を優先し、なければラン期間から選択します。</summary>
        internal static string[] CopyForensics(RunArchiveScenarioResult scenarioResult, DateTimeOffset runStartedAt, DateTimeOffset runFinishedAt, string outputDirectoryPath)
        {
            var sourceDirectoryPaths = new List<string>();
            if (scenarioResult != null && scenarioResult.exceptions != null && scenarioResult.exceptions.Length > 0)
            {
                for (var forensicIndex = 0; forensicIndex < scenarioResult.exceptions.Length; forensicIndex++)
                {
                    var sourceDirectoryPath = scenarioResult.exceptions[forensicIndex];
                    if (string.IsNullOrEmpty(sourceDirectoryPath) || !Directory.Exists(sourceDirectoryPath))
                    {
                        continue;
                    }

                    sourceDirectoryPaths.Add(sourceDirectoryPath);
                }
            }
            else
            {
                CollectWindowForensics(runStartedAt, runFinishedAt, sourceDirectoryPaths);
            }

            var copiedDirectoryNames = new List<string>(sourceDirectoryPaths.Count);
            for (var sourceIndex = 0; sourceIndex < sourceDirectoryPaths.Count; sourceIndex++)
            {
                var sourceDirectoryPath = sourceDirectoryPaths[sourceIndex];
                var directoryName = Path.GetFileName(sourceDirectoryPath);
                var destinationDirectoryPath = Path.Combine(outputDirectoryPath, directoryName);
                RunArchiveFileStorage.CopyDirectory(sourceDirectoryPath, destinationDirectoryPath);
                copiedDirectoryNames.Add(directoryName);
            }

            return copiedDirectoryNames.ToArray();
        }

        /// <summary>ラン期間の探索成果物を配送します。</summary>
        internal static void CopyMonkeyRuns(DateTimeOffset runStartedAt, DateTimeOffset runFinishedAt, string outputDirectoryPath)
        {
            var monkeyRootDirectoryPath = Path.Combine(DebugOutputPath.DirectoryPath, MonkeySourceDirectoryName);
            if (!Directory.Exists(monkeyRootDirectoryPath))
            {
                return;
            }

            Directory.CreateDirectory(outputDirectoryPath);
            var candidateDirectoryPaths = Directory.GetDirectories(monkeyRootDirectoryPath, "*", SearchOption.TopDirectoryOnly);
            Array.Sort(candidateDirectoryPaths, StringComparer.Ordinal);
            for (var directoryIndex = 0; directoryIndex < candidateDirectoryPaths.Length; directoryIndex++)
            {
                var candidateDirectoryPath = candidateDirectoryPaths[directoryIndex];
                if (!RunArchiveFileStorage.IsArtifactInWindow(candidateDirectoryPath, runStartedAt, runFinishedAt))
                {
                    continue;
                }

                var destinationDirectoryPath = Path.Combine(outputDirectoryPath, Path.GetFileName(candidateDirectoryPath));
                RunArchiveFileStorage.CopyDirectory(candidateDirectoryPath, destinationDirectoryPath);
            }
        }

        private static void CollectWindowRecordings(DateTimeOffset runStartedAt, DateTimeOffset runFinishedAt, List<string> sourceDirectoryPaths)
        {
            var recordingRootDirectoryPath = Path.Combine(DebugOutputPath.DirectoryPath, RecordingSourceDirectoryName);
            if (!Directory.Exists(recordingRootDirectoryPath))
            {
                return;
            }

            var candidateDirectoryPaths = Directory.GetDirectories(recordingRootDirectoryPath, "*", SearchOption.TopDirectoryOnly);
            Array.Sort(candidateDirectoryPaths, StringComparer.Ordinal);
            for (var directoryIndex = 0; directoryIndex < candidateDirectoryPaths.Length; directoryIndex++)
            {
                var candidateDirectoryPath = candidateDirectoryPaths[directoryIndex];
                var directoryName = Path.GetFileName(candidateDirectoryPath);
                if (directoryName == "_current")
                {
                    continue;
                }

                if (!RunArchiveFileStorage.IsArtifactInWindow(candidateDirectoryPath, runStartedAt, runFinishedAt))
                {
                    continue;
                }

                sourceDirectoryPaths.Add(candidateDirectoryPath);
            }
        }

        private static void CollectWindowForensics(DateTimeOffset runStartedAt, DateTimeOffset runFinishedAt, List<string> sourceDirectoryPaths)
        {
            var forensicRootDirectoryPath = Path.Combine(DebugOutputPath.DirectoryPath, ForensicsSourceDirectoryName);
            if (!Directory.Exists(forensicRootDirectoryPath))
            {
                return;
            }

            var candidateDirectoryPaths = Directory.GetDirectories(forensicRootDirectoryPath, "*", SearchOption.TopDirectoryOnly);
            Array.Sort(candidateDirectoryPaths, StringComparer.Ordinal);
            for (var directoryIndex = 0; directoryIndex < candidateDirectoryPaths.Length; directoryIndex++)
            {
                var candidateDirectoryPath = candidateDirectoryPaths[directoryIndex];
                if (!RunArchiveFileStorage.IsArtifactInWindow(candidateDirectoryPath, runStartedAt, runFinishedAt))
                {
                    continue;
                }

                sourceDirectoryPaths.Add(candidateDirectoryPath);
            }
        }
    }
}
