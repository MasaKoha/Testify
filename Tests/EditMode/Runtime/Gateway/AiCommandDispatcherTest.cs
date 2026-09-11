#if UNITY_EDITOR
using NUnit.Framework;

namespace UniTestify.Tests
{
    /// <summary>PlayMode 不要のゲートウェイ契約を検証します。</summary>
    public sealed class AiCommandDispatcherTest
    {
        /// <summary>同期入口でも不正な view を既存の失敗応答へ変換します。</summary>
        [TestCase("capture")]
        [TestCase("agent.observe")]
        public void InvalidViewReturnsFailure(string operation)
        {
            var response = AiCommandDispatcher.Execute(new AiCommandRequest { op = operation, args = "{\"view\":\"other\"}" });
            Assert.That(response.ok, Is.False);
            Assert.That(response.error, Does.Contain("view"));
            Assert.That(response.view, Is.Empty);
            Assert.That(response.path, Is.Empty);
        }

        /// <summary>非同期入口でもフォーカスや撮影より先に不正な view を拒否します。</summary>
        [TestCase("capture")]
        [TestCase("agent.observe")]
        public void AsyncInvalidViewReturnsFailure(string operation)
        {
            AiCommandResponse response = null;
            var execution = AiCommandDispatcher.ExecuteAsync(
                new AiCommandRequest { op = operation, args = "{\"view\":\"other\"}" }, result => response = result);
            try
            {
                Assert.That(execution.MoveNext(), Is.False);
                Assert.That(response.ok, Is.False);
                Assert.That(response.error, Does.Contain("view"));
                Assert.That(response.view, Is.Empty);
                Assert.That(response.path, Is.Empty);
            }
            finally
            {
                ((System.IDisposable)execution).Dispose();
            }
        }

        /// <summary>未知の操作を成功扱いしないことを保証します。</summary>
        [Test]
        public void UnknownOperationReturnsFailure()
        {
            var response = AiCommandDispatcher.Execute(new AiCommandRequest { op = "missing" });
            Assert.That(response.ok, Is.False);
            Assert.That(response.error, Is.EqualTo("unknown op"));
        }

        /// <summary>JsonUtility が寛容に読む入力も、プロトコル上は拒否します。</summary>
        [TestCase("{")]
        [TestCase("[]")]
        [TestCase("null")]
        [TestCase("{\"value\":1,}")]
        [TestCase("{} trailing")]
        [TestCase("{\"value\":01}")]
        [TestCase("{\"value\":\"\\q\"}")]
        public void InvalidArgumentsReturnFailure(string arguments)
        {
            var response = AiCommandDispatcher.Execute(new AiCommandRequest { op = "ops", args = arguments });
            Assert.That(response.ok, Is.False);
            Assert.That(response.error, Is.Not.Empty);
        }

        /// <summary>クライアントが操作一覧から必要な入口を発見できます。</summary>
        [Test]
        public void OperationsIncludeAgentAndCapture()
        {
            var response = AiCommandDispatcher.Execute(new AiCommandRequest { op = "ops" });
            Assert.That(response.ok, Is.True);
            Assert.That(response.text.Split('\n'), Is.EquivalentTo(AiCommandDispatcher.ListOps()));
            Assert.That(response.text, Does.Contain("agent.begin"));
            Assert.That(response.text, Does.Contain("adapters.load"));
            Assert.That(response.text, Does.Contain("agent.act"));
            Assert.That(response.text, Does.Contain("capture"));
            Assert.That(response.text, Does.Contain("scene.dump"));
            Assert.That(response.text, Does.Contain("scenario.run"));
            Assert.That(response.text, Does.Contain("scenario.status"));
        }

        /// <summary>Pipeline 未導入を専用の message で返します。</summary>
        [Test]
        public void AdaptersLoadWithoutPipelineReturnsMessage()
        {
            var previousLoader = GameAdapterLoader.Loader;
            try
            {
                GameAdapterLoader.Loader = null;
                var response = AiCommandDispatcher.Execute(new AiCommandRequest
                {
                    op = "adapters.load", args = "{\"directory\":\"DebugOutput/adapters\"}",
                });

                Assert.That(response.ok, Is.False);
                Assert.That(response.op, Is.EqualTo("adapters.load"));
                Assert.That(response.message, Is.EqualTo("TESTIFY_PIPELINE が無効です"));
                Assert.That(response.error, Is.Empty);
            }
            finally
            {
                GameAdapterLoader.Loader = previousLoader;
            }
        }

