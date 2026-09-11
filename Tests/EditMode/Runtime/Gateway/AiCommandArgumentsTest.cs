#if UNITY_EDITOR
using System;
using NUnit.Framework;

namespace UniTestify.Tests
{
    /// <summary>省略値と実時間の待機引数の契約を検証します。</summary>
    public sealed class AiCommandArgumentsTest
    {
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
        [TestCase(-1f)]
        public void InvalidDurationThrowsArgumentOutOfRangeException(float seconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AiSettleWait(new AiCommandArguments { settleSeconds = seconds }));
        }

        /// <summary>両方の対象操作で同じ view の検証を適用します。</summary>
        [TestCase("capture")]
        [TestCase("agent.observe")]
        public void InvalidViewThrowsArgumentException(string operation)
        {
            Assert.Throws<ArgumentException>(() => new AiCommandContext(
                new AiCommandRequest { op = operation, args = "{\"view\":\"other\"}" }));
        }
    }
}
#endif
