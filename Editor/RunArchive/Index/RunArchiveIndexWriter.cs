using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UniTestify.Editor
{
    /// <summary>保存済みランの概要から降順の索引を再構築します。</summary>
    internal static class RunArchiveIndexWriter
    {
        private const string VerificationRunsDirectoryName = "VerificationRuns";
        private const string RunDirectoryPrefix = "run-";
        private const string MetaFileName = "meta.json";
        private const string IndexFileName = "index.json";

        /// <summary>
        /// 配下ランから索引を再生成し、ギャラリーが常に最新の一覧を読める状態を保つ。
        /// </summary>
        internal static string RebuildIndex()
        {
            var verificationRunsDirectoryPath = Path.Combine(RunArchiveFileStorage.GetProjectRootDirectoryPath(), VerificationRunsDirectoryName);
            Directory.CreateDirectory(verificationRunsDirectoryPath);

            var runDirectoryPaths = Directory.GetDirectories(verificationRunsDirectoryPath, $"{RunDirectoryPrefix}*", SearchOption.TopDirectoryOnly);
            Array.Sort(runDirectoryPaths, StringComparer.Ordinal);
            Array.Reverse(runDirectoryPaths);

            var entries = new List<RunArchiveIndexEntry>(runDirectoryPaths.Length);
            for (var directoryIndex = 0; directoryIndex < runDirectoryPaths.Length; directoryIndex++)
            {
                var runDirectoryPath = runDirectoryPaths[directoryIndex];
                var metaFilePath = Path.Combine(runDirectoryPath, MetaFileName);
                if (!File.Exists(metaFilePath))
                {
                    continue;
                }

                var metaJson = File.ReadAllText(metaFilePath);
                var meta = JsonUtility.FromJson<RunArchiveMeta>(metaJson);
                if (meta == null)
                {
                    continue;
                }

                var runDirectoryName = Path.GetFileName(runDirectoryPath);
                var relativeRunPath = RunArchiveFileStorage.NormalizePathForJson(runDirectoryName);
                var relativeMetaPath = RunArchiveFileStorage.NormalizePathForJson(Path.Combine(runDirectoryName, MetaFileName));
                entries.Add(new RunArchiveIndexEntry(
                    runDirectoryName,
                    relativeRunPath,
                    relativeMetaPath,
                    meta.scenario,
                    meta.verdict,
                    meta.startedAt,
                    meta.finishedAt,
                    meta.durationSeconds));
            }

            var index = new RunArchiveIndex(DateTimeOffset.Now.ToString("o"), entries.ToArray());
            var indexFilePath = Path.Combine(verificationRunsDirectoryPath, IndexFileName);
            File.WriteAllText(indexFilePath, JsonUtility.ToJson(index, true));
            return indexFilePath;
        }
    }
}
