#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace UniTestify.Tests
{
    /// <summary>Unity のコンパイルや PlayMode を使わず、実装型の選別と登録を検証します。</summary>
    public sealed class GameAdapterTypeBinderTest
    {
        private const int AllAdapterKindCount = 3;
        private const string ConstructorFailureMessage = "アダプタ初期化に失敗しました";
        private IGameStateProvider _previousStateProvider;
        private IGameBusyProvider _previousBusyProvider;
        private IGameCommandHandler _previousCommandHandler;

        /// <summary>利用側の登録を保護し、各テストを未登録状態で始めます。</summary>
        [SetUp]
        public void SetUp()
        {
            _previousStateProvider = GameAdapterRegistry.StateProvider;
            _previousBusyProvider = GameAdapterRegistry.BusyProvider;
            _previousCommandHandler = GameAdapterRegistry.CommandHandler;
            GameAdapterRegistry.StateProvider = null;
            GameAdapterRegistry.BusyProvider = null;
            GameAdapterRegistry.CommandHandler = null;
        }

        /// <summary>他のテストや利用側へ登録状態を漏らさないよう復元します。</summary>
        [TearDown]
        public void TearDown()
        {
            GameAdapterRegistry.StateProvider = _previousStateProvider;
            GameAdapterRegistry.BusyProvider = _previousBusyProvider;
            GameAdapterRegistry.CommandHandler = _previousCommandHandler;
        }

        /// <summary>テスト内の三つの実装型が対応する登録窓口へ届きます。</summary>
        [Test]
        public void RegistersAllThreeAdapterKinds()
        {
            var registeredCount = GameAdapterTypeBinder.Bind(new[]
            {
                typeof(StateProvider), typeof(BusyProvider), typeof(CommandHandler),
            });

            Assert.That(registeredCount, Is.EqualTo(AllAdapterKindCount));
            Assert.That(GameAdapterRegistry.StateProvider, Is.TypeOf<StateProvider>());
            Assert.That(GameAdapterRegistry.BusyProvider, Is.TypeOf<BusyProvider>());
            Assert.That(GameAdapterRegistry.CommandHandler, Is.TypeOf<CommandHandler>());
            Assert.That(GameAdapterRegistry.IsGameBusy(out var reason), Is.True);
            Assert.That(reason, Is.EqualTo("adapter-test"));
        }

        /// <summary>複数契約を実装する型は一つのインスタンスを各窓口で共有します。</summary>
        [Test]
        public void RegistersOneInstanceForMultipleContracts()
        {
            var registeredCount = GameAdapterTypeBinder.Bind(new[] { typeof(CombinedAdapter) });

            Assert.That(registeredCount, Is.EqualTo(1));
            Assert.That(GameAdapterRegistry.StateProvider, Is.TypeOf<CombinedAdapter>());
            Assert.That(GameAdapterRegistry.BusyProvider, Is.SameAs(GameAdapterRegistry.StateProvider));
            Assert.That(GameAdapterRegistry.CommandHandler, Is.SameAs(GameAdapterRegistry.StateProvider));
        }

        /// <summary>同じ要求内と繰り返し要求の重複型は再登録しません。</summary>
        [Test]
        public void SkipsDuplicateTypesWithinAndAcrossCalls()
        {
            var types = new[] { typeof(StateProvider), typeof(BusyProvider), typeof(CommandHandler) };
            GameAdapterTypeBinder.Bind(types);
            var stateProvider = GameAdapterRegistry.StateProvider;
            var busyProvider = GameAdapterRegistry.BusyProvider;
            var commandHandler = GameAdapterRegistry.CommandHandler;

            Assert.That(GameAdapterTypeBinder.Bind(types), Is.Zero);
            Assert.That(GameAdapterRegistry.StateProvider, Is.SameAs(stateProvider));
            Assert.That(GameAdapterRegistry.BusyProvider, Is.SameAs(busyProvider));
            Assert.That(GameAdapterRegistry.CommandHandler, Is.SameAs(commandHandler));

            GameAdapterRegistry.BusyProvider = null;
            Assert.That(GameAdapterTypeBinder.Bind(new[] { typeof(BusyProvider), typeof(BusyProvider) }), Is.EqualTo(1));
        }

        /// <summary>利用側が事前に同じ型を登録していても、そのインスタンスを維持します。</summary>
        [Test]
        public void PreservesPreviouslyRegisteredInstance()
        {
            var adapter = new CombinedAdapter();
            GameAdapterRegistry.StateProvider = adapter;
            GameAdapterRegistry.BusyProvider = adapter;
            GameAdapterRegistry.CommandHandler = adapter;

            Assert.That(GameAdapterTypeBinder.Bind(new[] { typeof(CombinedAdapter) }), Is.Zero);
            Assert.That(GameAdapterRegistry.StateProvider, Is.SameAs(adapter));
            Assert.That(GameAdapterRegistry.BusyProvider, Is.SameAs(adapter));
            Assert.That(GameAdapterRegistry.CommandHandler, Is.SameAs(adapter));
        }

        /// <summary>契約外・抽象型・未確定の総称型・公開引数なしコンストラクタを持たない型は無視します。</summary>
        [Test]
        public void IgnoresNonApplicableTypes()
        {
            var registeredCount = GameAdapterTypeBinder.Bind(new[]
            {
                typeof(object), typeof(string), typeof(IGameBusyProvider), typeof(AbstractBusyProvider),
                typeof(GenericBusyProvider<>), typeof(ParameterizedBusyProvider), typeof(PrivateConstructorBusyProvider),
            });

            Assert.That(registeredCount, Is.Zero);
            Assert.That(GameAdapterRegistry.StateProvider, Is.Null);
            Assert.That(GameAdapterRegistry.BusyProvider, Is.Null);
            Assert.That(GameAdapterRegistry.CommandHandler, Is.Null);
        }

        /// <summary>コンストラクタの例外を握らず、Loader が原文を応答へ戻せるようにします。</summary>
        [Test]
        public void PreservesConstructorException()
        {
            var exception = Assert.Throws<TargetInvocationException>(
                () => GameAdapterTypeBinder.Bind(new[] { typeof(ThrowingBusyProvider) }));

            Assert.That(exception.InnerException.Message, Is.EqualTo(ConstructorFailureMessage));
            Assert.That(GameAdapterRegistry.BusyProvider, Is.Null);
        }

        /// <summary>状態取得だけの登録対象です。</summary>
        private sealed class StateProvider : IGameStateProvider
        {
            /// <summary>注入条件である公開の引数なし構築を提供します。</summary>
            public StateProvider() { }

            /// <summary>ゲーム依存を持たない状態を提供します。</summary>
            public IReadOnlyDictionary<string, object> GetState() => new Dictionary<string, object>();
        }

        /// <summary>busy 判定だけの登録対象です。</summary>
        private sealed class BusyProvider : IGameBusyProvider
        {
            /// <summary>注入条件である公開の引数なし構築を提供します。</summary>
            public BusyProvider() { }

            /// <summary>登録が判定に反映されたことを識別します。</summary>
            public bool IsBusy => true;

            /// <summary>登録が観測理由へ反映されたことを識別します。</summary>
            public string Reason => "adapter-test";
        }

        /// <summary>ゲームコマンドだけの登録対象です。</summary>
        private sealed class CommandHandler : IGameCommandHandler
        {
            /// <summary>注入条件である公開の引数なし構築を提供します。</summary>
            public CommandHandler() { }

            /// <summary>登録テストではゲーム操作を公開しません。</summary>
            public IReadOnlyList<string> CommandNames => Array.Empty<string>();

            /// <summary>登録テストがゲームへ副作用を持たないよう拒否します。</summary>
            public bool TryExecute(string commandName, IReadOnlyDictionary<string, string> arguments, out string message)
            {
                message = string.Empty;
                return false;
            }
        }

        /// <summary>複数契約でも一度だけ構築することを確かめる対象です。</summary>
        private sealed class CombinedAdapter : IGameStateProvider, IGameBusyProvider, IGameCommandHandler
        {
            /// <summary>注入条件である公開の引数なし構築を提供します。</summary>
            public CombinedAdapter() { }

            /// <summary>ゲーム依存を持たない状態を提供します。</summary>
            public IReadOnlyDictionary<string, object> GetState() => new Dictionary<string, object>();

            /// <summary>登録だけを確認するため busy を持ちません。</summary>
            public bool IsBusy => false;

            /// <summary>busy を持たないため理由は空です。</summary>
            public string Reason => string.Empty;

            /// <summary>登録テストではゲーム操作を公開しません。</summary>
            public IReadOnlyList<string> CommandNames => Array.Empty<string>();

            /// <summary>登録テストがゲームへ副作用を持たないよう拒否します。</summary>
            public bool TryExecute(string commandName, IReadOnlyDictionary<string, string> arguments, out string message)
            {
                message = string.Empty;
                return false;
            }
        }

        /// <summary>契約を満たしても抽象型は構築しないことを確認します。</summary>
        private abstract class AbstractBusyProvider : IGameBusyProvider
        {
            /// <summary>型の選別だけを確認するため busy を持ちません。</summary>
            public bool IsBusy => false;

            /// <summary>busy を持たないため理由は空です。</summary>
            public string Reason => string.Empty;
        }

        /// <summary>型引数が未確定の実装を構築しないことを確認します。</summary>
        private sealed class GenericBusyProvider<TValue> : AbstractBusyProvider
        {
            /// <summary>コンストラクタの有無とは独立して総称型を除外させます。</summary>
            public GenericBusyProvider() { }
        }

        /// <summary>構築引数が必要な実装を無理に注入しないことを確認します。</summary>
        private sealed class ParameterizedBusyProvider : AbstractBusyProvider
        {
            /// <summary>引数を解決する仕組みが注入に不要であることを確認します。</summary>
            public ParameterizedBusyProvider(string reason) { }
        }

        /// <summary>非公開コンストラクタを注入の対象にしないことを確認します。</summary>
        private sealed class PrivateConstructorBusyProvider : AbstractBusyProvider
        {
            private PrivateConstructorBusyProvider() { }
        }

        /// <summary>登録失敗の原文を保持するための対象です。</summary>
        private sealed class ThrowingBusyProvider : AbstractBusyProvider
        {
            /// <summary>呼び出し元が原文を回収できるよう、識別可能な例外を発生させます。</summary>
            public ThrowingBusyProvider()
            {
                throw new InvalidOperationException(ConstructorFailureMessage);
            }
        }
    }
}
#endif
