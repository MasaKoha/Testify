#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;

namespace UniTestify
{
    /// <summary>観測要素の変化とシーン・フォーカスの変化を比較します。</summary>
    internal static class UiSnapshotComparer
    {
        /// <summary>
        /// 2 つのスナップショット差分を返します。
        /// 操作結果が空振りかどうかを要素単位で即判定できるようにします。
        /// </summary>
        internal static UiSnapshotDiff Compare(UiSnapshotDocument before, UiSnapshotDocument after)
        {
            var beforeMap = BuildElementMap(before == null ? null : before.elements);
            var afterMap = BuildElementMap(after == null ? null : after.elements);
            var addedPaths = new List<string>();
            var removedPaths = new List<string>();
            var changedEntries = new List<UiSnapshotChange>();

            foreach (var beforePair in beforeMap)
            {
                if (!afterMap.ContainsKey(beforePair.Key))
                {
                    removedPaths.Add(beforePair.Key);
                }
            }

            foreach (var afterPair in afterMap)
            {
                if (!beforeMap.TryGetValue(afterPair.Key, out var beforeElement))
                {
                    addedPaths.Add(afterPair.Key);
                    continue;
                }

                AppendChangedField(changedEntries, afterPair.Key, "name", beforeElement.name, afterPair.Value.name);
                AppendChangedField(changedEntries, afterPair.Key, "kind", beforeElement.kind, afterPair.Value.kind);
                AppendChangedField(changedEntries, afterPair.Key, "label", beforeElement.label, afterPair.Value.label);
                AppendChangedField(changedEntries, afterPair.Key, "rect", FormatRect(beforeElement.rect), FormatRect(afterPair.Value.rect));
                AppendChangedField(changedEntries, afterPair.Key, "interactable", FormatBoolean(beforeElement.interactable), FormatBoolean(afterPair.Value.interactable));
                AppendChangedField(changedEntries, afterPair.Key, "blockedBy", beforeElement.blockedBy, afterPair.Value.blockedBy);
                AppendChangedField(changedEntries, afterPair.Key, "focused", FormatBoolean(beforeElement.focused), FormatBoolean(afterPair.Value.focused));
                AppendChangedField(changedEntries, afterPair.Key, "value", beforeElement.value, afterPair.Value.value);
                AppendChangedField(changedEntries, afterPair.Key, "clipped", FormatBoolean(beforeElement.clipped), FormatBoolean(afterPair.Value.clipped));
                AppendChangedField(changedEntries, afterPair.Key, "offscreen", FormatBoolean(beforeElement.offscreen), FormatBoolean(afterPair.Value.offscreen));
            }

            addedPaths.Sort(StringComparer.Ordinal);
            removedPaths.Sort(StringComparer.Ordinal);
            changedEntries.Sort((left, right) =>
            {
                var pathComparison = string.Compare(left.path, right.path, StringComparison.Ordinal);
                if (pathComparison != 0)
                {
                    return pathComparison;
                }

                return string.Compare(left.field, right.field, StringComparison.Ordinal);
            });

            var diff = new UiSnapshotDiff
            {
                addedPaths = addedPaths.ToArray(),
                removedPaths = removedPaths.ToArray(),
                changed = changedEntries.ToArray(),
                focusedBefore = before == null ? string.Empty : before.focusedPath,
                focusedAfter = after == null ? string.Empty : after.focusedPath,
                sceneBefore = before == null ? string.Empty : before.activeScene,
                sceneAfter = after == null ? string.Empty : after.activeScene,
            };
            diff.isEmpty =
                diff.addedPaths.Length == 0 &&
                diff.removedPaths.Length == 0 &&
                diff.changed.Length == 0 &&
                string.Equals(diff.focusedBefore, diff.focusedAfter, StringComparison.Ordinal) &&
                string.Equals(diff.sceneBefore, diff.sceneAfter, StringComparison.Ordinal);
            return diff;
        }

        private static string FormatBoolean(bool value)
        {
            return value ? "true" : "false";
        }

        private static Dictionary<string, UiSnapshotElement> BuildElementMap(UiSnapshotElement[] elements)
        {
            var map = new Dictionary<string, UiSnapshotElement>(StringComparer.Ordinal);
            if (elements == null)
            {
                return map;
            }

            for (var elementIndex = 0; elementIndex < elements.Length; elementIndex++)
            {
                var element = elements[elementIndex];
                if (element == null || string.IsNullOrEmpty(element.path))
                {
                    continue;
                }

                map[element.path] = element;
            }

            return map;
        }

        private static void AppendChangedField(List<UiSnapshotChange> changedEntries, string path, string fieldName, string beforeValue, string afterValue)
        {
            if (string.Equals(beforeValue ?? string.Empty, afterValue ?? string.Empty, StringComparison.Ordinal))
            {
                return;
            }

            changedEntries.Add(new UiSnapshotChange
            {
                path = path ?? string.Empty,
                field = fieldName ?? string.Empty,
                before = beforeValue ?? string.Empty,
                after = afterValue ?? string.Empty,
            });
        }

        private static string FormatRect(float[] rectValues)
        {
            if (rectValues == null || rectValues.Length < 4)
            {
                return string.Empty;
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:0.###},{1:0.###},{2:0.###},{3:0.###}",
                rectValues[0],
                rectValues[1],
                rectValues[2],
                rectValues[3]);
        }
    }
}
#endif