        /// <summary>ディレクトリと注入結果を変更せずに共通ディスパッチャから受け渡します。</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void AdaptersLoadForwardsDirectoryAndResult(bool succeeded)
        {
            const string directory = "/external/adapters";
            const string message = "元のコンパイル診断";
            var previousLoader = GameAdapterLoader.Loader;
            var receivedDirectory = string.Empty;
            try
            {
                GameAdapterLoader.Loader = requestedDirectory =>
                {
                    receivedDirectory = requestedDirectory;
                    return new GameAdapterLoadResult(succeeded, message);
                };
                var response = AiCommandDispatcher.Execute(new AiCommandRequest
                {
                    op = "adapters.load", args = "{\"directory\":\"" + directory + "\"}",
                });

                Assert.That(receivedDirectory, Is.EqualTo(directory));
                Assert.That(response.ok, Is.EqualTo(succeeded));
                Assert.That(response.message, Is.EqualTo(message));
            }
            finally
            {
                GameAdapterLoader.Loader = previousLoader;
            }
        }

        /// <summary>例外の原文と内部例外を message に残します。</summary>
        [Test]
        public void AdaptersLoadPreservesExceptionInMessage()
        {
            const string failureMessage = "元のコンストラクタ例外";
            var previousLoader = GameAdapterLoader.Loader;
            try
            {
                GameAdapterLoader.Loader = directory => throw new System.Reflection.TargetInvocationException(
                    new System.InvalidOperationException(failureMessage));
                var response = AiCommandDispatcher.Execute(new AiCommandRequest { op = "adapters.load" });

                Assert.That(response.ok, Is.False);
                Assert.That(response.message, Does.Contain(failureMessage));
                Assert.That(response.message, Does.Contain(nameof(System.InvalidOperationException)));
                Assert.That(response.error, Is.Empty);
            }
            finally
            {
                GameAdapterLoader.Loader = previousLoader;
            }
        }

        /// <summary>メールボックスの非同期入口も同じ注入処理へ渡します。</summary>
        [Test]
        public void AsyncAdaptersLoadUsesRegisteredLoader()
        {
            const string message = "注入結果";
            var previousLoader = GameAdapterLoader.Loader;
            AiCommandResponse response = null;
            var execution = AiCommandDispatcher.ExecuteAsync(new AiCommandRequest { op = "adapters.load" }, result => response = result);
            try
            {
                GameAdapterLoader.Loader = directory => new GameAdapterLoadResult(true, message);
                Assert.That(execution.MoveNext(), Is.False);
                Assert.That(response.ok, Is.True);
                Assert.That(response.message, Is.EqualTo(message));
            }
            finally
            {
                ((System.IDisposable)execution).Dispose();
                GameAdapterLoader.Loader = previousLoader;
            }
        }

        /// <summary>ディレクトリ逸脱や空の撮影名を撮影前に拒否します。</summary>
        [TestCase("../x")]
        [TestCase("")]
        [TestCase("x/y")]
        [TestCase("x.png")]
        [TestCase("x\n")]
        public void InvalidCaptureNameReturnsFailure(string name)
        {
            var arguments = UnityEngine.JsonUtility.ToJson(new CaptureName { name = name });
            var response = AiCommandDispatcher.Execute(new AiCommandRequest { op = "capture", args = arguments });
            Assert.That(response.ok, Is.False);
            Assert.That(response.error, Does.Contain("name"));
            Assert.That(response.path, Is.Empty);
        }

        /// <summary>Play 外の既存拒否文言を応答に保持します。</summary>
        [Test]
        public void AgentRequiresPlayMode()
        {
            var response = AiCommandDispatcher.Execute(new AiCommandRequest { op = "agent.observe" });
            Assert.That(response.ok, Is.False);
            Assert.That(response.message, Is.EqualTo("playMode が必要です"));
        }

        /// <summary>非同期入口の引数エラーも例外を漏らさず一度だけ通知します。</summary>
        [Test]
        public void AsyncInvalidArgumentsCompleteOnce()
        {
            var completionCount = 0;
            AiCommandResponse response = null;
            var execution = AiCommandDispatcher.ExecuteAsync(
                new AiCommandRequest { op = "ops", args = "{" },
                result =>
                {
                    completionCount++;
                    response = result;
                });
            Assert.That(execution.MoveNext(), Is.False);
            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(response.ok, Is.False);
            Assert.That(response.error, Is.Not.Empty);
        }

