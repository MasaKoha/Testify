#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;

namespace UniTestify
{
    /// <summary>ゲーム側の状態を安定したキー順序と文字列表現で収集します。</summary>
    internal static class UiSnapshotGameStateCollector
    {
        /// <summary>ゲーム状態の取得失敗を観測全体へ波及させず、キー順で返します。</summary>
        internal static List<UiSnapshotGameEntry> CollectGameEntries()
        {
            var entries = new List<UiSnapshotGameEntry>();
            var stateProvider = GameAdapterRegistry.StateProvider;
            if (stateProvider == null)
            {
                return entries;
            }

            IReadOnlyDictionary<string, object> state;
            try
            {
                state = stateProvider.GetState();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"[UiSnapshot] ゲーム状態の収集に失敗しました。 {exception.GetType().Name}: {exception.Message}");
                return entries;
            }

            if (state == null)
            {
                return entries;
            }

            var keys = new List<string>(state.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (var keyIndex = 0; keyIndex < keys.Count; keyIndex++)
            {
                var key = keys[keyIndex];
                entries.Add(new UiSnapshotGameEntry
                {
                    key = key ?? string.Empty,
                    value = FormatGameValue(state[key]),
                });
            }

            return entries;
        }

        private static string FormatGameValue(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            switch (value)
            {
                case string stringValue:
                    return stringValue;
                case bool boolValue:
                    return boolValue ? "true" : "false";
                case IFormattable formattable:
                    return formattable.ToString(null, CultureInfo.InvariantCulture);
                default:
                    return value.ToString();
            }
        }
    }
}
#endif
