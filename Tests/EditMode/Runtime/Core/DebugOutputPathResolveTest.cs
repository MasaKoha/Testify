#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;

namespace UniTestify.Tests
{
    /// <summary>ファイルを作らず、Editor と実機の出力先・相対指定の基準を検証します。</summary>
    public sealed class DebugOutputPathResolveTest
    {
        private const string ProjectDirectoryName = "UniTestifyPathProject";
        private const string PersistentDirectoryName = "UniTestifyPathPersistent";
        private const string RelativeScenarioPath = "scenarios/tour.json";

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

        /// <summary>相対シナリオと撮影先は DebugOutput の親を基準に解決します。</summary>
        [TestCase(true, RelativeScenarioPath)]
        [TestCase(false, RelativeScenarioPath)]
        public void RelativePathUsesEnvironmentRoot(bool isEditor, string relativePath)
        {
            var projectRoot = Path.Combine(Path.GetTempPath(), ProjectDirectoryName);
            var persistentDataPath = Path.Combine(Path.GetTempPath(), PersistentDirectoryName);
            var expectedRoot = isEditor ? projectRoot : persistentDataPath;

            Assert.That(DebugOutputPath.ResolveRelative(relativePath, isEditor, projectRoot, persistentDataPath),
                Is.EqualTo(Path.GetFullPath(Path.Combine(expectedRoot, relativePath))));
        }

        /// <summary>絶対指定には環境のルートを付け足しません。</summary>
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
    }
}
#endif