        /// <summary>Play 外の拒否だけで通過せず、scope 自体を検証します。</summary>
        [Test]
        public void InvalidObservationScopeReturnsFailure()
        {
            var response = AiCommandDispatcher.Execute(new AiCommandRequest { op = "agent.observe", args = "{\"scope\":\"foo\"}" });
            Assert.That(response.ok, Is.False);
            Assert.That(response.error, Does.Contain("scope"));
        }

        /// <summary>PlayMode の拒否に先立って必須のシナリオパスを検証します。</summary>
        [Test]
        public void ScenarioRunWithoutPathReturnsFailure()
        {
            var response = AiCommandDispatcher.Execute(new AiCommandRequest { op = "scenario.run" });
            Assert.That(response.ok, Is.False);
            Assert.That(response.error, Does.Contain("path"));
        }

        /// <summary>メールボックス経路でもパス未指定を同じエラーへ変換します。</summary>
        [Test]
        public void AsyncScenarioRunWithoutPathReturnsFailure()
        {
            AiCommandResponse response = null;
            var execution = AiCommandDispatcher.ExecuteAsync(new AiCommandRequest { op = "scenario.run" }, result => response = result);
            Assert.That(execution.MoveNext(), Is.False);
            Assert.That(response.ok, Is.False);
            Assert.That(response.error, Does.Contain("path"));
        }

        /// <summary>操作の落ち着き待ちから独立した既定のシナリオ待機時間を使います。</summary>
        [Test]
        public void ScenarioTimeoutDefaultsToDedicatedLimit()
        {
            var context = new AiCommandContext(new AiCommandRequest { op = "scenario.run", args = "{\"path\":\"scenario.json\",\"settleTimeoutSeconds\":1}" });
            Assert.That(context.Arguments.scenarioTimeoutSeconds, Is.EqualTo(AiCommandArguments.DefaultScenarioTimeoutSeconds));
        }

        /// <summary>シーン観測の省略値を経路間で共用します。</summary>
        [Test]
        public void SceneDumpArgumentsUseSharedDefaults()
        {
            var context = new AiCommandContext(new AiCommandRequest { op = "scene.dump" });
            Assert.That(context.Arguments.depth, Is.EqualTo(SceneHierarchyDumpText.DefaultDepth));
            Assert.That(context.Arguments.maxNodes, Is.EqualTo(SceneHierarchyDumpText.DefaultMaxNodes));
            Assert.That(context.Arguments.filter, Is.Null.Or.Empty);
            Assert.That(context.Arguments.save, Is.False);
        }

        /// <summary>不正な表示上限を共通の失敗応答へ変換します。</summary>
        [TestCase("{\"depth\":-1}", "depth")]
        [TestCase("{\"maxNodes\":0}", "maxNodes")]
        public void SceneDumpRejectsInvalidLimits(string arguments, string parameterName)
        {
            var response = AiCommandDispatcher.Execute(new AiCommandRequest { op = "scene.dump", args = arguments });
            Assert.That(response.ok, Is.False);
            Assert.That(response.error, Does.Contain(parameterName));
        }

        /// <summary>PlayMode 外でも共通ディスパッチャから非 UI 階層を観測できます。</summary>
        [Test]
        public void SceneDumpReturnsHierarchyWithoutPlayMode()
        {
            const string RootName = "__UniTestifySceneDumpRoot__";
            var root = new UnityEngine.GameObject(RootName);
            try
            {
                var child = new UnityEngine.GameObject("Child");
                child.transform.SetParent(root.transform);
                root.SetActive(false);
                var response = AiCommandDispatcher.Execute(new AiCommandRequest
                {
                    op = "scene.dump", args = "{\"depth\":0,\"maxNodes\":1,\"filter\":\"" + RootName + "\"}",
                });
                Assert.That(response.ok, Is.True);
                Assert.That(response.text, Does.Contain(RootName + " activeInHierarchy=false"));
                Assert.That(response.text, Does.Not.Contain("Child"));
                Assert.That(response.path, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [System.Serializable]
        private sealed class CaptureName
        {
            /// <summary>改行を含む名前も正しく JSON エスケープして検証します。</summary>
            public string name;
        }
    }
}
#endif
