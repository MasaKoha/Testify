#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace UniTestify.Tests
{
    /// <summary>ファイルを作らず、Editor と実機の出力先・相対指定の基準を検証します。</summary>
    public sealed class DebugOutputPathResolveTest
    {
        private const string ProjectDirectoryName = "UniTestifyPathProject";
        private const string PersistentDirectoryName = "UniTestifyPathPersistent";
        private const string RelativeScenarioPath = "scenarios/tour.json";
        private const string RelativeCaptureDirectory = "DebugOutput/captures";

        /// <summary>Editor ではプロジェクト、実機では永続データ領域の DebugOutput を使います。</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void ResolveSelectsEnvironmentRoot(bool isEditor)
        {
            var projectRoot = Path.Combine(Path.GetTempPath(), ProjectDirectoryName);
            var persistentDataPath = Path.Combine(Path.GetTempPath(), PersistentDirectoryName);
            var expectedRoot = isEditor ? projectRoot : persistentDataPath;

            Assert.That(DebugOutputPath.Resolve(isEditor, projectRoot, persistentDataPath),
                Is.EqualTo(Path.Combine(expectedRoot, "DebugOutput")));
        }

        /// <summary>使わない環境のパスに依存せず解決できます。</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void ResolveDoesNotRequireUnusedRoot(bool isEditor)
        {
            var rootDirectory = Path.Combine(Path.GetTempPath(), ProjectDirectoryName);
            var projectRoot = isEditor ? rootDirectory : null;
            var persistentDataPath = isEditor ? null : rootDirectory;

            Assert.That(DebugOutputPath.Resolve(isEditor, projectRoot, persistentDataPath),
                Is.EqualTo(Path.Combine(rootDirectory, "DebugOutput")));
        }

        /// <summary>相対シナリオと撮影先は DebugOutput の親を基準に解決します。</summary>
        [TestCase(true, RelativeScenarioPath)]
        [TestCase(false, RelativeScenarioPath)]
        [TestCase(true, RelativeCaptureDirectory)]
        [TestCase(false, RelativeCaptureDirectory)]
        public void RelativePathUsesEnvironmentRoot(bool isEditor, string relativePath)
        {
            var projectRoot = Path.Combine(Path.GetTempPath(), ProjectDirectoryName);
            var persistentDataPath = Path.Combine(Path.GetTempPath(), PersistentDirectoryName);
            var expectedRoot = isEditor ? projectRoot : persistentDataPath;

            Assert.That(DebugOutputPath.ResolveRelative(relativePath, isEditor, projectRoot, persistentDataPath),
                Is.EqualTo(Path.GetFullPath(Path.Combine(expectedRoot, relativePath))));
        }

        /// <summary>絶対指定には環境のルートを付け足しません。</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void AbsolutePathIsPreserved(bool isEditor)
        {
            var projectRoot = Path.Combine(Path.GetTempPath(), ProjectDirectoryName);
            var persistentDataPath = Path.Combine(Path.GetTempPath(), PersistentDirectoryName);
            var absolutePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), RelativeScenarioPath));

            Assert.That(DebugOutputPath.ResolveRelative(absolutePath, isEditor, projectRoot, persistentDataPath),
                Is.EqualTo(absolutePath));
        }

        /// <summary>親ディレクトリ指定を正規化し、カレントディレクトリへ依存させません。</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void RelativeParentSegmentsAreNormalized(bool isEditor)
        {
            var projectRoot = Path.Combine(Path.GetTempPath(), ProjectDirectoryName);
            var persistentDataPath = Path.Combine(Path.GetTempPath(), PersistentDirectoryName);
            var expectedRoot = isEditor ? projectRoot : persistentDataPath;
            var relativePath = Path.Combine("scenarios", "..", RelativeScenarioPath);

            Assert.That(DebugOutputPath.ResolveRelative(relativePath, isEditor, projectRoot, persistentDataPath),
                Is.EqualTo(Path.GetFullPath(Path.Combine(expectedRoot, RelativeScenarioPath))));
        }

        /// <summary>公開入口でも従来の Editor のプロジェクト基準を維持します。</summary>
        [Test]
        public void EditorPropertiesUseProjectRoot()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);

            Assert.That(DebugOutputPath.DirectoryPath, Is.EqualTo(Path.Combine(projectRoot, "DebugOutput")));
            Assert.That(DebugOutputPath.ResolveRelative(RelativeScenarioPath),
                Is.EqualTo(Path.GetFullPath(Path.Combine(projectRoot, RelativeScenarioPath))));
        }
    }
}
#endif
