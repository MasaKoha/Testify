#if UNITY_EDITOR
using System;
using NUnit.Framework;

namespace UniTestify.Tests
{
    /// <summary>PlayMode を使わず、自律実行の設定解釈・既定値・上書き順を検証します。</summary>
    public sealed class ScenarioAutorunConfigTest
    {
        private const string BuildScenarioPath = "scenarios/build.json";
        private const string FileScenarioPath = "scenarios/file.json";
        private const string ArgumentScenarioPath = "scenarios/argument.json";
        private const string ScenarioArgument = "-unitestify-scenario";
        private const float BuildDelaySeconds = 5f;
        private const float FileDelaySeconds = 1.5f;

        /// <summary>設定もパスもない状態では自律実行せず、待機時間は 2 秒です。</summary>
        [TestCase(null)]
        [TestCase("{}")]
        public void MissingConfigurationUsesDisabledDefaults(string configurationJson)
        {
            var configuration = ScenarioAutorun.ResolveConfiguration(configurationJson);

            Assert.That(configuration.Path, Is.Empty);
            Assert.That(configuration.Name, Is.Empty);
            Assert.That(configuration.DelaySeconds, Is.EqualTo(UniTestifySettings.DefaultAutorunDelaySeconds));
        }

        /// <summary>外部ファイルがない場合と未指定フィールドはビルド時の値を維持します。</summary>
        [TestCase(null)]
        [TestCase("{}")]
        [TestCase("{\"name\":\"tour\"}")]
        public void MissingFieldsPreserveBuildSettings(string configurationJson)
        {
            var configuration = ScenarioAutorun.ResolveConfiguration(configurationJson, BuildScenarioPath, BuildDelaySeconds);

            Assert.That(configuration.Path, Is.EqualTo(BuildScenarioPath));
            Assert.That(configuration.DelaySeconds, Is.EqualTo(BuildDelaySeconds));
        }

        /// <summary>外部 JSON でパス・結果名・待機時間を上書きできます。</summary>
        [Test]
        public void JsonOverridesBuildSettings()
        {
            var configuration = ScenarioAutorun.ResolveConfiguration(
                "{\"path\":\"scenarios/file.json\",\"name\":\"tour\",\"delaySeconds\":1.5}", BuildScenarioPath, BuildDelaySeconds);

            Assert.That(configuration.Path, Is.EqualTo(FileScenarioPath));
            Assert.That(configuration.Name, Is.EqualTo("tour"));
            Assert.That(configuration.DelaySeconds, Is.EqualTo(FileDelaySeconds));
        }

        /// <summary>パスだけのファイルでも、未指定の待機時間はビルド設定から引き継ぎます。</summary>
        [Test]
        public void PathOnlyJsonPreservesBuildDelay()
        {
            var configuration = ScenarioAutorun.ResolveConfiguration(
                "{\"path\":\"scenarios/file.json\"}", BuildScenarioPath, BuildDelaySeconds);

            Assert.That(configuration.Path, Is.EqualTo(FileScenarioPath));
            Assert.That(configuration.DelaySeconds, Is.EqualTo(BuildDelaySeconds));
        }

        /// <summary>明示した 0 秒を省略扱いに戻しません。</summary>
        [Test]
        public void ExplicitZeroOverridesBuildDelay()
        {
            var configuration = ScenarioAutorun.ResolveConfiguration("{\"delaySeconds\":0}", BuildScenarioPath, BuildDelaySeconds);

            Assert.That(configuration.Path, Is.EqualTo(BuildScenarioPath));
            Assert.That(configuration.DelaySeconds, Is.Zero);
        }

        /// <summary>ビルドにパスが含まれていても、空文字の上書きで自律実行を止められます。</summary>
        [Test]
        public void EmptyPathDisablesBuildAutorun()
        {
            var configuration = ScenarioAutorun.ResolveConfiguration("{\"path\":\"\"}", BuildScenarioPath, BuildDelaySeconds);

            Assert.That(configuration.Path, Is.Empty);
        }

