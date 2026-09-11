#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;

namespace UniTestify
{
    /// <summary>コンパイラに依存せず、生成型からゲーム接続契約の実装を選んで登録します。</summary>
    internal static class GameAdapterTypeBinder
    {
        /// <summary>公開の引数なしコンストラクタを持つ実装型を登録し、登録した型数を返します。</summary>
        internal static int Bind(Type[] types)
        {
            // perf: 型の列挙と重複判定用の確保は注入要求時だけ行う。
            var registeredTypeNames = new HashSet<string>(StringComparer.Ordinal);
            AddRegisteredTypeName(registeredTypeNames, GameAdapterRegistry.StateProvider);
            AddRegisteredTypeName(registeredTypeNames, GameAdapterRegistry.BusyProvider);
            AddRegisteredTypeName(registeredTypeNames, GameAdapterRegistry.CommandHandler);

            var registeredCount = 0;
            foreach (var type in types)
            {
                if (!IsAdapterType(type) || registeredTypeNames.Contains(type.FullName))
                {
                    continue;
                }

                var constructor = type.GetConstructor(Type.EmptyTypes);
                if (constructor == null)
                {
                    continue;
                }

                var adapter = constructor.Invoke(Array.Empty<object>());
                Register(adapter);
                registeredTypeNames.Add(type.FullName);
                registeredCount++;
            }

            return registeredCount;
        }

        private static bool IsAdapterType(Type type)
        {
            return type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters
                && (typeof(IGameStateProvider).IsAssignableFrom(type)
                    || typeof(IGameBusyProvider).IsAssignableFrom(type)
                    || typeof(IGameCommandHandler).IsAssignableFrom(type));
        }

        private static void AddRegisteredTypeName(HashSet<string> registeredTypeNames, object adapter)
        {
            if (adapter != null)
            {
                // 注入のたびにアセンブリ名が変わるため、型の同一性は名前空間を含む名前で比較する。
                registeredTypeNames.Add(adapter.GetType().FullName);
            }
        }

        private static void Register(object adapter)
        {
            if (adapter is IGameStateProvider stateProvider)
            {
                GameAdapterRegistry.StateProvider = stateProvider;
            }

            if (adapter is IGameBusyProvider busyProvider)
            {
                GameAdapterRegistry.BusyProvider = busyProvider;
            }

            if (adapter is IGameCommandHandler commandHandler)
            {
                GameAdapterRegistry.CommandHandler = commandHandler;
            }
        }
    }
}
#endif
