#if UNITY_EDITOR
using System;
using NUnit.Framework;
using UnityEngine;

namespace UniTestify.Tests
{
    /// <summary>行動前アンカーと待機上限を PlayMode に入らず検証します。</summary>
    public sealed class AgentActionWaitTest
    {
        private const string TargetName = "__UniTestifyAgentActionWaitTarget__";
        private const float ExpectedDefaultTimeoutSeconds = 30f;

        /// <summary>各 waitFor 語彙を欠落させずシナリオと同じアンカーへ変換します。</summary>
        [TestCase("{\"waitForText\":\"準備完了\"}")]
        [TestCase("{\"waitForObject\":\"Screen/Root\"}")]
        [TestCase("{\"waitForFocus\":\"StartButton\"}")]
        [TestCase("{\"waitForScene\":\"Game\"}")]
        public void CreatesSameAnchorAsScenarioForEachWaitCondition(string json)
        {
            var action = JsonUtility.FromJson<AgentAction>(json);
            var step = JsonUtility.FromJson<UiScenarioStep>(json);
            var anchor = AgentActionWait.CreateAnchor(action);
            Assert.That(JsonUtility.ToJson(anchor), Is.EqualTo(JsonUtility.ToJson(UiScenarioStepReader.CreateAnchor(step))));
            Assert.That(AgentActionWait.HasConditions(action), Is.True);
            Assert.That(AgentActionExecutor.GetActionKind(action), Is.EqualTo("wait"));
        }

        /// <summary>複数の条件と行動を指定しても、すべての待ち条件を保持します。</summary>
        [Test]
        public void ActionKeepsAllWaitConditionsBeforeSubmit()
        {
            var action = new AgentAction
            {
                submit = "StartButton", waitForText = "準備完了", waitForObject = "Screen/Root",
                waitForFocus = "StartButton", waitForScene = "Game",
            };
            var anchor = AgentActionWait.CreateAnchor(action);
            Assert.That(anchor.waitForText, Is.EqualTo(action.waitForText));
            Assert.That(anchor.waitForObject, Is.EqualTo(action.waitForObject));
            Assert.That(anchor.waitForFocus, Is.EqualTo(action.waitForFocus));
            Assert.That(anchor.waitForScene, Is.EqualTo(action.waitForScene));
            Assert.That(AgentActionExecutor.GetActionKind(action), Is.EqualTo("submit"));
        }

        /// <summary>単一行動と一括行動の JSON 省略値をシナリオの 30 秒へ揃えます。</summary>
        [TestCase("{\"action\":{\"waitForObject\":\"Screen\"}}")]
        [TestCase("{\"steps\":[{\"waitForObject\":\"Screen\"}]}")]
        public void TimeoutDefaultsToScenarioLimit(string arguments)
        {
            var context = new AiCommandContext(new AiCommandRequest { op = "agent.act", args = arguments });
            Assert.That(new AgentAction().timeoutSeconds, Is.EqualTo(ExpectedDefaultTimeoutSeconds));
            Assert.That(context.GetActions()[0].timeoutSeconds, Is.EqualTo(ExpectedDefaultTimeoutSeconds));
            Assert.That(UiScenarioStepReader.GetTimeoutSeconds(new UiScenarioStep()), Is.EqualTo(ExpectedDefaultTimeoutSeconds));
            Assert.That(UiScenarioStepReader.GetTimeoutSeconds(new UiScenarioStep { timeoutSeconds = 0f }), Is.EqualTo(ExpectedDefaultTimeoutSeconds));
        }

        /// <summary>待機上限の明示値は既定値で上書きしません。</summary>
        [Test]
        public void ExplicitTimeoutIsPreserved()
        {
            const float TimeoutSeconds = 90f;
            var context = new AiCommandContext(new AiCommandRequest
            {
                op = "agent.act", args = "{\"action\":{\"waitForScene\":\"Game\",\"timeoutSeconds\":90}}",
            });
            Assert.That(context.GetActions()[0].timeoutSeconds, Is.EqualTo(TimeoutSeconds));
            Assert.That(UiScenarioStepReader.GetTimeoutSeconds(new UiScenarioStep { timeoutSeconds = TimeoutSeconds }), Is.EqualTo(TimeoutSeconds));
        }

        /// <summary>成立まで待てない上限値を行動送出前に拒否します。</summary>
        [TestCase("{\"action\":{\"waitForScene\":\"Game\",\"timeoutSeconds\":0}}")]
        [TestCase("{\"steps\":[{\"waitForScene\":\"Game\",\"timeoutSeconds\":-1}]}")]
        public void NonPositiveTimeoutIsRejected(string arguments)
        {
            var context = new AiCommandContext(new AiCommandRequest { op = "agent.act", args = arguments });
            Assert.Throws<ArgumentOutOfRangeException>(() => context.GetActions());
        }

        /// <summary>非アクティブな対象で継続し、アクティブ化した反復で待機だけの要求が完了します。</summary>
        [Test]
        public void WaitCompletesWhenObjectBecomesActive()
        {
            var target = new GameObject(TargetName);
            try
            {
                target.SetActive(false);
                var action = new AgentAction { waitForObject = TargetName };
                var wait = new AgentActionWait(action);
                using (var execution = wait.WaitAsync())
                {
                    Assert.That(execution.MoveNext(), Is.True);
                    Assert.That(wait.IsSatisfied, Is.False);
                    target.SetActive(true);
                    Assert.That(execution.MoveNext(), Is.False);
                    Assert.That(wait.IsSatisfied, Is.True);
                }

                var executor = new AgentActionExecutor(() => throw new InvalidOperationException("待機だけの要求は入力ドライバを使いません。"));
                Assert.That(executor.ExecuteAction(action), Is.EqualTo("待機条件が成立しました。"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        /// <summary>同期入口が未成立の条件を読み飛ばして入力を送らないことを保証します。</summary>
        [Test]
        public void SynchronousActionRejectsUnsatisfiedAnchorBeforeInput()
        {
            var context = new AiCommandContext(new AiCommandRequest
            {
                op = "agent.act", args = "{\"action\":{\"submit\":\"Start\",\"waitForObject\":\"__UniTestifyMissingWaitTarget__\"}}",
            });
            var executedCount = 0;
            var response = AiCommandDispatcher.ActImmediately(context, action =>
            {
                executedCount++;
                return new AiCommandResponse { ok = true };
            }, () => new UiSnapshotDocument());
            Assert.That(executedCount, Is.Zero);
            Assert.That(response.ok, Is.False);
            Assert.That(response.message, Does.Contain("待機条件が未成立"));
        }
    }
}
#endif
