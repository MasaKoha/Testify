#if UNITY_EDITOR
using System;
using NUnit.Framework;

namespace UniTestify.Tests
{
    /// <summary>行動キーと省略値、実時間の待機引数の契約を検証します。</summary>
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

        /// <summary>expect の語彙を行動へ流用した誤りを、修正方法付きで拒否します。</summary>
        [TestCase("{\"action\":{\"kind\":\"click\",\"target\":\"SettingsButton\"}}", "kind, target")]
        [TestCase("{\"action\":{\"kind\":\"click\"}}", "kind")]
        [TestCase("{\"action\":{\"target\":\"SettingsButton\"}}", "target")]
        [TestCase("{\"steps\":[{\"click\":\"SettingsButton\"},{\"kind\":\"click\",\"target\":\"SettingsButton\"}]}", "kind, target")]
        [TestCase("{\"action\":{\"kind\":\"click\",\"target\":\"SettingsButton\"},\"expect\":[{\"kind\":\"focused\",\"target\":\"SettingsButton\"}]}", "kind, target")]
        public void ExpectationVocabularyInActionIsRejected(string arguments, string receivedKeys)
        {
            var context = new AiCommandContext(new AiCommandRequest { op = "agent.act", args = arguments });

            var exception = Assert.Throws<ArgumentException>(() => context.GetActions());

            Assert.That(exception.Message, Is.EqualTo(
                $"行動に解釈できるキーがありません（受け取ったキー: {receivedKeys}）。\n" +
                "kind / target は expect の語彙です。行動はフィールド名で指定します（例: {\"click\": \"対象名\"}）。"));
        }

        /// <summary>綴り違いや入れ子のキーを行動フィールドと誤認せず、受信キーと修正例を返します。</summary>
        [TestCase("{\"clic\":\"SettingsButton\"}", "clic")]
        [TestCase("{\"Click\":\"SettingsButton\"}", "Click")]
        [TestCase("{\"metadata\":{\"click\":\"SettingsButton\"}}", "metadata")]
        public void UnknownActionKeysReportCorrection(string actionJson, string receivedKeys)
        {
            var context = new AiCommandContext(new AiCommandRequest { op = "agent.act", args = "{\"action\":" + actionJson + "}" });

            var exception = Assert.Throws<ArgumentException>(() => context.GetActions());

            Assert.That(exception.Message, Is.EqualTo(
                $"行動に解釈できるキーがありません（受け取ったキー: {receivedKeys}）。\n" +
                "行動はフィールド名で指定します（例: {\"click\": \"対象名\"}）。"));
        }

        /// <summary>正しい行動キーがあれば、未知キーが混在していても既存の入力を保持します。</summary>
        [TestCase("{\"action\":{\"click\":\"SettingsButton\"}}")]
        [TestCase("{\"action\":{\"kind\":\"click\",\"target\":\"SettingsButton\",\"click\":\"SettingsButton\"}}")]
        [TestCase("{\"steps\":[{\"click\":\"SettingsButton\"}]}")]
        public void KnownActionKeyIsAccepted(string arguments)
        {
            var context = new AiCommandContext(new AiCommandRequest { op = "agent.act", args = arguments });

            var actions = context.GetActions();

            Assert.That(actions[0].click, Is.EqualTo("SettingsButton"));
        }

        /// <summary>空オブジェクトや入力を伴わない実在フィールドを、未知キーだけの誤用と区別します。</summary>
        [TestCase("{\"action\":{}}")]
        [TestCase("{\"steps\":[{}]}")]
        [TestCase("{\"action\":{\"click\":\"\"}}")]
        [TestCase("{\"action\":{\"timeoutSeconds\":30}}")]
        [TestCase("{\"action\":{\"expect\":[{\"kind\":\"focused\",\"target\":\"SettingsButton\"}]}}")]
        [TestCase("{\"action\":{},\"expect\":[{\"kind\":\"focused\",\"target\":\"SettingsButton\"}],\"settleSeconds\":0}")]
        public void EmptyOrNonInputActionIsAccepted(string arguments)
        {
            var context = new AiCommandContext(new AiCommandRequest { op = "agent.act", args = arguments });

            Assert.DoesNotThrow(() => context.GetActions());
        }
    }
}
#endif
