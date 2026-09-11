#if UNITY_EDITOR
using System;
using NUnit.Framework;

namespace UniTestify.Tests
{
    /// <summary>省略値と実時間の待機引数の契約を検証します。</summary>
    public sealed class AiCommandArgumentsTest
    {
        /// <summary>既存の要求はフォーカス変更を指定しないまま処理できます。</summary>
        [TestCase("capture")]
        [TestCase("agent.observe")]
        public void ViewDefaultsToEmpty(string operation)
        {
            var context = new AiCommandContext(new AiCommandRequest { op = operation, args = "{}" });
            Assert.That(context.Arguments.view, Is.Empty);
        }

        /// <summary>両方の対象操作で同じ view の検証を適用します。</summary>
        [TestCase("capture")]
        [TestCase("agent.observe")]
        public void InvalidViewThrowsArgumentException(string operation)
        {
            Assert.Throws<ArgumentException>(() => new AiCommandContext(
                new AiCommandRequest { op = operation, args = "{\"view\":\"other\"}" }));
        }

        /// <summary>JSON で省略しても初期値が保持されることを保証します。</summary>
        [Test]
        public void ReadyTimeoutDefaultsToFiveSeconds()
        {
            var context = new AiCommandContext(new AiCommandRequest { op = "agent.act", args = "{}" });
            Assert.That(context.Arguments.readyTimeoutSeconds, Is.EqualTo(5f));
        }

        /// <summary>不正値を入力実行前に拒否します。</summary>
        [Test]
        public void NegativeReadyTimeoutThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AiCommandContext(
                new AiCommandRequest { op = "agent.act", args = "{\"readyTimeoutSeconds\":-1}" }));
        }

        /// <summary>即時判定を指定できるようにします。</summary>
        [Test]
        public void ZeroReadyTimeoutIsAccepted()
        {
            var context = new AiCommandContext(new AiCommandRequest { op = "agent.act", args = "{\"readyTimeoutSeconds\":0}" });
            Assert.That(context.Arguments.readyTimeoutSeconds, Is.Zero);
        }

        /// <summary>準備待ちと落ち着き待ちで同じ不正値を拒否します。</summary>
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(-1f)]
        public void InvalidDurationThrowsArgumentOutOfRangeException(float seconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => AiCommandArguments.ValidateDuration(seconds, "seconds", false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AiSettleWait(new AiCommandArguments { settleSeconds = seconds }));
        }
    }
}
#endif
