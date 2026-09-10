#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using System.Text;

namespace UniTestify
{
    /// <summary>AI 向けテキストの表示順序・省略規則・表記を管理します。</summary>
    internal static class UiSnapshotCompactTextFormatter
    {
        private const string TextKind = "Text";
        private const int CompactTextCollapseThreshold = 5;
        private const int CompactTextExpandedHeadCount = 3;

        /// <summary>
        /// スナップショットを AI 向けの圧縮テキストへ変換します。
        /// 座標や内部詳細を省いてトークン効率を上げるためです。
        /// </summary>
        internal static string ToCompactText(UiSnapshotDocument document, string scope = null)
        {
            if (document == null)
            {
                return string.Empty;
            }

            document = UiObservationScope.Filter(document, scope);
            var lineBuilder = new StringBuilder();
            lineBuilder.Append("scene=");
            lineBuilder.Append(string.IsNullOrEmpty(document.activeScene) ? "-" : document.activeScene);
            lineBuilder.Append(" focus=");
            AppendFocusSummary(lineBuilder, document);
            lineBuilder.AppendLine();

            AppendCompactElements(lineBuilder, document.elements);

            if (document.game != null && document.game.Length > 0)
            {
                lineBuilder.Append("game:");
                for (var gameIndex = 0; gameIndex < document.game.Length; gameIndex++)
                {
                    var gameEntry = document.game[gameIndex];
                    if (gameEntry == null)
                    {
                        continue;
                    }

                    lineBuilder.Append(" ");
                    lineBuilder.Append(gameEntry.key);
                    lineBuilder.Append("=");
                    lineBuilder.Append(gameEntry.value);
                }
            }

            return lineBuilder.ToString().TrimEnd();
        }

        private static void AppendFocusSummary(StringBuilder lineBuilder, UiSnapshotDocument document)
        {
            if (string.IsNullOrEmpty(document.focusedPath) || document.elements == null)
            {
                lineBuilder.Append("-");
                return;
            }

            for (var elementIndex = 0; elementIndex < document.elements.Length; elementIndex++)
            {
                var element = document.elements[elementIndex];
                if (element == null || !string.Equals(element.path, document.focusedPath, StringComparison.Ordinal))
                {
                    continue;
                }

                lineBuilder.Append(GetCompactElementName(element));
                if (!string.IsNullOrEmpty(element.label))
                {
                    lineBuilder.Append("(");
                    lineBuilder.Append(element.label);
                    lineBuilder.Append(")");
                }

                return;
            }

            lineBuilder.Append(document.focusedPath);
        }

        private static void AppendCompactElements(StringBuilder lineBuilder, UiSnapshotElement[] elements)
        {
            if (elements == null)
            {
                return;
            }

            for (var elementIndex = 0; elementIndex < elements.Length; elementIndex++)
            {
                var element = elements[elementIndex];
                if (ShouldSkipCompactElement(element))
                {
                    continue;
                }

                var sequenceLength = CountCollapsibleSequenceLength(elements, elementIndex);
                if (sequenceLength < CompactTextCollapseThreshold)
                {
                    AppendCompactElementLine(lineBuilder, element);
                    continue;
                }

                var expandedCount = Math.Min(CompactTextExpandedHeadCount, sequenceLength);
                for (var expandedIndex = 0; expandedIndex < expandedCount; expandedIndex++)
                {
                    AppendCompactElementLine(lineBuilder, elements[elementIndex + expandedIndex]);
                }

                AppendCollapsedSequenceSummary(lineBuilder, element, sequenceLength - expandedCount);
                elementIndex += sequenceLength - 1;
            }
        }

        private static bool ShouldSkipCompactElement(UiSnapshotElement element)
        {
            if (element == null)
            {
                return true;
            }

            return element.kind == TextKind && string.IsNullOrWhiteSpace(element.label);
        }

