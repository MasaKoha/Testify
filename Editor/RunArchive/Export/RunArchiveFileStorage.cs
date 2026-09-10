using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace UniTestify.Editor
{
    /// <summary>成果物の時間範囲判定とディレクトリ配送・パス規則を管理します。</summary>
    internal static class RunArchiveFileStorage
    {
        private const string RunDirectoryPrefix = "run-";
        private const string TimestampFormat = "yyyyMMdd-HHmmss";
        private const double ArtifactWindowPaddingSeconds = 300.0;

        /// <summary>更新時刻が余裕幅を含むラン期間に収まるか判定します。</summary>
        internal static bool IsArtifactInWindow(string path, DateTimeOffset runStartedAt, DateTimeOffset runFinishedAt)
        {
            DateTimeOffset lastWriteTime;
            if (File.Exists(path))
            {
                lastWriteTime = File.GetLastWriteTime(path);
            }
            else if (Directory.Exists(path))
            {
                lastWriteTime = Directory.GetLastWriteTime(path);
            }
            else
            {
                return false;
            }

            var paddedStart = runStartedAt.AddSeconds(-ArtifactWindowPaddingSeconds);
            var paddedEnd = runFinishedAt.AddSeconds(ArtifactWindowPaddingSeconds);
            return lastWriteTime >= paddedStart && lastWriteTime <= paddedEnd;
        }

        /// <summary>同秒のランを上書きしない保存先を確保します。</summary>
        internal static string CreateUniqueRunDirectory(string verificationRunsDirectoryPath, DateTimeOffset runStartedAt)
        {
            var runDirectoryName = $"{RunDirectoryPrefix}{runStartedAt.ToString(TimestampFormat, CultureInfo.InvariantCulture)}";
            var runDirectoryPath = Path.Combine(verificationRunsDirectoryPath, runDirectoryName);
            if (!Directory.Exists(runDirectoryPath))
            {
                Directory.CreateDirectory(runDirectoryPath);
                return runDirectoryPath;
            }

            for (var suffixIndex = 1; suffixIndex < 1000; suffixIndex++)
            {
                var suffixedDirectoryPath = Path.Combine(verificationRunsDirectoryPath, $"{runDirectoryName}-{suffixIndex:D2}");
                if (Directory.Exists(suffixedDirectoryPath))
                {
                    continue;
                }

                Directory.CreateDirectory(suffixedDirectoryPath);
                return suffixedDirectoryPath;
            }

            throw new IOException("RunArchive の出力先を確保できません。");
        }

        /// <summary>更新時刻が最も新しい成果物のパスを返します。</summary>
        internal static string FindLatestFile(string directoryPath, string searchPattern)
        {
            if (!Directory.Exists(directoryPath))
            {
                return string.Empty;
            }

            var candidateFilePaths = Directory.GetFiles(directoryPath, searchPattern, SearchOption.TopDirectoryOnly);
            var latestFilePath = string.Empty;
            var latestWriteTime = DateTime.MinValue;
            for (var fileIndex = 0; fileIndex < candidateFilePaths.Length; fileIndex++)
            {
                var candidateFilePath = candidateFilePaths[fileIndex];
                var lastWriteTime = File.GetLastWriteTime(candidateFilePath);
                if (lastWriteTime <= latestWriteTime)
                {
                    continue;
                }

                latestWriteTime = lastWriteTime;
                latestFilePath = candidateFilePath;
            }

            return latestFilePath;
        }

        /// <summary>子ディレクトリを含め、既存ファイルを上書きして配送します。</summary>
        internal static void CopyDirectory(string sourceDirectoryPath, string destinationDirectoryPath)
        {
            Directory.CreateDirectory(destinationDirectoryPath);
            var sourceFilePaths = Directory.GetFiles(sourceDirectoryPath, "*", SearchOption.TopDirectoryOnly);
            for (var fileIndex = 0; fileIndex < sourceFilePaths.Length; fileIndex++)
            {
                var sourceFilePath = sourceFilePaths[fileIndex];
                var destinationFilePath = Path.Combine(destinationDirectoryPath, Path.GetFileName(sourceFilePath));
                File.Copy(sourceFilePath, destinationFilePath, true);
            }

            var childDirectoryPaths = Directory.GetDirectories(sourceDirectoryPath, "*", SearchOption.TopDirectoryOnly);
            for (var directoryIndex = 0; directoryIndex < childDirectoryPaths.Length; directoryIndex++)
            {
                var childSourceDirectoryPath = childDirectoryPaths[directoryIndex];
                var childDestinationDirectoryPath = Path.Combine(destinationDirectoryPath, Path.GetFileName(childSourceDirectoryPath));
                CopyDirectory(childSourceDirectoryPath, childDestinationDirectoryPath);
            }
        }

        /// <summary>Assets の親を成果物集約の基準パスとして返します。</summary>
        internal static string GetProjectRootDirectoryPath()
        {
            var assetsDirectoryPath = Application.dataPath;
            return Path.GetDirectoryName(assetsDirectoryPath) ?? assetsDirectoryPath;
        }

        /// <summary>既存 JSON と同じ区切り文字へパスを正規化します。</summary>
        internal static string NormalizePathForJson(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }
    }
}
