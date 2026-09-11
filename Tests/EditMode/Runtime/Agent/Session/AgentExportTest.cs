#if UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace UniTestify.Tests
{
    /// <summary>実入力を使わず、探索履歴から回帰 JSON への変換を検証します。</summary>
    public sealed class AgentExportTest
    {
        private string _outputDirectory;
        private AgentSessionArtifacts _artifacts;

        /// <summary>既存セッションの成果物と衝突しない一時出力先を使います。</summary>
        [SetUp]
        public void SetUp()
        {
            _outputDirectory = Path.Combine(Path.GetTempPath(), "unilab-agent-export-" + Guid.NewGuid().ToString("N"));
            var goal = new AgentGoal { freePlay = true };
            var options = new AgentOptions();
            _artifacts = new AgentSessionArtifacts(goal, options, new AgentSessionGuards(goal, options), new AgentExpectationEvaluator(), _outputDirectory);
            _artifacts.Initialize();
        }

        /// <summary>テストが作った成果物だけを片付けます。</summary>
        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_outputDirectory, true);
        }

        /// <summary>freePlay でも指定した事後条件を省略・追加せず再生用 JSON へ写します。</summary>
        [Test]
        public void FreePlayExportPreservesExpectationsAndCountsSteps()
        {
            var expectations = new[] { new ScenarioExpectation { kind = "sceneIs", value = "Game" } };
            _artifacts.RecordScenarioStep(new AgentAction { submit = "Start", expect = expectations });
            _artifacts.RecordScenarioStep(new AgentAction { press = "east" });
            var path = _artifacts.ExportAsScenario("regression");
            var scenario = JsonUtility.FromJson<UiScenario>(File.ReadAllText(path));
            Assert.That(Path.IsPathRooted(path), Is.True);
            Assert.That(scenario.steps, Has.Length.EqualTo(2));
            Assert.That(JsonUtility.ToJson(scenario.steps[0].expect[0]), Is.EqualTo(JsonUtility.ToJson(expectations[0])));
            Assert.That(scenario.steps[0].expect, Has.Length.EqualTo(1));
            Assert.That(scenario.steps[1].expect, Is.Null.Or.Empty);
            Assert.That(scenario.steps[0].comment, Is.Null.Or.Empty);
            Assert.That(_artifacts.ExportSummary, Is.EqualTo("steps=2 expectSteps=1"));
        }

        /// <summary>未達の事後条件も残し、元の実行との比較に必要な注記を付けます。</summary>
        [Test]
        public void FailedExpectationExportsOriginalStepWithComment()
        {
            var expectations = new[] { new ScenarioExpectation { kind = "sceneIs", value = "Game" } };
            var previousStepCount = _artifacts.StepCount;
            _artifacts.RecordScenarioStep(new AgentAction { submit = "Start", expect = expectations });
            var response = new AiCommandResponse { ok = true };
            var snapshot = new UiSnapshotDocument { activeScene = "Menu" };
            AgentActExpectation.Apply(response, expectations, snapshot, snapshot);
            _artifacts.RecordActExpectation(previousStepCount, response.expectOk);
            var scenario = JsonUtility.FromJson<UiScenario>(File.ReadAllText(_artifacts.ExportAsScenario("failed")));
            Assert.That(scenario.steps, Has.Length.EqualTo(1));
            Assert.That(scenario.steps[0].submit, Is.EqualTo("Start"));
            Assert.That(scenario.steps[0].expect[0].value, Is.EqualTo("Game"));
            Assert.That(scenario.steps[0].comment, Is.EqualTo("元の実行では未達"));
        }

        /// <summary>実行されなかった手の評価で直前の成功を未達へ書き換えません。</summary>
        [Test]
        public void UnrecordedActionDoesNotAnnotatePreviousStep()
        {
            _artifacts.RecordScenarioStep(new AgentAction { expect = new[] { new ScenarioExpectation { kind = "sceneIs", value = "Game" } } });
            _artifacts.RecordActExpectation(_artifacts.StepCount, false);
            var scenario = JsonUtility.FromJson<UiScenario>(File.ReadAllText(_artifacts.ExportAsScenario("unchanged")));
            Assert.That(scenario.steps[0].comment, Is.Null.Or.Empty);
        }

        /// <summary>待機だけの手も入力付きの手も、同名のフィールドで再生条件を保存します。</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void ExportPreservesWaitConditionsAndTimeout(bool hasInput)
        {
            var action = CreateWaitingAction();
            action.submit = hasInput ? "Start" : string.Empty;
            _artifacts.RecordScenarioStep(action);
            var scenario = JsonUtility.FromJson<UiScenario>(File.ReadAllText(_artifacts.ExportAsScenario("wait")));
            Assert.That(scenario.steps, Has.Length.EqualTo(1));
            var step = scenario.steps[0];
            Assert.That(step.submit, Is.EqualTo(action.submit));
            Assert.That(step.waitForText, Is.EqualTo(action.waitForText));
            Assert.That(step.waitForObject, Is.EqualTo(action.waitForObject));
            Assert.That(step.waitForFocus, Is.EqualTo(action.waitForFocus));
            Assert.That(step.waitForScene, Is.EqualTo(action.waitForScene));
            Assert.That(step.timeoutSeconds, Is.EqualTo(action.timeoutSeconds));
        }

        /// <summary>タイムアウトで拒否した要求にも、待ち条件と上限を履歴へ残します。</summary>
        [TestCase("acted")]
        [TestCase("rejected")]
        public void ActionLogPreservesWaitConditionsAndTimeout(string status)
        {
            var action = CreateWaitingAction();
            _artifacts.AppendActionLog(action, string.Empty, "wait", string.Empty, status, "待機結果", string.Empty);
            var entry = JsonUtility.FromJson<AgentActionLogEntry>(File.ReadAllText(Path.Combine(_outputDirectory, "actions.jsonl")));
            Assert.That(entry.status, Is.EqualTo(status));
            Assert.That(entry.waitForText, Is.EqualTo(action.waitForText));
            Assert.That(entry.waitForObject, Is.EqualTo(action.waitForObject));
            Assert.That(entry.waitForFocus, Is.EqualTo(action.waitForFocus));
            Assert.That(entry.waitForScene, Is.EqualTo(action.waitForScene));
            Assert.That(entry.timeoutSeconds, Is.EqualTo(action.timeoutSeconds));
            Assert.That(_artifacts.StepCount, Is.Zero);
        }

        private static AgentAction CreateWaitingAction()
        {
            const float TimeoutSeconds = 90f;
            return new AgentAction
            {
                waitForText = "準備完了", waitForObject = "Screen/Root", waitForFocus = "Start",
                waitForScene = "Game", timeoutSeconds = TimeoutSeconds,
            };
        }
    }
}
#endif
