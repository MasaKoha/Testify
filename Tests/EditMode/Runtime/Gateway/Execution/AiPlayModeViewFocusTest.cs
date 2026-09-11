#if UNITY_EDITOR
using System;
using NUnit.Framework;

namespace UniTestify.Tests
{
    /// <summary>Editor ウィンドウを操作せず、フォーカスの仲介と同期撮影の契約を検証します。</summary>
    [Parallelizable(ParallelScope.None)]
    public sealed class AiPlayModeViewFocusTest
    {
        private Func<string, bool> _originalFocusHandler;

        /// <summary>Editor が登録した処理をテストから隔離します。</summary>
        [SetUp]
        public void SetUp()
        {
            _originalFocusHandler = AiPlayModeViewFocus.FocusHandler;
            AiPlayModeViewFocus.FocusHandler = null;
        }

        /// <summary>テスト後も Editor のフォーカス機能を維持します。</summary>
        [TearDown]
        public void TearDown()
        {
            AiPlayModeViewFocus.FocusHandler = _originalFocusHandler;
        }

        /// <summary>Editor の仲介が無い環境でも例外にしません。</summary>
        [TestCase("game")]
        [TestCase("simulator")]
        public void MissingHandlerReturnsFalse(string view)
        {
            Assert.That(AiPlayModeViewFocus.TryFocus(view), Is.False);
        }

        /// <summary>対象未指定なら既存のフォーカスに触れません。</summary>
        [TestCase(null)]
        [TestCase("")]
        public void EmptyViewDoesNotInvokeHandler(string view)
        {
            var called = false;
            AiPlayModeViewFocus.FocusHandler = requestedView =>
            {
                called = true;
                return true;
            };

            Assert.That(AiPlayModeViewFocus.TryFocus(view), Is.False);
            Assert.That(called, Is.False);
        }

        /// <summary>指定値と適用結果を変更せず Editor 側と受け渡します。</summary>
        [TestCase("game", true)]
        [TestCase("simulator", true)]
        [TestCase("game", false)]
        [TestCase("simulator", false)]
        public void RegisteredHandlerReceivesViewAndReturnsResult(string view, bool applied)
        {
            var receivedView = string.Empty;
            AiPlayModeViewFocus.FocusHandler = requestedView =>
            {
                receivedView = requestedView;
                return applied;
            };

            Assert.That(AiPlayModeViewFocus.TryFocus(view), Is.EqualTo(applied));
            Assert.That(receivedView, Is.EqualTo(view));
        }

        /// <summary>仲介の登録状態にかかわらず対象の誤記を拒否します。</summary>
        [TestCase("other")]
        [TestCase("Game")]
        [TestCase(" game")]
        [TestCase("simulator ")]
        public void InvalidViewThrowsArgumentException(string view)
        {
            Assert.Throws<ArgumentException>(() => AiPlayModeViewFocus.TryFocus(view));
            AiPlayModeViewFocus.FocusHandler = requestedView => throw new InvalidOperationException("不正な view を Handler に渡してはいけません。");
            Assert.Throws<ArgumentException>(() => AiPlayModeViewFocus.TryFocus(view));
        }

        /// <summary>同期経路は解像度反映前の画像を生成せず、次回の撮影に委ねます。</summary>
        [TestCase("game")]
        [TestCase("simulator")]
        public void SynchronousCaptureWithViewOnlyFocuses(string view)
        {
            var receivedView = string.Empty;
            AiPlayModeViewFocus.FocusHandler = requestedView =>
            {
                receivedView = requestedView;
                return true;
            };

            var response = AiCommandDispatcher.Execute(new AiCommandRequest
            {
                op = "capture",
                args = "{\"name\":\"focus_only\",\"view\":\"" + view + "\"}",
            });

            Assert.That(response.ok, Is.True);
            Assert.That(response.view, Is.EqualTo(view));
            Assert.That(receivedView, Is.EqualTo(view));
            Assert.That(response.path, Is.Empty);
            Assert.That(response.width, Is.Zero);
            Assert.That(response.height, Is.Zero);
            Assert.That(response.settled, Is.False);
            Assert.That(response.message, Does.Contain("次の呼び出し"));
        }
    }
}
#endif