        /// <summary>文字列のエスケープと日本語名をシナリオへ渡せます。</summary>
        [Test]
        public void JsonDecodesEscapedPathAndName()
        {
            var configuration = ScenarioAutorun.ResolveConfiguration(
                "{\"path\":\"scenarios\\/巡回.json\",\"name\":\"巡回 \\\"A\\\"\"}");

            Assert.That(configuration.Path, Is.EqualTo("scenarios/巡回.json"));
            Assert.That(configuration.Name, Is.EqualTo("巡回 \"A\""));
        }

        /// <summary>起動引数はパスだけを最後に上書きし、ファイルの名前・待機時間を保ちます。</summary>
        [Test]
        public void CommandLineOverridesFilePath()
        {
            var arguments = new[] { "game", "-logFile", "player.log", ScenarioArgument, ArgumentScenarioPath };
            var configuration = ScenarioAutorun.ResolveConfiguration(
                "{\"path\":\"scenarios/file.json\",\"name\":\"tour\",\"delaySeconds\":1.5}",
                BuildScenarioPath, BuildDelaySeconds, arguments);

            Assert.That(configuration.Path, Is.EqualTo(ArgumentScenarioPath));
            Assert.That(configuration.Name, Is.EqualTo("tour"));
            Assert.That(configuration.DelaySeconds, Is.EqualTo(FileDelaySeconds));
        }

        /// <summary>起動引数だけでも既定の待機時間で実行できます。</summary>
        [Test]
        public void CommandLineWorksWithoutConfigurationFile()
        {
            var configuration = ScenarioAutorun.ResolveConfiguration(null,
                commandLineArguments: new[] { "game", ScenarioArgument, ArgumentScenarioPath });

            Assert.That(configuration.Path, Is.EqualTo(ArgumentScenarioPath));
            Assert.That(configuration.DelaySeconds, Is.EqualTo(UniTestifySettings.DefaultAutorunDelaySeconds));
        }

        /// <summary>値のない起動引数や次のオプションをパスとして扱いません。</summary>
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("-logFile")]
        public void CommandLineWithoutPathIsRejected(string pathArgument)
        {
            var arguments = pathArgument == null ? new[] { "game", ScenarioArgument } : new[] { "game", ScenarioArgument, pathArgument };

            Assert.Throws<ArgumentException>(() => ScenarioAutorun.ResolveConfiguration(null, commandLineArguments: arguments));
        }

        /// <summary>存在する設定ファイルが壊れていた場合、既定設定で実行しません。</summary>
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("null")]
        [TestCase("[]")]
        [TestCase("{\"path\":")]
        public void InvalidJsonIsRejected(string configurationJson)
        {
            Assert.Throws<FormatException>(() => ScenarioAutorun.ResolveConfiguration(configurationJson));
        }

        /// <summary>JsonUtility が黙って既定値へ変換し得る型違いを拒否します。</summary>
        [TestCase("{\"path\":1}")]
        [TestCase("{\"path\":null}")]
        [TestCase("{\"name\":true}")]
        [TestCase("{\"name\":null}")]
        [TestCase("{\"delaySeconds\":\"2\"}")]
        [TestCase("{\"delaySeconds\":null}")]
        [TestCase("{\"delaySeconds\":[]}")]
        public void InvalidFieldTypesAreRejected(string configurationJson)
        {
            Assert.Throws<ArgumentException>(() => ScenarioAutorun.ResolveConfiguration(configurationJson));
        }

        /// <summary>ビルド設定の負数・NaN・無限大は無限待機の原因になるため拒否します。</summary>
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidBuildDelayIsRejected(float delaySeconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ScenarioAutorun.ResolveConfiguration(null, BuildScenarioPath, delaySeconds));
        }

        /// <summary>JSON から指定された待機時間にも同じ境界を適用します。</summary>
        [Test]
        public void NegativeJsonDelayIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ScenarioAutorun.ResolveConfiguration("{\"delaySeconds\":-1}"));
        }
    }
}
#endif
