#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using UnityEngine;

namespace UniTestify
{
    /// <summary>スナップショット JSON の出力先と命名規則を維持します。</summary>
    internal static class UiSnapshotStorage
    {
        private const string SnapshotDirectoryName = "snapshots";
        private const string FileNamePrefix = "snapshot-";
        private const string FileNameTimestampFormat = "yyyyMMdd-HHmmss-fff";
        private const string FileExtension = ".json";

        /// <summary>
        /// スナップショットを JSON へ保存します。
        /// 人が見つけやすい既定出力先へ寄せて、他ツールと成果物の置き場を揃えます。
        /// </summary>
        internal static string Save(UiSnapshotDocument document, string outputDirectory = null)
        {
            var resolvedOutputDirectory = string.IsNullOrEmpty(outputDirectory)
                ? Path.Combine(DebugOutputPath.DirectoryPath, SnapshotDirectoryName)
                : outputDirectory;
            Directory.CreateDirectory(resolvedOutputDirectory);

            var filePath = Path.Combine(
                resolvedOutputDirectory,
                $"{FileNamePrefix}{DateTime.Now.ToString(FileNameTimestampFormat)}{FileExtension}");
            var json = JsonUtility.ToJson(document, true);
            File.WriteAllText(filePath, json);
            return filePath;
        }

        /// <summary>
        /// シナリオの `snapshot` 名と成果物名を一致させ、後続ツールがステップ名で証拠へ到達できるようにします。
        /// </summary>
        internal static string Save(UiSnapshotDocument document, string outputDirectory, string fileNameWithoutExtension)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension))
            {
                return Save(document, outputDirectory);
            }

            var resolvedOutputDirectory = string.IsNullOrEmpty(outputDirectory)
                ? Path.Combine(DebugOutputPath.DirectoryPath, SnapshotDirectoryName)
                : outputDirectory;
            Directory.CreateDirectory(resolvedOutputDirectory);
            var filePath = Path.Combine(resolvedOutputDirectory, $"{fileNameWithoutExtension}{FileExtension}");
            File.WriteAllText(filePath, JsonUtility.ToJson(document, true));
            return filePath;
        }
    }
}
#endif