        private static int CountCollapsibleSequenceLength(UiSnapshotElement[] elements, int startIndex)
        {
            var firstElement = elements[startIndex];
            if (firstElement == null)
            {
                return 0;
            }

            var firstKind = firstElement.kind ?? string.Empty;
            var firstParentPath = GetParentPath(firstElement.path);
            var count = 1;
            for (var elementIndex = startIndex + 1; elementIndex < elements.Length; elementIndex++)
            {
                var element = elements[elementIndex];
                if (ShouldSkipCompactElement(element))
                {
                    break;
                }

                if (firstElement.clipped || element.clipped)
                {
                    break;
                }

                if (!string.Equals(firstKind, element.kind ?? string.Empty, StringComparison.Ordinal))
                {
                    break;
                }

                if (!string.Equals(firstParentPath, GetParentPath(element.path), StringComparison.Ordinal))
                {
                    break;
                }

                count++;
            }

            return count;
        }

        private static void AppendCollapsedSequenceSummary(StringBuilder lineBuilder, UiSnapshotElement element, int collapsedCount)
        {
            if (collapsedCount <= 0)
            {
                return;
            }

            lineBuilder.Append("[");
            lineBuilder.Append(string.IsNullOrEmpty(element.kind) ? "-" : element.kind);
            lineBuilder.Append("] ");
            lineBuilder.Append(GetCompactParentName(element.path));
            lineBuilder.Append(" …他 ");
            lineBuilder.Append(collapsedCount.ToString(CultureInfo.InvariantCulture));
            lineBuilder.AppendLine(" 件");
        }

        private static void AppendCompactElementLine(StringBuilder lineBuilder, UiSnapshotElement element)
        {
            lineBuilder.Append("[");
            lineBuilder.Append(string.IsNullOrEmpty(element.kind) ? "-" : element.kind);
            lineBuilder.Append("] ");
            lineBuilder.Append(GetCompactElementName(element));

            if (!string.IsNullOrEmpty(element.label))
            {
                lineBuilder.Append(" 「");
                lineBuilder.Append(element.label);
                lineBuilder.Append("」");
            }

            if (!element.interactable && element.kind != TextKind)
            {
                lineBuilder.Append(" !disabled");
            }

            if (!string.IsNullOrEmpty(element.blockedBy))
            {
                lineBuilder.Append(" blocked:");
                lineBuilder.Append(element.blockedBy);
            }

            if (element.focused)
            {
                lineBuilder.Append(" *focused");
            }

            if (!string.IsNullOrEmpty(element.value))
            {
                lineBuilder.Append(" value:");
                lineBuilder.Append(element.value);
            }

            if (element.clipped)
            {
                lineBuilder.Append(" [clipped]");
            }

            lineBuilder.AppendLine();
        }

        private static string GetParentPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            var separatorIndex = path.LastIndexOf('/');
            if (separatorIndex <= 0)
            {
                return string.Empty;
            }

            return path.Substring(0, separatorIndex);
        }

        private static string GetCompactElementName(UiSnapshotElement element)
        {
            if (element == null)
            {
                return "-";
            }

            if (string.IsNullOrEmpty(element.path))
            {
                return element.name ?? string.Empty;
            }

            var pathSegments = element.path.Split('/');
            if (pathSegments.Length >= 2)
            {
                return $"{pathSegments[pathSegments.Length - 2]}/{pathSegments[pathSegments.Length - 1]}";
            }

            return element.name ?? element.path;
        }

        private static string GetCompactParentName(string path)
        {
            var parentPath = GetParentPath(path);
            if (string.IsNullOrEmpty(parentPath))
            {
                return "-";
            }

            var pathSegments = parentPath.Split('/');
            if (pathSegments.Length >= 2)
            {
                return $"{pathSegments[pathSegments.Length - 2]}/{pathSegments[pathSegments.Length - 1]}";
            }

            return parentPath;
        }
    }
}
#endif
